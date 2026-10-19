using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeStability(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("稳定性："+message);}
            void DialogClick(string name){saveWindow.Find(name).GetComponent<Button>().onClick.Invoke();}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;string savedSlot=slot,savedSnapshot=lastSavedSnapshot;bool savedSession=session;
            Model=new PlanSimulation(LoadConfig());Model.State.ground.Clear();Model.State.warehouse.capacity=1000;Model.State.cash=100000;Model.SetPaused(true);lastSavedSnapshot=null;MenuOpen=false;session=true;
            foreach(var item in Model.Config.items)Model.Discover(item.id);foreach(var a in Model.Config.Facility("wood").build)Model.State.warehouse.Add(a.item,10*a.Ticks);
            BuildPresentation();yield return null;
            buildType="wood";UpdatePreview(8,8);Check(preview.gameObject.activeSelf&&placementOutline.gameObject.activeSelf&&preview.GetComponent<SpriteRenderer>().color.g>preview.GetComponent<SpriteRenderer>().color.r,"有效建设预览不正确");
            MapLeftClick(8,8);var wood=Model.Site(selectedSite);Check(wood!=null&&wood.type=="wood","建设未成功");UpdatePreview(8,8);Check(preview.GetComponent<SpriteRenderer>().color.r>preview.GetComponent<SpriteRenderer>().color.g,"占用处仍显示绿色");CancelPlacement();
            movingSite=wood.id;UpdatePreview(11,2);Check(placementHint.text.Contains("无法放置"),"移动没有失败原因");MapLeftClick(11,2);Check(movingSite==wood.id&&wood.x==8&&wood.y==8,"移动失败退出模式或改变原设施");Shot("-placement-blocked");
            UpdatePreview(15,10);Check(preview.GetComponent<SpriteRenderer>().color.g>preview.GetComponent<SpriteRenderer>().color.r,"有效移动预览不正确");Shot("-placement-valid");MapLeftClick(15,10);Check(movingSite==0&&wood.x==15&&wood.y==10&&!preview.gameObject.activeSelf,"移动完成状态不正确");
            Check(TrySave(),"手动保存失败");string manual=ManualPath;long originalCash=Model.State.cash;string original=File.ReadAllText(manual);Model.State.cash=111;Save(true);Model.State.cash=222;
            OpenSaveBrowser();yield return null;Check(SaveDialogOpen&&saveDialogKind=="browser","存档列表缺失");
            var labels=saveWindow.GetComponentsInChildren<Text>();Check(labels.Any(t=>t.text.StartsWith("手动 ·"))&&labels.Any(t=>t.text.StartsWith("自动 ·")),"未区分手动和自动档");Shot("-save-browser");
            saveWindow.GetComponentsInChildren<Button>().First(b=>b.name=="载入 "+Path.GetFileName(manual)).onClick.Invoke();Check(saveDialogKind=="confirm","换档未保护未保存进度");Shot("-save-confirm");DialogClick("取消");Check(saveDialogKind=="browser"&&Model.State.cash==222,"取消换档丢失进度");
            RequestLoadFile(manual);DialogClick("保存并继续");Check(Model.State.cash==originalCash&&File.ReadAllText(manual)==original,"保存并读取覆盖了被选存档");
            Check(SaveFiles().Any(f=>f!=manual&&f.EndsWith("-manual.json")&&ReadSave(f).cash==222),"换档前的当前进度没有另存保留");yield return null;
            Model.State.cash=333;RequestNewGame("female");Check(SaveDialogOpen&&saveDialogKind=="confirm","新游戏未保护未保存进度");DialogClick("取消");Check(Model.State.cash==333&&session,"取消新游戏改变当前经营");
            string writableSlot=slot;slot="missing-"+Guid.NewGuid().ToString("N")+"/slot";RequestNewGame("female");DialogClick("保存并继续");Check(SaveDialogOpen&&Model.State.cash==333&&saveError.text.Contains("保存失败"),"保存失败仍然离开当前经营");slot=writableSlot;DialogClick("取消");
            RequestQuit();Check(SaveDialogOpen&&saveDialogKind=="confirm","退出未提供保存选项");DialogClick("取消");Check(!SaveDialogOpen,"取消退出未返回");
            string previousManual=ManualPath;RequestNewGame("female");DialogClick("保存并继续");Check(session&&!MenuOpen&&Model.State.hero=="female"&&ReadSave(previousManual).cash==333,"保存后新游戏流程不正确");
            Model.State.cash=444;RequestNewGame("male");DialogClick("不保存继续");Check(Model.State.hero=="male"&&Model.State.cash==0,"不保存开始新游戏失效");
            string broken=Path.Combine(SaveRoot,"stability-invalid.json");File.WriteAllText(broken,"{ invalid json");var current=Model;Check(!TryLoadFile(broken)&&ReferenceEquals(current,Model)&&File.Exists(broken),"读取失败损坏当前经营或删除了原文件");
            var invalid=JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(Model.Capture()));invalid.warehouse.Add("unknown-audit-item",PlanStock.Scale);string incompatible=Path.Combine(SaveRoot,"stability-unknown-item.json");File.WriteAllText(incompatible,JsonUtility.ToJson(invalid));Check(!TryLoadFile(incompatible)&&ReferenceEquals(current,Model),"不兼容物品记录替换了当前经营");
            Model.SetPaused(false);OpenSaveBrowser();double before=Model.State.seconds;Update();Check(Math.Abs(Model.State.seconds-before)<.00001,"存档窗口中模拟仍推进");EscapeInterface();Check(!SaveDialogOpen&&!MenuOpen,"Esc未只关闭存档窗口");
            Model=saved;slot=savedSlot;lastSavedSnapshot=savedSnapshot;session=savedSession;MenuOpen=false;BuildPresentation();RefreshPresentation(true);
        }
    }
}
