using System;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class MachineryConveyorTests
    {
        private static void Require(ActionResult r)=>Assert.That(r.Success,Is.True,r.Message);
        private static PrototypeSimulation Fresh()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false,warehouseCapacity=10000},4271,true);
            foreach(var item in new[]{ItemId.WasteWood,ItemId.RecoveredFastener,ItemId.Plank,ItemId.SteelPart,ItemId.RecoveredWire,ItemId.Motor,ItemId.MachineBlade,ItemId.FeedingModule,ItemId.ThermalModule,ItemId.GeneratorModule,ItemId.LightingModule})m.Warehouse.TryAdd(item,100);
            m.PlayerNeeds.nextMeal=double.MaxValue;m.Tutorial.Skip();m.SetAutomaticMachineStaffing(false);m.SetLivingDelivery(false);
            var s=m.Capture();s.gradualOpening=false;s.cash=100000;s.discovered=Enum.GetValues(typeof(ItemId)).Cast<ItemId>().Select(i=>(int)i).ToArray();return PrototypeSimulation.Restore(s);
        }
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        private static FacilityInstance Machine(PrototypeSimulation m,int kind=64,int x=20,int y=12)
        {var r=m.PlaceFacility(kind,x,y);Require(r);return m.GetFacility(r.Amount);}
        private static void Generator(PrototypeSimulation m,FacilityInstance f,bool fuel=true)
        {
            Require(m.BuildGenerator());var gen=m.SelectedFacility(8);gen.fuelWood=fuel?10:0;
            var side=PrototypeSimulation.ServiceCell(f);Require(m.PlaceWire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,side.x,side.y)));
        }
        private static PrototypeSimulation PlayerAt(PrototypeSimulation m,(int x,int y) side)
        {var s=m.Capture();s.playerX=side.x;s.playerY=side.y;return PrototypeSimulation.Restore(s);}
        private static void Prime(FacilityInstance f)
        {f.WoodInputStock.TryAdd(PrototypeSimulation.MachineFeed(f),4);f.MachineToken=f.WoodInputStock.Reserve(new ItemAmount(PrototypeSimulation.MachineFeed(f),4));f.MachineActive=true;f.MachinePhase=2;f.RunRequested=true;f.HumanProgress=f.Kind==64?10:0;}
        private static ConveyorLane Cell(int x,int y,int d=0)=>new ConveyorLane{X=x,Y=y,Direction=d};
        private static void FeedLane(PrototypeSimulation m,int id,ItemId item,double progress=0)
        {var b=m.GetConveyor(id);b.Item=(int)item;b.Progress=progress;}
        private static void PowerLanes(PrototypeSimulation m)
        {
            if(!m.GeneratorBuilt)Require(m.BuildGenerator());m.SelectedFacility(8).fuelWood=10;
            foreach(var b in m.Conveyors)
                Require(m.PlaceWire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,b.X,b.Y)));
        }
        [Test]public void TechnologyAcquisitionGatesConstructionAndRotationChangesFootprint()
        {
            var m=Fresh();var s=m.Capture();s.discovered=new[]{(int)ItemId.SteelPart};m=PrototypeSimulation.Restore(s);int before=m.Warehouse.UsedTicks;
            Assert.That(m.PlaceFacility(64,20,12).Success,Is.False);Assert.That(m.Warehouse.UsedTicks,Is.EqualTo(before));
            Require(m.Buy(ItemId.Motor));Require(m.Buy(ItemId.MachineBlade));Require(m.PlaceFacility(64,20,12,1));
            Assert.That(PrototypeSimulation.Footprint(m.SelectedFacility(64)),Is.EqualTo((2,3)));Assert.That(m.IsWalkable(20,14),Is.False);
        }
        [Test]public void ElectricWoodUsesTenHumanThenFifteenMachineSecondsAndCommitsOnce()
        {
            var m=Fresh();var f=Machine(m);int id=f.Id;Generator(m,f);f.WoodInputStock.TryAdd(ItemId.WasteWood,4);
            m=PlayerAt(m,PrototypeSimulation.ServiceCell(f));f=m.GetFacility(id);Require(m.ToggleMachinePlayerRole(id,false));
            m.Advance(10);Assert.That(f.HumanProgress,Is.EqualTo(10).Within(1e-5));Assert.That(f.MachineProgress,Is.Zero);Assert.That(m.SelectedFacility(8).fuelWood,Is.EqualTo(10));
            Assert.That(f.WoodInputStock.Reserved(ItemId.WasteWood),Is.EqualTo(4));m.Advance(15);
            Assert.That(f.MachineBatches,Is.EqualTo(1));Assert.That(f.Output.Count(ItemId.Plank),Is.EqualTo(3));Assert.That(f.Output.Count(ItemId.WoodChips),Is.EqualTo(1));
            Assert.That(f.WoodInputStock.Count(ItemId.WasteWood),Is.Zero);Assert.That(f.Durability,Is.EqualTo(95).Within(0.02));
        }
        [Test]public void PowerlessMachineKeepsProgressAndDoesNotWearDuringMachinePhase()
        {
            var m=Fresh();var f=Machine(m);Prime(f);m.Advance(30);
            Assert.That(f.MachineProgress,Is.Zero);Assert.That(f.Durability,Is.EqualTo(100));Assert.That(f.State,Is.EqualTo(WorkState.NeedsPower));
            Generator(m,f);m.Advance(5);Assert.That(f.MachineProgress,Is.EqualTo(5).Within(1e-5));
            foreach(var line in m.Wires.ToArray())Require(m.RemoveWire(line.X,line.Y));double progress=f.MachineProgress,durability=f.Durability;m.Advance(5);
            Assert.That(f.MachineProgress,Is.EqualTo(progress));Assert.That(f.Durability,Is.EqualTo(durability));
        }
        [Test]public void MachineStagesIgnoreHumanMultipliersAndSharePowerFairly()
        {
            var m=Fresh();var fs=new[]{Machine(m,64,10,18),Machine(m,64,14,18),Machine(m,128,18,18),Machine(m,128,23,18)};
            Generator(m,fs[0]);foreach(var f in fs.Skip(1)){var s=PrototypeSimulation.ServiceCell(f);Require(m.PlaceWire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,s.x,s.y)));}
            foreach(var f in fs)Prime(f);m.Advance(5);
            foreach(var f in fs){Assert.That(f.MachineProgress,Is.EqualTo(4).Within(0.001));Assert.That(f.Durability,Is.EqualTo(99).Within(0.001));}
        }
        [Test]public void MaintenanceWorksWithoutPowerAndRestartNeedsTwentyDurability()
        {
            var m=Fresh();var f=Machine(m);int id=f.Id;Prime(f);f.Durability=0;f.MachineTripped=true;
            m=PlayerAt(m,PrototypeSimulation.MaintenanceCell(f));f=m.GetFacility(id);Require(m.ToggleMachinePlayerRole(id,true));
            m.Advance(49.9);Assert.That(f.MachineTripped,Is.True);Assert.That(f.MachineProgress,Is.Zero);
            m.Advance(0.1);Assert.That(f.MachineTripped,Is.False);Assert.That(f.Durability,Is.EqualTo(20).Within(0.001));Assert.That(f.MachineProgress,Is.Zero);
        }
        [Test]public void RiotStopsMaintenanceButAutonomousMachineStageContinues()
        {
            var m=Fresh();var f=Machine(m,128);Generator(m,f);Prime(f);int id=f.Id;
            var s=m.Capture();s.community.riot=true;s.community.riotTarget=100;s.community.riotDeadline=10000;m=PrototypeSimulation.Restore(s);f=m.GetFacility(id);m.Advance(5);
            Assert.That(f.MachineProgress,Is.EqualTo(5).Within(0.001));Assert.That(f.Durability,Is.EqualTo(99).Within(0.001));
        }
        [Test]public void AutomaticWoodRequiresSortedWoodAndNoHandsawOrOperator()
        {
            var m=Fresh();var f=Machine(m,128);Generator(m,f);f.WoodInputStock.TryAdd(ItemId.WasteWood,4);Require(m.ToggleMachine(f.Id));m.Advance(1);
            Assert.That(f.MachineActive,Is.False);f.WoodInputStock.TryRemove(ItemId.WasteWood,4);f.WoodInputStock.TryAdd(ItemId.SortedWood,4);m.Advance(15.05);
            Assert.That(f.MachineBatches,Is.EqualTo(1));Assert.That(f.Output.Count(ItemId.Plank),Is.EqualTo(3));
        }
        [Test]public void WholeBatchOutputAndInputReservationsSurviveFileRoundtrip()
        {
            var m=Fresh();var f=Machine(m,128);Generator(m,f);f.Output.TryAdd(ItemId.Plank,5);f.WoodInputStock.TryAdd(ItemId.SortedWood,4);Require(m.ToggleMachine(f.Id));m.Advance(1);
            Assert.That(f.MachineActive,Is.False);Assert.That(f.WoodInputStock.Reserved(ItemId.SortedWood),Is.Zero);
            f.Output.TryRemove(ItemId.Plank,1);m.Advance(7);int id=f.Id;var copy=Copy(m);f=copy.GetFacility(id);
            Assert.That(f.WoodInputStock.Reserved(ItemId.SortedWood),Is.EqualTo(4));Assert.That(f.MachineProgress,Is.EqualTo(7).Within(0.001));copy.Advance(8.05);
            Assert.That(f.MachineBatches,Is.EqualTo(1));Assert.That(f.Output.Count(ItemId.Plank),Is.EqualTo(7));Assert.That(f.WoodInputStock.UsedTicks,Is.Zero);
        }
        [Test]public void CancellationAndMoveReleaseOnlyUnfinishedInputsAndKeepWear()
        {
            var m=Fresh();var f=Machine(m,128);Generator(m,f);Prime(f);m.Advance(5);int id=f.Id;double durability=f.Durability;int wood=m.Warehouse.Count(ItemId.SortedWood);
            Require(m.CancelMachineBatch(id));Assert.That(f.WoodInputStock.Available(ItemId.SortedWood),Is.EqualTo(4));Assert.That(f.Durability,Is.EqualTo(durability));
            Require(m.MoveFacility(id,24,18,1));Assert.That(m.Warehouse.Count(ItemId.SortedWood),Is.EqualTo(wood+4));Assert.That(f.Durability,Is.EqualTo(durability));
        }
        [Test]public void BeltConstructionUsesPairsAndPausedPlansSaveCancelAndCommitExactly()
        {
            var m=Fresh();int boards=m.Warehouse.Count(ItemId.Plank),steel=m.Warehouse.Count(ItemId.SteelPart);m.SetPaused(true);
            Require(m.PlaceConveyor(new[]{Cell(22,18),Cell(23,18),Cell(24,18)}));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(boards));
            var copy=Copy(m);Require(copy.CancelBlueprints());Assert.That(copy.Conveyors.Count,Is.Zero);Assert.That(copy.Warehouse.Reserved(ItemId.Plank),Is.Zero);
            m.SetPaused(false);m.Advance(0.05);Assert.That(m.Conveyors.All(b=>!b.Blueprint),Is.True);Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(boards-2));
            foreach(int id in m.Conveyors.Select(b=>b.Id).ToArray())Require(m.RemoveConveyor(id));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(boards));Assert.That(m.Warehouse.Count(ItemId.SteelPart),Is.EqualTo(steel));
        }
        [Test]public void BeltsMoveOneEntityPerCellPerSecondAndBackpressureKeepsCargo()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(20,18),Cell(21,18),Cell(22,18)}));PowerLanes(m);
            var bs=m.Conveyors.ToArray();FeedLane(m,bs[0].Id,ItemId.WasteWood);m.Advance(0.95);Assert.That(bs[0].Item,Is.EqualTo((int)ItemId.WasteWood));
            m.Advance(0.05);Assert.That(bs[1].Item,Is.EqualTo((int)ItemId.WasteWood));m.Advance(1);Assert.That(bs[2].Item,Is.EqualTo((int)ItemId.WasteWood));
            FeedLane(m,bs[1].Id,ItemId.Plank);m.Advance(3);Assert.That(bs[1].Item,Is.EqualTo((int)ItemId.Plank));Assert.That(bs[2].Item,Is.EqualTo((int)ItemId.WasteWood));
        }
        [Test]public void SimultaneousFullChainAdvancesWithoutHalvingThroughput()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(20,18),Cell(21,18),Cell(22,18),Cell(23,18)}));PowerLanes(m);
            var bs=m.Conveyors.ToArray();foreach(var b in bs.Take(3))FeedLane(m,b.Id,ItemId.WasteWood);m.Advance(1);
            Assert.That(bs[0].Item,Is.EqualTo(-1));Assert.That(bs.Skip(1).All(b=>b.Item==(int)ItemId.WasteWood),Is.True);
        }
        [Test]public void CrossingLanesStayIndependentAndMergesAlternate()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(20,18),Cell(21,18),Cell(22,18)}));Require(m.PlaceConveyor(new[]{Cell(21,17,1),Cell(21,18,1),Cell(21,19,1)}));PowerLanes(m);
            var h=m.Conveyors.Single(b=>b.X==20&&b.Y==18);var v=m.Conveyors.Single(b=>b.X==21&&b.Y==17);
            FeedLane(m,h.Id,ItemId.Plank);FeedLane(m,v.Id,ItemId.WoodChips);m.Advance(1);
            Assert.That(m.Conveyors.Single(b=>b.X==21&&b.Y==18&&b.Direction==0).Item,Is.EqualTo((int)ItemId.Plank));
            Assert.That(m.Conveyors.Single(b=>b.X==21&&b.Y==18&&b.Direction==1).Item,Is.EqualTo((int)ItemId.WoodChips));
        }
        [Test]public void BeltSaveKeepsPartialProgressAndPowerLossFreezesIt()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(22,18),Cell(23,18)}));PowerLanes(m);FeedLane(m,m.Conveyors[0].Id,ItemId.WoodChips);m.Advance(0.45);
            var copy=Copy(m);Assert.That(copy.Conveyors[0].Progress,Is.EqualTo(0.45).Within(0.001));m.Advance(0.55);copy.Advance(0.55);
            Assert.That(copy.Conveyors[1].Item,Is.EqualTo(m.Conveyors[1].Item));Require(copy.SetConveyorEnabled(copy.Conveyors[1].Id,false));double progress=copy.Conveyors[1].Progress;copy.Advance(2);Assert.That(copy.Conveyors[1].Progress,Is.EqualTo(progress));
        }
        [Test]public void WarehousePortsFeedRealStocksAndFiltersStopIncorrectItems()
        {
            var m=Fresh();Require(m.PlacePort(-1,14,3,true,(int)ItemId.WasteWood));Require(m.PlaceConveyor(new[]{Cell(15,3,1),Cell(15,4,1)}));PowerLanes(m);
            int before=m.Warehouse.Count(ItemId.WasteWood);m.Advance(0.05);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(before-1));
            Assert.That(m.Conveyors[0].Item,Is.EqualTo((int)ItemId.WasteWood));var p=m.ConveyorPorts[0];Require(m.ConfigurePort(p.Id,(int)ItemId.Plank,true));m.Advance(1);
            Assert.That(m.Conveyors[0].Item,Is.EqualTo((int)ItemId.Plank));Assert.That(m.Conveyors[1].Item,Is.EqualTo((int)ItemId.WasteWood));
        }
        [Test]public void InputsUseIndependentPortsAndDoNotOverrideReservedHumanSpace()
        {
            var m=Fresh();var f=Machine(m);Require(m.PlacePort(f.Id,19,13,false,(int)ItemId.WasteWood));Require(m.PlaceConveyor(new[]{Cell(18,13)}));PowerLanes(m);
            FeedLane(m,m.Conveyors[0].Id,ItemId.Plank);m.Advance(2);Assert.That(f.WoodInputStock.UsedTicks,Is.Zero);
            m.Conveyors[0].Item=(int)ItemId.WasteWood;m.Advance(0.05);Assert.That(f.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(1));
        }
        [Test]public void MovingMachineDisconnectsPortsAndKeepsApproachingCargo()
        {
            var m=Fresh();var f=Machine(m);Require(m.PlacePort(f.Id,19,13,false));Require(m.PlaceConveyor(new[]{Cell(18,13)}));PowerLanes(m);
            FeedLane(m,m.Conveyors[0].Id,ItemId.WasteWood);Require(m.MoveFacility(f.Id,24,18,1));m.Advance(2);
            Assert.That(m.ConveyorPorts[0].OwnerId,Is.Zero);Assert.That(m.ConveyorPorts[0].Enabled,Is.False);Assert.That(m.Conveyors[0].Item,Is.EqualTo((int)ItemId.WasteWood));
        }
        [Test]public void ConveyorRespectsSpaceReservedForPickedHumanCargoAcrossSave()
        {
            var m=Fresh();var f=Machine(m);f.WoodInputStock.TryAdd(ItemId.WasteWood,4);
            Require(m.PlacePort(f.Id,19,13,false));Require(m.PlaceConveyor(new[]{Cell(18,13)}));PowerLanes(m);FeedLane(m,m.Conveyors[0].Id,ItemId.WasteWood,1);
            var s=m.Capture();s.nextWage=double.MaxValue;var cargo=new UnitInventory(8);cargo.TryAdd(ItemId.WasteWood,4);var side=PrototypeSimulation.ServiceCell(f);
            s.residents.Add(new ResidentSave{id=1,x=12,y=5,cash=500,cargo=cargo.Capture(),needs=new LivingNeeds{nextMeal=double.MaxValue},
                delivery=new DeliverySave{id=1,item=(int)ItemId.WasteWood,amount=4,picked=true,from="warehouse",to=PrototypeSimulation.MachineInputKey(f.Id),sx=12,sy=3,tx=side.x,ty=side.y,retryAt=double.MaxValue}});
            m=Copy(PrototypeSimulation.Restore(s));m.Advance(2);f=m.GetFacility(f.Id);
            Assert.That(f.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Conveyors[0].Item,Is.EqualTo((int)ItemId.WasteWood));
            s=m.Capture();s.residents[0].x=side.x;s.residents[0].y=side.y;s.residents[0].delivery.retryAt=0;s.residents[0].delivery.progress=1.99;
            m=PrototypeSimulation.Restore(s);m.Advance(0.05);
            Assert.That(m.GetFacility(f.Id).WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(8));Assert.That(m.Residents[0].Cargo.UsedTicks,Is.Zero);Assert.That(m.Conveyors[0].Item,Is.EqualTo((int)ItemId.WasteWood));
        }
        [Test]public void SaveDuringHumanPreparationResumesTheSameReservedBatch()
        {
            var m=Fresh();var f=Machine(m);int id=f.Id;Generator(m,f);f.WoodInputStock.TryAdd(ItemId.WasteWood,4);
            m=PlayerAt(m,PrototypeSimulation.ServiceCell(f));Require(m.ToggleMachinePlayerRole(id,false));m.Advance(4.25);var copy=Copy(m);
            Assert.That(copy.GetFacility(id).HumanProgress,Is.EqualTo(4.25).Within(1e-6));Assert.That(copy.GetFacility(id).WoodInputStock.Reserved(ItemId.WasteWood),Is.EqualTo(4));
            m.Advance(20.75);copy.Advance(20.75);Assert.That(copy.GetFacility(id).MachineBatches,Is.EqualTo(1));Assert.That(copy.GetFacility(id).MachineBatches,Is.EqualTo(m.GetFacility(id).MachineBatches));
            Assert.That(copy.GetFacility(id).Durability,Is.EqualTo(m.GetFacility(id).Durability).Within(1e-6));
        }
        [Test]public void RemovingLoadedBeltReturnsCargoAndMaterialOnlyOnce()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(22,18)}));int boards=m.Warehouse.Count(ItemId.Plank);FeedLane(m,m.Conveyors[0].Id,ItemId.Plank);
            int id=m.Conveyors[0].Id;Require(m.RemoveConveyor(id));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(boards+2));Assert.That(m.RemoveConveyor(id).Success,Is.False);
        }
        [Test]public void MachineAndInfrastructureRejectBlockedSidesAndOverlappingConstruction()
        {
            var m=Fresh();var f=Machine(m);var front=PrototypeSimulation.ServiceCell(f);var maint=PrototypeSimulation.MaintenanceCell(f);
            Assert.That(m.PlacePort(f.Id,front.x,front.y,false).Success,Is.False);Assert.That(m.PlacePort(f.Id,maint.x,maint.y,false).Success,Is.False);
            Require(m.PlaceConveyor(new[]{Cell(24,18)}));Assert.That(m.PlaceFacility(1,24,18).Success,Is.False);
        }
        [Test]public void MergeRoundRobinSurvivesCongestionAndDoesNotStarveTheSideInput()
        {
            var m=Fresh();Require(m.PlaceConveyor(new[]{Cell(20,18),Cell(21,18),Cell(22,18)}));Require(m.PlaceConveyor(new[]{Cell(21,17,1)}));PowerLanes(m);
            var west=m.Conveyors.Single(b=>b.X==20);var south=m.Conveyors.Single(b=>b.Y==17);var middle=m.Conveyors.Single(b=>b.X==21&&b.Y==18);var end=m.Conveyors.Single(b=>b.X==22);
            FeedLane(m,west.Id,ItemId.Plank);FeedLane(m,south.Id,ItemId.WoodChips);m.Advance(1);Assert.That(middle.Item,Is.EqualTo((int)ItemId.Plank));
            m.Advance(1);Assert.That(middle.Item,Is.EqualTo((int)ItemId.WoodChips));Assert.That(south.Item,Is.EqualTo(-1));
            FeedLane(m,west.Id,ItemId.Plank);m.Advance(1);Require(m.RemoveConveyor(end.Id));Require(m.PlaceConveyor(new[]{Cell(22,18)}));
            FeedLane(m,south.Id,ItemId.WoodChips,1);m.Advance(0.05);Assert.That(middle.Item,Is.EqualTo((int)ItemId.Plank));Assert.That(south.Item,Is.EqualTo((int)ItemId.WoodChips));
        }
        [Test]public void ToolInstancesKeepIdentityDurabilityAndLocationOnBeltsAndInFiles()
        {
            var m=Fresh();m.Warehouse.TryAdd(ItemId.WoodHandSaw,1);var s=m.Capture();s.nextTool=501;s.tools.Add(new ToolSave{id=500,item=(int)ItemId.WoodHandSaw,durability=123,location="warehouse",usable=true});m=PrototypeSimulation.Restore(s);
            Require(m.PlacePort(-1,14,3,true,(int)ItemId.WoodHandSaw));Require(m.PlaceConveyor(new[]{Cell(15,3,1),Cell(15,4,1)}));PowerLanes(m);m.Advance(0.05);
            Assert.That(m.Conveyors[0].InstanceId,Is.EqualTo(500));Assert.That(m.Warehouse.Count(ItemId.WoodHandSaw),Is.Zero);
            var copy=Copy(m);Assert.That(copy.Tools.Single(t=>t.Id==500).Durability,Is.EqualTo(123));copy.Advance(1);
            Require(copy.RemoveConveyor(copy.Conveyors[1].Id));Assert.That(copy.Warehouse.Count(ItemId.WoodHandSaw),Is.EqualTo(1));Assert.That(copy.Tools.Single(t=>t.Id==500).Location,Is.EqualTo("warehouse"));
        }
        [Test]public void WarehouseOutputCannotExportTheProtectedDrinkingReserve()
        {
            var m=Fresh();m.Warehouse.TryAdd(ItemId.DrinkingWater,4);Require(m.PlacePort(-1,14,3,true,(int)ItemId.DrinkingWater));Require(m.PlaceConveyor(new[]{Cell(15,3,1),Cell(15,4,1),Cell(15,5,1)}));PowerLanes(m);m.Advance(4);
            Assert.That(m.Warehouse.Count(ItemId.DrinkingWater),Is.EqualTo(2));Assert.That(m.Conveyors.Count(b=>b.Item==(int)ItemId.DrinkingWater),Is.EqualTo(2));
        }
        [Test]public void ShortFuelRemainderCannotPowerAnEntireTickWithoutEnergyStorage()
        {
            var m=Fresh();var f=Machine(m,128);Generator(m,f,false);Prime(f);var gen=m.SelectedFacility(8);gen.fuelRemaining=0.025;gen.activeFuelHeatRate=1.0/120;
            m.Advance(0.05);Assert.That(f.MachineProgress,Is.EqualTo(0.025).Within(1e-6));Assert.That(f.Durability,Is.EqualTo(99.995).Within(1e-6));
            m.Advance(1);Assert.That(f.MachineProgress,Is.EqualTo(0.025).Within(1e-6));
        }
        [Test]public void RebuiltMachineIsNewWhileMovedMachineKeepsItsWear()
        {
            var m=Fresh();var f=Machine(m,128);f.Durability=12;int old=f.Id;Require(m.MoveFacility(old,24,18,1));Assert.That(f.Durability,Is.EqualTo(12));
            Require(m.RemoveFacilityById(old));var rebuilt=Machine(m,128,20,12);Assert.That(rebuilt.Id,Is.Not.EqualTo(old));Assert.That(rebuilt.Durability,Is.EqualTo(100));
        }
        [Test]public void WorkersReachSeparatePostsAndManualReleaseIsRespected()
        {
            var m=Fresh();var f=Machine(m);Generator(m,f);f.WoodInputStock.TryAdd(ItemId.WasteWood,4);Require(m.ToggleMachine(f.Id));var s=m.Capture();s.nextWage=double.MaxValue;
            foreach(int id in new[]{1,2})s.residents.Add(new ResidentSave{id=id,x=12,y=5,cash=500,cargo=new UnitInventory(8).Capture(),needs=new LivingNeeds{nextMeal=double.MaxValue}});
            m=PrototypeSimulation.Restore(s);m.SetAutomaticMachineStaffing(true);m.Advance(45);f=m.GetFacility(f.Id);
            Assert.That(f.MachineBatches,Is.GreaterThanOrEqualTo(1));Assert.That(f.Durability,Is.GreaterThan(98));Assert.That(m.Residents.Count(r=>r.Job==WorkerJob.Maintenance),Is.EqualTo(1));
            Require(m.AssignMachineWorker(f.Id,true));m.Advance(3);Assert.That(m.Residents.Any(r=>r.Job==WorkerJob.Maintenance&&r.FacilityId==f.Id),Is.False);
            Assert.That(Copy(m).GetFacility(f.Id).SuppressAutoMaintenance,Is.True);
        }
        [Test]public void RealWarehouseBeltMachineBeltLoopProducesAndPreservesEveryItemAcrossSave()
        {
            var m=Fresh();var f=Machine(m);int id=f.Id;
            Require(m.BuildGenerator());m.SelectedFacility(8).fuelWood=10;
            Require(m.PlacePort(-1,14,3,true,(int)ItemId.WasteWood));Require(m.PlacePort(id,19,13,false,(int)ItemId.WasteWood));
            Require(m.PlacePort(id,23,13,true));Require(m.PlacePort(-1,13,5,false));
            Require(m.PlaceConveyor(m.FindConveyorRoute(15,3,18,13,0)));Require(m.PlaceConveyor(m.FindConveyorRoute(24,13,13,6,3)));PowerLanes(m);
            m=PlayerAt(m,PrototypeSimulation.ServiceCell(f));f=m.GetFacility(id);Require(m.ToggleMachinePlayerRole(id,false));
            int raw=m.Warehouse.Count(ItemId.WasteWood),boards=m.Warehouse.Count(ItemId.Plank);m.Advance(120);
            Assert.That(f.MachineBatches,Is.GreaterThanOrEqualTo(2));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.GreaterThan(boards));
            Assert.That(m.Warehouse.Count(ItemId.WasteWood)+f.WoodInputStock.Count(ItemId.WasteWood)+m.Conveyors.Count(b=>b.Item==(int)ItemId.WasteWood),Is.EqualTo(raw-4*f.MachineBatches));
            Assert.That(m.Warehouse.Count(ItemId.Plank)+f.Output.Count(ItemId.Plank)+m.Conveyors.Count(b=>b.Item==(int)ItemId.Plank),Is.EqualTo(boards+3*f.MachineBatches));
            Assert.That(m.Warehouse.Count(ItemId.WoodChips)+f.Output.Count(ItemId.WoodChips)+m.Conveyors.Count(b=>b.Item==(int)ItemId.WoodChips),Is.EqualTo(f.MachineBatches));
            var copy=Copy(m);m.Advance(12.5);copy.Advance(12.5);Assert.That(copy.GetFacility(id).MachineBatches,Is.EqualTo(f.MachineBatches));
            Assert.That(copy.Conveyors.Select(b=>(b.Item,b.InstanceId,b.Progress)).ToArray(),Is.EqualTo(m.Conveyors.Select(b=>(b.Item,b.InstanceId,b.Progress)).ToArray()));
        }
        [Test]public void NormalProgressiveGameCanBuildAndOperateTheElectricLineWithoutInjectedResources()
        {
            var m=new PrototypeSimulation(new PrototypeSettings(),4271,true);
            CrazyRubbish.Prototype.PrototypeTutorialScenario.Run(m);
            int id=CrazyRubbish.Prototype.PrototypeAutomationScenario.Run(m);var f=m.GetFacility(id);
            Assert.That(f.MachineBatches,Is.GreaterThanOrEqualTo(3));Assert.That(f.RepairedDurability,Is.GreaterThan(0));Assert.That(m.ConveyorDeliveredItems,Is.GreaterThanOrEqualTo(20));
            Assert.That(m.Failed,Is.False);var copy=PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture(),true)));Assert.That(copy.GetFacility(id).MachineBatches,Is.EqualTo(f.MachineBatches));
            Assert.That(copy.GetFacility(id).Durability,Is.EqualTo(f.Durability).Within(1e-9));Assert.That(copy.GetFacility(id).RepairedDurability,Is.EqualTo(f.RepairedDurability).Within(1e-9));
            Assert.That(JsonUtility.ToJson(copy.GetFacility(id).WoodInputStock.Capture()),Is.EqualTo(JsonUtility.ToJson(f.WoodInputStock.Capture())));
            Assert.That(copy.Conveyors.Select(b=>(b.Id,b.Item,b.InstanceId)).ToArray(),Is.EqualTo(m.Conveyors.Select(b=>(b.Id,b.Item,b.InstanceId)).ToArray()));
            for(int i=0;i<m.Conveyors.Count;i++)Assert.That(copy.Conveyors[i].Progress,Is.EqualTo(m.Conveyors[i].Progress).Within(1e-9));
        }
    }
}
