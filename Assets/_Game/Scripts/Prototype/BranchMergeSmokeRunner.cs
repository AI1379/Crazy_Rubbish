using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // An opt-in acceptance check for switching from the new title to the preserved main runtime.
    public sealed class BranchMergeSmokeRunner : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool success, titleSwitch, independentArtwork, singleInputAndCanvas, legacyDiskSave;
            public string error;
        }
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mergeCompatibilitySmoke") < 0) return;
            new GameObject("Branch merge acceptance").AddComponent<BranchMergeSmokeRunner>();
        }
        private IEnumerator Start()
        {
            yield return null; yield return null;
            var report = new Report();
            try
            {
                if (string.IsNullOrEmpty(Argument("-prototypeSaveDirectory"))) throw new InvalidOperationException("An isolated legacy save directory is required.");
                var plan = FindObjectOfType<PlanGameController>();
                var legacy = FindObjectsOfType<PrototypeGame>(true).Single();
                if (plan == null || legacy.gameObject.activeSelf) throw new InvalidOperationException("Default runtime is not Plan.");
                report.independentArtwork = plan.appearance != legacy.appearance && legacy.appearance.characters.Length == 6;
                var button = plan.transform.Find("Plan Interface/Main Menu").GetComponentsInChildren<Button>(true).Single(b => b.name == "旧版游戏 / 存档");
                button.onClick.Invoke();
                report.titleSwitch = !plan.gameObject.activeSelf && legacy.gameObject.activeSelf;
            }
            catch (Exception e) { report.error = e.ToString(); }
            yield return null; yield return null;
            if (report.error == null)
            {
                try
                {
                    var legacy = FindObjectOfType<PrototypeGame>();
                    if (legacy == null || legacy.Model == null || !legacy.MenuOpen || legacy.SessionStarted)
                        throw new InvalidOperationException("Legacy title did not initialize normally.");
                    report.singleInputAndCanvas = FindObjectsOfType<EventSystem>().Length == 1 && FindObjectsOfType<Canvas>().Length == 1;
                    legacy.GetComponent<PrototypeHud>().VisualRoot.GetComponentsInChildren<Button>(true).Single(b => b.name == "Resume").onClick.Invoke();
                    if (!legacy.SessionStarted || legacy.MenuOpen) throw new InvalidOperationException("Legacy menu did not start a session.");
                    legacy.Model.SetPaused(true);
                    legacy.SaveGame();
                    var saved = legacy.Model;
                    legacy.LoadLatest();
                    report.legacyDiskSave = legacy.LastActionSucceeded && legacy.Model != saved && legacy.Model.CashCents == saved.CashCents
                        && legacy.Model.Ground.Sum(g => g.Amount) == saved.Ground.Sum(g => g.Amount)
                        && legacy.ListSavedGames().Any(s => s.Valid);
                    string screenshot = Argument("-mergeScreenshot");
                    if (screenshot != null) { Directory.CreateDirectory(Path.GetDirectoryName(screenshot)); PrototypeSmokeRunner.CapturePreview(screenshot); }
                }
                catch (Exception e) { report.error = e.ToString(); }
            }
            report.success = report.error == null && report.titleSwitch && report.independentArtwork && report.singleInputAndCanvas && report.legacyDiskSave;
            string path = Argument("-mergeResults");
            if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true)); }
            if (!report.success) Debug.LogError(JsonUtility.ToJson(report));
            Application.Quit(report.success ? 0 : 2);
        }
    }
}
