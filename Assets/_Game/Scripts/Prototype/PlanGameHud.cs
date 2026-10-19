using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform panelContent,menu;private Text topStatus,noticeText,siteStatus,riotText,lifeStatus;
        private int panelTab=1;private bool inventoryAll;private float rowY;
        private static readonly Color Ink=new Color32(235,230,211,255),PanelColor=new Color32(28,43,46,255),Accent=new Color32(222,173,103,255);
        private RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=new Vector2(0,1);t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);return t;}
        private Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size=16)
        {var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=appearance.font;t.text=value;t.color=Ink;t.fontSize=size;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;}
        private Button Button(Transform parent,string caption,float x,float y,float w,float h,Action action)
        {var t=Rect(parent,caption,x,y,w,h);var image=t.gameObject.AddComponent<Image>();image.color=new Color32(48,67,70,255);var b=t.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());var label=Text(t,"Label",caption,5,0,w-10,h,15);label.alignment=TextAnchor.MiddleCenter;return b;}
        private RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=PanelColor;image.raycastTarget=false;return r;}
        private void BuildInterface()
        {
            var canvas=new GameObject("Plan Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);planCanvas=canvas.transform;canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;canvas.GetComponent<Canvas>().scaleFactor=Mathf.Min(Screen.width/1600f,Screen.height/1000f);
            if(FindObjectOfType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(transform,false);
            var header=Box(planCanvas,"Header",20,16,1560,70);
            header.anchorMax=new Vector2(1,1);header.sizeDelta=new Vector2(-40,70);
            var cover=Rect(header,"Cover",8,6,100,58).gameObject.AddComponent<Image>();cover.sprite=appearance.gameCover;cover.preserveAspect=true;cover.raycastTarget=false;
            topStatus=Text(header,"Status","",124,15,550,42,21);
            BuildTimeHud(header);
            noticeText=Text(planCanvas,"Notice","",24,96,1134,44,17);
            var sidebar=Box(planCanvas,"Management",1180,102,400,720);management=sidebar;managementOpen=false;sidebar.gameObject.SetActive(false);contextMenu=null;
            sidebar.anchorMin=sidebar.anchorMax=new Vector2(1,1);sidebar.anchoredPosition=new Vector2(-420,-102);
            string[] tabs={"教程","建设","设施","库存","居民","科技","区域"};for(int i=0;i<tabs.Length;i++){int tab=i;Button(sidebar,tabs[i],8+i%4*98,8+i/4*40,92,34,()=>{if(tab==0)OpenGuideDetails();else{panelTab=tab;RebuildPanel();}});}
            Button(sidebar,"收起 Tab",302,48,92,34,HideManagement);
            var viewport=Rect(sidebar,"Viewport",8,94,384,614);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            panelContent=Rect(viewport,"Contents",0,0,384,2000);var scroll=sidebar.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=panelContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            var footer=Box(planCanvas,"Operations",20,850,1560,130);lifeStatus=Text(footer,"Living", "",12,56,1120,64,16);
            footer.anchorMin=new Vector2(0,0);footer.anchorMax=new Vector2(1,0);footer.anchoredPosition=new Vector2(20,150);footer.sizeDelta=new Vector2(-40,130);
            Button(footer,"拾取 E",12,10,128,38,PickupNearest);Button(footer,"入库 / 投料 Q",150,10,160,38,()=>{var s=Model.Site(selectedSite);if(s!=null)Act(()=>Model.Deposit(s));});
            Button(footer,"主角操作 F",320,10,150,38,()=>{if(selectedSite>0)Act(()=>Model.AssignHero(selectedSite));});
            Button(footer,"取消操作",480,10,150,38,()=>{CancelPlacement();CloseContext();moveX=-1;Model.State.heroSite=0;});
            BuildMinimap();Button(footer,"全图 M",640,10,110,38,ToggleOverview);Button(footer,"回主角 Home",760,10,150,38,FocusHero);
            var manage=Button(footer,"管理 Tab",0,10,140,38,ToggleManagement).GetComponent<RectTransform>();manage.anchorMin=manage.anchorMax=manage.pivot=new Vector2(1,1);manage.anchoredPosition=new Vector2(-76,-10);
            placementHint=Text(planCanvas,"Placement status","",0,0,880,64,18);placementHint.rectTransform.anchorMin=placementHint.rectTransform.anchorMax=placementHint.rectTransform.pivot=Vector2.zero;placementHint.rectTransform.anchoredPosition=new Vector2(240,158);placementHint.alignment=TextAnchor.LowerLeft;
            BuildRiskStrip();riotText=Text(planCanvas,"Riot warning","",24,184,1100,100,20);riotText.color=Accent;
            BuildGuideHud();menu=Box(planCanvas,"Main Menu",350,170,900,610);menu.anchorMin=menu.anchorMax=menu.pivot=new Vector2(.5f,.5f);menu.anchoredPosition=Vector2.zero;BuildSettings(footer);BuildSaveInterface();RebuildPanel();
        }
        private void Line(string value,int size=16,float height=44)
        {Text(panelContent,"Description",value,5,rowY,374,height,size);rowY+=height+5;}
        private void Row(string caption,Action action,float width=374)
        {Button(panelContent,caption,5,rowY,width,36,action);rowY+=43;}
        private string Amounts(PlanAmount[] values)=>string.Join("、",values.Select(a=>Model.Config.Item(a.item).name+"×"+a.quantity.ToString("0.####")));
        private void RebuildPanel()
        {
            if(panelContent==null||Model==null)return;for(int i=panelContent.childCount-1;i>=0;i--)Remove(panelContent.GetChild(i).gameObject);rowY=0;siteStatus=null;payrollStatus=null;workerReadouts.Clear();buildResults=null;buildSearchField=null;
            if(managementOpen)switch(panelTab)
            {
                case 0:GuidePanel();break;case 1:BuildPanel();break;case 2:SitePanel();break;case 3:InventoryPanel();break;case 4:PeoplePanel();break;case 5:TechnologyPanel();break;case 6:RegionPanel();break;
            }
            panelContent.sizeDelta=new Vector2(384,Mathf.Max(614,rowY+20));RebuildMenu();RefreshInterface();
        }
        private void GuidePanel()
        {
            Line("第一区域：从拾取到回收工场",21,42);
            if(Model.State.tutorial>=7){Line(Model.State.region<3?"下一目标：筹集600钱开放第三区域，建立勘探、开采和返程工程。":"返程目标：完成矿料加工、升降舱总装和带载测试，在检修平台确认登舱。",17,84);Row("查看矿料与返程目标",()=>ShowManagement(6));}
            Line("移动：WASD / 左键短按空地\n镜头：左键拖动、滚轮缩放\n选择：右键菜单 / 右键拖框\n操作：E 拾取，Q 投料，F 上岗\n建设：R 旋转，右键取消\n管理：Tab；建设 B；库存 I",16,138);
            Line("1. 先把食物、水和可用手锯收好。\n2. 拾取木料与紧固件，建设木工地点。\n3. 手工锯木板，解锁装配、仓储与住房。\n4. 拆解旧设备，建设发电机并铺线连接生活设施。\n5. 实际发电后招募居民，安排生产、维护与运输。\n6. 建立采水净化与太阳灯种植，按时发工资。\n7. 优化布局，攒够200钱购买二区。",16,238);
            foreach(string id in new[]{"food","water","handsaw_wood","waste_wood","fasteners","steel_parts","thermal_module","generator_module","wire"})
            {string item=id;Row("寻找："+Model.Config.Item(id).name,()=>FindItem(item));}
            Row("前往旧仓库",()=>GoToSite(Model.State.sites.First(s=>s.type=="warehouse")));
            Line("经营提示：先保障饮水和工资，再逐段投入生活产线。电动设备仍需维护人手和搬运；缩短原料、机器与仓库间的距离。小型种植只能补充食物，留出回收或采购来源。",16,140);
            Line("所有已解锁玩法可以提前使用。扩区只检查资金与前一区域，不以教程或经营时长作门槛，也不要求先造齐生活产线。",16,100);
        }
        private void BuildPanel()
        {
            Line("选择设施后，点击已购土地放置。",18,44);
            BuildSearchBox();
            Row("传送带交叉："+(beltCrossing?"独立交叉":"合流"),()=>{beltCrossing=!beltCrossing;RebuildPanel();});
            Row("物流方向显示："+(showLogistics?"开启":"关闭"),()=>{showLogistics=!showLogistics;RefreshLogisticsVisuals();RebuildPanel();});
            Row("选中运输与待命人员",SelectTransportPeople);
            if(Model.State.technologies.Contains(Model.Config.Facility("belt").technology))
            {
                Row("拖拽铺设传送带",()=>{CancelPlacement();buildType="belt";HideManagement();Notice="左键拖拽铺带，松手建设整段；Shift换拐角，R旋转，右键取消。";});
                foreach(string mode in new[]{"input","output"}){string kind=mode;Row(mode=="input"?"建设输入接口（自动绑定）":"建设输出接口（自动绑定）",()=>StartPortPlacement(kind));}
            }
            buildResultsY=rowY;buildResults=Rect(panelContent,"Build results",0,rowY,384,0);RefreshBuildResults();
        }
        private void GoToSite(PlanSite s){var f=Model.Front(s);SetHeroDestination(f.x,f.y);selectedSite=s.id;panelTab=2;RebuildPanel();}
        private void SitePanel()
        {
            if(selectedSite==-1){Line("洞口 · 去向不明",22,44);Row("走到洞口外侧",()=>SetHeroDestination(2,19));Line("只处理实际送达洞口的弃料；普通材料需先标记废弃。",16,74);foreach(var stack in Model.State.backpack.stacks.Where(s=>Model.CanDiscard(s.item)).ToArray()){string item=stack.item;Row("人工倾倒："+Model.Config.Item(item).name,()=>Act(()=>Model.DumpBackpack(item)));}foreach(var d in Model.Config.items.Where(i=>Model.CanDiscard(i.id)&&Model.Stored(i.id)>0)){string item=d.id;Row("工人运送："+d.name,()=>Act(()=>Model.State.holeFilter=Model.State.holeFilter==item?null:item));}Row("停止向洞运料",()=>Act(()=>Model.State.holeFilter=null));return;}
            var s=Model.Site(selectedSite);if(s==null){Line("点击地图上的设施查看和安排工作。",18,80);return;}
            var definition=Model.Config.Facility(s.type);Line(definition.name+" #"+s.id,22,40);siteStatus=Text(panelContent,"Site Status",FacilityDescription(s),5,rowY,374,10,16);float statusHeight=ReadoutHeight(siteStatus);siteStatus.rectTransform.sizeDelta=new Vector2(374,statusHeight);rowY+=statusHeight+10;
            Row("走到操作侧",()=>GoToSite(s));Row("主角进入 / 退出岗位",()=>Act(()=>Model.AssignHero(s.id)));
            Row(s.enabled?"停止生产 / 输送":"恢复运行",()=>Act(()=>Model.SetSiteEnabled(s,!s.enabled)));
            Row("岗位优先级 "+s.priority+"（点击循环1—5）",()=>Act(()=>s.priority=s.priority%5+1));
            Row("入库 / 投入随身物品",()=>Act(()=>Model.Deposit(s)));
            Row("查找缺料 / 制作方法",()=>OpenSiteMaterials(s));
            foreach(var item in (s.type=="warehouse"?s.input:s.output).stacks.Select(a=>a.item).Distinct().ToArray()){Row("取："+Model.Config.Item(item).name,()=>Act(()=>Model.TakeOutput(s,item,Math.Min(PlanStock.Scale,(s.type=="warehouse"?s.input:s.output).Count(item)))));}
            foreach(var stack in s.tools.stacks.ToArray()){var item=stack.item;Row("取回工具："+Model.Config.Item(item).name+"（耐久"+stack.durability.ToString("0")+"）",()=>Act(()=>Model.TakeTool(s,item)));}
            if(s.type!="warehouse")foreach(var item in s.input.stacks.Select(a=>a.item).Distinct().ToArray()){Row("取回投入："+Model.Config.Item(item).name,()=>Act(()=>Model.TakeInput(s,item,Math.Min(PlanStock.Scale,s.input.Count(item)))));}
            if(s.type=="bench")Row("自动制作备用工具："+(s.autoTools?"开启":"关闭"),()=>Act(()=>s.autoTools=!s.autoTools));
            Row("给本岗位员工奖金 +10%",()=>Act(()=>Model.Bonus(s.id)));
            if(s.type=="grow_lamp")foreach(var bed in Model.State.sites.Where(a=>a.type=="growing")){int target=bed.id;Row("绑定种植地 #"+target,()=>Act(()=>{if(Model.State.sites.Any(l=>l!=s&&l.type=="grow_lamp"&&l.lampBed==target))throw new InvalidOperationException("该种植地已绑定太阳灯。");s.lampBed=target;}));}
            if(s.type=="port")
            {
                Row("接口模式："+(s.portMode=="input"?"输入":"输出"),()=>Act(()=>s.portMode=s.portMode=="input"?"output":"input"));Row("输出方向："+PlanSimulation.DirectionName(s.direction),()=>Act(()=>Model.RotateTransport(s)));
                foreach(var machine in Model.State.sites.Where(a=>!Model.Overlay(a.type)&&Model.Covers(a,s.x,s.y))){var target=machine;Row("连接 "+Model.Config.Facility(machine.type).name+" #"+machine.id,()=>Act(()=>Model.ConfigurePort(s,target,s.portMode)));}
                Row("取消接口过滤",()=>Act(()=>s.filter=null));
                var connected=Model.Site(s.portSite);if(connected!=null)foreach(var item in Model.Config.recipes.Where(r=>r.facility==connected.type).SelectMany(r=>s.portMode=="input"?r.inputs:r.outputs).Select(a=>a.item).Concat(connected.input.stacks.Select(a=>a.item)).Distinct()){string id=item;Row("仅输送："+Model.Config.Item(id).name,()=>Act(()=>s.filter=id));}
            }
            if(s.type=="belt")Row("改变输送方向",()=>Act(()=>Model.RotateTransport(s)));
            if(s.type=="landfill")foreach(var d1 in Model.Config.items.Where(i=>Model.CanDiscard(i.id)&&Model.Stored(i.id)>0)){string item=d1.id;Row("填埋："+d1.name,()=>Act(()=>s.filter=item));}
            foreach(var r in Model.Config.recipes.Where(r=>r.facility==s.type&&Model.RecipeAvailable(r)))
            {var recipe=r;if(r.id=="lift_ascent"){Row("确认主角登舱返程",()=>ConfirmBoardLift(s));continue;}string problem=Model.ProgressionProblem(s,r);if(problem==null)Row("配方："+r.name,()=>Act(()=>Model.SelectRecipe(s,recipe.id)));else Line(r.name+"："+problem,14,66);Line(r.facility=="survey"?"完成后揭示对应矿脉，不直接产出矿石。":r.id=="lift_test"?"测试平台内同一升降舱，不消耗或复制载具。":r.id=="build_lift"?Amounts(r.inputs)+" → 平台唯一载具槽":Amounts(r.inputs)+" → "+Amounts(r.outputs),13,74);}
            Row("取消当前批次",()=>Act(()=>Model.CancelBatch(s)));
            foreach(var addon in Model.Config.recipes.Where(r=>r.facility==s.type).SelectMany(r=>r.addons??new PlanAmount[0]).Select(a=>a.item).Distinct()){string item=addon;Row("安装附件："+Model.Config.Item(item).name,()=>Act(()=>Model.AddAttachment(s,item)));}
            if(Model.Config.recipes.Any(r=>r.facility==s.type&&r.effluent_fraction>0))Row(s.captureEffluent?"已安装废水收集附件":"安装废水收集：容器1＋电线1",()=>Act(()=>Model.InstallEffluentCapture(s)));
            if(!s.community){if(!PlanSimulation.IsWaterIntake(s.type))Row("移动：选择后点击新位置",()=>{movingSite=s.id;buildType=null;HideManagement();Notice="点击新位置移动；右键取消。";});Row("拆除并返还实际建材",()=>Act(()=>Model.RemoveSite(s)));Row("出售设备（不另退建材）",()=>Act(()=>Model.RemoveSite(s,true)));}
        }
        private int movingSite;
        private void FindItem(string item)
        {var p=Model.State.player;var g=Model.State.ground.Where(g=>g.stock.Count(item)>0).OrderBy(g=>Math.Abs(g.x-p.x)+Math.Abs(g.y-p.y)).FirstOrDefault();if(g==null){Notice="当前地面没有该物品，可查看仓库或采购。";return;}SetHeroDestination(g.x,g.y);Notice="前往物品位置，走近后在库存页点“拾”或按 E。";}
        private void InventoryPanel()
        {
            Line("仓库与背包（生产运输需人或传送带）",18,50);Row(inventoryAll?"仅显示已有物品":"显示全部目录",()=>{inventoryAll=!inventoryAll;RebuildPanel();});
            foreach(var item in Model.Config.items.Where(i=>inventoryAll||Model.Stored(i.id)>0||Model.State.backpack.Count(i.id)>0||Model.State.ground.Any(g=>g.stock.Count(i.id)>0)))
            {
                var id=item.id;long carried=Model.State.backpack.Count(id),stock=Model.Stored(id);float at=rowY;
                var itemHit=Rect(panelContent,"Item context",5,at,374,64).gameObject;itemHit.AddComponent<Image>().color=Color.clear;ItemRightClick(itemHit,id);
                var icon=Rect(panelContent,"Icon",5,at,32,32).gameObject.AddComponent<Image>();icon.sprite=appearance.PlanSprite(id);icon.preserveAspect=true;icon.raycastTarget=false;
                Text(panelContent,"Item",item.name+"\n背包 "+(carried/(double)PlanStock.Scale).ToString("0.####")+" / 仓库 "+(stock/(double)PlanStock.Scale).ToString("0.####")+"\n买价 "+(item.canBuy?item.buy.ToString("0.##"):"—")+" / 售价 "+(item.canSell?Model.Price(id).ToString("0.##"):"—"),42,at,332,62,15);rowY+=70;
                Button(panelContent,"找",5,rowY,60,32,()=>FindItem(id));Button(panelContent,"拾1",72,rowY,70,32,()=>Act(()=>PickCatalogItem(id)));
                Button(panelContent,"买1",150,rowY,70,32,()=>Act(()=>Model.Buy(id,PlanStock.Scale)));Button(panelContent,"卖5",230,rowY,70,32,()=>Act(()=>Model.Sell(id,5*PlanStock.Scale)));rowY+=45;
                if(!Model.CanDiscard(id)&&!id.Contains("water")&&!id.StartsWith("lift_")&&!id.StartsWith("mined_")&&id!="reclaimed_structure"&&id!="ceramic_insulator")Row("标记废弃："+item.name,()=>Act(()=>Model.MarkDiscard(id)));
            }
        }
        private void PeoplePanel()
        {
            Line("居民与生活",22,40);payrollStatus=Text(panelContent,"Payroll forecast",PayrollDescription(),5,rowY,374,64,16);rowY+=74;Row("招募1人",()=>Act(Model.Recruit));Row("发薪：偿还全部可支付欠款",()=>Act(()=>Model.PayWages()));Row("按暴动固定目标补薪",()=>Act(()=>Model.PayWages(Math.Max(0,Model.State.riot.target-Model.State.riot.paid))));Row("给所有员工奖金 +10%",()=>Act(()=>Model.Bonus()));
            Row("饮水："+(Model.State.freeWater?"免费":"收费"),()=>Act(()=>Model.State.freeWater=!Model.State.freeWater));Row("食物："+(Model.State.freeFood?"免费":"收费"),()=>Act(()=>Model.State.freeFood=!Model.State.freeFood));
            Row("个人外购："+(Model.State.externalSupply?"开启":"关闭"),()=>Act(()=>Model.State.externalSupply=!Model.State.externalSupply));
            Row("自动补粮补水："+(Model.State.autoSupply?"开启":"关闭"),()=>Act(()=>Model.State.autoSupply=!Model.State.autoSupply));
            Row("每600秒补货预算 "+Model.State.autoSupplyBudget/100m+"钱（+10，超过1000归零）",()=>Act(()=>Model.State.autoSupplyBudget=(Model.State.autoSupplyBudget+1000)%101000));
            Row("生活保留周期：水 "+Model.State.waterReserveCycles+"（点击1—4）",()=>Act(()=>Model.State.waterReserveCycles=Model.State.waterReserveCycles%4+1));Row("生活保留周期：食物 "+Model.State.foodReserveCycles+"（点击1—4）",()=>Act(()=>Model.State.foodReserveCycles=Model.State.foodReserveCycles%4+1));
            Row("工具自动采购："+(Model.State.autoTools?"开启":"关闭")+"（20钱/600秒，预留工资）",()=>Act(()=>Model.State.autoTools=!Model.State.autoTools));
            Row("租金 "+Model.State.rent.ToString("0.0")+"钱/周期（0—2）",()=>Act(()=>Model.State.rent=Model.State.rent>=2?0:Model.State.rent+.5));
            Row("无力付款的饮水救助："+(Model.State.emergencyWater?"开启":"关闭"),()=>Act(()=>Model.State.emergencyWater=!Model.State.emergencyWater));
            Row("居民库存供应："+(Model.State.retailDiscount<1?"半价补贴":"正常价格"),()=>Act(()=>Model.State.retailDiscount=Model.State.retailDiscount<1?1:.5));
            Row("个人代外购补贴预算 "+Model.State.personalSubsidyBudget/100m+"钱/600秒（+5，超过100归零）",()=>Act(()=>Model.State.personalSubsidyBudget=(Model.State.personalSubsidyBudget+500)%10500));
            foreach(string resource in new[]{"water","food","clothes","soap"}){string id=resource;Row(Model.Config.Item(id).name+"零售价 "+Model.RetailPrice(id).ToString("0.##")+"（点击调低）",()=>Act(()=>{double maximum=Model.Config.Item(id).buy;double old=id=="water"?Model.State.waterRetail:id=="food"?Model.State.foodRetail:id=="clothes"?Model.State.clothesRetail:Model.State.soapRetail;if(old<0)old=maximum;double next=old<=0?maximum:Math.Max(0,old-maximum/4);if(id=="water")Model.State.waterRetail=next;else if(id=="food")Model.State.foodRetail=next;else if(id=="clothes")Model.State.clothesRetail=next;else Model.State.soapRetail=next;}));}
            Line("累计：已发工资 "+Model.State.wagesPaid/100m+" / 居民零售 "+Model.State.retailIncome/100m+" / 外销 "+Model.State.externalSaleIncome/100m+" / 玩家服务 "+Model.State.serviceIncome/100m+"\n本周期：补货 "+Model.State.autoSupplyUsed/100m+" / 代购补贴 "+Model.State.personalSubsidyUsed/100m+" / 工具采购 "+Model.State.toolBudgetUsed/100m,15,104);
            foreach(var pair in new[]{("power","修复住房与路灯供电"),("housing","补充住房"),("clothes","恢复衣物供应"),("clean","恢复清洁用品")}){string kind=pair.Item1;Row("承诺："+pair.Item2,()=>Act(()=>Model.Promise(kind)));}
            foreach(var person in Model.Alive)
            {
                var p=person;var live=Text(panelContent,"Worker task #"+p.id,PersonDescription(p),5,rowY,374,10,16);float height=ReadoutHeight(live);live.rectTransform.sizeDelta=new Vector2(374,height);rowY+=height+5;workerReadouts.Add(p.id,live);
                Row("指定到当前选中设施",()=>Act(()=>Model.AssignWorker(p,Model.Site(selectedSite))));
                Row("解除人工指定",()=>Act(()=>p.manualAssignment=0));Row("指定维护当前设施",()=>Act(()=>Model.AssignWorker(p,Model.Site(selectedSite),true)));
                Row("释放维护岗位",()=>Act(()=>{var s=Model.Site(p.maintaining);if(s!=null)s.maintainer=0;p.maintaining=0;}));
            }
        }
        private void TechnologyPanel()
        {
            Line("科技：实际取得材料后瞬间免费解锁",20,66);
            foreach(var t in Model.Config.technologies.Where(t=>t.region_required<=Model.State.region&&(Model.State.technologies.Contains(t.id)||t.acquired_all.Any(Model.State.acquired.Contains)||t.acquired_any.Any(Model.State.acquired.Contains)))){string id=t.id;Row((Model.State.technologies.Contains(id)?"已解锁：":"查看解锁条件：")+t.name,()=>OpenTechnology(id));}
            Line("出售知识永久压低相关商品售价，技术仍可使用。",16,68);
            foreach(var sale in Model.Config.sales.Where(s=>s.requires_technologies.All(Model.State.technologies.Contains))){var id=sale.id;Row((Model.State.soldKnowledge.Contains(id)?"已出售 ":"出售 ")+sale.name+" "+sale.cash+"钱",()=>ConfirmKnowledge(id));}
        }
        private string knowledgeConfirmation;
        private void ConfirmKnowledge(string id){if(knowledgeConfirmation==id){Act(()=>Model.SellKnowledge(id));knowledgeConfirmation=null;}else{knowledgeConfirmation=id;Notice="再次点击确认：相关产品将永久降至最低价。";}}
        private void RegionPanel()
        {
            Line("方形地图与付费地块",22,40);
            Line("整张地图为方形地表，岩脊与狭口划分付费地块。河流跨区连续；深水需走桥，浅滩可涉水。固定来料落点与洞口需保留。",16,120);
            foreach(var r in Model.Config.regions.Where(r=>r.id<=3)){int id=r.id;Line(r.name+" / "+PlanTerrain.CellCount(id)+"格可建设地面 / 开放费 "+r.purchase_cash+"钱",18,65);Row(id<=Model.State.region?"查看已开放洞室":"开放第"+id+"洞室",()=>Act(()=>{if(id>Model.State.region)Model.BuyRegion(id);var center=PlanTerrain.Center(id);mapCamera.transform.position=new Vector3(center.x,center.y,-10);}));if(id<=Model.State.region)Row("定位来料落点",()=>{var a=Model.Config.Region(id).garbage_rectangle;mapCamera.transform.position=new Vector3((a[0]+a[2])/2f,(a[1]+a[3])/2f,-10);mapCamera.orthographicSize=20;});}
            JourneyPanel();
        }
        private void RebuildMenu()
        {
            for(int i=menu.childCount-1;i>=0;i--)Remove(menu.GetChild(i).gameObject);menu.gameObject.SetActive(MenuOpen);if(!MenuOpen)return;
            Text(menu,"Title","喂——出来",30,28,620,50,36);Text(menu,"Version","v"+Application.version+" · 三区域 · 回收与返程工程",32,90,800,42,22);
            Text(menu,"Description",!string.IsNullOrEmpty(Model.State.endingCode)?"":string.IsNullOrEmpty(Model.State.ending)?"垃圾不会停止出现。建起回收产线，照顾居民，安排工资，购买三区土地，重建升降舱返回上层。\n\n使用本地像素素材；已有玩法不以固定小时数或教程锁定扩区。":Model.State.ending+"\n结局不会覆盖失败前手动存档，可读档或开启新游戏。",32,154,830,160,21);
            DrawEndingIllustration();
            Button(menu,"新游戏 · 男主",32,356,200,46,()=>RequestNewGame("male"));Button(menu,"新游戏 · 女主",248,356,200,46,()=>RequestNewGame("female"));Button(menu,"读取存档",464,356,200,46,OpenSaveBrowser);
            if(session&&string.IsNullOrEmpty(Model.State.ending))Button(menu,"返回经营",32,426,200,46,()=>{MenuOpen=false;RebuildPanel();});Button(menu,"退出",680,426,180,46,RequestQuit);
            Button(menu,"操作说明",248,426,200,46,OpenPlayerHelp);
            if(!session&&legacyRoot!=null)Button(menu,"旧版游戏 / 存档",464,426,200,46,OpenLegacyGame);
        }
        private static string CapacityLabel(double value)=>value>=1e9?"不限":value.ToString("0.##");
        private void RefreshInterface()
        {
            if(topStatus==null)return;var st=Model.State;
            menu.gameObject.SetActive(MenuOpen);if(MenuOpen)CloseContext();RefreshSettings();
            topStatus.text=$"第{st.region}区    现金 {st.cash/100m:0.00}";RefreshTimeHud();RefreshGuideHud();RefreshCatalogList();
            noticeText.text=Notice;
            lifeStatus.text=$"居民 {Model.Alive.Count()} / 床位 {Model.Beds-1}  欠薪 {Model.Debt/100m:0.00}  食物 {Model.Stored("food")/(double)PlanStock.Scale:0.##}（保留{Model.Protected("food")/(double)PlanStock.Scale:0}）  水 {Model.Stored("water")/(double)PlanStock.Scale:0.##}（保留{Model.Protected("water")/(double)PlanStock.Scale:0}）\n背包 {st.backpack.Used(Model.Config):0.##}/8   仓库 {Model.Warehouses.Sum(w=>w.Used(Model.Config)):0.##}/{Model.Warehouses.Sum(w=>w.capacity):0.##}   {(st.paused?"已暂停，可规划建设":"经营中")}";
            var water=Model.InspectSupply("water");var food=Model.InspectSupply("food");var pay=Model.InspectPayroll();
            lifeStatus.text+="\n仅仓库覆盖：饮水 "+water.cycles.ToString("0.0")+"轮 / 食物 "+food.cycles.ToString("0.0")+"轮 · "+(pay.scheduled?"发薪 "+Clock(pay.remaining)+"后 / 预计 "+(pay.expected/100m).ToString("0.00")+"钱":"暂无发薪计划");
            RefreshReadouts();RefreshRisks();
            var riot=st.riot;riotText.text=riot.active?$"工人暴动：剩余 {Math.Max(0,riot.deadline-st.seconds):0} 秒\n需补薪 {Math.Max(0,riot.target-riot.paid)/100m:0.00}；生活原因需实际修复并让组织者恢复幸福度。":"";
        }
    }
}
