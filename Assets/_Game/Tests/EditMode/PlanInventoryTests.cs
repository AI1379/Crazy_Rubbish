using System;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanInventoryTests
    {
        private static PlanSimulation Fresh()
        {
            var c=JsonUtility.FromJson<PlanConfig>(Resources.Load<TextAsset>("Plan/RuntimeConfig").text);c.Validate();var m=new PlanSimulation(c);
            var f=m.Front(m.State.sites.First(s=>s.type=="warehouse"));m.State.player.x=f.x;m.State.player.y=f.y;return m;
        }
        private static int Warehouse(PlanSimulation m)=>m.State.sites.First(s=>s.type=="warehouse").id;
        private static int Slot(PlanSimulation m,int site,string item,string quality=null)
        {
            var g=m.InventoryGrid(site);for(int i=0;i<g.slots.Count;i++){var s=m.InventorySlotStack(site,i);if(s!=null&&s.item==item&&(quality==null||s.quality==quality))return i;}throw new Exception("Missing grid item: "+item);
        }
        private static PlanSimulation Copy(PlanSimulation m)=>new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));
        [Test] public void HalfSinglePlacementAndCloseConserveStockAndLayout()
        {
            var m=Fresh();m.State.backpack.Add("boards",10*PlanStock.Scale);int slot=Slot(m,0,"boards");m.TakeInventorySlot(0,slot,true);
            Assert.That(m.InventoryHand.ticks,Is.EqualTo(5*PlanStock.Scale));m.PlaceInventorySlot(0,1,true);
            Assert.That(m.InventoryHand.ticks,Is.EqualTo(4*PlanStock.Scale));m.ReturnInventoryHand();
            Assert.That(m.State.backpack.Count("boards"),Is.EqualTo(10*PlanStock.Scale));Assert.That(m.InventoryGrid(0).slots[1].ticks,Is.EqualTo(PlanStock.Scale));Assert.That(m.InventoryHand,Is.Null);
        }
        [Test] public void CrossContainerSwapIsAtomicAndKeepsTheDisplacedStackInHand()
        {
            var m=Fresh();int w=Warehouse(m);m.State.backpack.capacity=100;m.State.backpack.Add("boards",3*PlanStock.Scale);m.State.warehouse.Add("wire",2*PlanStock.Scale);
            int b=Slot(m,0,"boards"),s=Slot(m,w,"wire");m.TakeInventorySlot(0,b);m.PlaceInventorySlot(w,s);
            Assert.That(m.InventoryHand,Is.Not.Null);Assert.That(m.State.warehouse.Count("boards"),Is.EqualTo(3*PlanStock.Scale));m.ReturnInventoryHand();
            Assert.That(m.State.backpack.Count("wire"),Is.EqualTo(2*PlanStock.Scale));Assert.That(m.State.warehouse.Count("wire"),Is.Zero);Assert.That(m.State.backpack.Count("boards"),Is.Zero);
        }
        [Test] public void FullDestinationRejectsMoveWithoutConsumingOrReleasingHand()
        {
            var m=Fresh();int w=Warehouse(m);m.State.backpack.Add("boards",2*PlanStock.Scale);m.State.warehouse.capacity=0;m.TakeInventorySlot(0,Slot(m,0,"boards"));
            Assert.Throws<InvalidOperationException>(()=>m.PlaceInventorySlot(w,0));Assert.That(m.InventoryHand.ticks,Is.EqualTo(2*PlanStock.Scale));m.ReturnInventoryHand();Assert.That(m.State.backpack.Count("boards"),Is.EqualTo(2*PlanStock.Scale));Assert.That(m.State.warehouse.stacks,Is.Empty);
        }
        [Test] public void QuickTransferMovesOnlyWhatFitsAndLeavesTheRemainder()
        {
            var m=Fresh();int w=Warehouse(m);m.State.warehouse.Add("boards",50*PlanStock.Scale);long n=m.QuickInventoryTransfer(w,Slot(m,w,"boards"),0);
            Assert.That(n,Is.GreaterThan(0).And.LessThan(50*PlanStock.Scale));Assert.That(m.State.backpack.Used(m.Config),Is.LessThanOrEqualTo(m.State.backpack.capacity+1e-8));Assert.That(m.State.backpack.Count("boards")+m.State.warehouse.Count("boards"),Is.EqualTo(50*PlanStock.Scale));
        }
        [Test] public void WarehousesRemainSeparateAndRemoteInventoryCannotBeTransferred()
        {
            var m=Fresh();int w=Warehouse(m);var other=new PlanSite{id=m.State.nextId++,type="warehouse",x=35,y=35,input=new PlanStock{capacity=80}};m.State.sites.Add(other);m.State.warehouse.Add("boards",3*PlanStock.Scale);
            Assert.That(m.InventoryStock(other.id).Count("boards"),Is.Zero);Assert.Throws<InvalidOperationException>(()=>m.QuickInventoryTransfer(w,Slot(m,w,"boards"),other.id));
            Assert.That(m.State.warehouse.Count("boards"),Is.EqualTo(3*PlanStock.Scale));Assert.That(other.input.stacks,Is.Empty);
        }
        [Test] public void RawWaterOriginsAndQualitySurviveFractionalTransfer()
        {
            var m=Fresh();int w=Warehouse(m);m.State.backpack.Add("raw_freshwater",PlanStock.Quantity(1.5),1,0,0,null,"qualified_fresh_source");m.State.backpack.Add("raw_freshwater",PlanStock.Quantity(.75),2,0,0,null,"industrial");
            int s=Slot(m,0,"raw_freshwater","industrial");m.QuickInventoryTransfer(0,s,w);
            Assert.That(m.State.warehouse.stacks.Single().quality,Is.EqualTo("industrial"));Assert.That(m.State.warehouse.stacks.Single().origin,Is.EqualTo(2));Assert.That(m.State.warehouse.stacks.Single().ticks,Is.EqualTo(PlanStock.Quantity(.75)));
            Assert.That(m.State.backpack.stacks.Single().quality,Is.EqualTo("qualified_fresh_source"));Assert.That(m.State.backpack.stacks.Single().ticks,Is.EqualTo(PlanStock.Quantity(1.5)));
        }
        [Test] public void ToolsAndConsumableInstancesCannotBeSplitIntoParts()
        {
            var m=Fresh();int instance=m.State.nextId++;m.State.backpack.Add("handsaw_wood",PlanStock.Scale,1,123.5,instance);m.TakeInventorySlot(0,Slot(m,0,"handsaw_wood"),true);
            Assert.That(m.InventoryHand.ticks,Is.EqualTo(PlanStock.Scale));Assert.That(m.State.backpack.Count("handsaw_wood"),Is.Zero);m.ReturnInventoryHand();var tool=m.State.backpack.stacks.Single();Assert.That(tool.instance,Is.EqualTo(instance));Assert.That(tool.durability,Is.EqualTo(123.5));Assert.That(tool.reserved,Is.Zero);
        }
        [Test] public void ReturningHandReleasesOnlyItsOwnReservation()
        {
            var m=Fresh();m.State.backpack.Add("boards",5*PlanStock.Scale);m.State.backpack.Reserve("boards",3*PlanStock.Scale);m.TakeInventorySlot(0,Slot(m,0,"boards"),true);m.ReturnInventoryHand();
            Assert.That(m.State.backpack.Total("boards"),Is.EqualTo(5*PlanStock.Scale));Assert.That(m.State.backpack.stacks.Single().reserved,Is.EqualTo(3*PlanStock.Scale));
        }
        [Test] public void FoodAndWaterProtectionIsNotBypassedByTheGrid()
        {
            var m=Fresh();int w=Warehouse(m);long keep=m.Protected("water");m.State.warehouse.Add("water",keep);int index=Slot(m,w,"water");
            Assert.Throws<InvalidOperationException>(()=>m.TakeInventorySlot(w,index));m.State.warehouse.Add("water",PlanStock.Scale);m.TakeInventorySlot(w,index);
            Assert.That(m.InventoryHand.ticks,Is.EqualTo(PlanStock.Scale));m.PlaceInventorySlot(0,0);Assert.That(m.Stored("water"),Is.EqualTo(keep));
        }
        [Test] public void GridLayoutRoundtripAndOlderS6SavesKeepTheSameLedger()
        {
            var m=Fresh();m.State.backpack.Add("boards",6*PlanStock.Scale);m.TakeInventorySlot(0,Slot(m,0,"boards"),true);m.PlaceInventorySlot(0,4);var copy=Copy(m);
            Assert.That(copy.InventoryGrid(0).slots[4].ticks,Is.EqualTo(3*PlanStock.Scale));Assert.That(copy.State.backpack.Count("boards"),Is.EqualTo(6*PlanStock.Scale));
            var old=m.Capture();old.inventoryGrids=null;var restored=new PlanSimulation(m.Config,1,old);Assert.That(restored.InventoryGrid(0).slots.Sum(s=>s.ticks),Is.EqualTo(6*PlanStock.Scale));
        }
        [Test] public void CapturingAHandCannotWriteDanglingReservations()
        {
            var m=Fresh();m.State.backpack.Add("boards",PlanStock.Scale);m.TakeInventorySlot(0,Slot(m,0,"boards"));Assert.Throws<InvalidOperationException>(()=>m.Capture());m.ReturnInventoryHand();Assert.DoesNotThrow(()=>Copy(m));
        }
        [Test] public void QuoteIsReadOnlyAndMatchesTheReleaseSaleAcrossOrigins()
        {
            var m=Fresh();m.State.warehouse.Add("boards",3*PlanStock.Scale,1);m.State.warehouse.Add("boards",2*PlanStock.Scale,2);string before=JsonUtility.ToJson(m.Capture());var q=m.QuoteInventorySale("boards",4*PlanStock.Scale);
            Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));long cash=m.State.cash;m.Sell("boards",q.ticks);Assert.That(m.State.cash-cash,Is.EqualTo(q.cents));
        }
        [Test] public void ExactGroundPickupKeepsItsOwnQualityThroughAMidPickupSave()
        {
            var m=Fresh();m.State.ground.Clear();m.State.player.x=9;m.State.player.y=4;var g=new PlanGround{id=m.State.nextId++,x=9,y=4};m.State.ground.Add(g);
            g.stock.Add("raw_freshwater",2*PlanStock.Scale,1,0,0,null,"qualified_fresh_source");g.stock.Add("raw_freshwater",PlanStock.Quantity(1.5),2,0,0,null,"industrial");m.SetPaused(false);m.PickInventoryGround(g.id,1,PlanStock.Quantity(1.5));m.Advance(.3);var copy=Copy(m);copy.Advance(.3);
            var stack=copy.State.backpack.stacks.Single();Assert.That(stack.quality,Is.EqualTo("industrial"));Assert.That(stack.origin,Is.EqualTo(2));Assert.That(stack.ticks,Is.EqualTo(PlanStock.Quantity(1.5)));Assert.That(copy.State.ground.Single().stock.stacks.Single().quality,Is.EqualTo("qualified_fresh_source"));
        }
        [Test] public void ChangingACommandReleasesTheExactGroundReservation()
        {
            var m=Fresh();m.State.ground.Clear();m.State.player.x=9;m.State.player.y=4;var g=new PlanGround{id=m.State.nextId++,x=9,y=4};m.State.ground.Add(g);g.stock.Add("raw_freshwater",2*PlanStock.Scale,1,0,0,null,"qualified_fresh_source");g.stock.Add("raw_freshwater",2*PlanStock.Scale,2,0,0,null,"industrial");g.stock.stacks[0].reserved=PlanStock.Scale;m.SetPaused(false);m.PickInventoryGround(g.id,1,PlanStock.Scale);m.CommandMove(new[]{0},9,5);
            Assert.That(g.stock.stacks[0].reserved,Is.EqualTo(PlanStock.Scale));Assert.That(g.stock.stacks[1].reserved,Is.Zero);
        }
        [Test] public void ConstructionShoppingQuotesAndPurchasesOnlyTheMissingMaterials()
        {
            var m=Fresh();m.State.cash=100000;m.State.backpack.Add("waste_wood",2*PlanStock.Scale);string before=JsonUtility.ToJson(m.Capture());var quote=m.QuoteMissingBuildMaterials("wood");
            Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));Assert.That(quote.problem,Is.Null);long cash=m.State.cash;m.BuyMissingBuildMaterials("wood");
            Assert.That(cash-m.State.cash,Is.EqualTo(quote.cents));Assert.That(m.State.backpack.Count("waste_wood")+m.Stored("waste_wood"),Is.EqualTo(4*PlanStock.Scale));Assert.That(m.Stored("fasteners"),Is.EqualTo(2*PlanStock.Scale));Assert.That(m.State.sites.Count(a=>a.type=="wood"),Is.Zero);
        }
        [Test] public void ConstructionShoppingRejectsTheWholePurchaseWhenTheWarehouseIsFull()
        {
            var m=Fresh();m.State.cash=100000;m.State.warehouse.capacity=0;string before=JsonUtility.ToJson(m.Capture());Assert.Throws<InvalidOperationException>(()=>m.BuyMissingBuildMaterials("wood"));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void SameBagSwapAndPartialCrossStackPlacementConserveBothObjects()
        {
            var m=Fresh();m.State.backpack.capacity=1000;m.State.warehouse.capacity=1000;m.State.backpack.Add("boards",64*PlanStock.Scale);m.State.warehouse.Add("boards",60*PlanStock.Scale);int w=Warehouse(m);
            m.TakeInventorySlot(0,Slot(m,0,"boards"));m.PlaceInventorySlot(w,Slot(m,w,"boards"));Assert.That(m.InventoryHand.ticks,Is.EqualTo(60*PlanStock.Scale));m.ReturnInventoryHand();Assert.That(m.State.warehouse.Count("boards"),Is.EqualTo(64*PlanStock.Scale));Assert.That(m.State.backpack.Count("boards"),Is.EqualTo(60*PlanStock.Scale));
            int tool=m.State.nextId++;m.State.backpack.Add("handsaw_wood",PlanStock.Scale,1,123.5,tool);m.TakeInventorySlot(0,Slot(m,0,"boards"),true);m.PlaceInventorySlot(0,Slot(m,0,"handsaw_wood"));m.ReturnInventoryHand();Assert.That(m.State.backpack.Count("boards"),Is.EqualTo(60*PlanStock.Scale));Assert.That(m.State.backpack.stacks.Single(a=>a.instance==tool).durability,Is.EqualTo(123.5));
        }
        [Test] public void PackedPayloadAndUnfinishedBatchStateSurviveGridTransferAndDiskShape()
        {
            var m=Fresh();m.State.backpack.capacity=1000;int w=Warehouse(m);var def=m.Config.items.First(i=>i.payload.Length>0);var stack=new PlanStack{item=def.id,ticks=PlanStock.Scale,instance=m.State.nextId++,origin=2,resumeStage=1,resumeProgress=12.5,payload=new[]{PlanPackedStack.FromStack(new PlanStack{item="raw_freshwater",ticks=1234,origin=2,quality="industrial"})}};m.State.backpack.stacks.Add(stack);
            m.QuickInventoryTransfer(0,Slot(m,0,def.id),w);var copy=Copy(m);var restored=copy.State.warehouse.stacks.Single();Assert.That(restored.instance,Is.EqualTo(stack.instance));Assert.That(restored.resumeProgress,Is.EqualTo(12.5));Assert.That(restored.payload.Single().quality,Is.EqualTo("industrial"));Assert.That(restored.payload.Single().ticks,Is.EqualTo(1234));
        }
    }
}
