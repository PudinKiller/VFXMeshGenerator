using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

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
        public void RingIgnoresArcDegreesAndCloses()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1f;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 73f;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var rowStride = recipe.shape.radialSegments + 1;
                Assert.That(
                    vertices,
                    Has.Length.EqualTo((recipe.shape.widthSegments + 1) * rowStride));

                for (var row = 0; row <= recipe.shape.widthSegments; row++)
                {
                    var first = row * rowStride;
                    var last = first + recipe.shape.radialSegments;
                    Assert.That(
                        Vector3.Distance(vertices[first], vertices[last]),
                        Is.LessThan(1e-5f),
                        $"Radial row {row} did not close.");
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
        public void CurvedRingWithStaleArcDegreesHasMatchingNormalsAcrossAngularSeam()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ring);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.widthSegments = 4;
            recipe.shape.arcDegrees = 137f;
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
        public void VariableWidthFullArcWithMatchingEndpointWidthsHasMatchingSeamNormals()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.25f;
            recipe.shape.widthSegments = 4;
            recipe.shape.arcDegrees = 360f;
            recipe.shape.arcWidthCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.6f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    Mathf.Abs(
                        recipe.shape.arcWidthCurve.Evaluate(0.5f) -
                        recipe.shape.arcWidthCurve.Evaluate(0f)),
                    Is.GreaterThan(1e-5f));

                var vertices = result.mesh.vertices;
                var normals = result.mesh.normals;
                var uv = result.mesh.uv;
                var startIndices = new List<int>();
                var endIndices = new List<int>();
                for (var index = 0; index < uv.Length; index++)
                {
                    if (Mathf.Abs(uv[index].x) <= 1e-5f)
                    {
                        startIndices.Add(index);
                    }

                    if (Mathf.Abs(uv[index].x - 1f) <= 1e-5f)
                    {
                        endIndices.Add(index);
                    }
                }

                Assert.That(startIndices, Has.Count.EqualTo(recipe.shape.widthSegments + 1));
                Assert.That(endIndices, Has.Count.EqualTo(startIndices.Count));
                for (var row = 0; row < startIndices.Count; row++)
                {
                    var first = startIndices[row];
                    var last = endIndices[row];
                    Assert.That(uv[last].y, Is.EqualTo(uv[first].y).Within(1e-6f));
                    Assert.That(
                        Vector3.Distance(vertices[first], vertices[last]),
                        Is.LessThan(1e-5f),
                        $"Seam positions differ at radial row {row}.");
                    Assert.That(
                        Vector3.Distance(normals[first], normals[last]),
                        Is.LessThan(1e-6f),
                        $"Seam normals differ at radial row {row}.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void ConstantWidthArcNormalizesRaw720DegreesAndSmoothsSeam()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.25f;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 720f;
            recipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var normals = result.mesh.normals;
                var rowStride = recipe.shape.radialSegments + 1;
                for (var row = 0; row <= recipe.shape.widthSegments; row++)
                {
                    var first = row * rowStride;
                    var last = first + recipe.shape.radialSegments;
                    Assert.That(
                        Vector3.Distance(vertices[first], vertices[last]),
                        Is.LessThan(1e-5f),
                        $"Normalized seam positions differ at radial row {row}.");
                    Assert.That(
                        Vector3.Distance(normals[first], normals[last]),
                        Is.LessThan(1e-6f),
                        $"Normalized seam normals differ at radial row {row}.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void ArcWidthCurveCreatesCleanCollapsedTipsAndSamplesMiddleWidth()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.25f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.arcWidthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.6f),
                new Keyframe(1f, 0f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    result.validationFlags & VFXMeshValidationFlags.DegenerateTriangle,
                    Is.EqualTo(VFXMeshValidationFlags.None));
                Assert.That(
                    result.validationFlags & VFXMeshValidationFlags.NonFiniteVertex,
                    Is.EqualTo(VFXMeshValidationFlags.None));

                var vertices = result.mesh.vertices;
                var uv = result.mesh.uv;
                var normals = result.mesh.normals;
                var tangents = result.mesh.tangents;
                Assert.That(uv, Has.Length.EqualTo(vertices.Length));
                Assert.That(normals, Has.Length.EqualTo(vertices.Length));
                Assert.That(tangents, Has.Length.EqualTo(vertices.Length));
                var startIndices = new List<int>();
                var middleIndices = new List<int>();
                var endIndices = new List<int>();
                for (var i = 0; i < uv.Length; i++)
                {
                    if (Mathf.Abs(uv[i].x) <= 1e-5f)
                    {
                        startIndices.Add(i);
                    }

                    if (Mathf.Abs(uv[i].x - 0.5f) <= 1e-5f)
                    {
                        middleIndices.Add(i);
                    }

                    if (Mathf.Abs(uv[i].x - 1f) <= 1e-5f)
                    {
                        endIndices.Add(i);
                    }
                }

                Assert.That(startIndices, Has.Count.EqualTo(1), "The start should collapse to one vertex.");
                Assert.That(endIndices, Has.Count.EqualTo(1), "The end should collapse to one vertex.");
                Assert.That(
                    middleIndices,
                    Has.Count.EqualTo(recipe.shape.widthSegments + 1),
                    "The middle should retain the requested radial resolution.");

                var centerRadius = (recipe.shape.innerRadius + recipe.shape.radius) * 0.5f;
                Assert.That(
                    RadialDistance(vertices[startIndices[0]]),
                    Is.EqualTo(centerRadius).Within(1e-5f));
                Assert.That(
                    RadialDistance(vertices[endIndices[0]]),
                    Is.EqualTo(centerRadius).Within(1e-5f));

                var minimumMiddleRadius = float.PositiveInfinity;
                var maximumMiddleRadius = float.NegativeInfinity;
                foreach (var index in middleIndices)
                {
                    var radialDistance = RadialDistance(vertices[index]);
                    minimumMiddleRadius = Mathf.Min(minimumMiddleRadius, radialDistance);
                    maximumMiddleRadius = Mathf.Max(maximumMiddleRadius, radialDistance);
                }

                var expectedMiddleWidth =
                    (recipe.shape.radius - recipe.shape.innerRadius) * 0.6f;
                Assert.That(
                    maximumMiddleRadius - minimumMiddleRadius,
                    Is.EqualTo(expectedMiddleWidth).Within(1e-5f));

                foreach (var vertex in vertices)
                {
                    Assert.That(IsFinite(vertex), Is.True, $"Non-finite vertex: {vertex}");
                }

                foreach (var normal in normals)
                {
                    Assert.That(IsFinite(normal), Is.True, $"Non-finite normal: {normal}");
                }

                foreach (var tangent in tangents)
                {
                    Assert.That(IsFinite(tangent), Is.True, $"Non-finite tangent: {tangent}");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void NullAndConstantOneArcWidthCurvesPreserveLegacyTopology()
        {
            var nullCurveRecipe = CreateRecipe(VFXMeshShapeType.Arc);
            nullCurveRecipe.shape.pivot = VFXPivot.Custom;
            nullCurveRecipe.shape.customPivotOffset = Vector3.zero;
            nullCurveRecipe.shape.innerRadius = 0.25f;
            nullCurveRecipe.shape.radius = 1f;
            nullCurveRecipe.shape.widthSegments = 3;
            nullCurveRecipe.shape.arcWidthCurve = null;

            var constantCurveRecipe = nullCurveRecipe.DeepCopy();
            constantCurveRecipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

            var nullCurveResult = VFXMeshBuilder.Build(nullCurveRecipe);
            var constantCurveResult = VFXMeshBuilder.Build(constantCurveRecipe);
            try
            {
                Assert.That(nullCurveResult.succeeded, Is.True, nullCurveResult.error);
                Assert.That(constantCurveResult.succeeded, Is.True, constantCurveResult.error);

                var expectedVertexCount =
                    (nullCurveRecipe.shape.widthSegments + 1) *
                    (nullCurveRecipe.shape.radialSegments + 1);
                var expectedTriangleCount =
                    nullCurveRecipe.shape.widthSegments *
                    nullCurveRecipe.shape.radialSegments *
                    2;
                Assert.That(nullCurveResult.vertexCount, Is.EqualTo(expectedVertexCount));
                Assert.That(nullCurveResult.triangleCount, Is.EqualTo(expectedTriangleCount));
                Assert.That(constantCurveResult.vertexCount, Is.EqualTo(expectedVertexCount));
                Assert.That(constantCurveResult.triangleCount, Is.EqualTo(expectedTriangleCount));

                CollectionAssert.AreEqual(
                    nullCurveResult.mesh.triangles,
                    constantCurveResult.mesh.triangles);
                var nullCurveVertices = nullCurveResult.mesh.vertices;
                var constantCurveVertices = constantCurveResult.mesh.vertices;
                Assert.That(constantCurveVertices, Has.Length.EqualTo(nullCurveVertices.Length));
                for (var i = 0; i < nullCurveVertices.Length; i++)
                {
                    Assert.That(
                        Vector3.Distance(nullCurveVertices[i], constantCurveVertices[i]),
                        Is.LessThan(1e-6f),
                        $"Vertex {i} differs.");
                }
            }
            finally
            {
                DestroyResult(nullCurveResult);
                DestroyResult(constantCurveResult);
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
        public void DeepCopyPreservesArcWidthCurve()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.arcWidthCurve =
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.4f, 0.85f),
                    new Keyframe(1f, 0.2f));

            var copy = recipe.DeepCopy();

            Assert.That(copy.shape.arcWidthCurve, Is.Not.Null);
            Assert.That(copy.shape.arcWidthCurve.Evaluate(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(copy.shape.arcWidthCurve.Evaluate(0.4f), Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(copy.shape.arcWidthCurve.Evaluate(1f), Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void PreviewModeNumericValuesRemainStable()
        {
            Assert.That((int)VFXPreviewMode.Shaded, Is.EqualTo(0));
            Assert.That((int)VFXPreviewMode.Unlit, Is.EqualTo(1));
            Assert.That((int)VFXPreviewMode.Wireframe, Is.EqualTo(2));
            Assert.That((int)VFXPreviewMode.UVChecker, Is.EqualTo(3));
            Assert.That((int)VFXPreviewMode.Normals, Is.EqualTo(4));
            Assert.That((int)VFXPreviewMode.VertexColors, Is.EqualTo(5));
            Assert.That((int)VFXPreviewMode.ShadedWireframe, Is.EqualTo(6));
        }

        [Test]
        public void WireframeOverlayUsesFiveUniqueQuadEdgesAndPreservesUInt32()
        {
            var source = new Mesh
            {
                name = "Wireframe Test Quad",
                indexFormat = IndexFormat.UInt32
            };
            Mesh overlay = null;
            try
            {
                source.vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(1f, -1f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(-1f, 1f, 0f)
                };
                source.triangles = new[]
                {
                    0, 1, 2,
                    0, 2, 3
                };
                source.RecalculateBounds();

                var previewControllerType = typeof(VFXMeshBuilder).Assembly.GetType(
                    "PudinKiller.VFXMeshGenerator.Editor.VFXMeshPreviewController",
                    true);
                var createOverlay = previewControllerType.GetMethod(
                    "CreateWireframeOverlayMesh",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(createOverlay, Is.Not.Null);

                overlay = createOverlay.Invoke(null, new object[] { source }) as Mesh;
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                Assert.That(overlay.GetTopology(0), Is.EqualTo(MeshTopology.Lines));

                var indices = overlay.GetIndices(0);
                Assert.That(indices, Has.Length.EqualTo(10));
                var edges = new HashSet<ulong>();
                for (var index = 0; index < indices.Length; index += 2)
                {
                    var minimum = Mathf.Min(indices[index], indices[index + 1]);
                    var maximum = Mathf.Max(indices[index], indices[index + 1]);
                    edges.Add(((ulong)(uint)minimum << 32) | (uint)maximum);
                }

                Assert.That(edges, Has.Count.EqualTo(5));
            }
            finally
            {
                if (overlay != null)
                {
                    UnityEngine.Object.DestroyImmediate(overlay);
                }

                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void PreviewShaderExposesBackfaceAndWireControls()
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
                Assert.That(material.HasProperty("_WirePass"), Is.True);
                Assert.That(material.HasProperty("_WireColor"), Is.True);
                Assert.That(material.HasProperty("_WireDepthBias"), Is.True);
                Assert.That(material.HasProperty("_ZWrite"), Is.True);
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

        private static float RadialDistance(Vector3 position)
        {
            return new Vector2(position.x, position.z).magnitude;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
