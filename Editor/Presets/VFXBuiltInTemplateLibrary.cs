using System;
using UnityEditor;

namespace PudinKiller.VFXMeshLab.Editor
{
    internal static class VFXBuiltInTemplateLibrary
    {
        internal const string TemplateRoot =
            "Packages/com.pudinkiller.vfx-mesh-lab/Editor/Presets/Templates";

        private static readonly string[] DisplayNames =
        {
            "Cross Plane",
            "Droplet",
            "Fan",
            "Shockwave",
            "Slash",
            "Spiral",
            "Splash"
        };

        private static readonly string[] FileNames =
        {
            "CrossPlane.asset",
            "Droplet.asset",
            "Fan.asset",
            "Shockwave.asset",
            "Slash.asset",
            "Spiral.asset",
            "Splash.asset"
        };

        internal static int Count => DisplayNames.Length;

        internal static string GetDisplayName(int index)
        {
            return index >= 0 && index < DisplayNames.Length
                ? DisplayNames[index]
                : string.Empty;
        }

        internal static string GetAssetPath(int index)
        {
            return index >= 0 && index < FileNames.Length
                ? TemplateRoot + "/" + FileNames[index]
                : string.Empty;
        }

        internal static VFXMeshRecipePreset Load(int index)
        {
            var path = GetAssetPath(index);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<VFXMeshRecipePreset>(path);
        }

        internal static bool IsEditableProjectPreset(
            VFXMeshRecipePreset preset)
        {
            if (preset == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(preset);
            return string.Equals(path, "Assets", StringComparison.Ordinal) ||
                   path.StartsWith("Assets/", StringComparison.Ordinal);
        }
    }
}
