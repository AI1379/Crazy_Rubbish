using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanCommandTests
    {
        private static PlanSimulation Fresh(int count=2)
        {
            var config=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(config);
            m.State.recognized=true;m.State.cash=100000;for(int i=0;i<count;i++)m.Recruit();return m;
        }
        private static PlanSimulation Restore(PlanSimulation m)=>new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));
        [Test]public void PausedGroupOrdersRestoreAndHoldAtDistinctDestinations()
        {
            var m=Fresh();var ids=m.Alive.Select(p=>p.id).Prepend(0).ToArray();m.CommandMove(ids,9,12);
            var positions=ids.Select(id=>(m.Person(id).x,m.Person(id).y)).ToArray();m.Advance(10);
            Assert.That(ids.Select(id=>(m.Person(id).x,m.Person(id).y)).ToArray(),Is.EqualTo(positions));
            m=Restore(m);Assert.That(ids.All(id=>m.Person(id).directed),Is.True);Assert.That(ids.Select(id=>(m.Person(id).commandX,m.Person(id).commandY)).Distinct().Count(),Is.EqualTo(ids.Length));
            m.SetPaused(false);m.Advance(25);
            foreach(int id in ids){var p=m.Person(id);Assert.That(p.x,Is.EqualTo(p.commandX).Within(.01));Assert.That(p.y,Is.EqualTo(p.commandY).Within(.01));}
            positions=ids.Select(id=>(m.Person(id).x,m.Person(id).y)).ToArray();m.Advance(5);Assert.That(ids.Select(id=>(m.Person(id).x,m.Person(id).y)).ToArray(),Is.EqualTo(positions));
        }
        [Test]public void InvalidGroupOrdersLeaveExistingOrdersAndReservationsUntouched()
        {
            var m=Fresh();int id=m.Alive.First().id;m.CommandMove(new[]{id},9,12);string before=JsonUtility.ToJson(m.Capture());
            Assert.Throws<InvalidOperationException>(()=>m.CommandMove(new[]{id},-1,-1));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
            Assert.Throws<InvalidOperationException>(()=>m.CommandMove(new[]{id,999999},9,10));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));
        }
        [Test]public void PendingTransportReservationsRemainValidWhileHeldAndResume()
        {
            var m=Fresh(1);var p=m.Alive.First();var pile=m.State.ground.First(g=>g.stock.Count("waste_wood")>=PlanStock.Scale);
            pile.stock.Reserve("waste_wood",PlanStock.Scale);p.trip=new PlanTrip{source="ground:"+pile.id,target=m.WarehouseCode(m.State.warehouse),item="waste_wood",ticks=PlanStock.Scale};
            m.CommandMove(new[]{p.id},9,12);m.SetPaused(false);m.Advance(20);Assert.That(p.trip,Is.Not.Null);Assert.That(p.trip.picked,Is.False);Assert.That(pile.stock.stacks.Where(s=>s.item=="waste_wood").Sum(s=>s.reserved),Is.EqualTo(PlanStock.Scale));
            m=Restore(m);p=m.Alive.First();m.ResumePeople(new[]{p.id});for(int i=0;i<1800&&m.Stored("waste_wood")<PlanStock.Scale;i++)m.Advance(.1);Assert.That(m.Stored("waste_wood"),Is.GreaterThanOrEqualTo(PlanStock.Scale),"恢复后应沿实际地形完成原运输");
        }
        [Test]public void LoadedCargoIsNotDroppedDuplicatedOrDeliveredWhileHeld()
        {
            var m=Fresh(1);var p=m.Alive.First();p.cargo.Add("steel_parts",PlanStock.Scale);
            p.trip=new PlanTrip{source="ground:999999",target=m.WarehouseCode(m.State.warehouse),item="steel_parts",ticks=PlanStock.Scale,picked=true};
            long total=m.Stored("steel_parts")+p.cargo.Count("steel_parts");m.CommandMove(new[]{p.id},9,12);m.SetPaused(false);m.Advance(20);
            Assert.That(p.cargo.Count("steel_parts"),Is.EqualTo(PlanStock.Scale));Assert.That(m.Stored("steel_parts")+p.cargo.Count("steel_parts"),Is.EqualTo(total));
            m=Restore(m);p=m.Alive.First();m.ResumePeople(new[]{p.id});m.Advance(25);Assert.That(m.Stored("steel_parts"),Is.GreaterThanOrEqualTo(PlanStock.Scale));
        }
        [Test]public void DirectedWorkerCannotAlsoManufactureUntilResumed()
        {
            var m=Fresh(1);foreach(var cost in m.Config.Facility("wood").build){m.Discover(cost.item);m.State.warehouse.Add(cost.item,cost.Ticks);}var site=m.Build("wood",8,8);m.SelectRecipe(site,"wood_boards");
            var p=m.Alive.First();p.assigned=site.id;site.tools.Add("handsaw_wood",PlanStock.Scale,1,300);site.input.Add("waste_wood",4*PlanStock.Scale);
            m.CommandMove(new[]{p.id},9,12);m.SetPaused(false);m.Advance(20);Assert.That(site.output.Count("boards"),Is.Zero);Assert.That(site.progress,Is.Zero);Assert.That(p.directed,Is.True);
            m.ResumePeople(new[]{p.id});m.Advance(60);Assert.That(site.output.Count("boards"),Is.GreaterThanOrEqualTo(3*PlanStock.Scale));
        }
        [Test]public void RiotAndPauseCannotBeBypassedWithMovementOrders()
        {
            var m=Fresh(1);var p=m.Alive.First();m.CommandMove(new[]{p.id},9,12);double x=p.x,y=p.y;m.State.riot.active=true;m.State.riot.deadline=10000;m.State.riot.wages=true;m.State.riot.target=1000;
            Assert.Throws<InvalidOperationException>(()=>m.CommandMove(new[]{p.id},9,10));m.SetPaused(false);m.Advance(1);Assert.That(p.x,Is.EqualTo(x));Assert.That(p.y,Is.EqualTo(y));
        }
        [Test]public void HeroMovementCancelsPickupReservationWithoutDuplicatingItems()
        {
            var m=Fresh(0);var g=m.State.ground.First(a=>a.stock.Count("waste_wood")>0);m.State.player.x=g.x;m.State.player.y=g.y;m.SetPaused(false);long before=g.stock.stacks.Where(s=>s.item=="waste_wood").Sum(s=>s.ticks);
            m.Pick(g.id,"waste_wood",PlanStock.Scale);m.CommandMove(new[]{0},9,12);Assert.That(m.State.player.trip,Is.Null);Assert.That(g.stock.Count("waste_wood"),Is.EqualTo(before));Assert.That(m.State.backpack.Count("waste_wood"),Is.Zero);
        }
    }
}
