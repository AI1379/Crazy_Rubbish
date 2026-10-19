using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // Complete v1 game panel (G): production lines, technology, regions/endings and living management,
    // plus the shared operation page for every generic v1 facility.
    public sealed partial class PrototypeHud
    {
        private const int GenericFacilityPage=9;
        // Serialized so the saved Main scene keeps these references without rebuilding the interface.
        [SerializeField] private RectTransform developmentPanel,genericPage;
        [SerializeField] private RectTransform[] developmentPages=new RectTransform[4];
        [SerializeField] private Button[] developmentTabs=new Button[4];
        [SerializeField] private Text[] developmentTexts=new Text[4];
        [SerializeField] private Text genericInfo;
        private Button processMode;
        private int developmentTab,genericFacilityId,armedSale=-1;
        private float developmentRefreshAt;
        private IEnumerable<(Button button,int kind)> BuildButtons=>buttonBindings.Where(b=>b.command.StartsWith("PlaceKind")&&int.TryParse(b.command.Substring(9),out int k)&&PrototypeSimulation.IsGenericKind(k)).Select(b=>(b.button,int.Parse(b.command.Substring(9))));
        private IEnumerable<(Button button,int sale)> SaleButtons=>buttonBindings.Where(b=>b.command.StartsWith("SellSale")).Select(b=>(b.button,int.Parse(b.command.Substring(8))));
        public bool DevelopmentOpen=>developmentPanel!=null&&developmentPanel.gameObject.activeSelf;
        private static readonly string[] DevelopmentTabNames={"产线建造","科技","区域与结局","生活"};
        private static readonly string[] Families={"处理与拆解","塑料","金属与零件","生活与种植","机电与自动化","能源与处置","第3区与返程"};
        private static int FamilyOf(FacilityDef d)
        {
            string k=d.Key;
            if(k=="survey"||k=="mine"||k.StartsWith("ore_")||k=="ceramic_kiln"||k.StartsWith("lift_"))return 6;
            if(k=="boiler"||k=="turbine"||k=="condenser"||k=="landfill"||k=="baler"||k=="water_recovery"||k=="warehouse")return 5;
            if(k.Contains("plastic")||k.Contains("complex"))return 1;
            if(k.Contains("metal")||k.Contains("parts")||k=="wire_maker"||k=="auto_tool_parts")return 2;
            if(k=="washing"||k=="growing"||k=="grow_lamp"||k=="composting"||k=="substrate_mixer")return 3;
            if(k=="refurbish"||k=="electromechanical"||k.StartsWith("auto_"))return 4;
            return 0;
        }
        private static IEnumerable<FacilityDef> GenericDefinitions=>DesignCatalog.Facilities.Where(d=>PrototypeSimulation.KindOf(d.Key)==d.Kind&&!new[]{"belt","port","power_line","steam_pipe"}.Contains(d.Key));

        private void BuildFullGameHud()
        {
            developmentPanel=FramedPanel(root,"Development",new Vector2(1,0),Vector2.one,new Vector2(-412,90),new Vector2(-16,-86),Panel);
            developmentPanel.GetComponent<Image>().raycastTarget=true;
            TextAt(developmentPanel,"Title","产业与经营",18,12,240,32,23,Ink);
            ButtonAt(developmentPanel,"CloseDevelopment","×",350,16,28,30);
            for(int i=0;i<4;i++)developmentTabs[i]=ButtonAt(developmentPanel,"DevTab"+i,DevelopmentTabNames[i],12+i*94,56,90,40);
            for(int i=0;i<4;i++)developmentPages[i]=StretchPanel(developmentPanel,DevelopmentTabNames[i]+" Page",Vector2.zero,Vector2.one,new Vector2(8,12),new Vector2(-8,-108),Color.clear);

            // Production lines: one placement button per v1 facility, grouped by line family.
            var defs=GenericDefinitions.OrderBy(FamilyOf).ThenBy(d=>d.Index).ToArray();
            float height=60+defs.Length*44+Families.Length*42;
            var lines=ScrollContent(developmentPages[0],height);float y=4;int family=-1;
            developmentTexts[0]=TextAt(lines,"Lines Help","选择设施后在地图放置，R旋转。灰色为尚未解锁或缺少地区，悬停原因见下方说明。",12,y,340,44,13,Muted);y+=50;
            foreach(var d in defs)
            {
                if(FamilyOf(d)!=family){family=FamilyOf(d);SectionHeading(lines,Families[family],(family+1).ToString("00"),y);y+=42;}
                ButtonAt(lines,"PlaceKind"+d.Kind,d.Name,12,y,340,38);y+=44;
            }

            var tech=ScrollContent(developmentPages[1],2600);
            developmentTexts[1]=TextAt(tech,"Technology",string.Empty,12,4,340,1500,13,Ink);developmentTexts[1].alignment=TextAnchor.UpperLeft;
            float sy=1520;SectionHeading(tech,"出售技术使用权","15",sy);sy+=44;
            for(int i=0;i<DesignCatalog.TechnologySales.Length;i++){ButtonAt(tech,"SellSale"+i,DesignCatalog.TechnologySales[i].Name,12,sy,340,36);sy+=42;}

            var regions=ScrollContent(developmentPages[2],760);
            developmentTexts[2]=TextAt(regions,"Regions",string.Empty,12,4,340,420,14,Ink);developmentTexts[2].alignment=TextAnchor.UpperLeft;
            ButtonAt(regions,"BuyRegion","购买下一地区",12,436,340,42,true);
            TextAt(regions,"Region Help","扩区只付钱，不强制等待。跨区人员、电线和传送带可直接连通；第3区开放勘探、开采与返程工程。",12,488,340,70,13,Muted);

            var living=ScrollContent(developmentPages[3],900);
            developmentTexts[3]=TextAt(living,"Living",string.Empty,12,4,340,460,14,Ink);developmentTexts[3].alignment=TextAnchor.UpperLeft;
            float ly=476;
            for(int i=1;i<=3;i++)ButtonAt(living,"PayBonus"+i,$"发奖金 {i}档 · +{i*10}%",12+(i-1)*114,ly,108,40);ly+=48;
            ButtonAt(living,"RentDown","房租 −0.5",12,ly,166,38);ButtonAt(living,"RentUp","房租 +0.5",186,ly,166,38);ly+=46;
            ButtonAt(living,"RestService","休息服务：关",12,ly,340,38);ly+=46;
            ButtonAt(living,"ToolMaking","工作台自动补制工具：关",12,ly,340,38);ly+=46;
            ButtonAt(living,"MakePromise","向暴动者作出生活承诺",12,ly,340,40,true);ly+=50;
            TextAt(living,"Living Help","衣物4800秒后变旧衣；每600秒每人1清洁单位，每4人1块抹布并磨损扫帚。\n奖金需无欠薪，持续到下次计划发薪。",12,ly,340,90,13,Muted);
            developmentPanel.gameObject.SetActive(false);
            BuildGenericFacilityPage();
            EnsureProcessModeButton();
        }
        // Upgrade the authored scene as well as freshly generated layouts before Start binds commands.
        private void EnsureProcessModeButton()
        {
            if(facilityPanel==null)return;
            var existing=facilityPanel.Find("ProcessMode");
            processMode=existing!=null?existing.GetComponent<Button>():ButtonAt(facilityPanel,"ProcessMode","扩展配方",82,54,240,24);
            foreach(Transform child in processMode.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            processMode.gameObject.SetActive(false);
        }
        private FacilityInstance ProcessModeFacility=>SelectedFacility==GenericFacilityPage?GenericFacility:
            SelectedFacility==2||SelectedFacility==3?game.Model.SelectedFacility(FacilityKind(SelectedFacility)):null;
        private void RefreshProcessMode()
        {
            if(processMode==null)return;
            var f=ProcessModeFacility;
            bool visible=f!=null&&f.Built&&(f.Kind==2||f.Kind==4);
            processMode.gameObject.SetActive(visible);
            if(visible)processMode.GetComponentInChildren<Text>().text=SelectedFacility==GenericFacilityPage?
                (f.Kind==2?"返回模板拆解":"返回工具装配"):"扩展配方";
        }
        private void ToggleProcessMode()
        {
            var f=ProcessModeFacility;if(f==null)return;
            if(SelectedFacility==GenericFacilityPage)ShowOwnedFacility(f);
            else ShowGenericFacility(f);
        }
        private void BuildGenericFacilityPage()
        {
            if(facilityPages.Length<GenericFacilityPage)Array.Resize(ref facilityPages,GenericFacilityPage);
            genericPage=At(facilityPanel,"Generic Facility",0,204,396,384);facilityPages[GenericFacilityPage-1]=genericPage;
            genericInfo=TextAt(genericPage,"Info","",20,0,356,110,13,Muted);genericInfo.alignment=TextAnchor.UpperLeft;
            string[] labels={"上一配方","下一配方","开始 / 停止","投料与工具","取出产物","取回输入","主角操作","主角维护","前往维护侧","安排/释放操作","安排/释放维护","取消未完批次","安装附件","火电启停"};
            string[] commands={"GenericPrev","GenericNext","GenericRun","GenericFeed","GenericTake","GenericTakeInput","GenericOperate","GenericMaintain","GenericGoMaintain","GenericAssign","GenericAssignMaintain","GenericCancel","GenericAddons","GenericThermal"};
            for(int i=0;i<labels.Length;i++)ButtonAt(genericPage,commands[i],labels[i],20+i%2*184,116+i/2*38,172,34);
            genericPage.gameObject.SetActive(false);
        }
        public void ToggleDevelopment()
        {
            if(DevelopmentOpen){ClosePanels();return;}
            ClosePanels();developmentPanel.gameObject.SetActive(true);SelectDevelopmentTab(developmentTab);
        }
        private void SelectDevelopmentTab(int tab)
        {
            developmentTab=Mathf.Clamp(tab,0,3);
            for(int i=0;i<4;i++){developmentPages[i].gameObject.SetActive(i==developmentTab);SetSelected(developmentTabs[i],i==developmentTab);}
            developmentRefreshAt=0;
        }
        public void ShowGenericFacility(FacilityInstance f)
        {
            if(facilityPanel==null||f==null)return;
            ClosePanels();genericFacilityId=f.Id;selectedFacility=GenericFacilityPage;
            facilityPanel.gameObject.SetActive(true);
            for(int i=0;i<facilityPages.Length;i++)if(facilityPages[i]!=null)facilityPages[i].gameObject.SetActive(i==GenericFacilityPage-1);
            facilityTitle.text=PrototypeSimulation.FacilityName(f.Kind)+" #"+f.Id;
            facilityArtwork.sprite=game.GetComponent<PrototypeWorld>().FacilitySprite(f.Kind);
            RefreshGenericFacility(game.Model);
            RefreshProcessMode();
        }
        private FacilityInstance GenericFacility=>SelectedFacility==GenericFacilityPage?game.Model.GetFacility(genericFacilityId):null;
        private void RefreshGenericFacility(PrototypeSimulation m)
        {
            var f=GenericFacility;if(f==null)return;
            if(!f.Built&&!f.Blueprint){ClosePanels();return;}
            var def=PrototypeSimulation.Definition(f.Kind);string key=def.Key;
            var r=DesignCatalog.Recipe(f.ProcessActive?f.ActiveProcess:f.ProcessRecipe);
            string recipe=r==null?"未选择配方（上一/下一配方切换）":
                string.Join(" + ",r.Inputs.Select(q=>ItemCatalog.Name(q.Item)+q.Amount.ToString("0.##")))+" → "+string.Join(" + ",r.Outputs.Select(q=>ItemCatalog.Name(q.Item)+q.Amount.ToString("0.##")))+
                $"\n人工{r.HumanSeconds:0}/机器{r.MachineSeconds:0}/自然{r.NaturalSeconds:0}秒 · 负载{r.Power:0.#}"+(r.Tools.Length>0?" · 工具 "+string.Join("、",r.Tools.Select(t=>PrototypeSimulation.ToolFamilyName(t.Family)+"磨损"+t.Wear)):"");
            int crew=m.Residents.Count(p=>p.FacilityId==f.Id&&p.Job==WorkerJob.MachineOperator),maint=m.Residents.Count(p=>p.FacilityId==f.Id&&p.Job==WorkerJob.Maintenance);
            facilitySummary.text=m.ProcessStatus(f);facilitySummary.resizeTextForBestFit=true;facilitySummary.resizeTextMinSize=11;facilitySummary.resizeTextMaxSize=15;
            genericInfo.text=recipe+$"\n现场输入 {f.ProcessInput.Used:0.#}/{f.ProcessInput.Capacity} · 输出 {f.ProcessOutput.Used:0.#}/{f.ProcessOutput.Capacity} · 工具 {f.ProcessToolSlot.Used:0.#}"+
                (def.Maintainers>0?$"\n耐久 {f.Durability:0.#} · 维护 {maint}/{def.Maintainers}":"")+$" · 操作 {crew}/{def.Operators} · 供电 {f.PowerRatio:P0} · 已完成 {f.ProcessBatches} 批"+
                (m.MissingAddons(f).Any()?"\n缺附件："+string.Join("、",m.MissingAddons(f).Select(ItemCatalog.Name)):"");
            foreach(var b in buttonBindings)
            {
                if(!b.command.StartsWith("Generic"))continue;
                bool recipes=PrototypeSimulation.ProcessRecipes(f.Kind).Length>0;
                switch(b.command)
                {
                    case "GenericPrev":case "GenericNext":b.button.interactable=recipes;break;
                    case "GenericRun":b.button.interactable=recipes||key=="landfill";b.button.GetComponentInChildren<Text>().text=f.ProcessRun?"停止生产":"开始生产";break;
                    case "GenericMaintain":case "GenericGoMaintain":case "GenericAssignMaintain":b.button.interactable=def.Maintainers>0;break;
                    case "GenericThermal":b.button.interactable=key=="turbine";b.button.GetComponentInChildren<Text>().text=f.ThermalRunning?"火电运行 · 停机":"火电启停";break;
                    case "GenericAddons":b.button.interactable=m.MissingAddons(f).Any();break;
                    case "GenericCancel":b.button.interactable=f.ProcessActive&&!f.ProcessCompleted;break;
                }
            }
        }
        private void CycleGenericRecipe(int step)
        {
            var m=game.Model;var f=GenericFacility;if(f==null)return;
            var options=PrototypeSimulation.ProcessRecipes(f.Kind);if(options.Length==0)return;
            int at=Array.FindIndex(options,r=>r.Key==(string.IsNullOrEmpty(f.PendingProcess)?f.ProcessRecipe:f.PendingProcess));
            for(int i=1;i<=options.Length;i++)
            {
                var next=options[((at<0?(step>0?-1:0):at)+step*i+options.Length*4)%options.Length];
                if(m.RecipeUnlocked(next)&&(next.Vein==""||next.Vein==f.Vein)){game.Report(m.SelectProcess(f.Id,next.Key));return;}
            }
            game.Report(new ActionResult(false,"该设施暂无已解锁配方："+string.Join("、",options.SelectMany(r=>r.Technologies).Distinct().Where(t=>!m.TechnologyUnlocked(t)).Select(t=>DesignCatalog.Technology(t).Name))));
        }
        private bool ExecuteFullGameCommand(string command)
        {
            var m=game.Model;developmentRefreshAt=0;
            if(command.StartsWith("DevTab")&&int.TryParse(command.Substring(6),out int tab)){SelectDevelopmentTab(tab);return true;}
            if(command.StartsWith("PayBonus")&&int.TryParse(command.Substring(8),out int level)){game.Report(m.PayBonus(level));return true;}
            if(command.StartsWith("SellSale")&&int.TryParse(command.Substring(8),out int sale))
            {
                if(armedSale!=sale){armedSale=sale;game.Report(new ActionResult(true,$"再次点击确认出售“{DesignCatalog.TechnologySales[sale].Name}”：一次性收入，对应商品永久按最低价。"));return true;}
                armedSale=-1;game.Report(m.SellTechnology(DesignCatalog.TechnologySales[sale].Key));return true;
            }
            var f=GenericFacility;int id=f?.Id??0;
            switch(command)
            {
                case "OpenDevelopment":ToggleDevelopment();return true;
                case "CloseDevelopment":ClosePanels();return true;
                case "BuyRegion":game.Report(m.PurchaseRegion());return true;
                case "RentDown":game.Report(m.SetRent(Math.Max(0,m.PlayerRentCents-50)));return true;
                case "RentUp":game.Report(m.SetRent(Math.Min(200,m.PlayerRentCents+50)));return true;
                case "RestService":m.SetRestService(!m.RestService);return true;
                case "ToolMaking":m.SetAutomaticToolMaking(!m.AutomaticToolMaking);return true;
                case "MakePromise":game.Report(m.MakePromise());return true;
                case "ProcessMode":ToggleProcessMode();return true;
                case "GenericPrev":CycleGenericRecipe(-1);return true;
                case "GenericNext":CycleGenericRecipe(1);return true;
                case "GenericRun":game.Report(m.ToggleProcess(id));return true;
                case "GenericFeed":game.Report(m.FeedProcess(id));return true;
                case "GenericTake":game.Report(m.TakeProcessStock(id));return true;
                case "GenericTakeInput":game.Report(m.TakeProcessStock(id,true));return true;
                case "GenericOperate":game.Report(m.ToggleProcessPlayerRole(id,false));return true;
                case "GenericMaintain":game.Report(m.ToggleProcessPlayerRole(id,true));return true;
                case "GenericGoMaintain":if(f!=null){var side=PrototypeSimulation.MaintenanceCell(f);game.GoTo(side.x,side.y);}return true;
                case "GenericAssign":game.Report(m.AssignProcessWorker(id,false));return true;
                case "GenericAssignMaintain":game.Report(m.AssignProcessWorker(id,true));return true;
                case "GenericCancel":game.Report(m.CancelProcess(id));return true;
                case "GenericAddons":game.Report(m.InstallAddons(id));return true;
                case "GenericThermal":game.Report(m.ToggleThermal(id));return true;
            }
            return false;
        }
        private void RefreshFullGameHud(PrototypeSimulation m)
        {
            RefreshProcessMode();
            if(SelectedFacility==GenericFacilityPage)RefreshGenericFacility(m);
            if(!DevelopmentOpen)return;
            if(game.MenuOpen||game.ResultVisible){developmentPanel.gameObject.SetActive(false);return;}
            foreach(var (button,kind) in BuildButtons)
            {
                var def=DesignCatalog.GenericFacility(kind);bool unlocked=m.FacilityUnlocked(def.Key)&&def.RegionRequired<=m.OwnedRegion;
                button.interactable=unlocked;
                button.GetComponentInChildren<Text>().text=$"{def.Name} · {def.Width}×{def.Height}"+(unlocked?"":" · 未解锁");
            }
            foreach(var (button,sale) in SaleButtons)
            {
                var s=DesignCatalog.TechnologySales[sale];bool sold=m.IsSaleSold(s.Key);
                button.interactable=!sold&&m.SaleAvailable(s);
                button.GetComponentInChildren<Text>().text=$"{s.Name} · {s.Cash:0}钱"+(sold?" · 已售":armedSale==sale?" · 再点确认":"");
            }
            foreach(var b in buttonBindings)
            {
                if(b.command=="BuyRegion"){var next=m.NextRegion;b.button.interactable=next!=null&&m.Money>=next.Price;b.button.GetComponentInChildren<Text>().text=next==null?"三个地区均已取得":$"购买第{next.Number}区 {next.Name} · {next.Price:0}钱";}
                if(b.command=="RestService")b.button.GetComponentInChildren<Text>().text="休息服务："+(m.RestService?"开（1钱/600秒，幸福+5）":"关");
                if(b.command=="ToolMaking")b.button.GetComponentInChildren<Text>().text="工作台自动补制工具："+(m.AutomaticToolMaking?"开":"关");
                if(b.command=="MakePromise")b.button.interactable=m.HappinessRiot&&!m.PromiseMade;
            }
            if(Time.unscaledTime<developmentRefreshAt)return;developmentRefreshAt=Time.unscaledTime+0.5f;
            if(developmentTab==0)
            {
                var locked=GenericDefinitions.Where(d=>!(m.FacilityUnlocked(d.Key)&&d.RegionRequired<=m.OwnedRegion)).Take(3).ToArray();
                developmentTexts[0].text="选择设施后在地图放置，R旋转。"+(locked.Length>0?"\n例："+locked[0].Name+"——"+(locked[0].RegionRequired>m.OwnedRegion?$"需第{locked[0].RegionRequired}区":string.Join("；",DesignCatalog.Technologies.Where(t=>t.Facilities.Contains(locked[0].Key)).Select(m.TechnologyRequirement))):"");
            }
            if(developmentTab==1)
            {
                var visible=m.VisibleTechnologies.ToArray();
                developmentTexts[1].text=$"已解锁科技 {m.UnlockedTechnologies.Count()} / {DesignCatalog.Technologies.Length}（只列出已见原料相关条目）\n首次取得即解锁，不收费、不耗材料、无研究时间。\n\n"+
                    string.Join("\n",visible.Select(t=>(m.TechnologyUnlocked(t.Key)?"● ":"○ ")+t.Name+(m.TechnologyUnlocked(t.Key)?"":"\n    "+m.TechnologyRequirement(t))));
            }
            if(developmentTab==2)
            {
                var lift=m.LiftStage==0?"未总装":m.LiftStage==1?"已总装，待测试":m.LiftStage==2?"已通过测试，可登舱":"已抵达上层";
                developmentTexts[2].text=$"已取得地区：{m.OwnedRegion} / 3\n"+string.Join("\n",DesignCatalog.Regions.Select(r=>$"{(r.Number<=m.OwnedRegion?"●":"○")} 第{r.Number}区 {r.Name} · 地价 {r.Price:0}"))+
                    $"\n\n勘探：{(m.SurveyedVeins.Count==0?"尚无":string.Join("、",m.SurveyedVeins.Select(v=>v=="iron"?"铁":v=="copper"?"铜":"陶土")))}\n升降舱：{lift}"+
                    $"\n\n倾倒量 {m.DumpedTicks/20m:0.#} / {PrototypeSimulation.DumpThresholdTicks/20}"+(m.InvasionAt>=0?$"\n下层入侵倒计时 {m.InvasionSeconds:0} 秒（不可逆）":"")+
                    $"\n永久填埋 {m.LandfilledTicks/20m:0.#} 容量\n\n结局：零倾倒并抵达上层为好结局；有倾倒但入侵前抵达为普通结局；越过阈值600秒后下层入侵。";
            }
            if(developmentTab==3)
            {
                developmentTexts[3].text=$"平均幸福 {m.AverageHappiness:0.#} · 奖金效率 +{m.BonusFactor:P0}\n"+
                    (m.HappinessRiot?$"生活暴动 · 剩余 {m.HappinessRiotSeconds:0} 秒"+(m.PromiseMade?" · 已承诺":"")+"\n":"")+
                    $"社区清洁 {(m.CommunityClean?"正常":"不足")} · 肥皂余量 {m.SoapUnits:0} 单位\n主角衣物 {m.HeroClothesSeconds/60:0} 分钟 · 自建住房租金 {m.PlayerRentCents/100m:0.##}\n可穿衣服 {m.Warehouse.Count(ItemId.Clothes)} · 干净抹布 {m.Warehouse.Count(ItemId.CleanRag)} · 扫帚 {m.Warehouse.Count(ItemId.Broom)}\n\n"+
                    string.Join("\n",m.Residents.Select(r=>$"居民{r.Id} 幸福 {r.Happiness:0} · 衣物 {r.Mood.clothesSeconds/60:0}分"+(r.Rioting?" · 暴动":"")+(m.ActiveCauses(r).Any()?" · "+string.Join("/",m.ActiveCauses(r)):"")));
            }
        }
    }
}
