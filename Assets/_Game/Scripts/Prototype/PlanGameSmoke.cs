using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator Smoke()
        {
            yield return null;yield return null;
            string[] args=Environment.GetCommandLineArgs();string Arg(string name){int n=Array.IndexOf(args,name);return n>=0&&n+1<args.Length?args[n+1]:null;}
            string screenshot=Arg("-planScreenshot"),result=Arg("-planResults"),balanceSnapshot=Arg("-planBalanceSnapshot");bool passed=false,balanceChecked=false;long longPathMs=-1;int longPathLength=0,chunks=0;
            try
            {
                if(!MenuOpen||session)throw new InvalidOperationException("首次启动未进入标题菜单。");
                EscapeInterface();if(!MenuOpen)throw new InvalidOperationException("标题菜单被Esc跳过。");
                menu.Find("新游戏 · 女主").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                if(MenuOpen||!session||Model.State.hero!="female")throw new InvalidOperationException("角色选择未建立经营会话。");
                MenuOpen=false;session=true;Model.State.paused=true;RefreshPresentation(true);
                var heroScreen=mapCamera.WorldToScreenPoint(new Vector3((float)Model.State.player.x,(float)Model.State.player.y,0));
                var mini=planCanvas.Find("Map overview");if(mini==null)throw new InvalidOperationException("小地图入口缺失。");if(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)mini,heroScreen,null))throw new InvalidOperationException("小地图遮挡开局角色。");
                if(Model.Config.items.Length!=201||Model.Config.facilities.Length!=53||Model.Config.recipes.Length!=216)throw new InvalidOperationException("配置目录不完整。");
                if(screenshot!=null){Directory.CreateDirectory(Path.GetDirectoryName(screenshot));PrototypeSmokeRunner.CapturePreview(screenshot);}
                var copy=JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(Model.Capture()));var resumed=new PlanSimulation(LoadConfig(),1,copy);
                if(resumed.State.ground.Sum(g=>g.stock.Used(resumed.Config))!=Model.State.ground.Sum(g=>g.stock.Used(Model.Config)))throw new InvalidOperationException("新存档物资台账未正确恢复。");
                var before=Model.Capture();uint seed=before.rng;double capacity=before.ground.Sum(g=>g.stock.Used(Model.Config));Save();Load();if(Model.State.rng!=seed||Model.State.ground.Sum(g=>g.stock.Used(Model.Config))!=capacity)throw new InvalidOperationException("新版存档磁盘保存读取失败。");
                foreach(string id in new[]{"waste_wood","fasteners","steel_parts","thermal_module","generator_module","wire","boards","plastic_mix"})Model.Discover(id);
                Model.State.cash=100000;Model.State.warehouse.capacity=1000;
                foreach(var a in Model.Config.Facility("wood").build)Model.State.warehouse.Add(a.item,a.Ticks);
                var wood=Model.Build("wood",8,8);Model.SelectRecipe(wood,"wood_boards");Model.State.paused=false;
                Model.State.player.x=9;Model.State.player.y=7;Model.State.heroSite=wood.id;
                Model.State.backpack.Add("handsaw_wood",PlanStock.Scale,1,300,Model.State.nextId++);wood.input.Add("waste_wood",4*PlanStock.Scale);
                Model.Advance(50);if(wood.output.Count("boards")<3*PlanStock.Scale)throw new InvalidOperationException("新版手工产线未产出木板。");
                Model.BuyRegion(2);if(Model.State.region!=2||Model.State.springs.Count!=2)throw new InvalidOperationException("第二区域未生成。");
                BuildPresentation();mapCamera.transform.position=new Vector3(28,13,-10);mapCamera.orthographicSize=24;RefreshPresentation(true);
                if(GetComponentsInChildren<Transform>().Count(t=>t.name=="Plan World")!=1)throw new InvalidOperationException("重建后旧地图仍在参与绘制。");
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-region2.png"));
                mapCamera.transform.position=new Vector3(29,30,-10);mapCamera.orthographicSize=35;RefreshPresentation(true);
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-expanded.png"));
                mapCamera.transform.position=new Vector3(15,43,-10);mapCamera.orthographicSize=18;RefreshPresentation(true);
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-terrain.png"));
                var pathWatch=System.Diagnostics.Stopwatch.StartNew();var far=PlanTerrain.Cells(2).Where(c=>Model.Walkable(c.x,c.y)).OrderBy(c=>(c.x-145)*(c.x-145)+(c.y-230)*(c.y-230)).First();var across=Model.Path(9,4,far.x,far.y);pathWatch.Stop();longPathMs=pathWatch.ElapsedMilliseconds;if(across==null)throw new InvalidOperationException("跨洞室长距离路径不可达。");longPathLength=across.Length;chunks=terrainRenderers.Count;if(chunks>1800)throw new InvalidOperationException("大地图分块过多。");
                mapCamera.transform.position=new Vector3(77,160,-10);mapCamera.orthographicSize=27;RefreshPresentation(true);
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-rivers.png"));
                mapCamera.transform.position=new Vector3(125,125,-10);mapCamera.orthographicSize=135;RefreshPresentation(true);
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-world.png"));
                Model=PlayRealStarter();BuildPresentation();RefreshPresentation(true);
                if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-starter.png"));
                passed=true;
            }
            catch(Exception e){Debug.LogException(e);Notice=e.Message;}
            if(passed)
            {
                var interaction=SmokeContextInterface(screenshot);
                while(true)
                {
                    bool more;try{more=interaction.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}
                    if(!more)break;yield return interaction.Current;
                }
            }
            if(passed)
            {
                var time=SmokeTimeHud(screenshot);
                while(true)
                {
                    bool more;try{more=time.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}
                    if(!more)break;yield return time.Current;
                }
            }
            if(passed)
            {
                var settings=SmokeSettings(screenshot);
                while(true)
                {
                    bool more;try{more=settings.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}
                    if(!more)break;yield return settings.Current;
                }
            }
            if(passed)
            {
                var stability=SmokeStability(screenshot);
                while(true){bool more;try{more=stability.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return stability.Current;}
            }
            if(passed)
            {
                var readouts=SmokeReadouts(screenshot);
                while(true){bool more;try{more=readouts.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return readouts.Current;}
            }
            if(passed)
            {
                var automation=SmokeAutomation(screenshot);
                while(true){bool more;try{more=automation.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return automation.Current;}
            }
            if(passed)
            {
                var journey=SmokeJourney(screenshot);
                while(true){bool more;try{more=journey.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return journey.Current;}
            }
            if(passed)
            {
                var guide=SmokeGuideAndSelectionMotion(screenshot);
                while(true){bool more;try{more=guide.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return guide.Current;}
            }
            if(passed)
            {
                var catalog=SmokeCatalog(screenshot);
                while(true){bool more;try{more=catalog.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return catalog.Current;}
            }
            if(passed)
            {
                var release=SmokeReleaseReadiness(screenshot);
                while(true){bool more;try{more=release.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more)break;yield return release.Current;}
            }
            if(passed&&!string.IsNullOrEmpty(balanceSnapshot))
            {
                var balance=SmokeBalanceCheckpoint(balanceSnapshot,screenshot);
                while(true){bool more;try{more=balance.MoveNext();}catch(Exception e){passed=false;Debug.LogException(e);Notice=e.Message;break;}if(!more){balanceChecked=true;break;}yield return balance.Current;}
            }
            if(result!=null){Directory.CreateDirectory(Path.GetDirectoryName(result));File.WriteAllText(result,"{\"version\":\""+Application.version+"\",\"development\":"+(Debug.isDebugBuild?"true":"false")+",\"activeGame\":\"S6Plan\",\"releaseReadiness\":"+(passed?"true":"false")+",\"balanceCheckpoint\":"+(balanceChecked?"true":"false")+",\"productionCatalog\":"+(passed?"true":"false")+",\"buildingSearch\":"+(passed?"true":"false")+",\"tutorialHud\":"+(passed?"true":"false")+",\"selectionAnimation\":"+(passed?"true":"false")+",\"returnJourney\":"+(passed?"true":"false")+",\"region3\":"+(passed?"true":"false")+",\"automationUi\":"+(passed?"true":"false")+",\"beltDrag\":"+(passed?"true":"false")+",\"readouts\":"+(passed?"true":"false")+",\"riskAlerts\":"+(passed?"true":"false")+",\"success\":"+(passed?"true":"false")+",\"stability\":"+(passed?"true":"false")+",\"menuDragging\":"+(passed?"true":"false")+",\"settingsUi\":"+(passed?"true":"false")+",\"timeHud\":"+(passed?"true":"false")+",\"surfaceWater\":"+(passed?"true":"false")+",\"starterPond\":"+(passed?"true":"false")+",\"leftDragMap\":"+(passed?"true":"false")+",\"boxSelection\":"+(passed?"true":"false")+",\"personCommands\":"+(passed?"true":"false")+",\"contextUi\":"+(passed?"true":"false")+",\"catalog\":true,\"fixtureProductionAndRegion2\":"+(passed?"true":"false")+",\"realZeroCashStarter\":"+(passed?"true":"false")+",\"diskSaveLoad\":"+(passed?"true":"false")+",\"longPathMs\":"+longPathMs+",\"longPathLength\":"+longPathLength+",\"terrainChunks\":"+chunks+"}");}
            Application.Quit(passed?0:2);
        }
        private PlanSimulation PlayRealStarter()
        {
            var m=new PlanSimulation(LoadConfig());m.SetPaused(false);
            void Check(bool valid,string error){if(!valid)throw new InvalidOperationException(error);}
            void Go(int x,int y){for(int i=0;i<3000&&Math.Abs(m.State.player.x-x)+Math.Abs(m.State.player.y-y)>.05;i++){Check(m.Walk(m.State.player,x,y,.1),"真实开局路径阻挡");m.Advance(.1);}Check(string.IsNullOrEmpty(m.State.ending),"真实开局发生生存失败");}
            void Collect(string id,int count){for(int i=0;i<count;i++){var g=m.State.ground.First(a=>a.stock.Count(id)>0);Go(g.x,g.y);m.Pick(g.id,id,Math.Min(PlanStock.Scale,g.stock.Count(id)));m.Advance(.5);}}
            void Store(){var w=m.State.sites.First(s=>s.type=="warehouse");var p=m.Front(w);Go(p.x,p.y);m.Deposit(w);}
            Collect("food",6);Store();Collect("water",8);Store();Collect("handsaw_wood",1);Collect("waste_wood",4);Collect("fasteners",2);
            var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");Collect("waste_wood",4);var f=m.Front(wood);Go(f.x,f.y);m.Deposit(wood);m.AssignHero(wood.id);m.Advance(50);Check(wood.output.Count("boards")>=3*PlanStock.Scale,"真实开局手工木工未出料");m.AssignHero(wood.id);
            for(int i=0;i<3;i++)m.TakeOutput(wood,"boards",PlanStock.Scale);Store();foreach(var cost in m.Config.Facility("generator").build){Collect(cost.item,(int)cost.quantity);Store();}
            var generator=m.Build("generator",12,6);Collect("waste_wood",2);f=m.Front(generator);Go(f.x,f.y);m.Deposit(generator);Collect("wire",2);Store();for(int x=12;x<=17;x++)m.Build("power_line",x,6);m.Advance(.2);Check(m.State.recognized,"真实开局未获得供电认可");m.Recruit();Check(m.Alive.Count()==1&&m.State.cash==0,"真实开局招募或零资金核对失败");return m;
        }
    }
}
