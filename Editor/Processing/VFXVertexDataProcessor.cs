using System;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor
{
    /// <summary>
    /// Builds UV0 and all optional shader-facing vertex streams configured by the recipe.
    /// </summary>
    public static class VFXVertexDataProcessor
    {
        public static void Apply(VFXMeshRecipe recipe, VFXMeshDraft draft)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (draft == null)
            {
                throw new ArgumentNullException(nameof(draft));
            }

            var settings = recipe.vertexData ?? new VFXVertexDataSettings();
            var bounds = draft.CalculateBounds();
            var vertexCount = draft.vertices.Count;
            var shapeAxis = recipe.shape == null ? VFXAxis.Y : recipe.shape.axis;
            var axis = VFXProcessingUtility.AxisVector(shapeAxis);
            var center = bounds.center;
            var maximumRadius =
                VFXProcessingUtility.CalculateMaximumRadius(draft.vertices, center, axis);

            if (settings.generateColors)
            {
                var colorAxis = VFXProcessingUtility.AxisVector(settings.gradientAxis);
                var colorMaximumRadius = VFXProcessingUtility.CalculateMaximumRadius(
                    draft.vertices,
                    center,
                    colorAxis);
                VFXProcessingUtility.ResizeList(draft.colors, vertexCount, Color.white);
                for (var i = 0; i < vertexCount; i++)
                {
                    draft.colors[i] = EvaluateColor(
                        settings,
                        draft.vertices[i],
                        bounds,
                        center,
                        colorMaximumRadius);
                }
            }
            else
            {
                draft.colors.Clear();
            }

            FillPackedChannel(
                draft.uv1,
                settings.generateUV1,
                settings.uv1,
                draft,
                bounds,
                center,
                axis,
                shapeAxis,
                maximumRadius);
            FillPackedChannel(
                draft.uv2,
                settings.generateUV2,
                settings.uv2,
                draft,
                bounds,
                center,
                axis,
                shapeAxis,
                maximumRadius);
            FillPackedChannel(
                draft.uv3,
                settings.generateUV3,
                settings.uv3,
                draft,
                bounds,
                center,
                axis,
                shapeAxis,
                maximumRadius);

            VFXUVProcessor.Apply(recipe, draft);
        }

        private static Color EvaluateColor(
            VFXVertexDataSettings settings,
            Vector3 position,
            Bounds bounds,
            Vector3 center,
            float maximumRadius)
        {
            if (settings.colorMode == VFXVertexColorMode.Solid)
            {
                return settings.solidColor;
            }

            float value;
            switch (settings.colorMode)
            {
                case VFXVertexColorMode.AxisGradient:
                    value = VFXProcessingUtility.NormalizedAxis(
                        position,
                        bounds,
                        settings.gradientAxis);
                    break;
                case VFXVertexColorMode.RadialGradient:
                {
                    var gradientAxis = VFXProcessingUtility.AxisVector(settings.gradientAxis);
                    value = VFXProcessingUtility.NormalizeRadius(
                        VFXProcessingUtility.RadialVector(position, center, gradientAxis).magnitude,
                        maximumRadius);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(settings.colorMode),
                        settings.colorMode,
                        "Unsupported VFX vertex color mode.");
            }

            return settings.colorGradient == null
                ? settings.solidColor
                : settings.colorGradient.Evaluate(value);
        }

        private static void FillPackedChannel(
            System.Collections.Generic.List<Vector4> destination,
            bool enabled,
            VFXChannelPackSettings pack,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            VFXAxis axisType,
            float maximumRadius)
        {
            if (!enabled)
            {
                destination.Clear();
                return;
            }

            VFXProcessingUtility.ResizeList(destination, draft.vertices.Count, Vector4.zero);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var random = VFXProcessingUtility.StableRandom01(i);
                destination[i] = pack == null
                    ? Vector4.zero
                    : new Vector4(
                        EvaluateSource(
                            pack.x,
                            position,
                            bounds,
                            center,
                            axis,
                            axisType,
                            maximumRadius,
                            random),
                        EvaluateSource(
                            pack.y,
                            position,
                            bounds,
                            center,
                            axis,
                            axisType,
                            maximumRadius,
                            random),
                        EvaluateSource(
                            pack.z,
                            position,
                            bounds,
                            center,
                            axis,
                            axisType,
                            maximumRadius,
                            random),
                        EvaluateSource(
                            pack.w,
                            position,
                            bounds,
                            center,
                            axis,
                            axisType,
                            maximumRadius,
                            random));
            }
        }

        private static float EvaluateSource(
            VFXDataSource source,
            Vector3 position,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            VFXAxis axisType,
            float maximumRadius,
            float random)
        {
            switch (source)
            {
                case VFXDataSource.Zero:
                    return 0f;
                case VFXDataSource.One:
                    return 1f;
                case VFXDataSource.NormalizedX:
                    return VFXProcessingUtility.Normalize(position.x, bounds.min.x, bounds.max.x);
                case VFXDataSource.NormalizedY:
                    return VFXProcessingUtility.Normalize(position.y, bounds.min.y, bounds.max.y);
                case VFXDataSource.NormalizedZ:
                    return VFXProcessingUtility.Normalize(position.z, bounds.min.z, bounds.max.z);
                case VFXDataSource.AlongAxis:
                    return VFXProcessingUtility.NormalizedAxis(position, bounds, axisType);
                case VFXDataSource.RadialDistance:
                    return VFXProcessingUtility.NormalizeRadius(
                        VFXProcessingUtility.RadialVector(position, center, axis).magnitude,
                        maximumRadius);
                case VFXDataSource.Random:
                    return random;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        source,
                        "Unsupported VFX vertex data source.");
            }
        }
    }
}
