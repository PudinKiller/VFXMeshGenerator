using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor.Tests
{
    public sealed class VFXMeshBuilderTests
    {
        [TestCase(VFXMeshShapeType.Quad)]
        [TestCase(VFXMeshShapeType.Disc)]
        [TestCase(VFXMeshShapeType.Ring)]
        [TestCase(VFXMeshShapeType.Arc)]
        [TestCase(VFXMeshShapeType.Cone)]
        [TestCase(VFXMeshShapeType.Cylinder)]
        [TestCase(VFXMeshShapeType.Tube)]
        [TestCase(VFXMeshShapeType.Sphere)]
        [TestCase(VFXMeshShapeType.Hemisphere)]
        [TestCase(VFXMeshShapeType.Torus)]
        [TestCase(VFXMeshShapeType.Box)]
        [TestCase(VFXMeshShapeType.Ribbon)]
        [TestCase(VFXMeshShapeType.CrossPlanes)]
        [TestCase(VFXMeshShapeType.Helix)]
        public void EveryShapeBuildsAValidMesh(VFXMeshShapeType shapeType)
        {
            var recipe = CreateRecipe(shapeType);
            var result = VFXMeshBuilder.Build(recipe);

            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(result.mesh, Is.Not.Null);
                Assert.That(result.vertexCount, Is.GreaterThan(0));
                Assert.That(result.triangleCount, Is.GreaterThan(0));
                Assert.That(
                    result.validationFlags & VFXMeshValidationFlags.InvalidIndex,
                    Is.EqualTo(VFXMeshValidationFlags.None));
                Assert.That(
                    result.validationFlags & VFXMeshValidationFlags.NonFiniteVertex,
                    Is.EqualTo(VFXMeshValidationFlags.None));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void DoubleSidedOutputDoublesTriangleCount()
        {
            var oneSided = CreateRecipe(VFXMeshShapeType.Quad);
            var twoSided = oneSided.DeepCopy();
            twoSided.output.doubleSided = true;

            var oneSidedResult = VFXMeshBuilder.Build(oneSided);
            var twoSidedResult = VFXMeshBuilder.Build(twoSided);

            try
            {
                Assert.That(twoSidedResult.triangleCount, Is.EqualTo(oneSidedResult.triangleCount * 2));
                Assert.That(twoSidedResult.vertexCount, Is.EqualTo(oneSidedResult.vertexCount * 2));
            }
            finally
            {
                DestroyResult(oneSidedResult);
                DestroyResult(twoSidedResult);
            }
        }

        [Test]
        public void FlatShadingSplitsVerticesPerTriangle()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Quad);
            recipe.shape.widthSegments = 2;
            recipe.shape.lengthSegments = 2;
            recipe.output.flatShading = true;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.vertexCount, Is.EqualTo(result.triangleCount * 3));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void SeededModifiersAreDeterministic()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Sphere);
            recipe.modifiers.Add(new VFXMeshModifierSettings
            {
                type = VFXModifierType.Noise,
                seed = 9281,
                strength = 0.2f,
                frequency = 2.75f,
                octaves = 3
            });

            var first = VFXMeshBuilder.Build(recipe);
            var second = VFXMeshBuilder.Build(recipe);

            try
            {
                Assert.That(first.vertexCount, Is.EqualTo(second.vertexCount));
                var firstVertices = first.mesh.vertices;
                var secondVertices = second.mesh.vertices;
                for (var i = 0; i < firstVertices.Length; i++)
                {
                    Assert.That(Vector3.Distance(firstVertices[i], secondVertices[i]), Is.LessThan(1e-6f));
                }
            }
            finally
            {
                DestroyResult(first);
                DestroyResult(second);
            }
        }

        [Test]
        public void OptionalVertexStreamsAreWritten()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ribbon);
            recipe.vertexData.generateColors = true;
            recipe.vertexData.colorMode = VFXVertexColorMode.AxisGradient;
            recipe.vertexData.generateUV1 = true;
            recipe.vertexData.generateUV2 = true;
            recipe.vertexData.generateUV3 = true;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.mesh.colors, Has.Length.EqualTo(result.vertexCount));

                for (var channel = 1; channel <= 3; channel++)
                {
                    var values = new List<Vector4>();
                    result.mesh.GetUVs(channel, values);
                    Assert.That(values, Has.Count.EqualTo(result.vertexCount), $"UV channel {channel}");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void AllModifierTypesProduceFiniteVertices()
        {
            foreach (VFXModifierType modifierType in Enum.GetValues(typeof(VFXModifierType)))
            {
                var recipe = CreateRecipe(VFXMeshShapeType.Sphere);
                recipe.modifiers.Add(new VFXMeshModifierSettings
                {
                    type = modifierType,
                    strength = 0.15f,
                    angle = 30f,
                    frequency = 2f,
                    seed = 4
                });

                var result = VFXMeshBuilder.Build(recipe);
                try
                {
                    Assert.That(result.succeeded, Is.True, $"{modifierType}: {result.error}");
                    Assert.That(
                        result.validationFlags & VFXMeshValidationFlags.NonFiniteVertex,
                        Is.EqualTo(VFXMeshValidationFlags.None),
                        modifierType.ToString());
                }
                finally
                {
                    DestroyResult(result);
                }
            }
        }

        [Test]
        public void TransformModifierAppliesUniformlyToEveryVertex()
        {
            var baselineRecipe = CreateRecipe(VFXMeshShapeType.Quad);
            var transformedRecipe = baselineRecipe.DeepCopy();
            var offset = new Vector3(1.5f, -2f, 0.75f);
            transformedRecipe.modifiers.Add(new VFXMeshModifierSettings
            {
                type = VFXModifierType.Transform,
                offset = offset,
                scale = Vector3.one,
                angle = 0f
            });

            var baseline = VFXMeshBuilder.Build(baselineRecipe);
            var transformed = VFXMeshBuilder.Build(transformedRecipe);
            try
            {
                var originalVertices = baseline.mesh.vertices;
                var transformedVertices = transformed.mesh.vertices;
                Assert.That(transformedVertices, Has.Length.EqualTo(originalVertices.Length));
                for (var i = 0; i < originalVertices.Length; i++)
                {
                    Assert.That(
                        Vector3.Distance(transformedVertices[i], originalVertices[i] + offset),
                        Is.LessThan(1e-6f));
                }
            }
            finally
            {
                DestroyResult(baseline);
                DestroyResult(transformed);
            }
        }

        [Test]
        public void ExcessiveResolutionIsRejectedBeforeGeneration()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Quad);
            recipe.shape.widthSegments = VFXMeshBuildLimits.MaximumSegmentsPerAxis + 1;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.False);
                Assert.That(result.mesh, Is.Null);
                StringAssert.Contains("cannot exceed", result.error);
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void AngularProjectionSplitsWrappedSeamTriangles()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.uv.projection = VFXUVProjection.Radial;
            var result = VFXMeshBuilder.Build(recipe);

            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var uv = result.mesh.uv;
                var triangles = result.mesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var first = uv[triangles[i]].x;
                    var second = uv[triangles[i + 1]].x;
                    var third = uv[triangles[i + 2]].x;
                    var minimum = Mathf.Min(first, Mathf.Min(second, third));
                    var maximum = Mathf.Max(first, Mathf.Max(second, third));
                    Assert.That(maximum - minimum, Is.LessThanOrEqualTo(0.5001f));
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void RingElevationCurveSamplesInnerAndOuterEdges()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1f;
            recipe.shape.widthSegments = 2;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, -0.25f),
                new Keyframe(0.5f, 0.9f),
                new Keyframe(1f, 0.75f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var rowStride = recipe.shape.radialSegments + 1;
                Assert.That(vertices[0].y, Is.EqualTo(-0.25f).Within(1e-5f));
                Assert.That(vertices[rowStride].y, Is.EqualTo(0.9f).Within(1e-5f));
                Assert.That(vertices[rowStride * recipe.shape.widthSegments].y,
                    Is.EqualTo(0.75f).Within(1e-5f));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void CurvedFullRingHasMatchingNormalsAcrossAngularSeam()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.widthSegments = 4;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 0.6f),
                new Keyframe(1f, -0.15f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var normals = result.mesh.normals;
                var rowStride = recipe.shape.radialSegments + 1;
                for (var row = 0; row <= recipe.shape.widthSegments; row++)
                {
                    var first = row * rowStride;
                    var last = first + recipe.shape.radialSegments;
                    Assert.That(Vector3.Distance(normals[first], normals[last]), Is.LessThan(1e-6f));
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void ZeroInnerRadiusRingElevatesFromCenterToOuterEdge()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0f;
            recipe.shape.radius = 1f;
            recipe.shape.radialElevationCurve = AnimationCurve.Linear(0f, 0.4f, 1f, -0.2f);

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                Assert.That(vertices[0].y, Is.EqualTo(0.4f).Within(1e-5f));
                Assert.That(vertices[vertices.Length - 1].y, Is.EqualTo(-0.2f).Within(1e-5f));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void NullRingElevationCurvePreservesFlatLegacyShape()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radialElevationCurve = null;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                foreach (var vertex in result.mesh.vertices)
                {
                    Assert.That(vertex.y, Is.EqualTo(0f).Within(1e-6f));
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void DeepCopyPreservesRingElevationCurve()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.radialElevationCurve =
                new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.45f, 1.1f), new Keyframe(1f, -0.3f));

            var copy = recipe.DeepCopy();

            Assert.That(copy.shape.radialElevationCurve, Is.Not.Null);
            Assert.That(copy.shape.radialElevationCurve.Evaluate(0f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(copy.shape.radialElevationCurve.Evaluate(0.45f), Is.EqualTo(1.1f).Within(1e-5f));
            Assert.That(copy.shape.radialElevationCurve.Evaluate(1f), Is.EqualTo(-0.3f).Within(1e-5f));
        }

        [Test]
        public void PreviewShaderExposesOpaqueBackfaceControls()
        {
            var shader = Shader.Find("Hidden/PudinKiller/VFXMeshPreview");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);

            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_Cull"), Is.True);
                Assert.That(material.HasProperty("_BackfacePass"), Is.True);
                Assert.That(material.HasProperty("_BackfaceColor"), Is.True);
                Assert.That(material.GetColor("_BackfaceColor").a, Is.EqualTo(1f).Within(1e-6f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static VFXMeshRecipe CreateRecipe(VFXMeshShapeType shapeType)
        {
            var recipe = new VFXMeshRecipe
            {
                meshName = $"Test {shapeType}",
                shapeType = shapeType
            };

            recipe.shape.radialSegments = 12;
            recipe.shape.longitudeSegments = 12;
            recipe.shape.latitudeSegments = 6;
            recipe.shape.widthSegments = 2;
            recipe.shape.lengthSegments = 8;
            recipe.shape.heightSegments = 2;
            recipe.shape.arcDegrees = shapeType == VFXMeshShapeType.Arc ? 210f : 360f;
            return recipe;
        }

        private static void DestroyResult(VFXMeshBuildResult result)
        {
            if (result?.mesh != null)
            {
                UnityEngine.Object.DestroyImmediate(result.mesh);
            }
        }
    }
}
