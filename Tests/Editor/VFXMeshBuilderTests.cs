using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
        public void FlatSphereSafetyEstimateDoesNotCountDiscardedPoleCopies()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Sphere);
            recipe.shape.longitudeSegments = 4096;
            recipe.shape.latitudeSegments = 40;
            recipe.uv.projection = VFXUVProjection.ShapeDefault;
            recipe.output.flatShading = true;

            var valid = VFXMeshBuildLimits.TryValidate(
                recipe,
                out var estimatedFinalVertices,
                out var error);

            Assert.That(valid, Is.True, error);
            Assert.That(
                estimatedFinalVertices,
                Is.LessThanOrEqualTo(VFXMeshBuildLimits.MaximumFinalVertexEstimate));
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

        [TestCase(3)]
        [TestCase(8)]
        public void DiscRadialProjectionDuplicatesCenterPerFanSector(int edgeCount)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 1.5f;
            recipe.shape.radialSegments = edgeCount;
            recipe.shape.widthSegments = 2;
            recipe.shape.angleOffset = 13f;
            recipe.uv.projection = VFXUVProjection.Radial;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var uv = result.mesh.uv;
                var triangles = result.mesh.triangles;
                var referencedCenterVertices = new HashSet<int>();
                var fanTriangleCount = 0;

                for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    var minimumU = float.PositiveInfinity;
                    var maximumU = float.NegativeInfinity;
                    var centerCorner = -1;
                    var centerCount = 0;
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var vertexIndex = triangles[triangle + corner];
                        var radius = RadialDistance(vertices[vertexIndex]);
                        Assert.That(
                            uv[vertexIndex].y,
                            Is.EqualTo(radius / recipe.shape.radius).Within(1e-5f),
                            $"Vertex {vertexIndex} has the wrong radial V coordinate.");
                        minimumU = Mathf.Min(minimumU, uv[vertexIndex].x);
                        maximumU = Mathf.Max(maximumU, uv[vertexIndex].x);
                        if (radius <= 1e-5f)
                        {
                            centerCorner = corner;
                            centerCount++;
                        }
                    }

                    Assert.That(
                        maximumU - minimumU,
                        Is.LessThanOrEqualTo(0.5001f),
                        $"Triangle {triangle / 3} crosses the wrapped U seam.");
                    if (centerCount == 0)
                    {
                        continue;
                    }

                    Assert.That(
                        centerCount,
                        Is.EqualTo(1),
                        $"Fan triangle {triangle / 3} should reference exactly one center.");
                    var centerIndex = triangles[triangle + centerCorner];
                    Assert.That(
                        referencedCenterVertices.Add(centerIndex),
                        Is.True,
                        $"Center vertex {centerIndex} was reused by more than one fan sector.");

                    var firstNeighbor =
                        triangles[triangle + (centerCorner + 1) % 3];
                    var secondNeighbor =
                        triangles[triangle + (centerCorner + 2) % 3];
                    Assert.That(
                        Mathf.Abs(uv[firstNeighbor].x - uv[secondNeighbor].x),
                        Is.LessThanOrEqualTo(0.5001f),
                        $"Fan sector {fanTriangleCount} did not use unwrapped neighbor UVs.");
                    Assert.That(
                        uv[centerIndex].x,
                        Is.EqualTo((uv[firstNeighbor].x + uv[secondNeighbor].x) * 0.5f)
                            .Within(1e-6f),
                        $"Fan sector {fanTriangleCount} center U is not the neighbor midpoint.");
                    Assert.That(
                        uv[centerIndex].y,
                        Is.EqualTo(0f).Within(1e-6f),
                        $"Fan sector {fanTriangleCount} center V should represent zero radius.");
                    fanTriangleCount++;
                }

                Assert.That(fanTriangleCount, Is.EqualTo(recipe.shape.radialSegments));
                Assert.That(
                    referencedCenterVertices,
                    Has.Count.EqualTo(recipe.shape.radialSegments));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void PartialDiscGeneratesAnOpenFanAtTheRequestedAngles()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 1f;
            recipe.shape.radialSegments = 4;
            recipe.shape.widthSegments = 1;
            recipe.shape.arcDegrees = 90f;
            recipe.shape.angleOffset = 0f;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(result.vertexCount, Is.EqualTo(6));
                Assert.That(result.triangleCount, Is.EqualTo(4));
                var vertices = result.mesh.vertices;
                Assert.That(
                    Vector3.Distance(vertices[1], new Vector3(1f, 0f, 0f)),
                    Is.LessThan(1e-5f));
                Assert.That(
                    Vector3.Distance(vertices[5], new Vector3(0f, 0f, 1f)),
                    Is.LessThan(1e-5f));
                Assert.That(
                    Vector3.Distance(vertices[1], vertices[5]),
                    Is.GreaterThan(1f),
                    "The fan's angular boundaries were incorrectly welded.");
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void DiscRadialDistributionRemapsRadiusElevationAndShapeDefaultUV()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 2f;
            recipe.shape.radialSegments = 4;
            recipe.shape.widthSegments = 4;
            recipe.shape.arcDegrees = 360f;
            recipe.shape.radialDistributionCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 1f));
            recipe.shape.radialElevationCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);
            recipe.uv.projection = VFXUVProjection.ShapeDefault;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var stride = recipe.shape.radialSegments + 1;
                var middleRingStart = 1 + stride;
                var middle = result.mesh.vertices[middleRingStart];
                var middleUV = result.mesh.uv[middleRingStart];

                Assert.That(RadialDistance(middle), Is.EqualTo(1.6f).Within(1e-5f));
                Assert.That(middle.y, Is.EqualTo(0.8f).Within(1e-5f));
                Assert.That(middleUV.x, Is.EqualTo(0.9f).Within(1e-5f));
                Assert.That(middleUV.y, Is.EqualTo(0.5f).Within(1e-5f));
                Assert.That(
                    RadialDistance(result.mesh.vertices[result.mesh.vertexCount - stride]),
                    Is.EqualTo(recipe.shape.radius).Within(1e-5f),
                    "The distribution curve moved the fixed outer endpoint.");
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void CurvedFullDiscSmoothsEveryRadialRowSeam()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radialSegments = 12;
            recipe.shape.widthSegments = 4;
            recipe.shape.arcDegrees = 360f;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.45f, 0.6f),
                new Keyframe(1f, -0.15f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var normals = result.mesh.normals;
                var stride = recipe.shape.radialSegments + 1;
                for (var ring = 0; ring < recipe.shape.widthSegments; ring++)
                {
                    var first = 1 + ring * stride;
                    var last = first + recipe.shape.radialSegments;
                    Assert.That(
                        Vector3.Distance(normals[first], normals[last]),
                        Is.LessThan(1e-6f),
                        $"Disc radial row {ring + 1} has a normal seam.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(VFXUVProjection.Radial, 360f)]
        [TestCase(VFXUVProjection.Radial, 225f)]
        [TestCase(VFXUVProjection.Cylindrical, 360f)]
        [TestCase(VFXUVProjection.Spherical, 225f)]
        public void ElevatedDiscAngularProjectionKeepsUVSplitNormalsSmooth(
            VFXUVProjection projection,
            float arcDegrees)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 1.5f;
            recipe.shape.radialSegments = 12;
            recipe.shape.widthSegments = 4;
            recipe.shape.arcDegrees = arcDegrees;
            recipe.shape.angleOffset = 137f;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, -0.15f),
                new Keyframe(0.45f, 0.7f),
                new Keyframe(1f, 0.1f));
            recipe.uv.projection = projection;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    AssertCoincidentNormalGroupsAreSmooth(
                        result.mesh.vertices,
                        result.mesh.normals),
                    Is.GreaterThan(0),
                    "The projection did not create any coincident UV-only vertices.");
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void SphereRadialProfileScalesRingRadiusWithoutMovingLatitude()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Sphere);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 1f;
            recipe.shape.longitudeSegments = 4;
            recipe.shape.latitudeSegments = 4;
            recipe.shape.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.4f),
                new Keyframe(1f, 1f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var equatorStart = 1 + recipe.shape.longitudeSegments + 1;
                var equator = result.mesh.vertices[equatorStart];
                Assert.That(RadialDistance(equator), Is.EqualTo(0.4f).Within(1e-5f));
                Assert.That(equator.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(result.mesh.vertices[0].y, Is.EqualTo(-1f).Within(1e-5f));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void HemisphereProfileKeepsScaledEquatorCapJoined()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Hemisphere);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.radius = 1f;
            recipe.shape.longitudeSegments = 4;
            recipe.shape.latitudeSegments = 2;
            recipe.shape.capEnd = true;
            recipe.shape.widthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var sideEquator = result.mesh.vertices[0];
                var topPoleIndex =
                    recipe.shape.latitudeSegments *
                    (recipe.shape.longitudeSegments + 1);
                var capRingStart = topPoleIndex + 2;
                var capEquator = result.mesh.vertices[capRingStart];

                Assert.That(RadialDistance(sideEquator), Is.EqualTo(0.5f).Within(1e-5f));
                Assert.That(
                    Vector3.Distance(sideEquator, capEquator),
                    Is.LessThan(1e-6f),
                    "The scaled Hemisphere side and cap no longer share an equator.");
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void BoxCrossSectionScaleFollowsMainAxisProfile()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Box);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.size = new Vector3(2f, 2f, 2f);
            recipe.shape.widthSegments = 1;
            recipe.shape.heightSegments = 2;
            recipe.shape.lengthSegments = 1;
            recipe.shape.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.25f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    MaximumCrossSectionExtentAtY(result.mesh.vertices, -1f),
                    Is.EqualTo(0.5f).Within(1e-5f));
                Assert.That(
                    MaximumCrossSectionExtentAtY(result.mesh.vertices, 0f),
                    Is.EqualTo(1f).Within(1e-5f));
                Assert.That(
                    MaximumCrossSectionExtentAtY(result.mesh.vertices, 1f),
                    Is.EqualTo(0.25f).Within(1e-5f));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void ClosedTorusScaleCurveKeepsFirstAndLastTubeRingsJoined()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Torus);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.longitudeSegments = 8;
            recipe.shape.radialSegments = 4;
            recipe.shape.arcDegrees = 360f;
            recipe.shape.widthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var stride = recipe.shape.radialSegments + 1;
                var lastRing = recipe.shape.longitudeSegments * stride;
                for (var tube = 0; tube <= recipe.shape.radialSegments; tube++)
                {
                    Assert.That(
                        Vector3.Distance(vertices[tube], vertices[lastRing + tube]),
                        Is.LessThan(1e-5f),
                        $"Torus tube vertex {tube} has an open 360-degree scale seam.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(VFXMeshShapeType.Sphere, 7, 14)]
        [TestCase(VFXMeshShapeType.Hemisphere, 4, 4)]
        public void ShapeDefaultSphericalPolesUseIndependentSectorUVs(
            VFXMeshShapeType shapeType,
            int longitudeSegments,
            int expectedFanTriangleCount)
        {
            var recipe = CreateRecipe(shapeType);
            recipe.shape.longitudeSegments = longitudeSegments;
            recipe.shape.latitudeSegments = 4;
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.uv.projection = VFXUVProjection.ShapeDefault;
            recipe.modifiers.Add(
                new VFXMeshModifierSettings
                {
                    type = VFXModifierType.Skew,
                    axis = VFXAxis.Y,
                    space = VFXModifierSpace.Axis,
                    strength = 0.12f
                });
            recipe.vertexData.generateUV1 = true;
            recipe.vertexData.uv1.x = VFXDataSource.Random;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var uv = result.mesh.uv;
                var vertices = result.mesh.vertices;
                var normals = result.mesh.normals;
                var triangles = result.mesh.triangles;
                var packedUV1 = new List<Vector4>();
                result.mesh.GetUVs(1, packedUV1);
                var latitudeSegments = recipe.shape.latitudeSegments;
                var topPoleIndex = shapeType == VFXMeshShapeType.Sphere
                    ? 1 + (latitudeSegments - 1) * (longitudeSegments + 1)
                    : latitudeSegments * (longitudeSegments + 1);
                var polePositions = shapeType == VFXMeshShapeType.Sphere
                    ? new[] { vertices[0], vertices[topPoleIndex] }
                    : new[] { vertices[topPoleIndex] };
                var referencedPoleVertices = new HashSet<int>();
                var groupNormals = new Dictionary<int, Vector3>();
                var groupRandomValues = new Dictionary<int, float>();
                var fanTriangleCount = 0;

                for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    var poleCorner = FindCoincidentPoleCorner(
                        vertices,
                        triangles,
                        triangle,
                        polePositions,
                        out var poleGroup);
                    if (poleCorner < 0)
                    {
                        continue;
                    }

                    var poleIndex = triangles[triangle + poleCorner];
                    var firstNeighbor =
                        triangles[triangle + (poleCorner + 1) % 3];
                    var secondNeighbor =
                        triangles[triangle + (poleCorner + 2) % 3];
                    Assert.That(
                        referencedPoleVertices.Add(poleIndex),
                        Is.True,
                        $"Pole vertex {poleIndex} was reused by multiple fan sectors.");
                    Assert.That(
                        uv[poleIndex].x,
                        Is.EqualTo(
                                (uv[firstNeighbor].x + uv[secondNeighbor].x) *
                                0.5f)
                            .Within(1e-6f));

                    var minimumU = Mathf.Min(
                        uv[poleIndex].x,
                        Mathf.Min(uv[firstNeighbor].x, uv[secondNeighbor].x));
                    var maximumU = Mathf.Max(
                        uv[poleIndex].x,
                        Mathf.Max(uv[firstNeighbor].x, uv[secondNeighbor].x));
                    Assert.That(
                        maximumU - minimumU,
                        Is.LessThanOrEqualTo(1f / longitudeSegments + 1e-5f));

                    if (groupNormals.TryGetValue(poleGroup, out var groupNormal))
                    {
                        Assert.That(
                            Vector3.Distance(normals[poleIndex], groupNormal),
                            Is.LessThan(1e-5f),
                            "UV pole splitting introduced a hard normal seam.");
                        Assert.That(
                            packedUV1[poleIndex].x,
                            Is.EqualTo(groupRandomValues[poleGroup]).Within(1e-6f),
                            "Coincident pole copies received different packed Random data.");
                    }
                    else
                    {
                        groupNormals.Add(poleGroup, normals[poleIndex]);
                        groupRandomValues.Add(poleGroup, packedUV1[poleIndex].x);
                    }

                    fanTriangleCount++;
                }

                Assert.That(fanTriangleCount, Is.EqualTo(expectedFanTriangleCount));
                Assert.That(referencedPoleVertices, Has.Count.EqualTo(expectedFanTriangleCount));
                var baseVertexCount = shapeType == VFXMeshShapeType.Sphere
                    ? (long)(latitudeSegments - 1) * (longitudeSegments + 1) + 2L
                    : (long)latitudeSegments * (longitudeSegments + 1) +
                      1L +
                      (recipe.shape.capEnd ? longitudeSegments + 2L : 0L);
                var expectedVertexCount = baseVertexCount +
                    (shapeType == VFXMeshShapeType.Sphere
                        ? 2L * (longitudeSegments - 1L)
                        : longitudeSegments - 1L);
                Assert.That(result.vertexCount, Is.EqualTo(expectedVertexCount));
                Assert.That(normals, Has.Length.EqualTo(result.vertexCount));
                Assert.That(result.mesh.tangents, Has.Length.EqualTo(result.vertexCount));
                Assert.That(packedUV1, Has.Count.EqualTo(result.vertexCount));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(2f)]
        [TestCase(-2f)]
        public void HelixFrontFacesPointTowardPositiveMainAxis(float turns)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Helix);
            recipe.shape.turns = turns;
            recipe.shape.pitch = 0.3f;
            recipe.shape.width = 0.2f;
            recipe.shape.axis = VFXAxis.Y;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var triangles = result.mesh.triangles;
                for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    Assert.That(
                        Vector3.Dot(
                            TriangleNormal(vertices, triangles, triangle),
                            Vector3.up),
                        Is.GreaterThan(1e-5f),
                        $"Triangle {triangle / 3} is back-facing.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void TaperedRibbonPreserveTexelDensityUsesAffineShapeDefaultUVs()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ribbon);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.width = 2f;
            recipe.shape.length = 4f;
            recipe.shape.widthSegments = 1;
            recipe.shape.lengthSegments = 8;
            recipe.shape.ribbonUVWidthMode = VFXRibbonUVWidthMode.PreserveTexelDensity;
            recipe.shape.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.08f),
                new Keyframe(0.25f, 0.2f),
                new Keyframe(0.65f, 0.55f),
                new Keyframe(1f, 1f));
            recipe.uv.projection = VFXUVProjection.ShapeDefault;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var uv = result.mesh.uv;
                Assert.That(uv, Has.Length.EqualTo(vertices.Length));

                for (var vertex = 0; vertex < vertices.Length; vertex++)
                {
                    Assert.That(
                        uv[vertex].x,
                        Is.EqualTo(vertices[vertex].x / recipe.shape.width + 0.5f)
                            .Within(1e-5f),
                        $"Vertex {vertex} does not share the ribbon's affine U mapping.");
                    Assert.That(
                        uv[vertex].y,
                        Is.EqualTo(vertices[vertex].y / recipe.shape.length + 0.5f)
                            .Within(1e-5f),
                        $"Vertex {vertex} does not share the ribbon's affine V mapping.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void TaperedRibbonStretchToWidthPreservesLegacyRowRange()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Ribbon);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.widthSegments = 2;
            recipe.shape.lengthSegments = 8;
            recipe.shape.ribbonUVWidthMode = VFXRibbonUVWidthMode.StretchToWidth;
            recipe.shape.widthCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 1f);
            recipe.uv.projection = VFXUVProjection.ShapeDefault;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var uv = result.mesh.uv;
                var stride = recipe.shape.widthSegments + 1;
                for (var row = 0; row <= recipe.shape.lengthSegments; row++)
                {
                    var rowStart = row * stride;
                    Assert.That(uv[rowStart].x, Is.EqualTo(0f).Within(1e-6f));
                    Assert.That(
                        uv[rowStart + recipe.shape.widthSegments].x,
                        Is.EqualTo(1f).Within(1e-6f));
                    Assert.That(
                        uv[rowStart].y,
                        Is.EqualTo(row / (float)recipe.shape.lengthSegments).Within(1e-6f));
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void SphericalPoleSmoothingDoesNotMergeFlattenedHemisphereCap()
        {
            const int longitudeSegments = 4;
            const int latitudeSegments = 3;
            var recipe = CreateRecipe(VFXMeshShapeType.Hemisphere);
            recipe.shape.longitudeSegments = longitudeSegments;
            recipe.shape.latitudeSegments = latitudeSegments;
            recipe.shape.capEnd = true;
            recipe.uv.projection = VFXUVProjection.ShapeDefault;
            recipe.modifiers.Add(
                new VFXMeshModifierSettings
                {
                    type = VFXModifierType.Transform,
                    scale = new Vector3(1f, 0f, 1f)
                });

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var topPoleIndex =
                    latitudeSegments * (longitudeSegments + 1);
                var capCenterIndex = topPoleIndex + 1;
                var normals = result.mesh.normals;
                Assert.That(
                    Vector3.Dot(normals[topPoleIndex], Vector3.up),
                    Is.GreaterThan(0.99f));
                Assert.That(
                    Vector3.Dot(normals[capCenterIndex], Vector3.up),
                    Is.LessThan(-0.99f));
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
        public void MirroredArcReflectsTopologyWindingNormalsAndTransformedProjectedUVs()
        {
            var sourceRecipe = CreateRecipe(VFXMeshShapeType.Arc);
            sourceRecipe.shape.pivot = VFXPivot.Custom;
            sourceRecipe.shape.customPivotOffset = Vector3.zero;
            sourceRecipe.shape.innerRadius = 0.3f;
            sourceRecipe.shape.radius = 1.1f;
            sourceRecipe.shape.radialSegments = 8;
            sourceRecipe.shape.widthSegments = 3;
            sourceRecipe.shape.arcDegrees = 145f;
            sourceRecipe.shape.angleOffset = 19f;
            sourceRecipe.shape.radialElevationCurve = AnimationCurve.Linear(
                0f,
                0.2f,
                1f,
                0.45f);
            sourceRecipe.shape.arcWidthCurve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.55f));
            sourceRecipe.uv.projection = VFXUVProjection.AlongLength;
            sourceRecipe.uv.scale = new Vector2(-1.25f, 0.7f);
            sourceRecipe.uv.offset = new Vector2(0.23f, -0.16f);
            sourceRecipe.uv.rotation = 33f;
            sourceRecipe.uv.flipU = true;
            sourceRecipe.uv.flipV = true;
            sourceRecipe.uv.swapUV = true;

            var mirroredRecipe = sourceRecipe.DeepCopy();
            mirroredRecipe.shape.mirrorArcAcrossShapePlane = true;
            var sourceResult = VFXMeshBuilder.Build(sourceRecipe);
            var mirroredResult = VFXMeshBuilder.Build(mirroredRecipe);

            try
            {
                Assert.That(sourceResult.succeeded, Is.True, sourceResult.error);
                Assert.That(mirroredResult.succeeded, Is.True, mirroredResult.error);
                Assert.That(
                    mirroredResult.vertexCount,
                    Is.EqualTo(sourceResult.vertexCount * 2));
                Assert.That(
                    mirroredResult.triangleCount,
                    Is.EqualTo(sourceResult.triangleCount * 2));

                var sourceVertices = sourceResult.mesh.vertices;
                var sourceNormals = sourceResult.mesh.normals;
                var sourceUV = sourceResult.mesh.uv;
                var sourceTriangles = sourceResult.mesh.triangles;
                var mirroredVertices = mirroredResult.mesh.vertices;
                var mirroredNormals = mirroredResult.mesh.normals;
                var mirroredUV = mirroredResult.mesh.uv;
                var mirroredTriangles = mirroredResult.mesh.triangles;
                var shellVertexCount = sourceVertices.Length;
                var outerElevation =
                    sourceRecipe.shape.radialElevationCurve.Evaluate(1f);

                for (var vertex = 0; vertex < shellVertexCount; vertex++)
                {
                    var alignedSourceVertex =
                        sourceVertices[vertex] - Vector3.up * outerElevation;
                    Assert.That(
                        Vector3.Distance(mirroredVertices[vertex], alignedSourceVertex),
                        Is.LessThan(1e-6f),
                        $"Source-shell vertex {vertex} was not aligned to its outer rim.");
                    Assert.That(
                        Vector3.Distance(
                            mirroredVertices[shellVertexCount + vertex],
                            ReflectAcrossXZ(alignedSourceVertex)),
                        Is.LessThan(1e-6f),
                        $"Mirrored vertex {vertex} was not reflected across Y=0.");
                    Assert.That(
                        Vector2.Distance(mirroredUV[vertex], sourceUV[vertex]),
                        Is.LessThan(1e-6f),
                        $"Source-shell UV {vertex} changed when mirroring was enabled.");
                    Assert.That(
                        Vector2.Distance(
                            mirroredUV[shellVertexCount + vertex],
                            mirroredUV[vertex]),
                        Is.LessThan(1e-6f),
                        $"Mirrored UV {vertex} does not match its source after UV transforms.");
                    Assert.That(
                        Vector3.Distance(mirroredNormals[vertex], sourceNormals[vertex]),
                        Is.LessThan(1e-6f),
                        $"Source-shell normal {vertex} changed when mirroring was enabled.");
                    Assert.That(
                        Vector3.Distance(
                            mirroredNormals[shellVertexCount + vertex],
                            ReflectAcrossXZ(sourceNormals[vertex])),
                        Is.LessThan(1e-5f),
                        $"Mirrored normal {vertex} has the wrong reflected direction.");
                }

                Assert.That(
                    mirroredTriangles,
                    Has.Length.EqualTo(sourceTriangles.Length * 2));
                for (var triangle = 0; triangle < sourceTriangles.Length; triangle += 3)
                {
                    Assert.That(mirroredTriangles[triangle], Is.EqualTo(sourceTriangles[triangle]));
                    Assert.That(mirroredTriangles[triangle + 1], Is.EqualTo(sourceTriangles[triangle + 1]));
                    Assert.That(mirroredTriangles[triangle + 2], Is.EqualTo(sourceTriangles[triangle + 2]));

                    var mirroredTriangle = sourceTriangles.Length + triangle;
                    Assert.That(
                        mirroredTriangles[mirroredTriangle],
                        Is.EqualTo(sourceTriangles[triangle] + shellVertexCount));
                    Assert.That(
                        mirroredTriangles[mirroredTriangle + 1],
                        Is.EqualTo(sourceTriangles[triangle + 2] + shellVertexCount));
                    Assert.That(
                        mirroredTriangles[mirroredTriangle + 2],
                        Is.EqualTo(sourceTriangles[triangle + 1] + shellVertexCount));
                }

                var sourceFaceNormal = TriangleNormal(
                    mirroredVertices,
                    mirroredTriangles,
                    0);
                var mirroredFaceNormal = TriangleNormal(
                    mirroredVertices,
                    mirroredTriangles,
                    sourceTriangles.Length);
                Assert.That(Vector3.Dot(sourceFaceNormal, Vector3.up), Is.GreaterThan(0.5f));
                Assert.That(Vector3.Dot(mirroredFaceNormal, Vector3.down), Is.GreaterThan(0.5f));
                Assert.That(
                    Vector3.Distance(
                        mirroredFaceNormal,
                        ReflectAcrossXZ(sourceFaceNormal)),
                    Is.LessThan(1e-5f));
            }
            finally
            {
                DestroyResult(sourceResult);
                DestroyResult(mirroredResult);
            }
        }

        [Test]
        public void MirroredVariableWidthArcAnchorsOuterRimOnMirrorPlane()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.25f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.angleOffset = 17f;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.5f, 0.3f),
                new Keyframe(1f, 0f));
            recipe.shape.arcWidthCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 0.4f));
            recipe.shape.mirrorArcAcrossShapePlane = true;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var columnStride = recipe.shape.widthSegments + 1;
                var shellVertexCount =
                    (recipe.shape.radialSegments + 1) * columnStride;
                Assert.That(vertices, Has.Length.EqualTo(shellVertexCount * 2));

                for (var segment = 0; segment <= recipe.shape.radialSegments; segment++)
                {
                    var angularT = segment / (float)recipe.shape.radialSegments;
                    var angle = (
                        recipe.shape.angleOffset +
                        recipe.shape.arcDegrees * angularT) * Mathf.Deg2Rad;
                    var expectedOuter = new Vector3(
                        Mathf.Cos(angle) * recipe.shape.radius,
                        0f,
                        Mathf.Sin(angle) * recipe.shape.radius);
                    var sourceInner = segment * columnStride;
                    var sourceOuter = sourceInner + recipe.shape.widthSegments;
                    var mirroredInner = shellVertexCount + sourceInner;
                    var mirroredOuter = shellVertexCount + sourceOuter;

                    Assert.That(
                        Vector3.Distance(vertices[sourceOuter], expectedOuter),
                        Is.LessThan(1e-5f),
                        $"Source outer vertex {segment} moved away from the outer-rim anchor.");
                    Assert.That(
                        Vector3.Distance(vertices[mirroredOuter], expectedOuter),
                        Is.LessThan(1e-5f),
                        $"Mirrored outer vertex {segment} does not meet the source at Y=0.");
                    Assert.That(
                        Vector3.Distance(vertices[sourceOuter], vertices[mirroredOuter]),
                        Is.LessThan(1e-6f),
                        $"Outer-rim pair {segment} is split across the mirror plane.");

                    Assert.That(
                        vertices[sourceInner].y,
                        Is.GreaterThan(1e-4f),
                        $"Source inner vertex {segment} lost its elevation.");
                    Assert.That(
                        vertices[mirroredInner].y,
                        Is.EqualTo(-vertices[sourceInner].y).Within(1e-6f),
                        $"Inner pair {segment} is not reflected across Y=0.");
                    Assert.That(
                        Vector3.Distance(vertices[sourceInner], vertices[mirroredInner]),
                        Is.GreaterThan(1e-4f),
                        $"Inner pair {segment} lost the intended mirrored volume.");
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(VFXArcWidthOrigin.OuterRim, 0.6f, 1f)]
        [TestCase(VFXArcWidthOrigin.Middle, 0.4f, 0.8f)]
        [TestCase(VFXArcWidthOrigin.InnerRim, 0.2f, 0.6f)]
        public void ArcWidthOriginControlsTheScaledRadialInterval(
            VFXArcWidthOrigin origin,
            float expectedInnerRadius,
            float expectedOuterRadius)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 2;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.arcWidthOrigin = origin;
            recipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                Assert.That(
                    RadialDistance(vertices[0]),
                    Is.EqualTo(expectedInnerRadius).Within(1e-5f));
                Assert.That(
                    RadialDistance(vertices[recipe.shape.widthSegments]),
                    Is.EqualTo(expectedOuterRadius).Within(1e-5f));
                Assert.That(
                    expectedOuterRadius - expectedInnerRadius,
                    Is.EqualTo(
                        (recipe.shape.radius - recipe.shape.innerRadius) * 0.5f)
                        .Within(1e-6f));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(VFXArcWidthOrigin.OuterRim, 1f)]
        [TestCase(VFXArcWidthOrigin.Middle, 0.6f)]
        [TestCase(VFXArcWidthOrigin.InnerRim, 0.2f)]
        public void CollapsedArcTipUsesTheSelectedWidthOrigin(
            VFXArcWidthOrigin origin,
            float expectedRadius)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 2;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.arcWidthOrigin = origin;
            recipe.shape.arcWidthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    RadialDistance(result.mesh.vertices[0]),
                    Is.EqualTo(expectedRadius).Within(1e-5f));
                Assert.That(
                    result.validationFlags & VFXMeshValidationFlags.DegenerateTriangle,
                    Is.EqualTo(VFXMeshValidationFlags.None));
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [TestCase(VFXArcWidthOrigin.OuterRim)]
        [TestCase(VFXArcWidthOrigin.Middle)]
        [TestCase(VFXArcWidthOrigin.InnerRim)]
        public void MirroredArcOriginsKeepOuterRimJoinedWithNonzeroOuterElevation(
            VFXArcWidthOrigin origin)
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.arcWidthOrigin = origin;
            recipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);
            recipe.shape.radialElevationCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 0.25f);
            recipe.shape.mirrorArcAcrossShapePlane = true;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var columnStride = recipe.shape.widthSegments + 1;
                var shellVertexCount =
                    (recipe.shape.radialSegments + 1) * columnStride;
                Assert.That(vertices, Has.Length.EqualTo(shellVertexCount * 2));

                for (var segment = 0; segment <= recipe.shape.radialSegments; segment++)
                {
                    var sourceInner = segment * columnStride;
                    var sourceOuter = sourceInner + recipe.shape.widthSegments;
                    var mirroredInner = shellVertexCount + sourceInner;
                    var mirroredOuter = shellVertexCount + sourceOuter;

                    Assert.That(vertices[sourceOuter].y, Is.EqualTo(0f).Within(1e-6f));
                    Assert.That(
                        vertices[sourceInner].y,
                        Is.EqualTo(0.175f).Within(1e-6f),
                        $"Origin {origin} changed the outer-anchored elevation profile.");
                    Assert.That(
                        Vector3.Distance(vertices[sourceOuter], vertices[mirroredOuter]),
                        Is.LessThan(1e-6f),
                        $"Origin {origin}, outer-rim pair {segment} is split.");
                    Assert.That(
                        vertices[mirroredInner].y,
                        Is.EqualTo(-vertices[sourceInner].y).Within(1e-6f));
                    Assert.That(
                        Vector2.Distance(
                            result.mesh.uv[sourceInner],
                            result.mesh.uv[mirroredInner]),
                        Is.LessThan(1e-6f));
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void MirroredClosedArcDoubleSidedSmoothsEveryAngularSeam()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.15f;
            recipe.shape.radialSegments = 12;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 360f;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.45f, 0.55f),
                new Keyframe(1f, 0.3f));
            recipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            recipe.shape.mirrorArcAcrossShapePlane = true;
            recipe.output.doubleSided = true;

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                var vertices = result.mesh.vertices;
                var normals = result.mesh.normals;
                var rowStride = recipe.shape.radialSegments + 1;
                var shellVertexCount = (recipe.shape.widthSegments + 1) * rowStride;
                Assert.That(vertices, Has.Length.EqualTo(shellVertexCount * 4));

                for (var shell = 0; shell < 4; shell++)
                {
                    var shellOffset = shell * shellVertexCount;
                    for (var row = 0; row <= recipe.shape.widthSegments; row++)
                    {
                        var first = shellOffset + row * rowStride;
                        var last = first + recipe.shape.radialSegments;
                        Assert.That(
                            Vector3.Distance(vertices[first], vertices[last]),
                            Is.LessThan(1e-5f),
                            $"Shell {shell}, radial row {row} has an open position seam.");
                        Assert.That(
                            Vector3.Distance(normals[first], normals[last]),
                            Is.LessThan(1e-6f),
                            $"Shell {shell}, radial row {row} has a normal seam.");
                    }
                }
            }
            finally
            {
                DestroyResult(result);
            }
        }

        [Test]
        public void ArcWidthCurveAnchorsCollapsedTipsToOuterRimAndSamplesMiddleWidth()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.25f;
            recipe.shape.radius = 1.25f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 3;
            recipe.shape.arcDegrees = 160f;
            recipe.shape.radialElevationCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.5f, 0.25f),
                new Keyframe(1f, 0f));
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

                Assert.That(
                    RadialDistance(vertices[startIndices[0]]),
                    Is.EqualTo(recipe.shape.radius).Within(1e-5f));
                Assert.That(
                    RadialDistance(vertices[endIndices[0]]),
                    Is.EqualTo(recipe.shape.radius).Within(1e-5f));
                Assert.That(vertices[startIndices[0]].y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(vertices[endIndices[0]].y, Is.EqualTo(0f).Within(1e-5f));

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

        [TestCase(VFXMeshShapeType.Disc)]
        [TestCase(VFXMeshShapeType.Ring)]
        [TestCase(VFXMeshShapeType.Arc)]
        public void NullAndLinearRadialDistributionPreserveLegacyGeometry(
            VFXMeshShapeType shapeType)
        {
            var nullCurveRecipe = CreateRecipe(shapeType);
            nullCurveRecipe.shape.pivot = VFXPivot.Custom;
            nullCurveRecipe.shape.customPivotOffset = Vector3.zero;
            nullCurveRecipe.shape.radialDistributionCurve = null;

            var linearCurveRecipe = nullCurveRecipe.DeepCopy();
            linearCurveRecipe.shape.radialDistributionCurve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var nullCurveResult = VFXMeshBuilder.Build(nullCurveRecipe);
            var linearCurveResult = VFXMeshBuilder.Build(linearCurveRecipe);
            try
            {
                Assert.That(nullCurveResult.succeeded, Is.True, nullCurveResult.error);
                Assert.That(linearCurveResult.succeeded, Is.True, linearCurveResult.error);
                CollectionAssert.AreEqual(
                    nullCurveResult.mesh.triangles,
                    linearCurveResult.mesh.triangles);

                var nullVertices = nullCurveResult.mesh.vertices;
                var linearVertices = linearCurveResult.mesh.vertices;
                var nullUV = nullCurveResult.mesh.uv;
                var linearUV = linearCurveResult.mesh.uv;
                Assert.That(linearVertices, Has.Length.EqualTo(nullVertices.Length));
                for (var vertex = 0; vertex < nullVertices.Length; vertex++)
                {
                    Assert.That(
                        Vector3.Distance(nullVertices[vertex], linearVertices[vertex]),
                        Is.LessThan(1e-6f));
                    Assert.That(
                        Vector2.Distance(nullUV[vertex], linearUV[vertex]),
                        Is.LessThan(1e-6f));
                }
            }
            finally
            {
                DestroyResult(nullCurveResult);
                DestroyResult(linearCurveResult);
            }
        }

        [Test]
        public void VariableWidthArcUsesDistributedRadialRows()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.pivot = VFXPivot.Custom;
            recipe.shape.customPivotOffset = Vector3.zero;
            recipe.shape.innerRadius = 0.2f;
            recipe.shape.radius = 1f;
            recipe.shape.radialSegments = 8;
            recipe.shape.widthSegments = 2;
            recipe.shape.arcWidthOrigin = VFXArcWidthOrigin.OuterRim;
            recipe.shape.arcWidthCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);
            recipe.shape.radialDistributionCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 1f));

            var result = VFXMeshBuilder.Build(recipe);
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(
                    RadialDistance(result.mesh.vertices[1]),
                    Is.EqualTo(0.92f).Within(1e-5f));
                Assert.That(result.mesh.uv[1].y, Is.EqualTo(0.8f).Within(1e-5f));
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
        public void DeepCopyPreservesRadialDistributionCurve()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Disc);
            recipe.shape.radialDistributionCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 0.72f),
                new Keyframe(1f, 1f));

            var copy = recipe.DeepCopy();

            Assert.That(copy.shape.radialDistributionCurve, Is.Not.Null);
            Assert.That(
                copy.shape.radialDistributionCurve.Evaluate(0.35f),
                Is.EqualTo(0.72f).Within(1e-5f));
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
        public void DeepCopyPreservesMirrorArcAcrossShapePlane()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.mirrorArcAcrossShapePlane = true;

            var copy = recipe.DeepCopy();

            Assert.That(copy.shape.mirrorArcAcrossShapePlane, Is.True);
        }

        [Test]
        public void ShapeSpecificUVAndArcOriginDefaultsRemainBackwardCompatible()
        {
            var settings = new VFXShapeSettings();

            Assert.That((int)VFXArcWidthOrigin.OuterRim, Is.EqualTo(0));
            Assert.That((int)VFXArcWidthOrigin.Middle, Is.EqualTo(1));
            Assert.That((int)VFXArcWidthOrigin.InnerRim, Is.EqualTo(2));
            Assert.That(settings.arcWidthOrigin, Is.EqualTo(VFXArcWidthOrigin.OuterRim));
            Assert.That(
                settings.ribbonUVWidthMode,
                Is.EqualTo(VFXRibbonUVWidthMode.PreserveTexelDensity));
        }

        [Test]
        public void DeepCopyPreservesArcWidthOriginAndRibbonUVWidthMode()
        {
            var recipe = CreateRecipe(VFXMeshShapeType.Arc);
            recipe.shape.arcWidthOrigin = VFXArcWidthOrigin.InnerRim;
            recipe.shape.ribbonUVWidthMode = VFXRibbonUVWidthMode.StretchToWidth;

            var copy = recipe.DeepCopy();

            Assert.That(copy.shape.arcWidthOrigin, Is.EqualTo(VFXArcWidthOrigin.InnerRim));
            Assert.That(
                copy.shape.ribbonUVWidthMode,
                Is.EqualTo(VFXRibbonUVWidthMode.StretchToWidth));
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
                Assert.That(material.HasProperty("_CheckerTexture"), Is.True);
                Assert.That(material.HasProperty("_UseCheckerTexture"), Is.True);
                Assert.That(material.GetColor("_BackfaceColor").a, Is.EqualTo(1f).Within(1e-6f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void PreviewControllerBindsAndClearsCustomCheckerTexture()
        {
            var previewControllerType = typeof(VFXMeshBuilder).Assembly.GetType(
                "PudinKiller.VFXMeshGenerator.Editor.VFXMeshPreviewController",
                true);
            var constructor = previewControllerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            var applyCheckerTexture = previewControllerType.GetMethod(
                "ApplyCheckerTexture",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var frontMaterialField = previewControllerType.GetField(
                "frontMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(applyCheckerTexture, Is.Not.Null);
            Assert.That(frontMaterialField, Is.Not.Null);

            object controller = null;
            Texture2D checkerTexture = null;
            try
            {
                controller = constructor.Invoke(Array.Empty<object>());
                checkerTexture = new Texture2D(2, 2);
                applyCheckerTexture.Invoke(controller, new object[] { checkerTexture });

                var frontMaterial = frontMaterialField.GetValue(controller) as Material;
                Assert.That(frontMaterial, Is.Not.Null);
                Assert.That(
                    frontMaterial.GetTexture("_CheckerTexture"),
                    Is.SameAs(checkerTexture));
                Assert.That(
                    frontMaterial.GetFloat("_UseCheckerTexture"),
                    Is.EqualTo(1f));

                applyCheckerTexture.Invoke(controller, new object[] { null });
                Assert.That(frontMaterial.GetTexture("_CheckerTexture"), Is.Null);
                Assert.That(
                    frontMaterial.GetFloat("_UseCheckerTexture"),
                    Is.EqualTo(0f));
            }
            finally
            {
                if (controller is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                if (checkerTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(checkerTexture);
                }
            }
        }

        [Test]
        public void ResetCurrentShapeProfilePreservesOtherProfilesAndRecipeSettings()
        {
            var windowType = typeof(VFXMeshGeneratorWindow);
            var switchShape = windowType.GetMethod(
                "SwitchShape",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resetShape = windowType.GetMethod(
                "ResetCurrentShapeProfile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var recipeField = windowType.GetField(
                "recipe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var shapeSettingsField = windowType.GetField(
                "shapeSettingsByType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentStateKeyField = windowType.GetField(
                "persistentStateKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(switchShape, Is.Not.Null);
            Assert.That(resetShape, Is.Not.Null);
            Assert.That(recipeField, Is.Not.Null);
            Assert.That(shapeSettingsField, Is.Not.Null);
            Assert.That(persistentStateKeyField, Is.Not.Null);

            VFXMeshGeneratorWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<VFXMeshGeneratorWindow>();
                persistentStateKeyField.SetValue(
                    window,
                    "com.pudinkiller.vfx-mesh-generator.reset-test." +
                    Guid.NewGuid().ToString("N"));
                var recipe = new VFXMeshRecipe
                {
                    meshName = "Keep Recipe Settings"
                };
                recipe.modifiers.Add(new VFXMeshModifierSettings());
                recipe.uv.scale = new Vector2(2f, 3f);
                recipe.output.doubleSided = true;
                recipeField.SetValue(window, recipe);
                shapeSettingsField.SetValue(
                    window,
                    Activator.CreateInstance(shapeSettingsField.FieldType));

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Cylinder });
                recipe.shape.radius = 2.4f;
                recipe.shape.height = 3.2f;
                recipe.shape.widthCurve =
                    AnimationCurve.Linear(0f, 0.4f, 1f, 1.3f);

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Arc });
                recipe.shape.radius = 4f;
                recipe.shape.arcDegrees = 73f;
                recipe.shape.mirrorArcAcrossShapePlane = true;
                recipe.shape.radialDistributionCurve =
                    AnimationCurve.Linear(0f, 0.8f, 1f, 0.2f);

                resetShape.Invoke(window, null);

                Assert.That(recipe.shapeType, Is.EqualTo(VFXMeshShapeType.Arc));
                Assert.That(recipe.shape.radius, Is.EqualTo(0.5f));
                Assert.That(recipe.shape.arcDegrees, Is.EqualTo(180f));
                Assert.That(recipe.shape.mirrorArcAcrossShapePlane, Is.False);
                Assert.That(
                    recipe.shape.radialDistributionCurve.Evaluate(0.5f),
                    Is.EqualTo(0.5f).Within(1e-5f));
                Assert.That(recipe.meshName, Is.EqualTo("Keep Recipe Settings"));
                Assert.That(recipe.modifiers, Has.Count.EqualTo(1));
                Assert.That(recipe.uv.scale, Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(recipe.output.doubleSided, Is.True);

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Cylinder });
                Assert.That(recipe.shape.radius, Is.EqualTo(2.4f));
                Assert.That(recipe.shape.height, Is.EqualTo(3.2f));
                Assert.That(
                    recipe.shape.widthCurve.Evaluate(0f),
                    Is.EqualTo(0.4f).Within(1e-5f));
            }
            finally
            {
                if (window != null)
                {
                    persistentStateKeyField.SetValue(window, null);
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        [Test]
        public void WindowPersistsIndependentShapeSettingsAndAdoptsOnlyPresetShape()
        {
            var windowType = typeof(VFXMeshGeneratorWindow);
            var buildStateKey = windowType.GetMethod(
                "BuildPersistentStateKey",
                BindingFlags.Static | BindingFlags.NonPublic);
            var switchShape = windowType.GetMethod(
                "SwitchShape",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var recipeField = windowType.GetField(
                "recipe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var shapeSettingsField = windowType.GetField(
                "shapeSettingsByType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentStateKeyField = windowType.GetField(
                "persistentStateKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var adoptRecipe = windowType.GetMethod(
                "AdoptRecipe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var savePersistentState = windowType.GetMethod(
                "SavePersistentState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var loadPersistentState = windowType.GetMethod(
                "LoadPersistentState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ensureRecipeState = windowType.GetMethod(
                "EnsureRecipeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var normalizeShapeSettingsMemory = windowType.GetMethod(
                "NormalizeShapeSettingsMemory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildStateKey, Is.Not.Null);
            Assert.That(switchShape, Is.Not.Null);
            Assert.That(recipeField, Is.Not.Null);
            Assert.That(shapeSettingsField, Is.Not.Null);
            Assert.That(persistentStateKeyField, Is.Not.Null);
            Assert.That(adoptRecipe, Is.Not.Null);
            Assert.That(savePersistentState, Is.Not.Null);
            Assert.That(loadPersistentState, Is.Not.Null);
            Assert.That(ensureRecipeState, Is.Not.Null);
            Assert.That(normalizeShapeSettingsMemory, Is.Not.Null);

            var stateKey =
                (buildStateKey.Invoke(null, null) as string) +
                ".test." +
                Guid.NewGuid().ToString("N");
            VFXMeshGeneratorWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<VFXMeshGeneratorWindow>();
                persistentStateKeyField.SetValue(window, stateKey);
                recipeField.SetValue(window, new VFXMeshRecipe());
                shapeSettingsField.SetValue(
                    window,
                    Activator.CreateInstance(shapeSettingsField.FieldType));
                var activeRecipe = recipeField.GetValue(window) as VFXMeshRecipe;
                Assert.That(activeRecipe, Is.Not.Null);

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Arc });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(180f));
                activeRecipe.shape.arcDegrees = 137f;
                activeRecipe.shape.radius = 1.7f;
                activeRecipe.shape.capEnd = false;
                activeRecipe.shape.arcWidthOrigin = VFXArcWidthOrigin.Middle;

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Cylinder });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(360f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(0.5f));
                Assert.That(activeRecipe.shape.capEnd, Is.True);
                activeRecipe.shape.arcDegrees = 271f;
                activeRecipe.shape.radius = 2.4f;

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Arc });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(137f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(1.7f));
                Assert.That(activeRecipe.shape.capEnd, Is.False);
                Assert.That(
                    activeRecipe.shape.arcWidthOrigin,
                    Is.EqualTo(VFXArcWidthOrigin.Middle));

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Cylinder });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(271f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(2.4f));

                var arcPresetRecipe = new VFXMeshRecipe
                {
                    shapeType = VFXMeshShapeType.Arc,
                    shape = new VFXShapeSettings
                    {
                        arcDegrees = 212f,
                        radius = 3.6f,
                        arcWidthOrigin = VFXArcWidthOrigin.InnerRim
                    }
                };
                adoptRecipe.Invoke(window, new object[] { arcPresetRecipe });
                activeRecipe = recipeField.GetValue(window) as VFXMeshRecipe;
                Assert.That(activeRecipe.shapeType, Is.EqualTo(VFXMeshShapeType.Arc));
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(212f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(3.6f));
                Assert.That(
                    activeRecipe.shape.arcWidthOrigin,
                    Is.EqualTo(VFXArcWidthOrigin.InnerRim));

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Cylinder });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(271f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(2.4f));

                savePersistentState.Invoke(window, null);
                UnityEngine.Object.DestroyImmediate(window);
                window = ScriptableObject.CreateInstance<VFXMeshGeneratorWindow>();
                persistentStateKeyField.SetValue(window, stateKey);
                loadPersistentState.Invoke(window, null);
                ensureRecipeState.Invoke(window, null);
                normalizeShapeSettingsMemory.Invoke(window, null);
                activeRecipe = recipeField.GetValue(window) as VFXMeshRecipe;
                Assert.That(activeRecipe.shapeType, Is.EqualTo(VFXMeshShapeType.Cylinder));
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(271f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(2.4f));

                switchShape.Invoke(window, new object[] { VFXMeshShapeType.Arc });
                Assert.That(activeRecipe.shape.arcDegrees, Is.EqualTo(212f));
                Assert.That(activeRecipe.shape.radius, Is.EqualTo(3.6f));
                Assert.That(
                    activeRecipe.shape.arcWidthOrigin,
                    Is.EqualTo(VFXArcWidthOrigin.InnerRim));
            }
            finally
            {
                if (window != null)
                {
                    persistentStateKeyField.SetValue(window, null);
                    UnityEngine.Object.DestroyImmediate(window);
                }

                EditorPrefs.DeleteKey(stateKey);
            }
        }

        [Test]
        public void ShadedWireframeMaterialDepthTestsWithoutForwardBias()
        {
            var previewControllerType = typeof(VFXMeshBuilder).Assembly.GetType(
                "PudinKiller.VFXMeshGenerator.Editor.VFXMeshPreviewController",
                true);
            var constructor = previewControllerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(constructor, Is.Not.Null);

            object controller = null;
            try
            {
                controller = constructor.Invoke(Array.Empty<object>());
                var wireMaterialField = previewControllerType.GetField(
                    "wireMaterial",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(wireMaterialField, Is.Not.Null);

                var wireMaterial = wireMaterialField.GetValue(controller) as Material;
                Assert.That(wireMaterial, Is.Not.Null);
                Assert.That(wireMaterial.GetFloat("_WirePass"), Is.EqualTo(1f));
                Assert.That(wireMaterial.GetFloat("_WireDepthBias"), Is.EqualTo(0f));
                Assert.That(wireMaterial.GetFloat("_ZWrite"), Is.EqualTo(0f));
                Assert.That(
                    wireMaterial.renderQueue,
                    Is.EqualTo((int)RenderQueue.Geometry + 1));
            }
            finally
            {
                if (controller is IDisposable disposable)
                {
                    disposable.Dispose();
                }
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

        private static int AssertCoincidentNormalGroupsAreSmooth(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<Vector3> normals)
        {
            Assert.That(normals.Count, Is.EqualTo(vertices.Count));
            var assigned = new bool[vertices.Count];
            var duplicateGroupCount = 0;
            for (var first = 0; first < vertices.Count; first++)
            {
                if (assigned[first])
                {
                    continue;
                }

                assigned[first] = true;
                var duplicateCount = 0;
                for (var candidate = first + 1;
                     candidate < vertices.Count;
                     candidate++)
                {
                    if (assigned[candidate] ||
                        Vector3.Distance(
                            vertices[first],
                            vertices[candidate]) > 1e-6f)
                    {
                        continue;
                    }

                    assigned[candidate] = true;
                    duplicateCount++;
                    Assert.That(
                        Vector3.Distance(normals[first], normals[candidate]),
                        Is.LessThan(1e-5f),
                        $"Coincident vertices {first} and {candidate} have a normal seam.");
                }

                if (duplicateCount > 0)
                {
                    duplicateGroupCount++;
                }
            }

            return duplicateGroupCount;
        }

        private static float MaximumCrossSectionExtentAtY(
            IReadOnlyList<Vector3> vertices,
            float y)
        {
            var maximum = float.NegativeInfinity;
            for (var vertex = 0; vertex < vertices.Count; vertex++)
            {
                if (Mathf.Abs(vertices[vertex].y - y) > 1e-5f)
                {
                    continue;
                }

                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        Mathf.Abs(vertices[vertex].x),
                        Mathf.Abs(vertices[vertex].z)));
            }

            return maximum;
        }

        private static Vector3 ReflectAcrossXZ(Vector3 value)
        {
            return new Vector3(value.x, -value.y, value.z);
        }

        private static int FindCoincidentPoleCorner(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles,
            int triangle,
            IReadOnlyList<Vector3> polePositions,
            out int poleGroup)
        {
            for (var corner = 0; corner < 3; corner++)
            {
                var position = vertices[triangles[triangle + corner]];
                for (var group = 0; group < polePositions.Count; group++)
                {
                    if (Vector3.Distance(position, polePositions[group]) <= 1e-6f)
                    {
                        poleGroup = group;
                        return corner;
                    }
                }
            }

            poleGroup = -1;
            return -1;
        }

        private static Vector3 TriangleNormal(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles,
            int triangle)
        {
            var first = vertices[triangles[triangle]];
            var second = vertices[triangles[triangle + 1]];
            var third = vertices[triangles[triangle + 2]];
            return Vector3.Cross(second - first, third - first).normalized;
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
