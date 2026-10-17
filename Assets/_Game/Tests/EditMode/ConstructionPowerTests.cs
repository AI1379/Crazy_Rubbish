using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class ConstructionPowerTests
    {
        private static PrototypeSimulation Fresh()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false,warehouseCapacity=10000},4271,true);
            m.PlayerNeeds.nextMeal=double.MaxValue;m.SetLivingDelivery(false);m.Tutorial.Skip();
            foreach(var item in new[]{ItemId.WasteWood,ItemId.RecoveredFastener,ItemId.Plank,ItemId.SteelPart,ItemId.RecoveredWire,ItemId.LightingModule,ItemId.ThermalModule,ItemId.GeneratorModule})Assert.That(m.Warehouse.TryAdd(item,200),Is.True);
            var s=m.Capture();s.discovered=new[]{(int)ItemId.Plank,(int)ItemId.SteelPart,(int)ItemId.RecoveredWire,(int)ItemId.LightingModule};return PrototypeSimulation.Restore(s);
        }
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        private static void Require(ActionResult result)=>Assert.That(result.Success,Is.True,result.Message);
        private static void Fuel(PrototypeSimulation m)
        {
            PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
            PrototypeStartingScenario.Act(m,()=>m.Withdraw(ItemId.WasteWood,2));
            PrototypeStartingScenario.MoveTo(m,m.GeneratorFrontX,m.GeneratorFrontY);
            PrototypeStartingScenario.Act(m,()=>m.AddFuel(ItemId.WasteWood,2));
        }
        [Test] public void BaselineHasStableIdsAndGeneratedRuntimeValues()
        {
            Assert.That(DesignBaseline.Version,Is.EqualTo("balance-2026-10-11-v1"));
            Assert.That(DesignBaseline.RegisteredItems,Is.EqualTo(197));Assert.That(DesignBaseline.RegisteredRecipes,Is.EqualTo(211));
            Assert.That(DesignBaseline.StableItemId(ItemId.RecoveredWire),Is.EqualTo("wire"));
            Assert.That(ItemCatalog.Price(ItemId.Plank).sale,Is.EqualTo(3m));
            var save=Fresh().Capture();Assert.That(save.itemIds[(int)ItemId.RecoveredWire],Is.EqualTo("wire"));
            save.itemIds[(int)ItemId.RecoveredWire]="unrelated-item";Assert.Throws<ArgumentException>(()=>PrototypeSimulation.Restore(save));
            Assert.That(Resources.Load<TextAsset>("Design/Balance20261011"),Is.Not.Null);
            using(var hash=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Docs/Source/ArtDelivery_2026-10-13/数值配置/全局数值_v1.json","Assets/_Game/Resources/Design/Balance20261011.json"})
                {string actual=BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();Assert.That(actual,Is.EqualTo(DesignBaseline.SourceSha256),path);}
        }
        [TestCase(0,20,11)][TestCase(1,19,13)][TestCase(2,21,14)][TestCase(3,22,12)]
        public void RotationChangesTheActualServiceSide(int rotation,int x,int y)
        {
            var m=Fresh();var result=m.PlaceFacility(1,20,12,rotation);Require(result);
            Assert.That((m.BenchFrontX,m.BenchFrontY),Is.EqualTo((x,y)));
            Assert.That(m.FindPath(m.PlayerX,m.PlayerY,x,y),Is.Not.Null);
            Assert.That(m.IsWalkable(20,12),Is.False);
        }
        [Test]public void RejectedPlacementDoesNotChargeAndCannotCoverPeopleOrWire()
        {
            var m=Fresh();int stock=m.Warehouse.UsedTicks;
            Assert.That(m.PlaceFacility(1,27,23).Success,Is.False);
            Assert.That(m.PlaceFacility(1,8,6).Success,Is.False);
            Require(m.PlaceWire(new[]{new WireCell{X=20,Y=12}}));int wired=m.Warehouse.UsedTicks;
            Assert.That(m.PlaceFacility(1,20,12).Success,Is.False);Assert.That(m.Warehouse.UsedTicks,Is.EqualTo(wired));
            Assert.That(m.Warehouse.UsedTicks,Is.LessThan(stock));
        }
        [Test]public void MultipleSameKindSitesKeepSeparateInventoryProgressAndSaveState()
        {
            var m=Fresh();int one=m.PlaceFacility(1,20,12).Amount,two=m.PlaceFacility(1,24,12,1).Amount;
            Assert.That(one,Is.Not.EqualTo(two));Require(m.SelectFacility(one));m.WoodInputStock.TryAdd(ItemId.WasteWood,4);
            Require(m.SelectFacility(two));Assert.That(m.WoodInputStock.Count(ItemId.WasteWood),Is.Zero);
            m.WoodInputStock.TryAdd(ItemId.WasteWood,8);m.SelectRecipe(WoodRecipe.WoodHandle);
            var copy=Copy(m);Require(copy.SelectFacility(one));Assert.That(copy.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(4));
            Require(copy.SelectFacility(two));Assert.That(copy.WoodInputStock.Count(ItemId.WasteWood),Is.EqualTo(8));
            Assert.That(copy.RecipeChangePending,Is.True);Assert.That(copy.BenchFrontX,Is.EqualTo(23));
        }
        [Test]public void PausedConstructionCanBeSavedCancelledAndResumedWithoutDuplicatingMaterials()
        {
            var m=Fresh();int wood=m.Warehouse.Count(ItemId.WasteWood);m.SetPaused(true);
            int id=m.PlaceFacility(1,20,12).Amount;Assert.That(m.GetFacility(id).Built,Is.False);
            Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(wood));Assert.That(m.Warehouse.Reserved(ItemId.WasteWood),Is.EqualTo(4));
            var copy=Copy(m);Require(copy.CancelBlueprint(id));Assert.That(copy.Warehouse.Reserved(ItemId.WasteWood),Is.Zero);
            m.SetPaused(false);m.Advance(0.1);Assert.That(m.GetFacility(id).Built,Is.True);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(wood-4));
        }
        [Test]public void MovingReturnsContentsToTheOldSiteAndPreservesTheFacilityIdentity()
        {
            var m=Fresh();int id=m.PlaceFacility(1,20,12).Amount;int before=m.Warehouse.Count(ItemId.WasteWood);
            m.WoodInputStock.TryAdd(ItemId.WasteWood,4);Require(m.MoveFacility(id,24,18,2));
            Assert.That(m.GetFacility(id).Built,Is.True);Assert.That(m.IsWalkable(20,12),Is.True);Assert.That(m.IsWalkable(24,18),Is.False);
            Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(before+4));Assert.That(m.WoodInputStock.UsedTicks,Is.Zero);
            Require(m.RemoveFacilityById(id));Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(before+8));
            Assert.That(m.RemoveFacilityById(id).Success,Is.False);
        }
        [Test]public void IdleGeneratorDoesNotBurnFuelOrGrantRecognition()
        {
            var m=Fresh();Require(m.BuildGenerator());Fuel(m);double before=m.FuelSeconds;m.Advance(130);
            Assert.That(m.FuelSeconds,Is.EqualTo(before).Within(1e-5));Assert.That(m.HasBeenPowered,Is.False);
            Assert.That(m.GeneratorResidue.UsedTicks,Is.Zero);
        }
        [Test]public void CommunityNeedsRealConnectionAndDisconnectFreezesRemainingFuel()
        {
            var m=Fresh();Require(m.BuildGenerator());Fuel(m);Require(m.ConnectCommunityPower());m.Advance(1);
            Assert.That(m.HousingPowered&&m.CommunityLampPowered,Is.True);Assert.That(m.HasBeenPowered,Is.True);
            Require(m.RemoveWire(18,6));double before=m.FuelSeconds;m.Advance(1);
            Assert.That(m.HousingPowered,Is.False);Assert.That(m.FuelSeconds,Is.EqualTo(before).Within(1e-5));
            Require(m.PlaceWire(new[]{new WireCell{X=18,Y=6}}));m.Advance(1);Assert.That(m.HousingPowered,Is.True);
            Assert.That(m.FuelSeconds,Is.LessThan(before));
        }
        [Test]public void SeparateNetworksDoNotShareGeneratorOutput()
        {
            var m=Fresh();Require(m.BuildGenerator());Require(m.ConnectCommunityPower());Fuel(m);
            int lamp=m.PlaceFacility(32,25,20).Amount;
            Require(m.PlaceWire(new[]{new WireCell{X=24,Y=20}}));m.Advance(0.1);
            Assert.That(m.HousingPowered,Is.True);Assert.That(m.GetFacility(lamp).PowerRatio,Is.Zero);
        }
        [Test]public void SameNetworkUsesFairPowerRatioAndParallelGeneratorsAddOutput()
        {
            var m=Fresh();Require(m.BuildGenerator());Require(m.ConnectCommunityPower());Fuel(m);
            var lamps=new System.Collections.Generic.List<int>();
            for(int y=12;y<=20;y+=2)for(int x=12;x<=26;x+=2)lamps.Add(m.PlaceFacility(32,x,y).Amount);
            Assert.That(lamps.All(id=>id>0),Is.True);
            foreach(int id in lamps){var side=PrototypeSimulation.ServiceCell(m.GetFacility(id));Require(m.PlaceWire(m.FindWireRoute(18,7,side.x,side.y)));}
            m.Advance(0.1);double ratio=4/4.3;
            foreach(int id in lamps)Assert.That(m.GetFacility(id).PowerRatio,Is.EqualTo(ratio).Within(1e-6));
            Assert.That(m.HousingPowerRatio,Is.EqualTo(ratio).Within(1e-6));
            int second=m.PlaceFacility(8,10,6).Amount;Require(m.SelectFacility(second));Fuel(m);
            Require(m.PlaceWire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,18,7)));m.Advance(0.1);
            Assert.That(m.HousingPowerRatio,Is.EqualTo(1));Assert.That(m.GetFacility(second).fuelRemaining,Is.GreaterThan(0));
        }
        [Test]public void PausedWirePlansRemainDisconnectedAndRefundExactlyAcrossSave()
        {
            var m=Fresh();Require(m.BuildGenerator());Fuel(m);int count=m.Warehouse.Count(ItemId.RecoveredWire);
            m.SetPaused(true);Require(m.ConnectCommunityPower());Assert.That(m.HousingPowered,Is.False);
            Assert.That(m.Warehouse.Count(ItemId.RecoveredWire),Is.EqualTo(count));Assert.That(m.Warehouse.Reserved(ItemId.RecoveredWire),Is.EqualTo(1));
            var copy=Copy(m);Require(copy.CancelBlueprints());Assert.That(copy.Wires.Count,Is.Zero);Assert.That(copy.Warehouse.Reserved(ItemId.RecoveredWire),Is.Zero);
            m.SetPaused(false);m.Advance(0.1);Assert.That(m.HousingPowered,Is.True);Assert.That(m.Warehouse.Count(ItemId.RecoveredWire),Is.EqualTo(count-1));
        }
        [Test]public void WireFragmentsCannotDuplicateMaterialsOnRepeatedPlaceAndRemove()
        {
            var m=Fresh();int count=m.Warehouse.Count(ItemId.RecoveredWire);
            for(int i=0;i<12;i++){Require(m.PlaceWire(new[]{new WireCell{X=22,Y=18}}));Require(m.RemoveWire(22,18));}
            Assert.That(m.Warehouse.Count(ItemId.RecoveredWire),Is.EqualTo(count));Assert.That(m.WireCredit,Is.Zero);
        }
        [Test]public void InvalidWirePathIsAtomicAndNeverCutsThroughAMachine()
        {
            var m=Fresh();Require(m.BuildGenerator());int count=m.Warehouse.Count(ItemId.RecoveredWire);
            Assert.That(m.PlaceWire(new[]{new WireCell{X=18,Y=7},new WireCell{X=18,Y=8}}).Success,Is.False);
            Assert.That(m.Wires.Count,Is.Zero);Assert.That(m.Warehouse.Count(ItemId.RecoveredWire),Is.EqualTo(count));
            Assert.That(m.PlaceWire(new[]{new WireCell{X=10,Y=10},new WireCell{X=12,Y=10}}).Success,Is.False);
        }
        [Test]public void LegacySaveMigrationPreservesFuelAndDoesNotInventWires()
        {
            var m=Fresh();var s=m.Capture();s.schema=3;s.sourceVersion=5;s.generator=true;s.fuelWood=2;
            var migrated=PrototypeSimulation.Restore(s);Assert.That(migrated.GeneratorBuilt,Is.True);Assert.That(migrated.FuelSeconds,Is.EqualTo(240));
            Assert.That(migrated.Wires.Count,Is.Zero);Assert.That(migrated.Capture().schema,Is.EqualTo(5));
        }
        [Test]public void TwoResidentsProduceAtSeparateSitesAndContinueBothBatchesAfterFileRoundtrip()
        {
            var m=Fresh();int one=m.PlaceFacility(1,20,12).Amount,two=m.PlaceFacility(1,24,12,1).Amount;
            foreach(int id in new[]{one,two})
            {Require(m.SelectFacility(id));m.WoodInputStock.TryAdd(ItemId.WasteWood,4);m.WoodTools.TryAdd(ItemId.WoodHandSaw,1);m.GetFacility(id).RunRequested=true;}
            var s=m.Capture();s.gradualOpening=false;s.runWood=true;s.nextWage=600;s.nextTool=1003;
            int worker=0;
            foreach(int id in new[]{one,two})
            {
                worker++;var front=PrototypeSimulation.ServiceCell(m.GetFacility(id));
                s.residents.Add(new ResidentSave{id=worker,job=(int)WorkerJob.Woodwork,facilityId=id,x=front.x,y=front.y,cash=500,cargo=new UnitInventory(8).Capture(),needs=new LivingNeeds{nextMeal=double.MaxValue}});
                s.tools.Add(new ToolSave{id=1000+worker,item=(int)ItemId.WoodHandSaw,durability=300,location=id==1?"benchTools":"benchTools:"+id,usable=true});
            }
            m=PrototypeSimulation.Restore(s);m.Advance(10);
            Assert.That(m.GetFacility(one).jobProgress,Is.GreaterThan(9));Assert.That(m.GetFacility(two).jobProgress,Is.GreaterThan(9));
            var copy=Copy(m);copy.Advance(31);
            Assert.That(copy.CompletedBatches,Is.EqualTo(2));Assert.That(copy.GetFacility(one).WoodInputStock.UsedTicks,Is.Zero);Assert.That(copy.GetFacility(two).WoodInputStock.UsedTicks,Is.Zero);
            Assert.That(copy.GetFacility(one).Output.Count(ItemId.Plank)+copy.GetFacility(two).Output.Count(ItemId.Plank)+copy.Warehouse.Count(ItemId.Plank),Is.EqualTo(206));
        }
        [Test]public void MovingGeneratorRedirectsAlreadyCarriedFuelBackToWarehouse()
        {
            var m=Fresh();Require(m.BuildGenerator());var s=m.Capture();var cargo=new UnitInventory(8);cargo.TryAdd(ItemId.WasteWood,1);
            s.residents.Add(new ResidentSave{id=1,x=12,y=5,cash=500,cargo=cargo.Capture(),needs=new LivingNeeds{nextMeal=double.MaxValue},delivery=new DeliverySave{id=1,item=(int)ItemId.WasteWood,amount=1,picked=true,from="warehouse",to="fuel",sx=12,sy=5,tx=18,ty=7}});
            m=PrototypeSimulation.Restore(s);Require(m.MoveFacility(8,24,18,1));
            var moved=m.Capture();Assert.That(moved.residents[0].delivery.to,Is.EqualTo("warehouse"));
            int count=m.Warehouse.Count(ItemId.WasteWood);m.Advance(3);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(count+1));
            Assert.That(m.FuelCount,Is.Zero);
        }
        [Test]public void WageRiotStillAllowsEmergencyFacilitySaleWithoutReturningMaterials()
        {
            var m=Fresh();Require(m.BuildGenerator());var s=m.Capture();s.community.riot=true;s.community.riotTarget=100;s.community.riotDeadline=240;
            m=PrototypeSimulation.Restore(s);int steel=m.Warehouse.Count(ItemId.SteelPart);long cash=m.CashCents;
            Require(m.RemoveFacilityById(8,true));Assert.That(m.CashCents,Is.EqualTo(cash+3420));Assert.That(m.Warehouse.Count(ItemId.SteelPart),Is.EqualTo(steel));
        }
        [Test]public void NewHousingAddsBedsAndItsRentGoesToThePlayerWithRealWalletTransfers()
        {
            var m=Fresh();Require(m.PlaceFacility(16,24,18));var s=m.Capture();s.powered=true;m=PrototypeSimulation.Restore(s);
            for(int i=0;i<5;i++)Require(m.Recruit());
            Assert.That(m.Recruit().Success,Is.False);Assert.That(m.TotalRecruited,Is.EqualTo(5));
            s=m.Capture();s.cash=3000;m=PrototypeSimulation.Restore(s);Require(m.Recruit());
            Assert.That(m.CashCents,Is.Zero);Assert.That(m.CommunityCashCents,Is.EqualTo(3000));
            s=m.Capture();s.community.firstPayroll=0;s.community.nextPeriod=0;
            foreach(var person in s.residents)person.needs.nextMeal=double.MaxValue;
            m=PrototypeSimulation.Restore(s);m.Advance(0.05);
            Assert.That(m.CommunityCashCents,Is.EqualTo(3500));Assert.That(m.CashCents,Is.EqualTo(100));
            Assert.That(m.Residents.All(r=>r.CashCents==400),Is.True);Assert.That(m.HousingCapacity,Is.EqualTo(10));
            s=m.Capture();s.residents.Clear();s.bills.Clear();s.cash=0;m=PrototypeSimulation.Restore(s);
            Assert.That(m.TotalRecruited,Is.EqualTo(6));Assert.That(m.Recruit().Success,Is.False,"Free hire quota cannot reset after death or reload.");
        }
        [Test]public void LargerRosterPaysForExpansionAndNeverSpawnsOutsideTheMap()
        {
            var m=Fresh();foreach(int x in new[]{12,16,20,24})Require(m.PlaceFacility(16,x,18));
            var s=m.Capture();s.powered=true;s.cash=39000;m=PrototypeSimulation.Restore(s);
            for(int i=0;i<18;i++)Require(m.Recruit());
            Assert.That(m.CashCents,Is.Zero);Assert.That(m.CommunityCashCents,Is.EqualTo(39000));
            Assert.That(m.Residents.Select(r=>r.Id).Distinct().Count(),Is.EqualTo(18));
            Assert.That(m.Residents.All(r=>m.IsWalkable((int)r.X,(int)r.Y)),Is.True);
            Assert.That(Copy(m).TotalRecruited,Is.EqualTo(18));
        }
        [Test]public void RotatedSiteOverflowStaysOnOwnedGroundWhenWarehouseIsFull()
        {
            var m=Fresh();int id=m.PlaceFacility(1,24,22,2).Amount;Assert.That(id,Is.GreaterThan(0));
            foreach(ItemId item in Enum.GetValues(typeof(ItemId)))m.Warehouse.TryRemove(item,m.Warehouse.Available(item));
            m.Warehouse.TryAdd(ItemId.WasteWood,10000);m.WoodInputStock.TryAdd(ItemId.WasteWood,4);
            Require(m.MoveFacility(id,20,12,1));
            Assert.That(m.Ground.Sum(p=>p.Amount),Is.EqualTo(4));Assert.That(m.Ground.All(p=>PrototypeSimulation.IsOwnedGround(p.X,p.Y)),Is.True);
            Assert.That(m.Ground.All(p=>p.X==24&&p.Y==22),Is.True);
        }
        [Test]public void MovingCompletedBatchCannotLoseABrokenToolWhenRecoveryShelfIsFull()
        {
            var m=Fresh();int id=m.PlaceFacility(1,20,12).Amount;
            m.WoodInputStock.TryAdd(ItemId.WasteWood,4);m.WoodTools.TryAdd(ItemId.WoodHandSaw,1);m.BrokenTools.TryAdd(ItemId.BrokenWoodTool,8);
            var f=m.GetFacility(id);f.woodToken=m.WoodInputStock.Reserve(new ItemAmount(ItemId.WasteWood,4));f.jobActive=true;f.jobProgress=40;f.woodToolId=1001;
            var s=m.Capture();s.nextTool=1002;s.tools.Add(new ToolSave{id=1001,item=(int)ItemId.WoodHandSaw,durability=4,usingAt=1,location="benchTools",usable=true});m=PrototypeSimulation.Restore(s);
            Assert.That(m.MoveFacility(id,24,18,1).Success,Is.False);Assert.That(m.GetFacility(id).X,Is.EqualTo(20));Assert.That(m.WoodInputStock.Reserved(ItemId.WasteWood),Is.EqualTo(4));
            m.BrokenTools.TryRemove(ItemId.BrokenWoodTool,1);Require(m.MoveFacility(id,24,18,1));
            Assert.That(m.BrokenTools.Count(ItemId.BrokenWoodTool),Is.EqualTo(8));Assert.That(m.CompletedBatches,Is.EqualTo(1));Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(203));
        }
        [Test]public void LegacyWorkerBindingMigratesAndDemolitionReleasesItsJob()
        {
            var m=Fresh();Require(m.BuildBench());var s=m.Capture();s.schema=3;s.sourceVersion=5;
            s.residents.Add(new ResidentSave{id=1,job=(int)WorkerJob.Woodwork,x=15,y=5,cash=500,cargo=new UnitInventory(8).Capture(),needs=new LivingNeeds{nextMeal=double.MaxValue}});
            m=PrototypeSimulation.Restore(s);Assert.That(m.Residents[0].FacilityId,Is.EqualTo(1));Require(m.RemoveFacilityById(1));
            Assert.That(m.Residents[0].Job,Is.EqualTo(WorkerJob.Idle));Assert.That(m.Residents[0].FacilityId,Is.Zero);
        }
    }
}
