using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PudinKiller.VFXMeshLab.Editor
{
    internal sealed class VFXMeshPreviewController : IDisposable
    {
        private readonly PreviewRenderUtility previewUtility;
        private readonly Material frontMaterial;
        private readonly Material backfaceMaterial;
        private readonly Material wireMaterial;
        private Mesh mesh;
        private Mesh wireframeMesh;
        private Vector2 orbit = new Vector2(32f, 20f);
        private Vector3 pan;
        private float distance = 3f;
        private float framingRadius = 0.5f;
        private bool orthographic;

        public VFXMeshPreviewController()
        {
            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.fieldOfView = 35f;
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 1000f;
            previewUtility.ambientColor = new Color(0.28f, 0.28f, 0.28f);
            previewUtility.lights[0].intensity = 1.25f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 0.75f;
            previewUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);

            var shader = Shader.Find("Hidden/PudinKiller/VFXMeshPreview");
            if (shader != null && shader.isSupported)
            {
                frontMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry
                };
                frontMaterial.SetFloat("_Cull", (float)CullMode.Back);
                frontMaterial.SetFloat("_BackfacePass", 0f);
                frontMaterial.SetFloat("_WirePass", 0f);
                frontMaterial.SetFloat("_WireDepthBias", 0f);
                frontMaterial.SetFloat("_UseCheckerTexture", 0f);
                frontMaterial.SetFloat("_ZWrite", 1f);

                backfaceMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry - 1
                };
                backfaceMaterial.SetFloat("_Cull", (float)CullMode.Front);
                backfaceMaterial.SetFloat("_BackfacePass", 1f);
                backfaceMaterial.SetFloat("_WirePass", 0f);
                backfaceMaterial.SetFloat("_WireDepthBias", 0f);
                backfaceMaterial.SetFloat("_ZWrite", 1f);
                backfaceMaterial.SetColor("_BackfaceColor", new Color(1f, 0.08f, 0.05f, 1f));

                wireMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = (int)RenderQueue.Geometry + 1
                };
                wireMaterial.SetFloat("_Cull", (float)CullMode.Off);
                wireMaterial.SetFloat("_BackfacePass", 0f);
                wireMaterial.SetFloat("_WirePass", 1f);
                wireMaterial.SetFloat("_WireDepthBias", 0f);
                wireMaterial.SetFloat("_ZWrite", 0f);
                wireMaterial.SetColor("_WireColor", new Color(0.025f, 0.035f, 0.05f, 1f));
            }
        }

        public void SetMesh(Mesh value, bool frame = true)
        {
            DestroyWireframeMesh();
            mesh = value;
            if (mesh == null || !frame)
            {
                return;
            }

            var bounds = mesh.bounds;
            framingRadius = Mathf.Max(0.05f, bounds.extents.magnitude);
            distance = Mathf.Max(0.25f, framingRadius * 3.2f);
            pan = Vector3.zero;
        }

        public void Draw(
            Rect rect,
            VFXPreviewMode mode,
            Color background,
            Texture2D checkerTexture)
        {
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            HandleInput(rect);
            EditorGUI.DrawRect(rect, background);

            if (mesh == null || frontMaterial == null || backfaceMaterial == null || wireMaterial == null)
            {
                DrawCenteredLabel(
                    rect,
                    frontMaterial == null || backfaceMaterial == null || wireMaterial == null
                    ? "Preview shader could not be loaded or is unsupported."
                    : "Adjust the settings to build a preview mesh.");
                return;
            }

            var bounds = mesh.bounds;
            var target = bounds.center + pan;
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            var camera = previewUtility.camera;
            camera.orthographic = orthographic;
            camera.orthographicSize = Mathf.Max(0.05f, framingRadius * 1.2f);
            camera.transform.position = target + rotation * (Vector3.back * distance);
            camera.transform.rotation = rotation;
            camera.nearClipPlane = Mathf.Max(0.001f, distance - framingRadius * 2.5f);
            camera.farClipPlane = distance + framingRadius * 4f + 10f;

            var surfaceMode = mode == VFXPreviewMode.ShadedWireframe
                ? VFXPreviewMode.Shaded
                : mode;
            frontMaterial.SetFloat("_Mode", (float)surfaceMode);
            frontMaterial.SetColor("_BaseColor", new Color(0.58f, 0.76f, 1f, 1f));
            frontMaterial.SetVector("_PreviewLightDir", new Vector4(0.35f, 0.8f, 0.45f, 0f));
            ApplyCheckerTexture(checkerTexture);

            var wireframe = mode == VFXPreviewMode.Wireframe;
            Texture texture = null;
            var previewBegan = false;
            try
            {
                previewUtility.BeginPreview(rect, GUIStyle.none);
                previewBegan = true;
                previewUtility.DrawMesh(mesh, Matrix4x4.identity, backfaceMaterial, 0);
                previewUtility.DrawMesh(mesh, Matrix4x4.identity, frontMaterial, 0);
                if (mode == VFXPreviewMode.ShadedWireframe)
                {
                    EnsureWireframeMesh();
                    if (wireframeMesh != null)
                    {
                        previewUtility.DrawMesh(
                            wireframeMesh,
                            Matrix4x4.identity,
                            wireMaterial,
                            0);
                    }
                }

                GL.wireframe = wireframe;
                previewUtility.Render(true);
                texture = previewUtility.EndPreview();
                previewBegan = false;
            }
            finally
            {
                GL.wireframe = false;
                if (previewBegan)
                {
                    try
                    {
                        previewUtility.EndPreview();
                    }
                    catch
                    {
                        // Preserve the original render exception while restoring preview state.
                    }
                }
            }

            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }

            DrawToolbarHints(rect);
        }

        private void ApplyCheckerTexture(Texture2D checkerTexture)
        {
            if (frontMaterial == null)
            {
                return;
            }

            frontMaterial.SetTexture("_CheckerTexture", checkerTexture);
            frontMaterial.SetFloat("_UseCheckerTexture", checkerTexture != null ? 1f : 0f);
        }

        public void FrameMesh()
        {
            SetMesh(mesh, true);
        }

        public void SetView(Vector2 newOrbit)
        {
            orbit = newOrbit;
        }

        private void HandleInput(Rect rect)
        {
            var current = Event.current;
            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.ScrollWheel)
            {
                var factor = 1f + current.delta.y * 0.08f;
                distance = Mathf.Clamp(distance * factor, framingRadius * 0.15f, framingRadius * 25f + 1f);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0)
            {
                orbit.x += current.delta.x * 0.5f;
                orbit.y = Mathf.Clamp(orbit.y + current.delta.y * 0.5f, -89f, 89f);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && (current.button == 1 || current.button == 2))
            {
                var camera = previewUtility.camera.transform;
                var scale = distance * 0.0025f;
                pan += (-camera.right * current.delta.x + camera.up * current.delta.y) * scale;
                current.Use();
            }
            else if (current.type == EventType.MouseDown && current.button == 0 && current.clickCount == 2)
            {
                FrameMesh();
                current.Use();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F)
            {
                FrameMesh();
                current.Use();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.O)
            {
                orthographic = !orthographic;
                current.Use();
            }
        }

        private static void DrawCenteredLabel(Rect rect, string message)
        {
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUI.Label(rect, message, style);
        }

        private static void DrawToolbarHints(Rect rect)
        {
            var hintRect = new Rect(rect.x + 8f, rect.yMax - 24f, rect.width - 16f, 18f);
            GUI.Label(hintRect, "LMB orbit  |  RMB/MMB pan  |  Wheel zoom  |  F frame  |  O ortho",
                EditorStyles.centeredGreyMiniLabel);
        }

        public void Dispose()
        {
            DestroyWireframeMesh();
            previewUtility.Cleanup();
            if (frontMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(frontMaterial);
            }

            if (backfaceMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(backfaceMaterial);
            }

            if (wireMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(wireMaterial);
            }
        }

        private void EnsureWireframeMesh()
        {
            if (wireframeMesh == null)
            {
                wireframeMesh = CreateWireframeOverlayMesh(mesh);
            }
        }

        private void DestroyWireframeMesh()
        {
            if (wireframeMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(wireframeMesh);
                wireframeMesh = null;
            }
        }

        private static Mesh CreateWireframeOverlayMesh(Mesh source)
        {
            if (source == null || !source.isReadable)
            {
                return null;
            }

            var vertices = source.vertices;
            var triangles = source.triangles;
            var edges = new HashSet<ulong>();
            var lineIndices = new List<int>(triangles.Length * 2);
            for (var triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
            {
                AddEdge(triangles[triangle], triangles[triangle + 1], edges, lineIndices);
                AddEdge(triangles[triangle + 1], triangles[triangle + 2], edges, lineIndices);
                AddEdge(triangles[triangle + 2], triangles[triangle], edges, lineIndices);
            }

            var overlay = new Mesh
            {
                name = source.name + " Wireframe Preview",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = source.indexFormat
            };
            overlay.SetVertices(vertices);
            overlay.SetIndices(lineIndices, MeshTopology.Lines, 0, false);
            overlay.bounds = source.bounds;
            return overlay;
        }

        private static void AddEdge(
            int first,
            int second,
            HashSet<ulong> edges,
            List<int> lineIndices)
        {
            var minimum = Mathf.Min(first, second);
            var maximum = Mathf.Max(first, second);
            var key = ((ulong)(uint)minimum << 32) | (uint)maximum;
            if (!edges.Add(key))
            {
                return;
            }

            lineIndices.Add(minimum);
            lineIndices.Add(maximum);
        }
    }
}
