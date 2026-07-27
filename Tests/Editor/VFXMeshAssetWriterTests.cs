using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PudinKiller.VFXMeshGenerator.Editor.Tests
{
    public sealed class VFXMeshAssetWriterTests
    {
        private const string TestFolder = "Assets/__VFXMeshGeneratorTests";
        private const string TestPath = TestFolder + "/Generated.asset";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "__VFXMeshGeneratorTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void UpdatingExistingMeshPreservesGuid()
        {
            var firstRecipe = new VFXMeshRecipe { shapeType = VFXMeshShapeType.Quad };
            var firstBuild = VFXMeshBuilder.Build(firstRecipe);
            var created = VFXMeshAssetWriter.GenerateNew(firstBuild, TestPath);
            Assert.That(created.succeeded, Is.True, created.error);

            var guidBefore = AssetDatabase.AssetPathToGUID(created.assetPath);
            var secondRecipe = new VFXMeshRecipe { shapeType = VFXMeshShapeType.Sphere };
            var secondBuild = VFXMeshBuilder.Build(secondRecipe);
            var updated = VFXMeshAssetWriter.UpdateExisting(secondBuild, created.mesh);

            try
            {
                Assert.That(updated.succeeded, Is.True, updated.error);
                Assert.That(AssetDatabase.AssetPathToGUID(updated.assetPath), Is.EqualTo(guidBefore));
                Assert.That(updated.mesh.vertexCount, Is.GreaterThan(4));
            }
            finally
            {
                if (secondBuild.mesh != null && !AssetDatabase.Contains(secondBuild.mesh))
                {
                    Object.DestroyImmediate(secondBuild.mesh);
                }
            }
        }

        [Test]
        public void NonReadableOutputDoesNotMutatePreviewMesh()
        {
            var recipe = new VFXMeshRecipe { shapeType = VFXMeshShapeType.Torus };
            recipe.output.readWriteEnabled = false;
            var build = VFXMeshBuilder.Build(recipe);

            try
            {
                Assert.That(build.mesh.isReadable, Is.True, "Preview meshes must remain readable.");

                var created = VFXMeshAssetWriter.GenerateNew(build, TestPath);
                Assert.That(created.succeeded, Is.True, created.error);
                Assert.That(created.mesh.isReadable, Is.False, "The saved asset should honor Read/Write Disabled.");
                Assert.That(build.mesh.isReadable, Is.True, "Saving must not mutate the preview mesh.");
            }
            finally
            {
                if (build.mesh != null && !AssetDatabase.Contains(build.mesh))
                {
                    Object.DestroyImmediate(build.mesh);
                }
            }
        }
    }
}
