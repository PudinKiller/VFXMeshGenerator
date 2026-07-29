using System;
using System.Collections.Generic;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor
{
    internal static class VFXUVProcessor
    {
        internal static void Apply(VFXMeshRecipe recipe, VFXMeshDraft draft)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            var vertexCount = draft.vertices.Count;
            VFXProcessingUtility.ResizeList(draft.uv0, vertexCount, Vector2.zero);
            if (vertexCount == 0)
            {
                return;
            }

            var settings = recipe.uv ?? new VFXUVSettings();
            if (settings.projection != VFXUVProjection.ShapeDefault)
            {
                var mirrorsArc =
                    recipe.shapeType == VFXMeshShapeType.Arc &&
                    recipe.shape?.mirrorArcAcrossShapePlane == true;
                var projectionVertexCount = mirrorsArc
                    ? vertexCount / 2
                    : vertexCount;
                var radialRowCoordinates =
                    settings.projection == VFXUVProjection.Radial
                        ? CaptureRadialRowCoordinates(
                            recipe,
                            draft,
                            projectionVertexCount)
                        : null;
                Project(
                    recipe,
                    draft,
                    settings.projection,
                    projectionVertexCount);
                RestoreRadialRowCoordinates(
                    draft,
                    radialRowCoordinates);
                if (mirrorsArc)
                {
                    CopyMirroredArcUVs(draft, projectionVertexCount);
                }

                if (IsAngularProjection(settings.projection))
                {
                    SplitAngularSeams(draft);
                    SplitAngularPoles(recipe, draft);
                }
            }
            else if (recipe.shapeType == VFXMeshShapeType.Sphere ||
                     recipe.shapeType == VFXMeshShapeType.Hemisphere)
            {
                SplitShapeDefaultSphericalPoles(recipe, draft);
            }

            vertexCount = draft.vertices.Count;
            for (var i = 0; i < vertexCount; i++)
            {
                draft.uv0[i] = Transform(draft.uv0[i], settings);
            }
        }

        private static float[] CaptureRadialRowCoordinates(
            VFXMeshRecipe recipe,
            VFXMeshDraft draft,
            int vertexCount)
        {
            if (recipe == null ||
                (recipe.shapeType != VFXMeshShapeType.Disc &&
                 recipe.shapeType != VFXMeshShapeType.Ring &&
                 recipe.shapeType != VFXMeshShapeType.Arc) ||
                draft == null ||
                vertexCount <= 0 ||
                draft.uv0.Count < vertexCount)
            {
                return null;
            }

            var usesDiscTopology = UsesDiscTopology(recipe);
            var coordinates = new float[vertexCount];
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                // Shape-default UVs retain the pre-deformation row coordinate.
                // Radial projection reuses it for V while Project still derives U
                // from the final mesh.
                var shapeDefaultUV = draft.uv0[vertex];
                coordinates[vertex] = usesDiscTopology
                    ? Mathf.Clamp01(
                        Vector2.Distance(
                            shapeDefaultUV,
                            new Vector2(0.5f, 0.5f)) *
                        2f)
                    : Mathf.Clamp01(shapeDefaultUV.y);
            }

            return coordinates;
        }

        private static void RestoreRadialRowCoordinates(
            VFXMeshDraft draft,
            IReadOnlyList<float> radialRowCoordinates)
        {
            if (draft == null || radialRowCoordinates == null)
            {
                return;
            }

            var count = Mathf.Min(
                draft.uv0.Count,
                radialRowCoordinates.Count);
            for (var vertex = 0; vertex < count; vertex++)
            {
                var uv = draft.uv0[vertex];
                uv.y = radialRowCoordinates[vertex];
                draft.uv0[vertex] = uv;
            }
        }

        private static bool UsesDiscTopology(VFXMeshRecipe recipe)
        {
            if (recipe?.shapeType == VFXMeshShapeType.Disc)
            {
                return true;
            }

            if (recipe?.shape == null ||
                (recipe.shapeType != VFXMeshShapeType.Ring &&
                 recipe.shapeType != VFXMeshShapeType.Arc))
            {
                return false;
            }

            var innerRadius = recipe.shape.innerRadius;
            var zeroInnerRadius =
                float.IsNaN(innerRadius) ||
                float.IsInfinity(innerRadius) ||
                Mathf.Abs(innerRadius) < 0.0001f;
            return zeroInnerRadius &&
                   (recipe.shapeType != VFXMeshShapeType.Arc ||
                    !VFXShapeGenerator.UsesVariableArcWidthTopology(
                        recipe.shape));
        }

        private static bool IsAngularProjection(VFXUVProjection projection)
        {
            return projection == VFXUVProjection.Radial ||
                   projection == VFXUVProjection.Cylindrical ||
                   projection == VFXUVProjection.Spherical;
        }

        private static void SplitAngularSeams(VFXMeshDraft draft)
        {
            var duplicates = new Dictionary<int, int>();
            for (var triangle = 0; triangle + 2 < draft.triangles.Count; triangle += 3)
            {
                var first = draft.triangles[triangle];
                var second = draft.triangles[triangle + 1];
                var third = draft.triangles[triangle + 2];
                var minimum = Mathf.Min(draft.uv0[first].x, Mathf.Min(draft.uv0[second].x, draft.uv0[third].x));
                var maximum = Mathf.Max(draft.uv0[first].x, Mathf.Max(draft.uv0[second].x, draft.uv0[third].x));
                if (maximum - minimum <= 0.5f + VFXProcessingUtility.Epsilon)
                {
                    continue;
                }

                for (var corner = 0; corner < 3; corner++)
                {
                    var triangleIndex = triangle + corner;
                    var sourceIndex = draft.triangles[triangleIndex];
                    if (draft.uv0[sourceIndex].x >= 0.5f)
                    {
                        continue;
                    }

                    if (!duplicates.TryGetValue(sourceIndex, out var duplicateIndex))
                    {
                        duplicateIndex = DuplicateVertexWithWrappedU(draft, sourceIndex);
                        duplicates.Add(sourceIndex, duplicateIndex);
                    }

                    draft.triangles[triangleIndex] = duplicateIndex;
                }
            }
        }

        private static int DuplicateVertexWithWrappedU(VFXMeshDraft draft, int sourceIndex)
        {
            var uv = draft.uv0[sourceIndex];
            uv.x += 1f;
            return DuplicateVertexWithUV(draft, sourceIndex, uv);
        }

        private static int DuplicateVertexWithUV(
            VFXMeshDraft draft,
            int sourceIndex,
            Vector2 uv)
        {
            var previousVertexCount = draft.vertices.Count;
            var duplicateIndex = previousVertexCount;
            draft.vertices.Add(draft.vertices[sourceIndex]);
            draft.uv0.Add(uv);
            DuplicateOptionalChannel(draft.uv1, previousVertexCount, sourceIndex);
            DuplicateOptionalChannel(draft.uv2, previousVertexCount, sourceIndex);
            DuplicateOptionalChannel(draft.uv3, previousVertexCount, sourceIndex);
            DuplicateOptionalChannel(draft.colors, previousVertexCount, sourceIndex);
            draft.uvNormalWeldPairs.Add(
                new Vector2Int(sourceIndex, duplicateIndex));
            return duplicateIndex;
        }

        private static void SplitShapeDefaultSphericalPoles(
            VFXMeshRecipe recipe,
            VFXMeshDraft draft)
        {
            var shape = recipe.shape ?? new VFXShapeSettings();
            var longitudeSegments = Mathf.Clamp(
                shape.longitudeSegments,
                3,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var isSphere = recipe.shapeType == VFXMeshShapeType.Sphere;
            var latitudeSegments = Mathf.Clamp(
                shape.latitudeSegments,
                isSphere ? 2 : 1,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
            var topPoleIndex = isSphere
                ? 1 + (latitudeSegments - 1) * (longitudeSegments + 1)
                : latitudeSegments * (longitudeSegments + 1);
            var bottomPoleIndex = isSphere ? 0 : -1;
            var claimedPoles = new HashSet<int>();
            var triangleIndexCount = draft.triangles.Count;
            for (var triangle = 0; triangle + 2 < triangleIndexCount; triangle += 3)
            {
                var poleCorner = FindPoleCornerByIndex(
                    draft,
                    triangle,
                    bottomPoleIndex,
                    topPoleIndex);
                if (poleCorner < 0)
                {
                    continue;
                }

                var poleTriangleIndex = triangle + poleCorner;
                var sourceIndex = draft.triangles[poleTriangleIndex];
                var firstNeighbor =
                    draft.triangles[triangle + (poleCorner + 1) % 3];
                var secondNeighbor =
                    draft.triangles[triangle + (poleCorner + 2) % 3];
                var poleUV = draft.uv0[sourceIndex];
                poleUV.x =
                    (draft.uv0[firstNeighbor].x + draft.uv0[secondNeighbor].x) *
                    0.5f;

                if (claimedPoles.Add(sourceIndex))
                {
                    draft.uv0[sourceIndex] = poleUV;
                }
                else
                {
                    draft.triangles[poleTriangleIndex] =
                        DuplicateVertexWithUV(draft, sourceIndex, poleUV);
                }
            }
        }

        private static int FindPoleCornerByIndex(
            VFXMeshDraft draft,
            int triangle,
            int bottomPoleIndex,
            int topPoleIndex)
        {
            for (var corner = 0; corner < 3; corner++)
            {
                var sourceIndex = draft.triangles[triangle + corner];
                if (sourceIndex != bottomPoleIndex &&
                    sourceIndex != topPoleIndex)
                {
                    continue;
                }

                var firstNeighbor =
                    draft.triangles[triangle + (corner + 1) % 3];
                var secondNeighbor =
                    draft.triangles[triangle + (corner + 2) % 3];
                var poleV = draft.uv0[sourceIndex].y;
                var firstNeighborV = draft.uv0[firstNeighbor].y;
                var secondNeighborV = draft.uv0[secondNeighbor].y;
                if (Mathf.Abs(firstNeighborV - secondNeighborV) <=
                        VFXProcessingUtility.Epsilon &&
                    Mathf.Abs(firstNeighborV - poleV) >
                        VFXProcessingUtility.Epsilon)
                {
                    return corner;
                }
            }

            return -1;
        }

        private static void SplitAngularPoles(VFXMeshRecipe recipe, VFXMeshDraft draft)
        {
            var axisType = recipe.shape == null ? VFXAxis.Y : recipe.shape.axis;
            var axis = VFXProcessingUtility.AxisVector(axisType);
            var bounds = draft.CalculateBounds();
            var center = ResolveAngularCenter(
                recipe,
                draft,
                bounds.center);
            var maximumRadius =
                VFXProcessingUtility.CalculateMaximumRadius(draft.vertices, center, axis);
            var poleTolerance = Mathf.Max(
                VFXProcessingUtility.Epsilon,
                maximumRadius * 1e-5f);
            var poleToleranceSquared = poleTolerance * poleTolerance;
            var triangleIndexCount = draft.triangles.Count;

            for (var triangle = 0; triangle + 2 < triangleIndexCount; triangle += 3)
            {
                var poleCornerMask = 0;
                var nonPoleUSum = 0f;
                var nonPoleCount = 0;
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertexIndex = draft.triangles[triangle + corner];
                    var radial = VFXProcessingUtility.RadialVector(
                        draft.vertices[vertexIndex],
                        center,
                        axis);
                    var isPole = radial.sqrMagnitude <= poleToleranceSquared;
                    if (isPole)
                    {
                        poleCornerMask |= 1 << corner;
                        continue;
                    }

                    nonPoleUSum += draft.uv0[vertexIndex].x;
                    nonPoleCount++;
                }

                if (nonPoleCount == 0 || nonPoleCount == 3)
                {
                    continue;
                }

                var poleU = nonPoleUSum / nonPoleCount;
                for (var corner = 0; corner < 3; corner++)
                {
                    if ((poleCornerMask & (1 << corner)) == 0)
                    {
                        continue;
                    }

                    var triangleIndex = triangle + corner;
                    var sourceIndex = draft.triangles[triangleIndex];
                    var poleUV = draft.uv0[sourceIndex];
                    poleUV.x = poleU;
                    draft.triangles[triangleIndex] =
                        DuplicateVertexWithUV(draft, sourceIndex, poleUV);
                }
            }
        }

        private static void DuplicateOptionalChannel<T>(List<T> values, int vertexCount, int sourceIndex)
        {
            if (values.Count == vertexCount)
            {
                values.Add(values[sourceIndex]);
            }
        }

        private static void Project(
            VFXMeshRecipe recipe,
            VFXMeshDraft draft,
            VFXUVProjection projection,
            int vertexCount)
        {
            var axisType = recipe.shape == null ? VFXAxis.Y : recipe.shape.axis;
            var axis = VFXProcessingUtility.AxisVector(axisType);
            VFXProcessingUtility.GetPlanarBasis(axisType, out var radialA, out var radialB);
            var bounds = CalculateBounds(draft.vertices, vertexCount);
            var center = ResolveAngularCenter(recipe, draft, bounds.center);
            GetProjectionRange(
                draft.vertices,
                radialA,
                vertexCount,
                out var radialAMinimum,
                out var radialAMaximum);
            GetProjectionRange(
                draft.vertices,
                radialB,
                vertexCount,
                out var radialBMinimum,
                out var radialBMaximum);

            var minimumRadius = float.PositiveInfinity;
            var maximumRadius = 0f;
            for (var i = 0; i < vertexCount; i++)
            {
                var radius =
                    VFXProcessingUtility.RadialVector(draft.vertices[i], center, axis).magnitude;
                minimumRadius = Mathf.Min(minimumRadius, radius);
                maximumRadius = Mathf.Max(maximumRadius, radius);
            }

            if (float.IsPositiveInfinity(minimumRadius))
            {
                minimumRadius = 0f;
            }

            var boxNormals = projection == VFXUVProjection.Box
                ? CalculateVertexNormals(draft)
                : null;
            for (var i = 0; i < vertexCount; i++)
            {
                var position = draft.vertices[i];
                switch (projection)
                {
                    case VFXUVProjection.Planar:
                        draft.uv0[i] = new Vector2(
                            VFXProcessingUtility.Normalize(
                                Vector3.Dot(position, radialA),
                                radialAMinimum,
                                radialAMaximum),
                            VFXProcessingUtility.Normalize(
                                Vector3.Dot(position, radialB),
                                radialBMinimum,
                                radialBMaximum));
                        break;
                    case VFXUVProjection.Radial:
                    {
                        var radial = VFXProcessingUtility.RadialVector(position, center, axis);
                        draft.uv0[i] = new Vector2(
                            Angle01(radial, radialA, radialB),
                            VFXProcessingUtility.Normalize(
                                radial.magnitude,
                                minimumRadius,
                                maximumRadius));
                        break;
                    }
                    case VFXUVProjection.Cylindrical:
                    {
                        var radial = VFXProcessingUtility.RadialVector(position, center, axis);
                        draft.uv0[i] = new Vector2(
                            Angle01(radial, radialA, radialB),
                            VFXProcessingUtility.NormalizedAxis(position, bounds, axisType));
                        break;
                    }
                    case VFXUVProjection.Spherical:
                    {
                        var direction = position - center;
                        if (direction.sqrMagnitude <=
                            VFXProcessingUtility.Epsilon *
                            VFXProcessingUtility.Epsilon)
                        {
                            draft.uv0[i] = new Vector2(0.5f, 0.5f);
                            break;
                        }

                        direction.Normalize();
                        var radial = direction - axis * Vector3.Dot(direction, axis);
                        var longitude = Angle01(radial, radialA, radialB);
                        var latitude =
                            Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction, axis), -1f, 1f)) /
                            Mathf.PI +
                            0.5f;
                        draft.uv0[i] = new Vector2(longitude, latitude);
                        break;
                    }
                    case VFXUVProjection.AlongLength:
                        if (recipe.shapeType == VFXMeshShapeType.Quad)
                        {
                            draft.uv0[i] = new Vector2(
                                VFXProcessingUtility.Normalize(
                                    Vector3.Dot(position, radialB),
                                    radialBMinimum,
                                    radialBMaximum),
                                VFXProcessingUtility.Normalize(
                                    Vector3.Dot(position, radialA),
                                    radialAMinimum,
                                    radialAMaximum));
                        }
                        else
                        {
                            draft.uv0[i] = new Vector2(
                                VFXProcessingUtility.NormalizedAxis(position, bounds, axisType),
                                VFXProcessingUtility.Normalize(
                                    Vector3.Dot(position, radialA),
                                    radialAMinimum,
                                    radialAMaximum));
                        }

                        break;
                    case VFXUVProjection.Box:
                        draft.uv0[i] = BoxProject(position, boxNormals[i], bounds);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(projection),
                            projection,
                            "Unsupported VFX UV projection.");
                }
            }
        }

        private static void CopyMirroredArcUVs(
            VFXMeshDraft draft,
            int sourceVertexCount)
        {
            if (sourceVertexCount <= 0 ||
                sourceVertexCount * 2 > draft.uv0.Count)
            {
                return;
            }

            for (var vertex = 0; vertex < sourceVertexCount; vertex++)
            {
                draft.uv0[sourceVertexCount + vertex] = draft.uv0[vertex];
            }
        }

        private static Vector3 ResolveAngularCenter(
            VFXMeshRecipe recipe,
            VFXMeshDraft draft,
            Vector3 fallbackCenter)
        {
            if (draft == null || draft.vertices.Count == 0)
            {
                return fallbackCenter;
            }

            return UsesDiscTopology(recipe)
                ? draft.vertices[0]
                : fallbackCenter;
        }

        private static Bounds CalculateBounds(
            IReadOnlyList<Vector3> vertices,
            int count)
        {
            count = Mathf.Clamp(count, 0, vertices?.Count ?? 0);
            if (count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (var index = 1; index < count; index++)
            {
                bounds.Encapsulate(vertices[index]);
            }

            return bounds;
        }

        private static void GetProjectionRange(
            IReadOnlyList<Vector3> vertices,
            Vector3 direction,
            int count,
            out float minimum,
            out float maximum)
        {
            count = Mathf.Clamp(count, 0, vertices?.Count ?? 0);
            if (count == 0)
            {
                minimum = 0f;
                maximum = 0f;
                return;
            }

            minimum = Vector3.Dot(vertices[0], direction);
            maximum = minimum;
            for (var index = 1; index < count; index++)
            {
                var value = Vector3.Dot(vertices[index], direction);
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }
        }

        private static Vector2 Transform(Vector2 uv, VFXUVSettings settings)
        {
            if (settings.swapUV)
            {
                uv = new Vector2(uv.y, uv.x);
            }

            if (settings.flipU)
            {
                uv.x = 1f - uv.x;
            }

            if (settings.flipV)
            {
                uv.y = 1f - uv.y;
            }

            if (Mathf.Abs(settings.rotation) > VFXProcessingUtility.Epsilon)
            {
                var radians = settings.rotation * Mathf.Deg2Rad;
                var sine = Mathf.Sin(radians);
                var cosine = Mathf.Cos(radians);
                var centered = uv - new Vector2(0.5f, 0.5f);
                uv = new Vector2(
                         centered.x * cosine - centered.y * sine,
                         centered.x * sine + centered.y * cosine) +
                     new Vector2(0.5f, 0.5f);
            }

            return Vector2.Scale(uv, settings.scale) + settings.offset;
        }

        private static float Angle01(Vector3 radial, Vector3 radialA, Vector3 radialB)
        {
            if (radial.sqrMagnitude <=
                VFXProcessingUtility.Epsilon *
                VFXProcessingUtility.Epsilon)
            {
                return 0.5f;
            }

            var angle = Mathf.Atan2(Vector3.Dot(radial, radialB), Vector3.Dot(radial, radialA));
            return Mathf.Repeat(angle / (Mathf.PI * 2f), 1f);
        }

        private static Vector2 BoxProject(Vector3 position, Vector3 normal, Bounds bounds)
        {
            var local = position - bounds.center;
            var extents = bounds.extents;
            var normalized = new Vector3(
                extents.x <= VFXProcessingUtility.Epsilon ? 0f : local.x / extents.x,
                extents.y <= VFXProcessingUtility.Epsilon ? 0f : local.y / extents.y,
                extents.z <= VFXProcessingUtility.Epsilon ? 0f : local.z / extents.z);
            var dominant = normal.sqrMagnitude > VFXProcessingUtility.Epsilon
                ? normal.normalized
                : normalized;
            var absolute = new Vector3(
                Mathf.Abs(dominant.x),
                Mathf.Abs(dominant.y),
                Mathf.Abs(dominant.z));

            Vector2 projected;
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
            {
                projected = dominant.x >= 0f
                    ? new Vector2(-normalized.z, normalized.y)
                    : new Vector2(normalized.z, normalized.y);
            }
            else if (absolute.y >= absolute.x && absolute.y >= absolute.z)
            {
                projected = dominant.y >= 0f
                    ? new Vector2(normalized.x, -normalized.z)
                    : new Vector2(normalized.x, normalized.z);
            }
            else
            {
                projected = dominant.z >= 0f
                    ? new Vector2(normalized.x, normalized.y)
                    : new Vector2(-normalized.x, normalized.y);
            }

            return projected * 0.5f + new Vector2(0.5f, 0.5f);
        }

        private static List<Vector3> CalculateVertexNormals(VFXMeshDraft draft)
        {
            var normals = new List<Vector3>(draft.vertices.Count);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                normals.Add(Vector3.zero);
            }

            for (var i = 0; i + 2 < draft.triangles.Count; i += 3)
            {
                var a = draft.triangles[i];
                var b = draft.triangles[i + 1];
                var c = draft.triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 ||
                    a >= draft.vertices.Count ||
                    b >= draft.vertices.Count ||
                    c >= draft.vertices.Count)
                {
                    continue;
                }

                var faceNormal = Vector3.Cross(
                    draft.vertices[b] - draft.vertices[a],
                    draft.vertices[c] - draft.vertices[a]);
                if (faceNormal.sqrMagnitude <= VFXProcessingUtility.Epsilon)
                {
                    continue;
                }

                normals[a] += faceNormal;
                normals[b] += faceNormal;
                normals[c] += faceNormal;
            }

            for (var i = 0; i < normals.Count; i++)
            {
                if (normals[i].sqrMagnitude > VFXProcessingUtility.Epsilon)
                {
                    normals[i].Normalize();
                }
            }

            return normals;
        }
    }
}
