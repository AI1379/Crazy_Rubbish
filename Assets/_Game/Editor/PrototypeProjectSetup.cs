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
            DemoArtSetup.Apply(appearance);
            EditorUtility.SetDirty(appearance);
            AssetDatabase.SaveAssets();

            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (UnityEngine.Object.FindObjectsOfType<PrototypeGame>(true).Length == 0) CreateScene(balance, appearance);
            UnityEngine.Object.FindObjectsOfType<PrototypeGame>(true)[0].gameObject.SetActive(true);
            PrototypePresentationEditor.EnsureCurrentLayout();
            var previewGame = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            var previewModel = new CrazyRubbish.Core.PrototypeSimulation(JsonUtility.FromJson<CrazyRubbish.Core.PrototypeSettings>(JsonUtility.ToJson(balance.values)), 4271, true);
            previewGame.GetComponent<PrototypeWorld>().RefreshView(previewModel);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Legacy prototype setup complete. Main scene and balance are ready.");
            PlanProjectSetup.Install();
        }


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
            camera.backgroundColor = new Color32(17, 27, 30, 255);
            // The world camera only clears its map viewport. Clear the entire frame first so
            // resizing the Game view cannot leave pixels from the previous resolution behind.
            var backgroundObject = new GameObject("Background Camera", typeof(Camera));
            var backgroundCamera = backgroundObject.GetComponent<Camera>();
            backgroundCamera.depth = -1;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = camera.backgroundColor;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.allowHDR = false;
            backgroundCamera.allowMSAA = false;
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
                if(development)
                {
                File.Copy("Docs/Development/11_新版策划核对与试玩.md", outputDirectory + "/新版策划核对与试玩.md", true);
                File.Copy("Docs/Development/12_自然洞室地图.md", outputDirectory + "/12_自然洞室地图.md", true);
                File.Copy("Docs/Development/14_大地图与地下河.md", outputDirectory + "/大地图与地下河.md", true);
                File.Copy("Docs/Development/15_方形地图与连续河网.md", outputDirectory + "/方形地图与连续河网.md", true);
                File.Copy("Docs/Development/16_右键交互与简洁界面.md", outputDirectory + "/右键交互与简洁界面.md", true);
                File.Copy("Docs/Development/17_拖动地图与框选指挥.md", outputDirectory + "/拖动地图与框选指挥.md", true);
                File.Copy("Docs/Development/18_河岸取水与出生地水塘.md", outputDirectory + "/河岸取水与出生地水塘.md", true);
                File.Copy("Docs/Development/19_嵌入式时间控制条.md", outputDirectory + "/嵌入式时间控制条.md", true);
                File.Copy("Docs/Development/20_底部设置与存档菜单.md", outputDirectory + "/底部设置与存档菜单.md", true);
                File.Copy("Docs/Development/21_设施菜单拖动.md", outputDirectory + "/设施菜单拖动.md", true);
                File.Copy("Docs/Development/23_启动停工放置与存档修复.md", outputDirectory + "/启动停工放置与存档修复.md", true);
                File.Copy("Docs/Development/24_产线状态与风险提示.md", outputDirectory + "/产线状态与风险提示.md", true);
                File.Copy("Docs/Development/25_自动化铺带接口与运输反馈.md", outputDirectory + "/自动化操作说明.md", true);
                File.Copy("Docs/Development/26_三区矿料与返程通关.md", outputDirectory + "/三区返程通关说明.md", true);
                File.Copy("Docs/Development/27_右上教程与选中人物动画.md", outputDirectory + "/右上教程与人物动画说明.md", true);
                File.Copy("Docs/Development/28_建设搜索与生产查阅.md", outputDirectory + "/建设搜索与生产查阅说明.md", true);
                }
                else
                {
                    File.Copy("Docs/Release/0.1.0_开始游戏.txt", outputDirectory + "/0.1.0_开始游戏.txt", true);
                    File.Copy("Docs/Release/0.1.0_已知问题.txt", outputDirectory + "/0.1.0_已知问题.txt", true);
                }
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
