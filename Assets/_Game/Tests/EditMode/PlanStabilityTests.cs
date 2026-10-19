using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanStabilityTests
    {
        private static PlanSimulation Fresh()
        {
            var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(c);
            m.State.ground.Clear();m.State.warehouse.capacity=10000;foreach(var i in c.items)m.Discover(i.id);m.State.player.nextMeal=m.State.player.nextServices=1e9;return m;
        }
        private static void Materials(PlanSimulation m,string type){foreach(var v in m.Config.Facility(type).build)m.State.warehouse.Add(v.item,10*v.Ticks);}
        [Test] public void StoppingHumanBatchReleasesOperatorButPreservesProgressAndMaintenance()
        {
            var m=Fresh();Materials(m,"wood");m.SetPaused(false);var s=m.Build("wood",8,8);m.SelectRecipe(s,"wood_boards");s.input.Add("waste_wood",4*PlanStock.Scale);s.tools.Add("handsaw_wood",PlanStock.Scale,1,300,900);
            var f=m.Front(s);var p=new PlanPerson{id=m.State.nextId++,x=f.x,y=f.y,nextMeal=1e9,nextServices=1e9};m.State.people.Add(p);m.Advance(.2);Assert.That(p.assigned,Is.EqualTo(s.id));
            var machine=new PlanSite{id=m.State.nextId++,type="generator",x=14,y=8,durability=50,enabled=false};m.State.sites.Add(machine);f=m.Front(machine);
            var maintainer=new PlanPerson{id=m.State.nextId++,x=f.x,y=f.y,maintaining=machine.id,nextMeal=1e9,nextServices=1e9};machine.maintainer=maintainer.id;m.State.people.Add(maintainer);
            double progress=s.progress;long reserved=s.reserved.Total("waste_wood");m.SetSiteEnabled(s,false);Assert.That(p.assigned,Is.Zero);m.Advance(1);
            Assert.That(p.assigned,Is.Not.EqualTo(s.id));Assert.That(s.progress,Is.EqualTo(progress));Assert.That(s.reserved.Total("waste_wood"),Is.EqualTo(reserved));Assert.That(machine.durability,Is.GreaterThan(50));Assert.That(maintainer.maintaining,Is.EqualTo(machine.id));
            m.SetSiteEnabled(s,true);m.AssignWorker(p,s);f=m.Front(s);p.x=f.x;p.y=f.y;m.Advance(.2);Assert.That(s.progress,Is.GreaterThan(progress));
        }
        [TestCase("input")][TestCase("output")] public void DisabledPortDoesNotExchangeWithItsMachine(string mode)
        {
            var m=Fresh();var w=m.State.sites.First(s=>s.type=="warehouse");var port=new PlanSite{id=m.State.nextId++,type="port",x=w.x,y=w.y,portSite=w.id,portMode=mode,enabled=false};m.State.sites.Add(port);
            if(mode=="input")port.input.Add("boards",PlanStock.Scale);else w.input.Add("boards",3*PlanStock.Scale);
            long source=mode=="input"?port.input.Count("boards"):w.input.Count("boards");m.SetPaused(false);m.Advance(.2);
            Assert.That(mode=="input"?port.input.Count("boards"):w.input.Count("boards"),Is.EqualTo(source));m.SetSiteEnabled(port,true);m.Advance(.2);Assert.That(mode=="input"?w.input.Count("boards"):port.input.Count("boards"),Is.EqualTo(PlanStock.Scale));
        }
        [Test] public void DisabledInputPortRejectsUpstreamTransferAndRetainsCargo()
        {
            var m=Fresh();var from=new PlanSite{id=501,type="port",x=8,y=8,portMode="output",direction=0};from.input.Add("boards",PlanStock.Scale);
            var to=new PlanSite{id=502,type="port",x=9,y=8,portMode="input",enabled=false};m.State.sites.Add(from);m.State.sites.Add(to);m.SetPaused(false);m.Advance(1.2);
            Assert.That(from.input.Count("boards"),Is.EqualTo(PlanStock.Scale));Assert.That(to.input.Count("boards"),Is.Zero);m.SetSiteEnabled(to,true);m.Advance(.1);Assert.That(to.input.Count("boards"),Is.EqualTo(PlanStock.Scale));
        }
        [Test] public void OlderStoppedCrewIsReleasedWhenLoadingWithoutDroppingBatchInputs()
        {
            var m=Fresh();Materials(m,"wood");var s=m.Build("wood",8,8);s.enabled=false;s.reserved.Add("waste_wood",PlanStock.Scale);s.active=true;s.recipe="wood_boards";
            var p=new PlanPerson{id=m.State.nextId++,assigned=s.id};m.State.people.Add(p);var restored=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));
            Assert.That(restored.Alive.Single().assigned,Is.Zero);Assert.That(restored.Site(s.id).reserved.Count("waste_wood"),Is.EqualTo(PlanStock.Scale));Assert.That(restored.Site(s.id).active,Is.True);
        }
        [Test] public void BuildPreviewIsReadOnlyAndReportsTheSameRejectionAsBuild()
        {
            var m=Fresh();Materials(m,"wood");string before=JsonUtility.ToJson(m.Capture());Assert.That(m.BuildProblem("wood",8,8),Is.Null);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            m.Build("wood",8,8);before=JsonUtility.ToJson(m.Capture());string problem=m.BuildProblem("wood",8,8);Assert.That(problem,Is.Not.Null);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));Assert.That(Assert.Throws<InvalidOperationException>(()=>m.Build("wood",8,8)).Message,Is.EqualTo(problem));
        }
        [Test] public void PreviewReportsMissingMaterialsAndIdleBuildersWithoutCharging()
        {
            var m=Fresh();Assert.That(m.BuildProblem("wood",8,8),Does.Contain("建材不足"));Materials(m,"wood");m.State.heroSite=999;
            string before=JsonUtility.ToJson(m.Capture());Assert.That(m.BuildProblem("wood",8,8),Does.Contain("空闲建设人员不足"));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void WaterPreviewDoesNotClaimOrCreateASource()
        {
            var m=Fresh();Materials(m,"water_intake");string before=JsonUtility.ToJson(m.Capture());Assert.That(m.BuildProblem("water_intake",22,22),Is.Null);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void FailedMovePreviewLeavesFacilityAndBatchUntouched()
        {
            var m=Fresh();Materials(m,"wood");var s=m.Build("wood",8,8);s.input.Add("waste_wood",PlanStock.Scale);string before=JsonUtility.ToJson(m.Capture());string problem=m.MoveProblem(s,11,2);
            Assert.That(problem,Is.Not.Null);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));Assert.That(Assert.Throws<InvalidOperationException>(()=>m.MoveSite(s,11,2)).Message,Is.EqualTo(problem));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));Assert.That(m.MoveProblem(s,15,10),Is.Null);
        }
    }
}
