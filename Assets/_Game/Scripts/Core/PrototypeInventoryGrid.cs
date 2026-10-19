using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class InventoryGridSlot
    {
        public ItemId item;
        public int amount;
        public InventoryGridSlot Copy()=>new InventoryGridSlot{item=item,amount=amount};
    }
    [Serializable] public sealed class InventoryGridLayout
    {
        public const int StackLimit=64;
        public List<InventoryGridSlot> slots=new List<InventoryGridSlot>();
        public InventoryGridLayout Copy()=>new InventoryGridLayout{slots=(slots??new List<InventoryGridSlot>()).Select(s=>s.Copy()).ToList()};
        public void Sync(UnitInventory inventory,int minimum,InventoryHand hand,bool warehouse)
        {
            if(slots==null)slots=new List<InventoryGridSlot>();
            while(slots.Count<minimum)slots.Add(new InventoryGridSlot());
            foreach(var slot in slots)if(slot.amount<0||slot.amount>StackLimit||!Enum.IsDefined(typeof(ItemId),slot.item))slot.amount=0;
            foreach(ItemId item in ItemCatalog.All)
            {
                int wanted=inventory.Count(item)-(hand!=null&&hand.warehouse==warehouse&&hand.item==item?hand.amount:0);
                int difference=wanted-slots.Where(s=>s.item==item).Sum(s=>s.amount);
                if(difference<0)
                    for(int i=slots.Count-1;i>=0&&difference<0;i--)
                        if(slots[i].item==item){int remove=Math.Min(-difference,slots[i].amount);slots[i].amount-=remove;difference+=remove;}
                while(difference>0)
                {
                    var slot=slots.FirstOrDefault(s=>s.amount>0&&s.item==item&&s.amount<StackLimit)??slots.FirstOrDefault(s=>s.amount==0);
                    if(slot==null){slot=new InventoryGridSlot();slots.Add(slot);}
                    int add=Math.Min(difference,StackLimit-slot.amount);slot.item=item;slot.amount+=add;difference-=add;
                }
            }
            int pageSize=warehouse?54:27;
            while(slots.Count%pageSize!=0)slots.Add(new InventoryGridSlot());
        }
    }
    public sealed class InventoryHand
    {
        public bool warehouse;
        public ItemId item;
        public int amount,originSlot;
        internal int token;
    }
    public sealed partial class PrototypeSimulation
    {
        public InventoryGridLayout CarryGrid {get;private set;}=new InventoryGridLayout();
        public InventoryGridLayout WarehouseGrid {get;private set;}=new InventoryGridLayout();
        public InventoryHand Hand {get;private set;}
        public bool InventoryTransferPending {get;private set;}
        private UnitInventory GridStock(bool warehouse)=>warehouse?Warehouse:Carry;
        public InventoryGridLayout Grid(bool warehouse)=>warehouse?WarehouseGrid:CarryGrid;
        private string GridKey(bool warehouse)=>warehouse?"warehouse":"carry";
        public void SyncInventoryGrids()
        {CarryGrid.Sync(Carry,27,Hand,false);WarehouseGrid.Sync(Warehouse,54,Hand,true);}
        private bool ToolInUse(bool warehouse,ItemId item)=>tools.Any(t=>t.Item==item&&t.Location==GridKey(warehouse)&&(t.UsingAt!=0||t.ReservedBy!=0));
        private void ReserveHandTools()
        {
            if(Hand==null)return;
            foreach(var tool in tools.Where(t=>t.Item==Hand.item&&t.Location==GridKey(Hand.warehouse)&&t.ReservedBy==0&&t.UsingAt==0).Take(Hand.amount))tool.ReservedBy=-Hand.token;
        }
        private void ReleaseHandTools()
        {if(Hand!=null)foreach(var tool in tools.Where(t=>t.ReservedBy==-Hand.token))tool.ReservedBy=0;}

        public ActionResult TakeInventoryStack(bool warehouse,int index,bool half=false)
        {
            if(Failed||Hand!=null||Interacting)return Fail("请先完成当前操作或放下手中物品。");
            if(warehouse&&!NearWarehouse)return Fail("需要靠近仓库门前才能取用。");
            SyncInventoryGrids();var layout=Grid(warehouse);
            if(index<0||index>=layout.slots.Count||layout.slots[index].amount==0)return Fail("这是空格。");
            var slot=layout.slots[index];var stock=GridStock(warehouse);
            int amount=Math.Min(half?(slot.amount+1)/2:slot.amount,stock.Available(slot.item));
            if(amount<=0||ToolInUse(warehouse,slot.item))return Fail("物品已预约或工具正在使用。");
            int token=stock.Reserve(new ItemAmount(slot.item,amount));
            if(token==0)return Fail("这叠物品当前无法移动。");
            Hand=new InventoryHand{warehouse=warehouse,item=slot.item,amount=amount,token=token,originSlot=index};
            ReserveHandTools();
            slot.amount-=amount;
            return Ok($"拿起 {ItemCatalog.Name(Hand.item)} ×{amount}；点击格子放下，右键放一份。",amount);
        }
        public void ReturnInventoryHand()
        {
            if(Hand==null)return;
            if(InventoryTransferPending){CancelInteraction();InventoryTransferPending=false;}
            ReleaseHandTools();GridStock(Hand.warehouse).Release(Hand.token);
            var layout=Grid(Hand.warehouse);
            if(Hand.originSlot>=0&&Hand.originSlot<layout.slots.Count)
            {
                var slot=layout.slots[Hand.originSlot];
                if(slot.amount==0||slot.item==Hand.item)
                {int restore=Math.Min(Hand.amount,InventoryGridLayout.StackLimit-slot.amount);slot.item=Hand.item;slot.amount+=restore;}
            }
            Hand=null;SyncInventoryGrids();
        }
        public ActionResult PlaceInventoryStack(bool warehouse,int index,bool one=false)
        {
            if(Hand==null||Interacting||Failed)return Fail("当前没有可放下的物品，或交互尚未完成。");
            if((warehouse||Hand.warehouse)&&!NearWarehouse)return Fail("需要靠近仓库门前才能存取。");
            SyncInventoryGrids();
            if(index<0||index>=Grid(warehouse).slots.Count)return Fail("请选择物品栏中的格子。");
            if(warehouse==Hand.warehouse)return ApplyInventoryPlacement(warehouse,index,one);
            var result=QueueInteraction(()=>{InventoryTransferPending=false;return ApplyInventoryPlacement(warehouse,index,one);});
            InventoryTransferPending=result.Success;return result;
        }
        private ActionResult ApplyInventoryPlacement(bool warehouse,int index,bool one)
        {
            if(Hand==null)return Fail("手中物品已经放回。");
            if((warehouse||Hand.warehouse)&&!NearWarehouse)return Fail("已经离开仓库，物品仍在手中。");
            SyncInventoryGrids();var slot=Grid(warehouse).slots[index];
            var source=GridStock(Hand.warehouse);var target=GridStock(warehouse);
            if(slot.amount>0&&slot.item!=Hand.item)
            {
                if(one)return Fail("右键只能放到空格或相同物品上。");
                if(target.Available(slot.item)<slot.amount||ToolInUse(warehouse,slot.item))return Fail("目标物品已预约或正在使用。");
                int handTicks=Hand.amount*ItemCatalog.CapacityTicks(Hand.item),otherTicks=slot.amount*ItemCatalog.CapacityTicks(slot.item);
                if(source!=target&&(source.FreeTicks+handTicks<otherTicks||target.FreeTicks+otherTicks<handTicks))return Fail("容量不足，无法交换这两叠物品。");
                ItemId oldItem=Hand.item;int oldAmount=Hand.amount;ItemId other=slot.item;int otherAmount=slot.amount;
                ReleaseHandTools();source.Release(Hand.token);
                if(source!=target)
                {
                    source.TryRemove(oldItem,oldAmount);target.TryRemove(other,otherAmount);
                    source.TryAdd(other,otherAmount);target.TryAdd(oldItem,oldAmount);
                    MoveInstances(GridKey(Hand.warehouse),GridKey(warehouse),oldItem,oldAmount);
                    MoveInstances(GridKey(warehouse),GridKey(Hand.warehouse),other,otherAmount);
                }
                slot.item=oldItem;slot.amount=oldAmount;Hand.item=other;Hand.amount=otherAmount;
                Hand.token=source.Reserve(new ItemAmount(other,otherAmount));
                ReserveHandTools();
                return Ok("已交换两叠物品。",oldAmount);
            }
            int amount=Math.Min(one?1:Hand.amount,InventoryGridLayout.StackLimit-slot.amount);
            if(source!=target)amount=Math.Min(amount,target.AvailableSlots(Hand.item));
            if(amount<=0)return Fail("目标格已满，或容器容量不足。物品仍在手中。");
            ReleaseHandTools();source.Release(Hand.token);
            if(source!=target)amount=MoveStock(source,target,GridKey(Hand.warehouse),GridKey(warehouse),Hand.item,amount);
            slot.item=Hand.item;slot.amount+=amount;Hand.amount-=amount;
            if(Hand.amount==0)Hand=null;else {Hand.token=source.Reserve(new ItemAmount(Hand.item,Hand.amount));ReserveHandTools();}
            return Ok($"已放下 {amount} 份。",amount);
        }
        public ActionResult QuickTransferStack(bool warehouse,int index)
        {
            if(!NearWarehouse)return Fail("靠近仓库后，Shift点击可在背包与仓库之间快速转移。");
            var picked=TakeInventoryStack(warehouse,index);if(!picked.Success)return picked;
            var queued=QueueInteraction(()=>{
                InventoryTransferPending=false;
                var hand=Hand;
                if(hand==null||!NearWarehouse){ReturnInventoryHand();return Fail("已经离开仓库，物品已放回。");}
                var source=GridStock(hand.warehouse);ReleaseHandTools();source.Release(hand.token);
                int moved=MoveStock(source,GridStock(!hand.warehouse),GridKey(hand.warehouse),GridKey(!hand.warehouse),hand.item,hand.amount);
                hand.amount-=moved;
                if(hand.amount>0)hand.token=source.Reserve(new ItemAmount(hand.item,hand.amount));else Hand=null;
                ReturnInventoryHand();SyncInventoryGrids();
                return moved>0?Ok($"已快速转移 {moved} 份。",moved):Fail("目标容量不足，物品已放回。");
            });
            InventoryTransferPending=queued.Success;if(!queued.Success)ReturnInventoryHand();return queued;
        }
    }
}
