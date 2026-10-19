using System;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;

namespace CrazyRubbish.Tests
{
    // Complete v1 catalog: generic production lines, regions, living, disposal and endings.
    public sealed partial class FullGameTests
    {
        private static void Require(ActionResult r)=>Assert.That(r.Success,Is.True,r.Message);
        private static PrototypeSimulation Fresh(bool allKnown=true,long cash=100000)
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false,warehouseCapacity=20000},4271,true);
            var buildItems=DesignCatalog.Facilities.SelectMany(f=>f.Build).Select(q=>q.Item).Distinct();
            foreach(var item in buildItems.Concat(new[]{ItemId.WasteWood,ItemId.RecoveredFastener,ItemId.Plank,ItemId.SteelPart,ItemId.RecoveredWire,ItemId.ThermalModule,ItemId.GeneratorModule}))m.Warehouse.TryAdd(item,60);
            m.PlayerNeeds.nextMeal=double.MaxValue;m.Tutorial.Skip();m.SetAutomaticMachineStaffing(false);m.SetLivingDelivery(false);
            var s=m.Capture();s.gradualOpening=false;s.cash=cash;
            if(allKnown)s.discovered=ItemCatalog.All.Select(i=>(int)i).ToArray();
            return PrototypeSimulation.Restore(s);
        }
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(m.Capture());
        private static PrototypeSimulation PlayerAt(PrototypeSimulation m,(int x,int y) side)
        {var s=m.Capture();s.playerX=side.x;s.playerY=side.y;return PrototypeSimulation.Restore(s);}
        private static FacilityInstance Place(PrototypeSimulation m,string key,int x,int y,int rotation=0)
        {var r=m.PlaceFacility(PrototypeSimulation.KindOf(key),x,y,rotation);Require(r);return m.GetFacility(r.Amount);}
        private static void Power(PrototypeSimulation m,FacilityInstance f,int generators=1)
        {
            if(!m.GeneratorBuilt)Require(m.BuildGenerator());
            m.SelectedFacility(8).fuelWood=10;
            var side=PrototypeSimulation.ServiceCell(f);var route=m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,side.x,side.y);
            Assert.That(route,Is.Not.Null);Require(m.PlaceWire(route));
            int fx=m.GeneratorFrontX,fy=m.GeneratorFrontY;
            for(int i=1;i<generators;i++)
            {
                var g=Place(m,"generator",10+i*3,20);g.fuelWood=10;var gs=PrototypeSimulation.ServiceCell(g);
                Require(m.PlaceWire(m.FindWireRoute(gs.x,gs.y,fx,fy)));
            }
        }
        private static PrototypeSimulation Operate(PrototypeSimulation m,FacilityInstance f,string recipe)
        {
            int id=f.Id;Require(m.SelectProcess(id,recipe));m=PlayerAt(m,PrototypeSimulation.ServiceCell(m.GetFacility(id)));
            Require(m.ToggleProcessPlayerRole(id,false));return m;
        }
        private static ToolInstance GiveTool(PrototypeSimulation m,FacilityInstance f,ItemId tool)
        {
            Require(m.Buy(tool));var instance=m.Tools.Last(t=>t.Item==tool&&t.Location=="warehouse");
            m.Warehouse.TryRemove(tool,1);f.ProcessToolSlot.TryAdd(tool,1);
            typeof(ToolInstance).GetProperty("Location").SetValue(instance,PrototypeSimulation.ProcessToolKey(f.Id));return instance;
        }

        [Test]public void GeneratedCatalogMatchesTheCompleteDelivery()
        {
            Assert.That(DesignCatalog.ItemKeys.Length,Is.EqualTo(DesignBaseline.RegisteredItems+2),"two S5 compatibility objects stay in the runtime catalog");
            Assert.That(DesignCatalog.Facilities.Length,Is.EqualTo(DesignBaseline.RegisteredFacilities));
            Assert.That(DesignCatalog.Recipes.Length,Is.EqualTo(DesignBaseline.RegisteredRecipes));
            Assert.That(DesignCatalog.Technologies.Length,Is.EqualTo(DesignBaseline.RegisteredTechnologies));
            Assert.That(DesignCatalog.TechnologySales.Length,Is.EqualTo(15));
            Assert.That(DesignCatalog.ItemKeys.Take(59),Is.EqualTo(ItemCatalog.All.Take(59).Select(DesignBaseline.StableItemId)));
            foreach(var r in DesignCatalog.Recipes)Assert.That(DesignCatalog.Facility(r.Facility),Is.Not.Null,r.Key);
            foreach(var t in DesignCatalog.Technologies)foreach(var f in t.Facilities)Assert.That(DesignCatalog.Facility(f),Is.Not.Null,t.Key);
            // Every recipe is runnable by a dedicated legacy flow or by the generic engine.
            int generic=DesignCatalog.Facilities.Sum(f=>PrototypeSimulation.ProcessRecipes(PrototypeSimulation.KindOf(f.Key)).Length);
            Assert.That(generic,Is.GreaterThan(170));
        }
        [Test]public void TechnologyFollowsFirstAcquisitionAndParents()
        {
            var m=Fresh(false);
            Assert.That(m.TechnologyUnlocked("plastic_pre"),Is.False);
            Assert.That(m.PlaceFacility(PrototypeSimulation.KindOf("sorting"),21,12).Success,Is.False);
            Require(m.Buy(ItemId.MixedPlastic));Assert.That(m.TechnologyUnlocked("sorting"),Is.True);
            Require(m.Buy(ItemId.SortedPp));Assert.That(m.TechnologyUnlocked("plastic_pre"),Is.True);
            Assert.That(m.TechnologyUnlocked("plastic_remake"),Is.False);
            Assert.That(m.TechnologyUnlocked("survey"),Is.False,"third-region technology needs the region");
        }
        [Test]public void PlasticSortingProducesTheRegionCompositionThroughTheOperator()
        {
            var m=Fresh();var f=Place(m,"sorting",21,12);int id=f.Id;
            f.ProcessInput.TryAdd(ItemId.MixedPlastic,10);m=Operate(m,f,"sort_plastic");f=m.GetFacility(id);
            m.Advance(89);Assert.That(f.ProcessBatches,Is.Zero);Assert.That(f.ProcessInput.Reserved(ItemId.MixedPlastic),Is.EqualTo(10));
            m.Advance(2);
            Assert.That(f.ProcessBatches,Is.EqualTo(1));Assert.That(f.ProcessOutput.Count(ItemId.SortedPp),Is.EqualTo(4));Assert.That(f.ProcessOutput.Count(ItemId.SortedHdpe),Is.EqualTo(3));
            Assert.That(f.ProcessOutput.Count(ItemId.SortedPet),Is.EqualTo(1));Assert.That(f.ProcessOutput.Count(ItemId.PlasticOther),Is.EqualTo(2));
            Assert.That(f.ProcessInput.Count(ItemId.MixedPlastic),Is.Zero);Assert.That(m.UnrecognizedOtherPlastic,Is.EqualTo(2));
        }
        [Test]public void WashingUsesFractionalWaterAndSoapCreditsWithoutLosingUnits()
        {
            var m=Fresh();var f=Place(m,"plastic_pre",21,12);int id=f.Id;Power(m,f);
            f.ProcessInput.TryAdd(ItemId.SortedPp,6);f.ProcessInput.TryAdd(ItemId.DrinkingWater,1);f.ProcessInput.TryAdd(ItemId.Soap,1);
            m=Operate(m,f,"wash_pp");f=m.GetFacility(id);
            m.Advance(30);Assert.That(f.ProcessStage,Is.EqualTo(1),"human stage finished");
            m.Advance(95);
            Assert.That(f.ProcessBatches,Is.EqualTo(2),"second batch needs no extra water or soap bar");
            Assert.That(f.ProcessOutput.Count(ItemId.FlakesPp),Is.EqualTo(6));
            Assert.That(f.ProcessInput.Count(ItemId.DrinkingWater),Is.Zero);Assert.That(f.ProcessInput.Count(ItemId.Soap),Is.Zero);
            var copy=Copy(m).GetFacility(id);
            Assert.That(copy.CreditAmounts[copy.CreditItems.IndexOf((int)ItemId.DrinkingWater)],Is.EqualTo(0.5).Within(1e-9));
            Assert.That(copy.CreditAmounts[copy.CreditItems.IndexOf((int)ItemId.Soap)],Is.EqualTo(0.96).Within(1e-9));
        }
        [Test]public void MetalSmeltingNeedsTongsPowerAndWearsMaintenance()
        {
            var m=Fresh();var f=Place(m,"metal_raw",21,12);int id=f.Id;Power(m,f,2);
            f.ProcessInput.TryAdd(ItemId.SteelMaterial,5);m=Operate(m,f,"smelt_steel");f=m.GetFacility(id);
            m.Advance(5);Assert.That(f.ProcessActive,Is.False);Assert.That(m.ProcessStatus(f),Does.Contain("夹钳"));
            var tongs=GiveTool(m,f,ItemId.Tongs);
            m.Advance(20.5);Assert.That(f.ProcessStage,Is.EqualTo(1));
            m.Advance(40.5);
            Assert.That(f.ProcessBatches,Is.EqualTo(1));Assert.That(f.ProcessOutput.Count(ItemId.SteelBillet),Is.EqualTo(4));Assert.That(f.ProcessOutput.Count(ItemId.Slag),Is.EqualTo(1));
            Assert.That(tongs.Durability,Is.EqualTo(295));Assert.That(f.Durability,Is.LessThan(90));Assert.That(m.HasDiscovered(ItemId.SteelBillet),Is.True);
            Assert.That(m.TechnologyUnlocked("metal_parts"),Is.True);
        }
        [Test]public void PowerShortageSlowsTheMachineStageProportionally()
        {
            var m=Fresh();var f=Place(m,"metal_raw",21,12);int id=f.Id;Power(m,f);
            GiveTool(m,f,ItemId.Tongs);f.ProcessInput.TryAdd(ItemId.SteelMaterial,5);m=Operate(m,f,"smelt_steel");f=m.GetFacility(id);
            m.Advance(20.5);Assert.That(f.ProcessStage,Is.EqualTo(1));double before=f.ProcessProgress;
            m.Advance(6);Assert.That(f.ProcessProgress-before,Is.EqualTo(4).Within(0.2),"4 output for a load of 6 runs at two thirds");
        }
        [Test]public void GrowingNeedsAPoweredSolarLampForLitStages()
        {
            var m=Fresh();var bed=Place(m,"growing",21,12);int id=bed.Id;
            bed.ProcessInput.TryAdd(ItemId.Seed,1);bed.ProcessInput.TryAdd(ItemId.Substrate,1);bed.ProcessInput.TryAdd(ItemId.DrinkingWater,1);
            GiveTool(m,bed,ItemId.Scissors);var lamp=Place(m,"grow_lamp",24,12);int lampId=lamp.Id;
            m=Operate(m,bed,"grow");bed=m.GetFacility(id);
            m.Advance(21);Assert.That(bed.ProcessStage,Is.EqualTo(1));m.Advance(60);Assert.That(bed.ProcessProgress,Is.Zero,"no lamp power, no growth");
            m.LeaveManualRole();Power(m,m.GetFacility(lampId));m=PlayerAt(m,PrototypeSimulation.ServiceCell(m.GetFacility(id)));bed=m.GetFacility(id);Require(m.ToggleProcessPlayerRole(id,false));
            m.Advance(272);Assert.That(bed.ProcessStage,Is.EqualTo(2));
            m.Advance(11+272+21);
            Assert.That(bed.ProcessBatches,Is.EqualTo(1));Assert.That(bed.ProcessOutput.Count(ItemId.Sprouts),Is.EqualTo(3));Assert.That(bed.ProcessOutput.Count(ItemId.SpentSubstrate),Is.EqualTo(1));
        }
        [Test]public void ResidentsSupplyInputsToolsAndCarryOutputsForGenericLines()
        {
            var m=Fresh();var f=Place(m,"sorting",21,12);int id=f.Id;
            Require(m.Buy(ItemId.MixedPlastic,20));
            var s=m.Capture();s.powered=true;m=PrototypeSimulation.Restore(s);f=m.GetFacility(id);
            Require(m.Recruit());Require(m.Recruit());m.SetAutomaticMachineStaffing(true);foreach(var r in m.Residents)r.Needs.nextMeal=double.MaxValue;
            Require(m.SelectProcess(id,"sort_plastic"));Require(m.ToggleProcess(id));
            m.Advance(400);
            Assert.That(f.ProcessBatches,Is.GreaterThanOrEqualTo(1));Assert.That(m.Warehouse.Count(ItemId.SortedPp),Is.GreaterThanOrEqualTo(4),"outputs were hauled to the warehouse");
            Assert.That(m.Residents.Any(r=>r.Job==WorkerJob.MachineOperator&&r.FacilityId==id),Is.True);
        }
        [Test]public void RegionsAreBoughtInOrderAndExtendTheBuildableMap()
        {
            var m=Fresh(true,1000000);Assert.That(m.OwnedRegion,Is.EqualTo(1));
            Assert.That(m.PlaceFacility(PrototypeSimulation.KindOf("sorting"),30,10).Success,Is.False);
            Require(m.PurchaseRegion());Assert.That(m.OwnedRegion,Is.EqualTo(2));Assert.That(m.Settings.mapWidth,Is.EqualTo(52));Assert.That(m.Money,Is.EqualTo(9800));
            Assert.That(m.Ground.Any(p=>p.Item==ItemId.OldPetFormer&&p.X>=44),Is.True,"region 2 fixed salvage arrives in its own heap");
            Assert.That(m.Tools.Where(t=>t.Item==ItemId.DamagedMotor).All(t=>t.Condition==1),Is.True);
            Require(m.PlaceFacility(PrototypeSimulation.KindOf("sorting"),30,10));
            Assert.That(m.FindPath(m.PlayerX,m.PlayerY,30,9),Is.Not.Null,"cross-region walking");
            Require(m.PurchaseRegion());Assert.That(m.Settings.mapWidth,Is.EqualTo(86));Assert.That(m.Settings.mapHeight,Is.EqualTo(32));Assert.That(m.Money,Is.EqualTo(9200));
            Assert.That(m.PurchaseRegion().Success,Is.False);
            var copy=Copy(m);Assert.That(copy.OwnedRegion,Is.EqualTo(3));Assert.That(copy.Settings.mapWidth,Is.EqualTo(86));
        }
        [Test]public void SurveyUnlocksOneMinePerVeinAndExtractionYieldsOre()
        {
            var m=Fresh(true,1000000);Require(m.PurchaseRegion());Require(m.PurchaseRegion());
            Assert.That(m.PlaceFacility(PrototypeSimulation.KindOf("mine"),55,18).Success,Is.False,"unsurveyed");
            var survey=Place(m,"survey",60,12);int sid=survey.Id;Power(m,survey,1);
            var s=m.Capture();s.playerX=PrototypeSimulation.ServiceCell(survey).x;s.playerY=PrototypeSimulation.ServiceCell(survey).y;m=PrototypeSimulation.Restore(s);survey=m.GetFacility(sid);
            GiveTool(m,survey,ItemId.Wrench);Require(m.SelectProcess(sid,"survey_iron"));Require(m.ToggleProcessPlayerRole(sid,false));
            m.Advance(30.5+90.5);Assert.That(m.SurveyedVeins,Does.Contain("iron"));
            m.LeaveManualRole();var mine=Place(m,"mine",55,18);Assert.That(mine.Vein,Is.EqualTo("iron"));
            Assert.That(m.PlaceFacility(PrototypeSimulation.KindOf("mine"),56,19).Success,Is.False,"one extractor per vein");
            Assert.That(m.SelectProcess(mine.Id,"extract_copper").Success,Is.False);
        }
        [Test]public void LandfillBuriesDisposalsPermanentlyAndSeals()
        {
            var m=Fresh();var f=Place(m,"landfill",21,12);int id=f.Id;
            f.ProcessInput.TryAdd(ItemId.Slag,5);Require(m.ToggleProcess(id));m=PlayerAt(m,PrototypeSimulation.ServiceCell(m.GetFacility(id)));
            Require(m.ToggleProcessPlayerRole(id,false));f=m.GetFacility(id);
            m.Advance(5.2);Assert.That(m.LandfilledTicks,Is.EqualTo(5*20));Assert.That(f.ProcessInput.Count(ItemId.Slag),Is.Zero);
            Assert.That(PrototypeSimulation.DisposalAccepted(ItemId.DrinkingWater),Is.False);Assert.That(PrototypeSimulation.DisposalAccepted(ItemId.LiftFrame),Is.False);
            f.ProcessInput.SetCapacity(200);f.ProcessInput.TryAdd(ItemId.Slag,75);m.Advance(80);
            Assert.That(f.Sealed,Is.True);Assert.That(m.RemoveFacilityById(id).Success,Is.False);
        }
        [Test]public void CrossingTheDumpThresholdStartsAnIrreversibleInvasionTimer()
        {
            var m=Fresh();var s=m.Capture();s.dumpedTicks=PrototypeSimulation.DumpThresholdTicks-20;s.playerX=PrototypeSimulation.DumpX;s.playerY=PrototypeSimulation.DumpY;m=PrototypeSimulation.Restore(s);
            m.Carry.TryAdd(ItemId.Slag,2);Assert.That(m.DumpIntoHole(ItemId.DrinkingWater,1).Success,Is.False);
            Require(m.DumpIntoHole(ItemId.Slag,1));m.Advance(0.6);
            Assert.That(m.InvasionAt,Is.EqualTo(m.GameSeconds+600).Within(1));
            m=Copy(m);m.Advance(605);Assert.That(m.Failed,Is.True);Assert.That(m.EndingKind,Is.EqualTo("invasion"));
        }
        [Test]public void HappinessCausesRampRecoverAndCanTriggerALifeRiot()
        {
            var m=Fresh();var s=m.Capture();s.powered=true;m=PrototypeSimulation.Restore(s);
            Require(m.Recruit());Require(m.Recruit());foreach(var r in m.Residents){r.Needs.nextMeal=double.MaxValue;r.Mood.clothesSeconds=1;}
            m.Advance(2);Assert.That(m.Residents.All(r=>r.Mood.clothesMissing),Is.True);Assert.That(m.Warehouse.Count(ItemId.OldClothes),Is.EqualTo(2),"worn clothes return as old clothes");
            m.Advance(120);double early=m.Residents[0].Happiness;
            m.Advance(600);
            Assert.That(early,Is.GreaterThan(m.Residents[0].Happiness));Assert.That(m.Residents[0].Happiness,Is.LessThanOrEqualTo(70-20+1e-6));
            Assert.That(m.ActiveCauses(m.Residents[0]),Does.Contain("缺少衣物"));
            foreach(var r in m.Residents)typeof(Resident).GetProperty("CashCents").SetValue(r,2000L);
            m.Warehouse.TryAdd(ItemId.Clothes,2);m.Advance(11);Assert.That(m.Residents.All(r=>!r.Mood.clothesMissing),Is.True);
            m.Advance(181);Assert.That(m.Residents[0].Mood.loss[3],Is.Zero,"clothes loss recovers within 180 s");
        }
        [Test]public void LowHappinessRiotNeedsCausesClearedBeforeTheDeadline()
        {
            var m=Fresh();var s=m.Capture();s.powered=true;m=PrototypeSimulation.Restore(s);
            Require(m.Recruit());Require(m.Recruit());s=m.Capture();s.expansion.communityClean=false;m=PrototypeSimulation.Restore(s);
            foreach(var r in m.Residents){r.Needs.nextMeal=double.MaxValue;r.Mood.loss=new double[]{0,10,5,20,15};r.Mood.since=new[]{-1.0,0,0,0,0};r.Mood.clothesSeconds=0.5;typeof(Resident).GetProperty("CashCents").SetValue(r,0L);}
            m.Advance(1);Assert.That(m.Residents.All(r=>r.Happiness<25),Is.True);
            m.Advance(181);Assert.That(m.HappinessRiot,Is.True);Assert.That(m.RiotActive,Is.True);Require(m.MakePromise());
            m.Advance(241);Assert.That(m.Failed,Is.True);Assert.That(m.FailureReason,Does.Contain("生活暴动"));
        }
        [Test]public void BonusRequiresNoArrearsRaisesEfficiencyAndExpiresAtTheNextPayroll()
        {
            var m=Fresh();var s=m.Capture();s.powered=true;m=PrototypeSimulation.Restore(s);Require(m.Recruit());m.Residents[0].Needs.nextMeal=double.MaxValue;
            long cash=m.CashCents;Require(m.PayBonus(2));Assert.That(m.CashCents,Is.EqualTo(cash-400));Assert.That(m.BonusFactor,Is.EqualTo(0.2).Within(1e-9));
            m.Advance(601);Assert.That(m.BonusFactor,Is.Zero);Assert.That(m.PayBonus(1).Success,Is.False,"arrears block bonuses");
        }
        [Test]public void EveryTechnologySaleUsesTheDeliveredTableAndFloorsPrices()
        {
            var m=Fresh();long cash=m.CashCents;
            Require(m.SellTechnology("pet"));Assert.That(m.CashCents,Is.EqualTo(cash+10000));
            Assert.That(m.SalePrice(ItemId.PetStrap),Is.EqualTo((double)ItemCatalog.Price(ItemId.PetStrap).minimum));
            Assert.That(m.SellTechnology("pet").Success,Is.False);
            Assert.That(m.SellTechnology("mineral").Success,Is.False,"requires third-region technology");
            Assert.That(Copy(m).IsSaleSold("pet"),Is.True);
        }
        [Test]public void InProcessBatchesSurviveSnapshotWithCreditsToolsAndStage()
        {
            var m=Fresh();var f=Place(m,"metal_raw",21,12);int id=f.Id;Power(m,f,2);var tongs=GiveTool(m,f,ItemId.Tongs);
            f.ProcessInput.TryAdd(ItemId.SteelMaterial,5);m=Operate(m,f,"smelt_steel");m.Advance(30);
            var copy=Copy(m);var g=copy.GetFacility(id);
            Assert.That(g.ProcessActive,Is.True);Assert.That(g.ProcessStage,Is.EqualTo(1));Assert.That(g.ProcessTools,Is.EqualTo(new[]{tongs.Id}));
            Assert.That(g.ProcessInput.Reserved(ItemId.SteelMaterial),Is.EqualTo(5));
            copy.Advance(60);Assert.That(g.ProcessBatches,Is.EqualTo(1));
        }
        [Test]public void ThermalStationNeedsStartupPowerThenSuppliesFifteen()
        {
            var m=Fresh();
            var boiler=Place(m,"boiler",21,12);var turbine=Place(m,"turbine",21,17);var condenser=Place(m,"condenser",25,17);
            boiler.ProcessInput.TryAdd(ItemId.WasteWood,10);boiler.ProcessInput.TryAdd(ItemId.DrinkingWater,10);
            Power(m,turbine);var load=Place(m,"metal_raw",25,12);
            Require(m.ToggleThermal(turbine.Id));m.Advance(21);
            Assert.That(turbine.ThermalRunning,Is.True,m.ProcessStatus(turbine));Assert.That(boiler.WaterLoop,Is.EqualTo(8).Within(1e-6));
            Assert.That(m.PowerNetworks.First(n=>n.FacilityIds.Contains(turbine.Id)).Output,Is.EqualTo(19).Within(1e-6));
        }
        [Test]public void WarehousesShareStockAndRaiseCapacity()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false},4271,true);
            var s=m.Capture();s.gradualOpening=false;s.cash=100000;s.discovered=ItemCatalog.All.Select(i=>(int)i).ToArray();m=PrototypeSimulation.Restore(s);
            m.Warehouse.TryAdd(ItemId.Plank,10);m.Warehouse.TryAdd(ItemId.SteelPart,10);int before=m.Warehouse.Capacity;
            Place(m,"warehouse",21,12);Assert.That(m.Warehouse.Capacity,Is.EqualTo(before+240));
        }
        [Test]public void OldSavesWithTheFirstRegionCatalogLoadIntoTheCompleteCatalog()
        {
            var m=Fresh();var s=m.Capture();s.schema=5;s.expansion=null;s.itemCount=59;s.itemIds=s.itemIds.Take(59).ToArray();
            foreach(var inv in s.inventories)inv.counts=inv.counts.Take(59).ToArray();
            foreach(var r in s.residents)r.cargo.counts=r.cargo.counts.Take(59).ToArray();
            var loaded=PrototypeSimulation.Restore(s);
            Assert.That(loaded.OwnedRegion,Is.EqualTo(1));Assert.That(loaded.Warehouse.Count(ItemId.Plank),Is.EqualTo(m.Warehouse.Count(ItemId.Plank)));
            Assert.That(loaded.Capture().schema,Is.EqualTo(SimulationSnapshot.CurrentSchema));
        }
    }
}
