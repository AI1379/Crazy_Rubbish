using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public enum Technology { Wood,Textile,Metal,Tools,Plastic,Growing }
    public sealed partial class PrototypeSimulation
    {
        public ActionResult RemoveFacility(int flag,bool sell,bool returnMaterials=true)
        {
            if(Interacting)return Fail("先完成或取消当前交互，再拆除设施。");
            bool built=flag==1?BenchBuilt:flag==2?DismantleBuilt:flag==4?TableBuilt:flag==8&&GeneratorBuilt;
            if(Failed)return Fail("失败后无法拆除或出售。");
            if(!built)return Fail("此玩家设施不存在；社区旧设施不能出售拆移。");
            var site=Station(flag);
            DetachFacilityPorts(site.Id);
            if(flag==1&&jobActive&&jobProgress>=Duration(activeRecipe)-1e-6&&!CanWear(woodToolId,WoodInput(activeRecipe))||
                flag==2&&dismantleActive&&dismantleProgress>=35-1e-6&&!CanWearDismantleTools())return Fail("损坏工具回收槽已满，先疏通仓库并搬出损坏工具，再拆除或搬迁。");
            if(flag==4)RemoveAssemblyBatch();
            if(flag==1&&jobActive){if(jobProgress>=Duration(activeRecipe)-1e-6){WoodInputStock.Commit(woodToken);foreach(var p in WoodProducts(activeRecipe))ReturnAtOldSite(p.Item,p.Amount,"",site.X,site.Y);WearTool(woodToolId,WoodInput(activeRecipe));CompletedBatches++;}else WoodInputStock.Release(woodToken);jobActive=false;jobProgress=0;RunRequested=false;}
            if(flag==2&&dismantleActive){if(dismantleProgress>=35-1e-6){DismantleInput.Commit(dismantleToken);foreach(var p in ItemCatalog.DeviceYield(SelectedDevice,dismantleUsable))ReturnAtOldSite(p.Item,p.Amount,"",site.X,site.Y);WearTool(screwToolId,1);WearTool(wrenchToolId,1);tools.RemoveAll(t=>t.Id==dismantleObjectId);DismantledBatches++;}else DismantleInput.Release(dismantleToken);dismantleActive=false;dismantleProgress=0;DismantleRequested=false;}
            int x=site.X,y=site.Y-1;
            if(flag==1)BenchBuilt=false;if(flag==2)DismantleBuilt=false;if(flag==4)TableBuilt=false;if(flag==8)GeneratorBuilt=false;
            geometryRevision++;GroundRevision++;manualStation=0;
            var inventories=flag==1?new[]{(WoodInputStock,StockKey("woodInput")),(Output,StockKey("woodOutput")),(WoodTools,StockKey("benchTools"))}:
                flag==2?new[]{(DismantleInput,StockKey("dismantleInput")),(DismantleOutput,StockKey("dismantleOutput")),(DismantleTools,StockKey("dismantleTools"))}:
                flag==4?new[]{(TableInput,StockKey("tableInput")),(TableOutput,StockKey("tableOutput"))}:new[]{(GeneratorResidue,StockKey("ash"))};
            foreach(var actor in residents.Where(r=>r.Delivery!=null))
            {
                if(inventories.Any(p=>p.Item1==actor.Delivery.Source||p.Item1==actor.Delivery.Target)||flag==8&&actor.Delivery.To==StockKey("fuel"))
                {
                    if(!actor.Delivery.Picked){if(actor.Delivery.Source!=null)actor.Delivery.Source.Release(actor.Delivery.Token);else actor.Delivery.Ground.Reserved-=actor.Delivery.Amount;ClearDelivery(actor);}
                    else {actor.Delivery.Target=Warehouse;actor.Delivery.To="warehouse";actor.Delivery.TargetX=WarehouseX;actor.Delivery.TargetY=WarehouseY;actor.Path=null;}
                }
            }
            foreach(var entry in inventories)foreach(ItemId item in ItemCatalog.All)
            {int count=entry.Item1.Available(item);entry.Item1.TryRemove(item,count);ReturnAtOldSite(item,count,entry.Item2,x,y+1);}
            if(flag==8){ReturnAtOldSite(ItemId.WasteWood,fuelWood,"",x,y+1);ReturnAtOldSite(ItemId.WoodChips,fuelChips,"",x,y+1);fuelWood=fuelChips=0;fuelRemaining=0;}
            ItemAmount[] costs=DesignBaseline.BuildCost(flag);
            if(returnMaterials&&sell)cashCents+=(long)decimal.Round(costs.Sum(p=>ItemCatalog.Price(p.Item).buy*p.Amount)*0.45m*100,0,MidpointRounding.AwayFromZero);
            else if(returnMaterials) foreach(var p in costs)ReturnAtOldSite(p.Item,p.Amount,"",x,y+1);
            foreach(var r in residents.Where(r=>r.FacilityId==Station(flag).Id)){r.Job=WorkerJob.Idle;r.FacilityId=0;}
            ConstructionRevision++;RecomputePower();ReleaseUnusedTools();RefreshState();return Ok(sell?"设施出售，建材不重复返还。":"设施拆除，材料及内部物品已返还或留旧址。");
        }
        private void ReturnAtOldSite(ItemId item,int count,string from,int x,int y)
        {
            if(count<=0)return;int inWarehouse=Math.Min(count,Warehouse.AvailableSlots(item));
            if(inWarehouse>0){Warehouse.TryAdd(item,inWarehouse);MoveInstances(from,"warehouse",item,inWarehouse);}
            for(int i=inWarehouse;i<count;i++){var pile=Place(item,1,null,x,y,false);var instance=tools.FirstOrDefault(t=>t.Item==item&&t.Location==from);if(instance!=null){instance.Location="ground:"+pile.Id;pile.ToolId=instance.Id;}}
        }
    }
}
