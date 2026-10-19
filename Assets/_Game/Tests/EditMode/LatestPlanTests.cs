using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class LatestPlanTests
    {
        private static PlanConfig Config(){var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));c.Validate();return c;}
        private static PlanSimulation Fresh(){var m=new PlanSimulation(Config());m.State.player.nextMeal=1e9;m.State.player.nextServices=1e9;return m;}
        private static void Materials(PlanSimulation m,string type){foreach(var a in m.Config.Facility(type).build){m.State.warehouse.Add(a.item,a.Ticks);m.Discover(a.item);}m.State.warehouse.capacity=10000;}
        private static PlanSimulation Community(int count=3){var m=Fresh();m.State.recognized=true;m.State.cash=100000;for(int i=0;i<count;i++)m.Recruit();return m;}
        [Test]public void LatestCatalogAndStartingLandMatchSuppliedVersion()
        {var m=Fresh();Assert.That(m.Config.items.Length,Is.EqualTo(201));Assert.That(m.Config.facilities.Length,Is.EqualTo(53));Assert.That(m.Config.recipes.Length,Is.EqualTo(216));Assert.That(m.Config.technologies.Length,Is.EqualTo(54));Assert.That(m.Owned(6,6),Is.True);Assert.That(m.Owned(23,23),Is.True);Assert.That(m.Owned(24,23),Is.True);Assert.That(m.Owned(30,15),Is.False);Assert.That(m.State.ground.Sum(g=>g.stock.Used(m.Config)),Is.EqualTo(246.5).Within(.00001));}
        [Test]public void TechnologyUsesHistoricalAcquisitionWithoutChargingOrConsuming()
        {var m=Fresh();m.Discover("boards");Assert.That(m.State.technologies.Contains("workbench"),Is.False);m.Discover("steel_parts");Assert.That(m.State.technologies.Contains("workbench"),Is.True);Assert.That(m.State.cash,Is.Zero);Assert.That(m.Stored("boards"),Is.Zero);m.Discover("boards");Assert.That(m.State.technologies.Count(t=>t=="workbench"),Is.EqualTo(1));}
        [Test]public void ExpansionRequiresOnlyCashAndPreviousOwnershipAndSpawnsOnce()
        {var m=Fresh();m.State.cash=19999;Assert.Throws<InvalidOperationException>(()=>m.BuyRegion(2));m.State.cash=20000;m.BuyRegion(2);Assert.That(m.State.cash,Is.Zero);Assert.That(m.Owned(28,6),Is.True);Assert.That(m.State.springs.Count,Is.EqualTo(2));int count=m.State.ground.Count;Assert.Throws<InvalidOperationException>(()=>m.BuyRegion(2));Assert.That(m.State.ground.Count,Is.EqualTo(count));}
        [Test]public void FractionalSoapAndFilterRemainPhysicalBars()
        {var c=Config();var s=new PlanStock();s.Add("soap",PlanStock.Scale);s.Take("soap",100);Assert.That(s.Count("soap"),Is.EqualTo(9900));Assert.That(s.Used(c),Is.EqualTo(.5));s.Add("filter_media",PlanStock.Scale);s.Take("filter_media",100);Assert.That(s.Used(c),Is.EqualTo(1));}
        [Test]public void InvalidBuildingDoesNotChargeMaterials()
        {var m=Fresh();Materials(m,"wood");long before=m.Stored("waste_wood");Assert.Throws<InvalidOperationException>(()=>m.Build("wood",32,6));Assert.That(m.Stored("waste_wood"),Is.EqualTo(before));}
        [Test]public void PausedBlueprintFinishesOnlyAfterResume()
        {var m=Fresh();Materials(m,"wood");var s=m.Build("wood",8,8);Assert.That(s.pending,Is.True);m.Advance(100);Assert.That(s.pending,Is.True);m.SetPaused(false);m.Advance(.1);Assert.That(s.pending,Is.False);}
        [Test]public void LifeAndSeedReservesProtectSales()
        {var m=Fresh();m.State.warehouse.Add("food",10*PlanStock.Scale);m.Sell("food",10*PlanStock.Scale);Assert.That(m.Stored("food"),Is.EqualTo(2*PlanStock.Scale));m.State.warehouse.Add("water",10*PlanStock.Scale);m.Sell("water",10*PlanStock.Scale);Assert.That(m.Stored("water"),Is.EqualTo(2*PlanStock.Scale));}
        [Test]public void SoldKnowledgeFloorSurvivesFurtherSalesAndTime()
        {var m=Fresh();m.Discover("waste_wood");m.State.warehouse.Add("boards",20*PlanStock.Scale);m.SellKnowledge("wood");m.Sell("boards",10*PlanStock.Scale);double p=m.Price("boards");m.SetPaused(false);m.Advance(120);Assert.That(m.Price("boards"),Is.EqualTo(p));Assert.That(p,Is.EqualTo(1));Assert.That(m.State.technologies.Contains("manual_wood"),Is.True);Assert.Throws<InvalidOperationException>(()=>m.SellKnowledge("wood"));}
        [Test]public void WaterCollectionReservesSourceAndCancelNeverCreatesWater()
        {var m=Fresh();Materials(m,"water_intake");var site=m.Build("water_intake",22,22);m.SelectRecipe(site,"collect_freshwater");m.State.player.x=23;m.State.player.y=21;m.State.heroSite=site.id;m.SetPaused(false);m.Advance(10);Assert.That(m.State.springs[0].reserved,Is.EqualTo(6));Assert.That(site.output.Count("raw_freshwater"),Is.Zero);m.CancelBatch(site);Assert.That(m.State.springs[0].reserved,Is.Zero);Assert.That(m.State.springs[0].remaining,Is.EqualTo(12));Assert.That(site.output.Count("raw_freshwater"),Is.Zero);}
        [Test]public void SourceCannotBeBoundTwiceAndDemolitionDoesNotRefillIt()
        {var m=Fresh();Materials(m,"water_intake");var site=m.Build("water_intake",22,22);Assert.Throws<InvalidOperationException>(()=>m.Build("water_intake",22,22));m.State.springs[0].remaining=4;m.RemoveSite(site);Assert.That(m.State.springs[0].remaining,Is.EqualTo(4));Assert.That(m.State.springs[0].site,Is.Zero);}
        [Test]public void BonusesRequireNoDebtAndExpireAtScheduledPayroll()
        {var m=Community();m.Bonus();Assert.That(m.Alive.All(p=>Math.Abs(p.bonus-.1)<1e-8),Is.True);m.SetPaused(false);m.Advance(600);Assert.That(m.Alive.All(p=>p.bonus==0),Is.True);Assert.That(m.Debt,Is.EqualTo(3000));Assert.Throws<InvalidOperationException>(()=>m.Bonus());}
        [Test]public void PayrollKeepsOriginalDenominatorAfterPopulationChanges()
        {var m=Community(2);m.SetPaused(false);m.Advance(600);var bill=m.State.bills[0];Assert.That(bill.original,Is.EqualTo(2000));m.Recruit();Assert.That(bill.original,Is.EqualTo(2000));m.PayWages(333);Assert.That(bill.unpaid.Sum(),Is.EqualTo(1667));Assert.That(m.Debt,Is.EqualTo(1667));}
        [Test]public void WageRiotHasFixedTargetAndDoesNotImmediatelyRetrigger()
        {var m=Community(2);m.SetPaused(false);m.Advance(1800);Assert.That(m.State.riot.active,Is.True);long target=m.State.riot.target;Assert.That(target,Is.EqualTo(2000));m.PayWages(target);Assert.That(m.State.riot.active,Is.False);Assert.That(m.Debt,Is.EqualTo(4000));m.Advance(.1);Assert.That(m.State.riot.active,Is.False);}
        [Test]public void LatestSavePreservesInputRngReservationsAndPersonalWallets()
        {var m=Community(2);m.SetPaused(false);m.Advance(100);var state=JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture()));var copy=new PlanSimulation(Config(),1,state);m.Advance(100);copy.Advance(100);Assert.That(copy.State.rng,Is.EqualTo(m.State.rng));Assert.That(copy.State.ground.Sum(g=>g.stock.Used(copy.Config)),Is.EqualTo(m.State.ground.Sum(g=>g.stock.Used(m.Config))));Assert.That(copy.Alive.Sum(p=>p.wallet),Is.EqualTo(m.Alive.Sum(p=>p.wallet)));}
    }
}
