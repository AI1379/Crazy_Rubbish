using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        private ActionResult lastInteraction=new ActionResult(true,"就绪。");
        private Func<ActionResult> pendingInteraction;
        private double interactionRemaining;
        private bool pickupInteraction;
        public bool Interacting=>pendingInteraction!=null;
        public bool PickingUp=>Interacting&&pickupInteraction;
        public int InteractionRevision {get;private set;}
        public ActionResult LastInteraction=>lastInteraction;
        private bool AtFront(int x,int y)=>(int)Math.Floor(PlayerY+0.5)==y&&((int)Math.Floor(PlayerX+0.5)==x||(int)Math.Floor(PlayerX+0.5)==x+1);
        public void LeaveManualRole(){manualStation=0;CancelInteraction();RefreshState();}
        private ActionResult QueueInteraction(Func<ActionResult> action,bool pickup=false)
        {
            if(Paused)return Fail("恢复运行后进行交互。");
            if(Interacting)return Fail("正在交互，请等待或按Esc取消。");
            manualStation=0;ReleaseUnusedTools();pendingInteraction=action;pickupInteraction=pickup;interactionRemaining=Settings.interactionSeconds;
            return Ok("交互中…");
        }
        public void CancelInteraction(){if(pendingInteraction==null)return;pendingInteraction=null;lastInteraction=Fail("交互取消，物品未转移。");InteractionRevision++;}
        private void TickInteraction(double seconds)
        {
            if(pendingInteraction==null)return;interactionRemaining-=seconds;if(interactionRemaining>1e-6)return;
            var action=pendingInteraction;pendingInteraction=null;lastInteraction=action();InteractionRevision++;RefreshState();
        }
        public ActionResult PickUp(int requested=1,ItemId? selected=null)
        {
            var pile=selected==null?NearestPickup():ground.Where(p=>p.Item==selected.Value&&p.Amount>p.Reserved&&Near(p.X,p.Y)).OrderBy(p=>Distance(PlayerX,PlayerY,p.X,p.Y)).ThenBy(p=>p.Id).FirstOrDefault();
            if(pile==null)return Fail("附近没有未预约的该物品。");
            return QueueInteraction(()=>{
                if(!ground.Contains(pile)||!Near(pile.X,pile.Y))return Fail("物品已经移走。");
                if(ItemCatalog.IsOpenable(pile.Item))
                {
                    var content=ItemCatalog.DeviceYield(pile.Item,pile.Usable);
                    if(pile.Item==ItemId.OldContainer&&Carry.AvailableSlots(ItemId.Container)==0)return Fail("背包放不下容器。");
                    int originalId=pile.ToolId;RemoveGround(pile,1);tools.RemoveAll(t=>t.Id==originalId);
                    foreach(var part in content)
                    {
                        if(pile.Item==ItemId.OldContainer&&Carry.TryAdd(part.Item,part.Amount))discovered.Add(part.Item);
                        else AddNatural(part.Item,part.Amount,pile.X,pile.Y,true);
                    }
                    return Ok("已取得固定内容；箱/线束产物留在原位，容器进入背包。",1);
                }
                int count=Math.Min(Math.Min(requested,pile.Amount-pile.Reserved),Carry.AvailableSlots(pile.Item));
                if(count<=0)return Fail("背包容量不足或物品已被预约。");
                Carry.TryAdd(pile.Item,count);MoveInstances("ground:"+pile.Id,"carry",pile.Item,count);RemoveGround(pile,count);discovered.Add(pile.Item);
                return Ok("拾取完成。",count);
            },true);
        }
        public ActionResult Deposit()
        {
            if(!NearWarehouse)return Fail("靠近仓库取用点后转存。");
            return QueueInteraction(()=>{
                int count=0;foreach(ItemId item in Enum.GetValues(typeof(ItemId)))if(item!=ItemId.WoodHandSaw)count+=MoveStock(Carry,Warehouse,"carry","warehouse",item,Carry.Available(item));
                return count>0?Ok("转存完成，主动携带的手锯仍随身。",count):Fail("没有可转存物品或仓库已满。");
            });
        }
        public ActionResult Withdraw(ItemId item,int requested=1)
        {
            if(!NearWarehouse)return Fail("靠近仓库取用点后取物。");
            return QueueInteraction(()=>{int count=MoveStock(Warehouse,Carry,"warehouse","carry",item,requested);return count>0?Ok("取出完成。",count):Fail("未预约库存或背包空间不足。");});
        }
        public ActionResult Drop(ItemId item)
        {
            if(Carry.Available(item)==0)return Fail("背包无可放下物品。");
            return QueueInteraction(()=>{
                if(!FindGroundSpace(item,(int)Math.Round(PlayerX),(int)Math.Round(PlayerY),out var pile,out int x,out int y,2.5))return Fail("附近没有合法普通存放格。");
                var instance=tools.FirstOrDefault(t=>t.Item==item&&t.Location=="carry"&&t.ReservedBy==0);
                Carry.TryRemove(item,1);var placed=Place(item,1,pile,x,y,false);if(instance!=null){instance.Location="ground:"+placed.Id;placed.ToolId=instance.Id;}
                return Ok("物品已放下。",1);
            });
        }
        public ActionResult HandOffTool(ItemId item)
        {
            int station=item==ItemId.WoodHandSaw?1:2;
            if(!ItemCatalog.IsTool(item)||station==1&&!NearBench||station==2&&!Near(DismantleX,DismantleY))return Fail("靠近对应地点正面后交接工具。");
            var slot=station==1?WoodTools:DismantleTools;string key=station==1?"benchTools":"dismantleTools";
            return QueueInteraction(()=>{
                if(slot.Count(item)>=2)return Fail("该工具已有两把。");
                int count=MoveStock(Carry,slot,"carry",key,item,1);return count>0?Ok("工具留在地点槽，居民可使用。",count):Fail("背包没有该工具。");
            });
        }
        public ActionResult DeliverInput(bool dismantle=false,int requested=int.MaxValue)
        {
            if(dismantle?!Near(DismantleX,DismantleY):!NearBench)return Fail("靠近地点正面后投料。");
            var input=dismantle?DismantleInput:WoodInputStock;var item=dismantle?SelectedDevice:ItemId.WasteWood;
            return QueueInteraction(()=>{
                int count=MoveStock(Carry,input,"carry",dismantle?"dismantleInput":"woodInput",item,requested);
                return count>0?Ok("已实际投入地点缓冲。",count):Fail("背包缺料或输入缓冲已满。");
            });
        }
        public ActionResult TakeOutput(bool dismantle=false)
        {
            if(dismantle?!Near(DismantleX,DismantleY):!NearBench)return Fail("靠近地点正面后取出。");
            var output=dismantle?DismantleOutput:Output;
            return QueueInteraction(()=>{
                int count=0;foreach(ItemId item in Enum.GetValues(typeof(ItemId)))count+=MoveStock(output,Carry,dismantle?"dismantleOutput":"woodOutput","carry",item,output.Available(item));
                return count>0?Ok("成品进入背包，需搬运入库出售。",count):Fail("没有成品或背包已满。");
            });
        }
        public ActionResult TakeInput(bool dismantle=false)
        {
            if(dismantle?!Near(DismantleX,DismantleY):!NearBench)return Fail("靠近地点正面后取料。");
            var input=dismantle?DismantleInput:WoodInputStock;
            return QueueInteraction(()=>{
                int count=0;foreach(ItemId item in Enum.GetValues(typeof(ItemId)))count+=MoveStock(input,Carry,dismantle?"dismantleInput":"woodInput","carry",item,input.Available(item));
                return count>0?Ok("未在制的输入进入背包。",count):Fail("输入已预约或没有空间。");
            });
        }
        public ActionResult AddFuel(ItemId item,int requested=1)
        {
            if(!GeneratorBuilt||!NearGenerator)return Fail("靠近发电机正面后补料。");
            if(item!=ItemId.WasteWood&&item!=ItemId.WoodChips)return Fail("只接受木料与碎屑。");
            return QueueInteraction(()=>{
                int count=Math.Min(Math.Min(requested,Carry.Available(item)),10-FuelCount);
                if(count<=0)return Fail("背包缺燃料或缓冲满。");Carry.TryRemove(item,count);
                if(item==ItemId.WasteWood)fuelWood+=count;else fuelChips+=count;Ignite();return Ok("补料完成。",count);
            });
        }
        public ActionResult TakeAsh()
        {
            if(!NearGenerator)return Fail("靠近发电机正面后取灰渣。");
            return QueueInteraction(()=>{int count=GeneratorResidue.TransferTo(Carry,ItemId.PowerAsh,int.MaxValue);return count>0?Ok("灰渣进入背包。",count):Fail("没有灰渣或背包已满。");});
        }
        public ActionResult OpenBox(ItemId item)
        {
            if(!ItemCatalog.IsOpenable(item)||Carry.Available(item)==0)return Fail("先拾取可直接打开的箱/容器。");
            return QueueInteraction(()=>{
                var instance=tools.FirstOrDefault(t=>t.Item==item&&t.Location=="carry");
                var items=ItemCatalog.DeviceYield(item,instance?.Usable??true);int ticks=items.Sum(p=>p.Amount*ItemCatalog.CapacityTicks(p.Item));
                if(Carry.FreeTicks+ItemCatalog.CapacityTicks(item)<ticks)return Fail("打开后的物品放不下，请先转存。");
                Carry.TryRemove(item,1);Carry.TryAddBatch(items);DiscoverProducts(items);return Ok("已打开，产物固定且只产生一次。");
            });
        }
        private int MoveStock(UnitInventory source,UnitInventory target,string from,string to,ItemId item,int requested)
        {
            int count=source.TransferTo(target,item,requested);MoveInstances(from,to,item,count);if(count>0)discovered.Add(item);return count;
        }
        private void MoveInstances(string from,string to,ItemId item,int count)
        {
            foreach(var tool in tools.Where(t=>t.Location==from&&t.Item==item&&t.ReservedBy==0).OrderBy(t=>t.Id).Take(count)){tool.Location=to;tool.UsingAt=0;}
        }
        private ToolInstance ToolFor(int station,ItemId item)
        {
            string slot=station==1?"benchTools":"dismantleTools";
            bool personal=manualStation==station&&(station==1?AtFront(BenchX,BenchY):AtFront(DismantleX,DismantleY));
            int used=station==1?woodToolId:item==ItemId.Wrench?wrenchToolId:screwToolId;
            var candidates=tools.Where(t=>t.Item==item&&t.Durability>0&&t.ReservedBy==0&&(t.Location==slot||personal&&t.Location=="carry")&&(t.UsingAt==0||t.UsingAt==station));
            return used>0?candidates.FirstOrDefault(t=>t.Id==used):candidates.OrderByDescending(t=>t.Durability).ThenBy(t=>t.Location==slot?0:1).ThenBy(t=>t.Id).FirstOrDefault();
        }
        private void WearTool(int id,int amount)
        {
            var tool=tools.FirstOrDefault(t=>t.Id==id);if(tool==null)return;
            tool.Durability=Math.Max(0,tool.Durability-amount);tool.UsingAt=0;
            if(tool.Durability>0)return;
            var inventory=tool.Location=="carry"?Carry:tool.Location=="warehouse"?Warehouse:tool.Location=="benchTools"?WoodTools:DismantleTools;
            inventory.TryRemove(tool.Item,1);tool.Item=ItemId.BrokenWoodTool;tool.Location="broken";BrokenTools.TryAdd(ItemId.BrokenWoodTool,1);
        }
        private bool CanWear(int id,int amount)
        {
            var tool=tools.FirstOrDefault(t=>t.Id==id);
            return tool==null||tool.Durability>amount||BrokenTools.FreeTicks>=20;
        }
        private bool CanWearDismantleTools()
        {
            int required=tools.Where(t=>(t.Id==screwToolId||t.Id==wrenchToolId)&&t.Durability<=1).Select(t=>t.Id).Distinct().Count();
            return BrokenTools.FreeTicks>=required*20;
        }
        private void ReleaseUnusedTools()
        {
            foreach(var tool in tools)if(tool.UsingAt==1&&!jobActive||tool.UsingAt==2&&!dismantleActive)tool.UsingAt=0;
        }
        private decimal SaleDurabilityFactor(ItemId item,int index)
        {
            if(!ItemCatalog.IsTool(item))return 1;
            var tool=tools.Where(t=>t.Item==item&&t.Location=="warehouse"&&t.ReservedBy==0).OrderBy(t=>t.Id).Skip(index).FirstOrDefault();
            return tool==null?0:tool.Durability/300m;
        }
        public ActionResult Buy(ItemId item,int count=1)
        {
            var price=ItemCatalog.Price(item);if(count<=0||price.buy<=0)return Fail("此物品无前期购买价。");
            long cost=(long)decimal.Ceiling(price.buy*count*100);
            if(cost>cashCents)return Fail("经营现金不足。");
            int inWarehouse=Math.Min(count,Warehouse.AvailableSlots(item));
            var placements=new System.Collections.Generic.List<(int x,int y)>();
            var shadow=new System.Collections.Generic.Dictionary<(int,int),int>(groundCapacity);
            for(int unit=inWarehouse;unit<count;unit++)
            {
                var candidates=Enumerable.Range(6,18).SelectMany(y=>Enumerable.Range(10,18).Select(x=>(x,y)))
                    .Where(p=>!Occupied(p.x,p.y)&&(!shadow.TryGetValue(p,out int ticks)||ticks+ItemCatalog.CapacityTicks(item)<=100))
                    .OrderBy(p=>Distance(p.x,p.y,WarehouseX,WarehouseY)).ThenBy(p=>p.y).ThenBy(p=>p.x).ToArray();
                if(candidates.Length==0)return Fail("仓库及产权地都无采购空间，未扣钱。");
                var cell=candidates[0];placements.Add(cell);shadow.TryGetValue(cell,out int used);shadow[cell]=used+ItemCatalog.CapacityTicks(item);
            }
            cashCents-=cost;if(inWarehouse>0)Warehouse.TryAdd(item,inWarehouse);discovered.Add(item);
            if(ItemCatalog.IsTool(item))for(int i=0;i<inWarehouse;i++)tools.Add(new ToolInstance{Id=nextToolId++,Item=item,Location="warehouse"});
            foreach(var cell in placements)Place(item,1,null,cell.x,cell.y);
            return Ok(placements.Count==0?"采购送入仓库。":"采购完成，仓库满的部分在产权地显示。",count);
        }
    }
}
