using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    public sealed class VFXMeshGeneratorWindow : EditorWindow
    {
        private const float ToolbarHeight = 24f;
        private const float MinimumSettingsWidth = 350f;
        private const float MaximumSettingsWidth = 500f;

        [SerializeField] private VFXMeshRecipe recipe = new VFXMeshRecipe();
        [SerializeField] private VFXMeshRecipePreset selectedPreset;
        [SerializeField] private Mesh updateTarget;
        [SerializeField] private string outputFolder = "Assets";
        [SerializeField] private VFXPreviewMode previewMode = VFXPreviewMode.Shaded;
        [SerializeField] private Color previewBackground = new Color(0.11f, 0.12f, 0.14f, 1f);

        private VFXMeshPreviewController preview;
        private VFXMeshBuildResult buildResult;
        private Vector2 settingsScroll;
        private bool rebuildQueued;
        private double rebuildAt;
        private string buildError;
        private VFXModifierType modifierToAdd = VFXModifierType.Noise;

        private bool shapeExpanded = true;
        private bool modifiersExpanded = true;
        private bool uvExpanded = true;
        private bool vertexDataExpanded;
        private bool outputExpanded = true;
        private bool presetsExpanded;

        [MenuItem("Tools/VFX Mesh Generator")]
        public static void Open()
        {
            var window = GetWindow<VFXMeshGeneratorWindow>();
            window.titleContent = new GUIContent("VFX Mesh Generator");
            window.minSize = new Vector2(820f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            recipe ??= new VFXMeshRecipe();
            preview = new VFXMeshPreviewController();
            ScheduleRebuild(0d);
        }

        private void OnDisable()
        {
            ReleaseBuildResult();
            preview?.Dispose();
            preview = null;
        }

        private void Update()
        {
            if (!rebuildQueued || EditorApplication.timeSinceStartup < rebuildAt)
            {
                return;
            }

            rebuildQueued = false;
            RebuildPreview(false);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            var contentRect = new Rect(
                0f,
                ToolbarHeight,
                position.width,
                Mathf.Max(0f, position.height - ToolbarHeight));
            var settingsWidth = Mathf.Clamp(position.width * 0.38f, MinimumSettingsWidth, MaximumSettingsWidth);
            var settingsRect = new Rect(contentRect.x, contentRect.y, settingsWidth, contentRect.height);
            var previewRect = new Rect(
                settingsRect.xMax + 3f,
                contentRect.y + 3f,
                Mathf.Max(0f, contentRect.width - settingsWidth - 6f),
                Mathf.Max(0f, contentRect.height - 6f));

            DrawSettings(settingsRect);
            DrawPreview(previewRect);
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight));
            GUILayout.Label("VFX Mesh Generator", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            previewMode = (VFXPreviewMode)EditorGUILayout.EnumPopup(
                previewMode,
                EditorStyles.toolbarPopup,
                GUILayout.Width(115f));

            if (GUILayout.Button("Front", EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                preview?.SetView(new Vector2(0f, 0f));
                Repaint();
            }

            if (GUILayout.Button("Side", EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                preview?.SetView(new Vector2(90f, 0f));
                Repaint();
            }

            if (GUILayout.Button("Top", EditorStyles.toolbarButton, GUILayout.Width(38f)))
            {
                preview?.SetView(new Vector2(0f, 90f));
                Repaint();
            }

            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                preview?.FrameMesh();
                Repaint();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSettings(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            EditorGUI.BeginChangeCheck();

            recipe.meshName = EditorGUILayout.TextField("Mesh Name", recipe.meshName);
            recipe.shapeType = (VFXMeshShapeType)EditorGUILayout.EnumPopup("Shape", recipe.shapeType);

            EditorGUILayout.Space(3f);
            shapeExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(shapeExpanded, "Shape Settings");
            if (shapeExpanded)
            {
                EditorGUI.indentLevel++;
                DrawShapeSettings(recipe.shapeType, recipe.shape);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            modifiersExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                modifiersExpanded,
                $"Modifiers ({recipe.modifiers.Count})");
            if (modifiersExpanded)
            {
                DrawModifiers();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            uvExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(uvExpanded, "UV0");
            if (uvExpanded)
            {
                EditorGUI.indentLevel++;
                DrawUVSettings(recipe.uv);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            vertexDataExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(vertexDataExpanded, "VFX Vertex Data");
            if (vertexDataExpanded)
            {
                EditorGUI.indentLevel++;
                DrawVertexDataSettings(recipe.vertexData);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            outputExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(outputExpanded, "Mesh Output");
            if (outputExpanded)
            {
                EditorGUI.indentLevel++;
                DrawOutputSettings(recipe.output);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            presetsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(presetsExpanded, "Presets");
            if (presetsExpanded)
            {
                DrawPresetControls();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this);
                ScheduleRebuild();
            }

            EditorGUILayout.Space(8f);
            DrawAssetControls();
            EditorGUILayout.Space(4f);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawShapeSettings(VFXMeshShapeType shapeType, VFXShapeSettings settings)
        {
            switch (shapeType)
            {
                case VFXMeshShapeType.Quad:
                    settings.width = PositiveFloat("Width", settings.width);
                    settings.length = PositiveFloat("Length", settings.length);
                    settings.widthSegments = SegmentField("Width Segments", settings.widthSegments, 1);
                    settings.lengthSegments = SegmentField("Length Segments", settings.lengthSegments, 1);
                    break;

                case VFXMeshShapeType.Disc:
                    settings.radius = PositiveFloat("Radius", settings.radius);
                    settings.radialSegments = SegmentField("Edge Count", settings.radialSegments, 3);
                    settings.widthSegments = SegmentField("Radial Resolution", settings.widthSegments, 1);
                    settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
                    break;

                case VFXMeshShapeType.Ring:
                case VFXMeshShapeType.Arc:
                    settings.innerRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Inner Radius", settings.innerRadius));
                    settings.radius = Mathf.Max(
                        settings.innerRadius + 0.0001f,
                        EditorGUILayout.FloatField("Outer Radius", settings.radius));
                    settings.radialSegments = SegmentField("Edge Count", settings.radialSegments, 3);
                    settings.widthSegments = SegmentField("Radial Resolution", settings.widthSegments, 1);
                    if (settings.radialElevationCurve == null)
                    {
                        settings.radialElevationCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
                    }

                    settings.radialElevationCurve = EditorGUILayout.CurveField(
                        new GUIContent(
                            "Axial Elevation Curve",
                            "Curve time runs from the inner edge or center (0) to the outer edge (1). " +
                            "Curve values are displacement along the selected Main Axis."),
                        settings.radialElevationCurve);
                    settings.arcDegrees = Mathf.Clamp(
                        EditorGUILayout.FloatField("Arc Degrees", settings.arcDegrees),
                        0.1f,
                        360f);
                    settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
                    break;

                case VFXMeshShapeType.Cone:
                    settings.height = PositiveFloat("Height", settings.height);
                    settings.radius = PositiveFloat("Bottom Radius", settings.radius);
                    settings.topRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Top Radius", settings.topRadius));
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Cylinder:
                    settings.height = PositiveFloat("Height", settings.height);
                    settings.radius = PositiveFloat("Radius", settings.radius);
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Tube:
                    settings.height = PositiveFloat("Height", settings.height);
                    settings.innerRadius = Mathf.Max(0f, EditorGUILayout.FloatField("Inner Radius", settings.innerRadius));
                    settings.radius = Mathf.Max(
                        settings.innerRadius + 0.0001f,
                        EditorGUILayout.FloatField("Outer Radius", settings.radius));
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Sphere:
                case VFXMeshShapeType.Hemisphere:
                    settings.radius = PositiveFloat("Radius", settings.radius);
                    settings.longitudeSegments = SegmentField("Longitude", settings.longitudeSegments, 3);
                    settings.latitudeSegments = SegmentField("Latitude", settings.latitudeSegments, 2);
                    if (shapeType == VFXMeshShapeType.Hemisphere)
                    {
                        settings.capEnd = EditorGUILayout.Toggle("Cap", settings.capEnd);
                    }
                    break;

                case VFXMeshShapeType.Torus:
                    settings.radius = PositiveFloat("Major Radius", settings.radius);
                    settings.thickness = PositiveFloat("Minor Radius", settings.thickness);
                    settings.longitudeSegments = SegmentField("Ring Segments", settings.longitudeSegments, 3);
                    settings.radialSegments = SegmentField("Tube Segments", settings.radialSegments, 3);
                    settings.arcDegrees = Mathf.Clamp(
                        EditorGUILayout.FloatField("Arc Degrees", settings.arcDegrees),
                        0.1f,
                        360f);
                    settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
                    break;

                case VFXMeshShapeType.Box:
                    settings.size = EditorGUILayout.Vector3Field("Size", settings.size);
                    settings.size.x = Mathf.Max(0.0001f, settings.size.x);
                    settings.size.y = Mathf.Max(0.0001f, settings.size.y);
                    settings.size.z = Mathf.Max(0.0001f, settings.size.z);
                    settings.widthSegments = SegmentField("X Segments", settings.widthSegments, 1);
                    settings.heightSegments = SegmentField("Y Segments", settings.heightSegments, 1);
                    settings.lengthSegments = SegmentField("Z Segments", settings.lengthSegments, 1);
                    break;

                case VFXMeshShapeType.Ribbon:
                    settings.width = PositiveFloat("Width", settings.width);
                    settings.length = PositiveFloat("Length", settings.length);
                    settings.widthSegments = SegmentField("Width Segments", settings.widthSegments, 1);
                    settings.lengthSegments = SegmentField("Length Segments", settings.lengthSegments, 1);
                    settings.widthCurve = EditorGUILayout.CurveField("Width Curve", settings.widthCurve);
                    break;

                case VFXMeshShapeType.CrossPlanes:
                    settings.width = PositiveFloat("Width", settings.width);
                    settings.length = PositiveFloat("Height", settings.length);
                    settings.planeCount = Mathf.Clamp(
                        EditorGUILayout.IntField("Plane Count", settings.planeCount),
                        2,
                        VFXMeshBuildLimits.MaximumPlaneCount);
                    settings.widthSegments = SegmentField("Width Segments", settings.widthSegments, 1);
                    settings.lengthSegments = SegmentField("Height Segments", settings.lengthSegments, 1);
                    settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
                    break;

                case VFXMeshShapeType.Helix:
                    settings.radius = PositiveFloat("Radius", settings.radius);
                    settings.width = PositiveFloat("Strip Width", settings.width);
                    settings.turns = Mathf.Clamp(
                        EditorGUILayout.FloatField("Turns", settings.turns),
                        0.05f,
                        VFXMeshBuildLimits.MaximumHelixTurns);
                    settings.pitch = EditorGUILayout.FloatField("Pitch", settings.pitch);
                    settings.lengthSegments = SegmentField("Length Segments", settings.lengthSegments, 3);
                    settings.widthSegments = SegmentField("Width Segments", settings.widthSegments, 1);
                    settings.widthCurve = EditorGUILayout.CurveField("Width Curve", settings.widthCurve);
                    settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
                    break;
            }

            settings.axis = (VFXAxis)EditorGUILayout.EnumPopup("Main Axis", settings.axis);
            settings.pivot = (VFXPivot)EditorGUILayout.EnumPopup("Pivot", settings.pivot);
            if (settings.pivot == VFXPivot.Custom)
            {
                settings.customPivotOffset = EditorGUILayout.Vector3Field("Pivot Offset", settings.customPivotOffset);
            }
        }

        private static void DrawRadialVolumeSettings(VFXShapeSettings settings)
        {
            settings.radialSegments = SegmentField("Radial Segments", settings.radialSegments, 3);
            settings.heightSegments = SegmentField("Height Segments", settings.heightSegments, 1);
            settings.arcDegrees = Mathf.Clamp(
                EditorGUILayout.FloatField("Arc Degrees", settings.arcDegrees),
                0.1f,
                360f);
            settings.angleOffset = EditorGUILayout.FloatField("Angle Offset", settings.angleOffset);
            settings.capStart = EditorGUILayout.Toggle("Cap Start", settings.capStart);
            settings.capEnd = EditorGUILayout.Toggle("Cap End", settings.capEnd);
        }

        private void DrawModifiers()
        {
            var removeIndex = -1;
            var moveFrom = -1;
            var moveTo = -1;

            for (var i = 0; i < recipe.modifiers.Count; i++)
            {
                var modifier = recipe.modifiers[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                modifier.enabled = EditorGUILayout.Toggle(modifier.enabled, GUILayout.Width(18f));
                modifier.type = (VFXModifierType)EditorGUILayout.EnumPopup(modifier.type);

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(25f)))
                    {
                        moveFrom = i;
                        moveTo = i - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(i == recipe.modifiers.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(25f)))
                    {
                        moveFrom = i;
                        moveTo = i + 1;
                    }
                }

                if (GUILayout.Button("×", GUILayout.Width(25f)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();

                if (modifier.enabled)
                {
                    EditorGUI.indentLevel++;
                    DrawModifierSettings(modifier);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (moveFrom >= 0)
            {
                var item = recipe.modifiers[moveFrom];
                recipe.modifiers.RemoveAt(moveFrom);
                recipe.modifiers.Insert(moveTo, item);
            }

            if (removeIndex >= 0)
            {
                recipe.modifiers.RemoveAt(removeIndex);
            }

            EditorGUILayout.BeginHorizontal();
            modifierToAdd = (VFXModifierType)EditorGUILayout.EnumPopup(modifierToAdd);
            if (GUILayout.Button("Add Modifier", GUILayout.Width(100f)))
            {
                recipe.modifiers.Add(new VFXMeshModifierSettings { type = modifierToAdd });
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawModifierSettings(VFXMeshModifierSettings modifier)
        {
            modifier.axis = (VFXAxis)EditorGUILayout.EnumPopup("Axis", modifier.axis);

            switch (modifier.type)
            {
                case VFXModifierType.Transform:
                    modifier.offset = EditorGUILayout.Vector3Field("Offset", modifier.offset);
                    modifier.scale = EditorGUILayout.Vector3Field("Scale", modifier.scale);
                    modifier.angle = EditorGUILayout.FloatField("Rotation", modifier.angle);
                    break;

                case VFXModifierType.Twist:
                case VFXModifierType.Bend:
                    modifier.angle = EditorGUILayout.FloatField("Angle", modifier.angle);
                    modifier.falloff = EditorGUILayout.CurveField("Falloff", modifier.falloff);
                    break;

                case VFXModifierType.Noise:
                    modifier.strength = EditorGUILayout.FloatField("Strength", modifier.strength);
                    modifier.frequency = PositiveFloat("Frequency", modifier.frequency);
                    modifier.offset = EditorGUILayout.Vector3Field("Noise Offset", modifier.offset);
                    modifier.seed = EditorGUILayout.IntField("Seed", modifier.seed);
                    modifier.octaves = Mathf.Clamp(
                        EditorGUILayout.IntField("Octaves", modifier.octaves),
                        1,
                        VFXMeshBuildLimits.MaximumNoiseOctaves);
                    modifier.lacunarity = PositiveFloat("Lacunarity", modifier.lacunarity);
                    modifier.persistence = Mathf.Clamp01(EditorGUILayout.FloatField("Persistence", modifier.persistence));
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup("Direction", modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField("Falloff", modifier.falloff);
                    break;

                case VFXModifierType.Wave:
                case VFXModifierType.RadialRipple:
                    modifier.strength = EditorGUILayout.FloatField("Strength", modifier.strength);
                    modifier.frequency = PositiveFloat("Frequency", modifier.frequency);
                    modifier.angle = EditorGUILayout.FloatField("Phase (Degrees)", modifier.angle);
                    modifier.falloff = EditorGUILayout.CurveField("Falloff", modifier.falloff);
                    break;

                case VFXModifierType.Taper:
                case VFXModifierType.Skew:
                case VFXModifierType.Inflate:
                case VFXModifierType.Spherize:
                case VFXModifierType.Flatten:
                    modifier.strength = EditorGUILayout.FloatField("Strength", modifier.strength);
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup("Space", modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField("Falloff", modifier.falloff);
                    break;
            }
        }

        private static void DrawUVSettings(VFXUVSettings settings)
        {
            settings.projection = (VFXUVProjection)EditorGUILayout.EnumPopup("Projection", settings.projection);
            settings.scale = EditorGUILayout.Vector2Field("Scale", settings.scale);
            settings.offset = EditorGUILayout.Vector2Field("Offset", settings.offset);
            settings.rotation = EditorGUILayout.FloatField("Rotation", settings.rotation);
            settings.flipU = EditorGUILayout.Toggle("Flip U", settings.flipU);
            settings.flipV = EditorGUILayout.Toggle("Flip V", settings.flipV);
            settings.swapUV = EditorGUILayout.Toggle("Swap U/V", settings.swapUV);
        }

        private static void DrawVertexDataSettings(VFXVertexDataSettings settings)
        {
            settings.generateColors = EditorGUILayout.Toggle("Vertex Colors", settings.generateColors);
            if (settings.generateColors)
            {
                settings.colorMode = (VFXVertexColorMode)EditorGUILayout.EnumPopup("Color Mode", settings.colorMode);
                settings.solidColor = EditorGUILayout.ColorField("Solid Color", settings.solidColor);
                if (settings.colorMode != VFXVertexColorMode.Solid)
                {
                    settings.colorGradient = EditorGUILayout.GradientField("Gradient", settings.colorGradient);
                    settings.gradientAxis = (VFXAxis)EditorGUILayout.EnumPopup("Gradient Axis", settings.gradientAxis);
                }
            }

            DrawPackedUV("UV1 / TEXCOORD1", ref settings.generateUV1, settings.uv1);
            DrawPackedUV("UV2 / TEXCOORD2", ref settings.generateUV2, settings.uv2);
            DrawPackedUV("UV3 / TEXCOORD3", ref settings.generateUV3, settings.uv3);
        }

        private static void DrawPackedUV(string label, ref bool enabled, VFXChannelPackSettings pack)
        {
            enabled = EditorGUILayout.Toggle(label, enabled);
            if (!enabled)
            {
                return;
            }

            EditorGUI.indentLevel++;
            pack.x = (VFXDataSource)EditorGUILayout.EnumPopup("X", pack.x);
            pack.y = (VFXDataSource)EditorGUILayout.EnumPopup("Y", pack.y);
            pack.z = (VFXDataSource)EditorGUILayout.EnumPopup("Z", pack.z);
            pack.w = (VFXDataSource)EditorGUILayout.EnumPopup("W", pack.w);
            EditorGUI.indentLevel--;
        }

        private static void DrawOutputSettings(VFXMeshOutputSettings settings)
        {
            settings.flipWinding = EditorGUILayout.Toggle("Flip Winding", settings.flipWinding);
            settings.doubleSided = EditorGUILayout.Toggle("Double Sided", settings.doubleSided);
            settings.flatShading = EditorGUILayout.Toggle("Flat Shading", settings.flatShading);
            settings.generateTangents = EditorGUILayout.Toggle("Generate Tangents", settings.generateTangents);
            settings.readWriteEnabled = EditorGUILayout.Toggle("Read/Write Enabled", settings.readWriteEnabled);
            settings.optimizeMesh = EditorGUILayout.Toggle("Optimize Mesh", settings.optimizeMesh);
            settings.compression = (VFXMeshCompression)EditorGUILayout.EnumPopup("Compression", settings.compression);
            settings.indexFormat = (VFXIndexFormatMode)EditorGUILayout.EnumPopup("Index Format", settings.indexFormat);
            settings.boundsPadding = Mathf.Max(0f, EditorGUILayout.FloatField("Bounds Padding", settings.boundsPadding));
        }

        private void DrawPresetControls()
        {
            selectedPreset = (VFXMeshRecipePreset)EditorGUILayout.ObjectField(
                "Preset",
                selectedPreset,
                typeof(VFXMeshRecipePreset),
                false);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedPreset == null))
            {
                if (GUILayout.Button("Load"))
                {
                    recipe = selectedPreset.recipe.DeepCopy();
                    ScheduleRebuild(0d);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Update"))
                {
                    Undo.RecordObject(selectedPreset, "Update VFX Mesh Preset");
                    selectedPreset.recipe = recipe.DeepCopy();
                    EditorUtility.SetDirty(selectedPreset);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button("Save New"))
            {
                SaveNewPreset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetControls()
        {
            EditorGUILayout.LabelField("Asset Output", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Default Folder", outputFolder);
            if (GUILayout.Button("…", GUILayout.Width(28f)))
            {
                SelectOutputFolder();
            }
            EditorGUILayout.EndHorizontal();

            updateTarget = (Mesh)EditorGUILayout.ObjectField(
                "Update Mesh",
                updateTarget,
                typeof(Mesh),
                false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate New", GUILayout.Height(28f)))
            {
                GenerateNewMesh();
            }

            using (new EditorGUI.DisabledScope(updateTarget == null))
            {
                if (GUILayout.Button("Update Existing", GUILayout.Height(28f)))
                {
                    UpdateExistingMesh();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(Rect rect)
        {
            preview?.Draw(rect, previewMode, previewBackground);

            var infoRect = new Rect(rect.x + 8f, rect.y + 8f, Mathf.Min(410f, rect.width - 16f), 62f);
            GUI.Box(infoRect, GUIContent.none, EditorStyles.helpBox);

            if (!string.IsNullOrEmpty(buildError))
            {
                GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 4f, infoRect.width - 12f, infoRect.height - 8f),
                    buildError,
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (buildResult == null)
            {
                GUI.Label(infoRect, "Building preview…", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var bounds = buildResult.bounds;
            var info =
                $"{buildResult.vertexCount:N0} vertices  |  {buildResult.triangleCount:N0} triangles\n" +
                $"Bounds {FormatVector(bounds.size)}  |  Validation: {buildResult.validationFlags}";
            if (!string.IsNullOrEmpty(buildResult.warning))
            {
                info += $"\n{buildResult.warning}";
            }

            GUI.Label(new Rect(infoRect.x + 6f, infoRect.y + 4f, infoRect.width - 12f, infoRect.height - 8f),
                info,
                EditorStyles.wordWrappedMiniLabel);
        }

        private void ScheduleRebuild(double delay = 0.08d)
        {
            rebuildQueued = true;
            rebuildAt = EditorApplication.timeSinceStartup + delay;
        }

        private void RebuildPreview(bool frame)
        {
            ReleaseBuildResult();
            buildError = null;

            try
            {
                buildResult = VFXMeshBuilder.Build(recipe);
                if (!buildResult.succeeded)
                {
                    buildError = buildResult.error;
                }

                preview?.SetMesh(buildResult.mesh, frame);
            }
            catch (Exception exception)
            {
                buildError = exception.Message;
                Debug.LogException(exception);
            }
        }

        private bool EnsureFreshBuild()
        {
            rebuildQueued = false;
            RebuildPreview(false);
            if (buildResult != null && buildResult.succeeded)
            {
                return true;
            }

            ShowNotification(new GUIContent(buildError ?? "The mesh could not be generated."));
            return false;
        }

        private void GenerateNewMesh()
        {
            if (!EnsureFreshBuild())
            {
                return;
            }

            var folder = AssetDatabase.IsValidFolder(outputFolder) ? outputFolder : "Assets";
            var defaultName = SanitizeAssetName(recipe.meshName);
            var path = EditorUtility.SaveFilePanelInProject(
                "Generate VFX Mesh",
                defaultName,
                "asset",
                "Choose where to save the generated mesh.",
                folder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var result = VFXMeshAssetWriter.GenerateNew(buildResult, path);
            if (!result.succeeded)
            {
                EditorUtility.DisplayDialog("VFX Mesh Generator", result.error, "OK");
                return;
            }

            updateTarget = result.mesh;
            outputFolder = Path.GetDirectoryName(result.assetPath)?.Replace('\\', '/') ?? "Assets";
            Selection.activeObject = result.mesh;
            EditorGUIUtility.PingObject(result.mesh);
            ShowNotification(new GUIContent($"Generated {result.assetPath}"));
            ScheduleRebuild(0d);
        }

        private void UpdateExistingMesh()
        {
            if (updateTarget == null || !EnsureFreshBuild())
            {
                return;
            }

            var result = VFXMeshAssetWriter.UpdateExisting(buildResult, updateTarget);
            if (!result.succeeded)
            {
                EditorUtility.DisplayDialog("VFX Mesh Generator", result.error, "OK");
                return;
            }

            updateTarget = result.mesh;
            EditorGUIUtility.PingObject(result.mesh);
            ShowNotification(new GUIContent($"Updated {result.assetPath}"));
            ScheduleRebuild(0d);
        }

        private void SaveNewPreset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save VFX Mesh Preset",
                $"{SanitizeAssetName(recipe.meshName)} Preset",
                "asset",
                "Choose where to save the generator preset.",
                AssetDatabase.IsValidFolder(outputFolder) ? outputFolder : "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var preset = CreateInstance<VFXMeshRecipePreset>();
            preset.recipe = recipe.DeepCopy();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            selectedPreset = preset;
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private void SelectOutputFolder()
        {
            var absolute = EditorUtility.OpenFolderPanel(
                "Select Output Folder",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrEmpty(absolute))
            {
                return;
            }

            var relative = FileUtil.GetProjectRelativePath(absolute);
            if (string.IsNullOrEmpty(relative) || !AssetDatabase.IsValidFolder(relative))
            {
                EditorUtility.DisplayDialog(
                    "VFX Mesh Generator",
                    "Mesh assets must be saved inside this project's Assets folder.",
                    "OK");
                return;
            }

            outputFolder = relative;
        }

        private void ReleaseBuildResult()
        {
            if (buildResult?.mesh != null && !AssetDatabase.Contains(buildResult.mesh))
            {
                DestroyImmediate(buildResult.mesh);
            }

            buildResult = null;
        }

        private static int SegmentField(string label, int value, int minimum)
        {
            return Mathf.Clamp(
                EditorGUILayout.IntField(label, value),
                minimum,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
        }

        private static float PositiveFloat(string label, float value)
        {
            return Mathf.Max(0.0001f, EditorGUILayout.FloatField(label, value));
        }

        private static string SanitizeAssetName(string value)
        {
            var name = string.IsNullOrWhiteSpace(value) ? "VFXMesh" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";
        }
    }
}
