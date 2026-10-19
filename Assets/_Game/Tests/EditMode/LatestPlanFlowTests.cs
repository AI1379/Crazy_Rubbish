using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class LatestPlanFlowTests
    {
        private static PlanSimulation Fresh(){var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));return new PlanSimulation(c);}
        private static void Go(PlanSimulation m,int x,int y)
        {
            for(int i=0;i<3000&&Math.Abs(m.State.player.x-x)+Math.Abs(m.State.player.y-y)>.05;i++){Assert.That(m.Walk(m.State.player,x,y,.1),Is.True,"No route to "+x+","+y);m.Advance(.1);}
            Assert.That(Math.Abs(m.State.player.x-x)+Math.Abs(m.State.player.y-y),Is.LessThan(.1));Assert.That(m.State.ending,Is.Null.Or.Empty);
        }
        private static void Collect(PlanSimulation m,string id,int amount)
        {
            int collected=0;
            while(collected<amount)
            {
                var g=m.State.ground.First(a=>a.stock.Count(id)>0);Go(m,g.x,g.y);long n=Math.Min(PlanStock.Scale,g.stock.Count(id));m.Pick(g.id,id,n);m.Advance(.5);collected++;
            }
        }
        private static void DepositWarehouse(PlanSimulation m){var w=m.State.sites.First(s=>s.type=="warehouse");var f=m.Front(w);Go(m,f.x,f.y);m.Deposit(w);}
        // This path uses only actual fixed starting objects, walking, pickup, build and work actions.
        // It has no money, unlocked-tech, inventory, population, efficiency or timer injections.
        public static PlanSimulation PlayStarter()
        {
            var m=Fresh();m.SetPaused(false);Collect(m,"food",6);DepositWarehouse(m);Collect(m,"water",8);DepositWarehouse(m);
            Collect(m,"handsaw_wood",1);Collect(m,"waste_wood",4);Collect(m,"fasteners",2);var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");
            Collect(m,"waste_wood",4);var f=m.Front(wood);Go(m,f.x,f.y);m.Deposit(wood);m.AssignHero(wood.id);m.Advance(50);Assert.That(wood.output.Count("boards"),Is.GreaterThanOrEqualTo(3*PlanStock.Scale));m.AssignHero(wood.id);
            for(int i=0;i<3;i++)m.TakeOutput(wood,"boards",PlanStock.Scale);DepositWarehouse(m);
            foreach(var a in m.Config.Facility("generator").build){Collect(m,a.item,(int)a.quantity);DepositWarehouse(m);}
            var generator=m.Build("generator",12,6);Collect(m,"waste_wood",2);f=m.Front(generator);Go(m,f.x,f.y);m.Deposit(generator);
            Collect(m,"wire",2);DepositWarehouse(m);for(int x=12;x<=17;x++)m.Build("power_line",x,6);m.Advance(.2);
            Assert.That(m.State.recognized,Is.True);Assert.That(m.State.sites.First(s=>s.type=="housing").powerRatio,Is.GreaterThanOrEqualTo(.5));
            m.Recruit();Assert.That(m.Alive.Count(),Is.EqualTo(1));Assert.That(m.State.cash,Is.Zero);Assert.That(m.State.ending,Is.Null.Or.Empty);return m;
        }
        [Test]public void RealZeroCashStarterReachesPowerAndFirstHire(){PlayStarter();}
        [Test]public void FailedFractionalToolPurchaseDoesNotDeductMoney()
        {var m=Fresh();m.State.cash=10000;Assert.Throws<InvalidOperationException>(()=>m.Buy("handsaw_wood",PlanStock.Scale/2));Assert.That(m.State.cash,Is.EqualTo(10000));}
        [Test]public void MovingOntoPlayerIsRejectedAndDemolitionReleasesHeroJob()
        {var m=Fresh();foreach(var a in m.Config.Facility("wood").build){m.State.warehouse.Add(a.item,a.Ticks);m.Discover(a.item);}var s=m.Build("wood",8,8);m.State.player.x=11;m.State.player.y=11;Assert.That(Assert.Throws<InvalidOperationException>(()=>m.MoveSite(s,11,11)).Message,Does.Contain("角色"));Assert.That(s.x,Is.EqualTo(8));Assert.That(s.y,Is.EqualTo(8));m.State.heroSite=s.id;m.RemoveSite(s);Assert.That(m.State.heroSite,Is.Zero);}
        [Test]public void PickupTakesHalfSecondAndReservationsSurviveClone()
        {var m=Fresh();m.SetPaused(false);var g=m.State.ground.First(g=>g.stock.Count("handsaw_wood")>0);m.State.player.x=g.x;m.State.player.y=g.y;m.Pick(g.id,"handsaw_wood",PlanStock.Scale);m.Advance(.4);Assert.That(m.State.backpack.Count("handsaw_wood"),Is.Zero);var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));copy.Advance(.1);Assert.That(copy.State.backpack.Count("handsaw_wood"),Is.EqualTo(PlanStock.Scale));Assert.That(copy.State.player.trip,Is.Null);}
        [Test]public void AutoRestockPreservesSalaryAndUsesSeparateBudget()
        {var m=Fresh();m.State.recognized=true;m.Recruit();m.State.cash=2000;m.State.autoSupply=true;m.SetPaused(false);m.Advance(.1);Assert.That(m.State.cash,Is.GreaterThanOrEqualTo(1000));Assert.That(m.State.autoSupplyUsed,Is.LessThanOrEqualTo(1000));Assert.That(m.Stored("water"),Is.GreaterThan(0));Assert.That(m.State.toolBudgetUsed,Is.Zero);}
        [Test]public void PromiseKeepsExistingRiotDeadlineAndReleasesOnlyOrganizers()
        {var m=Fresh();m.State.recognized=true;for(int i=0;i<3;i++)m.Recruit();m.State.riot.active=true;m.State.riot.deadline=111;m.State.riot.organizers.Add(m.Alive.First().id);m.Promise("power");Assert.That(m.State.riot.deadline,Is.EqualTo(111));Assert.That(m.State.riot.organizers.Count,Is.EqualTo(1));Assert.That(m.State.riot.repair,Is.EqualTo("power"));}
        [Test]public void LandfillPermanentlyConsumesLandOnlyAfterRealDisposal()
        {var m=Fresh();m.Discover("boards");m.Discover("steel_parts");m.State.warehouse.Add("boards",100000);m.State.warehouse.Add("steel_parts",100000);m.State.warehouse.capacity=10000;var s=m.Build("landfill",8,8);m.SetPaused(false);m.Advance(.1);Assert.That(s.permanent,Is.False);s.input.Add("ash",PlanStock.Scale);var f=m.Front(s);m.State.player.x=f.x;m.State.player.y=f.y;m.AssignHero(s.id);m.Advance(2);Assert.That(s.permanent,Is.True);Assert.Throws<InvalidOperationException>(()=>m.RemoveSite(s));Assert.That(m.State.dumped,Is.Zero);m.State.technologies.Add(m.Config.Facility("power_line").technology);m.State.warehouse.Add("wire",PlanStock.Scale);Assert.That(Assert.Throws<InvalidOperationException>(()=>m.Build("power_line",8,8)).Message,Does.Contain("永久"));}
        [Test]public void CompletedWarehouseDeliveryPreventsSameTickThirstDeath()
        {
            var m=Fresh();m.State.recognized=true;m.Recruit();var p=m.Alive.First();p.waterSince=0;p.waterDebt=1;p.nextRetry=0;p.nextMeal=1e9;m.State.seconds=1199.9;m.State.nextPayroll=1e9;m.State.player.nextMeal=1e9;
            var w=m.State.sites.First(s=>s.type=="warehouse");var f=m.Front(w);p.x=f.x;p.y=f.y;p.cargo.Add("water",PlanStock.Scale);p.trip=new PlanTrip{source="backpack",target="input:"+w.id,item="water",ticks=PlanStock.Scale,picked=true,elapsed=1.9};m.SetPaused(false);m.Advance(.1);
            Assert.That(p.dead,Is.False);Assert.That(p.waterDebt,Is.Zero);Assert.That(p.waterSince,Is.EqualTo(-1));
        }
        [Test]public void PartialConsumableCannotFitIntoSmallerPhysicalSpace()
        {var m=Fresh();var s=new PlanStock{capacity=.2};Assert.That(s.Fits(m.Config,"filter_media",100),Is.False);}
        [Test]public void HalfSupplyReducesDebtButDoesNotResetShortageTimer()
        {var m=Fresh();m.State.player.nextMeal=1e9;m.State.player.nextServices=1e9;m.State.player.waterDebt=1;m.State.player.waterSince=0;m.State.seconds=200;m.State.warehouse.Add("water",5000);m.SetPaused(false);m.Advance(.1);Assert.That(m.State.player.waterDebt,Is.EqualTo(.5));Assert.That(m.State.player.waterSince,Is.Zero);m.State.warehouse.Add("water",5000);m.Advance(10);Assert.That(m.State.player.waterDebt,Is.Zero);Assert.That(m.State.player.waterSince,Is.EqualTo(-1));}
        [Test]public void InstanceReservationDoesNotConsumeAnotherJobsOldMotor()
        {var m=Fresh();var stock=new PlanStock();stock.Add("old_motor",PlanStock.Scale,2,0,41,null,"scrap_only");stock.Add("old_motor",PlanStock.Scale,2,0,42,null,"repairable");stock.Reserve("old_motor",PlanStock.Scale,41);stock.Reserve("old_motor",PlanStock.Scale,42);var received=stock.Take("old_motor",PlanStock.Scale,true,42);Assert.That(received[0].instance,Is.EqualTo(42));Assert.That(received[0].quality,Is.EqualTo("repairable"));Assert.That(stock.stacks.Single().reserved,Is.EqualTo(PlanStock.Scale));stock.Release("old_motor",PlanStock.Scale,41);Assert.That(stock.Count("old_motor"),Is.EqualTo(PlanStock.Scale));}
        [Test]public void RegionTwoFixedOverflowOccupiesActualOwnedGround()
        {var m=Fresh();m.State.cash=20000;m.BuyRegion(2);var r=m.Config.Region(2);double extra=m.State.ground.Where(g=>PlanSimulation.InRect(g.x,g.y,r.land_rectangle)).Sum(g=>g.stock.Used(m.Config));Assert.That(extra,Is.GreaterThanOrEqualTo(21));Assert.That(m.State.ground.Sum(g=>g.stock.Used(m.Config)),Is.EqualTo(447.5).Within(.00001));}
        private static PlanSite Fixture(PlanSimulation m,string type,string recipe,int x=8,int y=8)
        {
            m.State.technologies=m.Config.technologies.Select(t=>t.id).ToList();m.State.player.nextMeal=1e9;m.State.player.nextServices=1e9;m.State.player.clothes=1e9;
            var s=new PlanSite{id=m.State.nextId++,type=type,recipe=recipe,x=x,y=y,input=new PlanStock{capacity=100},output=new PlanStock{capacity=100}};m.State.sites.Add(s);var f=m.Front(s);m.State.player.x=f.x;m.State.player.y=f.y;m.State.heroSite=s.id;m.SetPaused(false);return s;
        }
        private static void Power(PlanSimulation m,PlanSite consumer)
        {var gen=new PlanSite{id=m.State.nextId++,type="generator",x=12,y=6,input=new PlanStock(),output=new PlanStock{capacity=10}};gen.input.Add("waste_wood",10*PlanStock.Scale);m.State.sites.Add(gen);var f=m.Front(consumer);for(int x=Math.Min(12,f.x);x<=Math.Max(12,f.x);x++)m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="power_line",x=x,y=6});for(int y=7;y<=f.y;y++)m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="power_line",x=f.x,y=y});}
        [Test]public void OrganicSortingProducesItsOwnRecordedComposition()
        {var m=Fresh();var s=Fixture(m,"sorting","organic_sort");s.input.Add("organic_mix",5*PlanStock.Scale);m.Advance(30);Assert.That(s.output.Count("organic_feed"),Is.EqualTo(4*PlanStock.Scale));Assert.That(s.output.Count("organic_residual"),Is.EqualTo(PlanStock.Scale));}
        [Test]public void RegionTwoPlasticKeepsOriginThroughSorting()
        {var m=Fresh();var s=Fixture(m,"sorting","sort_plastic");s.input.Add("plastic_mix",10*PlanStock.Scale,2,0,0,m.Config.Region(2).plastic_parts_per_10);m.Advance(100);Assert.That(s.output.Count("sorted_pp"),Is.EqualTo(3*PlanStock.Scale));Assert.That(s.output.Count("sorted_pet"),Is.EqualTo(2*PlanStock.Scale));Assert.That(s.output.stacks.All(v=>v.origin==2),Is.True);}
        [Test]public void IdentifiedOtherPlasticCannotGeneratePetAgain()
        {var m=Fresh();var s=Fixture(m,"sorting","recognize_old_plastic");s.input.Add("plastic_other",10*PlanStock.Scale,1,0,0,null,"recognized");m.Advance(70);Assert.That(s.active,Is.False);Assert.That(s.output.Count("sorted_pet"),Is.Zero);Assert.That(s.input.Count("plastic_other"),Is.EqualTo(10*PlanStock.Scale));}
        [Test]public void OptionalCaptureYieldsOnlyEightyPercentOfActualProcessWater()
        {var m=Fresh();var s=Fixture(m,"plastic_pre","wash_pp");s.captureEffluent=true;s.input.Add("sorted_pp",3*PlanStock.Scale);s.input.Add("water",PlanStock.Quantity(.25));s.input.Add("soap",PlanStock.Scale,1,0,m.State.nextId++);Power(m,s);m.Advance(70);Assert.That(s.output.Count("flakes_pp"),Is.EqualTo(3*PlanStock.Scale));Assert.That(s.output.Count("industrial_effluent"),Is.EqualTo(2000));Assert.That(s.input.Count("soap"),Is.EqualTo(9800));}
        [Test]public void PurificationUsesOneFilterUnitAndNeverIndustrialWater()
        {var m=Fresh();var s=Fixture(m,"water_purifier","make_drinking_water");s.input.Add("raw_freshwater",6*PlanStock.Scale,1,0,0,null,"qualified_fresh_source");s.input.Add("wood_chips",2500);s.input.Add("filter_media",PlanStock.Scale,1,0,m.State.nextId++);m.Advance(60);Assert.That(s.output.Count("water"),Is.EqualTo(5*PlanStock.Scale));Assert.That(s.input.Count("filter_media"),Is.EqualTo(9900));s.input.Add("raw_freshwater",6*PlanStock.Scale,1,0,0,null,"industrial");s.input.Add("wood_chips",2500);m.Advance(60);Assert.That(s.output.Count("water"),Is.EqualTo(5*PlanStock.Scale));}
        [Test]public void CancelAutoWaterReturnsDrawnWaterOnceWithoutRefillingSource()
        {var m=Fresh();var s=Fixture(m,"auto_water_plant","auto_drinking_water",22,21);s.springRegion=1;m.State.springs[0].site=s.id;s.input.Add("wood_chips",2500);s.input.Add("filter_media",PlanStock.Scale,1,0,m.State.nextId++);m.Advance(.1);Assert.That(m.State.springs[0].remaining,Is.EqualTo(6).Within(.00001));m.CancelBatch(s);Assert.That(s.input.Count("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));m.CancelBatch(s);Assert.That(s.input.Count("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));Assert.That(m.State.springs[0].remaining,Is.EqualTo(6).Within(.00001));Assert.That(s.output.Count("water"),Is.Zero);}
        [Test]public void PlantedCropCancellationReturnsSpentSubstrateButNoSeedOrSpentWater()
        {var m=Fresh();var s=Fixture(m,"growing","grow_tuber");s.input.Add("seed_tuber",PlanStock.Scale);s.input.Add("substrate",PlanStock.Scale);s.input.Add("water",6*PlanStock.Scale);m.Advance(1);m.CancelBatch(s);Assert.That(s.input.Count("seed_tuber"),Is.Zero);Assert.That(s.input.Count("substrate"),Is.Zero);Assert.That(s.input.Count("spent_substrate"),Is.EqualTo(PlanStock.Scale));Assert.That(s.input.Count("water"),Is.EqualTo(3*PlanStock.Scale));}
        [Test]public void PackedPlasticRoundTripPreservesActualRegionAndQuantity()
        {var m=Fresh();var s=Fixture(m,"baler","pack_plastic_mix");s.input.Add("plastic_mix",10*PlanStock.Scale,2,0,0,m.Config.Region(2).plastic_parts_per_10);s.input.Add("pet_strap",PlanStock.Scale);Power(m,s);m.Advance(60);Assert.That(s.output.stacks.First().payload[0].origin,Is.EqualTo(2));var state=JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture()));var restored=new PlanSimulation(m.Config,1,state);var packet=restored.Site(s.id).output.Take("pack_plastic_mix",PlanStock.Scale);var unpack=Fixture(restored,"disassembly","unpack_plastic_mix",18,8);unpack.input.Put(packet);restored.Advance(25);Assert.That(unpack.output.Count("plastic_mix"),Is.EqualTo(10*PlanStock.Scale));Assert.That(unpack.output.stacks.First(v=>v.item=="plastic_mix").origin,Is.EqualTo(2));Assert.That(unpack.output.Count("sorted_pet"),Is.EqualTo(PlanStock.Scale));}
        [Test]public void CancellingReservedFilterRejoinsTheOriginalPhysicalBar()
        {var m=Fresh();var s=Fixture(m,"water_purifier","make_drinking_water");s.input.Add("raw_freshwater",6*PlanStock.Scale,1,0,0,null,"qualified_fresh_source");s.input.Add("wood_chips",2500);s.input.Add("filter_media",PlanStock.Scale,1,0,m.State.nextId++);m.Advance(.1);m.CancelBatch(s);Assert.That(s.input.Count("filter_media"),Is.EqualTo(PlanStock.Scale));Assert.That(s.input.stacks.Count(v=>v.item=="filter_media"),Is.EqualTo(1));}
        [Test]public void BeltMergeAlternatesSendersAndMovesOnlyOneEntity()
        {var m=Fresh();m.SetPaused(false);var a=new PlanSite{id=m.State.nextId++,type="belt",x=8,y=8,direction=0,beltProgress=1};var b=new PlanSite{id=m.State.nextId++,type="belt",x=9,y=7,direction=1,beltProgress=1};var to=new PlanSite{id=m.State.nextId++,type="belt",x=9,y=8,direction=0};m.State.sites.AddRange(new[]{a,b,to});a.input.Add("boards",PlanStock.Scale);b.input.Add("steel_parts",PlanStock.Scale);Power(m,a);m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="power_line",x=9,y=7});m.Advance(.1);Assert.That(to.input.stacks.Count,Is.EqualTo(1));int first=to.mergeTurn;to.input.stacks.Clear();m.Advance(.1);Assert.That(to.mergeTurn,Is.Not.EqualTo(first));Assert.That(to.input.stacks.Count,Is.EqualTo(1));}
    }
}
