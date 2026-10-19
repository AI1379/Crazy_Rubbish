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
        private IEnumerator SmokeReleaseReadiness(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("首版验收："+message);}
            var saved=Model;bool savedSession=session;Model=new PlanSimulation(LoadConfig());Model.SetPaused(true);session=false;MenuOpen=true;BuildPresentation();yield return null;
            Check(menu.Find("Version").GetComponent<Text>().text.Contains(Application.version),"标题版本号不准确");
            menu.Find("操作说明").GetComponent<Button>().onClick.Invoke();Check(SaveDialogOpen&&saveDialogKind=="help"&&MenuOpen&&!session,"标题说明误开经营");
            Check(saveWindow.GetComponentsInChildren<ScrollRect>().Any()&&saveWindow.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("恢复工作")&&t.text.Contains("手动支付")),"操作说明缺少关键内容或不能滚动");
            yield return null;if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-player-help.png"));
            EscapeInterface();Check(!SaveDialogOpen&&MenuOpen&&!session,"关闭说明越过角色选择");
            session=true;MenuOpen=false;Model.State.ground.Clear();Model.State.warehouse.capacity=1000;
            foreach(var item in Model.Config.items)Model.Discover(item.id);foreach(var a in Model.Config.Facility("wood").build)Model.State.warehouse.Add(a.item,a.Ticks);
            var wood=Model.Build("wood",8,8);wood.pending=false;Model.SelectRecipe(wood,"wood_boards");wood.input.Add("waste_wood",4*PlanStock.Scale);wood.tools.Add("handsaw_wood",PlanStock.Scale,1,0,Model.State.nextId++);
            BuildPresentation();RefreshInterface();string before=JsonUtility.ToJson(Model.Capture());
            Check(riskNotices.Count(r=>r.key=="tools")==1,"单条产线缺工具没有提醒");
            riskNotices.Single(r=>r.key=="tools").open();Check(ContextOpen&&contextMenu.Find("Title").GetComponent<Text>().text=="缺工具设施","工具提醒无法打开列表");
            contextBody.GetComponentInChildren<Button>().onClick.Invoke();Check(selectedSite==wood.id&&contextLiveText!=null,"工具提醒不能定位设施");
            Check(JsonUtility.ToJson(Model.Capture())==before,"工具提醒改变台账");CloseContext();
            wood.tools.Add("handsaw_wood",PlanStock.Scale,1,300,Model.State.nextId++);RefreshInterface();Check(!riskNotices.Any(r=>r.key=="tools"),"补充工具后提醒不消失");
            Model.SetSiteEnabled(wood,false);wood.tools.stacks.Clear();RefreshInterface();Check(!riskNotices.Any(r=>r.key=="tools"),"停工产线仍提醒工具");
            Model.SetPaused(false);OpenPlayerHelp();double elapsed=Model.State.seconds;Update();Check(Model.State.seconds==elapsed,"阅读说明时经营继续");EscapeInterface();Check(!SaveDialogOpen&&!Model.State.paused,"说明改变原运行状态");
            Model=saved;session=savedSession;MenuOpen=false;BuildPresentation();RefreshPresentation(true);
        }
    }
}
