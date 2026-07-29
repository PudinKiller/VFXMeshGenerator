using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor
{
    public sealed class VFXMeshLabWindow : EditorWindow
    {
        private const float ToolbarHeight = 24f;
        private const float MinimumSettingsWidth = 350f;
        private const float MaximumSettingsWidth = 500f;
        private const int PersistentStateVersion = 1;
        private const double PersistentStateSaveDelay = 0.2d;
        private const string PersistentStateKeyPrefix =
            "com.pudinkiller.vfx-mesh-lab.window-state.";

        [Serializable]
        private sealed class ShapeSettingsEntry
        {
            public VFXMeshShapeType shapeType;
            public VFXShapeSettings settings;
        }

        [Serializable]
        private sealed class PersistentState
        {
            public int version = PersistentStateVersion;
            public VFXMeshRecipe recipe = new VFXMeshRecipe();
            public List<ShapeSettingsEntry> shapeSettingsByType =
                new List<ShapeSettingsEntry>();
            public string outputFolder = "Assets";
            public int previewMode;
            public Color previewBackground = new Color(0.11f, 0.12f, 0.14f, 1f);
            public string previewCheckerTextureGuid;
            public int modifierToAdd = (int)VFXModifierType.Noise;
            public bool shapeExpanded = true;
            public bool modifiersExpanded = true;
            public bool uvExpanded = true;
            public bool vertexDataExpanded;
            public bool outputExpanded = true;
            public bool presetsExpanded;
            public string selectedPresetGuid;
        }

        [SerializeField] private VFXMeshRecipe recipe = new VFXMeshRecipe();
        [SerializeField] private List<ShapeSettingsEntry> shapeSettingsByType =
            new List<ShapeSettingsEntry>();
        [SerializeField] private VFXMeshRecipePreset selectedPreset;
        [SerializeField] private Mesh updateTarget;
        [SerializeField] private string outputFolder = "Assets";
        [SerializeField] private VFXPreviewMode previewMode = VFXPreviewMode.Shaded;
        [SerializeField] private Color previewBackground = new Color(0.11f, 0.12f, 0.14f, 1f);
        [SerializeField] private Texture2D previewCheckerTexture;

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
        private string persistentStateKey;
        private bool persistentStateSavePending;
        private double persistentStateSaveTime;

        [MenuItem("Tools/VFX Mesh Lab")]
        public static void Open()
        {
            var window = GetWindow<VFXMeshLabWindow>();
            window.titleContent = new GUIContent("VFX Mesh Lab");
            window.minSize = new Vector2(820f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            persistentStateKey = BuildPersistentStateKey();
            LoadPersistentState();
            EnsureRecipeState();
            NormalizeShapeSettingsMemory();
            RememberShapeSettings(recipe.shapeType, recipe.shape);
            outputFolder = IsWritableAssetFolder(outputFolder)
                ? NormalizeAssetPath(outputFolder)
                : "Assets";
            preview = new VFXMeshPreviewController();
            ScheduleRebuild(0d);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SavePersistentState();
            EditorApplication.update -= SavePersistentStateWhenDue;
            persistentStateSavePending = false;
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
            GUILayout.Label("VFX Mesh Lab", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var selectedPreviewMode = (VFXPreviewMode)EditorGUILayout.EnumPopup(
                previewMode,
                EditorStyles.toolbarPopup,
                GUILayout.Width(145f));
            if (selectedPreviewMode != previewMode)
            {
                previewMode = selectedPreviewMode;
                QueuePersistentStateSave();
            }
            DrawTooltipOverLastControl(VFXMeshLabContent.PreviewMode);

            if (previewMode == VFXPreviewMode.UVChecker)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    VFXMeshLabContent.CheckerTexture,
                    EditorStyles.miniLabel,
                    GUILayout.Width(48f));
                EditorGUI.BeginChangeCheck();
                var selectedCheckerTexture = (Texture2D)EditorGUILayout.ObjectField(
                    previewCheckerTexture,
                    typeof(Texture2D),
                    false,
                    GUILayout.Width(120f));
                if (EditorGUI.EndChangeCheck())
                {
                    previewCheckerTexture = selectedCheckerTexture;
                    QueuePersistentStateSave();
                    Repaint();
                }
                DrawTooltipOverLastControl(VFXMeshLabContent.CheckerTexture);
            }

            if (GUILayout.Button(
                    VFXMeshLabContent.FrontView,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(44f)))
            {
                preview?.SetView(new Vector2(0f, 0f));
                Repaint();
            }

            if (GUILayout.Button(
                    VFXMeshLabContent.SideView,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(42f)))
            {
                preview?.SetView(new Vector2(90f, 0f));
                Repaint();
            }

            if (GUILayout.Button(
                    VFXMeshLabContent.TopView,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(38f)))
            {
                preview?.SetView(new Vector2(0f, 90f));
                Repaint();
            }

            if (GUILayout.Button(
                    VFXMeshLabContent.FrameView,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(48f)))
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

            recipe.meshName = EditorGUILayout.TextField(VFXMeshLabContent.MeshName, recipe.meshName);
            EditorGUILayout.BeginHorizontal();
            var selectedShape = (VFXMeshShapeType)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.Shape,
                recipe.shapeType);
            if (GUILayout.Button(
                    VFXMeshLabContent.ResetShapeProfile,
                    GUILayout.Width(52f)))
            {
                ResetCurrentShapeProfile();
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();
            if (selectedShape != recipe.shapeType)
            {
                SwitchShape(selectedShape);
            }

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
                QueuePersistentStateSave();
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
                    settings.width = PositiveFloat(new GUIContent("Width"), settings.width);
                    settings.length = PositiveFloat(new GUIContent("Length"), settings.length);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.WidthSegments,
                        settings.widthSegments,
                        1);
                    settings.lengthSegments = SegmentField(
                        VFXMeshLabContent.LengthSegments,
                        settings.lengthSegments,
                        1);
                    DrawScaleCurve(settings, VFXMeshLabContent.WidthCurve);
                    DrawWidthProfileUVMode(settings);
                    break;

                case VFXMeshShapeType.Disc:
                    settings.radius = PositiveFloat(new GUIContent("Radius"), settings.radius);
                    settings.radialSegments = SegmentField(
                        VFXMeshLabContent.EdgeCount,
                        settings.radialSegments,
                        3);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.RadialResolution,
                        settings.widthSegments,
                        1);
                    DrawRadialDistribution(settings);
                    DrawRadialElevation(settings);
                    settings.arcDegrees = Mathf.Clamp(
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.DiscArcDegrees,
                            settings.arcDegrees),
                        0.1f,
                        360f);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;

                case VFXMeshShapeType.Ring:
                    DrawAnnulusSettings(settings);
                    DrawRadialDistribution(settings);
                    DrawRadialElevation(settings);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;

                case VFXMeshShapeType.Arc:
                    DrawAnnulusSettings(settings);
                    DrawRadialDistribution(settings);
                    DrawRadialElevation(settings);
                    if (settings.arcWidthCurve == null)
                    {
                        settings.arcWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
                    }

                    settings.arcWidthCurve = EditorGUILayout.CurveField(
                        VFXMeshLabContent.ArcWidthCurve,
                        settings.arcWidthCurve);
                    settings.arcWidthOrigin = (VFXArcWidthOrigin)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.ArcWidthOrigin,
                        settings.arcWidthOrigin);
                    settings.mirrorArcAcrossShapePlane = EditorGUILayout.Toggle(
                        VFXMeshLabContent.MirrorArcAcrossShapePlane,
                        settings.mirrorArcAcrossShapePlane);
                    settings.arcDegrees = Mathf.Clamp(
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.ArcDegrees,
                            settings.arcDegrees),
                        0.1f,
                        360f);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;

                case VFXMeshShapeType.Cone:
                    settings.height = PositiveFloat(new GUIContent("Height"), settings.height);
                    settings.radius = PositiveFloat(new GUIContent("Bottom Radius"), settings.radius);
                    settings.topRadius = Mathf.Max(
                        0f,
                        EditorGUILayout.FloatField(
                            new GUIContent("Top Radius", "Set to zero for a pointed cone."),
                            settings.topRadius));
                    DrawScaleCurve(settings, VFXMeshLabContent.RadiusScaleCurve);
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Cylinder:
                    settings.height = PositiveFloat(new GUIContent("Height"), settings.height);
                    settings.radius = PositiveFloat(new GUIContent("Radius"), settings.radius);
                    DrawScaleCurve(settings, VFXMeshLabContent.RadiusScaleCurve);
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Tube:
                    settings.height = PositiveFloat(new GUIContent("Height"), settings.height);
                    settings.innerRadius = Mathf.Max(
                        0f,
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.InnerRadius,
                            settings.innerRadius));
                    settings.radius = Mathf.Max(
                        settings.innerRadius + 0.0001f,
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.OuterRadius,
                            settings.radius));
                    DrawScaleCurve(settings, VFXMeshLabContent.RadiusScaleCurve);
                    DrawRadialVolumeSettings(settings);
                    break;

                case VFXMeshShapeType.Sphere:
                case VFXMeshShapeType.Hemisphere:
                    settings.radius = PositiveFloat(new GUIContent("Radius"), settings.radius);
                    settings.longitudeSegments = SegmentField(
                        VFXMeshLabContent.Longitude,
                        settings.longitudeSegments,
                        3);
                    settings.latitudeSegments = SegmentField(
                        VFXMeshLabContent.Latitude,
                        settings.latitudeSegments,
                        2);
                    DrawScaleCurve(
                        settings,
                        VFXMeshLabContent.RadialProfileScaleCurve);
                    if (shapeType == VFXMeshShapeType.Hemisphere)
                    {
                        settings.capEnd = EditorGUILayout.Toggle(
                            VFXMeshLabContent.HemisphereCap,
                            settings.capEnd);
                    }
                    break;

                case VFXMeshShapeType.Torus:
                    settings.radius = PositiveFloat(VFXMeshLabContent.MajorRadius, settings.radius);
                    settings.thickness = PositiveFloat(
                        VFXMeshLabContent.MinorRadius,
                        settings.thickness);
                    settings.longitudeSegments = SegmentField(
                        VFXMeshLabContent.RingSegments,
                        settings.longitudeSegments,
                        3);
                    settings.radialSegments = SegmentField(
                        VFXMeshLabContent.TubeSegments,
                        settings.radialSegments,
                        3);
                    DrawScaleCurve(settings, VFXMeshLabContent.TubeScaleCurve);
                    settings.arcDegrees = Mathf.Clamp(
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.VolumeArcDegrees,
                            settings.arcDegrees),
                        0.1f,
                        360f);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;

                case VFXMeshShapeType.Box:
                    settings.size = EditorGUILayout.Vector3Field(
                        new GUIContent("Size", "Full local-space dimensions of the box."),
                        settings.size);
                    settings.size.x = Mathf.Max(0.0001f, settings.size.x);
                    settings.size.y = Mathf.Max(0.0001f, settings.size.y);
                    settings.size.z = Mathf.Max(0.0001f, settings.size.z);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.XSegments,
                        settings.widthSegments,
                        1);
                    settings.heightSegments = SegmentField(
                        VFXMeshLabContent.YSegments,
                        settings.heightSegments,
                        1);
                    settings.lengthSegments = SegmentField(
                        VFXMeshLabContent.ZSegments,
                        settings.lengthSegments,
                        1);
                    DrawScaleCurve(
                        settings,
                        VFXMeshLabContent.CrossSectionScaleCurve);
                    break;

                case VFXMeshShapeType.Ribbon:
                    settings.width = PositiveFloat(new GUIContent("Width"), settings.width);
                    settings.length = PositiveFloat(new GUIContent("Length"), settings.length);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.WidthSegments,
                        settings.widthSegments,
                        1);
                    settings.lengthSegments = SegmentField(
                        VFXMeshLabContent.LengthSegments,
                        settings.lengthSegments,
                        1);
                    DrawScaleCurve(settings, VFXMeshLabContent.WidthCurve);
                    DrawWidthProfileUVMode(settings);
                    break;

                case VFXMeshShapeType.CrossPlanes:
                    settings.width = PositiveFloat(new GUIContent("Width"), settings.width);
                    settings.length = PositiveFloat(new GUIContent("Height"), settings.length);
                    settings.planeCount = Mathf.Clamp(
                        EditorGUILayout.IntField(
                            VFXMeshLabContent.PlaneCount,
                            settings.planeCount),
                        2,
                        VFXMeshBuildLimits.MaximumPlaneCount);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.WidthSegments,
                        settings.widthSegments,
                        1);
                    settings.lengthSegments = SegmentField(
                        VFXMeshLabContent.HeightSegments,
                        settings.lengthSegments,
                        1);
                    DrawScaleCurve(settings, VFXMeshLabContent.WidthCurve);
                    DrawWidthProfileUVMode(settings);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;

                case VFXMeshShapeType.Helix:
                    settings.radius = PositiveFloat(new GUIContent("Radius"), settings.radius);
                    settings.width = PositiveFloat(
                        new GUIContent("Strip Width", "Base width of the helix ribbon."),
                        settings.width);
                    settings.turns = Mathf.Clamp(
                        EditorGUILayout.FloatField(VFXMeshLabContent.Turns, settings.turns),
                        0.05f,
                        VFXMeshBuildLimits.MaximumHelixTurns);
                    settings.pitch = EditorGUILayout.FloatField(
                        VFXMeshLabContent.Pitch,
                        settings.pitch);
                    settings.lengthSegments = SegmentField(
                        VFXMeshLabContent.HelixLengthSegments,
                        settings.lengthSegments,
                        3);
                    settings.widthSegments = SegmentField(
                        VFXMeshLabContent.WidthSegments,
                        settings.widthSegments,
                        1);
                    DrawScaleCurve(settings, VFXMeshLabContent.WidthCurve);
                    DrawWidthProfileUVMode(settings);
                    settings.angleOffset = EditorGUILayout.FloatField(
                        VFXMeshLabContent.AngleOffset,
                        settings.angleOffset);
                    break;
            }

            settings.axis = (VFXAxis)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.MainAxis,
                settings.axis);
            settings.pivot = (VFXPivot)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.Pivot,
                settings.pivot);
            if (settings.pivot == VFXPivot.Custom)
            {
                settings.customPivotOffset = EditorGUILayout.Vector3Field(
                    VFXMeshLabContent.CustomPivotPosition,
                    settings.customPivotOffset);
            }
        }

        private void SwitchShape(VFXMeshShapeType selectedShape)
        {
            EnsureRecipeState();
            if (!Enum.IsDefined(typeof(VFXMeshShapeType), selectedShape) ||
                selectedShape == recipe.shapeType)
            {
                return;
            }

            RememberShapeSettings(recipe.shapeType, recipe.shape);
            recipe.shapeType = selectedShape;
            recipe.shape = RecallShapeSettings(selectedShape);
        }

        private void ResetCurrentShapeProfile()
        {
            EnsureRecipeState();
            Undo.RegisterCompleteObjectUndo(
                this,
                "Reset " +
                ObjectNames.NicifyVariableName(recipe.shapeType.ToString()) +
                " Shape Profile");
            recipe.shape = CreateDefaultShapeSettings(recipe.shapeType);
            RememberShapeSettings(recipe.shapeType, recipe.shape);
            if (Event.current != null)
            {
                GUI.FocusControl(null);
            }
            EditorUtility.SetDirty(this);
            QueuePersistentStateSave();
            ScheduleRebuild(0d);
            Repaint();
        }

        private void OnUndoRedo()
        {
            EnsureRecipeState();
            NormalizeShapeSettingsMemory();
            RememberShapeSettings(recipe.shapeType, recipe.shape);
            QueuePersistentStateSave();
            ScheduleRebuild(0d);
            Repaint();
        }

        private static VFXShapeSettings CreateDefaultShapeSettings(
            VFXMeshShapeType shapeType)
        {
            var settings = new VFXShapeSettings();
            if (shapeType == VFXMeshShapeType.Arc)
            {
                settings.arcDegrees = 180f;
            }

            return settings;
        }

        private static VFXShapeSettings CloneShapeSettings(
            VFXShapeSettings source)
        {
            var clone = new VFXShapeSettings();
            if (source != null)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            }

            return clone;
        }

        private void RememberShapeSettings(
            VFXMeshShapeType shapeType,
            VFXShapeSettings settings)
        {
            if (!Enum.IsDefined(typeof(VFXMeshShapeType), shapeType))
            {
                return;
            }

            shapeSettingsByType ??= new List<ShapeSettingsEntry>();
            var snapshot = settings == null
                ? CreateDefaultShapeSettings(shapeType)
                : CloneShapeSettings(settings);
            var existingIndex = -1;
            for (var index = shapeSettingsByType.Count - 1; index >= 0; index--)
            {
                var entry = shapeSettingsByType[index];
                if (entry == null || entry.shapeType != shapeType)
                {
                    continue;
                }

                if (existingIndex < 0)
                {
                    existingIndex = index;
                }
                else
                {
                    shapeSettingsByType.RemoveAt(index);
                    existingIndex--;
                }
            }

            if (existingIndex >= 0)
            {
                shapeSettingsByType[existingIndex].settings = snapshot;
            }
            else
            {
                shapeSettingsByType.Add(
                    new ShapeSettingsEntry
                    {
                        shapeType = shapeType,
                        settings = snapshot
                    });
            }
        }

        private VFXShapeSettings RecallShapeSettings(
            VFXMeshShapeType shapeType)
        {
            if (shapeSettingsByType != null)
            {
                for (var index = shapeSettingsByType.Count - 1; index >= 0; index--)
                {
                    var entry = shapeSettingsByType[index];
                    if (entry != null &&
                        entry.shapeType == shapeType &&
                        entry.settings != null)
                    {
                        return CloneShapeSettings(entry.settings);
                    }
                }
            }

            return CreateDefaultShapeSettings(shapeType);
        }

        private void NormalizeShapeSettingsMemory()
        {
            var source = shapeSettingsByType;
            shapeSettingsByType = new List<ShapeSettingsEntry>();
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var entry = source[index];
                if (entry == null ||
                    entry.settings == null ||
                    !Enum.IsDefined(typeof(VFXMeshShapeType), entry.shapeType))
                {
                    continue;
                }

                RememberShapeSettings(entry.shapeType, entry.settings);
            }
        }

        private void AdoptRecipe(VFXMeshRecipe incomingRecipe)
        {
            EnsureRecipeState();
            RememberShapeSettings(recipe.shapeType, recipe.shape);

            recipe = incomingRecipe?.DeepCopy() ?? new VFXMeshRecipe();
            EnsureRecipeState();
            RememberShapeSettings(recipe.shapeType, recipe.shape);
        }

        private static void DrawAnnulusSettings(VFXShapeSettings settings)
        {
            settings.innerRadius = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    VFXMeshLabContent.InnerRadius,
                    settings.innerRadius));
            settings.radius = Mathf.Max(
                settings.innerRadius + 0.0001f,
                EditorGUILayout.FloatField(
                    VFXMeshLabContent.OuterRadius,
                    settings.radius));
            settings.radialSegments = SegmentField(
                VFXMeshLabContent.EdgeCount,
                settings.radialSegments,
                3);
            settings.widthSegments = SegmentField(
                VFXMeshLabContent.RadialResolution,
                settings.widthSegments,
                1);
        }

        private static void DrawRadialElevation(VFXShapeSettings settings)
        {
            settings.radialElevationCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 0f);
            settings.radialElevationCurve = EditorGUILayout.CurveField(
                VFXMeshLabContent.AxialElevationCurve,
                settings.radialElevationCurve);
        }

        private static void DrawRadialDistribution(VFXShapeSettings settings)
        {
            settings.radialDistributionCurve ??=
                AnimationCurve.Linear(0f, 0f, 1f, 1f);
            settings.radialDistributionCurve = EditorGUILayout.CurveField(
                VFXMeshLabContent.RadialVertexDistribution,
                settings.radialDistributionCurve);
        }

        private static void DrawScaleCurve(
            VFXShapeSettings settings,
            GUIContent content)
        {
            settings.widthCurve ??= AnimationCurve.Linear(0f, 1f, 1f, 1f);
            settings.widthCurve = EditorGUILayout.CurveField(
                content,
                settings.widthCurve);
        }

        private static void DrawWidthProfileUVMode(VFXShapeSettings settings)
        {
            settings.ribbonUVWidthMode =
                (VFXRibbonUVWidthMode)EditorGUILayout.EnumPopup(
                    VFXMeshLabContent.RibbonUVWidthMode,
                    settings.ribbonUVWidthMode);
        }

        private static void DrawRadialVolumeSettings(VFXShapeSettings settings)
        {
            settings.radialSegments = SegmentField(
                VFXMeshLabContent.RadialSegments,
                settings.radialSegments,
                3);
            settings.heightSegments = SegmentField(
                VFXMeshLabContent.HeightSegments,
                settings.heightSegments,
                1);
            settings.arcDegrees = Mathf.Clamp(
                EditorGUILayout.FloatField(
                    VFXMeshLabContent.VolumeArcDegrees,
                    settings.arcDegrees),
                0.1f,
                360f);
            settings.angleOffset = EditorGUILayout.FloatField(
                VFXMeshLabContent.AngleOffset,
                settings.angleOffset);
            settings.capStart = EditorGUILayout.Toggle(
                VFXMeshLabContent.CapStart,
                settings.capStart);
            settings.capEnd = EditorGUILayout.Toggle(
                VFXMeshLabContent.CapEnd,
                settings.capEnd);
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
                modifier.enabled = GUILayout.Toggle(
                    modifier.enabled,
                    VFXMeshLabContent.ModifierEnabled,
                    GUILayout.Width(18f));
                modifier.type = (VFXModifierType)EditorGUILayout.EnumPopup(modifier.type);
                DrawTooltipOverLastControl(VFXMeshLabContent.ModifierType);

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button(VFXMeshLabContent.MoveEarlier, GUILayout.Width(25f)))
                    {
                        moveFrom = i;
                        moveTo = i - 1;
                    }
                }

                using (new EditorGUI.DisabledScope(i == recipe.modifiers.Count - 1))
                {
                    if (GUILayout.Button(VFXMeshLabContent.MoveLater, GUILayout.Width(25f)))
                    {
                        moveFrom = i;
                        moveTo = i + 1;
                    }
                }

                if (GUILayout.Button(VFXMeshLabContent.RemoveModifier, GUILayout.Width(25f)))
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
                GUI.changed = true;
            }

            if (removeIndex >= 0)
            {
                recipe.modifiers.RemoveAt(removeIndex);
                GUI.changed = true;
            }

            EditorGUILayout.BeginHorizontal();
            modifierToAdd = (VFXModifierType)EditorGUILayout.EnumPopup(modifierToAdd);
            DrawTooltipOverLastControl(VFXMeshLabContent.ModifierToAdd);
            if (GUILayout.Button(VFXMeshLabContent.AddModifier, GUILayout.Width(100f)))
            {
                recipe.modifiers.Add(new VFXMeshModifierSettings { type = modifierToAdd });
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawModifierSettings(VFXMeshModifierSettings modifier)
        {
            modifier.axis = (VFXAxis)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.ModifierAxis,
                modifier.axis);

            switch (modifier.type)
            {
                case VFXModifierType.Transform:
                    modifier.offset = EditorGUILayout.Vector3Field(
                        VFXMeshLabContent.TransformOffset,
                        modifier.offset);
                    modifier.scale = EditorGUILayout.Vector3Field(
                        VFXMeshLabContent.TransformScale,
                        modifier.scale);
                    modifier.angle = EditorGUILayout.FloatField(
                        VFXMeshLabContent.TransformRotation,
                        modifier.angle);
                    break;

                case VFXModifierType.Twist:
                case VFXModifierType.Bend:
                    modifier.angle = EditorGUILayout.FloatField(
                        modifier.type == VFXModifierType.Twist
                            ? VFXMeshLabContent.TwistAngle
                            : VFXMeshLabContent.BendAngle,
                        modifier.angle);
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.Space(modifier.type),
                        modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField(
                        VFXMeshLabContent.Falloff,
                        modifier.falloff);
                    break;

                case VFXModifierType.Noise:
                    modifier.strength = EditorGUILayout.FloatField(
                        VFXMeshLabContent.Strength(modifier.type),
                        modifier.strength);
                    modifier.frequency = PositiveFloat(
                        VFXMeshLabContent.NoiseFrequency,
                        modifier.frequency);
                    modifier.offset = EditorGUILayout.Vector3Field(
                        VFXMeshLabContent.NoiseOffset,
                        modifier.offset);
                    modifier.seed = EditorGUILayout.IntField(VFXMeshLabContent.Seed, modifier.seed);
                    modifier.octaves = Mathf.Clamp(
                        EditorGUILayout.IntField(VFXMeshLabContent.Octaves, modifier.octaves),
                        1,
                        VFXMeshBuildLimits.MaximumNoiseOctaves);
                    modifier.lacunarity = PositiveFloat(
                        VFXMeshLabContent.Lacunarity,
                        modifier.lacunarity);
                    modifier.persistence = Mathf.Clamp01(
                        EditorGUILayout.FloatField(
                            VFXMeshLabContent.Persistence,
                            modifier.persistence));
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.NoiseDirection,
                        modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField(
                        VFXMeshLabContent.Falloff,
                        modifier.falloff);
                    break;

                case VFXModifierType.Wave:
                case VFXModifierType.RadialRipple:
                    modifier.strength = EditorGUILayout.FloatField(
                        VFXMeshLabContent.Strength(modifier.type),
                        modifier.strength);
                    modifier.frequency = PositiveFloat(
                        VFXMeshLabContent.WaveFrequency,
                        modifier.frequency);
                    modifier.angle = EditorGUILayout.FloatField(
                        VFXMeshLabContent.Phase,
                        modifier.angle);
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.Space(modifier.type),
                        modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField(
                        VFXMeshLabContent.Falloff,
                        modifier.falloff);
                    break;

                case VFXModifierType.Taper:
                case VFXModifierType.Skew:
                case VFXModifierType.Inflate:
                case VFXModifierType.Spherize:
                case VFXModifierType.Flatten:
                    modifier.strength = EditorGUILayout.FloatField(
                        VFXMeshLabContent.Strength(modifier.type),
                        modifier.strength);
                    modifier.space = (VFXModifierSpace)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.Space(modifier.type),
                        modifier.space);
                    modifier.falloff = EditorGUILayout.CurveField(
                        VFXMeshLabContent.Falloff,
                        modifier.falloff);
                    break;
            }
        }

        private static void DrawUVSettings(VFXUVSettings settings)
        {
            settings.projection = (VFXUVProjection)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.UVProjection,
                settings.projection);
            settings.scale = EditorGUILayout.Vector2Field(VFXMeshLabContent.UVScale, settings.scale);
            settings.offset = EditorGUILayout.Vector2Field(VFXMeshLabContent.UVOffset, settings.offset);
            settings.rotation = EditorGUILayout.FloatField(
                VFXMeshLabContent.UVRotation,
                settings.rotation);
            settings.flipU = EditorGUILayout.Toggle(VFXMeshLabContent.FlipU, settings.flipU);
            settings.flipV = EditorGUILayout.Toggle(VFXMeshLabContent.FlipV, settings.flipV);
            settings.swapUV = EditorGUILayout.Toggle(VFXMeshLabContent.SwapUV, settings.swapUV);
        }

        private static void DrawVertexDataSettings(VFXVertexDataSettings settings)
        {
            settings.generateColors = EditorGUILayout.Toggle(
                VFXMeshLabContent.VertexColors,
                settings.generateColors);
            if (settings.generateColors)
            {
                settings.colorMode = (VFXVertexColorMode)EditorGUILayout.EnumPopup(
                    VFXMeshLabContent.ColorMode,
                    settings.colorMode);
                if (settings.colorMode == VFXVertexColorMode.Solid)
                {
                    settings.solidColor = EditorGUILayout.ColorField(
                        VFXMeshLabContent.SolidColor,
                        settings.solidColor);
                }
                else
                {
                    settings.colorGradient = EditorGUILayout.GradientField(
                        VFXMeshLabContent.ColorGradient,
                        settings.colorGradient);
                    settings.gradientAxis = (VFXAxis)EditorGUILayout.EnumPopup(
                        VFXMeshLabContent.GradientAxis,
                        settings.gradientAxis);
                }
            }

            DrawPackedUV(VFXMeshLabContent.UV1, ref settings.generateUV1, settings.uv1);
            DrawPackedUV(VFXMeshLabContent.UV2, ref settings.generateUV2, settings.uv2);
            DrawPackedUV(VFXMeshLabContent.UV3, ref settings.generateUV3, settings.uv3);
        }

        private static void DrawPackedUV(GUIContent label, ref bool enabled, VFXChannelPackSettings pack)
        {
            enabled = EditorGUILayout.Toggle(label, enabled);
            if (!enabled)
            {
                return;
            }

            EditorGUI.indentLevel++;
            pack.x = (VFXDataSource)EditorGUILayout.EnumPopup(VFXMeshLabContent.ChannelX, pack.x);
            pack.y = (VFXDataSource)EditorGUILayout.EnumPopup(VFXMeshLabContent.ChannelY, pack.y);
            pack.z = (VFXDataSource)EditorGUILayout.EnumPopup(VFXMeshLabContent.ChannelZ, pack.z);
            pack.w = (VFXDataSource)EditorGUILayout.EnumPopup(VFXMeshLabContent.ChannelW, pack.w);
            EditorGUI.indentLevel--;
        }

        private static void DrawOutputSettings(VFXMeshOutputSettings settings)
        {
            settings.flipWinding = EditorGUILayout.Toggle(
                VFXMeshLabContent.FlipWinding,
                settings.flipWinding);
            settings.doubleSided = EditorGUILayout.Toggle(
                VFXMeshLabContent.DoubleSided,
                settings.doubleSided);
            settings.flatShading = EditorGUILayout.Toggle(
                VFXMeshLabContent.FlatShading,
                settings.flatShading);
            settings.generateTangents = EditorGUILayout.Toggle(
                VFXMeshLabContent.GenerateTangents,
                settings.generateTangents);
            settings.readWriteEnabled = EditorGUILayout.Toggle(
                VFXMeshLabContent.ReadWrite,
                settings.readWriteEnabled);
            settings.optimizeMesh = EditorGUILayout.Toggle(
                VFXMeshLabContent.OptimizeMesh,
                settings.optimizeMesh);
            settings.compression = (VFXMeshCompression)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.Compression,
                settings.compression);
            settings.indexFormat = (VFXIndexFormatMode)EditorGUILayout.EnumPopup(
                VFXMeshLabContent.IndexFormat,
                settings.indexFormat);
            settings.boundsPadding = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    VFXMeshLabContent.BoundsPadding,
                    settings.boundsPadding));
        }

        private void DrawPresetControls()
        {
            if (GUILayout.Button(VFXMeshLabContent.ApplyBuiltInTemplate))
            {
                ShowBuiltInTemplateMenu(GUILayoutUtility.GetLastRect());
            }

            selectedPreset = (VFXMeshRecipePreset)EditorGUILayout.ObjectField(
                VFXMeshLabContent.Preset,
                selectedPreset,
                typeof(VFXMeshRecipePreset),
                false);

            var canUpdatePreset =
                VFXBuiltInTemplateLibrary.IsEditableProjectPreset(selectedPreset);
            if (selectedPreset != null && !canUpdatePreset)
            {
                EditorGUILayout.HelpBox(
                    VFXMeshLabContent.ReadOnlyPresetMessage,
                    MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedPreset == null))
            {
                if (GUILayout.Button(VFXMeshLabContent.LoadPreset))
                {
                    AdoptRecipe(selectedPreset.recipe);
                    SavePersistentState();
                    ScheduleRebuild(0d);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(!canUpdatePreset))
            {
                if (GUILayout.Button(VFXMeshLabContent.UpdatePreset))
                {
                    Undo.RecordObject(selectedPreset, "Update VFX Mesh Preset");
                    selectedPreset.recipe = recipe.DeepCopy();
                    EditorUtility.SetDirty(selectedPreset);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button(VFXMeshLabContent.SavePreset))
            {
                SaveNewPreset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowBuiltInTemplateMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            for (var index = 0;
                 index < VFXBuiltInTemplateLibrary.Count;
                 index++)
            {
                var templateIndex = index;
                menu.AddItem(
                    new GUIContent(
                        VFXBuiltInTemplateLibrary.GetDisplayName(templateIndex)),
                    false,
                    () => ApplyBuiltInTemplate(templateIndex));
            }

            menu.DropDown(buttonRect);
        }

        private void ApplyBuiltInTemplate(int templateIndex)
        {
            var template = VFXBuiltInTemplateLibrary.Load(templateIndex);
            if (template == null || template.recipe == null)
            {
                ShowNotification(
                    new GUIContent("The selected built-in template could not be loaded."));
                return;
            }

            selectedPreset = template;
            AdoptRecipe(template.recipe);
            SavePersistentState();
            ScheduleRebuild(0d);
            Repaint();
        }

        private void DrawAssetControls()
        {
            EditorGUILayout.LabelField("Asset Output", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            var currentFolderAsset =
                IsWritableAssetFolder(outputFolder)
                    ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputFolder)
                    : null;
            EditorGUI.BeginChangeCheck();
            var selectedFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(
                VFXMeshLabContent.DefaultFolder,
                currentFolderAsset,
                typeof(DefaultAsset),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedFolderAsset == null)
                {
                    SetOutputFolder("Assets", false);
                }
                else
                {
                    SetOutputFolder(AssetDatabase.GetAssetPath(selectedFolderAsset), true);
                }
            }

            if (GUILayout.Button(VFXMeshLabContent.BrowseFolder, GUILayout.Width(28f)))
            {
                SelectOutputFolder();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(outputFolder, EditorStyles.miniLabel);

            updateTarget = (Mesh)EditorGUILayout.ObjectField(
                VFXMeshLabContent.UpdateMesh,
                updateTarget,
                typeof(Mesh),
                false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(VFXMeshLabContent.GenerateNew, GUILayout.Height(28f)))
            {
                GenerateNewMesh();
            }

            using (new EditorGUI.DisabledScope(updateTarget == null))
            {
                if (GUILayout.Button(VFXMeshLabContent.UpdateExisting, GUILayout.Height(28f)))
                {
                    UpdateExistingMesh();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(Rect rect)
        {
            preview?.Draw(
                rect,
                previewMode,
                previewBackground,
                previewCheckerTexture);

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

            var folder = IsWritableAssetFolder(outputFolder) ? outputFolder : "Assets";
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
                EditorUtility.DisplayDialog("VFX Mesh Lab", result.error, "OK");
                return;
            }

            updateTarget = result.mesh;
            SetOutputFolder(
                Path.GetDirectoryName(result.assetPath)?.Replace('\\', '/') ?? "Assets",
                false);
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
                EditorUtility.DisplayDialog("VFX Mesh Lab", result.error, "OK");
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
                IsWritableAssetFolder(outputFolder) ? outputFolder : "Assets");
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
            SetOutputFolder(
                Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets",
                false);
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private void SelectOutputFolder()
        {
            var initialFolder = Application.dataPath;
            if (IsWritableAssetFolder(outputFolder))
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                initialFolder = Path.GetFullPath(Path.Combine(projectRoot, outputFolder));
            }

            var absolute = EditorUtility.OpenFolderPanel(
                "Select Output Folder",
                initialFolder,
                string.Empty);
            if (string.IsNullOrEmpty(absolute))
            {
                return;
            }

            SetOutputFolder(FileUtil.GetProjectRelativePath(absolute), true);
        }

        private bool SetOutputFolder(string path, bool notifyOnFailure)
        {
            var normalized = NormalizeAssetPath(path);
            if (!IsWritableAssetFolder(normalized))
            {
                if (notifyOnFailure)
                {
                    ShowNotification(
                        new GUIContent(
                            "Choose a project folder inside Assets. Package folders and files cannot be used."));
                }

                return false;
            }

            outputFolder = normalized;
            SavePersistentState();
            return true;
        }

        private static bool IsWritableAssetFolder(string path)
        {
            var normalized = NormalizeAssetPath(path);
            return !string.IsNullOrEmpty(normalized) &&
                   (string.Equals(normalized, "Assets", StringComparison.Ordinal) ||
                    normalized.StartsWith("Assets/", StringComparison.Ordinal)) &&
                   AssetDatabase.IsValidFolder(normalized);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static string BuildPersistentStateKey()
        {
            var projectRoot =
                Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    .Replace('\\', '/')
                    .TrimEnd('/');
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                projectRoot = projectRoot.ToLowerInvariant();
            }

            return PersistentStateKeyPrefix +
                   Hash128.Compute(projectRoot) +
                   ".v" +
                   PersistentStateVersion;
        }

        private void LoadPersistentState()
        {
            if (string.IsNullOrEmpty(persistentStateKey) ||
                !EditorPrefs.HasKey(persistentStateKey))
            {
                outputFolder = IsWritableAssetFolder(outputFolder) ? outputFolder : "Assets";
                return;
            }

            try
            {
                var json = EditorPrefs.GetString(persistentStateKey, string.Empty);
                var state = new PersistentState();
                JsonUtility.FromJsonOverwrite(json, state);
                if (state.version != PersistentStateVersion)
                {
                    EditorPrefs.DeleteKey(persistentStateKey);
                    return;
                }

                recipe = state.recipe ?? new VFXMeshRecipe();
                shapeSettingsByType = state.shapeSettingsByType ??
                    new List<ShapeSettingsEntry>();
                outputFolder = IsWritableAssetFolder(state.outputFolder)
                    ? NormalizeAssetPath(state.outputFolder)
                    : "Assets";
                previewMode = Enum.IsDefined(typeof(VFXPreviewMode), state.previewMode)
                    ? (VFXPreviewMode)state.previewMode
                    : VFXPreviewMode.Shaded;
                previewBackground = state.previewBackground;
                previewCheckerTexture = LoadAssetFromGuid<Texture2D>(
                    state.previewCheckerTextureGuid);
                modifierToAdd = Enum.IsDefined(typeof(VFXModifierType), state.modifierToAdd)
                    ? (VFXModifierType)state.modifierToAdd
                    : VFXModifierType.Noise;
                shapeExpanded = state.shapeExpanded;
                modifiersExpanded = state.modifiersExpanded;
                uvExpanded = state.uvExpanded;
                vertexDataExpanded = state.vertexDataExpanded;
                outputExpanded = state.outputExpanded;
                presetsExpanded = state.presetsExpanded;
                selectedPreset = LoadAssetFromGuid<VFXMeshRecipePreset>(
                    state.selectedPresetGuid);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "VFX Mesh Lab could not restore its editor state: " +
                    exception.Message);
                EditorPrefs.DeleteKey(persistentStateKey);
                shapeSettingsByType = new List<ShapeSettingsEntry>();
                outputFolder = "Assets";
            }
        }

        private void QueuePersistentStateSave()
        {
            if (string.IsNullOrEmpty(persistentStateKey))
            {
                return;
            }

            persistentStateSavePending = true;
            persistentStateSaveTime =
                EditorApplication.timeSinceStartup + PersistentStateSaveDelay;
            EditorApplication.update -= SavePersistentStateWhenDue;
            EditorApplication.update += SavePersistentStateWhenDue;
        }

        private void SavePersistentStateWhenDue()
        {
            if (!persistentStateSavePending ||
                EditorApplication.timeSinceStartup < persistentStateSaveTime)
            {
                return;
            }

            SavePersistentState();
        }

        private void SavePersistentState()
        {
            persistentStateSavePending = false;
            EditorApplication.update -= SavePersistentStateWhenDue;
            EnsureRecipeState();
            NormalizeShapeSettingsMemory();
            RememberShapeSettings(recipe.shapeType, recipe.shape);
            if (string.IsNullOrEmpty(persistentStateKey))
            {
                return;
            }

            try
            {
                var state = new PersistentState
                {
                    recipe = recipe ?? new VFXMeshRecipe(),
                    shapeSettingsByType = shapeSettingsByType,
                    outputFolder = IsWritableAssetFolder(outputFolder)
                        ? NormalizeAssetPath(outputFolder)
                        : "Assets",
                    previewMode = (int)previewMode,
                    previewBackground = previewBackground,
                    previewCheckerTextureGuid = GetAssetGuid(previewCheckerTexture),
                    modifierToAdd = (int)modifierToAdd,
                    shapeExpanded = shapeExpanded,
                    modifiersExpanded = modifiersExpanded,
                    uvExpanded = uvExpanded,
                    vertexDataExpanded = vertexDataExpanded,
                    outputExpanded = outputExpanded,
                    presetsExpanded = presetsExpanded,
                    selectedPresetGuid = GetAssetGuid(selectedPreset)
                };
                EditorPrefs.SetString(persistentStateKey, JsonUtility.ToJson(state));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "VFX Mesh Lab could not save its editor state: " +
                    exception.Message);
            }
        }

        private void EnsureRecipeState()
        {
            recipe ??= new VFXMeshRecipe();
            if (!Enum.IsDefined(typeof(VFXMeshShapeType), recipe.shapeType))
            {
                recipe.shapeType = VFXMeshShapeType.Quad;
                recipe.shape = CreateDefaultShapeSettings(recipe.shapeType);
            }

            recipe.shape ??= CreateDefaultShapeSettings(recipe.shapeType);
            recipe.modifiers ??= new List<VFXMeshModifierSettings>();
            for (var index = 0; index < recipe.modifiers.Count; index++)
            {
                recipe.modifiers[index] ??= new VFXMeshModifierSettings();
            }

            recipe.uv ??= new VFXUVSettings();
            recipe.vertexData ??= new VFXVertexDataSettings();
            recipe.vertexData.uv1 ??= new VFXChannelPackSettings();
            recipe.vertexData.uv2 ??= new VFXChannelPackSettings();
            recipe.vertexData.uv3 ??= new VFXChannelPackSettings();
            recipe.output ??= new VFXMeshOutputSettings();
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
        }

        private static T LoadAssetFromGuid<T>(string guid)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void ReleaseBuildResult()
        {
            if (buildResult?.mesh != null && !AssetDatabase.Contains(buildResult.mesh))
            {
                DestroyImmediate(buildResult.mesh);
            }

            buildResult = null;
        }

        private static int SegmentField(GUIContent label, int value, int minimum)
        {
            return Mathf.Clamp(
                EditorGUILayout.IntField(label, value),
                minimum,
                VFXMeshBuildLimits.MaximumSegmentsPerAxis);
        }

        private static float PositiveFloat(GUIContent label, float value)
        {
            return Mathf.Max(0.0001f, EditorGUILayout.FloatField(label, value));
        }

        private static void DrawTooltipOverLastControl(GUIContent content)
        {
            if (content != null && !string.IsNullOrEmpty(content.tooltip))
            {
                GUI.Label(
                    GUILayoutUtility.GetLastRect(),
                    new GUIContent(string.Empty, content.tooltip),
                    GUIStyle.none);
            }
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
