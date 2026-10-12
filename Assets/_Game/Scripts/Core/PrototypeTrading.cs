using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public enum Technology { Wood,Textile,Metal,Tools,Plastic,Growing }
    public sealed partial class PrototypeSimulation
    {
        private readonly bool[] soldTechnologies=new bool[6];
        public bool IsTechnologySold(Technology technology)=>soldTechnologies[(int)technology];
        private bool MinimumPriceOnly(ItemId item)=>soldTechnologies[0]&&(item==ItemId.Plank||item==ItemId.WoodHandle||item==ItemId.SortedWood)||
            soldTechnologies[2]&&(item==ItemId.SteelPart||item==ItemId.CopperPart||item==ItemId.AluminumPart)||
            soldTechnologies[3]&&(item==ItemId.HammerHead||item==ItemId.SawBlade||item==ItemId.ScrewdriverRod||item==ItemId.Hammer||item==ItemId.WoodHandSaw||item==ItemId.Screwdriver)||
            soldTechnologies[5]&&item==ItemId.Food;
        public ActionResult SellTechnology(Technology technology)
        {
            if((int)technology<0||(int)technology>5||IsTechnologySold(technology))return Fail("技术不存在或已售出。");
            bool unlocked=technology==Technology.Wood||technology==Technology.Textile&&(discovered.Contains(ItemId.WasteTextile)||discovered.Contains(ItemId.OldClothes))||
                technology==Technology.Metal&&(discovered.Contains(ItemId.SteelMaterial)||discovered.Contains(ItemId.CopperMaterial)||discovered.Contains(ItemId.AluminumMaterial))||
                technology==Technology.Tools&&discovered.Contains(ItemId.Plank)&&discovered.Contains(ItemId.SteelPart)||
                technology==Technology.Plastic&&discovered.Contains(ItemId.HeatingElement)||technology==Technology.Growing&&discovered.Contains(ItemId.GrowingLight);
            if(!unlocked)return Fail("先取得对应材料解锁科技。");
            int[] values={60,50,100,80,80,40};cashCents+=values[(int)technology]*100;soldTechnologies[(int)technology]=true;
            return Ok("技术使用权保留，对应商品永久最低价。收入"+values[(int)technology]+"钱。");
        }
        public ActionResult RemoveFacility(int flag,bool sell)
        {
            bool built=flag==1?BenchBuilt:flag==2?DismantleBuilt:flag==4?TableBuilt:flag==8&&GeneratorBuilt;
            if(!built)return Fail("此玩家设施不存在；社区旧设施不能出售拆移。");
            if(flag==4)RemoveAssemblyBatch();
            if(flag==1&&jobActive){if(jobProgress>=Duration(activeRecipe)-1e-6){WoodInputStock.Commit(woodToken);foreach(var p in WoodProducts(activeRecipe))ReturnAtOldSite(p.Item,p.Amount,"",BenchX,BenchY+1);WearTool(woodToolId,WoodInput(activeRecipe));CompletedBatches++;}else WoodInputStock.Release(woodToken);jobActive=false;jobProgress=0;RunRequested=false;}
            if(flag==2&&dismantleActive){if(dismantleProgress>=35-1e-6){DismantleInput.Commit(dismantleToken);foreach(var p in ItemCatalog.DeviceYield(SelectedDevice,dismantleUsable))ReturnAtOldSite(p.Item,p.Amount,"",DismantleX,DismantleY+1);DismantledBatches++;}else DismantleInput.Release(dismantleToken);dismantleActive=false;dismantleProgress=0;DismantleRequested=false;}
            int x=flag==1?BenchX:flag==2?DismantleX:flag==4?TableX:GeneratorX,y=flag==1?BenchY:flag==2?DismantleY:flag==4?TableY:GeneratorY;
            if(flag==1)BenchBuilt=false;if(flag==2)DismantleBuilt=false;if(flag==4)TableBuilt=false;if(flag==8)GeneratorBuilt=false;
            geometryRevision++;GroundRevision++;manualStation=0;
            var inventories=flag==1?new[]{(WoodInputStock,"woodInput"),(Output,"woodOutput"),(WoodTools,"benchTools")}:
                flag==2?new[]{(DismantleInput,"dismantleInput"),(DismantleOutput,"dismantleOutput"),(DismantleTools,"dismantleTools")}:
                flag==4?new[]{(TableInput,"tableInput"),(TableOutput,"tableOutput")}:new[]{(GeneratorResidue,"ash")};
            foreach(var actor in residents.Where(r=>r.Delivery!=null))
            {
                if(inventories.Any(p=>p.Item1==actor.Delivery.Source||p.Item1==actor.Delivery.Target))
                {
                    if(!actor.Delivery.Picked){if(actor.Delivery.Source!=null)actor.Delivery.Source.Release(actor.Delivery.Token);else actor.Delivery.Ground.Reserved-=actor.Delivery.Amount;ClearDelivery(actor);}
                    else {actor.Delivery.Target=Warehouse;actor.Delivery.To="warehouse";actor.Delivery.TargetX=WarehouseX;actor.Delivery.TargetY=WarehouseY;actor.Path=null;}
                }
            }
            foreach(var entry in inventories)foreach(ItemId item in Enum.GetValues(typeof(ItemId)))
            {int count=entry.Item1.Available(item);entry.Item1.TryRemove(item,count);ReturnAtOldSite(item,count,entry.Item2,x,y+1);}
            if(flag==8){ReturnAtOldSite(ItemId.WasteWood,fuelWood,"",x,y+1);ReturnAtOldSite(ItemId.WoodChips,fuelChips,"",x,y+1);fuelWood=fuelChips=0;fuelRemaining=0;}
            ItemAmount[] costs=flag==1||flag==2?new[]{new ItemAmount(ItemId.WasteWood,4),new ItemAmount(ItemId.RecoveredFastener,2)}:
                flag==4?new[]{new ItemAmount(ItemId.Plank,4),new ItemAmount(ItemId.SteelPart,2)}:
                new[]{new ItemAmount(ItemId.SteelPart,6),new ItemAmount(ItemId.ThermalModule,1),new ItemAmount(ItemId.GeneratorModule,1),new ItemAmount(ItemId.RecoveredWire,2)};
            if(sell)cashCents+=(long)decimal.Round(costs.Sum(p=>ItemCatalog.Price(p.Item).buy*p.Amount)*0.45m*100,0,MidpointRounding.AwayFromZero);
            else foreach(var p in costs)ReturnAtOldSite(p.Item,p.Amount,"",x,y+1);
            ReleaseUnusedTools();RefreshState();return Ok(sell?"设施出售，建材不重复返还。":"设施拆除，材料及内部物品已返还或留旧址。");
        }
        private void ReturnAtOldSite(ItemId item,int count,string from,int x,int y)
        {
            if(count<=0)return;int inWarehouse=Math.Min(count,Warehouse.AvailableSlots(item));
            if(inWarehouse>0){Warehouse.TryAdd(item,inWarehouse);MoveInstances(from,"warehouse",item,inWarehouse);}
            for(int i=inWarehouse;i<count;i++){var pile=Place(item,1,null,x,y,false);var instance=tools.FirstOrDefault(t=>t.Item==item&&t.Location==from);if(instance!=null){instance.Location="ground:"+pile.Id;pile.ToolId=instance.Id;}}
        }
    }
}
