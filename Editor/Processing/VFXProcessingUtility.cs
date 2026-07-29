using System;
using System.Collections.Generic;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor
{
    internal static class VFXProcessingUtility
    {
        internal const float Epsilon = 1e-6f;

        internal static Vector3 AxisVector(VFXAxis axis)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    return Vector3.right;
                case VFXAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.up;
            }
        }

        internal static void GetPlanarBasis(VFXAxis axis, out Vector3 first, out Vector3 second)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    first = Vector3.up;
                    second = Vector3.forward;
                    break;
                case VFXAxis.Z:
                    first = Vector3.right;
                    second = Vector3.up;
                    break;
                default:
                    first = Vector3.right;
                    second = Vector3.forward;
                    break;
            }
        }

        internal static float AxisComponent(Vector3 value, VFXAxis axis)
        {
            switch (axis)
            {
                case VFXAxis.X:
                    return value.x;
                case VFXAxis.Z:
                    return value.z;
                default:
                    return value.y;
            }
        }

        internal static float NormalizedAxis(Vector3 position, Bounds bounds, VFXAxis axis)
        {
            var minimum = AxisComponent(bounds.min, axis);
            var maximum = AxisComponent(bounds.max, axis);
            return Normalize(AxisComponent(position, axis), minimum, maximum);
        }

        internal static float Normalize(float value, float minimum, float maximum)
        {
            var range = maximum - minimum;
            return Mathf.Abs(range) <= Epsilon
                ? 0.5f
                : Mathf.Clamp01((value - minimum) / range);
        }

        internal static void GetProjectionRange(
            IReadOnlyList<Vector3> vertices,
            Vector3 direction,
            out float minimum,
            out float maximum)
        {
            if (vertices == null || vertices.Count == 0)
            {
                minimum = 0f;
                maximum = 0f;
                return;
            }

            minimum = Vector3.Dot(vertices[0], direction);
            maximum = minimum;
            for (var i = 1; i < vertices.Count; i++)
            {
                var value = Vector3.Dot(vertices[i], direction);
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
            }
        }

        internal static Vector3 RadialVector(Vector3 position, Vector3 center, Vector3 axis)
        {
            var fromCenter = position - center;
            return fromCenter - axis * Vector3.Dot(fromCenter, axis);
        }

        internal static float CalculateMaximumRadius(
            IReadOnlyList<Vector3> vertices,
            Vector3 center,
            Vector3 axis)
        {
            var maximum = 0f;
            if (vertices == null)
            {
                return maximum;
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                maximum = Mathf.Max(maximum, RadialVector(vertices[i], center, axis).magnitude);
            }

            return maximum;
        }

        internal static float EvaluateFalloff(
            VFXMeshModifierSettings modifier,
            Vector3 position,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            var normalized = modifier.space == VFXModifierSpace.Radial
                ? NormalizeRadius(RadialVector(position, center, axis).magnitude, maximumRadius)
                : NormalizedAxis(position, bounds, modifier.axis);

            var value = modifier.falloff == null
                ? normalized
                : modifier.falloff.Evaluate(normalized);
            return IsFinite(value) ? value : 0f;
        }

        internal static float NormalizeRadius(float radius, float maximumRadius)
        {
            return maximumRadius <= Epsilon ? 0f : Mathf.Clamp01(radius / maximumRadius);
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static float StableRandom01(int vertexIndex)
        {
            unchecked
            {
                var value = (uint)vertexIndex + 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }

        internal static void ResizeList<T>(List<T> list, int count, T defaultValue)
        {
            if (list.Count > count)
            {
                list.RemoveRange(count, list.Count - count);
            }

            while (list.Count < count)
            {
                list.Add(defaultValue);
            }
        }
    }
}
