using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class WireCell
    { public int X,Y;public bool Blueprint;public WireCell Copy()=>(WireCell)MemberwiseClone(); }
    [Serializable] public sealed class WirePlan
    {public int Id,CreditUsed,Units;public List<WireCell> Cells;public WirePlan Copy()=>new WirePlan{Id=Id,CreditUsed=CreditUsed,Units=Units,Cells=Cells.Select(w=>w.Copy()).ToList()};}
    public sealed class PowerNetwork
    {
        public int Id;public double Output,Requested,Ratio;
        public readonly List<int> FacilityIds=new List<int>();
        public readonly List<WireCell> Wires=new List<WireCell>();
    }
    public sealed partial class PrototypeSimulation
    {
        private readonly List<WireCell> wires=new List<WireCell>();
        private readonly List<PowerNetwork> powerNetworks=new List<PowerNetwork>();
        private int wireCredit;
        private readonly List<WirePlan> wirePlans=new List<WirePlan>();
        private int nextWirePlan=-1,powerTopologyRevision=-1;
        public IReadOnlyList<WireCell> Wires=>wires;
        public IReadOnlyList<PowerNetwork> PowerNetworks=>powerNetworks;
        public int WireCredit=>wireCredit;
        public double HousingPowerRatio {get;private set;}
        public double CommunityLampPowerRatio {get;private set;}
        public bool HousingPowered=>HousingPowerRatio>=DesignBaseline.HousingValidRatio;
        public bool CommunityLampPowered=>CommunityLampPowerRatio>=DesignBaseline.LampValidRatio;
        public double RequestedPower=>powerNetworks.Sum(n=>n.Requested);
        public ActionResult ConnectCommunityPower()
        {
            var source=Station(8);if(!source.Built)return Fail("先建设发电机。");
            var front=ServiceCell(source);var path=FindWireRoute(front.x,front.y,18,5);
            if(path==null)return Fail("发电机到旧住房没有可用接线路径，请手动铺线。");
            if(!path.Any(w=>w.X==19&&w.Y==5))path.Add(new WireCell{X=19,Y=5});
            return PlaceWire(path);
        }
        private bool WireAllowed(int x,int y)=>x>=0&&y>=0&&x<Settings.mapWidth&&y<Settings.mapHeight&&
            !InBuilding(x,y,11,2,3,3)&&!InBuilding(x,y,16,2,3,3)&&!InBuilding(x,y,2,20)&&!(x==20&&y==5)&&
            !facilities.Any(f=>(f.Built||f.Blueprint)&&Contains(f,x,y));
        public string WirePlacementError(IReadOnlyList<WireCell> path)
        {
            if(path==null||path.Count==0)return "请选择电线起点与终点。";
            var seen=new HashSet<(int,int)>();
            for(int i=0;i<path.Count;i++)
            {
                var w=path[i];if(!WireAllowed(w.X,w.Y))return "电线不能穿过设施、洞口或地图边界。";
                if(!seen.Add((w.X,w.Y)))return "电线路径重复。";
                if(i>0&&Math.Abs(w.X-path[i-1].X)+Math.Abs(w.Y-path[i-1].Y)!=1)return "电线必须逐格正交连接。";
            }
            return null;
        }
        public List<WireCell> FindWireRoute(int sx,int sy,int tx,int ty)
        {
            if(!WireAllowed(sx,sy)||!WireAllowed(tx,ty))return null;
            var q=new Queue<(int,int)>();var previous=new Dictionary<(int,int),(int,int)>();
            q.Enqueue((sx,sy));previous[(sx,sy)]=(sx,sy);
            while(q.Count>0)
            {
                var at=q.Dequeue();if(at==(tx,ty))
                {
                    var result=new List<WireCell>();var p=at;
                    while(p!=(sx,sy)){result.Add(new WireCell{X=p.Item1,Y=p.Item2});p=previous[p];}
                    result.Add(new WireCell{X=sx,Y=sy});result.Reverse();return result;
                }
                foreach(var next in new[]{(at.Item1+1,at.Item2),(at.Item1-1,at.Item2),(at.Item1,at.Item2+1),(at.Item1,at.Item2-1)})
                    if(WireAllowed(next.Item1,next.Item2)&&!previous.ContainsKey(next)){previous[next]=at;q.Enqueue(next);}
            }
            return null;
        }
        public ActionResult PlaceWire(IReadOnlyList<WireCell> path)
        {
            if(RiotActive||Failed)return Fail("暴动或失败期间无法铺设电线。");
            if(!HasDiscovered(ItemId.RecoveredWire))return Fail("先取得可用电线。");
            if(manualStation!=0&&!residents.Any(r=>r.Job==WorkerJob.Idle&&r.Delivery==null))return Fail("先退出岗位或提供空闲建设人员。");
            string error=WirePlacementError(path);if(error!=null)return Fail(error);
            var added=path.Where(w=>!wires.Any(p=>p.X==w.X&&p.Y==w.Y)).ToArray();
            int units=Math.Max(0,(added.Length-wireCredit+3)/4);
            var reserved=ReserveConstruction(units==0?Array.Empty<ItemAmount>():new[]{new ItemAmount(ItemId.RecoveredWire,units)});
            if(reserved==null)return Fail("电线不足：每1件铺4格，已有线不重复收费。");
            if(Paused&&added.Length>0)
            {
                int used=Math.Min(wireCredit,added.Length);wireCredit-=used;
                var plan=new WirePlan{Id=nextWirePlan--,CreditUsed=used,Units=units,Cells=added.Select(w=>new WireCell{X=w.X,Y=w.Y,Blueprint=true}).ToList()};
                wirePlans.Add(plan);blueprints[plan.Id]=reserved;wires.AddRange(plan.Cells.Select(w=>w.Copy()));ConstructionRevision++;
                return Ok("电线蓝图已预约；恢复后接通，取消会释放材料。",added.Length);
            }
            foreach(var r in reserved)r.inventory.Commit(r.token);
            wireCredit+=units*4-added.Length;wires.AddRange(added.Select(w=>w.Copy()));
            ConstructionRevision++;RecomputePower();return Ok($"已铺设 {added.Length} 格电线，余料 {wireCredit}/4。",added.Length);
        }
        private void NormalizeWireCredit(int x,int y)
        {while(wireCredit>=4){ReturnAtOldSite(ItemId.RecoveredWire,1,"",x,y);wireCredit-=4;}}
        private void CompleteWires()
        {
            foreach(var plan in wirePlans.ToArray())
            {
                foreach(var r in blueprints[plan.Id])if(!r.inventory.Commit(r.token))throw new InvalidOperationException("电线材料预约丢失。");
                foreach(var w in plan.Cells)wires.First(a=>a.X==w.X&&a.Y==w.Y).Blueprint=false;
                wireCredit+=plan.CreditUsed+plan.Units*4-plan.Cells.Count;
                NormalizeWireCredit(plan.Cells[0].X,plan.Cells[0].Y);blueprints.Remove(plan.Id);wirePlans.Remove(plan);ConstructionRevision++;
            }
        }
        private void CancelWirePlan(WirePlan plan)
        {
            foreach(var r in blueprints[plan.Id])r.inventory.Release(r.token);
            foreach(var w in plan.Cells)wires.RemoveAll(a=>a.X==w.X&&a.Y==w.Y);
            wireCredit+=plan.CreditUsed;NormalizeWireCredit(plan.Cells[0].X,plan.Cells[0].Y);
            blueprints.Remove(plan.Id);wirePlans.Remove(plan);ConstructionRevision++;
        }
        public ActionResult RemoveWire(int x,int y)
        {
            if(Failed||RiotActive)return Fail("当前无法拆线。");
            var plan=wirePlans.FirstOrDefault(p=>p.Cells.Any(w=>w.X==x&&w.Y==y));
            if(plan!=null){CancelWirePlan(plan);return Ok("该段电线蓝图已取消，材料预约已释放。");}
            int removed=wires.RemoveAll(w=>w.X==x&&w.Y==y);if(removed==0)return Fail("该格没有电线。");
            wireCredit+=removed;
            while(wireCredit>=4){ReturnAtOldSite(ItemId.RecoveredWire,1,"",x,y);wireCredit-=4;}
            ConstructionRevision++;RecomputePower();return Ok("电线已拆除；余料累计满4格返还1件。");
        }
        public void RecomputePower()
        {
            if(powerTopologyRevision==ConstructionRevision){RefreshPowerRatios();return;}
            powerTopologyRevision=ConstructionRevision;
            powerNetworks.Clear();HousingPowerRatio=CommunityLampPowerRatio=0;
            foreach(var f in facilities){f.PowerRatio=0;f.PowerRequested=0;}
            // A device is one electrical node. Adjacent wires connect to its perimeter;
            // unrelated manual workstations never bridge two electrical circuits.
            var electric=facilities.Where(f=>f.Built&&(f.Kind==8||f.Kind==16||f.Kind==32)).ToList();
            var oldHousing=new FacilityInstance{Id=-1,Kind=16,X=16,Y=2,Built=true};
            var oldLamp=new FacilityInstance{Id=-2,Kind=32,X=20,Y=5,Built=true};
            electric.Add(oldHousing);electric.Add(oldLamp);
            var live=wires.Where(w=>!w.Blueprint).ToList();
            var cells=new Dictionary<(int,int),int>();int total=live.Count+electric.Count;
            var parent=Enumerable.Range(0,total).ToArray();
            Func<int,int> root=null;root=i=>parent[i]==i?i:(parent[i]=root(parent[i]));
            Action<int,int> union=(a,b)=>{a=root(a);b=root(b);if(a!=b)parent[b]=a;};
            for(int i=0;i<live.Count;i++)cells[(live[i].X,live[i].Y)]=i;
            for(int i=0;i<live.Count;i++)
            {
                var w=live[i];
                foreach(var near in new[]{(w.X+1,w.Y),(w.X-1,w.Y),(w.X,w.Y+1),(w.X,w.Y-1)})
                    if(cells.TryGetValue(near,out int other))union(i,other);
            }
            for(int i=0;i<electric.Count;i++)
            {
                var f=electric[i];var size=Footprint(f);
                for(int x=f.X-1;x<=f.X+size.width;x++)for(int y=f.Y-1;y<=f.Y+size.height;y++)
                {
                    bool perimeter=(x>=f.X&&x<f.X+size.width&&(y==f.Y-1||y==f.Y+size.height))||
                        (y>=f.Y&&y<f.Y+size.height&&(x==f.X-1||x==f.X+size.width));
                    if(perimeter&&cells.TryGetValue((x,y),out int wire))union(live.Count+i,wire);
                }
            }
            var groups=new Dictionary<int,PowerNetwork>();
            for(int i=0;i<total;i++)
            {
                int group=root(i);if(!groups.TryGetValue(group,out var n)){n=new PowerNetwork{Id=groups.Count+1};groups[group]=n;powerNetworks.Add(n);}
                if(i<live.Count)n.Wires.Add(live[i]);else n.FacilityIds.Add(electric[i-live.Count].Id);
            }
            RefreshPowerRatios();
        }
        private void RefreshPowerRatios()
        {
            foreach(var f in facilities){f.PowerRequested=0;f.PowerRatio=0;}
            foreach(var n in powerNetworks)
            {
                n.Output=n.Requested=0;
                foreach(int id in n.FacilityIds)
                {
                    if(id<0){n.Requested+=id==-1?0.2:0.1;continue;}
                    var f=GetFacility(id);
                    if(f.Kind==8){if((f.fuelRemaining>0||f.fuelWood+f.fuelChips>0)&&(f.equivalentHeat<20-1e-6||f.GeneratorResidue.FreeTicks>=20))n.Output+=DesignBaseline.GeneratorPower;}
                    else n.Requested+=f.Kind==16?0.2:0.1;
                }
                n.Ratio=n.Requested<=0?0:Math.Min(1,n.Output/n.Requested);
                foreach(int id in n.FacilityIds)
                {
                    if(id==-1){HousingPowerRatio=n.Ratio;continue;}if(id==-2){CommunityLampPowerRatio=n.Ratio;continue;}
                    var f=GetFacility(id);f.PowerRatio=n.Ratio;f.PowerRequested=n.Requested;
                }
            }
        }
        private void TickPower(double seconds)
        {
            RecomputePower();
            foreach(var f in facilities.Where(f=>f.Built&&f.Kind==8&&f.PowerRequested>0).ToArray())AtFacility(f,()=>{
                Ignite();if(fuelRemaining<=0)return;
                HasBeenPowered=true;
                if(equivalentHeat>=20-1e-6&&!GeneratorResidue.TryAdd(ItemId.PowerAsh,1))return;
                if(equivalentHeat>=20-1e-6)equivalentHeat-=20;
                double burned=Math.Min(seconds,fuelRemaining);fuelRemaining=Math.Max(0,fuelRemaining-burned);
                if(fuelRemaining<1e-8)fuelRemaining=0;
                equivalentHeat+=burned*activeFuelHeatRate;
                if(equivalentHeat>=20-1e-6&&GeneratorResidue.TryAdd(ItemId.PowerAsh,1))equivalentHeat=Math.Max(0,equivalentHeat-20);
                Ignite();
            });
            RecomputePower();
        }
        public double WirePowerRatio(int x,int y)=>powerNetworks.FirstOrDefault(n=>n.Wires.Any(w=>w.X==x&&w.Y==y))?.Ratio??0;
    }
}
