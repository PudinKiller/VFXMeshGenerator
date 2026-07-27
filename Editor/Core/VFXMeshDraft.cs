using System.Collections.Generic;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    public sealed class VFXMeshDraft
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();
        public readonly List<Vector2> uv0 = new List<Vector2>();
        public readonly List<Vector4> uv1 = new List<Vector4>();
        public readonly List<Vector4> uv2 = new List<Vector4>();
        public readonly List<Vector4> uv3 = new List<Vector4>();
        public readonly List<Color> colors = new List<Color>();

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            var index = vertices.Count;
            vertices.Add(position);
            uv0.Add(uv);
            return index;
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public Bounds CalculateBounds()
        {
            if (vertices.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (var i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            return bounds;
        }

        public VFXMeshValidationFlags Validate()
        {
            var flags = vertices.Count == 0 || triangles.Count == 0
                ? VFXMeshValidationFlags.Empty
                : VFXMeshValidationFlags.None;

            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                if (!IsFinite(vertex.x) || !IsFinite(vertex.y) || !IsFinite(vertex.z))
                {
                    flags |= VFXMeshValidationFlags.NonFiniteVertex;
                    break;
                }
            }

            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 ||
                    a >= vertices.Count || b >= vertices.Count || c >= vertices.Count)
                {
                    flags |= VFXMeshValidationFlags.InvalidIndex;
                    continue;
                }

                if (a == b || b == c || c == a ||
                    Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude < 1e-12f)
                {
                    flags |= VFXMeshValidationFlags.DegenerateTriangle;
                }
            }

            return flags;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IVFXMeshShapeGenerator
    {
        void Generate(VFXMeshRecipe recipe, VFXMeshDraft draft);
    }
}
