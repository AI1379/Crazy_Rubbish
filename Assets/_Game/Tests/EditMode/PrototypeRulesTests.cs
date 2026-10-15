using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CrazyRubbish.Tests
{
    public sealed class PrototypeRulesTests
    {
        // These focused pre-demo rules isolate player consumption and optional living deliveries.
        // Community and complete-demo tests below run with the real defaults.
        private static PrototypeSimulation Fresh(bool input=false)
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=input});m.PlayerNeeds.nextMeal=double.MaxValue;m.SetLivingDelivery(false);return m;
        }
        private static void Move(PrototypeSimulation m,int x,int y)=>PrototypeStartingScenario.MoveTo(m,x,y);
        private static void Act(PrototypeSimulation m,Func<ActionResult> action)=>PrototypeStartingScenario.Act(m,action);
        private static void Collect(PrototypeSimulation m,ItemId item,int amount)=>PrototypeStartingScenario.Collect(m,item,amount);
        private static PrototypeSimulation Ready(bool feed=true)
        {
            var m=Fresh();Collect(m,ItemId.WoodHandSaw,1);Collect(m,ItemId.RecoveredFastener,2);Collect(m,ItemId.WasteWood,12);
            Assert.That(m.BuildBench().Success,Is.True);
            if(feed)
            {
                while(m.WoodInputStock.Count(ItemId.WasteWood)<8)
                {
                    Move(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(m,()=>m.Withdraw(ItemId.WasteWood,8-m.WoodInputStock.Count(ItemId.WasteWood)));
                    Move(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(m,()=>m.DeliverInput());
                }
            }
            Move(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);return m;
        }
        private static PrototypeSimulation Powered()
        {
            var m=Ready(false);Collect(m,ItemId.SteelPart,6);Collect(m,ItemId.ThermalModule,1);Collect(m,ItemId.GeneratorModule,1);Collect(m,ItemId.RecoveredWire,2);
            Assert.That(m.BuildGenerator().Success,Is.True);
            Collect(m,ItemId.RecoveredWire,1);Assert.That(m.ConnectCommunityPower().Success,Is.True);
            Move(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(m,()=>m.Withdraw(ItemId.WasteWood,2));
            Move(m,PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY);Act(m,()=>m.AddFuel(ItemId.WasteWood,2));return m;
        }
        private static PrototypeSimulation Roundtrip(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        [Test]public void WeightedCapacityAndOverflowAreExact()
        {
            var s=new UnitInventory(8);Assert.That(s.TryAdd(ItemId.ThermalModule,1),Is.True);Assert.That(s.TryAdd(ItemId.DrinkingWater,40),Is.True);
            Assert.That(s.UsedTicks,Is.EqualTo(160));Assert.That(s.TryAdd(ItemId.DrinkingWater,1),Is.False);Assert.That(s.TryRemove(ItemId.DrinkingWater,-1),Is.False);
        }
        [Test]public void ReservationDoesNotConsumeAndPreventsDoubleSpending()
        {
            var s=new UnitInventory(10);s.TryAdd(ItemId.WasteWood,8);int token=s.Reserve(new ItemAmount(ItemId.WasteWood,4));
            Assert.That(token,Is.Not.Zero);Assert.That(s.Count(ItemId.WasteWood),Is.EqualTo(8));Assert.That(s.Available(ItemId.WasteWood),Is.EqualTo(4));
            Assert.That(s.TryRemove(ItemId.WasteWood,5),Is.False);Assert.That(s.Commit(token),Is.True);Assert.That(s.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(s.Commit(token),Is.False);
        }
        [Test]public void CancellingReservationReturnsAvailabilityWithoutCreatingStock()
        {
            var s=new UnitInventory(10);s.TryAdd(ItemId.WasteWood,4);int token=s.Reserve(new ItemAmount(ItemId.WasteWood,4));s.Release(token);
            Assert.That(s.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(s.Available(ItemId.WasteWood),Is.EqualTo(4));
        }
        [Test]public void DuplicateCostsAndMultipleOutputsStayAtomic()
        {
            var s=new UnitInventory(4);s.TryAdd(ItemId.WasteWood,4);
            Assert.That(s.Reserve(new ItemAmount(ItemId.WasteWood,3),new ItemAmount(ItemId.WasteWood,2)),Is.Zero);
            var output=new UnitInventory(3);Assert.That(output.TryAddBatch(new ItemAmount(ItemId.Plank,3),new ItemAmount(ItemId.WoodChips,1)),Is.False);Assert.That(output.UsedTicks,Is.Zero);
        }
        [Test]public void ReservedTransferWaitsForWholeDestinationCapacity()
        {
            var source=new UnitInventory(8);source.TryAdd(ItemId.WasteWood,8);int token=source.Reserve(new ItemAmount(ItemId.WasteWood,8));
            Assert.That(source.TransferReserved(token,new UnitInventory(7)),Is.False);Assert.That(source.Count(ItemId.WasteWood),Is.EqualTo(8));
            var target=new UnitInventory(8);Assert.That(source.TransferReserved(token,target),Is.True);Assert.That(target.Count(ItemId.WasteWood),Is.EqualTo(8));
        }
        [Test]public void NewSceneAndFixedSourcesMatchS5()
        {
            var m=Fresh();Assert.That(m.Settings.mapWidth,Is.EqualTo(32));Assert.That(m.Settings.mapHeight,Is.EqualTo(28));Assert.That(m.PlayerX,Is.EqualTo(8));Assert.That(m.PlayerY,Is.EqualTo(6));
            Assert.That(m.Ground.Where(p=>p.Item==ItemId.OldCutter).Sum(p=>p.Amount),Is.EqualTo(2));Assert.That(m.Ground.Where(p=>p.Item==ItemId.OldFurnace).Sum(p=>p.Amount),Is.EqualTo(2));
            Assert.That(m.Ground.Any(p=>p.Item==ItemId.DamagedMotor),Is.False);Assert.That(m.Warehouse.Used,Is.Zero);Assert.That(m.Ground.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(m.Ground.Count));
        }
        [Test]public void LargeDevicesHaveTwoGroundCells()
        {
            var m=Fresh();Assert.That(m.Ground.Where(p=>p.Item==ItemId.OldCutter).All(p=>p.Width==2),Is.True);
            Assert.That(m.Ground.All(p=>!PrototypeSimulation.IsReservedCell(p.X,p.Y)),Is.True);
        }
        [Test]public void CommunityAndHoleAreSolidButGarbageIsWalkable()
        {
            var m=Fresh();Assert.That(m.IsWalkable(12,3),Is.False);Assert.That(m.IsWalkable(17,3),Is.False);Assert.That(m.IsWalkable(2,20),Is.False);
            Assert.That(m.IsWalkable(7,6),Is.True);Assert.That(m.FindPath(8,6,12,5),Is.Not.Null);
        }
        [Test]public void BuiltBodyBlocksPathsAndFrontRemainsReachable()
        {
            var m=Ready();Assert.That(m.IsWalkable(13,8),Is.False);Assert.That(m.IsWalkable(13,7),Is.True);
            var path=m.FindPath(8,6,13,7);int previous=6*32+8;
            foreach(int cell in path){Assert.That(Math.Abs(cell%32-previous%32)+Math.Abs(cell/32-previous/32),Is.EqualTo(1));previous=cell;}
        }
        [Test]public void PlayerCannotWalkThroughWarehouse()
        {
            var m=Fresh();Move(m,10,3);m.MovePlayer(1,0,10);Assert.That(m.PlayerX,Is.LessThan(10.5));Assert.That(m.IsWalkable((int)Math.Floor(m.PlayerX+0.5),3),Is.True);
        }
        [Test]public void PickupUsesHalfASecondAndCanBeCancelled()
        {
            var m=Fresh();Assert.That(m.PickUp(1,ItemId.WoodHandSaw).Success,Is.True);m.Advance(0.25);Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.Zero);
            m.MovePlayer(1,0,0.1);m.Advance(1);Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.Zero);
            Move(m,8,6);Act(m,()=>m.PickUp(1,ItemId.WoodHandSaw));Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.EqualTo(1));
        }
        [Test]public void PauseFreezesInteractionAndOnlyThreeSpeedLevelsAreAllowed()
        {
            var m=Fresh();m.PickUp(1,ItemId.WoodHandSaw);m.SetPaused(true);m.Advance(100);Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.Zero);
            m.SetPaused(false);m.Advance(0.55);Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.EqualTo(1));Assert.Throws<ArgumentOutOfRangeException>(()=>m.SetSpeed(4));m.SetSpeed(3);
        }
        [Test]public void BlueprintReservesUntilResumeAndCancelIsLossless()
        {
            var m=Fresh();Collect(m,ItemId.WasteWood,4);Collect(m,ItemId.RecoveredFastener,2);m.SetPaused(true);
            Assert.That(m.BuildBench().Success,Is.True);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Warehouse.Available(ItemId.WasteWood),Is.Zero);
            Assert.That(m.CancelBlueprints().Success,Is.True);Assert.That(m.Warehouse.Available(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.BenchBuilt,Is.False);
            m.BuildBench();m.SetPaused(false);m.Advance(0.05);Assert.That(m.BenchBuilt,Is.True);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.Zero);
        }
        [Test]public void FailedConstructionDoesNotConsumePartialMaterials()
        {
            var m=Fresh();Collect(m,ItemId.WasteWood,4);Assert.That(m.BuildBench().Success,Is.False);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(4));
        }
        [Test]public void GeneratorHasNoUnspecifiedWorkbenchGate()
        {
            var m=Powered();Assert.That(m.TableBuilt,Is.False);Assert.That(m.HasBeenPowered,Is.True);Assert.That(m.AvailablePower,Is.EqualTo(4));
        }
        [Test]public void WarehouseWoodDoesNotTeleportIntoProduction()
        {
            var m=Ready(false);m.ToggleWork();m.Advance(20);Assert.That(m.State,Is.EqualTo(WorkState.NeedsMaterials));Assert.That(m.CompletedBatches,Is.Zero);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(8));
        }
        [Test]public void BatchInputIsChargedAtCompletionAndOutputStaysAtSite()
        {
            var m=Ready();m.ToggleWork();m.Advance(1);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(8));Assert.That(m.ReservedWood,Is.EqualTo(4));
            PrototypeStartingScenario.WaitUntil(m,()=>m.CompletedBatches==1,90);Assert.That(m.Output.Count(ItemId.Plank),Is.EqualTo(3));Assert.That(m.Output.Count(ItemId.WoodChips),Is.EqualTo(1));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.Zero);
        }
        [Test]public void ForcedCancellationReleasesInProcessStockWithoutHalfProducts()
        {
            var m=Ready();m.ToggleWork();m.Advance(3);int before=m.WoodInputStock.Count(ItemId.WasteWood);
            Assert.That(m.CancelBatch().Success,Is.True);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(before));Assert.That(m.WoodInputStock.Reserved(ItemId.WasteWood),Is.Zero);Assert.That(m.Output.Used,Is.Zero);
        }
        [Test]public void FullOutputStopsBeforeConsumingThirdBatch()
        {
            var m=Ready();m.ToggleWork();PrototypeStartingScenario.WaitUntil(m,()=>m.CompletedBatches==2,180);int input=m.WoodInputStock.Count(ItemId.WasteWood);m.Advance(30);
            Assert.That(m.State,Is.EqualTo(WorkState.OutputBlocked));Assert.That(m.CompletedBatches,Is.EqualTo(2));Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(input));
        }
        [Test]public void ToolInstanceMovesOnceAndWearOccursOncePerCompletedBatch()
        {
            var m=Ready();var saw=m.Tools.Single(t=>t.Item==ItemId.WoodHandSaw);int id=saw.Id;Assert.That(saw.Location,Is.EqualTo("carry"));
            m.ToggleWork();PrototypeStartingScenario.WaitUntil(m,()=>m.CompletedBatches==1,90);Assert.That(saw.Durability,Is.EqualTo(296));
            m.LeaveManualRole();Act(m,()=>m.HandOffTool(ItemId.WoodHandSaw));Assert.That(saw.Id,Is.EqualTo(id));Assert.That(saw.Location,Is.EqualTo("benchTools"));Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.Zero);
        }
        [Test]public void ResidentCannotUseProtagonistsRemotePersonalSaw()
        {
            var m=Powered();m.Recruit();m.AssignWoodworker();if(!m.RunRequested)m.ToggleWork();Move(m,8,5);m.Advance(60);
            Assert.That(m.CompletedBatches,Is.Zero);Assert.That(m.State,Is.EqualTo(WorkState.NeedsTool));Assert.That(m.Carry.Count(ItemId.WoodHandSaw),Is.EqualTo(1));
        }
        [Test]public void PhysicalTransportTakesTimeAndCommitsActualCargo()
        {
            var m=Powered();Move(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(m,()=>m.HandOffTool(ItemId.WoodHandSaw));m.Recruit();m.AssignWoodworker();
            if(!m.RunRequested)m.ToggleWork();Move(m,8,5);m.Advance(0.1);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.Zero);
            PrototypeStartingScenario.WaitUntil(m,()=>m.DeliveredTrips>0,90);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.GreaterThan(0));Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.LessThan(6));
        }
        [Test]public void IdleWorkersAndProducerDoNotReserveSameStockTwice()
        {
            var m=Powered();Move(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(m,()=>m.HandOffTool(ItemId.WoodHandSaw));m.Recruit();m.Recruit();m.AssignWoodworker();if(!m.RunRequested)m.ToggleWork();m.Advance(0.1);
            Assert.That(m.Warehouse.Reserved(ItemId.WasteWood),Is.LessThanOrEqualTo(m.Warehouse.Count(ItemId.WasteWood)));Assert.That(m.WoodInputStock.Used,Is.Zero);
        }
        [Test]public void MotorPresetAndInstanceStatusSurviveSerialization()
        {
            var m=Fresh();var other=Roundtrip(m);Assert.That(other.Ground.Where(p=>p.Item==ItemId.OldCutter).All(p=>p.Usable),Is.True);
            Assert.That(ItemCatalog.DeviceYield(ItemId.OldCutter).Single(p=>p.Item==ItemId.Motor).Amount,Is.EqualTo(1));
            Assert.That(ItemCatalog.DeviceYield(ItemId.OldCutter,false).Any(p=>p.Item==ItemId.Motor),Is.False);
        }
        [Test]public void DirectOpeningDoesNotRequireManufacturingTools()
        {
            var m=Fresh();var box=m.Ground.First(p=>p.Item==ItemId.MouldBox);Move(m,box.X,box.Y);Act(m,()=>m.PickUp(1,ItemId.MouldBox));
            Assert.That(m.Ground.Count(p=>p.Item==ItemId.MouldBox),Is.Zero);Assert.That(m.Ground.Where(p=>p.Item==ItemId.HandleMould).Sum(p=>p.Amount),Is.EqualTo(1));
        }
        [Test]public void RecruitingAndPayrollUseOriginalEmployeeList()
        {
            var m=Powered();for(int i=0;i<5;i++)Assert.That(m.Recruit().Success,Is.True);foreach(var r in m.Residents)r.Needs.nextMeal=double.MaxValue;Assert.That(m.Recruit().Success,Is.False);m.Advance(600);
            Assert.That(m.UnpaidWages,Is.EqualTo(50));Assert.That(m.Residents.All(r=>r.Cash==5),Is.True);Assert.That(m.WageBills[0].ResidentIds.Length,Is.EqualTo(5));
        }
        [Test]public void PayrollTransfersCashWithoutCreatingMoney()
        {
            var m=Powered();m.Recruit();m.Residents[0].Needs.nextMeal=double.MaxValue;m.Warehouse.TryAdd(ItemId.Plank,10);m.Sell(ItemId.Plank,10);m.Advance(600);
            long total=m.CashCents+m.Residents.Sum(r=>r.CashCents);Assert.That(m.PayWages().Success,Is.True);Assert.That(m.Residents[0].Cash,Is.EqualTo(15));Assert.That(m.CashCents+m.Residents.Sum(r=>r.CashCents),Is.EqualTo(total));
        }
        [Test]public void PartialWagesDistributeFairlyAndPreserveDebt()
        {
            var m=Powered();m.Recruit();m.Recruit();foreach(var r in m.Residents)r.Needs.nextMeal=double.MaxValue;m.Warehouse.TryAdd(ItemId.Plank,1);m.Sell(ItemId.Plank,1);m.Advance(600);m.PayWages();
            Assert.That(m.UnpaidWages,Is.EqualTo(17));Assert.That(m.Residents[0].Cash,Is.EqualTo(6.5m));Assert.That(m.Residents[1].Cash,Is.EqualTo(6.5m));
        }
        [Test]public void SalesAndRoundingRemainConsistentAcrossBatchSizes()
        {
            var one=Fresh();var many=Fresh();one.Warehouse.TryAdd(ItemId.Plank,30);many.Warehouse.TryAdd(ItemId.Plank,30);one.Sell(ItemId.Plank,30);for(int i=0;i<30;i++)many.Sell(ItemId.Plank,1);
            Assert.That(one.Money,Is.EqualTo(78.14m));Assert.That(one.CashCents,Is.EqualTo(many.CashCents));
        }
        [Test]public void TechnologyCanOnlyBeSoldOnceAndLocksMinimumPrice()
        {
            var m=Fresh();Assert.That(m.SellTechnology(Technology.Wood).Success,Is.True);Assert.That(m.Money,Is.EqualTo(60));Assert.That(m.SellTechnology(Technology.Wood).Success,Is.False);
            Assert.That(m.SalePrice(ItemId.Plank),Is.EqualTo(1));m.Advance(600);Assert.That(m.SalePrice(ItemId.Plank),Is.EqualTo(1));
        }
        [Test]public void GeneratorSaleCannotAlsoReturnConstructionMaterials()
        {
            var m=Powered();int steel=m.Warehouse.Count(ItemId.SteelPart);Assert.That(m.RemoveFacility(8,true).Success,Is.True);
            Assert.That(m.Money,Is.EqualTo(34.20m));Assert.That(m.Warehouse.Count(ItemId.SteelPart),Is.EqualTo(steel));Assert.That(m.RemoveFacility(8,false).Success,Is.False);
        }
        [Test]public void PurchaseDoesNotDeductWhenBothWarehouseAndGroundCannotAcceptIt()
        {
            var m=Fresh();m.SellTechnology(Technology.Wood);long cash=m.CashCents;Assert.That(m.Buy(ItemId.WoodHandSaw,100).Success,Is.False);Assert.That(m.CashCents,Is.EqualTo(cash));
        }
        [Test]public void InputFrequencyAndHappinessValuesMatchNewDefaultRules()
        {
            Assert.That(DesignRules.InputMeanInterval(0),Is.EqualTo(60));Assert.That(DesignRules.InputMeanInterval(600),Is.EqualTo(54));Assert.That(DesignRules.InputMeanInterval(1800),Is.EqualTo(48));Assert.That(DesignRules.InputMeanInterval(3600),Is.EqualTo(42));
            Assert.That(DesignRules.InputRate(3600),Is.EqualTo(18.75));Assert.That(DesignRules.HappinessEfficiency(40),Is.EqualTo(0.8714285714).Within(1e-8));
            Assert.That(DesignRules.HappinessLoss(720,120,600,20),Is.EqualTo(20));Assert.That(DesignRules.RiotPayment(100),Is.EqualTo(34));
        }
        [Test]public void NaturalWasteRemainsVisibleAndDoesNotCoverCommunityBuildings()
        {
            var m=Fresh(true);long initial=m.Ground.Sum(p=>(long)p.Amount);m.Advance(7200);
            Assert.That(m.IncomingUnits,Is.Zero);Assert.That(m.Ground.Sum(p=>(long)p.Amount),Is.EqualTo(initial+m.SpawnedUnits));
            Assert.That(m.Ground.Any(p=>m.GroundCapacityAt(p.X,p.Y)>5),Is.True);Assert.That(m.Ground.All(p=>!PrototypeSimulation.IsReservedCell(p.X,p.Y)),Is.True);
        }
        [Test]public void SnapshotPreservesInProcessInputsToolsAndCompletionCount()
        {
            var m=Ready();m.ToggleWork();m.Advance(8);var copy=Roundtrip(m);Assert.That(copy.ReservedWood,Is.EqualTo(4));Assert.That(copy.Progress,Is.EqualTo(m.Progress));
            m.Advance(90);copy.Advance(90);Assert.That(copy.CompletedBatches,Is.EqualTo(m.CompletedBatches));Assert.That(copy.Tools.Single(t=>t.Item==ItemId.WoodHandSaw).Durability,Is.EqualTo(m.Tools.Single(t=>t.Item==ItemId.WoodHandSaw).Durability));
        }
        [Test]public void CompletedBlockedBatchCannotRefundInputsAndCommitsOnlyOnce()
        {
            var m=Ready();m.ToggleWork();m.Advance(1);m.Output.TryAdd(ItemId.Plank,8);m.Advance(90);
            Assert.That(m.Progress,Is.EqualTo(1));Assert.That(m.CancelBatch().Success,Is.False);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(8));
            Act(m,()=>m.TakeOutput());Assert.That(m.CompletedBatches,Is.EqualTo(1));Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(4));m.Advance(1);Assert.That(m.CompletedBatches,Is.EqualTo(1));
        }
        [Test]public void ToolAtZeroBecomesOneDamagedInstanceAndStopsFurtherWork()
        {
            var m=Ready();var saw=m.Tools.Single(t=>t.Item==ItemId.WoodHandSaw);int id=saw.Id;m.ToggleWork();
            for(int batch=1;batch<=75;batch++)
            {
                if(m.WoodInputStock.Count(ItemId.WasteWood)<4)m.WoodInputStock.TryAdd(ItemId.WasteWood,4);
                PrototypeStartingScenario.WaitUntil(m,()=>m.CompletedBatches>=batch,90);
                m.Output.TryRemove(ItemId.Plank,m.Output.Available(ItemId.Plank));m.Output.TryRemove(ItemId.WoodChips,m.Output.Available(ItemId.WoodChips));
            }
            m.Advance(1);Assert.That(m.Tools.Single(t=>t.Id==id).Durability,Is.Zero);Assert.That(m.BrokenTools.Count(ItemId.BrokenWoodTool),Is.EqualTo(1));Assert.That(m.State,Is.EqualTo(WorkState.NeedsTool));
        }
        [Test]public void PendingHalfSecondInteractionCannotCreateAnInconsistentSave()
        {
            var m=Fresh();m.PickUp(1,ItemId.WoodHandSaw);Assert.Throws<InvalidOperationException>(()=>m.Capture());m.CancelInteraction();Assert.DoesNotThrow(()=>m.Capture());
        }
        [Test]public void FullDamagedToolShelfPreventsLosingTheToolOrDoubleCommitting()
        {
            var original=Ready();var saved=original.Capture();saved.tools.Single(t=>t.item==(int)ItemId.WoodHandSaw).durability=4;
            var m=PrototypeSimulation.Restore(saved);m.BrokenTools.TryAdd(ItemId.BrokenWoodTool,8);m.ToggleWork();m.Advance(90);
            Assert.That(m.CompletedBatches,Is.Zero);Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(8));
            m.BrokenTools.TryRemove(ItemId.BrokenWoodTool,1);m.Advance(0.1);Assert.That(m.CompletedBatches,Is.EqualTo(1));Assert.That(m.BrokenTools.Count(ItemId.BrokenWoodTool),Is.EqualTo(8));
        }
        [Test]public void SnapshotPreservesPausedBlueprintAndRandomFuture()
        {
            var m=Fresh(true);Collect(m,ItemId.WasteWood,4);Collect(m,ItemId.RecoveredFastener,2);m.SetPaused(true);m.BuildBench();var copy=Roundtrip(m);
            Assert.That(copy.BuildPending,Is.True);Assert.That(copy.Warehouse.Reserved(ItemId.WasteWood),Is.EqualTo(4));m.SetPaused(false);copy.SetPaused(false);m.Advance(200);copy.Advance(200);
            Assert.That(copy.SpawnedUnits,Is.EqualTo(m.SpawnedUnits));Assert.That(copy.Ground.Sum(p=>p.Amount),Is.EqualTo(m.Ground.Sum(p=>p.Amount)));Assert.That(copy.BenchBuilt,Is.True);
        }
        [Test]public void SnapshotPreservesMidDeliveryCargoAndReservations()
        {
            var m=Powered();Move(m,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(m,()=>m.HandOffTool(ItemId.WoodHandSaw));m.Recruit();m.AssignWoodworker();if(!m.RunRequested)m.ToggleWork();
            PrototypeStartingScenario.WaitUntil(m,()=>m.Residents[0].Cargo.UsedTicks>0,90);var copy=Roundtrip(m);m.Advance(30);copy.Advance(30);
            Assert.That(copy.Residents[0].X,Is.EqualTo(m.Residents[0].X).Within(1e-6));Assert.That(copy.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(m.WoodInputStock.Count(ItemId.WasteWood)));Assert.That(copy.DeliveredTrips,Is.EqualTo(m.DeliveredTrips));
        }
        [Test]public void RecipeChangeQueuesUntilBatchFinishesAndOutputsArePhysicallyRemoved()
        {
            var m=Ready();m.ToggleWork();m.Advance(1);Assert.That(m.SelectRecipe(WoodRecipe.WoodHandle).Success,Is.True);Assert.That(m.SelectedRecipe,Is.EqualTo(WoodRecipe.Plank));
            PrototypeStartingScenario.WaitUntil(m,()=>m.CompletedBatches==1,90);Assert.That(m.SelectedRecipe,Is.EqualTo(WoodRecipe.Plank));Act(m,()=>m.TakeOutput());m.Advance(0.1);
            Assert.That(m.SelectedRecipe,Is.EqualTo(WoodRecipe.WoodHandle));Assert.That(m.WoodInputStock.Capacity,Is.EqualTo(4));
        }
        [Test]public void SnapshotPreservesWalletsDebtPricesAndTechSale()
        {
            var m=Powered();m.Recruit();m.Warehouse.TryAdd(ItemId.Plank,3);m.Sell(ItemId.Plank,3);m.Advance(600);m.PayWages();m.SellTechnology(Technology.Wood);
            var copy=Roundtrip(m);Assert.That(copy.CashCents,Is.EqualTo(m.CashCents));Assert.That(copy.UnpaidWageCents,Is.EqualTo(m.UnpaidWageCents));Assert.That(copy.Residents[0].CashCents,Is.EqualTo(m.Residents[0].CashCents));Assert.That(copy.SalePrice(ItemId.Plank),Is.EqualTo(1));
        }
        [Test]public void FreshGameFollowsNormalActionsThroughPhysicalLogisticsAndFirstPayroll()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});Assert.DoesNotThrow(()=>PrototypeStartingScenario.Run(m));Assert.That(m.TotalWagesPaidCents,Is.EqualTo(5000));Assert.That(m.CompletedBatches,Is.GreaterThanOrEqualTo(12));Assert.That(m.DeliveredTrips,Is.GreaterThan(0));Assert.That(m.MealsConsumed,Is.GreaterThan(0));
        }
        [Test]public void SavedSceneContainsCurrentSettingsAndPortableChineseFont()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");var game=UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            Assert.That(game,Is.Not.Null);Assert.That(game.appearance.font.HasCharacter('锯'),Is.True);Assert.That(game.balance.values.mapWidth,Is.EqualTo(32));Assert.That(game.balance.values.moveSpeed,Is.EqualTo(0.8f));Assert.DoesNotThrow(()=>game.balance.values.Validate());
        }
    }
}
