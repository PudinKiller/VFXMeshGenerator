using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    internal static class VFXMeshGeneratorContent
    {
        public static readonly GUIContent PreviewMode = Create(
            string.Empty,
            "Changes only the preview visualization. Generated mesh data is unchanged.");
        public static readonly GUIContent CheckerTexture = Create(
            "Checker",
            "Optional project Texture2D sampled from UV0 in UV Checker view. Leave empty to use the built-in procedural checker. Texture import filtering and wrap settings are respected; this affects only the preview.");
        public static readonly GUIContent FrontView = Create("Front", "Set the preview camera to the front view.");
        public static readonly GUIContent SideView = Create("Side", "Set the preview camera to the side view.");
        public static readonly GUIContent TopView = Create("Top", "Set the preview camera to the top view.");
        public static readonly GUIContent FrameView = Create("Frame", "Center and fit the generated mesh in the preview.");
        public static readonly GUIContent MeshName = Create(
            "Mesh Name",
            "Name stored on the generated Mesh and used as the default asset and preset name.");
        public static readonly GUIContent Shape = Create(
            "Shape",
            "Select the base topology generated before modifiers. Ring is always a closed 360-degree loop; Arc exposes sweep and width-profile controls.");

        public static readonly GUIContent WidthSegments = Segments(
            "Width Segments",
            "Number of subdivisions across the width.");
        public static readonly GUIContent LengthSegments = Segments(
            "Length Segments",
            "Number of subdivisions along the length.");
        public static readonly GUIContent HeightSegments = Segments(
            "Height Segments",
            "Number of subdivisions along Main Axis.");
        public static readonly GUIContent EdgeCount = Segments(
            "Edge Count",
            "Number of segments around the angular sweep.");
        public static readonly GUIContent RadialResolution = Segments(
            "Radial Resolution",
            "Number of subdivisions from the inner radius or center to the outer edge.");
        public static readonly GUIContent RadialSegments = Segments(
            "Radial Segments",
            "Number of segments around the circumference.");
        public static readonly GUIContent Longitude = Segments(
            "Longitude",
            "Number of segments around the sphere.");
        public static readonly GUIContent Latitude = Segments(
            "Latitude",
            "Number of subdivisions from pole to pole.");
        public static readonly GUIContent RingSegments = Segments(
            "Ring Segments",
            "Number of subdivisions around the torus ring.");
        public static readonly GUIContent TubeSegments = Segments(
            "Tube Segments",
            "Number of subdivisions around the torus tube.");
        public static readonly GUIContent XSegments = Segments("X Segments", "Number of subdivisions along local X.");
        public static readonly GUIContent YSegments = Segments("Y Segments", "Number of subdivisions along local Y.");
        public static readonly GUIContent ZSegments = Segments("Z Segments", "Number of subdivisions along local Z.");
        public static readonly GUIContent HelixLengthSegments = Segments(
            "Length Segments",
            "Base subdivisions per revolution. Final density increases with Turns.");

        public static readonly GUIContent InnerRadius = Create(
            "Inner Radius",
            "Distance from the center to the inner edge, in local mesh units.");
        public static readonly GUIContent OuterRadius = Create(
            "Outer Radius",
            "Distance from the center to the outer edge, in local mesh units.");
        public static readonly GUIContent MajorRadius = Create(
            "Major Radius",
            "Distance from the torus center to the centerline of its tube.");
        public static readonly GUIContent MinorRadius = Create(
            "Minor Radius",
            "Radius of the torus tube cross-section.");
        public static readonly GUIContent ArcDegrees = Create(
            "Arc Degrees",
            "Angular sweep in degrees. Arc is partial by design; use Ring for a closed 360-degree loop.");
        public static readonly GUIContent VolumeArcDegrees = Create(
            "Arc Degrees",
            "Angular sweep in degrees. Values below 360 create an open radial volume.");
        public static readonly GUIContent AngleOffset = Create(
            "Angle Offset",
            "Rotates the start edge or UV seam around Main Axis, in degrees.");
        public static readonly GUIContent AxialElevationCurve = Create(
            "Axial Elevation Curve",
            "Curve time runs from inner edge or center (0) to outer edge (1). Values displace along Main Axis.");
        public static readonly GUIContent ArcWidthCurve = Create(
            "Angular Width Curve",
            "Curve time runs from arc start (0) to end (1). Values from 0 to 1 scale radial thickness around the selected Radial Width Origin.");
        public static readonly GUIContent ArcWidthOrigin = Create(
            "Radial Width Origin",
            "Radial pivot used when Angular Width Curve scales annulus thickness. Outer Rim keeps the outer radius fixed, Middle moves both radii evenly, and Inner Rim keeps the inner radius fixed. Axial elevation remains referenced to the outer rim so mirrored shells stay joined.");
        public static readonly GUIContent MirrorArcAcrossShapePlane = Create(
            "Mirror Across Shape Plane",
            "Add a disconnected second Arc shell reflected across the plane perpendicular to Main Axis. The generated shells are aligned at the outer rim, keep identical UVs so texture scrolling moves in the same direction, and are then modified independently. This is different from Double Sided.");
        public static readonly GUIContent WidthCurve = Create(
            "Width Curve",
            "Curve time runs from shape start (0) to end (1). Values multiply local width.");
        public static readonly GUIContent RibbonUVWidthMode = Create(
            "Shape Default Width UV",
            "Preserve Texel Density makes U follow local mesh width, eliminating diagonal checker kinks on tapered ribbons. Stretch To Width keeps every row at U 0-1, but low-resolution tapers can show triangle interpolation.");
        public static readonly GUIContent PlaneCount = Create(
            "Plane Count",
            "Number of intersecting cards distributed evenly around Main Axis.");
        public static readonly GUIContent Turns = Create(
            "Turns",
            "Number of complete revolutions made by the helix.");
        public static readonly GUIContent Pitch = Create(
            "Pitch",
            "Signed distance traveled along Main Axis per helix revolution.");
        public static readonly GUIContent MainAxis = Create(
            "Main Axis",
            "Orients the shape's primary height or length axis and drives shape-default axial data.");
        public static readonly GUIContent Pivot = Create(
            "Pivot",
            "Moves vertices so Center, Start, End, or Custom becomes the mesh's local origin.");
        public static readonly GUIContent CustomPivotPosition = Create(
            "Custom Pivot Position",
            "Local point that becomes the origin. This value is subtracted from every vertex.");
        public static readonly GUIContent HemisphereCap = Create(
            "Cap",
            "Close the flat equator with a filled disc.");
        public static readonly GUIContent CapStart = Create(
            "Cap Start",
            "Close the minimum end along Main Axis. Partial angular cuts remain open.");
        public static readonly GUIContent CapEnd = Create(
            "Cap End",
            "Close the maximum end along Main Axis. Partial angular cuts remain open.");

        public static readonly GUIContent ModifierEnabled = Create(string.Empty, "Enable or bypass this modifier.");
        public static readonly GUIContent ModifierType = Create(
            string.Empty,
            "Select the modifier type. Modifiers are evaluated from top to bottom.");
        public static readonly GUIContent MoveEarlier = Create("▲", "Move this modifier earlier in the stack.");
        public static readonly GUIContent MoveLater = Create("▼", "Move this modifier later in the stack.");
        public static readonly GUIContent RemoveModifier = Create("×", "Remove this modifier.");
        public static readonly GUIContent ModifierToAdd = Create(
            string.Empty,
            "Choose the modifier that Add Modifier will append to the stack.");
        public static readonly GUIContent AddModifier = Create("Add Modifier", "Append the selected modifier to the stack.");
        public static readonly GUIContent ModifierAxis = Create(
            "Axis",
            "Axis used by this deformation and Axis-space falloff. Independent of the shape's Main Axis.");
        public static readonly GUIContent TransformOffset = Create(
            "Offset",
            "Translate every vertex in local mesh units.");
        public static readonly GUIContent TransformScale = Create(
            "Scale",
            "Scale the complete mesh around its bounds center.");
        public static readonly GUIContent TransformRotation = Create(
            "Rotation",
            "Rotate the complete mesh around the selected Axis, in degrees.");
        public static readonly GUIContent TwistAngle = Create(
            "Angle",
            "Rotation in degrees where falloff equals 1.");
        public static readonly GUIContent BendAngle = Create(
            "Angle",
            "Bend curvature in degrees, locally scaled by falloff.");
        public static readonly GUIContent Falloff = Create(
            "Falloff",
            "Curve X is normalized position along Axis, or center-to-edge distance in Radial space. Curve Y multiplies the effect.");
        public static readonly GUIContent NoiseFrequency = Create(
            "Frequency",
            "Spatial sampling frequency. Higher values produce smaller noise features.");
        public static readonly GUIContent WaveFrequency = Create(
            "Frequency",
            "Number of wave cycles across the normalized axis or radius.");
        public static readonly GUIContent NoiseOffset = Create(
            "Noise Offset",
            "Moves noise sampling coordinates without translating the mesh.");
        public static readonly GUIContent Seed = Create("Seed", "Selects a deterministic noise variation.");
        public static readonly GUIContent Octaves = Create("Octaves", "Number of layered noise frequencies.");
        public static readonly GUIContent Lacunarity = Create(
            "Lacunarity",
            "Frequency multiplier between noise octaves.");
        public static readonly GUIContent Persistence = Create(
            "Persistence",
            "Amplitude multiplier between noise octaves.");
        public static readonly GUIContent NoiseDirection = Create(
            "Direction",
            "Axis displaces along the selected Axis; Radial displaces away from the mesh center. This also selects the falloff coordinate.");
        public static readonly GUIContent Phase = Create(
            "Phase (Degrees)",
            "Offsets the wave cycle without moving the mesh.");
        public static readonly GUIContent ModifierSpace = Create(
            "Space",
            "Axis evaluates falloff along Axis; Radial evaluates it from center to outer radius. Some modifiers also use this for deformation direction.");
        private static readonly GUIContent SkewSpace = Create(
            "Space",
            "Axis shifts perpendicular to Axis with axial falloff. Radial shifts along Axis with center-to-edge falloff.");
        private static readonly GUIContent FlattenSpace = Create(
            "Space",
            "Axis collapses toward a plane perpendicular to Axis. Radial collapses toward the selected Axis line.");

        public static readonly GUIContent UVProjection = Create(
            "Projection",
            "Select how UV0 is generated after deformation. Shape Default preserves generator-authored UVs.");
        public static readonly GUIContent UVScale = Create(
            "Scale",
            "Multiply final UV coordinates. Values above 1 increase tiling.");
        public static readonly GUIContent UVOffset = Create("Offset", "Add an offset to final UV coordinates.");
        public static readonly GUIContent UVRotation = Create(
            "Rotation",
            "Rotate UVs in degrees around the center at 0.5, 0.5.");
        public static readonly GUIContent FlipU = Create("Flip U", "Mirror U inside the 0-1 range.");
        public static readonly GUIContent FlipV = Create("Flip V", "Mirror V inside the 0-1 range.");
        public static readonly GUIContent SwapUV = Create(
            "Swap U/V",
            "Exchange U and V before the other UV transforms.");

        public static readonly GUIContent VertexColors = Create(
            "Vertex Colors",
            "Write a COLOR value for every vertex.");
        public static readonly GUIContent ColorMode = Create(
            "Color Mode",
            "Solid writes one color; gradients evaluate normalized final mesh position.");
        public static readonly GUIContent SolidColor = Create("Solid Color", "Color written to every vertex in Solid mode.");
        public static readonly GUIContent ColorGradient = Create(
            "Gradient",
            "Color sampled from normalized axial or radial position.");
        public static readonly GUIContent GradientAxis = Create(
            "Gradient Axis",
            "Axis Gradient runs from bounds minimum to maximum. Radial Gradient measures distance perpendicular to this axis.");
        public static readonly GUIContent UV1 = PackedUV("UV1 / TEXCOORD1");
        public static readonly GUIContent UV2 = PackedUV("UV2 / TEXCOORD2");
        public static readonly GUIContent UV3 = PackedUV("UV3 / TEXCOORD3");
        public static readonly GUIContent ChannelX = Channel("X");
        public static readonly GUIContent ChannelY = Channel("Y");
        public static readonly GUIContent ChannelZ = Channel("Z");
        public static readonly GUIContent ChannelW = Channel("W");

        public static readonly GUIContent FlipWinding = Create(
            "Flip Winding",
            "Reverse every triangle's front face, producing an inside-out mesh.");
        public static readonly GUIContent DoubleSided = Create(
            "Double Sided",
            "Duplicate vertices and reversed triangles for independently lit back faces. Approximately doubles mesh size.");
        public static readonly GUIContent FlatShading = Create(
            "Flat Shading",
            "Split vertices per triangle to create hard face normals. Can greatly increase vertex count.");
        public static readonly GUIContent GenerateTangents = Create(
            "Generate Tangents",
            "Calculate tangent data from UV0 for tangent-space effects such as normal mapping.");
        public static readonly GUIContent ReadWrite = Create(
            "Read/Write Enabled",
            "Keep a CPU-side copy after saving. Disable to reduce memory; scripts can no longer read or modify the mesh.");
        public static readonly GUIContent OptimizeMesh = Create(
            "Optimize Mesh",
            "Reorder saved vertex and index buffers for rendering efficiency.");
        public static readonly GUIContent Compression = Create(
            "Compression",
            "Apply Unity mesh compression when saving. Higher levels reduce precision; preview remains uncompressed.");
        public static readonly GUIContent IndexFormat = Create(
            "Index Format",
            "Auto uses 16-bit indices when possible and 32-bit above 65,535 vertices. Forced UInt16 fails above the limit.");
        public static readonly GUIContent BoundsPadding = Create(
            "Bounds Padding",
            "Extra local-space distance added on every side of calculated bounds.");

        public static readonly GUIContent Preset = Create("Preset", "A reusable copy of the complete generator recipe.");
        public static readonly GUIContent LoadPreset = Create("Load", "Replace all current settings with the selected preset.");
        public static readonly GUIContent UpdatePreset = Create(
            "Update",
            "Overwrite the selected preset with current settings.");
        public static readonly GUIContent SavePreset = Create("Save New", "Create a new recipe preset asset.");
        public static readonly GUIContent DefaultFolder = Create(
            "Default Folder",
            "Project folder used initially by mesh and preset save dialogs. Drag a folder from the Project window here. Assets are not saved automatically.");
        public static readonly GUIContent BrowseFolder = Create("…", "Choose the default output folder.");
        public static readonly GUIContent UpdateMesh = Create(
            "Update Mesh",
            "Standalone .asset Mesh to overwrite. Imported model sub-assets are not supported.");
        public static readonly GUIContent GenerateNew = Create(
            "Generate New",
            "Create a new Mesh asset from the current preview.");
        public static readonly GUIContent UpdateExisting = Create(
            "Update Existing",
            "Overwrite mesh data while preserving the selected asset's GUID and references.");
        private static readonly GUIContent DisplacementStrength = Create(
            "Strength",
            "Maximum displacement in local mesh units before falloff.");
        private static readonly GUIContent TaperStrength = Create(
            "Strength",
            "Radial scale change at full weight. 1 doubles width; -1 collapses it.");
        private static readonly GUIContent SkewStrength = Create(
            "Strength",
            "Maximum offset as a fraction of mesh length along Axis.");
        private static readonly GUIContent BlendStrength = Create(
            "Strength",
            "Blend amount: 0 is unchanged and 1 reaches the target shape.");

        public static GUIContent Strength(VFXModifierType type)
        {
            switch (type)
            {
                case VFXModifierType.Taper:
                    return TaperStrength;
                case VFXModifierType.Skew:
                    return SkewStrength;
                case VFXModifierType.Spherize:
                case VFXModifierType.Flatten:
                    return BlendStrength;
                default:
                    return DisplacementStrength;
            }
        }

        public static GUIContent Space(VFXModifierType type)
        {
            switch (type)
            {
                case VFXModifierType.Skew:
                    return SkewSpace;
                case VFXModifierType.Flatten:
                    return FlattenSpace;
                default:
                    return ModifierSpace;
            }
        }

        private static GUIContent Segments(string text, string tooltip)
        {
            return Create(text, tooltip + " Higher values increase vertex and triangle counts.");
        }

        private static GUIContent PackedUV(string text)
        {
            return Create(text, "Write a Vector4 shader stream to this TEXCOORD channel.");
        }

        private static GUIContent Channel(string text)
        {
            return Create(
                text,
                "Select the scalar source written into this component. Along Axis uses the shape's Main Axis; Radial Distance measures perpendicular distance from that axis. Position and distance sources are normalized to 0-1 using final bounds; Random is deterministic per vertex.");
        }

        private static GUIContent Create(string text, string tooltip)
        {
            return new GUIContent(text, tooltip);
        }
    }
}
