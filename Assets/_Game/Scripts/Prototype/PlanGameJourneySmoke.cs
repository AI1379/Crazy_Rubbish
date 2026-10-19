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
        private IEnumerator SmokeJourney(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("返程流程："+message);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;string savedSlot=slot,savedSnapshot=lastSavedSnapshot;Model=new PlanSimulation(LoadConfig());Model.State.cash=1000000;Model.BuyRegion(2);MenuOpen=false;session=true;BuildPresentation();ShowManagement(6);
            panelContent.GetComponentsInChildren<Button>().First(b=>b.name=="开放第3洞室").onClick.Invoke();Check(Model.State.region==3,"区域页不能开放三区");
            foreach(var item in Model.Config.items)Model.Discover(item.id);Model.State.ground.Clear();Model.State.revision++;Model.State.warehouse.capacity=10000;Model.State.nextInput=1e9;Model.State.player.nextMeal=Model.State.player.nextServices=1e9;
            foreach(var facility in Model.Config.facilities)foreach(var a in facility.build)Model.State.warehouse.Add(a.item,20*a.Ticks);
            Model.State.surveyedVeins.AddRange(PlanSimulation.MineralKinds);HideManagement();FocusJourneyPoint(66,18);RefreshPresentation(true);yield return null;Shot("-region3");
            var vein=Model.Vein("iron");OpenWorldContext(vein.anchor[0]+1,vein.anchor[1]+1,new Vector2(Screen.width*.55f,Screen.height*.75f),false);Check(ContextOpen&&contextMenu.Find("Title").GetComponent<Text>().text.Contains("铁矿"),"矿点没有交互入口");CloseContext();
            var anchor=Model.Config.Region(3).launch_platform_anchor;
            for(int i=0;i<3;i++)Model.State.people.Add(new PlanPerson{id=Model.State.nextId++,nextMeal=1e9,nextServices=1e9});
            var platform=Model.Build("lift_launch",anchor[0],anchor[1]);Model.SelectRecipe(platform,"build_lift");foreach(var a in Model.Config.Recipe("build_lift").inputs)platform.input.Add(a.item,a.Ticks);
            foreach(string family in new[]{"screwdriver","wrench"}){var tool=Model.Config.items.First(i=>i.tool_family==family);platform.tools.Add(tool.id,PlanStock.Scale,1,tool.durability,Model.State.nextId++);}
            var front=Model.Front(platform);foreach(var person in Model.Alive){person.x=front.x;person.y=front.y;person.manualAssignment=platform.id;}
            for(int x=60;x<=72;x+=4){var generator=Model.Build("generator",x,14);generator.input.Add("waste_wood",10*PlanStock.Scale);}
            for(int x=61;x<=74;x++)Model.Build("power_line",x,13);for(int y=14;y<=20;y++)Model.Build("power_line",74,y);
            Model.SetPaused(false);for(int i=0;i<600&&platform.cabinId==0;i++)Model.Advance(1);Check(platform.cabinId>0&&platform.output.Count("lift_cabin")==0,"实际总装未生成唯一舱");
            Model.SelectRecipe(platform,"lift_test");for(int i=0;i<400&&!platform.cabinTested;i++)Model.Advance(1);Check(platform.cabinTested&&string.IsNullOrEmpty(Model.State.ending),"试车未完成或提前通关");
            Model.SetPaused(true);Model.State.player.x=front.x;Model.State.player.y=front.y;FocusJourneyPoint(anchor[0]+2,anchor[1]+3);RefreshPresentation(true);
            OpenWorldContext(platform.x,platform.y,new Vector2(Screen.width*.62f,Screen.height*.78f),false,platform.id);Check(contextLiveText.text.Contains("已通过带载测试"),"平台状态未显示试车通过");yield return null;Shot("-lift-tested");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="确认登舱返程").onClick.Invoke();Check(ContextOpen&&contextMenu.Find("Title").GetComponent<Text>().text.Contains("确认登舱"),"登舱没有确认步骤");contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="确认登舱并启动").onClick.Invoke();Check(Model.HeroAboard&&!platform.active,"暂停登舱状态不正确");
            Check(TrySave(),"登舱后立即保存失败");string path=ManualPath;int platformId=platform.id;Check(TryLoadFile(path)&&Model.HeroAboard,"立即读档未恢复登舱");platform=Model.Site(platformId);Model.SetPaused(false);Model.Advance(35);Check(platform.stage==1&&platform.progress>0,"恢复后没有实际爬升");Model.SetPaused(true);double progress=platform.progress;Check(TrySave()&&TryLoadFile(path),"爬升中保存读取失败");platform=Model.Site(platformId);Check(Math.Abs(platform.progress-progress)<.0001&&Model.HeroAboard,"爬升进度或乘员丢失");
            FocusJourneyPoint(platform.x+2,platform.y+5);RefreshPresentation(true);yield return null;Check(liftView!=null&&liftView.gameObject.activeSelf&&!actorViews[0].gameObject.activeSelf,"升降舱或舱内主角表现不正确");Shot("-lift-ascent");
            Model.SetPaused(false);for(int i=0;i<300&&string.IsNullOrEmpty(Model.State.ending);i++)Model.Advance(1);Check(Model.State.endingCode=="good","没有到达好结局："+Model.State.ending);MenuOpen=true;RebuildPanel();yield return null;Check(menu.Find("Return ending")!=null&&menu.Find("返回经营")==null,"结局画面或结束状态缺失");Shot("-good-ending");string prior=File.ReadAllText(path);Check(!TrySave()&&File.ReadAllText(path)==prior,"结局覆盖了返程前存档");
            menu.Find("新游戏 · 女主").GetComponent<Button>().onClick.Invoke();Check(Model.State.region==1&&Model.State.cash==0&&!Model.HeroAboard&&string.IsNullOrEmpty(Model.State.ending),"结局后新游戏没有从最初开始");
            Model=saved;slot=savedSlot;lastSavedSnapshot=savedSnapshot;MenuOpen=false;BuildPresentation();RefreshPresentation(true);
        }
    }
}
