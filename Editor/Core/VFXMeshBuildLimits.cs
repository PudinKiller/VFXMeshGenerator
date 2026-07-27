using System;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    /// <summary>
    /// Central safety limits for synchronous editor generation.
    /// </summary>
    public static class VFXMeshBuildLimits
    {
        public const int MaximumSegmentsPerAxis = 4096;
        public const int MaximumPlaneCount = 64;
        public const int MaximumNoiseOctaves = 12;
        public const float MaximumHelixTurns = 256f;
        public const long MaximumBaseVertexEstimate = 500000;
        public const long MaximumFinalVertexEstimate = 1000000;

        public static bool TryValidate(VFXMeshRecipe recipe, out long estimatedFinalVertices, out string error)
        {
            estimatedFinalVertices = 0;
            error = null;
            if (recipe?.shape == null)
            {
                error = "The mesh recipe has no shape settings.";
                return false;
            }

            var shape = recipe.shape;
            if (!ValidateSegment(shape.widthSegments, "Width segments", out error) ||
                !ValidateSegment(shape.lengthSegments, "Length segments", out error) ||
                !ValidateSegment(shape.heightSegments, "Height segments", out error) ||
                !ValidateSegment(shape.radialSegments, "Radial segments", out error) ||
                !ValidateSegment(shape.longitudeSegments, "Longitude segments", out error) ||
                !ValidateSegment(shape.latitudeSegments, "Latitude segments", out error))
            {
                return false;
            }

            if (shape.planeCount > MaximumPlaneCount)
            {
                error = $"Plane count cannot exceed {MaximumPlaneCount:N0}.";
                return false;
            }

            if (float.IsNaN(shape.turns) || float.IsInfinity(shape.turns) ||
                Math.Abs(shape.turns) > MaximumHelixTurns)
            {
                error = $"Helix turns must be finite and no greater than {MaximumHelixTurns:N0}.";
                return false;
            }

            var width = SafeSegments(shape.widthSegments, 1);
            var length = SafeSegments(shape.lengthSegments, 1);
            var height = SafeSegments(shape.heightSegments, 1);
            var radial = SafeSegments(shape.radialSegments, 3);
            var longitude = SafeSegments(shape.longitudeSegments, 3);
            var latitude = SafeSegments(shape.latitudeSegments, 2);
            var planes = Math.Max(2, shape.planeCount);
            long baseVertices;

            checked
            {
                switch (recipe.shapeType)
                {
                    case VFXMeshShapeType.Quad:
                    case VFXMeshShapeType.Ribbon:
                        baseVertices = (long)(width + 1) * (length + 1);
                        break;

                    case VFXMeshShapeType.Disc:
                        baseVertices = 1L + (long)width * (radial + 1);
                        break;

                    case VFXMeshShapeType.Ring:
                    case VFXMeshShapeType.Arc:
                        baseVertices = (long)(width + 1) * (radial + 1);
                        break;

                    case VFXMeshShapeType.Cone:
                    case VFXMeshShapeType.Cylinder:
                        baseVertices =
                            (long)(height + 1) * (radial + 1) +
                            (shape.capStart ? radial + 2L : 0L) +
                            (shape.capEnd ? radial + 2L : 0L);
                        break;

                    case VFXMeshShapeType.Tube:
                        baseVertices =
                            2L * (height + 1) * (radial + 1) +
                            (shape.capStart ? 2L * (radial + 1) : 0L) +
                            (shape.capEnd ? 2L * (radial + 1) : 0L);
                        break;

                    case VFXMeshShapeType.Sphere:
                        baseVertices = (long)Math.Max(1, latitude - 1) * (longitude + 1) + 2L;
                        break;

                    case VFXMeshShapeType.Hemisphere:
                        baseVertices =
                            (long)Math.Max(1, latitude) * (longitude + 1) +
                            1L +
                            (shape.capEnd ? longitude + 2L : 0L);
                        break;

                    case VFXMeshShapeType.Torus:
                        baseVertices = (long)(longitude + 1) * (radial + 1);
                        break;

                    case VFXMeshShapeType.Box:
                        baseVertices = 2L *
                            ((long)(width + 1) * (height + 1) +
                             (long)(height + 1) * (length + 1) +
                             (long)(width + 1) * (length + 1));
                        break;

                    case VFXMeshShapeType.CrossPlanes:
                        baseVertices = (long)planes * (width + 1) * (length + 1);
                        break;

                    case VFXMeshShapeType.Helix:
                        var turnCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(shape.turns)));
                        baseVertices = ((long)length * turnCount + 1L) * (width + 1);
                        break;

                    default:
                        error = $"Unsupported mesh shape type: {recipe.shapeType}.";
                        return false;
                }

                if (baseVertices > MaximumBaseVertexEstimate)
                {
                    error =
                        $"These settings would generate approximately {baseVertices:N0} base vertices. " +
                        $"The editor safety limit is {MaximumBaseVertexEstimate:N0}. Reduce the resolution.";
                    return false;
                }

                estimatedFinalVertices = baseVertices;
                if (recipe.output?.flatShading == true)
                {
                    estimatedFinalVertices *= 6L;
                }

                if (recipe.output?.doubleSided == true)
                {
                    estimatedFinalVertices *= 2L;
                }
            }

            if (estimatedFinalVertices > MaximumFinalVertexEstimate)
            {
                error =
                    $"The selected topology options could expand this mesh to approximately " +
                    $"{estimatedFinalVertices:N0} vertices. The editor safety limit is " +
                    $"{MaximumFinalVertexEstimate:N0}. Reduce the resolution or disable topology expansion.";
                return false;
            }

            return true;
        }

        private static bool ValidateSegment(int value, string label, out string error)
        {
            if (value > MaximumSegmentsPerAxis)
            {
                error = $"{label} cannot exceed {MaximumSegmentsPerAxis:N0}.";
                return false;
            }

            error = null;
            return true;
        }

        private static int SafeSegments(int value, int minimum)
        {
            return Math.Max(minimum, value);
        }
    }
}
