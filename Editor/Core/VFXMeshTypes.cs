using System;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    public enum VFXMeshShapeType
    {
        Quad,
        Disc,
        Ring,
        Arc,
        Cone,
        Cylinder,
        Tube,
        Sphere,
        Hemisphere,
        Torus,
        Box,
        Ribbon,
        CrossPlanes,
        Helix
    }

    public enum VFXAxis
    {
        X,
        Y,
        Z
    }

    public enum VFXPivot
    {
        Center,
        Start,
        End,
        Custom
    }

    public enum VFXModifierType
    {
        Transform,
        Taper,
        Twist,
        Bend,
        Skew,
        Wave,
        RadialRipple,
        Noise,
        Inflate,
        Spherize,
        Flatten
    }

    public enum VFXModifierSpace
    {
        Axis,
        Radial
    }

    public enum VFXUVProjection
    {
        ShapeDefault,
        Planar,
        Radial,
        Cylindrical,
        Spherical,
        AlongLength,
        Box
    }

    public enum VFXVertexColorMode
    {
        Solid,
        AxisGradient,
        RadialGradient
    }

    public enum VFXDataSource
    {
        Zero,
        One,
        NormalizedX,
        NormalizedY,
        NormalizedZ,
        AlongAxis,
        RadialDistance,
        Random
    }

    public enum VFXMeshCompression
    {
        Off,
        Low,
        Medium,
        High
    }

    public enum VFXIndexFormatMode
    {
        Auto,
        UInt16,
        UInt32
    }

    public enum VFXPreviewMode
    {
        Shaded,
        Unlit,
        Wireframe,
        UVChecker,
        Normals,
        VertexColors
    }

    [Flags]
    public enum VFXMeshValidationFlags
    {
        None = 0,
        Empty = 1 << 0,
        InvalidIndex = 1 << 1,
        NonFiniteVertex = 1 << 2,
        DegenerateTriangle = 1 << 3
    }
}
