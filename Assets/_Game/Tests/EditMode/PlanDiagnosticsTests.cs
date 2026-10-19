using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanDiagnosticsTests
    {
        private static PlanSimulation Fresh()
        {
            var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(c);
            m.State.ground.Clear();m.State.warehouse.capacity=10000;foreach(var item in c.items)m.Discover(item.id);return m;
        }
        private static PlanSite Wood(PlanSimulation m)
        {
            foreach(var a in m.Config.Facility("wood").build)m.State.warehouse.Add(a.item,10*a.Ticks);
            var s=m.Build("wood",8,8);s.pending=false;m.SelectRecipe(s,"wood_boards");return s;
        }
        [Test] public void InspectingDoesNotReserveAssignConsumeOrChangeRandomState()
        {
            var m=Fresh();var s=Wood(m);s.input.Add("waste_wood",4*PlanStock.Scale);string before=JsonUtility.ToJson(m.Capture());
            for(int i=0;i<5;i++){m.InspectFacility(s);m.InspectSupply("water");m.InspectSupply("food");m.InspectPayroll();m.UnpoweredHomes();}
            Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void MissingInputsReportExactShortfallAndHeldBatchIsNotMissing()
        {
            var m=Fresh();var s=Wood(m);s.input.Add("waste_wood",PlanStock.Quantity(1.5));var recipe=m.Config.Recipe(s.recipe);
            var read=m.InspectFacility(s);Assert.That(read.issue,Is.EqualTo("input"));Assert.That(read.missing.Single().quantity,Is.EqualTo(recipe.inputs.Single().quantity-1.5).Within(.0001));
            s.active=true;s.reserved.Put(s.input.Take("waste_wood",s.input.Count("waste_wood")));Assert.That(m.InspectFacility(s).missing,Is.Empty);
        }
        [Test] public void ToolChoiceAndArrivalMatchProductionRequirements()
        {
            var m=Fresh();var s=Wood(m);s.input.Add("waste_wood",10*PlanStock.Scale);
            Assert.That(m.InspectFacility(s).issue,Is.EqualTo("tool"));
            var tools=m.Config.items.Where(i=>i.tool_family=="handsaw").OrderByDescending(i=>i.efficiency).ToArray();
            foreach(var tool in tools)s.tools.Add(tool.id,PlanStock.Scale,1,tool.durability/2,m.State.nextId++);
            var read=m.InspectFacility(s);Assert.That(read.tools.Single().item,Is.EqualTo(tools[0].id));Assert.That(read.tools.Single().durability,Is.EqualTo(tools[0].durability/2));Assert.That(read.issue,Is.EqualTo("worker"));
            var front=m.Front(s);var p=new PlanPerson{id=m.State.nextId++,assigned=s.id,x=front.x+2,y=front.y};m.State.people.Add(p);
            Assert.That(m.InspectFacility(s).workersAssigned,Is.EqualTo(1));Assert.That(m.InspectFacility(s).workersHere,Is.Zero);
            p.x=front.x;read=m.InspectFacility(s);Assert.That(read.workersHere,Is.EqualTo(1));Assert.That(read.blocked,Is.False);
        }
        [Test] public void ActiveMachineWithPartialPowerRunsAndRiotDoesNotStopAutomaticStage()
        {
            var m=Fresh();var recipe=m.Config.recipes.First(r=>r.power>0&&r.stages.Any(a=>a.kind=="machine"));
            var s=new PlanSite{id=501,type=recipe.facility,recipe=recipe.id,active=true,stage=Array.FindIndex(recipe.stages,a=>a.kind=="machine"),stageCommitted=true,powerRatio=.5};m.State.sites.Add(s);
            var read=m.InspectFacility(s);Assert.That(read.blocked,Is.False);Assert.That(read.status,Does.Contain("减速"));
            m.State.riot.active=true;Assert.That(m.InspectFacility(s).blocked,Is.False);s.powerRatio=0;Assert.That(m.InspectFacility(s).issue,Is.EqualTo("power"));
            s.enabled=false;Assert.That(m.InspectFacility(s).issue,Is.EqualTo("disabled"));
        }
        [Test] public void FullOutputBlocksNewBatchButNotAnAlreadyReservedBatch()
        {
            var m=Fresh();var s=Wood(m);s.input.Add("waste_wood",10*PlanStock.Scale);s.output.capacity=1;s.output.Add("boards",PlanStock.Scale);
            Assert.That(m.InspectFacility(s).issue,Is.EqualTo("output"));s.active=true;Assert.That(m.InspectFacility(s).issue,Is.Not.EqualTo("output"));
        }
        [Test] public void SupplyCountsOnlyWarehouseAndSubtractsOutstandingNeeds()
        {
            var m=Fresh();m.State.warehouse.stacks.Clear();m.State.warehouse.Add("water",4*PlanStock.Scale);m.State.backpack.Add("water",10*PlanStock.Scale);
            m.State.people.Add(new PlanPerson{id=501,waterDebt=1});m.State.people.Add(new PlanPerson{id=502,dead=true,waterDebt=9});
            var supply=m.InspectSupply("water");Assert.That(supply.available,Is.EqualTo(4*PlanStock.Scale));Assert.That(supply.affected,Is.EqualTo(1));Assert.That(supply.cycles,Is.EqualTo(1.5));
            m.State.externalSupply=true;m.State.cash=100000;Assert.That(m.InspectSupply("water").cycles,Is.EqualTo(1.5));
        }
        [Test] public void PayrollUsesLivingEmployeesExcludesHeroAndReportsDebtSeparately()
        {
            var m=Fresh();Assert.That(m.InspectPayroll().scheduled,Is.False);m.State.seconds=500;m.State.nextPayroll=600;
            m.State.people.Add(new PlanPerson{id=501});m.State.people.Add(new PlanPerson{id=502,dead=true});
            m.State.bills.Add(new PlanBill{ids=new[]{501},unpaid=new long[]{150},original=150});var p=m.InspectPayroll();
            Assert.That(p.scheduled,Is.True);Assert.That(p.remaining,Is.EqualTo(100));Assert.That(p.expected,Is.EqualTo(PlanSimulation.Cents(m.Config.Rule("payroll.salary_per_worker"))));Assert.That(p.unpaid,Is.EqualTo(150));
        }
        [Test] public void OccupiedHomesAreDeduplicatedAndEmptyHomesDoNotWarn()
        {
            var m=Fresh();var occupied=m.State.sites.First(s=>s.type=="housing");occupied.powerRatio=.25;
            var empty=new PlanSite{id=502,type="housing",x=30,y=30,powerRatio=0};m.State.sites.Add(empty);m.State.people.Add(new PlanPerson{id=501});
            Assert.That(m.UnpoweredHomes(),Is.EqualTo(new[]{occupied.id}));occupied.powerRatio=.5;Assert.That(m.UnpoweredHomes(),Is.Empty);
        }
        [Test] public void UnqualifiedFreshwaterCannotSatisfyPurificationInput()
        {
            var m=Fresh();var r=m.Config.recipes.First(a=>a.quality_required=="qualified_fresh_source_only");
            var s=new PlanSite{id=501,type=r.facility,recipe=r.id};m.State.sites.Add(s);foreach(var a in r.inputs)s.input.Add(a.item,a.Ticks);
            Assert.That(m.InspectFacility(s).missing.Any(a=>a.item=="raw_freshwater"),Is.True);
            foreach(var a in s.input.stacks.Where(a=>a.item=="raw_freshwater"))a.quality="qualified_fresh_source";
            Assert.That(m.InspectFacility(s).missing,Is.Empty);
        }
        [Test] public void StagedRecipeReportsOnlyCurrentUncommittedInputs()
        {
            var m=Fresh();var r=m.Config.recipes.First(a=>a.consumption=="staged_at_start"&&a.stages.Length>1&&a.stages.Skip(1).Any(b=>b.water>0||b.commit_inputs.Length>0));
            int stage=Array.FindIndex(r.stages,1,a=>a.water>0||a.commit_inputs.Length>0);var s=new PlanSite{id=501,type=r.facility,recipe=r.id,active=true,stage=stage};m.State.sites.Add(s);
            Assert.That(m.InspectFacility(s).missing,Is.Not.Empty);s.stageCommitted=true;Assert.That(m.InspectFacility(s).missing,Is.Empty);
        }
        [Test] public void WaterSourceBudgetWaitAndMaintenanceRestartAreExplained()
        {
            var m=Fresh();foreach(var a in m.Config.Facility("water_intake").build)m.State.warehouse.Add(a.item,10*a.Ticks);
            var s=m.Build("water_intake",22,22);s.pending=false;var r=m.Config.recipes.First(a=>a.facility==s.type&&a.source_liters>0);m.SelectRecipe(s,r.id);m.WaterSource(s).remaining=0;
            Assert.That(m.InspectFacility(s).issue,Is.EqualTo("source"));s.stopped=true;s.durability=19;Assert.That(m.InspectFacility(s).issue,Is.EqualTo("maintenance"));
            s.durability=20;Assert.That(m.InspectFacility(s).issue,Is.EqualTo("source"));
        }
    }
}
