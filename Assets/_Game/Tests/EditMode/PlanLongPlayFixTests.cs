using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanLongPlayFixTests
    {
        private PlanSimulation Fresh()
        {
            var model=new PlanSimulation(JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json")));
            model.State.ground.Clear();foreach(string item in new[]{"waste_wood","fasteners","wire"}){model.Discover(item);model.State.warehouse.Add(item,20*PlanStock.Scale);}return model;
        }
        [Test]public void NewMachineCannotCoverAnExistingWarehouseOperationSide()
        {
            var m=Fresh();var front=m.Front(m.State.sites.First(s=>s.type=="warehouse"));string before=JsonUtility.ToJson(m.Capture());
            string problem=m.BuildProblem("wood",front.x,front.y,2);Assert.That(problem,Does.Contain("会堵住"));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>m.Build("wood",front.x,front.y,2)).Message,Is.EqualTo(problem));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test]public void MovingCannotCoverAnotherOperationSideOrLoseTheOriginalMachine()
        {
            var m=Fresh();var wood=m.Build("wood",8,8,2);var front=m.Front(m.State.sites.First(s=>s.type=="warehouse"));string before=JsonUtility.ToJson(m.Capture());
            Assert.That(m.MoveProblem(wood,front.x,front.y),Does.Contain("会堵住"));Assert.Throws<InvalidOperationException>(()=>m.MoveSite(wood,front.x,front.y));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test]public void WalkablePowerLineIsAllowedOnAnExistingOperationSide()
        {
            var m=Fresh();m.State.technologies.Add(m.Config.Facility("power_line").technology);var front=m.Front(m.State.sites.First(s=>s.type=="warehouse"));Assert.That(m.BuildProblem("power_line",front.x,front.y),Is.Null);
            m.Build("power_line",front.x,front.y);Assert.That(m.Walkable(front.x,front.y),Is.True);
        }
        [TestCase(true)][TestCase(false)]public void OnlyUsableToolsAreDeliveredToProductionToolSlots(bool broken)
        {
            var m=Fresh();var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");wood.input.Add("waste_wood",4*PlanStock.Scale);
            m.State.warehouse.stacks.RemoveAll(s=>s.item=="waste_wood");string item=broken?"broken_handsaw_wood":"handsaw_wood";m.State.warehouse.Add(item,PlanStock.Scale,1,broken?0:300,999);
            m.State.people.Add(new PlanPerson{id=m.State.nextId++,x=12,y=5});m.SetPaused(false);m.Advance(60);
            if(broken){Assert.That(wood.tools.Count(item),Is.Zero);Assert.That(m.Stored(item),Is.EqualTo(PlanStock.Scale));}
            else Assert.That(wood.tools.Count(item),Is.EqualTo(PlanStock.Scale));
        }
        [Test]public void ActualRawWaterBatchCanTravelInOneTripWithoutLosingQuality()
        {
            var m=Fresh();foreach(var item in m.Config.items)m.Discover(item.id);foreach(var amount in m.Config.Facility("water_intake").build)m.State.warehouse.Add(amount.item,amount.Ticks);
            var intake=m.Build("water_intake",22,22);m.SelectRecipe(intake,"collect_freshwater");var front=m.Front(intake);m.State.player.x=front.x;m.State.player.y=front.y;m.AssignHero(intake.id);m.SetPaused(false);m.Advance(35);m.AssignHero(intake.id);
            Assert.That(intake.output.Count("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));Assert.That(intake.output.stacks.Single().instance,Is.GreaterThan(0));
            var warehouse=m.State.sites.First(s=>s.type=="warehouse");var worker=new PlanPerson{id=m.State.nextId++,x=12,y=5,manualAssignment=warehouse.id};m.State.people.Add(worker);m.Advance(.1);
            Assert.That(worker.trip.item,Is.EqualTo("raw_freshwater"));Assert.That(worker.trip.ticks,Is.EqualTo(6*PlanStock.Scale));m.Advance(180);
            Assert.That(m.Stored("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));Assert.That(warehouse.input.stacks.Where(s=>s.item=="raw_freshwater").All(s=>s.quality=="qualified_fresh_source"),Is.True);
        }
        private PlanPerson Hauler(PlanSimulation m)
        {var person=new PlanPerson{id=m.State.nextId++,x=12,y=5};m.State.people.Add(person);return person;}
        [Test]public void CriticalFuelIsDeliveredBeforeRoutineProductionOutputs()
        {
            var m=Fresh();var wood=m.Build("wood",8,8);wood.output.Add("boards",3*PlanStock.Scale);
            var generator=new PlanSite{id=m.State.nextId++,type="generator",x=12,y=6};m.State.sites.Add(generator);var p=Hauler(m);m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip.item,Is.EqualTo("waste_wood"));Assert.That(p.trip.target,Is.EqualTo("input:"+generator.id));
        }
        [Test]public void EmergencyPurificationInputsComeBeforeNonLifeOutputs()
        {
            var m=Fresh();var wood=m.Build("wood",8,8);wood.output.Add("boards",6*PlanStock.Scale);
            var purifier=new PlanSite{id=m.State.nextId++,type="water_purifier",recipe="make_drinking_water",x=18,y=12};m.State.sites.Add(purifier);
            m.State.warehouse.Add("raw_freshwater",6*PlanStock.Scale,1,0,999,null,"qualified_fresh_source");var p=Hauler(m);m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip.item,Is.EqualTo("raw_freshwater"));Assert.That(p.trip.target,Is.EqualTo("input:"+purifier.id));
        }
        [Test]public void SafeLifeBufferTopUpDoesNotStarveRoutineProductionInputs()
        {
            var m=Fresh();var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");wood.tools.Add("handsaw_wood",PlanStock.Scale,1,300,999);
            m.State.warehouse.Add("water",3*PlanStock.Scale);m.State.warehouse.Add("food",4*PlanStock.Scale);var ground=new PlanGround{id=m.State.nextId++,x=5,y=10};ground.stock.Add("water",8*PlanStock.Scale);m.State.ground.Add(ground);
            var p=Hauler(m);m.SetPaused(false);m.Advance(.1);Assert.That(p.trip.item,Is.EqualTo("waste_wood"));Assert.That(p.trip.target,Is.EqualTo("input:"+wood.id));
        }
        [Test]public void PurifierCanReceiveActualRawOutputDirectlyFromTheIntake()
        {
            var m=Fresh();var intake=new PlanSite{id=m.State.nextId++,type="water_intake",x=22,y=22};intake.output.Add("raw_freshwater",6*PlanStock.Scale,1,0,999,null,"qualified_fresh_source");m.State.sites.Add(intake);
            var purifier=new PlanSite{id=m.State.nextId++,type="water_purifier",recipe="make_drinking_water",x=18,y=12};m.State.sites.Add(purifier);var p=Hauler(m);m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip.source,Is.EqualTo("output:"+intake.id));Assert.That(p.trip.target,Is.EqualTo("input:"+purifier.id));Assert.That(p.trip.ticks,Is.EqualTo(6*PlanStock.Scale));
        }
        [Test]public void AlreadyBufferedRawWaterDoesNotKeepFillingWarehouses()
        {
            var m=Fresh();var intake=new PlanSite{id=m.State.nextId++,type="water_intake",recipe="collect_freshwater",x=22,y=22};intake.output.Add("raw_freshwater",6*PlanStock.Scale,1,0,999,null,"qualified_fresh_source");m.State.sites.Add(intake);
            m.State.warehouse.Add("raw_freshwater",12*PlanStock.Scale,1,0,998,null,"qualified_fresh_source");var p=Hauler(m);p.manualAssignment=m.State.sites.First(s=>s.type=="warehouse").id;m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip,Is.Null);Assert.That(intake.output.Count("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));
        }
        [Test]public void MissingFuelIngredientDoesNotCauseEndlessPrefetchOfOtherRecipeInputs()
        {
            var m=Fresh();var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");wood.tools.Add("handsaw_wood",PlanStock.Scale,1,300,999);
            var purifier=new PlanSite{id=m.State.nextId++,type="water_purifier",recipe="make_drinking_water",x=18,y=12};purifier.input.Add("raw_freshwater",6*PlanStock.Scale,1,0,998,null,"qualified_fresh_source");purifier.input.Add("filter_media",PlanStock.Scale,1,0,997);m.State.sites.Add(purifier);
            m.State.warehouse.Add("raw_freshwater",6*PlanStock.Scale,1,0,996,null,"qualified_fresh_source");var p=Hauler(m);m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip.item,Is.EqualTo("waste_wood"));Assert.That(p.trip.target,Is.EqualTo("input:"+wood.id));
        }
        [Test]public void ProtectedSeedCanBeDeliveredForPlantingWhileRemainingProtectedFromSale()
        {
            var m=Fresh();var bed=new PlanSite{id=m.State.nextId++,type="growing",recipe="grow_tuber",x=18,y=11};bed.input.Add("substrate",PlanStock.Scale);bed.input.Add("water",6*PlanStock.Scale);m.State.sites.Add(bed);m.State.warehouse.Add("seed_tuber",PlanStock.Scale);
            Assert.That(m.Surplus("seed_tuber"),Is.Zero);Assert.Throws<InvalidOperationException>(()=>m.Sell("seed_tuber",PlanStock.Scale));var p=Hauler(m);m.SetPaused(false);m.Advance(.1);
            Assert.That(p.trip.item,Is.EqualTo("seed_tuber"));Assert.That(p.trip.target,Is.EqualTo("input:"+bed.id));
        }
        [Test]public void AutoToolBudgetIsNotSpentOnStoppedProductionLines()
        {
            var m=Fresh();var wood=m.Build("wood",8,8);m.SelectRecipe(wood,"wood_boards");m.SetSiteEnabled(wood,false);m.State.autoTools=true;m.State.cash=10000;m.SetPaused(false);m.Advance(.1);
            Assert.That(m.State.cash,Is.EqualTo(10000));Assert.That(m.State.toolBudgetUsed,Is.Zero);
        }
    }
}
