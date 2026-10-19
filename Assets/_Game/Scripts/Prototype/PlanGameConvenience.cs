using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private int planInterfaceSize,knownAcquiredCount;
        private bool planSizeLoaded;
        private Button planSizeButton;
        private bool PlanTestRun=>Array.IndexOf(Environment.GetCommandLineArgs(),"-planSmoke")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-mergeCompatibilitySmoke")>=0;
        private string PlanInterfaceSizeName=>new[]{"自适应","紧凑","大号"}[planInterfaceSize];
        private void ApplyPlanInterfaceSize()
        {
            if(!planSizeLoaded){planInterfaceSize=Application.isPlaying&&!PlanTestRun?Mathf.Clamp(PlayerPrefs.GetInt("PlanInterfaceSize",0),0,2):0;planSizeLoaded=true;}
            var scaler=planCanvas.GetComponent<CanvasScaler>();scaler.referenceResolution=planInterfaceSize==1?new Vector2(1800,1125):planInterfaceSize==2?new Vector2(1440,900):new Vector2(1600,1000);
            planCanvas.GetComponent<Canvas>().scaleFactor=Mathf.Min(Screen.width/scaler.referenceResolution.x,Screen.height/scaler.referenceResolution.y);
            Canvas.ForceUpdateCanvases();if(planSizeButton!=null)planSizeButton.transform.Find("Label").GetComponent<Text>().text="界面大小："+PlanInterfaceSizeName;
        }
        public void CyclePlanInterfaceSize()
        {
            planInterfaceSize=(planInterfaceSize+1)%3;ApplyPlanInterfaceSize();FitViewport();
            if(!PlanTestRun&&Application.isPlaying)PlayerPrefs.SetInt("PlanInterfaceSize",planInterfaceSize);
        }
        private void NotifyPlanDiscoveries()
        {
            if(Model==null)return;int count=Model.State.acquired.Count;if(count<knownAcquiredCount){knownAcquiredCount=count;return;}
            var fresh=Model.State.acquired.Skip(knownAcquiredCount).Select(id=>Model.Config.items.FirstOrDefault(i=>i.id==id)).Where(i=>i!=null).ToArray();knownAcquiredCount=count;if(fresh.Length==0)return;
            var item=fresh[0];var recipes=Model.Config.recipes.Where(r=>r.inputs.Any(a=>a.item==item.id)||!string.IsNullOrEmpty(item.tool_family)&&r.tools.Any(a=>a.item==item.tool_family)).Select(r=>r.name).Distinct().Take(3).ToArray();
            Notice="新物品："+item.name+(recipes.Length>0?" · 可用于："+string.Join("、",recipes):" · 可在物品图鉴查看用途。")+(fresh.Length>1?"（另有"+(fresh.Length-1)+"种新物品）":"");
        }
    }
}
