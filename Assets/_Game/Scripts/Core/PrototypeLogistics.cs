using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public sealed class DeliveryTask
    {
        public ItemId Item;public int Amount,Token,Stage,Id,SourceX,SourceY,TargetX,TargetY;
        public string From,To;public UnitInventory Source,Target;public GroundPile Ground;
        public double Progress,RetryAt;public bool Picked;
    }
    public sealed partial class PrototypeSimulation
    {
        private int nextDelivery=1;
        private int IncomingTicks(UnitInventory target)=>residents.Where(r=>r.Delivery?.Target==target).Sum(r=>r.Delivery.Amount*ItemCatalog.CapacityTicks(r.Delivery.Item));
        private DeliveryTask Plan(Resident actor,UnitInventory source,UnitInventory target,string from,string to,ItemId item,int sx,int sy,int tx,int ty,int wanted)
        {
            int room=target==null?10-FuelCount-residents.Where(r=>r.Delivery?.To=="fuel").Sum(r=>r.Delivery.Amount):(target.FreeTicks-IncomingTicks(target))/ItemCatalog.CapacityTicks(item);
            int count=Math.Min(Math.Min(wanted,8*20/ItemCatalog.CapacityTicks(item)),room);
            if(source!=null)count=Math.Min(count,source.Available(item));
            if(count<=0||FindPath(actor.X,actor.Y,sx,sy)==null||FindPath(sx,sy,tx,ty)==null)return null;
            GroundPile pile=null;int token=0;
            if(source==null)
            {
                pile=ground.Where(p=>p.Item==item&&p.Amount>p.Reserved&&p.X==sx&&p.Y==sy).OrderBy(p=>p.Id).FirstOrDefault();
                if(pile==null)return null;count=Math.Min(count,pile.Amount-pile.Reserved);pile.Reserved+=count;
            }
            else {count=Math.Min(count,source.Available(item));if(count<=0)return null;token=source.Reserve(new ItemAmount(item,count));if(token==0)return null;}
            var task=new DeliveryTask{Id=nextDelivery++,Item=item,Amount=count,Token=token,Source=source,Target=target,Ground=pile,From=from,To=to,SourceX=sx,SourceY=sy,TargetX=tx,TargetY=ty};
            if(ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))foreach(var tool in tools.Where(t=>t.Item==item&&t.Location==from&&t.ReservedBy==0).OrderByDescending(t=>t.Durability).Take(count))tool.ReservedBy=task.Id;
            return task;
        }
        private DeliveryTask Supply(Resident actor,UnitInventory target,string key,ItemId item,int x,int y,int wanted)
        {
            DeliveryTask task=null;
            if(!ReferenceEquals(Warehouse,target)){task=Plan(actor,Warehouse,target,"warehouse",key,item,WarehouseX,WarehouseY,x,y,wanted);if(task!=null)return task;}
            foreach(var pile in ground.Where(p=>p.Item==item&&p.Amount>p.Reserved).OrderBy(p=>Distance(actor.X,actor.Y,p.X,p.Y)).ThenBy(p=>p.Id))
            {task=Plan(actor,null,target,"ground:"+pile.Id,key,item,pile.X,pile.Y,x,y,wanted);if(task!=null)return task;}
            return null;
        }
        private DeliveryTask NextTask(Resident actor)
        {
            DeliveryTask task=null;
            if(AutomaticLivingSupply)
                foreach(var item in new[]{ItemId.DrinkingWater,ItemId.Food})
                {
                    int wanted=(item==ItemId.DrinkingWater?2:1)*(residents.Count+1)-Warehouse.Count(item);
                    if(wanted>0){task=Supply(actor,Warehouse,"warehouse",item,WarehouseX,WarehouseY,wanted);if(task!=null)return task;}
                }
            if(BenchBuilt&&RunRequested&&Woodworker!=null&&(WoodTools.Count(ItemId.WoodHandSaw)==0||WoodTools.Count(ItemId.WoodHandSaw)<2&&tools.Any(t=>t.Item==ItemId.WoodHandSaw&&t.Location=="benchTools"&&t.Durability<=60)))
            {task=Supply(actor,WoodTools,"benchTools",ItemId.WoodHandSaw,BenchX,BenchY,1);if(task!=null)return task;}
            if(DismantleBuilt&&DismantleRequested)
                foreach(var item in new[]{ItemId.Screwdriver,ItemId.Wrench})
                    if((item!=ItemId.Wrench||ItemCatalog.NeedsWrench(SelectedDevice))&&DismantleTools.Count(item)==0)
                    {task=Supply(actor,DismantleTools,"dismantleTools",item,DismantleX,DismantleY,1);if(task!=null)return task;}
            if(GeneratorBuilt&&FuelSeconds<60)
            {
                foreach(var fuel in new[]{ItemId.WoodChips,ItemId.WasteWood})
                {task=Supply(actor,null,"fuel",fuel,GeneratorX,GeneratorY,Math.Min(8,10-FuelCount));if(task!=null)return task;}
            }
            foreach(var output in new[]{(Output,"woodOutput",BenchX,BenchY),(DismantleOutput,"dismantleOutput",DismantleX,DismantleY),(TableOutput,"tableOutput",TableX,TableY),(GeneratorResidue,"ash",GeneratorX,GeneratorY),(BrokenTools,"broken",BenchX,BenchY)})
                foreach(ItemId item in Enum.GetValues(typeof(ItemId)))if(output.Item1.Available(item)>0)
                {task=Plan(actor,output.Item1,Warehouse,output.Item2,"warehouse",item,output.Item3,output.Item4,WarehouseX,WarehouseY,output.Item1.Available(item));if(task!=null)return task;}
            if(BenchBuilt&&RunRequested&&pendingRecipe==null&&WoodInputStock.Count(ItemId.WasteWood)<WoodInput(SelectedRecipe))
            {task=Supply(actor,WoodInputStock,"woodInput",ItemId.WasteWood,BenchX,BenchY,WoodInput(SelectedRecipe)*2-WoodInputStock.Count(ItemId.WasteWood));if(task!=null)return task;}
            if(DismantleBuilt&&DismantleRequested&&DismantleInput.Count(SelectedDevice)<1)
            {task=Supply(actor,DismantleInput,"dismantleInput",SelectedDevice,DismantleX,DismantleY,2-DismantleInput.Count(SelectedDevice));if(task!=null)return task;}
            // Remaining ground-to-warehouse stock is deliberately an explicit command until supply policies arrive.
            return null;
        }
        private void TickResidents(double seconds)
        {
            foreach(var actor in residents)
            {
                if(RiotActive){actor.Activity="欠薪暴动：人工岗位停工";continue;}
                if(actor.Delivery==null)
                {
                    bool producing=actor.Job==WorkerJob.Woodwork&&RunRequested&&(jobActive||pendingRecipe==null&&WoodInputStock.Available(ItemId.WasteWood)>=WoodInput(SelectedRecipe))&&Output.FreeTicks>=BatchTicks(SelectedRecipe)&&(SelectedRecipe==WoodRecipe.SortedWood||ToolFor(1,ItemId.WoodHandSaw)!=null);
                    if(producing){actor.Activity="木工操作";WalkResident(actor,BenchX,BenchY,seconds);continue;}
                    actor.Delivery=NextTask(actor);
                    if(actor.Delivery==null){actor.Activity=actor.Job==WorkerJob.Woodwork?"等待原料/工具":"空闲";WalkResident(actor,actor.Job==WorkerJob.Woodwork?BenchX:14+actor.Id,actor.Job==WorkerJob.Woodwork?BenchY:5,seconds);continue;}
                }
                var delivery=actor.Delivery;
                if(delivery.RetryAt>GameSeconds)continue;
                int x=delivery.Picked?delivery.TargetX:delivery.SourceX,y=delivery.Picked?delivery.TargetY:delivery.SourceY;
                actor.Activity=delivery.Picked?"送货："+ItemCatalog.Name(delivery.Item):"取货："+ItemCatalog.Name(delivery.Item);
                WalkResident(actor,x,y,seconds);
                if(actor.Path==null)
                {
                    if(!delivery.Picked){if(delivery.Source!=null)delivery.Source.Release(delivery.Token);else delivery.Ground.Reserved-=delivery.Amount;ClearDelivery(actor);}
                    else {actor.Activity="无路径，保留携货，10秒后重试";delivery.RetryAt=GameSeconds+10;actor.Path=null;}
                    continue;
                }
                if(Distance(actor.X,actor.Y,x,y)>0.001)continue;
                delivery.Progress+=seconds*Math.Max(0.25,PressureAt(x,y)*LivingEfficiency(actor));
                if(delivery.Progress<2-1e-6)continue;
                if(!delivery.Picked)
                {
                    if(delivery.Source!=null){if(!delivery.Source.TransferReserved(delivery.Token,actor.Cargo))continue;}
                    else {actor.Cargo.TryAdd(delivery.Item,delivery.Amount);delivery.Ground.Reserved-=delivery.Amount;RemoveGround(delivery.Ground,delivery.Amount);}
                    foreach(var tool in tools.Where(t=>t.ReservedBy==delivery.Id)){tool.Location="worker:"+actor.Id;tool.ReservedBy=0;}
                    delivery.Picked=true;delivery.Progress=0;actor.Path=null;
                }
                else
                {
                    if(delivery.To=="fuel")
                    {if(FuelCount+delivery.Amount>10){actor.Activity="燃料缓冲满，等待";continue;}actor.Cargo.TryRemove(delivery.Item,delivery.Amount);if(delivery.Item==ItemId.WasteWood)fuelWood+=delivery.Amount;else fuelChips+=delivery.Amount;Ignite();}
                    else
                    {
                        if(delivery.Target.AvailableSlots(delivery.Item)<delivery.Amount){actor.Activity="目标满，保留携货";continue;}
                        MoveStock(actor.Cargo,delivery.Target,"worker:"+actor.Id,delivery.To,delivery.Item,delivery.Amount);
                    }
                    DeliveredTrips++;ClearDelivery(actor);
                }
            }
        }
        private void ClearDelivery(Resident actor)
        {
            if(actor.Delivery!=null)foreach(var tool in tools.Where(t=>t.ReservedBy==actor.Delivery.Id))tool.ReservedBy=0;
            actor.Delivery=null;actor.Path=null;
        }
    }
}
