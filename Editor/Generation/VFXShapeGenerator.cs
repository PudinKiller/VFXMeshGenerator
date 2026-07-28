using System;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    /// <summary>
    /// Builds the base topology and shape-default UV0 data for every supported mesh type.
    /// Shapes are authored around a canonical Y axis, then oriented and pivoted as a final step.
    /// </summary>
    public static class VFXShapeGenerator
    {
        private const float MinimumDimension = 0.0001f;
        private const float FullArcThreshold = 359.999f;

        public static void Generate(VFXMeshRecipe recipe, VFXMeshDraft draft)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            Clear(draft);
            var shape = recipe.shape ?? new VFXShapeSettings();

            switch (recipe.shapeType)
            {
                case VFXMeshShapeType.Quad:
                    GenerateQuad(shape, draft);
                    break;
                case VFXMeshShapeType.Disc:
                    GenerateDisc(shape, draft);
                    break;
                case VFXMeshShapeType.Ring:
                    GenerateRing(shape, draft, false);
                    break;
                case VFXMeshShapeType.Arc:
                    GenerateArc(shape, draft);
                    break;
                case VFXMeshShapeType.Cone:
                    GenerateFrustum(shape, draft, Positive(shape.radius), NonNegative(shape.topRadius));
                    break;
                case VFXMeshShapeType.Cylinder:
                    GenerateFrustum(shape, draft, Positive(shape.radius), Positive(shape.radius));
                    break;
                case VFXMeshShapeType.Tube:
                    GenerateTube(shape, draft);
                    break;
                case VFXMeshShapeType.Sphere:
                    GenerateSphere(shape, draft);
                    break;
                case VFXMeshShapeType.Hemisphere:
                    GenerateHemisphere(shape, draft);
                    break;
                case VFXMeshShapeType.Torus:
                    GenerateTorus(shape, draft);
                    break;
                case VFXMeshShapeType.Box:
                    GenerateBox(shape, draft);
                    break;
                case VFXMeshShapeType.Ribbon:
                    GenerateRibbon(shape, draft);
                    break;
                case VFXMeshShapeType.CrossPlanes:
                    GenerateCrossPlanes(shape, draft);
                    break;
                case VFXMeshShapeType.Helix:
                    GenerateHelix(shape, draft);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(recipe.shapeType),
                        recipe.shapeType,
                        "Unsupported VFX mesh shape type.");
            }

            OrientAndPivot(shape, draft);
        }

        private static void GenerateQuad(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var width = Positive(shape.width);
            var length = Positive(shape.length);
            var widthSegments = Segments(shape.widthSegments, 1);
            var lengthSegments = Segments(shape.lengthSegments, 1);

            for (var y = 0; y <= lengthSegments; y++)
            {
                var v = y / (float)lengthSegments;
                var z = Mathf.Lerp(-length * 0.5f, length * 0.5f, v);
                var rowWidth = width * EvaluateWidth(shape, v);

                for (var x = 0; x <= widthSegments; x++)
                {
                    var u = x / (float)widthSegments;
                    draft.AddVertex(
                        new Vector3(Mathf.Lerp(-rowWidth * 0.5f, rowWidth * 0.5f, u), 0f, z),
                        new Vector2(u, v));
                }
            }

            var stride = widthSegments + 1;
            for (var y = 0; y < lengthSegments; y++)
            {
                for (var x = 0; x < widthSegments; x++)
                {
                    var a = y * stride + x;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;
                    draft.AddTriangle(a, c, b);
                    draft.AddTriangle(b, c, d);
                }
            }
        }

        private static void GenerateDisc(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            GenerateDisc(shape, draft, 360f, false);
        }

        private static void GenerateDisc(
            VFXShapeSettings shape,
            VFXMeshDraft draft,
            float requestedArcDegrees,
            bool applyRadialElevation)
        {
            var radialSegments = Segments(shape.radialSegments, 3);
            var radiusSegments = Segments(shape.widthSegments, 1);
            var radius = Positive(shape.radius);
            var arcDegrees = SanitizeArcDegrees(requestedArcDegrees);
            var angleOffset = Degrees(shape.angleOffset);
            var arcRadians = Degrees(arcDegrees);
            var positiveWinding = arcRadians >= 0f;

            var centerElevation = applyRadialElevation
                ? EvaluateRadialElevation(shape, 0f)
                : 0f;
            var center = draft.AddVertex(
                new Vector3(0f, centerElevation, 0f),
                new Vector2(0.5f, 0.5f));
            var previousRing = -1;

            for (var ring = 1; ring <= radiusSegments; ring++)
            {
                var radialT = ring / (float)radiusSegments;
                var ringRadius = radius * radialT;
                var elevation = applyRadialElevation
                    ? EvaluateRadialElevation(shape, radialT)
                    : 0f;
                var ringStart = draft.vertices.Count;

                for (var segment = 0; segment <= radialSegments; segment++)
                {
                    var angularT = segment / (float)radialSegments;
                    var angle = angleOffset + arcRadians * angularT;
                    var cosine = Mathf.Cos(angle);
                    var sine = Mathf.Sin(angle);
                    draft.AddVertex(
                        new Vector3(cosine * ringRadius, elevation, sine * ringRadius),
                        new Vector2(0.5f + cosine * radialT * 0.5f, 0.5f + sine * radialT * 0.5f));
                }

                if (ring == 1)
                {
                    for (var segment = 0; segment < radialSegments; segment++)
                    {
                        var current = ringStart + segment;
                        var next = current + 1;
                        if (positiveWinding)
                        {
                            draft.AddTriangle(center, next, current);
                        }
                        else
                        {
                            draft.AddTriangle(center, current, next);
                        }
                    }
                }
                else
                {
                    AddRadialStrip(
                        draft,
                        previousRing,
                        ringStart,
                        radialSegments,
                        positiveWinding);
                }

                previousRing = ringStart;
            }
        }

        private static void GenerateArc(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var angularSegments = Segments(shape.radialSegments, 3);
            if (!UsesVariableArcWidthTopology(shape, angularSegments))
            {
                GenerateRing(shape, draft, true);
                return;
            }

            GenerateVariableWidthArc(shape, draft, angularSegments);
        }

        private static void GenerateRing(
            VFXShapeSettings shape,
            VFXMeshDraft draft,
            bool partialArc)
        {
            var radialSegments = Segments(shape.radialSegments, 3);
            var radiusSegments = Segments(shape.widthSegments, 1);
            var firstRadius = NonNegative(shape.innerRadius);
            var secondRadius = Positive(shape.radius);
            var innerRadius = Mathf.Min(firstRadius, secondRadius);
            var outerRadius = Mathf.Max(firstRadius, secondRadius);
            var arcDegrees = partialArc ? SanitizeArcDegrees(shape.arcDegrees) : 360f;
            if (outerRadius - innerRadius < MinimumDimension)
            {
                outerRadius = innerRadius + MinimumDimension;
            }

            if (innerRadius < MinimumDimension)
            {
                GenerateDisc(shape, draft, arcDegrees, true);
                return;
            }

            var arcRadians = Degrees(arcDegrees);
            var angleOffset = Degrees(shape.angleOffset);
            var positiveWinding = arcRadians >= 0f;
            var stride = radialSegments + 1;

            for (var ring = 0; ring <= radiusSegments; ring++)
            {
                var radialT = ring / (float)radiusSegments;
                var ringRadius = Mathf.Lerp(innerRadius, outerRadius, radialT);
                var elevation = EvaluateRadialElevation(shape, radialT);

                for (var segment = 0; segment <= radialSegments; segment++)
                {
                    var angularT = segment / (float)radialSegments;
                    var angle = angleOffset + arcRadians * angularT;
                    draft.AddVertex(
                        new Vector3(
                            Mathf.Cos(angle) * ringRadius,
                            elevation,
                            Mathf.Sin(angle) * ringRadius),
                        new Vector2(angularT, radialT));
                }
            }

            for (var ring = 0; ring < radiusSegments; ring++)
            {
                AddRadialStrip(
                    draft,
                    ring * stride,
                    (ring + 1) * stride,
                    radialSegments,
                    positiveWinding);
            }
        }

        private static void GenerateVariableWidthArc(
            VFXShapeSettings shape,
            VFXMeshDraft draft,
            int angularSegments)
        {
            var radiusSegments = Segments(shape.widthSegments, 1);
            var firstRadius = NonNegative(shape.innerRadius);
            var secondRadius = Positive(shape.radius);
            var innerRadius = Mathf.Min(firstRadius, secondRadius);
            var outerRadius = Mathf.Max(firstRadius, secondRadius);
            if (outerRadius - innerRadius < MinimumDimension)
            {
                outerRadius = innerRadius + MinimumDimension;
            }

            innerRadius = Mathf.Max(MinimumDimension, innerRadius);
            var centerRadius = (innerRadius + outerRadius) * 0.5f;
            var halfWidth = (outerRadius - innerRadius) * 0.5f;
            var middleElevation = EvaluateRadialElevation(shape, 0.5f);
            var arcRadians = Degrees(SanitizeArcDegrees(shape.arcDegrees));
            var angleOffset = Degrees(shape.angleOffset);
            var positiveWinding = arcRadians >= 0f;
            var starts = new int[angularSegments + 1];
            var collapsed = new bool[angularSegments + 1];

            for (var segment = 0; segment <= angularSegments; segment++)
            {
                var angularT = segment / (float)angularSegments;
                var widthFactor = EvaluateArcWidth(shape, angularT);
                var angle = angleOffset + arcRadians * angularT;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                starts[segment] = draft.vertices.Count;
                collapsed[segment] = widthFactor <= MinimumDimension;

                if (collapsed[segment])
                {
                    draft.AddVertex(
                        new Vector3(
                            cosine * centerRadius,
                            middleElevation,
                            sine * centerRadius),
                        new Vector2(angularT, 0.5f));
                    continue;
                }

                var currentHalfWidth = halfWidth * widthFactor;
                for (var ring = 0; ring <= radiusSegments; ring++)
                {
                    var radialT = ring / (float)radiusSegments;
                    var ringRadius = Mathf.Lerp(
                        centerRadius - currentHalfWidth,
                        centerRadius + currentHalfWidth,
                        radialT);
                    var elevation = Mathf.Lerp(
                        middleElevation,
                        EvaluateRadialElevation(shape, radialT),
                        widthFactor);
                    draft.AddVertex(
                        new Vector3(
                            cosine * ringRadius,
                            elevation,
                            sine * ringRadius),
                        new Vector2(angularT, radialT));
                }
            }

            for (var segment = 0; segment < angularSegments; segment++)
            {
                AddVariableArcStrip(
                    draft,
                    starts[segment],
                    collapsed[segment],
                    starts[segment + 1],
                    collapsed[segment + 1],
                    radiusSegments,
                    positiveWinding);
            }
        }

        private static void AddVariableArcStrip(
            VFXMeshDraft draft,
            int currentStart,
            bool currentCollapsed,
            int nextStart,
            bool nextCollapsed,
            int radiusSegments,
            bool positiveWinding)
        {
            if (currentCollapsed && nextCollapsed)
            {
                return;
            }

            for (var ring = 0; ring < radiusSegments; ring++)
            {
                if (currentCollapsed)
                {
                    if (positiveWinding)
                    {
                        draft.AddTriangle(currentStart, nextStart + ring, nextStart + ring + 1);
                    }
                    else
                    {
                        draft.AddTriangle(currentStart, nextStart + ring + 1, nextStart + ring);
                    }
                }
                else if (nextCollapsed)
                {
                    if (positiveWinding)
                    {
                        draft.AddTriangle(currentStart + ring, nextStart, currentStart + ring + 1);
                    }
                    else
                    {
                        draft.AddTriangle(currentStart + ring, currentStart + ring + 1, nextStart);
                    }
                }
                else
                {
                    var a = currentStart + ring;
                    var b = a + 1;
                    var c = nextStart + ring;
                    var d = c + 1;
                    if (positiveWinding)
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                    else
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }
                }
            }
        }

        internal static bool UsesVariableArcWidthTopology(VFXShapeSettings shape)
        {
            return UsesVariableArcWidthTopology(
                shape,
                Segments(shape == null ? 0 : shape.radialSegments, 3));
        }

        private static bool UsesVariableArcWidthTopology(
            VFXShapeSettings shape,
            int angularSegments)
        {
            for (var segment = 0; segment <= angularSegments; segment++)
            {
                if (Mathf.Abs(EvaluateArcWidth(shape, segment / (float)angularSegments) - 1f) >
                    MinimumDimension)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRadialStrip(
            VFXMeshDraft draft,
            int innerStart,
            int outerStart,
            int radialSegments,
            bool positiveWinding)
        {
            for (var segment = 0; segment < radialSegments; segment++)
            {
                var a = innerStart + segment;
                var b = outerStart + segment;
                var c = a + 1;
                var d = b + 1;

                if (positiveWinding)
                {
                    draft.AddTriangle(a, c, b);
                    draft.AddTriangle(b, c, d);
                }
                else
                {
                    draft.AddTriangle(a, b, c);
                    draft.AddTriangle(b, d, c);
                }
            }
        }

        private static void GenerateFrustum(
            VFXShapeSettings shape,
            VFXMeshDraft draft,
            float bottomRadius,
            float topRadius)
        {
            var radialSegments = Segments(shape.radialSegments, 3);
            var heightSegments = Segments(shape.heightSegments, 1);
            var height = Positive(shape.height);
            var angleOffset = Degrees(shape.angleOffset);
            var arcRadians = Degrees(SanitizeArcDegrees(shape.arcDegrees));
            var positiveWinding = arcRadians >= 0f;
            var stride = radialSegments + 1;
            var rowRadii = new float[heightSegments + 1];

            for (var row = 0; row <= heightSegments; row++)
            {
                var v = row / (float)heightSegments;
                var y = Mathf.Lerp(-height * 0.5f, height * 0.5f, v);
                var radius = Mathf.Lerp(bottomRadius, topRadius, v) * EvaluateWidth(shape, v);
                rowRadii[row] = radius;

                for (var segment = 0; segment <= radialSegments; segment++)
                {
                    var u = segment / (float)radialSegments;
                    var angle = angleOffset + u * arcRadians;
                    draft.AddVertex(
                        new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius),
                        new Vector2(u, v));
                }
            }

            for (var row = 0; row < heightSegments; row++)
            {
                var lowerRadius = rowRadii[row];
                var upperRadius = rowRadii[row + 1];
                for (var segment = 0; segment < radialSegments; segment++)
                {
                    var a = row * stride + segment;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;

                    if (lowerRadius < MinimumDimension && upperRadius < MinimumDimension)
                    {
                        continue;
                    }

                    if (lowerRadius < MinimumDimension)
                    {
                        if (positiveWinding)
                        {
                            draft.AddTriangle(a, c, d);
                        }
                        else
                        {
                            draft.AddTriangle(a, d, c);
                        }
                    }
                    else if (upperRadius < MinimumDimension)
                    {
                        if (positiveWinding)
                        {
                            draft.AddTriangle(a, c, b);
                        }
                        else
                        {
                            draft.AddTriangle(a, b, c);
                        }
                    }
                    else if (positiveWinding)
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                    else
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }
                }
            }

            if (shape.capStart && rowRadii[0] >= MinimumDimension)
            {
                AddCircleCap(
                    draft,
                    -height * 0.5f,
                    rowRadii[0],
                    radialSegments,
                    angleOffset,
                    arcRadians,
                    false);
            }

            if (shape.capEnd && rowRadii[heightSegments] >= MinimumDimension)
            {
                AddCircleCap(
                    draft,
                    height * 0.5f,
                    rowRadii[heightSegments],
                    radialSegments,
                    angleOffset,
                    arcRadians,
                    true);
            }
        }

        private static void AddCircleCap(
            VFXMeshDraft draft,
            float y,
            float radius,
            int radialSegments,
            float angleOffset,
            float arcRadians,
            bool facesUp)
        {
            var center = draft.AddVertex(new Vector3(0f, y, 0f), new Vector2(0.5f, 0.5f));
            var ringStart = draft.vertices.Count;

            for (var segment = 0; segment <= radialSegments; segment++)
            {
                var u = segment / (float)radialSegments;
                var angle = angleOffset + u * arcRadians;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                draft.AddVertex(
                    new Vector3(cosine * radius, y, sine * radius),
                    new Vector2(0.5f + cosine * 0.5f, 0.5f + sine * 0.5f));
            }

            for (var segment = 0; segment < radialSegments; segment++)
            {
                var current = ringStart + segment;
                var next = current + 1;
                if (facesUp == (arcRadians >= 0f))
                {
                    draft.AddTriangle(center, next, current);
                }
                else
                {
                    draft.AddTriangle(center, current, next);
                }
            }
        }

        private static void GenerateTube(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var radialSegments = Segments(shape.radialSegments, 3);
            var heightSegments = Segments(shape.heightSegments, 1);
            var height = Positive(shape.height);
            var firstRadius = NonNegative(shape.innerRadius);
            var secondRadius = Positive(shape.radius);
            var innerRadius = Mathf.Min(firstRadius, secondRadius);
            var outerRadius = Mathf.Max(firstRadius, secondRadius);
            if (outerRadius - innerRadius < MinimumDimension)
            {
                outerRadius = innerRadius + MinimumDimension;
            }

            if (innerRadius < MinimumDimension)
            {
                GenerateFrustum(shape, draft, outerRadius, outerRadius);
                return;
            }

            var angleOffset = Degrees(shape.angleOffset);
            var arcRadians = Degrees(SanitizeArcDegrees(shape.arcDegrees));
            var positiveWinding = arcRadians >= 0f;
            var outerStarts = new int[heightSegments + 1];
            var innerStarts = new int[heightSegments + 1];
            var outerRadii = new float[heightSegments + 1];
            var innerRadii = new float[heightSegments + 1];

            for (var row = 0; row <= heightSegments; row++)
            {
                var v = row / (float)heightSegments;
                var y = Mathf.Lerp(-height * 0.5f, height * 0.5f, v);
                var widthFactor = EvaluateWidth(shape, v);
                var currentOuter = outerRadius * widthFactor;
                var currentInner = innerRadius * widthFactor;
                outerRadii[row] = currentOuter;
                innerRadii[row] = currentInner;

                outerStarts[row] = draft.vertices.Count;
                AddTubeRing(draft, currentOuter, y, radialSegments, angleOffset, arcRadians, v, false);
                innerStarts[row] = draft.vertices.Count;
                AddTubeRing(draft, currentInner, y, radialSegments, angleOffset, arcRadians, v, true);
            }

            for (var row = 0; row < heightSegments; row++)
            {
                var outerLower = outerStarts[row];
                var outerUpper = outerStarts[row + 1];
                var innerLower = innerStarts[row];
                var innerUpper = innerStarts[row + 1];

                for (var segment = 0; segment < radialSegments; segment++)
                {
                    var a = outerLower + segment;
                    var b = a + 1;
                    var c = outerUpper + segment;
                    var d = c + 1;
                    if (positiveWinding)
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                    else
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }

                    a = innerLower + segment;
                    b = a + 1;
                    c = innerUpper + segment;
                    d = c + 1;
                    if (positiveWinding)
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }
                    else
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                }
            }

            if (shape.capStart)
            {
                AddAnnulusCap(
                    draft,
                    -height * 0.5f,
                    innerRadii[0],
                    outerRadii[0],
                    radialSegments,
                    angleOffset,
                    arcRadians,
                    false);
            }

            if (shape.capEnd)
            {
                AddAnnulusCap(
                    draft,
                    height * 0.5f,
                    innerRadii[heightSegments],
                    outerRadii[heightSegments],
                    radialSegments,
                    angleOffset,
                    arcRadians,
                    true);
            }
        }

        private static void AddTubeRing(
            VFXMeshDraft draft,
            float radius,
            float y,
            int radialSegments,
            float angleOffset,
            float arcRadians,
            float v,
            bool inner)
        {
            for (var segment = 0; segment <= radialSegments; segment++)
            {
                var angularT = segment / (float)radialSegments;
                var angle = angleOffset + angularT * arcRadians;
                draft.AddVertex(
                    new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius),
                    new Vector2(inner ? 1f - angularT : angularT, v));
            }
        }

        private static void AddAnnulusCap(
            VFXMeshDraft draft,
            float y,
            float innerRadius,
            float outerRadius,
            int radialSegments,
            float angleOffset,
            float arcRadians,
            bool facesUp)
        {
            var innerStart = draft.vertices.Count;
            for (var segment = 0; segment <= radialSegments; segment++)
            {
                var angularT = segment / (float)radialSegments;
                var angle = angleOffset + angularT * arcRadians;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                draft.AddVertex(
                    new Vector3(cosine * innerRadius, y, sine * innerRadius),
                    new Vector2(
                        0.5f + cosine * innerRadius / outerRadius * 0.5f,
                        0.5f + sine * innerRadius / outerRadius * 0.5f));
            }

            var outerStart = draft.vertices.Count;
            for (var segment = 0; segment <= radialSegments; segment++)
            {
                var angularT = segment / (float)radialSegments;
                var angle = angleOffset + angularT * arcRadians;
                var cosine = Mathf.Cos(angle);
                var sine = Mathf.Sin(angle);
                draft.AddVertex(
                    new Vector3(cosine * outerRadius, y, sine * outerRadius),
                    new Vector2(0.5f + cosine * 0.5f, 0.5f + sine * 0.5f));
            }

            for (var segment = 0; segment < radialSegments; segment++)
            {
                var a = innerStart + segment;
                var b = outerStart + segment;
                var c = a + 1;
                var d = b + 1;

                if (facesUp == (arcRadians >= 0f))
                {
                    draft.AddTriangle(a, c, b);
                    draft.AddTriangle(b, c, d);
                }
                else
                {
                    draft.AddTriangle(a, b, c);
                    draft.AddTriangle(b, d, c);
                }
            }
        }

        private static void GenerateSphere(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var longitudeSegments = Segments(shape.longitudeSegments, 3);
            var latitudeSegments = Segments(shape.latitudeSegments, 2);
            var radius = Positive(shape.radius);
            var angleOffset = Degrees(shape.angleOffset);
            var bottomPole = draft.AddVertex(new Vector3(0f, -radius, 0f), new Vector2(0.5f, 0f));
            var firstRing = -1;
            var previousRing = -1;

            for (var latitude = 1; latitude < latitudeSegments; latitude++)
            {
                var v = latitude / (float)latitudeSegments;
                var phi = -Mathf.PI * 0.5f + Mathf.PI * v;
                var y = Mathf.Sin(phi) * radius;
                var ringRadius = Mathf.Cos(phi) * radius;
                var ringStart = draft.vertices.Count;

                for (var longitude = 0; longitude <= longitudeSegments; longitude++)
                {
                    var u = longitude / (float)longitudeSegments;
                    var theta = angleOffset + u * Mathf.PI * 2f;
                    draft.AddVertex(
                        new Vector3(Mathf.Cos(theta) * ringRadius, y, Mathf.Sin(theta) * ringRadius),
                        new Vector2(u, v));
                }

                if (firstRing < 0)
                {
                    firstRing = ringStart;
                }

                if (previousRing >= 0)
                {
                    AddSphereStrip(draft, previousRing, ringStart, longitudeSegments);
                }

                previousRing = ringStart;
            }

            var topPole = draft.AddVertex(new Vector3(0f, radius, 0f), new Vector2(0.5f, 1f));
            for (var longitude = 0; longitude < longitudeSegments; longitude++)
            {
                draft.AddTriangle(bottomPole, firstRing + longitude, firstRing + longitude + 1);
                draft.AddTriangle(
                    previousRing + longitude,
                    topPole,
                    previousRing + longitude + 1);
            }
        }

        private static void GenerateHemisphere(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var longitudeSegments = Segments(shape.longitudeSegments, 3);
            var latitudeSegments = Segments(shape.latitudeSegments, 1);
            var radius = Positive(shape.radius);
            var angleOffset = Degrees(shape.angleOffset);
            var firstRing = -1;
            var previousRing = -1;

            for (var latitude = 0; latitude < latitudeSegments; latitude++)
            {
                var v = latitude / (float)latitudeSegments;
                var phi = Mathf.PI * 0.5f * v;
                var y = Mathf.Sin(phi) * radius;
                var ringRadius = Mathf.Cos(phi) * radius;
                var ringStart = draft.vertices.Count;

                for (var longitude = 0; longitude <= longitudeSegments; longitude++)
                {
                    var u = longitude / (float)longitudeSegments;
                    var theta = angleOffset + u * Mathf.PI * 2f;
                    draft.AddVertex(
                        new Vector3(Mathf.Cos(theta) * ringRadius, y, Mathf.Sin(theta) * ringRadius),
                        new Vector2(u, v));
                }

                if (firstRing < 0)
                {
                    firstRing = ringStart;
                }

                if (previousRing >= 0)
                {
                    AddSphereStrip(draft, previousRing, ringStart, longitudeSegments);
                }

                previousRing = ringStart;
            }

            var topPole = draft.AddVertex(new Vector3(0f, radius, 0f), new Vector2(0.5f, 1f));
            for (var longitude = 0; longitude < longitudeSegments; longitude++)
            {
                draft.AddTriangle(
                    previousRing + longitude,
                    topPole,
                    previousRing + longitude + 1);
            }

            if (shape.capEnd)
            {
                AddCircleCap(
                    draft,
                    0f,
                    radius,
                    longitudeSegments,
                    angleOffset,
                    Mathf.PI * 2f,
                    false);
            }
        }

        private static void AddSphereStrip(
            VFXMeshDraft draft,
            int lowerStart,
            int upperStart,
            int longitudeSegments)
        {
            for (var longitude = 0; longitude < longitudeSegments; longitude++)
            {
                var a = lowerStart + longitude;
                var b = a + 1;
                var c = upperStart + longitude;
                var d = c + 1;
                draft.AddTriangle(a, c, b);
                draft.AddTriangle(b, c, d);
            }
        }

        private static void GenerateTorus(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var majorSegments = Segments(shape.longitudeSegments, 3);
            var tubeSegments = Segments(shape.radialSegments, 3);
            var majorRadius = Positive(shape.radius);
            var tubeRadius = Positive(shape.thickness);
            var arcDegrees = SanitizeArcDegrees(shape.arcDegrees);
            var arcRadians = Degrees(arcDegrees);
            var angleOffset = Degrees(shape.angleOffset);
            var positiveWinding = arcRadians >= 0f;
            var tubeStride = tubeSegments + 1;

            for (var major = 0; major <= majorSegments; major++)
            {
                var u = major / (float)majorSegments;
                var theta = angleOffset + arcRadians * u;
                var radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                var center = radial * majorRadius;
                var currentTubeRadius = tubeRadius * EvaluateWidth(shape, u);

                for (var tube = 0; tube <= tubeSegments; tube++)
                {
                    var v = tube / (float)tubeSegments;
                    var phi = v * Mathf.PI * 2f;
                    var position = center +
                                   radial * (Mathf.Cos(phi) * currentTubeRadius) +
                                   Vector3.up * (Mathf.Sin(phi) * currentTubeRadius);
                    draft.AddVertex(position, new Vector2(u, v));
                }
            }

            for (var major = 0; major < majorSegments; major++)
            {
                for (var tube = 0; tube < tubeSegments; tube++)
                {
                    var a = major * tubeStride + tube;
                    var b = a + 1;
                    var c = a + tubeStride;
                    var d = c + 1;

                    if (positiveWinding)
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }
                    else
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                }
            }

            if (Mathf.Abs(arcDegrees) < FullArcThreshold)
            {
                if (shape.capStart)
                {
                    AddTorusCap(
                        draft,
                        angleOffset,
                        majorRadius,
                        tubeRadius * EvaluateWidth(shape, 0f),
                        tubeSegments,
                        -Mathf.Sign(arcRadians));
                }

                if (shape.capEnd)
                {
                    AddTorusCap(
                        draft,
                        angleOffset + arcRadians,
                        majorRadius,
                        tubeRadius * EvaluateWidth(shape, 1f),
                        tubeSegments,
                        Mathf.Sign(arcRadians));
                }
            }
        }

        private static void AddTorusCap(
            VFXMeshDraft draft,
            float theta,
            float majorRadius,
            float tubeRadius,
            int tubeSegments,
            float normalAlongTangent)
        {
            var radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
            var centerPosition = radial * majorRadius;
            var center = draft.AddVertex(centerPosition, new Vector2(0.5f, 0.5f));
            var ringStart = draft.vertices.Count;

            for (var tube = 0; tube <= tubeSegments; tube++)
            {
                var v = tube / (float)tubeSegments;
                var phi = v * Mathf.PI * 2f;
                var cosine = Mathf.Cos(phi);
                var sine = Mathf.Sin(phi);
                draft.AddVertex(
                    centerPosition + radial * (cosine * tubeRadius) + Vector3.up * (sine * tubeRadius),
                    new Vector2(0.5f + cosine * 0.5f, 0.5f + sine * 0.5f));
            }

            for (var tube = 0; tube < tubeSegments; tube++)
            {
                var current = ringStart + tube;
                var next = current + 1;
                if (normalAlongTangent > 0f)
                {
                    draft.AddTriangle(center, current, next);
                }
                else
                {
                    draft.AddTriangle(center, next, current);
                }
            }
        }

        private static void GenerateBox(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var size = new Vector3(
                Positive(shape.size.x),
                Positive(shape.size.y),
                Positive(shape.size.z));
            var half = size * 0.5f;
            var xSegments = Segments(shape.widthSegments, 1);
            var ySegments = Segments(shape.heightSegments, 1);
            var zSegments = Segments(shape.lengthSegments, 1);

            AddBoxFace(
                draft,
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(0f, size.y, 0f),
                new Vector3(0f, 0f, size.z),
                ySegments,
                zSegments);
            AddBoxFace(
                draft,
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(0f, size.y, 0f),
                new Vector3(0f, 0f, -size.z),
                ySegments,
                zSegments);
            AddBoxFace(
                draft,
                new Vector3(-half.x, half.y, half.z),
                new Vector3(size.x, 0f, 0f),
                new Vector3(0f, 0f, -size.z),
                xSegments,
                zSegments);
            AddBoxFace(
                draft,
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3(size.x, 0f, 0f),
                new Vector3(0f, 0f, size.z),
                xSegments,
                zSegments);
            AddBoxFace(
                draft,
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(size.x, 0f, 0f),
                new Vector3(0f, size.y, 0f),
                xSegments,
                ySegments);
            AddBoxFace(
                draft,
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(-size.x, 0f, 0f),
                new Vector3(0f, size.y, 0f),
                xSegments,
                ySegments);
        }

        private static void AddBoxFace(
            VFXMeshDraft draft,
            Vector3 origin,
            Vector3 uVector,
            Vector3 vVector,
            int uSegments,
            int vSegments)
        {
            var start = draft.vertices.Count;
            var stride = uSegments + 1;

            for (var vIndex = 0; vIndex <= vSegments; vIndex++)
            {
                var v = vIndex / (float)vSegments;
                for (var uIndex = 0; uIndex <= uSegments; uIndex++)
                {
                    var u = uIndex / (float)uSegments;
                    draft.AddVertex(origin + uVector * u + vVector * v, new Vector2(u, v));
                }
            }

            for (var vIndex = 0; vIndex < vSegments; vIndex++)
            {
                for (var uIndex = 0; uIndex < uSegments; uIndex++)
                {
                    var a = start + vIndex * stride + uIndex;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;
                    draft.AddTriangle(a, b, c);
                    draft.AddTriangle(b, d, c);
                }
            }
        }

        private static void GenerateRibbon(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            AddAxialPlane(
                shape,
                draft,
                Vector3.right,
                Positive(shape.width),
                Positive(shape.length),
                Segments(shape.widthSegments, 1),
                Segments(shape.lengthSegments, 1));
        }

        private static void GenerateCrossPlanes(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var planeCount = Segments(shape.planeCount, 2);
            var angleOffset = Degrees(shape.angleOffset);
            var width = Positive(shape.width);
            var length = Positive(shape.length);
            var widthSegments = Segments(shape.widthSegments, 1);
            var lengthSegments = Segments(shape.lengthSegments, 1);

            for (var plane = 0; plane < planeCount; plane++)
            {
                var angle = angleOffset + Mathf.PI * plane / planeCount;
                var widthDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                AddAxialPlane(
                    shape,
                    draft,
                    widthDirection,
                    width,
                    length,
                    widthSegments,
                    lengthSegments);
            }
        }

        private static void AddAxialPlane(
            VFXShapeSettings shape,
            VFXMeshDraft draft,
            Vector3 widthDirection,
            float width,
            float length,
            int widthSegments,
            int lengthSegments)
        {
            var start = draft.vertices.Count;
            var stride = widthSegments + 1;

            for (var row = 0; row <= lengthSegments; row++)
            {
                var v = row / (float)lengthSegments;
                var y = Mathf.Lerp(-length * 0.5f, length * 0.5f, v);
                var rowWidth = width * EvaluateWidth(shape, v);

                for (var column = 0; column <= widthSegments; column++)
                {
                    var u = column / (float)widthSegments;
                    var across = Mathf.Lerp(-rowWidth * 0.5f, rowWidth * 0.5f, u);
                    draft.AddVertex(widthDirection * across + Vector3.up * y, new Vector2(u, v));
                }
            }

            for (var row = 0; row < lengthSegments; row++)
            {
                for (var column = 0; column < widthSegments; column++)
                {
                    var a = start + row * stride + column;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;
                    draft.AddTriangle(a, b, c);
                    draft.AddTriangle(b, d, c);
                }
            }
        }

        private static void GenerateHelix(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            var turns = Finite(shape.turns, 1f);
            if (Mathf.Abs(turns) < MinimumDimension)
            {
                turns = MinimumDimension;
            }

            var turnsForDensity = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(turns)));
            var lengthSegments = Segments(shape.lengthSegments, 3) * turnsForDensity;
            var widthSegments = Segments(shape.widthSegments, 1);
            var radius = Positive(shape.radius);
            var width = Positive(shape.width);
            var pitch = Finite(shape.pitch, 0.25f);
            var angleOffset = Degrees(shape.angleOffset);
            var totalAngle = turns * Mathf.PI * 2f;
            var totalHeight = Mathf.Abs(turns) * pitch;
            var stride = widthSegments + 1;

            for (var row = 0; row <= lengthSegments; row++)
            {
                var v = row / (float)lengthSegments;
                var angle = angleOffset + totalAngle * v;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var center = radial * radius + Vector3.up * Mathf.Lerp(-totalHeight * 0.5f, totalHeight * 0.5f, v);
                var rowWidth = width * EvaluateWidth(shape, v);

                for (var column = 0; column <= widthSegments; column++)
                {
                    var u = column / (float)widthSegments;
                    var across = Mathf.Lerp(-rowWidth * 0.5f, rowWidth * 0.5f, u);
                    draft.AddVertex(center + radial * across, new Vector2(u, v));
                }
            }

            var positiveWinding = totalAngle >= 0f;
            for (var row = 0; row < lengthSegments; row++)
            {
                for (var column = 0; column < widthSegments; column++)
                {
                    var a = row * stride + column;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;

                    if (positiveWinding)
                    {
                        draft.AddTriangle(a, b, c);
                        draft.AddTriangle(b, d, c);
                    }
                    else
                    {
                        draft.AddTriangle(a, c, b);
                        draft.AddTriangle(b, c, d);
                    }
                }
            }
        }

        private static void OrientAndPivot(VFXShapeSettings shape, VFXMeshDraft draft)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                draft.vertices[i] = Orient(draft.vertices[i], shape.axis);
            }

            if (draft.vertices.Count == 0)
            {
                return;
            }

            var bounds = draft.CalculateBounds();
            Vector3 pivotPoint;
            switch (shape.pivot)
            {
                case VFXPivot.Center:
                    pivotPoint = bounds.center;
                    break;
                case VFXPivot.Start:
                    pivotPoint = bounds.center;
                    SetAxisComponent(ref pivotPoint, shape.axis, AxisMinimum(bounds, shape.axis));
                    break;
                case VFXPivot.End:
                    pivotPoint = bounds.center;
                    SetAxisComponent(ref pivotPoint, shape.axis, AxisMaximum(bounds, shape.axis));
                    break;
                case VFXPivot.Custom:
                    pivotPoint = IsFinite(shape.customPivotOffset)
                        ? shape.customPivotOffset
                        : Vector3.zero;
                    break;
                default:
                    pivotPoint = bounds.center;
                    break;
            }

            for (var i = 0; i < draft.vertices.Count; i++)
            {
                draft.vertices[i] -= pivotPoint;
            }
        }

        private static Vector3 Orient(Vector3 point, VFXAxis axis)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    return new Vector3(point.y, -point.x, point.z);
                case VFXAxis.Z:
                    return new Vector3(point.x, -point.z, point.y);
                default:
                    return point;
            }
        }

        private static float AxisMinimum(Bounds bounds, VFXAxis axis)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    return bounds.min.x;
                case VFXAxis.Z:
                    return bounds.min.z;
                default:
                    return bounds.min.y;
            }
        }

        private static float AxisMaximum(Bounds bounds, VFXAxis axis)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    return bounds.max.x;
                case VFXAxis.Z:
                    return bounds.max.z;
                default:
                    return bounds.max.y;
            }
        }

        private static void SetAxisComponent(ref Vector3 value, VFXAxis axis, float component)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    value.x = component;
                    break;
                case VFXAxis.Z:
                    value.z = component;
                    break;
                default:
                    value.y = component;
                    break;
            }
        }

        private static void Clear(VFXMeshDraft draft)
        {
            draft.vertices.Clear();
            draft.triangles.Clear();
            draft.uv0.Clear();
            draft.uv1.Clear();
            draft.uv2.Clear();
            draft.uv3.Clear();
            draft.colors.Clear();
        }

        private static int Segments(int value, int minimum)
        {
            return Mathf.Clamp(value, minimum, VFXMeshBuildLimits.MaximumSegmentsPerAxis);
        }

        private static float EvaluateWidth(VFXShapeSettings shape, float normalizedPosition)
        {
            var value = shape.widthCurve != null
                ? shape.widthCurve.Evaluate(Mathf.Clamp01(normalizedPosition))
                : 1f;
            return Mathf.Max(MinimumDimension, Mathf.Abs(Finite(value, 1f)));
        }

        private static float EvaluateRadialElevation(VFXShapeSettings shape, float normalizedRadius)
        {
            var value = shape.radialElevationCurve != null
                ? shape.radialElevationCurve.Evaluate(Mathf.Clamp01(normalizedRadius))
                : 0f;
            return Finite(value, 0f);
        }

        internal static float EvaluateArcWidth(VFXShapeSettings shape, float normalizedAngle)
        {
            var value = shape?.arcWidthCurve != null
                ? shape.arcWidthCurve.Evaluate(Mathf.Clamp01(normalizedAngle))
                : 1f;
            return Mathf.Clamp01(Finite(value, 1f));
        }

        private static float Positive(float value)
        {
            return Mathf.Max(MinimumDimension, Mathf.Abs(Finite(value, 1f)));
        }

        private static float NonNegative(float value)
        {
            return Mathf.Max(0f, Mathf.Abs(Finite(value, 0f)));
        }

        internal static float SanitizeArcDegrees(float value)
        {
            value = Mathf.Clamp(Finite(value, 360f), -360f, 360f);
            if (Mathf.Abs(value) < MinimumDimension)
            {
                value = value < 0f ? -MinimumDimension : MinimumDimension;
            }

            return value;
        }

        private static float Degrees(float value)
        {
            return Finite(value, 0f) * Mathf.Deg2Rad;
        }

        private static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
