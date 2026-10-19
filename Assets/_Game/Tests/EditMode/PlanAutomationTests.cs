using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanAutomationTests
    {
        private static PlanSimulation Fresh()
        {
            var config=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(config);
            m.State.ground.Clear();m.State.warehouse.capacity=10000;foreach(var item in config.items)m.Discover(item.id);
            m.State.warehouse.Add("boards",100*PlanStock.Scale);m.State.warehouse.Add("steel_parts",100*PlanStock.Scale);return m;
        }
        private static PlanSite Belt(PlanSimulation m,int x,int y,int direction)
        {var s=new PlanSite{id=m.State.nextId++,type="belt",x=x,y=y,direction=direction,powerRatio=1};m.State.sites.Add(s);return s;}
        [TestCase(false)][TestCase(true)] public void RouteHasNoGapsAndCornerPointsToNextCell(bool vertical)
        {
            var route=PlanSimulation.BeltPath(8,8,11,10,vertical);Assert.That(route.Length,Is.EqualTo(6));Assert.That(route.Select(c=>(c.x,c.y)).Distinct().Count(),Is.EqualTo(6));
            for(int i=0;i<route.Length-1;i++){var a=route[i];var b=route[i+1];Assert.That(Math.Abs(b.x-a.x)+Math.Abs(b.y-a.y),Is.EqualTo(1));Assert.That(a.direction,Is.EqualTo(b.x>a.x?0:b.y>a.y?1:b.x<a.x?2:3));}
            Assert.That(route[1].x,Is.EqualTo(vertical?8:9));
        }
        [Test] public void RoutePreviewIsReadOnlyAndChargesCombinedBillExactlyOnce()
        {
            var m=Fresh();string before=JsonUtility.ToJson(m.Capture());var preview=m.InspectBeltRoute(8,8,10,10);Assert.That(preview.Valid,Is.True,preview.problem);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            long boards=m.Stored("boards");var built=m.BuildBeltRoute(8,8,10,10);Assert.That(built.Length,Is.EqualTo(5));Assert.That(m.Stored("boards"),Is.EqualTo(boards-PlanStock.Quantity(2.5)));Assert.That(built.All(s=>s.pending),Is.True);
            before=JsonUtility.ToJson(m.Capture());var reused=m.BuildBeltRoute(8,8,10,10);Assert.That(reused.Select(s=>s.id),Is.EqualTo(built.Select(s=>s.id)));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void InsufficientTotalMaterialsRejectsWholeRouteWithoutPartialConstruction()
        {
            var m=Fresh();m.State.warehouse.Take("boards",m.Stored("boards")-PlanStock.Scale);string before=JsonUtility.ToJson(m.Capture());
            Assert.That(m.InspectBeltRoute(8,8,12,8).problem,Does.Contain("整段建材不足"));Assert.Throws<InvalidOperationException>(()=>m.BuildBeltRoute(8,8,12,8));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void ObstacleInMiddleRejectsWholeRouteAndConflictingDirectionIsNotOverwritten()
        {
            var m=Fresh();Belt(m,10,8,2);string before=JsonUtility.ToJson(m.Capture());Assert.That(m.InspectBeltRoute(8,8,12,8).Valid,Is.False);Assert.Throws<InvalidOperationException>(()=>m.BuildBeltRoute(8,8,12,8));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test] public void RouteReusesOutputAndInputPortsWithoutChargingForTheirCells()
        {
            var m=Fresh();var output=new PlanSite{id=m.State.nextId++,type="port",x=8,y=8,portMode="output",direction=0};var input=new PlanSite{id=m.State.nextId++,type="port",x=12,y=8,portMode="input"};m.State.sites.AddRange(new[]{output,input});
            var route=m.InspectBeltRoute(8,8,12,8);Assert.That(route.Valid,Is.True,route.problem);Assert.That(route.newCount,Is.EqualTo(3));var sites=m.BuildBeltRoute(8,8,12,8);Assert.That(sites.First(),Is.SameAs(output));Assert.That(sites.Last(),Is.SameAs(input));
        }
        [Test] public void IndependentIntersectionMarksBothLanesAndRejectsTurningAtIntersection()
        {
            var m=Fresh();var vertical=m.Build("belt",10,8,1);m.Build("belt",10,8,0,true);Assert.That(vertical.crossing,Is.True);
            Assert.That(m.InspectBeltRoute(8,8,10,10,false,true).Valid,Is.False);
        }
        [Test] public void CrossingsKeepCargoOnItsAxisAndSingleCrossingModeBeltCanStillTurn()
        {
            var m=Fresh();var west=Belt(m,8,9,0);var vertical=Belt(m,9,9,1);vertical.crossing=true;var horizontal=Belt(m,9,9,0);horizontal.crossing=true;west.input.Add("boards",PlanStock.Scale);
            Assert.That(m.InspectTransport(west).nextSite,Is.EqualTo(horizontal.id));horizontal.input.Add("boards",PlanStock.Scale);Assert.That(m.InspectTransport(west).issue,Is.EqualTo("output"));
            m.State.sites.Remove(horizontal);Assert.That(m.InspectTransport(west).nextSite,Is.EqualTo(vertical.id));
        }
        [Test] public void LegacyTwoLaneCrossingIsNormalizedWhenLoadingWithoutLosingCargo()
        {
            var m=Fresh();var a=Belt(m,8,8,0);var b=Belt(m,8,8,1);b.crossing=true;a.input.Add("boards",PlanStock.Scale);
            var loaded=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(loaded.Site(a.id).crossing,Is.True);Assert.That(loaded.Site(a.id).input.Count("boards"),Is.EqualTo(PlanStock.Scale));
        }
        [Test] public void PortPreviewSharesActualPerimeterAndCapacityValidation()
        {
            var m=Fresh();var w=m.State.sites.First(s=>s.type=="warehouse");var p=new PlanSite{id=m.State.nextId++,type="port",x=w.x,y=w.y};m.State.sites.Add(p);
            Assert.That(m.PortConnectionProblem(p,w,"input"),Is.Null);m.ConfigurePort(p,w,"input");string before=JsonUtility.ToJson(m.Capture());Assert.That(m.PortConnectionProblem(p,w,"output"),Is.Null);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            p.x=w.x+1;p.y=w.y+1;Assert.That(m.PortConnectionProblem(p,w,"input"),Does.Contain("周边"));p.x=w.x;p.y=w.y;
            for(int i=0;i<m.Config.Facility(w.type).ports;i++)m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="port",portSite=w.id});
            Assert.That(m.PortConnectionProblem(p,w,"input"),Does.Contain("已满"));Assert.That(Assert.Throws<InvalidOperationException>(()=>m.ConfigurePort(p,w,"input")).Message,Does.Contain("已满"));
        }
        [Test] public void BeltDiagnosticsDistinguishMissingDisabledFullAndFilteredDownstream()
        {
            var m=Fresh();var from=Belt(m,8,8,0);from.input.Add("boards",PlanStock.Scale);Assert.That(m.InspectTransport(from).status,Does.Contain("未连接"));
            var to=Belt(m,9,8,0);to.enabled=false;Assert.That(m.InspectTransport(from).status,Does.Contain("停止"));to.enabled=true;to.input.Add("boards",PlanStock.Scale);Assert.That(m.InspectTransport(from).issue,Is.EqualTo("output"));
            to.input.stacks.Clear();to.filter="steel_parts";Assert.That(m.InspectTransport(from).issue,Is.EqualTo("filter"));to.filter=null;Assert.That(m.InspectTransport(from).blocked,Is.False);from.powerRatio=0;Assert.That(m.InspectTransport(from).issue,Is.EqualTo("power"));
        }
        [Test] public void InputPortExplainsRecipeMismatchAndFullMachineIncludingHeldInputs()
        {
            var m=Fresh();var machine=new PlanSite{id=m.State.nextId++,type="wood",recipe="wood_boards",x=8,y=8};machine.input.capacity=4;m.State.sites.Add(machine);
            var port=new PlanSite{id=m.State.nextId++,type="port",x=8,y=8,portSite=machine.id};m.State.sites.Add(port);port.input.Add("steel_parts",PlanStock.Scale);Assert.That(m.InspectTransport(port).issue,Is.EqualTo("filter"));
            port.input.stacks.Clear();port.input.Add("waste_wood",PlanStock.Scale);machine.reserved.Add("waste_wood",4*PlanStock.Scale);Assert.That(m.InspectTransport(port).issue,Is.EqualTo("output"));machine.reserved.stacks.Clear();Assert.That(m.InspectTransport(port).blocked,Is.False);
        }
        [Test] public void TransportAndWorkerQueriesNeverTouchReservationsOrTaskState()
        {
            var m=Fresh();var s=Belt(m,8,8,0);s.input.Add("boards",PlanStock.Scale);var p=new PlanPerson{id=m.State.nextId++,trip=new PlanTrip{source="ground:999",target="input:999",item="boards",ticks=PlanStock.Scale}};m.State.people.Add(p);
            string before=JsonUtility.ToJson(m.Capture());Assert.That(m.WorkerTask(p),Does.Contain("尚未取走"));Assert.That(m.WorkerTask(p),Does.Contain("已移除"));m.InspectTransport(s);Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            p.trip.picked=true;p.directed=true;p.commandX=8;p.commandY=8;Assert.That(m.WorkerTask(p),Does.Contain("已携带"));Assert.That(m.WorkerTask(p),Does.Contain("原工作挂起"));
        }
        [Test] public void LoadedAndNewIndependentCrossingsActuallyTransferToMatchingLane()
        {
            var m=Fresh();m.SetPaused(false);var west=Belt(m,8,8,0);west.beltProgress=1;var vertical=Belt(m,9,8,1);var horizontal=Belt(m,9,8,0);horizontal.crossing=true;vertical.crossing=true;west.input.Add("boards",PlanStock.Scale);
            var generator=new PlanSite{id=m.State.nextId++,type="generator",x=7,y=7,fuelSeconds=120};m.State.sites.Add(generator);for(int x=7;x<=10;x++)m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="power_line",x=x,y=8});
            m.Advance(.1);Assert.That(horizontal.input.Count("boards"),Is.EqualTo(PlanStock.Scale));Assert.That(vertical.input.Count("boards"),Is.Zero);
        }
        [Test] public void RotatingCrossingKeepsSeparateAxesAndRetainsCargo()
        {
            var m=Fresh();var horizontal=Belt(m,8,8,0);var vertical=Belt(m,8,8,1);horizontal.crossing=vertical.crossing=true;horizontal.input.Add("boards",PlanStock.Scale);
            m.RotateTransport(horizontal);Assert.That(horizontal.direction,Is.EqualTo(2));Assert.That(vertical.direction,Is.EqualTo(1));Assert.That(horizontal.input.Count("boards"),Is.EqualTo(PlanStock.Scale));
            m.State.sites.Remove(vertical);m.RotateTransport(horizontal);Assert.That(horizontal.direction,Is.EqualTo(3));
        }
    }
}
