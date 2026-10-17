using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        private readonly Dictionary<(int port,int item,int owner),bool> routeCache=new Dictionary<(int,int,int),bool>();
        private double routeCacheTime=-1;private int routeCacheRevision=-1;
        private bool PortCanSupply(ConveyorPort p,ItemId item)
        {
            if(!LivePort(p)||!p.Output||p.Filter>=0&&p.Filter!=(int)item)return false;
            if(p.OwnerId==-1)return true;
            var stock=PortStock(p).stock;if(stock!=null&&stock.Available(item)>0)return true;
            var f=GetFacility(p.OwnerId);if(f==null)return false;
            if(IsWoodMachine(f.Kind))return item==ItemId.Plank||item==ItemId.WoodChips;
            if(f.Kind==1)return item==Product(f.SelectedRecipe)||f.SelectedRecipe!=WoodRecipe.SortedWood&&item==ItemId.WoodChips;
            if(f.Kind==2)return ItemCatalog.DeviceYield(f.SelectedDevice,f.dismantleUsable).Any(a=>a.Item==item);
            if(f.Kind==4)return item==(f.SelectedAssembly==AssemblyRecipe.Saw?ItemId.WoodHandSaw:f.SelectedAssembly==AssemblyRecipe.Hammer?ItemId.Hammer:ItemId.Screwdriver);
            return f.Kind==8&&item==ItemId.PowerAsh;
        }
        private bool PoweredConveyorRoute(ConveyorPort source,ItemId item,int? targetOwner=null)
        {
            if(routeCacheTime!=GameSeconds||routeCacheRevision!=ConstructionRevision){routeCache.Clear();routeCacheTime=GameSeconds;routeCacheRevision=ConstructionRevision;}
            var key=(source.Id,(int)item,targetOwner??0);if(routeCache.TryGetValue(key,out bool value))return value;
            return routeCache[key]=EvaluatePoweredConveyorRoute(source,item,targetOwner);
        }
        private bool EvaluatePoweredConveyorRoute(ConveyorPort source,ItemId item,int? targetOwner)
        {
            if(!PortCanSupply(source,item))return false;
            var b=LaneReceiving(source.X+ConveyorDx[source.Facing],source.Y+ConveyorDy[source.Facing],source.Facing);
            var seen=new HashSet<int>();var queued=new HashSet<ItemId>();
            while(b!=null&&b.PowerRatio>0&&seen.Add(b.Id))
            {
                if(b.Item>=0)queued.Add((ItemId)b.Item);
                var next=ConveyorNext(b);var p=PortReceiving(b,next);
                if(p!=null)return PortAccepts(p,item)&&(!targetOwner.HasValue||p.OwnerId==targetOwner.Value)&&queued.All(i=>PortAccepts(p,i));
                b=LaneReceiving(next.x,next.y,b.Direction);
            }
            return false;
        }
        private bool PreferConveyorInput(int owner,ItemId item)=>ports.Any(p=>PoweredConveyorRoute(p,item,owner));
        private bool PreferConveyorOutput(UnitInventory stock,ItemId item)=>ports.Any(p=>p.Output&&PortStock(p).stock==stock&&PoweredConveyorRoute(p,item));
        private DeliveryTask SupplyConveyorSources(Resident actor)
        {
            foreach(var p in ports.Where(p=>p.OwnerId==-1&&p.Output&&p.Filter>=0&&LivePort(p)).OrderBy(p=>p.Id))
            {
                var item=(ItemId)p.Filter;
                int desired=Math.Max(1,160/ItemCatalog.CapacityTicks(item));
                int incoming=residents.Where(r=>r.Delivery?.Target==Warehouse&&r.Delivery.Item==item).Sum(r=>r.Delivery.Amount);
                int wanted=desired-Warehouse.Count(item)-incoming;if(wanted<=0||!PoweredConveyorRoute(p,item))continue;
                var task=Supply(actor,Warehouse,"warehouse",item,WarehouseX,WarehouseY,wanted);if(task!=null)return task;
            }
            return null;
        }
        private bool MachineOperatorCanTakeDelivery(Resident actor,DeliveryTask task)
        {
            if(actor.Job!=WorkerJob.MachineOperator)return true;
            var f=GetFacility(actor.FacilityId);if(f==null||!f.MachineActive||f.MachinePhase!=2)return true;
            var first=FindPath(actor.X,actor.Y,task.SourceX,task.SourceY);var second=FindPath(task.SourceX,task.SourceY,task.TargetX,task.TargetY);
            if(first==null||second==null)return false;
            double travel=(first.Length+second.Length)/Settings.residentMoveSpeed+4/Math.Max(0.25,LivingEfficiency(actor));
            double available=(DesignBaseline.WoodMachineSeconds-f.MachineProgress)/Math.Max(0.01,f.PowerRatio);
            return travel+0.5<=available;
        }
    }
}
