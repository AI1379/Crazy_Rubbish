using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class ConveyorLane
    {
        public int Id,X,Y,Direction,Item=-1,InstanceId,MergeCursor;
        public bool Blueprint,Enabled=true;
        public double Progress,PowerRatio;
        [NonSerialized] public double StepPowerRatio;
        public ConveyorLane Copy()=>(ConveyorLane)MemberwiseClone();
    }
    [Serializable] public sealed class ConveyorPort
    {
        public int Id,OwnerId,X,Y,Facing,Filter=-1,ItemCursor;
        public bool Output,Blueprint,Enabled=true;
        public ConveyorPort Copy()=>(ConveyorPort)MemberwiseClone();
    }
    [Serializable] public sealed class InfrastructurePlan
    {
        public int Id,Pairs,CreditUsed;public List<int> LaneIds=new List<int>(),PortIds=new List<int>();
        public InfrastructurePlan Copy()=>new InfrastructurePlan{Id=Id,Pairs=Pairs,CreditUsed=CreditUsed,LaneIds=new List<int>(LaneIds),PortIds=new List<int>(PortIds)};
    }
    public sealed partial class PrototypeSimulation
    {
        private readonly List<ConveyorLane> conveyors=new List<ConveyorLane>();
        private readonly List<ConveyorPort> ports=new List<ConveyorPort>();
        private readonly List<InfrastructurePlan> infrastructurePlans=new List<InfrastructurePlan>();
        private int nextConveyorId=1,nextPortId=1,nextInfrastructurePlan=-1000000000,beltCredit,portTurn;
        private (int x,int y)? portPlacementObstacle;
        private int portPlacementIgnore;
        private static readonly int[] ConveyorDx={1,0,-1,0},ConveyorDy={0,1,0,-1};
        public IReadOnlyList<ConveyorLane> Conveyors=>conveyors;
        public IReadOnlyList<ConveyorPort> ConveyorPorts=>ports;
        public int BeltCredit=>beltCredit;
        public ConveyorLane GetConveyor(int id)=>conveyors.FirstOrDefault(b=>b.Id==id);
        public ConveyorPort GetConveyorPort(int id)=>ports.FirstOrDefault(p=>p.Id==id);
        public static (int x,int y) ConveyorNext(ConveyorLane b)=>(b.X+ConveyorDx[b.Direction],b.Y+ConveyorDy[b.Direction]);
        private bool InfrastructureOccupied(int x,int y)=>conveyors.Any(b=>b.X==x&&b.Y==y)||ports.Any(p=>p.X==x&&p.Y==y);
        private bool InfrastructureBlocksMovement(int x,int y)=>ports.Any(p=>p.Id!=portPlacementIgnore&&(!p.Blueprint||!placementActorPaths&&(portPlacementObstacle.HasValue||placementObstacle!=null))&&p.X==x&&p.Y==y)||portPlacementObstacle.HasValue&&portPlacementObstacle.Value==(x,y);
        private bool LogisticsLand(int x,int y)=>IsOwnedGround(x,y)||x>=9&&x<=21&&y>=1&&y<=5;
        private bool InfrastructureCellAllowed(int x,int y)=>x>=0&&y>=0&&x<Settings.mapWidth&&y<Settings.mapHeight&&LogisticsLand(x,y)&&!IsReservedCell(x,y)&&!facilities.Any(f=>(f.Built||f.Blueprint)&&Contains(f,x,y));
        private string InfrastructurePermission()
        {
            if(Failed||RiotActive)return "暴动或失败期间不能施工。";
            if(Interacting)return "先结束当前交互。";
            if(!ConveyorsUnlocked)return "首次取得电机与钢铁零件后解锁输送。";
            if(manualStation!=0&&!residents.Any(r=>r.Job==WorkerJob.Idle&&r.Delivery==null))return "先退出岗位或提供空闲建设人员。";
            return null;
        }
        public string ConveyorPlacementError(IReadOnlyList<ConveyorLane> path)
        {
            if(path==null||path.Count==0)return "请选择传送带起点与终点。";
            var seen=new HashSet<(int,int,int)>();
            for(int i=0;i<path.Count;i++)
            {
                var b=path[i];if(b.Direction<0||b.Direction>3||!InfrastructureCellAllowed(b.X,b.Y))return "传送带须位于产权地或社区物流通道，不能穿过设施。";
                if(i+1<path.Count&&ConveyorNext(b)!=(path[i+1].X,path[i+1].Y))return "传送带必须按箭头逐格正交连接。";
                if(ports.Any(p=>p.X==b.X&&p.Y==b.Y))return "接口格不能同时放传送带。";
                if(!seen.Add((b.X,b.Y,b.Direction)))return "路径重复。";
                var existing=conveyors.Where(a=>a.X==b.X&&a.Y==b.Y).ToArray();
                if(existing.Any(a=>a.Direction==b.Direction))continue;
                if(existing.Length>=2||existing.Any(a=>a.Direction%2==b.Direction%2))return "同格只允许两条正交的独立直行带。";
                if(existing.Length>0&&i>0&&(path[i-1].Direction%2!=b.Direction%2))return "交叉格不能同时转弯；两条直行线各自独立。";
                if(CellTicks(b.X,b.Y)>0)return "先搬走该格的地面物品。";
                if(CellCoordinate(PlayerX)==b.X&&CellCoordinate(PlayerY)==b.Y||residents.Any(r=>CellCoordinate(r.X)==b.X&&CellCoordinate(r.Y)==b.Y))return "建设位置有人。";
                if(i+1<path.Count&&ConveyorNext(b)!=(path[i+1].X,path[i+1].Y))return "传送带必须按箭头逐格正交连接。";
            }
            return null;
        }
        public List<ConveyorLane> FindConveyorRoute(int sx,int sy,int tx,int ty,int finalDirection=0)
        {
            bool Allowed(int x,int y)=>InfrastructureCellAllowed(x,y)&&!ports.Any(p=>p.X==x&&p.Y==y)&&CellTicks(x,y)==0&&!(CellCoordinate(PlayerX)==x&&CellCoordinate(PlayerY)==y)&&!residents.Any(r=>CellCoordinate(r.X)==x&&CellCoordinate(r.Y)==y)&&!facilities.Any(f=>f.Built&&IsWoodMachine(f.Kind)&&(ServiceCell(f)==(x,y)||MaintenanceCell(f)==(x,y)));
            bool CanLeave(int x,int y,int direction,int incoming)
            {
                if(direction<0||direction>3||incoming>=0&&direction==(incoming+2)%4)return false;
                var existing=conveyors.Where(b=>b.X==x&&b.Y==y).ToArray();
                if(existing.Length==0)return true;
                if(existing.Length==2)return existing.Any(b=>b.Direction==direction)&&(incoming<0||incoming==direction);
                if(existing[0].Direction==direction)return true;
                return existing[0].Direction%2!=direction%2&&(incoming<0||incoming==direction);
            }
            if(!Allowed(sx,sy)||!Allowed(tx,ty)||finalDirection>3)return null;
            int startDirection=ports.Where(p=>p.Output&&p.X+ConveyorDx[p.Facing]==sx&&p.Y+ConveyorDy[p.Facing]==sy).Select(p=>p.Facing).DefaultIfEmpty(-1).First();
            var start=(sx,sy,startDirection);
            var queue=new Queue<(int x,int y,int incoming)>();var previous=new Dictionary<(int,int,int),(int,int,int)>();
            queue.Enqueue(start);previous[start]=start;
            while(queue.Count>0)
            {
                var at=queue.Dequeue();int endDirection=finalDirection<0?(at.incoming<0?0:at.incoming):finalDirection;
                if((at.x,at.y)==(tx,ty)&&CanLeave(at.x,at.y,endDirection,at.incoming))
                {
                    var points=new List<(int x,int y,int incoming)>();var cur=at;while(cur!=start){points.Add(cur);cur=previous[cur];}points.Add(cur);points.Reverse();
                    var result=new List<ConveyorLane>();
                    for(int i=0;i<points.Count;i++)
                    {int direction=i+1==points.Count?endDirection:points[i+1].incoming;result.Add(new ConveyorLane{X=points[i].x,Y=points[i].y,Direction=direction});}
                    return ConveyorPlacementError(result)==null?result:null;
                }
                for(int d=0;d<4;d++)
                {if(!CanLeave(at.x,at.y,d,at.incoming))continue;var next=(at.x+ConveyorDx[d],at.y+ConveyorDy[d],d);if(Allowed(next.Item1,next.Item2)&&!previous.ContainsKey(next)){previous[next]=at;queue.Enqueue(next);}}
            }
            return null;
        }
        public ActionResult PlaceConveyor(IReadOnlyList<ConveyorLane> path)
        {
            string error=InfrastructurePermission()??ConveyorPlacementError(path);if(error!=null)return Fail(error);
            var added=path.Where(b=>!conveyors.Any(a=>a.X==b.X&&a.Y==b.Y&&a.Direction==b.Direction)).ToArray();if(added.Length==0)return Ok("已有同向传送带，无需重复扣料。");
            int used=Math.Min(beltCredit,added.Length),pairs=(added.Length-used+1)/2;
            var reserved=ReserveConstruction(pairs==0?Array.Empty<ItemAmount>():new[]{new ItemAmount(ItemId.Plank,pairs),new ItemAmount(ItemId.SteelPart,pairs)});
            if(reserved==null)return Fail("建材不足：木板1 + 钢铁零件1可铺2格。");
            var plan=new InfrastructurePlan{Id=nextInfrastructurePlan--,Pairs=pairs,CreditUsed=used};beltCredit-=used;
            foreach(var b in added){var lane=new ConveyorLane{Id=nextConveyorId++,X=b.X,Y=b.Y,Direction=b.Direction,Blueprint=true};conveyors.Add(lane);plan.LaneIds.Add(lane.Id);}
            infrastructurePlans.Add(plan);blueprints[plan.Id]=reserved;ConstructionRevision++;
            if(!Paused)CompleteInfrastructure();RecomputePower();return Ok(Paused?"输送蓝图已预约，恢复后铺设。":"传送带已铺设，每格缓存一个实体。",added.Length);
        }
        private FacilityInstance PortOwner(int id)=>id==-1?new FacilityInstance{Id=-1,Kind=-1,X=11,Y=2,Built=true}:GetFacility(id);
        private int PortFacing(FacilityInstance owner,int x,int y)
        {
            var size=Footprint(owner);
            if(x==owner.X-1&&y>=owner.Y&&y<owner.Y+size.height)return 2;
            if(x==owner.X+size.width&&y>=owner.Y&&y<owner.Y+size.height)return 0;
            if(y==owner.Y-1&&x>=owner.X&&x<owner.X+size.width)return 3;
            if(y==owner.Y+size.height&&x>=owner.X&&x<owner.X+size.width)return 1;
            return -1;
        }
        public string PortPlacementError(int ownerId,int x,int y,int ignoreId=0)
        {
            var owner=PortOwner(ownerId);
            if(owner==null||!owner.Built||!(ownerId==-1||owner.Kind<=8||IsWoodMachine(owner.Kind)))return "请选择有实际物品缓冲的已建成设施或旧仓库。";
            if(PortFacing(owner,x,y)<0)return "接口必须贴在所属设施的外沿，不能在角格或机身内。";
            if(!InfrastructureCellAllowed(x,y)||conveyors.Any(b=>b.X==x&&b.Y==y)||ports.Any(p=>p.Id!=ignoreId&&p.X==x&&p.Y==y))return "接口位置被设施、传送带或其他接口占用。";
            if(ports.Count(p=>p.Id!=ignoreId&&p.OwnerId==ownerId)>=DesignBaseline.PortLimit(ownerId==-1?-1:owner.Kind))return "该设施接口数量已达上限。";
            if(CellTicks(x,y)>0)return "先搬走接口位置的地面物品。";
            if(CellCoordinate(PlayerX)==x&&CellCoordinate(PlayerY)==y||residents.Any(r=>CellCoordinate(r.X)==x&&CellCoordinate(r.Y)==y))return "接口位置有人。";
            var service=ownerId==-1?(WarehouseX,WarehouseY):ServiceCell(owner);
            if(service==(x,y)||IsWoodMachine(owner.Kind)&&MaintenanceCell(owner)==(x,y))return "请保留人员作业面与维护侧。";
            portPlacementObstacle=(x,y);portPlacementIgnore=ignoreId;
            try
            {
                foreach(var f in facilities.Where(f=>f.Built||f.Blueprint))
                {var s=ServiceCell(f);if(FindPath(WarehouseX,WarehouseY,s.x,s.y)==null)return "接口会封住设施作业面。";if(IsWoodMachine(f.Kind)){s=MaintenanceCell(f);if(FindPath(WarehouseX,WarehouseY,s.x,s.y)==null)return "接口会封住维护侧。";}}
                placementActorPaths=true;
                if(FindPath(PlayerX,PlayerY,WarehouseX,WarehouseY)==null||residents.Any(r=>FindPath(r.X,r.Y,WarehouseX,WarehouseY)==null))return "接口会困住角色。";
            }
            finally{portPlacementObstacle=null;portPlacementIgnore=0;placementActorPaths=false;}
            return null;
        }
        public ActionResult PlacePort(int ownerId,int x,int y,bool output,int filter=-1)
        {
            string error=InfrastructurePermission()??PortPlacementError(ownerId,x,y);if(error!=null)return Fail(error);
            if(filter< -1||filter>=Enum.GetValues(typeof(ItemId)).Length)return Fail("过滤物品无效。");
            if(output&&ownerId==-1&&filter<0)filter=(int)ItemId.WasteWood;
            var reserved=ReserveConstruction(new[]{new ItemAmount(ItemId.SteelPart,1)});if(reserved==null)return Fail("接口需要钢铁零件1。");
            var owner=PortOwner(ownerId);var port=new ConveyorPort{Id=nextPortId++,OwnerId=ownerId,X=x,Y=y,Facing=PortFacing(owner,x,y),Output=output,Filter=filter,Blueprint=true};ports.Add(port);
            var plan=new InfrastructurePlan{Id=nextInfrastructurePlan--};plan.PortIds.Add(port.Id);infrastructurePlans.Add(plan);blueprints[plan.Id]=reserved;ConstructionRevision++;
            if(!Paused)CompleteInfrastructure();return Ok(Paused?"接口蓝图已预约。":"独立接口已建成，每个接口只连接外侧一条带。",port.Id);
        }
        public ActionResult ConfigurePort(int id,int filter,bool enabled)
        {
            var p=GetConveyorPort(id);if(p==null||filter< -1||filter>=Enum.GetValues(typeof(ItemId)).Length)return Fail("接口或过滤物品无效。");
            p.Filter=filter;p.Enabled=enabled;ConstructionRevision++;return Ok(enabled?"接口过滤已更新。":"接口已停用，物品保留在原处。");
        }
        public ActionResult SetConveyorEnabled(int id,bool enabled)
        {var b=GetConveyor(id);if(b==null)return Fail("没有该传送带。");b.Enabled=enabled;ConstructionRevision++;RecomputePower();return Ok(enabled?"传送带已启用。":"传送带已停止，实物与进度保留。");}
        private void ReturnInfrastructureItem(ItemId item,int count,string from,int x,int y)
        {
            if(!IsOwnedGround(x,y))
            {
                var cell=(from py in Enumerable.Range(0,Settings.mapHeight) from px in Enumerable.Range(0,Settings.mapWidth) where IsOwnedGround(px,py)&&!Occupied(px,py) orderby CellTicks(px,py),Distance(px,py,x,y),py,px select(px,py)).First();x=cell.px;y=cell.py;
            }
            ReturnAtOldSite(item,count,from,x,y);
        }
        private void NormalizeBeltCredit(int x,int y)
        {while(beltCredit>=2){ReturnInfrastructureItem(ItemId.Plank,1,"",x,y);ReturnInfrastructureItem(ItemId.SteelPart,1,"",x,y);beltCredit-=2;}}
        private bool InfrastructurePlanClear(InfrastructurePlan plan)
        {
            foreach(int id in plan.LaneIds){var b=GetConveyor(id);if(b==null||CellTicks(b.X,b.Y)>0||CellCoordinate(PlayerX)==b.X&&CellCoordinate(PlayerY)==b.Y||residents.Any(r=>CellCoordinate(r.X)==b.X&&CellCoordinate(r.Y)==b.Y))return false;}
            foreach(int id in plan.PortIds){var p=GetConveyorPort(id);if(p==null||PortPlacementError(p.OwnerId,p.X,p.Y,p.Id)!=null)return false;}
            return true;
        }
        private void CompleteInfrastructure()
        {
            foreach(var plan in infrastructurePlans.ToArray())
            {
                if(!InfrastructurePlanClear(plan))continue;
                foreach(var r in blueprints[plan.Id])if(!r.inventory.Commit(r.token))throw new InvalidOperationException("输送建材预约丢失。");
                foreach(int id in plan.LaneIds)GetConveyor(id).Blueprint=false;foreach(int id in plan.PortIds)GetConveyorPort(id).Blueprint=false;
                beltCredit+=plan.CreditUsed+plan.Pairs*2-plan.LaneIds.Count;NormalizeBeltCredit(10,6);
                blueprints.Remove(plan.Id);infrastructurePlans.Remove(plan);ConstructionRevision++;geometryRevision++;GroundRevision++;
            }
        }
        private void CancelInfrastructurePlan(InfrastructurePlan plan)
        {
            foreach(var r in blueprints[plan.Id])r.inventory.Release(r.token);
            conveyors.RemoveAll(b=>plan.LaneIds.Contains(b.Id));ports.RemoveAll(p=>plan.PortIds.Contains(p.Id));beltCredit+=plan.CreditUsed;NormalizeBeltCredit(10,6);
            blueprints.Remove(plan.Id);infrastructurePlans.Remove(plan);ConstructionRevision++;
        }
        public ActionResult RemoveConveyor(int id)
        {
            var b=GetConveyor(id);if(b==null||Failed||RiotActive)return Fail("当前不能拆除该传送带。");
            var plan=infrastructurePlans.FirstOrDefault(p=>p.LaneIds.Contains(id));if(plan!=null){CancelInfrastructurePlan(plan);return Ok("输送蓝图已取消，建材预约释放。");}
            if(b.Item>=0)ReturnInfrastructureItem((ItemId)b.Item,1,"belt:"+b.Id,b.X,b.Y);
            conveyors.Remove(b);beltCredit++;NormalizeBeltCredit(b.X,b.Y);ConstructionRevision++;RecomputePower();return Ok("传送带已拆除，携带实物已返仓或留在合法地面。");
        }
        public ActionResult RemovePort(int id)
        {
            var p=GetConveyorPort(id);if(p==null||Failed||RiotActive)return Fail("当前不能拆除该接口。");
            var plan=infrastructurePlans.FirstOrDefault(a=>a.PortIds.Contains(id));if(plan!=null){CancelInfrastructurePlan(plan);return Ok("接口蓝图已取消。");}
            ports.Remove(p);ReturnInfrastructureItem(ItemId.SteelPart,1,"",p.X,p.Y);ConstructionRevision++;geometryRevision++;GroundRevision++;return Ok("接口已拆除，钢铁零件已返还。");
        }
        private void DetachFacilityPorts(int id)
        {
            foreach(var plan in infrastructurePlans.Where(p=>p.PortIds.Any(pid=>GetConveyorPort(pid)?.OwnerId==id)).ToArray())CancelInfrastructurePlan(plan);
            foreach(var p in ports.Where(p=>p.OwnerId==id)){p.OwnerId=0;p.Enabled=false;}ConstructionRevision++;
        }
        private void RestoreAutomation(SimulationSnapshot s)
        {
            AutomaticMachineStaffing=s.schema>=5?s.autoMachineStaffing:true;nextMachineStaffing=s.nextMachineStaffing;
            ConveyorDeliveredItems=s.conveyorDelivered;
            conveyors.Clear();ports.Clear();infrastructurePlans.Clear();if(s.schema<5)return;
            if(s.conveyors!=null)conveyors.AddRange(s.conveyors.Select(b=>b.Copy()));if(s.ports!=null)ports.AddRange(s.ports.Select(p=>p.Copy()));if(s.infrastructurePlans!=null)infrastructurePlans.AddRange(s.infrastructurePlans.Select(p=>p.Copy()));
            nextConveyorId=s.nextConveyor;nextPortId=s.nextPort;nextInfrastructurePlan=s.nextInfrastructurePlan;beltCredit=s.beltCredit;portTurn=s.portTurn;
            if(nextConveyorId<1||nextPortId<1||beltCredit<0||beltCredit>1||conveyors.Select(b=>b.Id).Distinct().Count()!=conveyors.Count||ports.Select(p=>p.Id).Distinct().Count()!=ports.Count)throw new ArgumentException("输送存档编号或余料无效。");
            foreach(var b in conveyors)if(b.Id<=0||b.Direction<0||b.Direction>3||b.X<0||b.Y<0||b.X>=Settings.mapWidth||b.Y>=Settings.mapHeight||b.Item< -1||b.Item>=Enum.GetValues(typeof(ItemId)).Length||double.IsNaN(b.Progress)||b.Progress<0||b.Progress>1.00001)throw new ArgumentException("传送带实体或进度无效。");
            foreach(var f in facilities.Where(f=>IsWoodMachine(f.Kind)))
            {
                if(double.IsNaN(f.Durability)||f.Durability<0||f.Durability>100||f.MachinePhase<0||f.MachinePhase>2||double.IsNaN(f.HumanProgress)||double.IsNaN(f.MachineProgress)||f.HumanProgress<0||f.HumanProgress>10.00001||f.MachineProgress<0||f.MachineProgress>15.00001||f.MachineActive&&f.MachineToken<=0)throw new ArgumentException("机器耐久、预约或阶段无效。");
                if(f.Built)f.WasConstructed=true;
            }
        }
        private void ValidateAutomationCargo()
        {
            var claimed=new HashSet<int>();
            foreach(var b in conveyors)
            {
                if(b.Item<0){if(b.InstanceId!=0)throw new ArgumentException("空带不能带有实例。");continue;}
                var item=(ItemId)b.Item;bool instanceItem=ConveyorNeedsInstance(item);
                if(instanceItem&&(b.InstanceId<=0||!claimed.Add(b.InstanceId)||!tools.Any(t=>t.Id==b.InstanceId&&t.Item==item&&t.Location=="belt:"+b.Id)))throw new ArgumentException("带上实例与工具/设备位置不一致。");
                if(!instanceItem&&b.InstanceId!=0)throw new ArgumentException("普通物品不能冒充工具实例。");
            }
            if(tools.Any(t=>t.Location.StartsWith("belt:")&&!claimed.Contains(t.Id)))throw new ArgumentException("存在没有对应实物的输送实例。");
        }
    }
}
