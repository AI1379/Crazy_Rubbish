using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class InventoryGridTests
    {
        private static PrototypeSimulation Fresh()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false},4271,true);
            var s=m.Capture();s.playerX=PrototypeSimulation.WarehouseX;s.playerY=PrototypeSimulation.WarehouseY;
            return PrototypeSimulation.Restore(s);
        }
        private static int Slot(PrototypeSimulation m,bool warehouse,ItemId item)
        {m.SyncInventoryGrids();return m.Grid(warehouse).slots.FindIndex(s=>s.amount>0&&s.item==item);}
        private static void Finish(PrototypeSimulation m){m.Advance(0.55);Assert.That(m.LastInteraction.Success,Is.True,m.LastInteraction.Message);}
        [Test]public void RightClickSplitsOddStackAndPlacesOneWithoutChangingCapacity()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.Food,5);int slot=Slot(m,false,ItemId.Food);
            Assert.That(m.TakeInventoryStack(false,slot,true).Success,Is.True);Assert.That(m.Hand.amount,Is.EqualTo(3));
            Assert.That(m.CarryGrid.slots[slot].amount,Is.EqualTo(2));Assert.That(m.Carry.Used,Is.EqualTo(5));
            Assert.That(m.PlaceInventoryStack(false,10,true).Success,Is.True);Assert.That(m.Hand.amount,Is.EqualTo(2));
            Assert.That(m.CarryGrid.slots[10].amount,Is.EqualTo(1));m.ReturnInventoryHand();
            Assert.That(m.CarryGrid.slots[slot].amount,Is.EqualTo(4));Assert.That(m.Carry.Count(ItemId.Food),Is.EqualTo(5));Assert.That(m.Carry.Reserved(ItemId.Food),Is.Zero);
        }
        [Test]public void TransferIsTimedAndClosingReturnsTheReservedHandWithoutLoss()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.WasteWood,4);m.TakeInventoryStack(false,Slot(m,false,ItemId.WasteWood));
            Assert.That(m.PlaceInventoryStack(true,0).Success,Is.True);Assert.That(m.Warehouse.UsedTicks,Is.Zero);
            m.ReturnInventoryHand();m.Advance(1);
            Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.Zero);Assert.That(m.Hand,Is.Null);
            m.TakeInventoryStack(false,Slot(m,false,ItemId.WasteWood));m.PlaceInventoryStack(true,0);Finish(m);
            Assert.That(m.Carry.Count(ItemId.WasteWood),Is.Zero);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Hand,Is.Null);
        }
        [Test]public void FullTargetDoesNotDeleteTheHeldStack()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.WasteWood,3);m.Warehouse.TryAdd(ItemId.Food,80);
            m.TakeInventoryStack(false,Slot(m,false,ItemId.WasteWood));m.PlaceInventoryStack(true,20);m.Advance(0.55);
            Assert.That(m.LastInteraction.Success,Is.False);Assert.That(m.Hand.amount,Is.EqualTo(3));
            m.ReturnInventoryHand();Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(3));Assert.That(m.Warehouse.Used,Is.EqualTo(80));
        }
        [Test]public void ShiftTransferMovesOnlyWhatFitsAndReturnsTheRemainder()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.WasteWood,4);m.Warehouse.TryAdd(ItemId.Food,78);
            Assert.That(m.QuickTransferStack(false,Slot(m,false,ItemId.WasteWood)).Success,Is.True);Finish(m);
            Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(2));Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(2));Assert.That(m.Hand,Is.Null);
            Assert.That(m.Carry.Reserved(ItemId.WasteWood),Is.Zero);
        }
        [Test]public void CrossContainerSwapPreservesBothKindsAndTheirCapacity()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.WasteWood,4);m.Warehouse.TryAdd(ItemId.Food,2);
            m.TakeInventoryStack(false,Slot(m,false,ItemId.WasteWood));m.PlaceInventoryStack(true,Slot(m,true,ItemId.Food));Finish(m);
            Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Hand.item,Is.EqualTo(ItemId.Food));
            m.ReturnInventoryHand();Assert.That(m.Carry.Count(ItemId.Food),Is.EqualTo(2));Assert.That(m.Warehouse.Count(ItemId.Food),Is.Zero);
        }
        [Test]public void ReorderedSplitSlotsSurviveJsonSaveAndLoad()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.Food,6);m.TakeInventoryStack(false,Slot(m,false,ItemId.Food),true);m.PlaceInventoryStack(false,12);
            var restored=PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));restored.SyncInventoryGrids();
            Assert.That(restored.CarryGrid.slots[12].amount,Is.EqualTo(3));Assert.That(restored.CarryGrid.slots[0].amount,Is.EqualTo(3));
        }
        [Test]public void HeldItemsCannotBeConsumedOrSavedAsOrdinaryFreeStock()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.Food,4);m.TakeInventoryStack(false,Slot(m,false,ItemId.Food));
            Assert.That(m.Carry.Available(ItemId.Food),Is.Zero);Assert.That(m.Carry.TryRemove(ItemId.Food,1),Is.False);
            Assert.Throws<InvalidOperationException>(()=>m.Capture());m.ReturnInventoryHand();Assert.DoesNotThrow(()=>m.Capture());
        }
        [Test]public void RemoteWarehouseAndReservedStockCannotBeTaken()
        {
            var m=Fresh();m.Warehouse.TryAdd(ItemId.WasteWood,4);int token=m.Warehouse.Reserve(new ItemAmount(ItemId.WasteWood,4));
            Assert.That(m.TakeInventoryStack(true,Slot(m,true,ItemId.WasteWood)).Success,Is.False);m.Warehouse.Release(token);
            PrototypeStartingScenario.MoveTo(m,8,6);Assert.That(m.TakeInventoryStack(true,Slot(m,true,ItemId.WasteWood)).Success,Is.False);
        }
        [Test]public void PickingAnExactWholePileRemovesItAndPartialPickupReportsRemainder()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false},4271,true);m.Advance(9.05);
            var food=m.Ground.First(p=>p.Item==ItemId.Food);PrototypeStartingScenario.MoveTo(m,food.X,food.Y);
            m.Carry.TryAdd(ItemId.WasteWood,7);m.PickUp(int.MaxValue,null,food.Id);Finish(m);
            Assert.That(food.Amount,Is.GreaterThan(0));Assert.That(m.LastInteraction.Message,Does.Contain("剩余"));
            m.Carry.TryRemove(ItemId.WasteWood,7);m.PickUp(int.MaxValue,null,food.Id);Finish(m);
            Assert.That(m.Ground.Any(p=>p.Id==food.Id),Is.False);Assert.That(m.LastInteraction.Message,Does.Contain("拿空"));
        }
        [Test]public void NaturalArrivalsPreferSeparateKindsWhileReceivingCellsRemainEmpty()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false},4271,true);m.Advance(110);
            Assert.That(m.Ground.GroupBy(p=>(p.X,p.Y)).All(g=>g.Select(p=>p.Item).Distinct().Count()==1),Is.True);
        }
        [Test]public void AToolHeldByTheCursorCannotStartAProductionBatch()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            PrototypeStartingScenario.Act(m,()=>m.PickUp(1,ItemId.WoodHandSaw));
            m.Warehouse.TryAdd(ItemId.WasteWood,4);m.Warehouse.TryAdd(ItemId.RecoveredFastener,2);m.BuildBench();
            PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);m.WoodInputStock.TryAdd(ItemId.WasteWood,4);m.ToggleWork();
            m.TakeInventoryStack(false,Slot(m,false,ItemId.WoodHandSaw));m.Advance(1);
            Assert.That(m.JobActive,Is.False);m.ReturnInventoryHand();m.Advance(1);Assert.That(m.JobActive,Is.True);
        }
        [Test]public void MovingASawBetweenGridsPreservesItsInstanceAndReleasesTheCursorReservation()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});PrototypeStartingScenario.Act(m,()=>m.PickUp(1,ItemId.WoodHandSaw));
            int id=m.Tools.Single(t=>t.Item==ItemId.WoodHandSaw).Id;
            PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
            m.TakeInventoryStack(false,Slot(m,false,ItemId.WoodHandSaw));m.PlaceInventoryStack(true,0);Finish(m);
            var saw=m.Tools.Single(t=>t.Id==id);Assert.That(saw.Location,Is.EqualTo("warehouse"));Assert.That(saw.ReservedBy,Is.Zero);Assert.That(saw.Durability,Is.EqualTo(300));
        }
        [Test]public void ANewKindAfterManySplitStacksRemainsAccessibleOnAnExtraPage()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.DrinkingWater,54);
            m.CarryGrid.slots=Enumerable.Range(0,27).Select(_=>new InventoryGridSlot{item=ItemId.DrinkingWater,amount=2}).ToList();
            m.Carry.TryAdd(ItemId.Food,1);m.SyncInventoryGrids();
            Assert.That(m.CarryGrid.slots.Count,Is.EqualTo(54));
            Assert.That(m.TakeInventoryStack(false,27).Success,Is.True);
            Assert.That(m.PlaceInventoryStack(false,53).Success,Is.True);
            Assert.That(m.CarryGrid.slots[53].item,Is.EqualTo(ItemId.Food));Assert.That(m.Carry.Count(ItemId.DrinkingWater),Is.EqualTo(54));
        }
    }
}
