using System;
using System.IO;
using CrazyRubbish.Prototype;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrazyRubbish.Editor
{
    public static class PrototypeProjectSetup
    {
        private const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        private const string BalancePath = "Assets/_Game/Data/M1Balance.asset";
        private const string AppearancePath = "Assets/_Game/Data/M1Appearance.asset";

        [MenuItem("Crazy Rubbish/Setup Starting Prototype")]
        public static void Setup()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string art = "Assets/_Game/Art/Prototype/";
            string[] names = { "Worker", "WasteWood", "MixedPlastic", "Fastener", "Saw", "Plank", "WoodHandle", "Warehouse", "Workbench", "Tile" };
            foreach (string name in names)
            {
                string path = art + name + ".png";
                if (!File.Exists(path)) throw new FileNotFoundException("Run Tools/PreparePrototypeArt.py first.", path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            const string fontPath = "Assets/_Game/Art/Fonts/RubbishPrototypeSans.ttf";
            if (!File.Exists(fontPath)) throw new FileNotFoundException("Run Tools/PreparePrototypeFont.py first.", fontPath);
            var fontImporter = (TrueTypeFontImporter)AssetImporter.GetAtPath(fontPath);
            fontImporter.fontTextureCase = FontTextureCase.Dynamic;
            fontImporter.includeFontData = true;
            fontImporter.SaveAndReimport();

            var balance = AssetDatabase.LoadAssetAtPath<PrototypeBalance>(BalancePath);
            if (balance == null)
            {
                balance = ScriptableObject.CreateInstance<PrototypeBalance>();
                AssetDatabase.CreateAsset(balance, BalancePath);
            }
            balance.values.Validate();
            var appearance = AssetDatabase.LoadAssetAtPath<PrototypeAppearance>(AppearancePath);
            if (appearance == null)
            {
                appearance = ScriptableObject.CreateInstance<PrototypeAppearance>();
                AssetDatabase.CreateAsset(appearance, AppearancePath);
            }
            appearance.font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            appearance.worker = LoadSprite(art, "Worker");
            appearance.wasteWood = LoadSprite(art, "WasteWood");
            appearance.mixedPlastic = LoadSprite(art, "MixedPlastic");
            appearance.fastener = LoadSprite(art, "Fastener");
            appearance.saw = LoadSprite(art, "Saw");
            appearance.plank = LoadSprite(art, "Plank");
            appearance.woodHandle = LoadSprite(art, "WoodHandle");
            appearance.warehouse = LoadSprite(art, "Warehouse");
            appearance.workbench = LoadSprite(art, "Workbench");
            appearance.tile = LoadSprite(art, "Tile");
            EditorUtility.SetDirty(appearance);
            AssetDatabase.SaveAssets();

            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (UnityEngine.Object.FindObjectOfType<PrototypeGame>() == null) CreateScene(balance, appearance);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("M2 starting prototype setup complete. Main scene and balance are ready.");
        }

        private static Sprite LoadSprite(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>(folder + name + ".png");

        private static void CreateScene(PrototypeBalance balance, PrototypeAppearance appearance)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(9.5f, 6.5f, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(18, 28, 31, 255);
            var root = new GameObject("M1 Prototype");
            var game = root.AddComponent<PrototypeGame>();
            game.balance = balance;
            game.appearance = appearance;
            var world = root.AddComponent<PrototypeWorld>();
            world.game = game; world.worldCamera = camera;
            root.AddComponent<PrototypeHud>().game = game;
            root.AddComponent<PrototypeSmokeRunner>().game = game;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Crazy Rubbish/Build Starting Windows Prototype")]
        public static void BuildWindows() => BuildWindowsPlayer(true);

        [MenuItem("Crazy Rubbish/Build Windows Release")]
        public static void BuildWindowsRelease() => BuildWindowsPlayer(false);

        private static void BuildWindowsPlayer(bool development)
        {
            Setup();
            string outputDirectory = development ? "Builds/Windows" : "Builds/WindowsRelease";
            Directory.CreateDirectory(outputDirectory);
            string originalVersion = PlayerSettings.bundleVersion;
            string releaseVersion = CommandLineValue("-releaseVersion");
            if (!string.IsNullOrEmpty(releaseVersion) && !System.Text.RegularExpressions.Regex.IsMatch(releaseVersion,
                @"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$"))
                throw new ArgumentException("Release version must be a numeric version with an optional prerelease suffix.");
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("The Windows CI pipeline requires the Mono scripting backend.");
            try
            {
                if (!string.IsNullOrEmpty(releaseVersion)) PlayerSettings.bundleVersion = releaseVersion;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputDirectory + "/CrazyRubbish.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                File.Copy("Assets/_Game/Art/Fonts/OFL.txt", outputDirectory + "/Font-OFL.txt", true);
                Debug.Log("Windows " + (development ? "development" : "release") + " build succeeded: " + outputDirectory);
            }
            finally { PlayerSettings.bundleVersion = originalVersion; }
        }

        private static string CommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
