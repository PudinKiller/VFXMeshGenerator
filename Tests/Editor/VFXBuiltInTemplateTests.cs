using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PudinKiller.VFXMeshLab.Editor.Tests
{
    public sealed class VFXBuiltInTemplateTests
    {
        private const string TemplateRoot =
            "Packages/com.pudinkiller.vfx-mesh-lab/Editor/Presets/Templates";

        [TestCase("CrossPlane", VFXMeshShapeType.CrossPlanes)]
        [TestCase("Droplet", VFXMeshShapeType.Hemisphere)]
        [TestCase("Fan", VFXMeshShapeType.Disc)]
        [TestCase("Shockwave", VFXMeshShapeType.Disc)]
        [TestCase("Slash", VFXMeshShapeType.Arc)]
        [TestCase("Spiral", VFXMeshShapeType.Helix)]
        [TestCase("Splash", VFXMeshShapeType.Ring)]
        public void BuiltInTemplateLoadsAndBuilds(
            string assetName,
            VFXMeshShapeType expectedShape)
        {
            var preset = AssetDatabase.LoadAssetAtPath<VFXMeshRecipePreset>(
                TemplateRoot + "/" + assetName + ".asset");

            Assert.That(preset, Is.Not.Null, assetName);
            Assert.That(preset.name, Is.EqualTo(assetName));
            Assert.That(preset.recipe, Is.Not.Null);
            Assert.That(preset.recipe.shapeType, Is.EqualTo(expectedShape));
            Assert.That(preset.recipe.shape, Is.Not.Null);
            Assert.That(
                preset.recipe.shape.radialDistributionCurve,
                Is.Not.Null,
                assetName + " did not receive a default radial distribution curve.");

            var result = VFXMeshBuilder.Build(preset.recipe.DeepCopy());
            try
            {
                Assert.That(result.succeeded, Is.True, result.error);
                Assert.That(result.vertexCount, Is.GreaterThan(0));
                Assert.That(result.triangleCount, Is.GreaterThan(0));
            }
            finally
            {
                if (result.mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(result.mesh);
                }
            }
        }

        [Test]
        public void CatalogContainsTheSevenAuthoredTemplatesInOrder()
        {
            var libraryType = typeof(VFXMeshLabWindow).Assembly.GetType(
                "PudinKiller.VFXMeshLab.Editor.VFXBuiltInTemplateLibrary");
            var countProperty = libraryType?.GetProperty(
                "Count",
                BindingFlags.Static | BindingFlags.NonPublic);
            var getDisplayName = libraryType?.GetMethod(
                "GetDisplayName",
                BindingFlags.Static | BindingFlags.NonPublic);
            var getAssetPath = libraryType?.GetMethod(
                "GetAssetPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(libraryType, Is.Not.Null);
            Assert.That(countProperty, Is.Not.Null);
            Assert.That(getDisplayName, Is.Not.Null);
            Assert.That(getAssetPath, Is.Not.Null);

            var expectedDisplayNames = new[]
            {
                "Cross Plane",
                "Droplet",
                "Fan",
                "Shockwave",
                "Slash",
                "Spiral",
                "Splash"
            };
            var expectedFileNames = new[]
            {
                "CrossPlane.asset",
                "Droplet.asset",
                "Fan.asset",
                "Shockwave.asset",
                "Slash.asset",
                "Spiral.asset",
                "Splash.asset"
            };
            Assert.That(
                (int)countProperty.GetValue(null),
                Is.EqualTo(expectedDisplayNames.Length));

            for (var index = 0; index < expectedDisplayNames.Length; index++)
            {
                Assert.That(
                    (string)getDisplayName.Invoke(null, new object[] { index }),
                    Is.EqualTo(expectedDisplayNames[index]));
                Assert.That(
                    (string)getAssetPath.Invoke(null, new object[] { index }),
                    Is.EqualTo(TemplateRoot + "/" + expectedFileNames[index]));
            }
        }

        [Test]
        public void ApplyingBuiltInTemplateDeepCopiesItsRecipe()
        {
            var source = AssetDatabase.LoadAssetAtPath<VFXMeshRecipePreset>(
                TemplateRoot + "/Slash.asset");
            Assert.That(source, Is.Not.Null);
            var sourceRadius = source.recipe.shape.radius;
            var windowType = typeof(VFXMeshLabWindow);
            var applyTemplate = windowType.GetMethod(
                "ApplyBuiltInTemplate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var recipeField = windowType.GetField(
                "recipe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var selectedPresetField = windowType.GetField(
                "selectedPreset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentStateKeyField = windowType.GetField(
                "persistentStateKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyTemplate, Is.Not.Null);
            Assert.That(recipeField, Is.Not.Null);
            Assert.That(selectedPresetField, Is.Not.Null);
            Assert.That(persistentStateKeyField, Is.Not.Null);

            var stateKey =
                "com.pudinkiller.vfx-mesh-lab.template-test." +
                Guid.NewGuid().ToString("N");
            VFXMeshLabWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<VFXMeshLabWindow>();
                persistentStateKeyField.SetValue(window, stateKey);
                applyTemplate.Invoke(window, new object[] { 4 });

                var appliedRecipe =
                    (VFXMeshRecipe)recipeField.GetValue(window);
                Assert.That(
                    selectedPresetField.GetValue(window),
                    Is.SameAs(source));
                Assert.That(appliedRecipe, Is.Not.SameAs(source.recipe));
                Assert.That(appliedRecipe.shape, Is.Not.SameAs(source.recipe.shape));

                appliedRecipe.shape.radius += 10f;
                Assert.That(
                    source.recipe.shape.radius,
                    Is.EqualTo(sourceRadius).Within(1e-6f));
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }

                EditorPrefs.DeleteKey(stateKey);
            }
        }

        [Test]
        public void OnlyProjectPresetsCanBeUpdated()
        {
            var libraryType = typeof(VFXMeshLabWindow).Assembly.GetType(
                "PudinKiller.VFXMeshLab.Editor.VFXBuiltInTemplateLibrary");
            var isEditable = libraryType?.GetMethod(
                "IsEditableProjectPreset",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(isEditable, Is.Not.Null);

            var packagePreset =
                AssetDatabase.LoadAssetAtPath<VFXMeshRecipePreset>(
                    TemplateRoot + "/Fan.asset");
            Assert.That(packagePreset, Is.Not.Null);
            Assert.That(
                (bool)isEditable.Invoke(null, new object[] { packagePreset }),
                Is.False);

            var editablePath =
                "Assets/VFXMeshLabEditablePresetTest_" +
                Guid.NewGuid().ToString("N") +
                ".asset";
            var editablePreset =
                ScriptableObject.CreateInstance<VFXMeshRecipePreset>();
            try
            {
                AssetDatabase.CreateAsset(editablePreset, editablePath);
                AssetDatabase.SaveAssets();
                Assert.That(
                    (bool)isEditable.Invoke(null, new object[] { editablePreset }),
                    Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(editablePath);
            }
        }
    }
}
