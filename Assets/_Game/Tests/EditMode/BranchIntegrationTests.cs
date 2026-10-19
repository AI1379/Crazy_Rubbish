using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class BranchIntegrationTests
    {
        [TearDown] public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test] public void SavedSceneSelectsPlanAndPreservesIndependentLegacyPresentation()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");
            var plan = Object.FindObjectOfType<PlanGameController>();
            var legacy = Object.FindObjectsOfType<PrototypeGame>(true).Single();
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.legacyRoot, Is.SameAs(legacy.gameObject));
            Assert.That(legacy.gameObject.activeSelf, Is.False);
            Assert.That(plan.appearance, Is.Not.SameAs(legacy.appearance));
            Assert.That(AssetDatabase.GetAssetPath(legacy.appearance), Is.EqualTo("Assets/_Game/Data/M1Appearance.asset"));
            Assert.That(AssetDatabase.GetAssetPath(plan.appearance), Is.EqualTo("Assets/_Game/Data/PlanAppearance.asset"));
            Assert.That(legacy.GetComponent<PrototypeHud>().HasLayout, Is.True);
            Assert.That(legacy.GetComponent<PrototypeWorld>().HasCurrentLayout, Is.True);
            Assert.That(plan.appearance.planItems.Length, Is.EqualTo(201));
            Assert.That(plan.appearance.planFacilities.Length, Is.EqualTo(53));
            Assert.That(legacy.appearance.characters.Length, Is.EqualTo(6));
            Assert.That(AssetDatabase.GetAssetPath(legacy.appearance.characters[0].idle[0]), Does.EndWith("chr_hero_up.png"));
            Assert.That(AssetDatabase.GetAssetPath(plan.appearance.male.idle[0]), Does.EndWith("chr_hero_down.png"));
            Assert.That(Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<Canvas>().Length, Is.EqualTo(1));
            foreach (var root in plan.gameObject.scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), Is.Zero, t.name);
        }

        [Test] public void BothCataloguesRemainIndependentlyLoadable()
        {
            Assert.That(DesignBaseline.RegisteredItems, Is.EqualTo(197));
            Assert.That(DesignCatalog.ItemCount, Is.EqualTo(199));
            var plan = JsonUtility.FromJson<PlanConfig>(Resources.Load<TextAsset>("Plan/RuntimeConfig").text);
            Assert.DoesNotThrow(plan.Validate);
            Assert.That(plan.items.Length, Is.EqualTo(201));
            Assert.That(plan.facilities.Length, Is.EqualTo(53));
            Assert.That(plan.recipes.Length, Is.EqualTo(216));
            Assert.That(plan.technologies.Length, Is.EqualTo(54));
            Assert.That(Resources.Load<TextAsset>("Design/Balance20261011"), Is.Not.Null);
            Assert.That(SimulationSnapshot.CurrentSchema, Is.EqualTo(6));
        }

        [Test] public void SharedFontRetainsGlyphsPreviouslyExclusiveToEitherBranch()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/Art/Fonts/RubbishPrototypeSans.ttf");
            foreach (char c in "办°↓似勿吸喷墨威屈帮幂幽弱彼我油珀珊琥瑚瓦甚窄膝臂虽補辆鉴锈饱｜")
                Assert.That(font.HasCharacter(c), Is.True, c.ToString());
        }
    }
}
