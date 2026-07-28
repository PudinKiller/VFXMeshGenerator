using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    /// <summary>
    /// The complete result of generating a preview mesh. The mesh remains readable so the
    /// editor preview and the asset writer can safely inspect and copy it.
    /// </summary>
    public sealed class VFXMeshBuildResult
    {
        public Mesh mesh;
        public VFXMeshDraft draft;
        public VFXMeshValidationFlags validationFlags;
        public int vertexCount;
        public int triangleCount;
        public Bounds bounds;
        public string warning;
        public string error;
        public VFXMeshOutputSettings outputSettings;

        public bool succeeded
        {
            get { return mesh != null && string.IsNullOrEmpty(error); }
        }
    }

    /// <summary>
    /// Runs the editor generation pipeline and converts its draft into a Unity Mesh.
    /// </summary>
    public static class VFXMeshBuilder
    {
        private const int MaximumUInt16VertexCount = 65535;

        public static VFXMeshBuildResult Build(VFXMeshRecipe recipe)
        {
            var result = new VFXMeshBuildResult
            {
                draft = new VFXMeshDraft(),
                validationFlags = VFXMeshValidationFlags.Empty
            };

            if (recipe == null)
            {
                result.error = "Cannot build a VFX mesh without a recipe.";
                return result;
            }

            result.outputSettings = CopyOutputSettings(recipe.output);
            var warnings = new List<string>();
            if (!VFXMeshBuildLimits.TryValidate(recipe, out _, out result.error))
            {
                return result;
            }

            try
            {
                VFXShapeGenerator.Generate(recipe, result.draft);
                VFXModifierProcessor.Apply(recipe, result.draft);
                VFXVertexDataProcessor.Apply(recipe, result.draft);

                result.validationFlags = result.draft.Validate();
                if (result.draft.triangles.Count % 3 != 0)
                {
                    result.error = "The generated index buffer does not contain complete triangles.";
                    PopulateStatistics(result);
                    return result;
                }

                if (HasFatalValidationError(result.validationFlags))
                {
                    result.error = DescribeFatalValidationError(result.validationFlags);
                    PopulateStatistics(result);
                    return result;
                }

                NormalizeVertexChannels(result.draft, warnings);
                ApplyOutputTopology(result.draft, result.outputSettings);

                result.validationFlags = result.draft.Validate();
                if (HasFatalValidationError(result.validationFlags))
                {
                    result.error = DescribeFatalValidationError(result.validationFlags);
                    PopulateStatistics(result);
                    return result;
                }

                if ((result.validationFlags & VFXMeshValidationFlags.DegenerateTriangle) != 0)
                {
                    warnings.Add("The generated mesh contains one or more degenerate triangles.");
                }

                result.mesh = CreateMesh(recipe, result.draft, result.outputSettings, warnings);
                PopulateStatistics(result);
                result.warning = JoinMessages(warnings);
                return result;
            }
            catch (Exception exception)
            {
                if (result.mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.mesh);
                    result.mesh = null;
                }

                PopulateStatistics(result);
                result.warning = JoinMessages(warnings);
                result.error = "Mesh generation failed: " + exception.Message;
                return result;
            }
        }

        private static Mesh CreateMesh(
            VFXMeshRecipe recipe,
            VFXMeshDraft draft,
            VFXMeshOutputSettings output,
            List<string> warnings)
        {
            var indexFormat = ResolveIndexFormat(output.indexFormat, draft.vertices.Count);
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(recipe.meshName) ? "VFXMesh" : recipe.meshName.Trim(),
                indexFormat = indexFormat
            };

            try
            {
                mesh.SetVertices(draft.vertices);
                mesh.SetUVs(0, draft.uv0);

                if (draft.uv1.Count == draft.vertices.Count)
                {
                    mesh.SetUVs(1, draft.uv1);
                }

                if (draft.uv2.Count == draft.vertices.Count)
                {
                    mesh.SetUVs(2, draft.uv2);
                }

                if (draft.uv3.Count == draft.vertices.Count)
                {
                    mesh.SetUVs(3, draft.uv3);
                }

                if (draft.colors.Count == draft.vertices.Count)
                {
                    mesh.SetColors(draft.colors);
                }

                mesh.SetTriangles(draft.triangles, 0, false);
                mesh.RecalculateNormals();
                SmoothShapeDefaultSphericalPoleNormals(recipe, mesh, output);
                SmoothClosedRingSeamNormals(recipe, mesh, output);

                if (output.generateTangents)
                {
                    try
                    {
                        mesh.RecalculateTangents();
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Tangents could not be generated: " + exception.Message);
                    }
                }

                mesh.RecalculateBounds();
                var padding = output.boundsPadding;
                if (padding < 0f)
                {
                    padding = 0f;
                    warnings.Add("Negative bounds padding was clamped to zero.");
                }

                if (padding > 0f)
                {
                    var paddedBounds = mesh.bounds;
                    paddedBounds.Expand(Vector3.one * (padding * 2f));
                    mesh.bounds = paddedBounds;
                }

                return mesh;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static void SmoothShapeDefaultSphericalPoleNormals(
            VFXMeshRecipe recipe,
            Mesh mesh,
            VFXMeshOutputSettings output)
        {
            var projection = recipe.uv?.projection ?? VFXUVProjection.ShapeDefault;
            var isSphere = recipe.shapeType == VFXMeshShapeType.Sphere;
            var isHemisphere = recipe.shapeType == VFXMeshShapeType.Hemisphere;
            if (output.flatShading ||
                projection != VFXUVProjection.ShapeDefault ||
                (!isSphere && !isHemisphere) ||
                recipe.shape == null)
            {
                return;
            }

            var normals = mesh.normals;
            if (normals.Length == 0)
            {
                return;
            }

            var longitudeSegments = Mathf.Clamp(
                recipe.shape.longitudeSegments,
                3,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var latitudeSegments = Mathf.Clamp(
                recipe.shape.latitudeSegments,
                isSphere ? 2 : 1,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var topPoleIndex = isSphere
                ? 1 + (latitudeSegments - 1) * (longitudeSegments + 1)
                : latitudeSegments * (longitudeSegments + 1);
            var baseVertexCount = isSphere
                ? topPoleIndex + 1
                : topPoleIndex +
                  1 +
                  (recipe.shape.capEnd ? longitudeSegments + 2 : 0);
            var sideVertexCount = output.doubleSided
                ? normals.Length / 2
                : normals.Length;

            if (isSphere)
            {
                SmoothNormalIndexGroup(
                    normals,
                    0,
                    baseVertexCount,
                    2,
                    longitudeSegments - 1,
                    0,
                    sideVertexCount);
            }

            SmoothNormalIndexGroup(
                normals,
                topPoleIndex,
                baseVertexCount + (isSphere ? 1 : 0),
                isSphere ? 2 : 1,
                longitudeSegments - 1,
                0,
                sideVertexCount);

            if (output.doubleSided)
            {
                if (isSphere)
                {
                    SmoothNormalIndexGroup(
                        normals,
                        sideVertexCount,
                        sideVertexCount + baseVertexCount,
                        2,
                        longitudeSegments - 1,
                        sideVertexCount,
                        normals.Length);
                }

                SmoothNormalIndexGroup(
                    normals,
                    sideVertexCount + topPoleIndex,
                    sideVertexCount +
                    baseVertexCount +
                    (isSphere ? 1 : 0),
                    isSphere ? 2 : 1,
                    longitudeSegments - 1,
                    sideVertexCount,
                    normals.Length);
            }

            mesh.normals = normals;
        }

        private static void SmoothNormalIndexGroup(
            IList<Vector3> normals,
            int referenceIndex,
            int firstDuplicateIndex,
            int duplicateStride,
            int duplicateCount,
            int start,
            int end)
        {
            if (referenceIndex < start ||
                referenceIndex >= end ||
                referenceIndex >= normals.Count ||
                duplicateStride < 1 ||
                duplicateCount < 1 ||
                end > normals.Count)
            {
                return;
            }

            var sum = normals[referenceIndex];
            var validDuplicateCount = 0;
            for (var duplicate = 0; duplicate < duplicateCount; duplicate++)
            {
                var index = firstDuplicateIndex + duplicate * duplicateStride;
                if (index < start || index >= end || index >= normals.Count)
                {
                    continue;
                }

                sum += normals[index];
                validDuplicateCount++;
            }

            if (validDuplicateCount == 0 || sum.sqrMagnitude <= 1e-12f)
            {
                return;
            }

            var smoothed = sum.normalized;
            normals[referenceIndex] = smoothed;
            for (var duplicate = 0; duplicate < duplicateCount; duplicate++)
            {
                var index = firstDuplicateIndex + duplicate * duplicateStride;
                if (index >= start && index < end && index < normals.Count)
                {
                    normals[index] = smoothed;
                }
            }
        }

        private static void SmoothClosedRingSeamNormals(
            VFXMeshRecipe recipe,
            Mesh mesh,
            VFXMeshOutputSettings output)
        {
            var isRing = recipe.shapeType == VFXMeshShapeType.Ring;
            var isClosedArc =
                recipe.shapeType == VFXMeshShapeType.Arc &&
                recipe.shape != null &&
                Mathf.Abs(
                    Mathf.Abs(VFXShapeGenerator.SanitizeArcDegrees(recipe.shape.arcDegrees)) -
                    360f) <= 0.001f;
            if (output.flatShading || (!isRing && !isClosedArc) || recipe.shape == null)
            {
                return;
            }

            var normals = mesh.normals;
            var vertices = mesh.vertices;
            if (normals.Length != vertices.Length || normals.Length == 0)
            {
                return;
            }

            var sideVertexCount = output.doubleSided ? vertices.Length / 2 : vertices.Length;
            var variableArcTopology =
                isClosedArc &&
                VFXShapeGenerator.UsesVariableArcWidthTopology(recipe.shape);
            var mirrorsArc =
                isClosedArc &&
                recipe.shape.mirrorArcAcrossShapePlane;
            var mirroredShellOffset = mirrorsArc
                ? GetArcShellVertexCount(recipe.shape, variableArcTopology)
                : 0;
            SmoothClosedRadialSide(
                recipe.shape,
                vertices,
                normals,
                0,
                sideVertexCount,
                variableArcTopology);
            if (mirrorsArc)
            {
                SmoothClosedRadialSide(
                    recipe.shape,
                    vertices,
                    normals,
                    mirroredShellOffset,
                    sideVertexCount,
                    variableArcTopology);
            }

            if (output.doubleSided)
            {
                SmoothClosedRadialSide(
                    recipe.shape,
                    vertices,
                    normals,
                    sideVertexCount,
                    vertices.Length,
                    variableArcTopology);
                if (mirrorsArc)
                {
                    SmoothClosedRadialSide(
                        recipe.shape,
                        vertices,
                        normals,
                        sideVertexCount + mirroredShellOffset,
                        vertices.Length,
                        variableArcTopology);
                }
            }

            mesh.normals = normals;
        }

        private static int GetArcShellVertexCount(
            VFXShapeSettings shape,
            bool variableArcTopology)
        {
            var angularSegments = Mathf.Clamp(
                shape.radialSegments,
                3,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var radialSegments = Mathf.Clamp(
                shape.widthSegments,
                1,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            if (!variableArcTopology)
            {
                var innerRadius = shape.innerRadius;
                var usesDiscTopology =
                    float.IsNaN(innerRadius) ||
                    float.IsInfinity(innerRadius) ||
                    Mathf.Abs(innerRadius) < 0.0001f;
                return usesDiscTopology
                    ? 1 + radialSegments * (angularSegments + 1)
                    : (radialSegments + 1) * (angularSegments + 1);
            }

            var radialVertexCount = radialSegments + 1;
            var vertexCount = 0;
            for (var segment = 0; segment <= angularSegments; segment++)
            {
                var angularT = segment / (float)angularSegments;
                vertexCount +=
                    VFXShapeGenerator.EvaluateArcWidth(shape, angularT) <= 0.0001f
                        ? 1
                        : radialVertexCount;
            }

            return vertexCount;
        }

        private static void SmoothClosedRadialSide(
            VFXShapeSettings shape,
            Vector3[] vertices,
            Vector3[] normals,
            int vertexOffset,
            int sideEnd,
            bool variableArcTopology)
        {
            if (variableArcTopology)
            {
                SmoothVariableArcSide(shape, vertices, normals, vertexOffset, sideEnd);
                return;
            }

            SmoothRegularRadialSide(shape, vertices, normals, vertexOffset, sideEnd);
        }

        private static void SmoothRegularRadialSide(
            VFXShapeSettings shape,
            Vector3[] vertices,
            Vector3[] normals,
            int vertexOffset,
            int sideEnd)
        {
            var radialSegments = Mathf.Clamp(
                shape.radialSegments,
                3,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var radiusSegments = Mathf.Clamp(
                shape.widthSegments,
                1,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var stride = radialSegments + 1;
            var innerRadius = shape.innerRadius;
            var usesDiscTopology =
                float.IsNaN(innerRadius) ||
                float.IsInfinity(innerRadius) ||
                Mathf.Abs(innerRadius) < 0.0001f;

            var rowCount = usesDiscTopology ? radiusSegments : radiusSegments + 1;
            var firstRow = usesDiscTopology ? vertexOffset + 1 : vertexOffset;
            for (var row = 0; row < rowCount; row++)
            {
                var first = firstRow + row * stride;
                var last = first + radialSegments;
                SmoothNormalPair(vertices, normals, first, last, vertexOffset, sideEnd);
            }
        }

        private static void SmoothVariableArcSide(
            VFXShapeSettings shape,
            Vector3[] vertices,
            Vector3[] normals,
            int vertexOffset,
            int sideEnd)
        {
            var angularSegments = Mathf.Clamp(
                shape.radialSegments,
                3,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var radialVertexCount =
                Mathf.Clamp(
                    shape.widthSegments,
                    1,
                    VFXMeshBuildLimits.MaximumSegmentsPerAxis) +
                1;
            var firstCollapsed =
                VFXShapeGenerator.EvaluateArcWidth(shape, 0f) <= 0.0001f;
            var lastCollapsed =
                VFXShapeGenerator.EvaluateArcWidth(shape, 1f) <= 0.0001f;
            if (firstCollapsed != lastCollapsed)
            {
                return;
            }

            var lastColumn = vertexOffset;
            for (var segment = 0; segment < angularSegments; segment++)
            {
                var angularT = segment / (float)angularSegments;
                lastColumn +=
                    VFXShapeGenerator.EvaluateArcWidth(shape, angularT) <= 0.0001f
                        ? 1
                        : radialVertexCount;
            }

            var pairCount = firstCollapsed ? 1 : radialVertexCount;
            for (var row = 0; row < pairCount; row++)
            {
                SmoothNormalPair(
                    vertices,
                    normals,
                    vertexOffset + row,
                    lastColumn + row,
                    vertexOffset,
                    sideEnd);
            }
        }

        private static void SmoothNormalPair(
            Vector3[] vertices,
            Vector3[] normals,
            int first,
            int second,
            int sideStart,
            int sideEnd)
        {
            if (first < sideStart || second >= sideEnd)
            {
                return;
            }

            var positionTolerance = Mathf.Max(
                1e-5f,
                Mathf.Max(vertices[first].magnitude, vertices[second].magnitude) * 1e-5f);
            if ((vertices[first] - vertices[second]).sqrMagnitude >
                positionTolerance * positionTolerance)
            {
                return;
            }

            var sum = normals[first] + normals[second];
            if (sum.sqrMagnitude <= 1e-12f)
            {
                return;
            }

            var smoothed = sum.normalized;
            normals[first] = smoothed;
            normals[second] = smoothed;
        }

        private static IndexFormat ResolveIndexFormat(VFXIndexFormatMode mode, int vertexCount)
        {
            switch (mode)
            {
                case VFXIndexFormatMode.UInt16:
                    if (vertexCount > MaximumUInt16VertexCount)
                    {
                        throw new InvalidOperationException(
                            "The mesh contains " + vertexCount +
                            " vertices, which exceeds the forced 16-bit index limit. Use Auto or UInt32.");
                    }

                    return IndexFormat.UInt16;

                case VFXIndexFormatMode.UInt32:
                    return IndexFormat.UInt32;

                default:
                    return vertexCount > MaximumUInt16VertexCount
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16;
            }
        }

        private static void ApplyOutputTopology(VFXMeshDraft draft, VFXMeshOutputSettings output)
        {
            if (output.flatShading)
            {
                ConvertToFlatShadedTopology(draft);
            }

            if (output.flipWinding)
            {
                ReverseTriangleWinding(draft.triangles);
            }

            if (output.doubleSided)
            {
                AppendBackFaces(draft);
            }
        }

        private static void ConvertToFlatShadedTopology(VFXMeshDraft draft)
        {
            var sourceVertices = draft.vertices.ToArray();
            var sourceTriangles = draft.triangles.ToArray();
            var sourceUV0 = draft.uv0.ToArray();
            var sourceUV1 = draft.uv1.ToArray();
            var sourceUV2 = draft.uv2.ToArray();
            var sourceUV3 = draft.uv3.ToArray();
            var sourceColors = draft.colors.ToArray();

            var hasUV1 = sourceUV1.Length == sourceVertices.Length;
            var hasUV2 = sourceUV2.Length == sourceVertices.Length;
            var hasUV3 = sourceUV3.Length == sourceVertices.Length;
            var hasColors = sourceColors.Length == sourceVertices.Length;

            draft.vertices.Clear();
            draft.triangles.Clear();
            draft.uv0.Clear();
            draft.uv1.Clear();
            draft.uv2.Clear();
            draft.uv3.Clear();
            draft.colors.Clear();

            for (var i = 0; i < sourceTriangles.Length; i++)
            {
                var sourceIndex = sourceTriangles[i];
                var destinationIndex = draft.vertices.Count;

                draft.vertices.Add(sourceVertices[sourceIndex]);
                draft.uv0.Add(sourceUV0[sourceIndex]);
                if (hasUV1)
                {
                    draft.uv1.Add(sourceUV1[sourceIndex]);
                }

                if (hasUV2)
                {
                    draft.uv2.Add(sourceUV2[sourceIndex]);
                }

                if (hasUV3)
                {
                    draft.uv3.Add(sourceUV3[sourceIndex]);
                }

                if (hasColors)
                {
                    draft.colors.Add(sourceColors[sourceIndex]);
                }

                draft.triangles.Add(destinationIndex);
            }
        }

        private static void ReverseTriangleWinding(List<int> triangles)
        {
            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                var temporary = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temporary;
            }
        }

        private static void AppendBackFaces(VFXMeshDraft draft)
        {
            var vertexOffset = draft.vertices.Count;
            var sourceTriangleCount = draft.triangles.Count;

            DuplicateListContents(draft.vertices);
            DuplicateListContents(draft.uv0);
            DuplicateListContents(draft.uv1);
            DuplicateListContents(draft.uv2);
            DuplicateListContents(draft.uv3);
            DuplicateListContents(draft.colors);

            for (var i = 0; i + 2 < sourceTriangleCount; i += 3)
            {
                draft.triangles.Add(draft.triangles[i + 2] + vertexOffset);
                draft.triangles.Add(draft.triangles[i + 1] + vertexOffset);
                draft.triangles.Add(draft.triangles[i] + vertexOffset);
            }
        }

        private static void DuplicateListContents<T>(List<T> values)
        {
            var originalCount = values.Count;
            if (originalCount == 0)
            {
                return;
            }

            values.Capacity = Math.Max(values.Capacity, originalCount * 2);
            for (var i = 0; i < originalCount; i++)
            {
                values.Add(values[i]);
            }
        }

        private static void NormalizeVertexChannels(VFXMeshDraft draft, List<string> warnings)
        {
            NormalizeRequiredChannel(draft.uv0, draft.vertices.Count, Vector2.zero, "UV0", warnings);
            NormalizeOptionalChannel(draft.uv1, draft.vertices.Count, Vector4.zero, "UV1", warnings);
            NormalizeOptionalChannel(draft.uv2, draft.vertices.Count, Vector4.zero, "UV2", warnings);
            NormalizeOptionalChannel(draft.uv3, draft.vertices.Count, Vector4.zero, "UV3", warnings);
            NormalizeOptionalChannel(draft.colors, draft.vertices.Count, Color.white, "vertex colors", warnings);
        }

        private static void NormalizeRequiredChannel<T>(
            List<T> values,
            int vertexCount,
            T defaultValue,
            string channelName,
            List<string> warnings)
        {
            if (values.Count != vertexCount)
            {
                warnings.Add(channelName + " data did not match the vertex count and was padded with defaults.");
                ResizeList(values, vertexCount, defaultValue);
            }
        }

        private static void NormalizeOptionalChannel<T>(
            List<T> values,
            int vertexCount,
            T defaultValue,
            string channelName,
            List<string> warnings)
        {
            if (values.Count == 0 || values.Count == vertexCount)
            {
                return;
            }

            warnings.Add(channelName + " data did not match the vertex count and was padded with defaults.");
            ResizeList(values, vertexCount, defaultValue);
        }

        private static void ResizeList<T>(List<T> values, int requiredCount, T defaultValue)
        {
            if (values.Count > requiredCount)
            {
                values.RemoveRange(requiredCount, values.Count - requiredCount);
            }

            while (values.Count < requiredCount)
            {
                values.Add(defaultValue);
            }
        }

        private static bool HasFatalValidationError(VFXMeshValidationFlags flags)
        {
            const VFXMeshValidationFlags fatalFlags =
                VFXMeshValidationFlags.Empty |
                VFXMeshValidationFlags.InvalidIndex |
                VFXMeshValidationFlags.NonFiniteVertex;
            return (flags & fatalFlags) != 0;
        }

        private static string DescribeFatalValidationError(VFXMeshValidationFlags flags)
        {
            var messages = new List<string>();
            if ((flags & VFXMeshValidationFlags.Empty) != 0)
            {
                messages.Add("the generator produced no usable geometry");
            }

            if ((flags & VFXMeshValidationFlags.InvalidIndex) != 0)
            {
                messages.Add("one or more triangle indices are outside the vertex buffer");
            }

            if ((flags & VFXMeshValidationFlags.NonFiniteVertex) != 0)
            {
                messages.Add("one or more vertices contain NaN or infinity");
            }

            return "Mesh validation failed: " + string.Join("; ", messages) + ".";
        }

        private static void PopulateStatistics(VFXMeshBuildResult result)
        {
            if (result.draft != null)
            {
                result.vertexCount = result.draft.vertices.Count;
                result.triangleCount = result.draft.triangles.Count / 3;
                result.bounds = result.mesh != null ? result.mesh.bounds : result.draft.CalculateBounds();
            }
        }

        private static string JoinMessages(List<string> messages)
        {
            return messages.Count == 0 ? null : string.Join(Environment.NewLine, messages);
        }

        private static VFXMeshOutputSettings CopyOutputSettings(VFXMeshOutputSettings source)
        {
            if (source == null)
            {
                return new VFXMeshOutputSettings();
            }

            return new VFXMeshOutputSettings
            {
                flipWinding = source.flipWinding,
                doubleSided = source.doubleSided,
                flatShading = source.flatShading,
                generateTangents = source.generateTangents,
                readWriteEnabled = source.readWriteEnabled,
                optimizeMesh = source.optimizeMesh,
                boundsPadding = source.boundsPadding,
                compression = source.compression,
                indexFormat = source.indexFormat
            };
        }
    }
}
