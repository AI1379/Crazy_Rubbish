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
        private IEnumerator SmokeReadouts(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("实时提示："+message);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.State.ground.Clear();Model.State.warehouse.capacity=1000;Model.State.cash=100000;
            foreach(var item in Model.Config.items)Model.Discover(item.id);foreach(var a in Model.Config.Facility("wood").build)Model.State.warehouse.Add(a.item,10*a.Ticks);
            MenuOpen=false;session=true;Notice="右键设施查看实时信息；点击上方提醒定位问题。";Model.SetPaused(true);BuildPresentation();var site=Model.Build("wood",8,8);site.pending=false;Model.SelectRecipe(site,"wood_boards");
            OpenWorldContext(site.x,site.y,new Vector2(Screen.width*.55f,Screen.height*.75f),false,site.id);yield return null;
            Check(contextLiveText!=null&&contextLiveText.text.Contains("缺额"),"设施没有实时缺料信息");var window=contextMenu;var text=contextLiveText;contextMenu.anchoredPosition=new Vector2(300,-210);
            var scroll=contextMenu.GetComponent<ScrollRect>();contextBody.anchoredPosition=new Vector2(0,35);scroll.velocity=Vector2.zero;Vector2 position=contextMenu.anchoredPosition,offset=contextBody.anchoredPosition;
            site.input.Add("waste_wood",PlanStock.Scale);RefreshInterface();
            Check(contextMenu==window&&contextLiveText==text&&contextMenu.anchoredPosition==position&&contextBody.anchoredPosition==offset,"实时刷新重建窗口或重置拖动和滚动");
            Check(contextLiveText.text.Contains("3"),"缺额未随投入更新");
            site.input.Add("waste_wood",3*PlanStock.Scale);RefreshInterface();Check(contextLiveText.text.Contains("缺少可用工具"),"补料后没有更新缺工具原因");
            site.tools.Add("handsaw_wood",PlanStock.Scale,1,150,Model.State.nextId++);RefreshInterface();Check(contextLiveText.text.Contains("等待操作工"),"工具补齐后未更新人力原因");
            var f=Model.Front(site);Model.State.player.x=f.x;Model.State.player.y=f.y;Model.State.heroSite=site.id;site.active=true;site.stageCommitted=true;site.progress=12.5;site.reserved.Put(site.input.Take("waste_wood",4*PlanStock.Scale));RefreshInterface();
            Check(contextLiveText.text.Contains("12.5")&&contextLiveText.text.Contains("150"),"批次进度或工具耐久未实时显示");
            site.output.Add("boards",2*PlanStock.Scale);RefreshInterface();Check(outputButtons.ContainsKey("boards")&&outputButtons["boards"].GetComponentInChildren<Text>().text.Contains("共2"),"新产物未自动出现");
            site.output.Take("boards",PlanStock.Scale);RefreshInterface();Check(outputButtons["boards"].GetComponentInChildren<Text>().text.Contains("共1"),"产物数量未更新");
            yield return null;Shot("-facility-live");outputButtons["boards"].onClick.Invoke();Check(site.output.Count("boards")==0&&!ContextOpen,"实时产物按钮不能正常取货");
            OpenWorldContext(site.x,site.y,new Vector2(Screen.width*.55f,Screen.height*.75f),false,site.id);Model.State.sites.Remove(site);RefreshInterface();Check(!ContextOpen,"已删除设施仍保留可操作菜单");
            Model.State.heroSite=0;Model.State.warehouse.stacks.Clear();Model.State.player.waterDebt=1;Model.State.nextPayroll=Model.State.seconds+100;
            var employee=new PlanPerson{id=Model.State.nextId++,x=17,y=5};Model.State.people.Add(employee);var homes=Model.State.sites.First(s=>s.type=="housing");homes.powerRatio=0;
            for(int i=0;i<3;i++){var s=new PlanSite{id=Model.State.nextId++,type="wood",recipe="wood_boards",x=30+i*4,y=10};Model.State.sites.Add(s);}
            RefreshInterface();Check(riskStrip.gameObject.activeSelf&&riskNotices.Count==5&&riskNotices.Select(r=>r.key).Distinct().Count()==5,"提醒没有汇总去重");
            Check(riskNotices[0].key=="water"&&riskNotices[0].caption.Contains("1人")&&riskButtons[3].GetComponentInChildren<Text>().text.Contains("还有"),"风险排序或更多入口错误");
            string snapshot=JsonUtility.ToJson(Model.Capture());RefreshInterface();Check(JsonUtility.ToJson(Model.Capture())==snapshot,"只读提示改变模拟");
            Shot("-risk-alerts");Vector2 hero=new Vector2((float)Model.State.player.x,(float)Model.State.player.y);double seconds=Model.State.seconds;
            riskButtons[3].onClick.Invoke();Check(ContextOpen&&contextMenu.Find("Title").GetComponent<Text>().text=="当前提醒","更多提醒无法展开");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("多处停机")).onClick.Invoke();Check(ContextOpen&&contextMenu.Find("Title").GetComponent<Text>().text=="停机设施","停机提醒不能打开列表");
            contextBody.GetComponentInChildren<Button>().onClick.Invoke();Check(ContextOpen&&contextLiveText!=null&&selectedSite==Model.State.sites.First(s=>s.type=="wood").id,"停机列表不能定位设施");CloseContext();
            OpenRiskSlot(0);Check(managementOpen&&panelTab==4&&new Vector2((float)Model.State.player.x,(float)Model.State.player.y)==hero&&Model.State.seconds==seconds,"点击预警修改人物或推进时间");
            Check(payrollStatus!=null&&payrollStatus.text.Contains("01:40")&&payrollStatus.text.Contains("预计"),"发薪预测未显示");
            employee.waterDebt=1;Model.State.player.waterDebt=0;RefreshInterface();Check(riskNotices.Single(r=>r.key=="water").caption.Contains("1人"),"预警未跟随实际缺水者");
            employee.waterDebt=0;Model.State.warehouse.Add("water",10*PlanStock.Scale);Model.State.warehouse.Add("food",10*PlanStock.Scale);RefreshInterface();
            Check(!riskNotices.Any(r=>r.key=="water"||r.key=="food"),"补足库存后提醒未消失");
            Model.State.bills.Add(new PlanBill{ids=new[]{employee.id},unpaid=new long[]{100},original=100});RefreshInterface();Check(riskNotices.Count(r=>r.key=="payroll")==1&&riskNotices.Single(r=>r.key=="payroll").caption.Contains("欠薪"),"欠薪提醒与即将发薪重复");
            yield return null;Shot("-payroll-alerts");OpenSaveBrowser();RefreshInterface();Check(!riskStrip.gameObject.activeSelf,"存档窗口仍显示可点预警");CancelSaveDialog();MenuOpen=true;RefreshInterface();Check(!riskStrip.gameObject.activeSelf,"标题菜单仍显示预警");
            Model=saved;MenuOpen=false;BuildPresentation();RefreshPresentation(true);
        }
    }
}
