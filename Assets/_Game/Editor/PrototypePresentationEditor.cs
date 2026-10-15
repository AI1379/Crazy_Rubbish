using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrazyRubbish.Editor
{
    // Materialize the starting presentation once; thereafter the saved scene is editable.
    [InitializeOnLoad]
    public static class PrototypePresentationEditor
    {
        private const string MainScene = "Assets/_Game/Scenes/Main.unity";
        static PrototypePresentationEditor()
        {
            EditorApplication.delayCall += EnsureOnOpen;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += EnsureOnOpen;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += EnsureOnOpen;
            };
        }

        private static void EnsureOnOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScene) return;
            var world = UnityEngine.Object.FindObjectOfType<PrototypeWorld>();
            var hud = UnityEngine.Object.FindObjectOfType<PrototypeHud>();
            if (world == null || hud == null || (world.HasCurrentLayout && hud.HasLayout)) return;
            bool wasDirty = scene.isDirty;
            EnsureCurrentLayout();
            // Never save unrelated unsaved scene work during automatic migration.
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            if (!Application.isBatchMode) FocusWorld();
        }

        public static void EnsureCurrentLayout()
        {
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            if (game == null || game.balance == null || game.appearance == null) return;
            if (game.appearance.characters == null || game.appearance.characters.Length != 6)
            {
                DemoArtSetup.Apply(game.appearance);
                EditorUtility.SetDirty(game.appearance);
                AssetDatabase.SaveAssets();
            }
            if (game.appearance.marker == null)
            {
                game.appearance.marker = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                EditorUtility.SetDirty(game.appearance);
                AssetDatabase.SaveAssets();
            }
            var settings = JsonUtility.FromJson<PrototypeSettings>(JsonUtility.ToJson(game.balance.values));
            var model = new PrototypeSimulation(settings, 4271, true);
            var world = game.GetComponent<PrototypeWorld>();
            var hud = game.GetComponent<PrototypeHud>();
            bool changed = !world.HasCurrentLayout || !hud.HasLayout;
            world.BuildLayout(model);
            hud.BuildLayout();
            if (!changed) return;
            world.RefreshView(model); hud.RefreshView(model);
            game.name = "Crazy Rubbish";
            EditorUtility.SetDirty(world); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Scene presentation ready: World and Interface are visible without entering Play mode.");
        }

        [MenuItem("Crazy Rubbish/Refresh Scene Presentation")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            if (game == null) return;
            Undo.RegisterFullObjectHierarchyUndo(game.gameObject, "Refresh Scene Presentation");
            var world = game.GetComponent<PrototypeWorld>();
            var hud = game.GetComponent<PrototypeHud>();
            if (world.VisualRoot != null) UnityEngine.Object.DestroyImmediate(world.VisualRoot.gameObject);
            if (hud.VisualRoot != null) UnityEngine.Object.DestroyImmediate(hud.VisualRoot.gameObject);
            EnsureCurrentLayout();
            FocusWorld();
        }

        [MenuItem("Crazy Rubbish/Focus World in Scene")]
        public static void FocusWorld()
        {
            var world = UnityEngine.Object.FindObjectOfType<PrototypeWorld>();
            if (world == null || world.VisualRoot == null) return;
            Selection.activeGameObject = world.VisualRoot.gameObject;
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.in2DMode = true;
            view.orthographic = true;
            view.LookAtDirect(new Vector3(15.5f, 13.5f, 0), Quaternion.identity, 19);
            view.Repaint();
        }

        // Command-line validation includes a disk roundtrip, not just a generated in-memory view.
        public static void ValidateSavedPresentation()
        {
            PrototypeProjectSetup.Setup();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            var world = game.GetComponent<PrototypeWorld>();
            var hud = game.GetComponent<PrototypeHud>();
            if (!world.HasLayout || !hud.HasLayout || game.Model != null)
                throw new InvalidOperationException("Saved scene is missing its edit-mode presentation.");
            int sprites = world.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Length;
            int buttons = hud.VisualRoot.GetComponentsInChildren<Button>(true).Length;
            if (sprites < game.balance.values.mapWidth * game.balance.values.mapHeight || buttons < 250)
                throw new InvalidOperationException("Serialized world/UI is incomplete.");
            if (world.VisualRoot.Find("Ground Items").childCount == 0)
                throw new InvalidOperationException("Starting items are invisible in edit mode.");
            if (UnityEngine.Object.FindObjectsOfType<Canvas>().Length != 1)
                throw new InvalidOperationException("Duplicate UI canvas.");
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "presentation.json"),
                "{\"success\":true,\"savedScene\":true,\"simulationStarted\":false,\"sprites\":" + sprites + ",\"buttons\":" + buttons + "}");
            Debug.Log("Saved presentation verified: " + sprites + " sprites, " + buttons + " buttons, no running simulation.");
        }
    }
}
