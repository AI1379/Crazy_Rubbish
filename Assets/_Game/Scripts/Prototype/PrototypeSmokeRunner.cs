using System;
using System.Collections;
using System.IO;
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
            public bool success, restart, development, saveRoundtrip;
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
                PrototypeSimulation model = game.Model;
                PrototypeStartingScenario.Run(model);
                if (model.Money <= 0 || model.TotalWagesPaidCents != 5000 || model.Residents.Count != 5 || model.DeliveredTrips <= 0)
                    throw new InvalidOperationException("M2起步、招募和首期发薪验证失败。");
                wagesPaid = model.TotalWagesPaidCents;
                recruited = model.Residents.Count;
                deliveredTrips = model.DeliveredTrips;
                if (!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
                {
                    decimal savedCash = model.Money;
                    game.SaveGame();
                    if (!game.LastActionSucceeded) throw new InvalidOperationException("Runtime save failed: " + game.Notice);
                    Require(model.SellTechnology(Technology.Wood), "change cash after save");
                    game.LoadLatest();
                    model = game.Model;
                    if (!game.LastActionSucceeded || model.Money != savedCash || model.DeliveredTrips != deliveredTrips || model.TotalWagesPaidCents != wagesPaid)
                        throw new InvalidOperationException("Runtime file save/load did not restore cash, transport and payroll.");
                    saveRoundtrip = true;
                }
                double time = model.GameSeconds;
                model.SetPaused(true); model.Advance(10);
                if (Math.Abs(model.GameSeconds - time) > 0.0001) throw new InvalidOperationException("Pause did not stop game time.");
                model.SetPaused(false);
                if (failed) throw new InvalidOperationException("Runtime emitted an error or exception.");
                game.Report(new ActionResult(true, "起步、发电、招募与首次发薪验证成功。"));
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
            game.ResetGame();
            yield return null;
            yield return null;
            try
            {
                int visiblePiles = 0;
                foreach (Transform child in game.transform)
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
                        deliveredTrips = deliveredTrips, saveRoundtrip = saveRoundtrip
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

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static void CapturePreview(string path)
        {
            Camera main = Camera.main;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (main == null || canvas == null) throw new InvalidOperationException("Preview camera or canvas missing.");
            var target = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGB32);
            target.Create();
            Rect viewport = main.rect;
            var worldTarget = new RenderTexture(Mathf.RoundToInt(1280 * viewport.width),
                Mathf.RoundToInt(800 * viewport.height), 24, RenderTextureFormat.ARGB32);
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
            uiCamera.orthographicSize = 400;
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
                GL.LoadPixelMatrix(0, 1280, 800, 0);
                Graphics.DrawTexture(new Rect(viewport.x * 1280, (1 - viewport.yMax) * 800,
                    viewport.width * 1280, viewport.height * 800), worldTarget);
                GL.PopMatrix();
                uiCamera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
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
