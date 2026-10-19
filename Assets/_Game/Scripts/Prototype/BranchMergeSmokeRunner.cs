using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    // Opt-in player acceptance for the unified game. All files use an isolated test save root.
    public sealed class BranchMergeSmokeRunner : MonoBehaviour
    {
        private static string Argument(string name){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Initialize()
        {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-mergeCompatibilitySmoke")>=0)new GameObject("Unified game acceptance").AddComponent<BranchMergeSmokeRunner>();}
        private IEnumerator Start()
        {
            yield return null;yield return null;var report=new UnifiedGameReport{version=Application.version,development=Debug.isDebugBuild};
            IEnumerator run=null;try{if(string.IsNullOrEmpty(Argument("-planSaveRoot")))throw new InvalidOperationException("An isolated save root is required.");run=FindObjectOfType<PlanGameController>().SmokeUnifiedInventory(report,Argument("-mergeScreenshot"));}catch(Exception e){report.error=e.ToString();}
            if(run!=null)while(true){bool more;try{more=run.MoveNext();}catch(Exception e){report.error=e.ToString();break;}if(!more)break;yield return run.Current;}
            report.success=report.error==null&&report.singleGame&&report.inventoryUi&&report.splitAndDrag&&report.metadataAndSwap&&report.capacityAndReservations&&report.releaseRules&&report.saveRoundtrip&&report.interfaceSizes&&report.constructionShopping;
            string file=Argument("-mergeResults");if(file!=null){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,JsonUtility.ToJson(report,true));}if(!report.success)Debug.LogError(JsonUtility.ToJson(report));Application.Quit(report.success?0:2);
        }
    }
}
