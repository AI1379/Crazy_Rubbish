using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // Runs only with an explicit command-line flag. Uses the same actions as the player.
    public sealed class PrototypeSmokeRunner : MonoBehaviour
    {
        public PrototypeGame game;
        private bool failed;

        [Serializable]
        private sealed class SmokeReport
        {
            public bool success, restart, development, saveRoundtrip, demoCompleted, tutorialCompleted;
            public double demoMinutes;
            public int assembledTools, mealsConsumed;
            public long demoWagesPaidCents;
            public double money;
            public int batches, buttons, residents, deliveredTrips;
            public long wagesPaidCents;
            public string version;
        }

        private IEnumerator Start()
        {
            if (!game.SmokeMode) yield break;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            yield return null;
            yield return null;
            string screenshot = Argument("-prototypeScreenshot");
            decimal earnedMoney = 0;
            long wagesPaid = 0;
            int recruited = 0;
            int deliveredTrips = 0;
            bool saveRoundtrip = false;
            int batches = 0;
            bool demoCompleted=false;double demoMinutes=0;int assembledTools=0,mealsConsumed=0;long demoWages=0;
            bool tutorialCompleted=false;
            if (!string.IsNullOrEmpty(screenshot))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot)));
                // A hidden batch-mode window has a black backbuffer on some Windows drivers.
                // Render the game and its UI into our own texture instead of capturing that window.
                try { CapturePreview(screenshot); }
                catch (Exception exception) { failed = true; Debug.LogException(exception); }
            }
            try
            {
                if (FindObjectsOfType<Button>().Length < 20 || Camera.main == null)
                    throw new InvalidOperationException("Scene HUD or camera was not initialized.");
                VerifyHudBindings(screenshot);
                VerifyWorldPresentation(screenshot);
                PrototypeSimulation model = game.Model;
                bool tutorialRoute=Array.IndexOf(Environment.GetCommandLineArgs(),"-prototypeTutorialSmoke")>=0;
                if(tutorialRoute)PrototypeTutorialScenario.Run(model);else PrototypeStartingScenario.Run(model);
                if (model.Money <= 0 || model.TotalWagesPaidCents != 5000 || model.Residents.Count != 5 || model.DeliveredTrips <= 0)
                    throw new InvalidOperationException("M2起步、招募和首期发薪验证失败。");
                wagesPaid = model.TotalWagesPaidCents;
                recruited = model.Residents.Count;
                deliveredTrips = model.DeliveredTrips;
                if (!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
                {
                    decimal savedCash = model.Money;
                    int savedTutorialStep=model.Tutorial.step;bool savedTutorialActive=model.Tutorial.active;
                    game.SaveGame();
                    if (!game.LastActionSucceeded) throw new InvalidOperationException("Runtime save failed: " + game.Notice);
                    Require(model.SellTechnology(Technology.Wood), "change cash after save");
                    model.Tutorial.Skip();
                    game.LoadLatest();
                    model = game.Model;
                    if (!game.LastActionSucceeded || model.Money != savedCash || model.DeliveredTrips != deliveredTrips || model.TotalWagesPaidCents != wagesPaid || model.Tutorial.step!=savedTutorialStep || model.Tutorial.active!=savedTutorialActive)
                        throw new InvalidOperationException("Runtime file save/load did not restore cash, transport and payroll.");
                    saveRoundtrip = true;
                }
                if(!tutorialRoute)PrototypeDemoScenario.RunAfterStarting(model);
                tutorialCompleted=model.Tutorial.Completed;
                if(!tutorialCompleted)throw new InvalidOperationException("教程实际进度未到完成，或教程按钮未绑定。");
                if(!model.DemoCompleted||model.AssembledTools<1||model.MissingSupplyPeople>0||model.UnpaidWageCents>0||model.MealsConsumed==0)
                    throw new InvalidOperationException("完整demo目标、生活供应或工具续补未完成。");
                demoCompleted=model.DemoCompleted;demoMinutes=model.DemoCompletedAt/60;assembledTools=model.AssembledTools;mealsConsumed=model.MealsConsumed;demoWages=model.TotalWagesPaidCents;
                if(!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
                {
                    game.SaveGame();if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                    model.Tutorial.Skip();
                    game.LoadLatest();model=game.Model;
                    if(!game.LastActionSucceeded||!model.DemoCompleted||model.AssembledTools!=assembledTools||model.MealsConsumed!=mealsConsumed||!model.Tutorial.Completed||!model.Tutorial.active)
                        throw new InvalidOperationException("完整demo存档恢复失败。");
                }
                double time = model.GameSeconds;
                model.SetPaused(true); model.Advance(10);
                if (Math.Abs(model.GameSeconds - time) > 0.0001) throw new InvalidOperationException("Pause did not stop game time.");
                model.SetPaused(false);
                if (failed) throw new InvalidOperationException("Runtime emitted an error or exception.");
                game.Report(new ActionResult(true, "完整首区demo验证成功：生活、工具制作、200钱、发薪与存档。"));
                earnedMoney = model.Money;
                batches = model.CompletedBatches;
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            // Let presentation render the completed production state before quitting.
            yield return null;
            yield return null;
            if (!failed && !string.IsNullOrEmpty(screenshot))
            {
                string completed = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot)),
                    Path.GetFileNameWithoutExtension(screenshot) + "-completed.png");
                try { CapturePreview(completed); }
                catch (Exception exception) { failed = true; Debug.LogException(exception); }
            }
            if(!failed&&!string.IsNullOrEmpty(screenshot))
            {
                try{game.ShowResult();game.GetComponent<PrototypeHud>().RefreshView(game.Model);CapturePreview(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot)),Path.GetFileNameWithoutExtension(screenshot)+"-result.png"));game.ContinueAfterResult();}
                catch(Exception exception){failed=true;Debug.LogException(exception);}
            }
            game.ResetGame();
            yield return null;
            yield return null;
            try
            {
                int visiblePiles = 0;
                foreach (Transform child in game.GetComponent<PrototypeWorld>().VisualRoot.Find("Ground Items"))
                    if (child.name.StartsWith("GroundPile_", StringComparison.Ordinal)) visiblePiles++;
                if (game.Model.Money != 0 || game.Model.BenchBuilt || game.Model.GeneratorBuilt || game.Model.Residents.Count != 0 || game.Model.Carry.Used != 0
                    || game.Model.Warehouse.Used != 0 || visiblePiles != game.Model.Ground.Count)
                    throw new InvalidOperationException("Restart did not reset state and visible ground piles.");
                if (failed) throw new InvalidOperationException("Runtime emitted an error or exception.");
                string resultPath = Argument("-prototypeResults");
                if (!string.IsNullOrEmpty(resultPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath)));
                    File.WriteAllText(resultPath, JsonUtility.ToJson(new SmokeReport
                    {
                        success = true, restart = true, development = Debug.isDebugBuild,
                        money = (double)earnedMoney, batches = batches, buttons = FindObjectsOfType<Button>().Length,
                        wagesPaidCents = wagesPaid, residents = recruited, version = Application.version,
                        deliveredTrips = deliveredTrips, saveRoundtrip = saveRoundtrip,demoCompleted=demoCompleted,demoMinutes=demoMinutes,assembledTools=assembledTools,mealsConsumed=mealsConsumed,demoWagesPaidCents=demoWages,tutorialCompleted=tutorialCompleted
                    }));
                }
                Debug.Log("PROTOTYPE_SMOKE_PASS: pickup, weighted storage, woodwork, dismantle, generator, recruitment, resident production, payroll, pause and restart.");
            }
            catch (Exception exception) { failed = true; Debug.LogException(exception); }
            Application.logMessageReceived -= OnLog;
            Application.Quit(failed ? 2 : 0);
        }

        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }

        private void VerifyHudBindings(string screenshot)
        {
            var root = game.GetComponent<PrototypeHud>().VisualRoot;
            if (game.transform.Find("World") == null || root == null || FindObjectsOfType<Canvas>().Length != 1)
                throw new InvalidOperationException("Runtime duplicated or lost the saved presentation.");
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Button Find(string name) => Array.Find(buttons, button => button.name == name);
            Find("Menu").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!game.MenuOpen||!root.Find("Demo Menu").gameObject.activeSelf)throw new InvalidOperationException("Demo menu is inaccessible.");
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-menu.png"));
            Find("Resume").onClick.Invoke();if(game.MenuOpen)throw new InvalidOperationException("Resume button is unbound.");
            game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            Find("Tab3").onClick.Invoke();
            if(!root.Find("Management/教程 Page").gameObject.activeSelf)throw new InvalidOperationException("Tutorial page missing.");
            Find("TutorialAction").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!game.Model.Tutorial.started || game.Model.Tutorial.step!=1)throw new InvalidOperationException("Tutorial start button is unbound.");
            Find("TutorialPrevious").onClick.Invoke();Find("TutorialNext").onClick.Invoke();
            if(game.Model.Tutorial.step!=1 || game.Model.CashCents!=0 || game.Model.Carry.UsedTicks!=0)throw new InvalidOperationException("Reviewing lessons changes world state.");
            Find("TutorialStart").onClick.Invoke();Find("TutorialSkip").onClick.Invoke();
            if(game.Model.Tutorial.active)throw new InvalidOperationException("Tutorial skip button is unbound.");
            Find("TutorialStart").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            Find("TutorialLocate").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!Find("Pick3").gameObject.activeInHierarchy)throw new InvalidOperationException("Tutorial does not reveal the suggested inventory control.");
            Find("InventoryFilter").onClick.Invoke();
            Find("Tab3").onClick.Invoke();game.GetComponent<PrototypeWorld>().RefreshView(game.Model);game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-tutorial.png"));
            Find("FoodPolicy").onClick.Invoke();if(game.Model.FoodPolicy!=SupplyPolicy.Free)throw new InvalidOperationException("Supply policy button is unbound.");Find("FoodPolicy").onClick.Invoke();Find("FoodPolicy").onClick.Invoke();
            Find("ExternalSupply").onClick.Invoke();if(game.Model.ExternalSupply)throw new InvalidOperationException("External supply button is unbound.");Find("ExternalSupply").onClick.Invoke();
            long beforeTrade=game.Model.CashCents;Find("Command22").onClick.Invoke();if(game.Model.CashCents!=beforeTrade||!root.Find("Trade Confirmation").gameObject.activeSelf)throw new InvalidOperationException("Technology trade confirmation is missing.");Find("CancelTrade").onClick.Invoke();
            Find("Pause").onClick.Invoke();
            if (!game.Model.Paused) throw new InvalidOperationException("Saved Pause button is unbound.");
            Find("Pause").onClick.Invoke();
            Find("Speed3").onClick.Invoke();
            if (game.Model.Speed != 3) throw new InvalidOperationException("Saved speed buttons are unbound.");
            Find("Speed1").onClick.Invoke();
            for (int i = 0; i < 4; i++)
            {
                Find("Tab" + i).onClick.Invoke();
                string[] names = { "现场 Page", "人员 Page", "仓库 Page", "教程 Page" };
                for (int j = 0; j < names.Length; j++)
                    if (root.Find("Management/" + names[j]).gameObject.activeSelf != (i == j))
                        throw new InvalidOperationException("Management tab navigation failed.");
                if (i > 0 && i<3 && !string.IsNullOrEmpty(screenshot))
                    CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + (i == 1 ? "-people.png" : "-inventory.png")));
            }
            Find("Tab2").onClick.Invoke();Find("InventoryFilter").onClick.Invoke();
            if (!Find("Pick0").gameObject.activeInHierarchy)
                throw new InvalidOperationException("Full inventory catalogue is inaccessible.");
            Find("InventoryFilter").onClick.Invoke();
            Find("Tab0").onClick.Invoke();
        }

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private void VerifyWorldPresentation(string screenshot)
        {
            var world = game.GetComponent<PrototypeWorld>();
            var hud = game.GetComponent<PrototypeHud>();
            var model = game.Model;
            var camera = world.worldCamera;
            float zoom = camera.orthographicSize;
            Vector3 position = camera.transform.position;
            long units = model.Ground.Sum(p => (long)p.Amount);
            int piles = model.Ground.Count;
            int cellCount = model.Ground.Select(p => (p.X, p.Y)).Distinct().Count();
            camera.orthographicSize = 16;
            world.RefreshView(model);
            var items = world.VisualRoot.Find("Ground Items");
            if (items.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Item" && r.enabled) != cellCount)
                throw new InvalidOperationException("Dense ground cells are not represented by one icon per cell.");
            if (items.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Quantity"))
                throw new InvalidOperationException("Quantity labels clutter the overview zoom.");
            if (world.VisualRoot.Find("PlayerLabel").gameObject.activeSelf || world.VisualRoot.Find("Tutorial Guide Target/Tutorial Target Label") != null)
                throw new InvalidOperationException("Player or tutorial text overlaps ground items.");
            var crowded = model.Ground.GroupBy(p => (p.X, p.Y)).OrderByDescending(g => g.Count()).First();
            world.FocusPoint(crowded.Key.X, crowded.Key.Y, 4);
            world.RefreshView(model);
            Vector3 targetPoint = camera.WorldToViewportPoint(new Vector3(crowded.Key.X, crowded.Key.Y, 0));
            if (!new Rect(0, 0, 1, 1).Contains(targetPoint)) throw new InvalidOperationException("Focused inspection target is outside the map viewport.");
            if (items.GetComponentsInChildren<TextMesh>().Count(t => t.name == "Quantity") != cellCount)
                throw new InvalidOperationException("Detailed zoom lost cell summaries.");
            world.InspectAt(new Vector2(crowded.Key.X, crowded.Key.Y));
            Transform inspection = hud.VisualRoot.Find("Map Inspection");
            string details = inspection.Find("Contents").GetComponent<Text>().text;
            foreach (var item in crowded.Select(p => p.Item).Distinct())
                if (!details.Contains(ItemCatalog.Name(item))) throw new InvalidOperationException("Inspection lost a mixed-cell item.");
            if (inspection.GetComponentsInChildren<Graphic>().Any(g => g.raycastTarget))
                throw new InvalidOperationException("Map inspection blocks movement clicks.");
            Canvas.ForceUpdateCanvases();
            var tooltipCorners = new Vector3[4]; inspection.GetComponent<RectTransform>().GetWorldCorners(tooltipCorners);
            Rect viewport = camera.pixelRect;
            if (tooltipCorners.Any(p => p.x < viewport.xMin || p.x > viewport.xMax || p.y < viewport.yMin || p.y > viewport.yMax))
                throw new InvalidOperationException("Map inspection extends outside the map viewport.");
            if (!string.IsNullOrEmpty(screenshot)) CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + "-inspection.png"));
            world.InspectAt(null);
            if (inspection.gameObject.activeSelf) throw new InvalidOperationException("Map inspection remains after pointer exit.");
            camera.orthographicSize = zoom; camera.transform.position = position; world.RefreshView(model);
            if (piles != model.Ground.Count || units != model.Ground.Sum(p => (long)p.Amount))
                throw new InvalidOperationException("Presentation changed the simulated resources.");
        }

        private static void CapturePreview(string path)
        {
            Camera main = Camera.main;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (main == null || canvas == null) throw new InvalidOperationException("Preview camera or canvas missing.");
            int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            Rect viewport = main.rect;
            var worldTarget = new RenderTexture(Mathf.RoundToInt(width * viewport.width),
                Mathf.RoundToInt(height * viewport.height), 24, RenderTextureFormat.ARGB32);
            worldTarget.Create();
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = main.targetTexture;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCanvasCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraObject = new GameObject("OffscreenPreviewCamera", typeof(Camera));
            var uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.enabled = false;
            uiCamera.transform.position = new Vector3(0, 0, -100);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = height / 2f;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 200;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << 5;
            uiCamera.targetTexture = target;
            Texture2D pixels = null;
            try
            {
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();
                main.targetTexture = worldTarget;
                main.rect = new Rect(0, 0, 1, 1);
                main.Render();
                RenderTexture.active = target;
                GL.Clear(true, true, new Color32(18, 28, 31, 255));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);
                Graphics.DrawTexture(new Rect(viewport.x * width, (1 - viewport.yMax) * height,
                    viewport.width * width, viewport.height * height), worldTarget);
                GL.PopMatrix();
                uiCamera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color32[] samples = pixels.GetPixels32();
                int visible = 0;
                foreach (Color32 pixel in samples) if (pixel.r + pixel.g + pixel.b > 120) visible++;
                if (visible < samples.Length / 100) throw new InvalidOperationException("Offscreen preview appears blank.");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                main.targetTexture = previousTarget;
                main.rect = viewport;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCanvasCamera;
                canvas.planeDistance = previousPlane;
                RenderTexture.active = previousActive;
                target.Release();
                worldTarget.Release();
                Destroy(target); Destroy(worldTarget); Destroy(cameraObject);
                if (pixels != null) Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(ActionResult result, string action)
        { if (!result.Success) throw new InvalidOperationException(action + ": " + result.Message); }

        private static void MoveTo(PrototypeSimulation model, double x, double y)
        {
            for (int step = 0; step < 400; step++)
            {
                double dx = x - model.PlayerX, dy = y - model.PlayerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 0.001) return;
                double delta = Math.Min(0.05, distance / (model.Settings.moveSpeed * model.Speed));
                model.MovePlayer(dx / distance, dy / distance, delta);
                model.Advance(delta);
            }
            throw new InvalidOperationException("Smoke test could not reach the interaction point.");
        }
    }
}
