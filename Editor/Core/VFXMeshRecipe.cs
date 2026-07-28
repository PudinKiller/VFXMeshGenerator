using System;
using System.Collections.Generic;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    [Serializable]
    public sealed class VFXMeshRecipe
    {
        public string meshName = "VFXMesh";
        public VFXMeshShapeType shapeType = VFXMeshShapeType.Quad;
        public VFXShapeSettings shape = new VFXShapeSettings();
        public List<VFXMeshModifierSettings> modifiers = new List<VFXMeshModifierSettings>();
        public VFXUVSettings uv = new VFXUVSettings();
        public VFXVertexDataSettings vertexData = new VFXVertexDataSettings();
        public VFXMeshOutputSettings output = new VFXMeshOutputSettings();

        public VFXMeshRecipe DeepCopy()
        {
            var copy = new VFXMeshRecipe();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class VFXShapeSettings
    {
        public float width = 1f;
        public float length = 1f;
        public float height = 1f;
        public float radius = 0.5f;
        public float innerRadius = 0.25f;
        public float topRadius;
        public float thickness = 0.1f;
        public Vector3 size = Vector3.one;
        public float arcDegrees = 360f;
        public float angleOffset;
        public float turns = 3f;
        public float pitch = 0.25f;
        public int widthSegments = 1;
        public int lengthSegments = 8;
        public int heightSegments = 1;
        public int radialSegments = 24;
        public int longitudeSegments = 24;
        public int latitudeSegments = 12;
        public int planeCount = 2;
        public bool capStart = true;
        public bool capEnd = true;
        public VFXAxis axis = VFXAxis.Y;
        public VFXPivot pivot = VFXPivot.Center;
        public Vector3 customPivotOffset;
        public AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve radialElevationCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public AnimationCurve arcWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    }

    [Serializable]
    public sealed class VFXMeshModifierSettings
    {
        public bool enabled = true;
        public VFXModifierType type = VFXModifierType.Noise;
        public VFXAxis axis = VFXAxis.Y;
        public VFXModifierSpace space = VFXModifierSpace.Axis;
        public float strength = 0.1f;
        public float frequency = 1f;
        public float angle = 45f;
        public Vector3 offset;
        public Vector3 scale = Vector3.one;
        public int seed;
        public int octaves = 2;
        public float lacunarity = 2f;
        public float persistence = 0.5f;
        public AnimationCurve falloff = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    [Serializable]
    public sealed class VFXUVSettings
    {
        public VFXUVProjection projection = VFXUVProjection.ShapeDefault;
        public Vector2 scale = Vector2.one;
        public Vector2 offset;
        public float rotation;
        public bool flipU;
        public bool flipV;
        public bool swapUV;
    }

    [Serializable]
    public sealed class VFXChannelPackSettings
    {
        public VFXDataSource x = VFXDataSource.NormalizedX;
        public VFXDataSource y = VFXDataSource.NormalizedY;
        public VFXDataSource z = VFXDataSource.NormalizedZ;
        public VFXDataSource w = VFXDataSource.One;
    }

    [Serializable]
    public sealed class VFXVertexDataSettings
    {
        public bool generateColors;
        public VFXVertexColorMode colorMode = VFXVertexColorMode.Solid;
        public Color solidColor = Color.white;
        public Gradient colorGradient = new Gradient();
        public VFXAxis gradientAxis = VFXAxis.Y;
        public bool generateUV1;
        public bool generateUV2;
        public bool generateUV3;
        public VFXChannelPackSettings uv1 = new VFXChannelPackSettings();
        public VFXChannelPackSettings uv2 = new VFXChannelPackSettings();
        public VFXChannelPackSettings uv3 = new VFXChannelPackSettings();
    }

    [Serializable]
    public sealed class VFXMeshOutputSettings
    {
        public bool flipWinding;
        public bool doubleSided;
        public bool flatShading;
        public bool generateTangents = true;
        public bool readWriteEnabled = true;
        public bool optimizeMesh;
        public float boundsPadding;
        public VFXMeshCompression compression = VFXMeshCompression.Off;
        public VFXIndexFormatMode indexFormat = VFXIndexFormatMode.Auto;
    }
}
