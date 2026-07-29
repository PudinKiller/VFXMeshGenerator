using System;
using System.Collections.Generic;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor
{
    /// <summary>
    /// Applies the recipe's enabled deformation modifiers to a mesh draft in list order.
    /// </summary>
    public static class VFXModifierProcessor
    {
        private const int MaximumNoiseOctaves = 12;

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

            if (draft.vertices.Count == 0 || recipe.modifiers == null)
            {
                return;
            }

            for (var modifierIndex = 0; modifierIndex < recipe.modifiers.Count; modifierIndex++)
            {
                var modifier = recipe.modifiers[modifierIndex];
                if (modifier == null || !modifier.enabled)
                {
                    continue;
                }

                ApplyModifier(modifier, draft);
            }
        }

        private static void ApplyModifier(VFXMeshModifierSettings modifier, VFXMeshDraft draft)
        {
            var bounds = draft.CalculateBounds();
            var center = bounds.center;
            var axis = VFXProcessingUtility.AxisVector(modifier.axis);
            VFXProcessingUtility.GetPlanarBasis(modifier.axis, out var radialA, out var radialB);
            var maximumRadius =
                VFXProcessingUtility.CalculateMaximumRadius(draft.vertices, center, axis);

            switch (modifier.type)
            {
                case VFXModifierType.Transform:
                    ApplyTransform(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                case VFXModifierType.Taper:
                    ApplyTaper(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                case VFXModifierType.Twist:
                    ApplyTwist(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                case VFXModifierType.Bend:
                    ApplyBend(
                        modifier,
                        draft,
                        bounds,
                        center,
                        axis,
                        radialA,
                        radialB,
                        maximumRadius);
                    break;
                case VFXModifierType.Skew:
                    ApplySkew(modifier, draft, bounds, center, axis, radialA, maximumRadius);
                    break;
                case VFXModifierType.Wave:
                    ApplyWave(modifier, draft, bounds, center, axis, radialA, maximumRadius);
                    break;
                case VFXModifierType.RadialRipple:
                    ApplyRadialRipple(
                        modifier,
                        draft,
                        bounds,
                        center,
                        axis,
                        radialA,
                        maximumRadius);
                    break;
                case VFXModifierType.Noise:
                    ApplyNoise(
                        modifier,
                        draft,
                        bounds,
                        center,
                        axis,
                        radialA,
                        maximumRadius);
                    break;
                case VFXModifierType.Inflate:
                    ApplyInflate(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                case VFXModifierType.Spherize:
                    ApplySpherize(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                case VFXModifierType.Flatten:
                    ApplyFlatten(modifier, draft, bounds, center, axis, maximumRadius);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(modifier.type),
                        modifier.type,
                        "Unsupported VFX mesh modifier type.");
            }
        }

        private static void ApplyTransform(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            var rotation = Quaternion.AngleAxis(modifier.angle, axis);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var original = draft.vertices[i];
                var local = Vector3.Scale(original - center, modifier.scale);
                var target = center + rotation * local + modifier.offset;
                draft.vertices[i] = target;
            }
        }

        private static void ApplyTaper(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var local = position - center;
                var axial = axis * Vector3.Dot(local, axis);
                var radial = local - axial;
                draft.vertices[i] = center + axial + radial * (1f + modifier.strength * weight);
            }
        }

        private static void ApplyTwist(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var local = position - center;
                var axial = axis * Vector3.Dot(local, axis);
                var radial = local - axial;
                var rotation = Quaternion.AngleAxis(modifier.angle * weight, axis);
                draft.vertices[i] = center + axial + rotation * radial;
            }
        }

        private static void ApplyBend(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            Vector3 radialA,
            Vector3 radialB,
            float maximumRadius)
        {
            var axisMinimum = VFXProcessingUtility.AxisComponent(bounds.min, modifier.axis);
            var axisMaximum = VFXProcessingUtility.AxisComponent(bounds.max, modifier.axis);
            var axisLength = axisMaximum - axisMinimum;
            var totalRadians = modifier.angle * Mathf.Deg2Rad;
            if (Mathf.Abs(axisLength) <= VFXProcessingUtility.Epsilon ||
                Mathf.Abs(totalRadians) <= VFXProcessingUtility.Epsilon)
            {
                return;
            }

            var baseCenter = center -
                             axis * (VFXProcessingUtility.AxisComponent(center, modifier.axis) -
                                     axisMinimum);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var fromBase = position - baseCenter;
                var distanceAlongAxis = Vector3.Dot(fromBase, axis);
                var radialOffsetA = Vector3.Dot(fromBase, radialA);
                var radialOffsetB = Vector3.Dot(fromBase, radialB);
                var curvature = totalRadians * weight / axisLength;
                if (Mathf.Abs(curvature) <= VFXProcessingUtility.Epsilon)
                {
                    continue;
                }

                var radius = 1f / curvature;
                var theta = distanceAlongAxis * curvature;
                var curvedAxis = (radius + radialOffsetA) * Mathf.Sin(theta);
                var curvedRadial = (radius + radialOffsetA) * Mathf.Cos(theta) - radius;
                draft.vertices[i] =
                    baseCenter +
                    axis * curvedAxis +
                    radialA * curvedRadial +
                    radialB * radialOffsetB;
            }
        }

        private static void ApplySkew(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            Vector3 radialA,
            float maximumRadius)
        {
            var axisLength =
                VFXProcessingUtility.AxisComponent(bounds.max, modifier.axis) -
                VFXProcessingUtility.AxisComponent(bounds.min, modifier.axis);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var direction = modifier.space == VFXModifierSpace.Radial
                    ? axis
                    : radialA;
                draft.vertices[i] = position + direction * (modifier.strength * axisLength * weight);
            }
        }

        private static void ApplyWave(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            Vector3 radialA,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var radial = VFXProcessingUtility.RadialVector(position, center, axis);
                var radialDistance = radial.magnitude;
                var coordinate = modifier.space == VFXModifierSpace.Radial
                    ? VFXProcessingUtility.NormalizeRadius(radialDistance, maximumRadius)
                    : VFXProcessingUtility.NormalizedAxis(position, bounds, modifier.axis);
                var phase = (coordinate * modifier.frequency * Mathf.PI * 2f) +
                            modifier.angle * Mathf.Deg2Rad;
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var direction = modifier.space == VFXModifierSpace.Radial
                    ? SafeRadialDirection(radial, radialA)
                    : radialA;
                draft.vertices[i] =
                    position + direction * (Mathf.Sin(phase) * modifier.strength * weight);
            }
        }

        private static void ApplyRadialRipple(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            Vector3 radialA,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var radial = VFXProcessingUtility.RadialVector(position, center, axis);
                var radialNormalized =
                    VFXProcessingUtility.NormalizeRadius(radial.magnitude, maximumRadius);
                var phase = radialNormalized * modifier.frequency * Mathf.PI * 2f +
                            modifier.angle * Mathf.Deg2Rad;
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                var direction = modifier.space == VFXModifierSpace.Axis
                    ? axis
                    : SafeRadialDirection(radial, radialA);
                draft.vertices[i] =
                    position + direction * (Mathf.Sin(phase) * modifier.strength * weight);
            }
        }

        private static void ApplyNoise(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            Vector3 radialA,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var samplePosition = position + modifier.offset;
                var noise = FractalNoise(
                    samplePosition,
                    modifier.frequency,
                    modifier.seed,
                    modifier.octaves,
                    modifier.lacunarity,
                    modifier.persistence);
                var radial = VFXProcessingUtility.RadialVector(position, center, axis);
                var direction = modifier.space == VFXModifierSpace.Radial
                    ? SafeRadialDirection(radial, radialA)
                    : axis;
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                draft.vertices[i] =
                    position + direction * (noise * modifier.strength * weight);
            }
        }

        private static void ApplyInflate(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            var normals = CalculateVertexNormals(draft);
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                draft.vertices[i] = position + normals[i] * (modifier.strength * weight);
            }
        }

        private static void ApplySpherize(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (radius <= VFXProcessingUtility.Epsilon)
            {
                return;
            }

            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var direction = position - center;
                if (direction.sqrMagnitude <= VFXProcessingUtility.Epsilon)
                {
                    continue;
                }

                var target = center + direction.normalized * radius;
                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                draft.vertices[i] =
                    Vector3.LerpUnclamped(position, target, modifier.strength * weight);
            }
        }

        private static void ApplyFlatten(
            VFXMeshModifierSettings modifier,
            VFXMeshDraft draft,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            for (var i = 0; i < draft.vertices.Count; i++)
            {
                var position = draft.vertices[i];
                var local = position - center;
                Vector3 target;
                if (modifier.space == VFXModifierSpace.Radial)
                {
                    target = center + axis * Vector3.Dot(local, axis);
                }
                else
                {
                    target = position - axis * Vector3.Dot(local, axis);
                }

                var weight = Weight(modifier, position, bounds, center, axis, maximumRadius);
                draft.vertices[i] =
                    Vector3.LerpUnclamped(position, target, modifier.strength * weight);
            }
        }

        private static float Weight(
            VFXMeshModifierSettings modifier,
            Vector3 position,
            Bounds bounds,
            Vector3 center,
            Vector3 axis,
            float maximumRadius)
        {
            return VFXProcessingUtility.EvaluateFalloff(
                modifier,
                position,
                bounds,
                center,
                axis,
                maximumRadius);
        }

        private static Vector3 SafeRadialDirection(Vector3 radial, Vector3 fallback)
        {
            return radial.sqrMagnitude <= VFXProcessingUtility.Epsilon
                ? fallback
                : radial.normalized;
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

                var normal = Vector3.Cross(
                    draft.vertices[b] - draft.vertices[a],
                    draft.vertices[c] - draft.vertices[a]);
                if (normal.sqrMagnitude <= VFXProcessingUtility.Epsilon)
                {
                    continue;
                }

                normals[a] += normal;
                normals[b] += normal;
                normals[c] += normal;
            }

            for (var i = 0; i < normals.Count; i++)
            {
                normals[i] = normals[i].sqrMagnitude <= VFXProcessingUtility.Epsilon
                    ? Vector3.up
                    : normals[i].normalized;
            }

            return normals;
        }

        private static float FractalNoise(
            Vector3 position,
            float baseFrequency,
            int seed,
            int octaveCount,
            float lacunarity,
            float persistence)
        {
            var octaves = Mathf.Clamp(octaveCount, 1, MaximumNoiseOctaves);
            var frequency = baseFrequency;
            var octaveScale = Mathf.Abs(lacunarity) <= VFXProcessingUtility.Epsilon
                ? 2f
                : lacunarity;
            var amplitude = 1f;
            var weightedValue = 0f;
            var totalAmplitude = 0f;

            for (var octave = 0; octave < octaves; octave++)
            {
                var octaveOffset = new Vector3(
                    octave * 19.19f,
                    octave * -7.73f,
                    octave * 13.17f);
                var octaveSeed = unchecked(seed + octave * 1013);
                weightedValue +=
                    ValueNoise(position * frequency + octaveOffset, octaveSeed) * amplitude;
                totalAmplitude += Mathf.Abs(amplitude);
                frequency *= octaveScale;
                amplitude *= persistence;
            }

            return totalAmplitude <= VFXProcessingUtility.Epsilon
                ? 0f
                : weightedValue / totalAmplitude;
        }

        private static float ValueNoise(Vector3 position, int seed)
        {
            var x0 = Mathf.FloorToInt(position.x);
            var y0 = Mathf.FloorToInt(position.y);
            var z0 = Mathf.FloorToInt(position.z);
            var tx = Smooth(position.x - x0);
            var ty = Smooth(position.y - y0);
            var tz = Smooth(position.z - z0);

            var x00 = Mathf.Lerp(HashValue(x0, y0, z0, seed), HashValue(x0 + 1, y0, z0, seed), tx);
            var x10 = Mathf.Lerp(
                HashValue(x0, y0 + 1, z0, seed),
                HashValue(x0 + 1, y0 + 1, z0, seed),
                tx);
            var x01 = Mathf.Lerp(
                HashValue(x0, y0, z0 + 1, seed),
                HashValue(x0 + 1, y0, z0 + 1, seed),
                tx);
            var x11 = Mathf.Lerp(
                HashValue(x0, y0 + 1, z0 + 1, seed),
                HashValue(x0 + 1, y0 + 1, z0 + 1, seed),
                tx);
            var y0Value = Mathf.Lerp(x00, x10, ty);
            var y1Value = Mathf.Lerp(x01, x11, ty);
            return Mathf.Lerp(y0Value, y1Value, tz);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float HashValue(int x, int y, int z, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)x * 0x8DA6B343u;
                hash ^= (uint)y * 0xD8163841u;
                hash ^= (uint)z * 0xCB1AB31Fu;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return ((hash & 0x00FFFFFFu) / 8388607.5f) - 1f;
            }
        }
    }
}
