using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public PlanStock Stock(string code)
        {
            if(code=="backpack")return State.backpack;
            if(code=="hole")return State.holeInput;
            var parts=code.Split(':');int id=int.Parse(parts[1]);if(parts[0]=="ground")return State.ground.FirstOrDefault(g=>g.id==id)?.stock;
            var site=Site(id);if(site==null)return null;return parts[0]=="input"?site.input:parts[0]=="tools"?site.tools:site.output;
        }
        public (int x,int y) StockPosition(string code)
        {if(code=="hole")return(2,19);var parts=code.Split(':');int id=int.Parse(parts[1]);if(parts[0]=="ground"){var g=State.ground.First(p=>p.id==id);return(g.x,g.y);}return Front(Site(id));}
        public string WarehouseCode(PlanStock warehouse)=>"input:"+State.sites.First(s=>s.input==warehouse).id;
        private IEnumerable<string> Sources(string item,bool ground=true)
        {
            foreach(var s in State.sites.Where(s=>!s.pending&&s.type=="warehouse"&&s.input.Count(item)>0))yield return "input:"+s.id;
            foreach(var s in State.sites.Where(s=>s.enabled&&!s.pending&&s.type!="warehouse"&&s.output.Count(item)>0))yield return "output:"+s.id;
            if(ground)foreach(var g in State.ground.Where(g=>g.stock.Count(item)>0))yield return "ground:"+g.id;
        }
        private double Incoming(string target)=>Alive.Where(p=>p.trip!=null&&p.trip.target==target).Sum(p=>Config.Item(p.trip.item).capacity*p.trip.ticks/PlanStock.Scale);
        private double UsedTarget(string target)
        {
            var stock=Stock(target);if(target.StartsWith("input:")){var site=Site(int.Parse(target.Split(':')[1]));if(site.type!="warehouse")return PlanStock.CombinedUsed(Config,site.input,site.reserved);}return stock.Used(Config);
        }
        private long IncomingCount(string target,string item)=>Alive.Where(p=>p.trip!=null&&p.trip.target==target&&p.trip.item==item).Sum(p=>p.trip.ticks);
        private bool Schedule(PlanPerson person,string source,string target,string item,long requested,string quality=null)
        {
            var from=Stock(source);var to=Stock(target);if(from==null||to==null||source==target)return false;
            var selected=from.stacks.FirstOrDefault(v=>v.item==item&&v.ticks>v.reserved&&(string.IsNullOrEmpty(quality)||v.quality==quality));if(selected==null)return false;
            long available=string.IsNullOrEmpty(quality)?from.Count(item):from.stacks.Where(v=>v.item==item&&v.quality==quality).Sum(v=>v.ticks-v.reserved);
            bool plantingSeed=item=="seed_tuber"&&target.StartsWith("input:")&&Site(int.Parse(target.Split(':')[1]))?.type=="growing";
            if(!plantingSeed&&source.StartsWith("input:")&&Site(int.Parse(source.Split(':')[1])).type=="warehouse")available=Math.Min(available,Surplus(item));
            double free=Math.Min(person.cargo.capacity,to.capacity-UsedTarget(target)-Incoming(target));
            long amount=Math.Min(Math.Min(available,requested),PlanStock.Quantity(Math.Max(0,free)/Math.Max(.001,Config.Item(item).capacity)));
            if(Config.Item(item).durability>0||Config.Item(item).payload.Length>0||!string.IsNullOrEmpty(Config.Item(item).instance_state))amount=amount>=PlanStock.Scale?PlanStock.Scale:0;
            if(Config.Item(item).uses_per_item>0)
            {var bar=from.stacks.FirstOrDefault(s=>s.item==item&&s.reserved==0);amount=bar!=null&&free>=Config.Item(item).capacity?bar.ticks:0;}
            if(selected.instance>0)amount=Math.Min(amount,selected.ticks-selected.reserved);
            if(amount<=0)return false;var a=StockPosition(source);var b=StockPosition(target);
            if(Path(person.x,person.y,a.x,a.y)==null||Path(a.x,a.y,b.x,b.y)==null)return false;
            int instance=selected.instance>0?selected.instance:-1;from.Reserve(item,amount,instance);person.trip=new PlanTrip{source=source,target=target,item=item,ticks=amount,instance=instance};person.activity="搬运";return true;
        }
        private bool Supply(PlanPerson p,PlanSite site,string kind,string item,long desired)
        {
            string target=kind+":"+site.id;long deficit=desired-Stock(target).Count(item)-IncomingCount(target,item);if(deficit<=0)return false;
            string quality=kind=="input"&&!string.IsNullOrEmpty(site.recipe)?Config.Recipe(site.recipe).requires_state:null;if(kind=="input"&&item=="raw_freshwater")quality="qualified_fresh_source";
            foreach(string source in Sources(item).OrderBy(c=>{var at=StockPosition(c);return Distance(p.x,p.y,at.x,at.y);}))if(Schedule(p,source,target,item,deficit,quality))return true;
            return false;
        }
        private bool FindDelivery(PlanPerson p)
        {
            // A removed destination never destroys cargo; find a warehouse with real space.
            if(p.cargo.stacks.Count>0)
            {
                var stack=p.cargo.stacks[0];foreach(var w in Warehouses)
                    if(w.Fits(Config,stack.item,stack.ticks)){p.trip=new PlanTrip{source="backpack",target=WarehouseCode(w),item=stack.item,ticks=stack.ticks,picked=true,stage=2};return true;}
                return false;
            }
            foreach(string item in new[]{"water","food"})if(InspectSupply(item).cycles<1&&DeliverLifeStock(p,item))return true;
            var sites=State.sites.Where(s=>s.enabled&&!s.pending&&RepairAllowed(s)).OrderByDescending(WorkPriority).ThenBy(s=>s.id).ToArray();
            foreach(var site in sites)if(SupplyTools(p,site))return true;
            foreach(var site in sites)
            {
                bool water=(site.type=="water_purifier"||site.type=="auto_water_plant")&&InspectSupply("water").cycles<1;
                bool food=(site.type=="kitchen"||site.recipe=="wash_food")&&InspectSupply("food").cycles<1;
                if((water||food)&&SupplyRecipe(p,site))return true;
            }
            // Keep power and planted crops alive before clearing routine outputs.
            foreach(var site in sites)
            {
                if(site.type=="generator"||site.type=="boiler")
                {
                    if(site.fuelSeconds<60&&(Supply(p,site,"input","wood_chips",2*PlanStock.Scale)||Supply(p,site,"input","waste_wood",PlanStock.Scale)))return true;
                    if(site.type=="boiler"&&Supply(p,site,"input","water",PlanStock.Quantity(8.1)))return true;
                }
                if(site.type=="growing"&&site.active&&Config.Recipe(site.recipe).stages[site.stage].kind=="human"&&SupplyRecipe(p,site))return true;
            }
            if(!State.riot.active&&!string.IsNullOrEmpty(State.holeFilter)&&CanDiscard(State.holeFilter))foreach(string source in Sources(State.holeFilter))if(Schedule(p,source,"hole",State.holeFilter,PlanStock.Quantity(10/Math.Max(.01,Config.Item(State.holeFilter).capacity))-State.holeInput.Count(State.holeFilter)))return true;
            foreach(var site in sites)if(OutputNeedsSpace(site)&&DeliverOutput(p,site))return true;
            foreach(var site in sites)
            {
                if(site.type=="landfill"&&!string.IsNullOrEmpty(site.filter)&&CanDiscard(site.filter)&&site.landfilled<Config.Facility(site.type).capacity)
                    if(Supply(p,site,"input",site.filter,PlanStock.Quantity(10/Math.Max(.01,Config.Item(site.filter).capacity))))return true;
                if(SupplyRecipe(p,site))return true;
            }
            foreach(string item in new[]{"water","food"})if(DeliverLifeStock(p,item))return true;
            foreach(var site in sites)if(DeliverOutput(p,site))return true;
            if(State.riot.active)return false;
            foreach(var g in State.ground.OrderBy(g=>Distance(p.x,p.y,g.x,g.y)))foreach(var stack in g.stock.stacks.ToArray())foreach(var w in Warehouses)
                if(Schedule(p,"ground:"+g.id,WarehouseCode(w),stack.item,stack.ticks-stack.reserved))return true;
            return false;
        }
        private bool DeliverLifeStock(PlanPerson p,string item)
        {
            long incoming=Alive.Where(a=>a.trip!=null&&a.trip.item==item&&a.trip.target.StartsWith("input:")&&Site(int.Parse(a.trip.target.Split(':')[1]))?.type=="warehouse").Sum(a=>a.trip.ticks);
            long need=Protected(item)-Stored(item)-incoming;if(need<=0)return false;
            foreach(string source in Sources(item).Where(c=>!c.StartsWith("input:")))foreach(var w in Warehouses)if(Schedule(p,source,WarehouseCode(w),item,need))return true;return false;
        }
        private bool SupplyTools(PlanPerson p,PlanSite site)
        {
            if(string.IsNullOrEmpty(site.recipe))return false;
            foreach(var tool in Config.Recipe(site.recipe).tools)
            {
                if(site.tools.stacks.Any(t=>Config.Item(t.item).tool_family==tool.item&&t.durability>0))continue;
                foreach(var candidate in Config.items.Where(i=>i.tool_family==tool.item&&i.durability>0).OrderByDescending(i=>i.efficiency))if(Supply(p,site,"tools",candidate.id,PlanStock.Scale))return true;
            }return false;
        }
        private bool SupplyRecipe(PlanPerson p,PlanSite site)
        {
            if(string.IsNullOrEmpty(site.recipe))return false;
            var inputs=Config.Recipe(site.recipe).inputs;bool missing=false;
            foreach(var a in inputs)if(site.input.Count(a.item)+IncomingCount("input:"+site.id,a.item)<a.Ticks)
            {missing=true;if(Supply(p,site,"input",a.item,a.Ticks))return true;}
            if(missing)return false;
            foreach(var a in inputs)if(Supply(p,site,"input",a.item,2*a.Ticks))return true;return false;
        }
        private bool OutputNeedsSpace(PlanSite site)=>string.IsNullOrEmpty(site.recipe)?site.output.Used(Config)>=site.output.capacity-1e-8:site.output.Used(Config)+OutputVolume(site,Config.Recipe(site.recipe))>site.output.capacity+1e-8;
        private bool DeliverOutput(PlanPerson p,PlanSite site)
        {
            foreach(var stack in site.output.stacks.Where(v=>v.ticks>v.reserved).ToArray())
            {
                // A full intake is normal backpressure once two downstream batches are buffered.
                // Do not spend all haulers filling warehouses with raw water indefinitely.
                if(stack.item=="raw_freshwater"&&RawWaterBuffered(site))continue;
                foreach(var w in Warehouses)if(Schedule(p,"output:"+site.id,WarehouseCode(w),stack.item,stack.ticks-stack.reserved))return true;
            }return false;
        }
        private bool RawWaterBuffered(PlanSite source)
        {
            var users=State.sites.Where(s=>s.enabled&&!s.pending&&!string.IsNullOrEmpty(s.recipe)&&Config.Recipe(s.recipe).inputs.Any(a=>a.item=="raw_freshwater")).ToArray();
            long target=users.Sum(s=>Config.Recipe(s.recipe).inputs.Where(a=>a.item=="raw_freshwater").Sum(a=>2*a.Ticks));
            if(target==0&&!string.IsNullOrEmpty(source.recipe))target=Config.Recipe(source.recipe).outputs.Where(a=>a.item=="raw_freshwater").Sum(a=>2*a.Ticks);
            if(target==0)return false;
            long pending=Alive.Where(p=>p.trip!=null&&p.trip.item=="raw_freshwater").Sum(p=>p.trip.ticks);
            return Stored("raw_freshwater")+users.Sum(s=>s.input.Count("raw_freshwater")+s.reserved.Count("raw_freshwater"))+pending>=target;
        }
        private void TickTrip(PlanPerson p,double dt)
        {
            var t=p.trip;if(t==null)return;var target=Stock(t.target);
            if(target==null){if(!t.picked)ReleaseInventoryTrip(t);p.trip=null;return;}
            var position=StockPosition(t.picked?t.target:t.source);
            if(!Walk(p,position.x,position.y,dt)){if(!t.picked)ReleaseInventoryTrip(t);p.trip=null;p.activity="路径受阻，稍后重试";return;}
            if(Distance(p.x,p.y,position.x,position.y)>.1)return;
            t.elapsed+=dt;double wait=t.picked?Config.Rule("workers.unloading_seconds"):Config.Rule("workers.loading_seconds");if(t.elapsed+1e-8<wait)return;
            if(!t.picked)
            {CompleteInventoryPickup(t,p.cargo);t.picked=true;t.elapsed=0;Discover(t.item);State.revision++;}
            else if(UsedTarget(t.target)+Config.Item(t.item).capacity*(Config.Item(t.item).uses_per_item>0?Math.Ceiling(t.ticks/(double)PlanStock.Scale):t.ticks/(double)PlanStock.Scale)<=target.capacity+1e-8)
            {target.Put(p.cargo.Take(t.item,t.ticks));p.trip=null;p.activity="空闲";Discover(t.item);State.revision++;}
        }
        private void AbortTripsForSite(int id)
        {
            foreach(var p in Alive.Where(p=>p.trip!=null&&(p.trip.source.EndsWith(":"+id)||p.trip.target.EndsWith(":"+id))))
            {if(!p.trip.picked)ReleaseInventoryTrip(p.trip);p.trip=null;}
        }
        private void TickWorkers(double dt,bool allowSeek)
        {
            foreach(var p in Alive)
            {
                if(p.directed)continue;
                if(!MayWork(p)){p.activity="暴动停工";continue;}
                if(p.trip!=null){TickTrip(p,dt);continue;}
                if(p.maintaining>0)
                {
                    var site=Site(p.maintaining);if(site==null){p.maintaining=0;continue;}if(!RepairAllowed(site)){p.activity="暴动停工";continue;}var front=Front(site);Walk(p,front.x,front.y,dt);p.activity="维护";
                    if(Distance(p.x,p.y,front.x,front.y)<.1)site.durability=Math.Min(100,site.durability+Config.Rule("maintenance.repair_per_second_at_efficiency1")*Efficiency(p)*dt);
                    continue;
                }
                if(p.assigned>0){var site=Site(p.assigned);if(site!=null&&site.enabled){var front=Front(site);Walk(p,front.x,front.y,dt);p.activity="生产";continue;}p.assigned=0;}
                if(p.assigned==-1)continue;
                if(p.manualAssignment>0){var pinned=Site(p.manualAssignment);if(pinned!=null&&pinned.enabled&&pinned.active&&Config.Recipe(pinned.recipe).stages[pinned.stage].kind=="human"){p.assigned=pinned.id;continue;}}
                if(allowSeek&&State.seconds>=p.nextSeek)
                {
                    p.nextSeek=State.seconds+Config.Rule("workers.idle_seek_interval");
                    var vacancy=State.sites.FirstOrDefault(s=>RepairAllowed(s)&&!s.pending&&s.maintainer==0&&Config.Facility(s.type).maintainers>0);
                    if(vacancy!=null&&(p.manualAssignment==0||Site(p.manualAssignment)==null||!Site(p.manualAssignment).enabled)){p.maintaining=vacancy.id;vacancy.maintainer=p.id;continue;}
                    if(FindDelivery(p))TickTrip(p,dt);else p.activity="空闲";
                }
            }
        }
    }
}
