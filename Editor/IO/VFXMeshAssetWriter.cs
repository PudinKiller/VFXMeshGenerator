using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor
{
    public sealed class VFXMeshAssetWriteResult
    {
        public Mesh mesh;
        public string assetPath;
        public string error;

        public bool succeeded
        {
            get { return mesh != null && string.IsNullOrEmpty(error); }
        }
    }

    /// <summary>
    /// Writes generated preview meshes to standalone native Mesh assets.
    /// </summary>
    public static class VFXMeshAssetWriter
    {
        public static VFXMeshAssetWriteResult GenerateNew(
            VFXMeshBuildResult buildResult,
            string requestedAssetPath)
        {
            var result = new VFXMeshAssetWriteResult();
            if (!TryValidateBuild(buildResult, out result.error))
            {
                return result;
            }

            if (!TryNormalizeNewAssetPath(requestedAssetPath, out var normalizedPath, out result.error))
            {
                return result;
            }

            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(normalizedPath);
            Mesh assetMesh = null;
            var assetCreated = false;

            try
            {
                assetMesh = UnityEngine.Object.Instantiate(buildResult.mesh);
                assetMesh.name = buildResult.mesh.name;
                assetMesh.hideFlags = HideFlags.None;

                AssetDatabase.CreateAsset(assetMesh, uniquePath);
                assetCreated = true;

                ApplyAssetOutputSettings(assetMesh, buildResult.outputSettings);
                EditorUtility.SetDirty(assetMesh);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.assetPath = uniquePath;
                result.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(uniquePath);
                if (result.mesh == null)
                {
                    result.error = "The mesh asset was written, but Unity could not load it from " + uniquePath + ".";
                }

                return result;
            }
            catch (Exception exception)
            {
                if (assetCreated)
                {
                    AssetDatabase.DeleteAsset(uniquePath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else if (assetMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(assetMesh);
                }

                result.error = "Could not create the mesh asset: " + exception.Message;
                return result;
            }
        }

        public static VFXMeshAssetWriteResult UpdateExisting(
            VFXMeshBuildResult buildResult,
            Mesh existingAsset)
        {
            var result = new VFXMeshAssetWriteResult();
            if (!TryValidateBuild(buildResult, out result.error))
            {
                return result;
            }

            if (!TryValidateExistingAsset(existingAsset, out var assetPath, out result.error))
            {
                return result;
            }

            Mesh backup = null;
            try
            {
                backup = UnityEngine.Object.Instantiate(existingAsset);
                backup.hideFlags = HideFlags.HideAndDontSave;

                Undo.RegisterCompleteObjectUndo(existingAsset, "Update VFX Mesh");
                EditorUtility.CopySerialized(buildResult.mesh, existingAsset);
                existingAsset.name = buildResult.mesh.name;
                existingAsset.hideFlags = HideFlags.None;

                ApplyAssetOutputSettings(existingAsset, buildResult.outputSettings);
                EditorUtility.SetDirty(existingAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                result.assetPath = assetPath;
                result.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (result.mesh == null)
                {
                    result.error = "The mesh asset was updated, but Unity could not reload it from " + assetPath + ".";
                }

                return result;
            }
            catch (Exception exception)
            {
                if (backup != null)
                {
                    try
                    {
                        EditorUtility.CopySerialized(backup, existingAsset);
                        EditorUtility.SetDirty(existingAsset);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                    catch
                    {
                        // Preserve the original write error. A failed rollback remains visible in the asset.
                    }
                }

                result.error = "Could not update the mesh asset: " + exception.Message;
                return result;
            }
            finally
            {
                if (backup != null)
                {
                    UnityEngine.Object.DestroyImmediate(backup);
                }
            }
        }

        private static bool TryValidateBuild(VFXMeshBuildResult buildResult, out string error)
        {
            if (buildResult == null)
            {
                error = "There is no mesh build result to save.";
                return false;
            }

            if (!buildResult.succeeded || buildResult.mesh == null)
            {
                error = string.IsNullOrEmpty(buildResult.error)
                    ? "The mesh preview is not valid and cannot be saved."
                    : buildResult.error;
                return false;
            }

            if (!buildResult.mesh.isReadable)
            {
                error = "The preview mesh is not readable. Rebuild the preview before saving it.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeNewAssetPath(
            string requestedAssetPath,
            out string normalizedPath,
            out string error)
        {
            normalizedPath = null;
            if (string.IsNullOrWhiteSpace(requestedAssetPath))
            {
                error = "Choose a destination for the mesh asset.";
                return false;
            }

            try
            {
                var path = requestedAssetPath.Trim().Replace('\\', '/');
                if (Path.IsPathRooted(path))
                {
                    var projectAssetsPath = Path.GetFullPath(Application.dataPath)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var fullPath = Path.GetFullPath(path);
                    var projectAssetsPrefix = projectAssetsPath + Path.DirectorySeparatorChar;

                    if (!fullPath.StartsWith(projectAssetsPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Mesh assets must be saved inside this project's Assets folder.";
                        return false;
                    }

                    path = "Assets/" + fullPath.Substring(projectAssetsPrefix.Length).Replace('\\', '/');
                }

                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    error = "Mesh assets must be saved inside this project's Assets folder.";
                    return false;
                }

                path = "Assets/" + path.Substring("Assets/".Length);
                path = Path.ChangeExtension(path, ".asset").Replace('\\', '/');
                var folder = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder.Replace('\\', '/')))
                {
                    error = "The selected destination folder does not exist in the project.";
                    return false;
                }

                normalizedPath = path;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "The selected asset path is invalid: " + exception.Message;
                return false;
            }
        }

        private static bool TryValidateExistingAsset(
            Mesh existingAsset,
            out string assetPath,
            out string error)
        {
            assetPath = null;
            if (existingAsset == null)
            {
                error = "Select an existing generated Mesh asset to update.";
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(existingAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                error = "The selected mesh is not a saved project asset.";
                return false;
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only standalone Mesh assets in the project's Assets folder can be updated.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(assetPath), ".asset", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.IsSubAsset(existingAsset) ||
                !AssetDatabase.IsMainAsset(existingAsset))
            {
                error = "Imported models and sub-assets cannot be updated. Select a standalone .asset Mesh.";
                return false;
            }

            error = null;
            return true;
        }

        private static void ApplyAssetOutputSettings(Mesh mesh, VFXMeshOutputSettings outputSettings)
        {
            var output = outputSettings ?? new VFXMeshOutputSettings();

            MeshUtility.SetMeshCompression(mesh, ToUnityCompression(output.compression));
            if (output.optimizeMesh)
            {
                MeshUtility.Optimize(mesh);
            }

            if (!output.readWriteEnabled)
            {
                mesh.UploadMeshData(true);
            }
        }

        private static ModelImporterMeshCompression ToUnityCompression(VFXMeshCompression compression)
        {
            switch (compression)
            {
                case VFXMeshCompression.Low:
                    return ModelImporterMeshCompression.Low;
                case VFXMeshCompression.Medium:
                    return ModelImporterMeshCompression.Medium;
                case VFXMeshCompression.High:
                    return ModelImporterMeshCompression.High;
                default:
                    return ModelImporterMeshCompression.Off;
            }
        }
    }
}
