using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        private readonly List<FacilityInstance> facilities=new List<FacilityInstance>();
        private int[] selectedFacilities={1,2,4,8,0,0,0,0};
        private int nextFacilityId=9;
        private FacilityInstance placementObstacle;
        private int placementIgnoredId;
        private bool placementActorPaths;
        public IReadOnlyList<FacilityInstance> Facilities=>facilities;
        public int ConstructionRevision {get;private set;}
        private static int KindIndex(int kind)
        {
            switch(kind){case 1:return 0;case 2:return 1;case 4:return 2;case 8:return 3;case 16:return 4;case 32:return 5;case 64:return 6;case 128:return 7;default:throw new ArgumentOutOfRangeException(nameof(kind));}
        }
        private void InitializeFacilities()
        {
            AddFacility(1,1,BenchX,BenchY+1);AddFacility(2,2,DismantleX,DismantleY+1);
            AddFacility(4,4,TableX,TableY+1);AddFacility(8,8,GeneratorX,GeneratorY+1);
        }
        private FacilityInstance AddFacility(int id,int kind,int x,int y,int rotation=0)
        {
            var f=new FacilityInstance{Id=id,Kind=kind,X=x,Y=y,Rotation=rotation};
            f.InitializeStocks(Settings.outputCapacity);facilities.Add(f);return f;
        }
        private FacilityInstance Station(int kind)=>facilities.First(f=>f.Id==selectedFacilities[KindIndex(kind)]);
        public FacilityInstance GetFacility(int id)=>facilities.FirstOrDefault(f=>f.Id==id);
        public FacilityInstance SelectedFacility(int kind)=>Station(kind);
        public void SelectNearestFacility()
        {
            if(Interacting)return;
            foreach(int kind in new[]{1,2,4,8})
            {
                var f=facilities.Where(a=>a.Kind==kind&&a.Built).OrderBy(a=>{var p=ServiceCell(a);return Distance(PlayerX,PlayerY,p.x,p.y);}).FirstOrDefault();
                if(f!=null){var side=ServiceCell(f);if(Near(side.x,side.y))SelectRuntimeFacility(f);}
            }
        }
        public ActionResult SelectFacility(int id)
        {
            var f=GetFacility(id);if(f==null)return Fail("设施不存在。");
            if(Interacting)return Fail("先完成或取消当前交互，再切换设施。");
            SelectRuntimeFacility(f);RefreshState();return Ok("已选择"+FacilityName(f.Kind)+" #"+f.Id);
        }
        private void SelectRuntimeFacility(FacilityInstance f){if(!IsGenericKind(f.Kind))selectedFacilities[KindIndex(f.Kind)]=f.Id;}
        private T AtFacility<T>(FacilityInstance f,Func<T> action)
        {
            int index=KindIndex(f.Kind),previous=selectedFacilities[index];selectedFacilities[index]=f.Id;
            try{return action();}finally{selectedFacilities[index]=previous;}
        }
        private void AtFacility(FacilityInstance f,Action action)=>AtFacility(f,()=>{action();return true;});
        private bool ManualAt(int kind)=>manualStationValue==(kind==4?3:kind)&&manualFacilityId==Station(kind).Id;
        public bool PlayerWorksAt(int id)=>manualFacilityId==id&&manualStationValue!=0;
        public static string FacilityName(int kind)
        {
            switch(kind){case 1:return "手工木料地点";case 2:return "拆解地点";case 4:return "工作台";case 8:return "简陋发电机";case 16:return "新住房";case 32:return "路灯";case 64:return "电动木工";case 128:return "自动木工";default:return DesignCatalog.GenericFacility(kind)?.Name??"设施";}
        }
        public static (int width,int height) Footprint(FacilityInstance f)
        {
            if(IsGenericKind(f.Kind)){var def=DesignCatalog.GenericFacility(f.Kind);return f.Rotation%2==0?(def.Width,def.Height):(def.Height,def.Width);}
            return f.Kind==16||f.Kind==128||f.Kind==-1?(3,3):f.Kind==32?(1,1):f.Kind==64?(f.Rotation%2==0?(3,2):(2,3)):(2,2);
        }
        public static bool HasMaintenanceSide(int kind)=>IsWoodMachine(kind)||IsGenericKind(kind)&&DesignCatalog.GenericFacility(kind).Maintainers>0;
        private static readonly string[] InfrastructureKeys={"belt","port","power_line","steam_pipe"};
        private static bool Placeable(int kind)=>new[]{1,2,4,8,16,32,64,128}.Contains(kind)||IsGenericKind(kind)&&DesignCatalog.GenericFacility(kind)!=null&&!InfrastructureKeys.Contains(FacilityKey(kind))&&KindOf(FacilityKey(kind))==kind;
        public static (int x,int y) ServiceCell(FacilityInstance f)
        {
            var size=Footprint(f);
            switch(f.Rotation){case 1:return(f.X-1,f.Y+size.height-1);case 2:return(f.X+size.width-1,f.Y+size.height);case 3:return(f.X+size.width,f.Y);default:return(f.X,f.Y-1);}
        }
        public static bool Contains(FacilityInstance f,int x,int y)
        {var size=Footprint(f);return x>=f.X&&x<f.X+size.width&&y>=f.Y&&y<f.Y+size.height;}
        public FacilityInstance FacilityAtCell(int x,int y)=>facilities.LastOrDefault(f=>(f.Built||f.Blueprint)&&Contains(f,x,y));
        private bool ConstructionCell(int x,int y)=>x>=10&&x<=27&&y>=6&&y<=23||RegionBuildCell(x,y);
        private bool BlocksMovement(int x,int y)=>facilities.Any(f=>f.Id!=placementIgnoredId&&(f.Built||placementObstacle!=null&&!placementActorPaths&&f.Blueprint)&&Contains(f,x,y))||placementObstacle!=null&&Contains(placementObstacle,x,y)||InfrastructureBlocksMovement(x,y);
        public string PlacementError(int kind,int x,int y,int rotation,int ignoreId=0)
        {
            if(!Placeable(kind)||rotation<0||rotation>3)return "设施或方向无效。";
            var candidate=new FacilityInstance{Kind=kind,X=x,Y=y,Rotation=rotation};var size=Footprint(candidate);
            string special=SpecialPlacementError(candidate,ignoreId);if(special!=null)return special;
            for(int cx=x;cx<x+size.width;cx++)for(int cy=y;cy<y+size.height;cy++)
            {
                if(!ConstructionCell(cx,cy)||IsReservedCell(cx,cy))return "设施必须完整位于建设区内。";
                if(facilities.Any(f=>f.Id!=ignoreId&&(f.Built||f.Blueprint)&&Contains(f,cx,cy)))return "与设施或已有蓝图重叠。";
                if(InfrastructureOccupied(cx,cy))return "占地内有传送带或接口，请先拆除。";
                if(wires.Any(w=>w.X==cx&&w.Y==cy))return "占地内有电线，请先拆线。";
                if(CellTicks(cx,cy)>0)return "占地有地面物品，请先搬走。";
                if(CellCoordinate(PlayerX)==cx&&CellCoordinate(PlayerY)==cy||residents.Any(r=>CellCoordinate(r.X)==cx&&CellCoordinate(r.Y)==cy))return "建设位置有人，请先离开。";
            }
            var side=ServiceCell(candidate);
            if(side.x<0||side.y<0||side.x>=Settings.mapWidth||side.y>=Settings.mapHeight||
                facilities.Any(f=>f.Id!=ignoreId&&f.Blueprint&&Contains(f,side.x,side.y)))return "作业面不可到达，请旋转或移动。";
            placementObstacle=candidate;placementIgnoredId=ignoreId;
            try
            {
                if(FindPath(WarehouseX,WarehouseY,side.x,side.y)==null)return "作业面与仓库之间没有通路。";
                foreach(var f in facilities.Where(f=>f.Id!=ignoreId&&(f.Built||f.Blueprint)))
                {var front=ServiceCell(f);if(FindPath(WarehouseX,WarehouseY,front.x,front.y)==null)return "放置会封住已有设施的作业面。";}
                if(HasMaintenanceSide(kind)){var maintenance=MaintenanceCell(candidate);if(FindPath(WarehouseX,WarehouseY,maintenance.x,maintenance.y)==null)return "维护侧与仓库之间没有通路。";}
                foreach(var f in facilities.Where(f=>f.Id!=ignoreId&&f.Built&&HasMaintenanceSide(f.Kind))){var maintenance=MaintenanceCell(f);if(FindPath(WarehouseX,WarehouseY,maintenance.x,maintenance.y)==null)return "放置会封住已有机器的维护侧。";}
                placementActorPaths=true;
                if(FindPath(PlayerX,PlayerY,WarehouseX,WarehouseY)==null||residents.Any(r=>FindPath(r.X,r.Y,WarehouseX,WarehouseY)==null))return "放置会困住角色，请保留通道。";
            }
            finally{placementObstacle=null;placementIgnoredId=0;placementActorPaths=false;}
            return null;
        }
        private bool CanConstruct(int kind,out string reason)
        {
            reason=null;
            if(Interacting)reason="先完成或取消当前交互。";
            else if(RiotActive||Failed)reason="暴动或失败期间无法安排建设。";
            else if(manualStation!=0&&!residents.Any(r=>r.Job==WorkerJob.Idle&&r.Delivery==null))reason="缺少空闲建设人员，请先退出岗位。";
            else if(kind==4&&!TableUnlocked)reason="先取得木板与钢铁零件。";
            else if((kind==8||kind==16)&&!GeneratorUnlocked)reason="先取得钢铁零件。";
            else if(kind==32&&!HasDiscovered(ItemId.LightingModule))reason="先取得照明部件。";
            else if(kind==64&&!ElectricWoodUnlocked)reason="首次取得电机和机器锯片后解锁电动木工。";
            else if(kind==128&&!AutomaticWoodUnlocked)reason="先解锁电动木工，再取得送料组件。";
            else if(IsGenericKind(kind))
            {
                var def=DesignCatalog.GenericFacility(kind);
                if(def.RegionRequired>OwnedRegion)reason="需先购买第"+def.RegionRequired+"区。";
                else if(!FacilityUnlocked(def.Key))reason="尚未解锁："+string.Join("；",DesignCatalog.Technologies.Where(t=>t.Facilities.Contains(def.Key)).Select(t=>t.Name+"（"+TechnologyRequirement(t)+"）"));
            }
            return reason==null;
        }
        private List<(UnitInventory inventory,int token)> ReserveConstruction(ItemAmount[] costs)
        {
            var reserved=new List<(UnitInventory,int)>();
            foreach(var cost in costs)
            {
                int carry=Math.Min(cost.Amount,Carry.Available(cost.Item)),stock=cost.Amount-carry;
                if(Warehouse.Available(cost.Item)<stock){foreach(var r in reserved)r.Item1.Release(r.Item2);return null;}
                if(carry>0)reserved.Add((Carry,Carry.Reserve(new ItemAmount(cost.Item,carry))));
                if(stock>0)reserved.Add((Warehouse,Warehouse.Reserve(new ItemAmount(cost.Item,stock))));
            }
            return reserved;
        }
        public ActionResult PlaceFacility(int kind,int x,int y,int rotation=0)
        {
            if(!CanConstruct(kind,out string reason))return Fail(reason);
            reason=PlacementError(kind,x,y,rotation);if(reason!=null)return Fail(reason);
            var reservations=ReserveConstruction(BuildCost(kind));if(reservations==null)return Fail("背包与仓库建材不足，未扣材料。");
            var f=facilities.FirstOrDefault(a=>a.Kind==kind&&!a.Built&&!a.Blueprint&&(!IsWoodMachine(kind)&&!IsGenericKind(kind)||!a.WasConstructed));
            if(f==null)f=AddFacility(nextFacilityId++,kind,x,y,rotation);
            f.X=x;f.Y=y;f.Rotation=rotation;f.Blueprint=true;f.Vein=VeinAt(f);blueprints[f.Id]=reservations;SelectRuntimeFacility(f);
            ConstructionRevision++;if(!Paused)CompleteBuildings();RefreshState();
            return Ok(Paused?"蓝图已预约，恢复后施工；取消会释放全部材料。":"设施已建成。",f.Id);
        }
        public ActionResult BuildBench()=>BuildSelected(1);
        public ActionResult BuildDismantle()=>BuildSelected(2);
        public ActionResult BuildTable()=>BuildSelected(4);
        public ActionResult BuildGenerator()=>BuildSelected(8);
        private ActionResult BuildSelected(int kind)
        {var f=Station(kind);return f.Built||f.Blueprint?Fail("已建成或规划；可在建造页新增同类设施。"):PlaceFacility(kind,f.X,f.Y,f.Rotation);}
        private void CompleteBuildings()
        {
            if(!facilities.Any(f=>f.Blueprint)&&wirePlans.Count==0&&infrastructurePlans.Count==0)return;
            CompleteInfrastructure();
            CompleteWires();
            foreach(var f in facilities.Where(f=>f.Blueprint).ToArray())
            {
                if(PlacementError(f.Kind,f.X,f.Y,f.Rotation,f.Id)!=null)continue;
                foreach(var r in blueprints[f.Id])if(!r.inventory.Commit(r.token))throw new InvalidOperationException("施工材料预约丢失。");
                blueprints.Remove(f.Id);f.Blueprint=false;f.Built=true;f.WasConstructed=true;geometryRevision++;GroundRevision++;ConstructionRevision++;
            }
            UpdateWarehouseCapacity();
            RecomputePower();
        }
        public ActionResult CancelBlueprint(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Blueprint)return Fail("没有该施工蓝图。");
            foreach(var r in blueprints[id])r.inventory.Release(r.token);
            blueprints.Remove(id);f.Blueprint=false;ConstructionRevision++;return Ok("已取消，材料预约全部释放。");
        }
        public ActionResult CancelBlueprints()
        {foreach(var f in facilities.Where(f=>f.Blueprint).ToArray())CancelBlueprint(f.Id);foreach(var plan in wirePlans.ToArray())CancelWirePlan(plan);foreach(var plan in infrastructurePlans.ToArray())CancelInfrastructurePlan(plan);return Ok("全部蓝图已取消。");}
        public ActionResult MoveFacility(int id,int x,int y,int rotation)
        {
            var f=GetFacility(id);if(f==null||!f.Built)return Fail("只能搬迁已建成的玩家设施。");
            if(!CanConstruct(f.Kind,out string reason))return Fail(reason);
            if(f.X==x&&f.Y==y&&f.Rotation==rotation)return Ok("设施位置未改变。");
            reason=PlacementError(f.Kind,x,y,rotation,id);if(reason!=null)return Fail(reason);
            if(f.Kind<=8){var removed=AtFacility(f,()=>RemoveFacility(f.Kind,false,false));if(!removed.Success)return removed;}
            if(IsWoodMachine(f.Kind)){var removed=EvacuateMachine(f,false,false);if(!removed.Success)return removed;}
            else if(IsGenericKind(f.Kind)){var removed=EvacuateProcess(f,false,false);if(!removed.Success)return removed;}
            DetachFacilityPorts(f.Id);
            f.X=x;f.Y=y;f.Rotation=rotation;f.Built=true;f.Vein=VeinAt(f);UpdateWarehouseCapacity();
            geometryRevision++;GroundRevision++;ConstructionRevision++;RecomputePower();RefreshState();
            return Ok("搬迁完成；内部物品已返仓，溢出留在旧址。已耗原料不重复返还。");
        }
        public ActionResult RemoveFacilityById(int id,bool sell=false)
        {
            var f=GetFacility(id);if(f==null)return Fail("设施不存在。");
            if(f.Blueprint)return CancelBlueprint(id);
            if(f.Kind<=8)return AtFacility(f,()=>RemoveFacility(f.Kind,sell));
            if(IsWoodMachine(f.Kind))return !f.Built?Fail("机器尚未建成。"):EvacuateMachine(f,sell,true);
            if(IsGenericKind(f.Kind))return !f.Built?Fail("设施尚未建成。"):EvacuateProcess(f,sell,true);
            if(!f.Built)return Fail("设施尚未建成。");
            if(f.Kind==16&&residents.Count+1>HousingCapacity-4)return Fail("该住房仍被占用，先提供足够床位。");
            f.Built=false;var costs=BuildCost(f.Kind);
            if(sell)cashCents+=(long)decimal.Round(costs.Sum(p=>ItemCatalog.Price(p.Item).buy*p.Amount)*0.45m*100,0,MidpointRounding.AwayFromZero);
            else foreach(var p in costs)ReturnAtOldSite(p.Item,p.Amount,"",f.X,f.Y);
            geometryRevision++;GroundRevision++;ConstructionRevision++;RecomputePower();return Ok(sell?"设施已出售。":"设施已拆除，材料已返还。");
        }
        public int HousingCapacity=>Settings.housingCapacity+facilities.Count(f=>f.Built&&f.Kind==16)*4;
        private string StockKey(string key)
        {
            int kind=key.StartsWith("wood")||key=="benchTools"?1:key.StartsWith("dismantle")?2:key.StartsWith("table")?4:8;
            int id=Station(kind).Id;return id==kind?key:key+":"+id;
        }
        private Dictionary<string,UnitInventory> InventoryMap()
        {
            var map=new Dictionary<string,UnitInventory>{{"carry",Carry},{"warehouse",Warehouse},{"broken",BrokenTools}};
            foreach(var f in facilities){map[ProcessInputKey(f.Id)]=f.ProcessInput;map[ProcessOutputKey(f.Id)]=f.ProcessOutput;map[ProcessToolKey(f.Id)]=f.ProcessToolSlot;}
            foreach(var f in facilities.Where(f=>!IsGenericKind(f.Kind)))AtFacility(f,()=>{
                if(f.Kind==1){map[StockKey("woodInput")]=WoodInputStock;map[StockKey("woodOutput")]=Output;map[StockKey("benchTools")]=WoodTools;}
                if(f.Kind==2){map[StockKey("dismantleInput")]=DismantleInput;map[StockKey("dismantleOutput")]=DismantleOutput;map[StockKey("dismantleTools")]=DismantleTools;}
                if(f.Kind==4){map[StockKey("tableInput")]=TableInput;map[StockKey("tableOutput")]=TableOutput;}
                if(f.Kind==8)map[StockKey("ash")]=GeneratorResidue;
                if(IsWoodMachine(f.Kind)){map[MachineInputKey(f.Id)]=f.WoodInputStock;map[MachineOutputKey(f.Id)]=f.Output;}
            });
            return map;
        }
        private void RestoreConstruction(SimulationSnapshot s)
        {
            if(s.schema<4||s.facilities==null)return;
            if(s.baselineVersion!=DesignBaseline.Version)throw new ArgumentException("存档数值基线不兼容。");
            if(s.facilities.Select(f=>f.Id).Distinct().Count()!=s.facilities.Count||s.nextFacility<9)throw new ArgumentException("存档设施编号无效。");
            facilities.Clear();
            foreach(var saved in s.facilities)
            {
                if(saved.Id<=0||!Placeable(saved.Kind)||saved.Rotation<0||saved.Rotation>3)throw new ArgumentException("存档设施类型或方向无效。");
                var f=saved.Copy();f.InitializeStocks(Settings.outputCapacity);facilities.Add(f);
            }
            if(s.selectedFacilities==null||s.selectedFacilities.Length<6||s.selectedFacilities.Length>8)throw new ArgumentException("存档缺少设施选择。");
            selectedFacilities=new int[8];Array.Copy(s.selectedFacilities,selectedFacilities,s.selectedFacilities.Length);
            foreach(int kind in new[]{1,2,4,8})if(!facilities.Any(f=>f.Id==selectedFacilities[KindIndex(kind)]&&f.Kind==kind))throw new ArgumentException("存档设施选择无效。");
            nextFacilityId=s.nextFacility;wires.Clear();
            if(s.wires!=null)foreach(var w in s.wires){if(w.X<0||w.Y<0||w.X>=Settings.mapWidth||w.Y>=Settings.mapHeight||wires.Any(a=>a.X==w.X&&a.Y==w.Y))throw new ArgumentException("存档电线无效。");wires.Add(w.Copy());}
            RestoreAutomation(s);
            wireCredit=s.wireCredit;
            wirePlans.Clear();if(s.wirePlans!=null)wirePlans.AddRange(s.wirePlans.Select(p=>p.Copy()));nextWirePlan=s.nextWirePlan<0?s.nextWirePlan:-1;
            if(wireCredit<0||wireCredit>3)throw new ArgumentException("存档电线余料无效。");
        }
        private string VeinAt(FacilityInstance f)
        {
            if(FacilityKey(f.Kind)!="mine"||OwnedRegion<3)return "";
            var size=Footprint(f);
            foreach(var vein in Region(3).Veins)if(vein.Contains(f.X,f.Y)&&vein.Contains(f.X+size.width-1,f.Y+size.height-1))return vein.Key;
            return "";
        }
        private string SpecialPlacementError(FacilityInstance candidate,int ignoreId)
        {
            string key=FacilityKey(candidate.Kind);var size=Footprint(candidate);
            var veins=OwnedRegion>=3?Region(3).Veins:new VeinDef[0];
            if(key=="mine")
            {
                string vein=VeinAt(candidate);
                if(vein=="")return "采掘机必须完整建在第3区的一处矿脉内。";
                if(!surveyedVeins.Contains(vein))return "该矿脉尚未勘探；先在地质勘探站完成对应勘探。";
                if(facilities.Any(f=>f.Id!=ignoreId&&(f.Built||f.Blueprint)&&FacilityKey(f.Kind)=="mine"&&f.Vein==vein))return "每处矿脉只允许一台采掘机。";
                return null;
            }
            for(int cx=candidate.X;cx<candidate.X+size.width;cx++)for(int cy=candidate.Y;cy<candidate.Y+size.height;cy++)
                if(veins.Any(v=>v.Contains(cx,cy)))return "矿脉保留给采掘机，请换个位置。";
            if(key=="lift_launch")
            {
                if(OwnedRegion<3)return "升降舱检修平台位于第3区旧检修竖井。";
                if(candidate.X!=LaunchX||candidate.Y!=LaunchY)return "升降舱检修平台必须建在旧检修竖井口（第3区东南角标记处）。";
                if(facilities.Any(f=>f.Id!=ignoreId&&(f.Built||f.Blueprint)&&FacilityKey(f.Kind)=="lift_launch"))return "只有一处检修平台。";
            }
            return null;
        }
        public const int LaunchX=72,LaunchY=21;
        private void RestoreBlueprintFlags(SimulationSnapshot s)
        {
            if(s.schema>=4)return;
            foreach(var f in facilities)f.Blueprint=(s.pendingBuildings&f.Kind)!=0;
        }
    }
}
