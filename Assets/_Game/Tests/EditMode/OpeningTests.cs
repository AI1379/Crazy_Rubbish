using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class OpeningTests
    {
        private static PrototypeSimulation Fresh()=>new PrototypeSimulation(new PrototypeSettings{automaticInput=false},4271,true);
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        [Test]public void OpeningStartsEmptyAndFirstArrivalIsOneUsableSaw()
        {
            var m=Fresh();Assert.That(m.Ground,Is.Empty);m.Tutorial.Skip();m.Advance(2.05);
            Assert.That(m.Ground.Count,Is.EqualTo(1));Assert.That(m.Ground[0].Item,Is.EqualTo(ItemId.WoodHandSaw));Assert.That(m.Ground[0].Usable,Is.True);
            Assert.That(m.CashCents,Is.Zero);Assert.That(m.Carry.UsedTicks+m.Warehouse.UsedTicks,Is.Zero);
        }
        [Test]public void EarlyDeliveriesDoNotRevealAdvancedEquipmentOrDependOnTutorialProgress()
        {
            var m=Fresh();m.Tutorial.Skip();m.Advance(120);
            Assert.That(m.Ground.Select(p=>p.Item).Distinct().Count(),Is.EqualTo(5));
            Assert.That(m.Ground.Sum(p=>p.Item==ItemId.Food?p.Amount:0),Is.EqualTo(30));
            Assert.That(m.Ground.Any(p=>ItemCatalog.IsDevice(p.Item)),Is.False);
            Assert.That(m.AwaitingOpeningItem(ItemId.SteelPart),Is.True);
        }
        [Test]public void PausingAndSavingCannotDuplicateOrSkipAnArrival()
        {
            var m=Fresh();m.Advance(2);m.SetPaused(true);m.Advance(60);Assert.That(m.Opening.batch,Is.EqualTo(1));
            var copy=Copy(m);copy.SetPaused(false);m.SetPaused(false);m.Advance(12);copy.Advance(12);
            Assert.That(copy.Opening.batch,Is.EqualTo(m.Opening.batch));
            Assert.That(copy.Ground.Select(p=>(p.Item,p.Amount)),Is.EqualTo(m.Ground.Select(p=>(p.Item,p.Amount))));
        }
        [Test]public void AllGuaranteedShipmentsPreserveTheOriginalResourceBudget()
        {
            var m=Fresh();var s=m.Capture();s.bench=true;s.woodBatches=1;s.powered=true;
            m=PrototypeSimulation.Restore(s);m.Advance(1100);
            var original=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            foreach(ItemId item in Enum.GetValues(typeof(ItemId)))
                Assert.That(m.Ground.Where(p=>p.Item==item).Sum(p=>p.Amount),Is.EqualTo(original.Ground.Where(p=>p.Item==item).Sum(p=>p.Amount)),item.ToString());
            Assert.That(m.AwaitingOpeningItem(ItemId.OldControlBox),Is.False);
        }
        [Test]public void OlderSavesDoNotReceiveAnotherSetOfStartingResources()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});var copy=Copy(m);int stock=copy.Ground.Sum(p=>p.Amount);
            copy.Advance(120);Assert.That(copy.Opening,Is.Null);Assert.That(copy.Ground.Sum(p=>p.Amount),Is.EqualTo(stock));
        }
        [Test]public void RealProgressiveOpeningCanFinishTheTutorialWithoutInjectedStock()
        {
            var m=new PrototypeSimulation(new PrototypeSettings(),4271,true);PrototypeTutorialScenario.Run(m);
            Assert.That(m.DemoCompleted,Is.True);Assert.That(m.Tutorial.Completed,Is.True);
            Assert.That(m.DumpedTicks,Is.Zero,"The tutorial must never require dumping.");
        }
        [Test]public void DumpingRequiresTheHoleAndCompletesOnlyAfterTheTimedInteraction()
        {
            var m=Fresh();m.Carry.TryAdd(ItemId.WasteWood,2);
            Assert.That(m.DumpIntoHole(ItemId.WasteWood,1).Success,Is.False);
            PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.DumpX,PrototypeSimulation.DumpY);
            Assert.That(m.DumpIntoHole(ItemId.WasteWood,1).Success,Is.True);m.CancelInteraction();m.Advance(1);
            Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(2));Assert.That(m.DumpedTicks,Is.Zero);
            m.DumpIntoHole(ItemId.WasteWood,1);m.Advance(0.55);
            Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(1));Assert.That(Copy(m).DumpedTicks,Is.EqualTo(ItemCatalog.CapacityTicks(ItemId.WasteWood)));
        }
        [Test]public void SeenConceptsAndOrientationAreIndependentCopiesInSaves()
        {
            var m=Fresh();m.Tutorial.orientation=2;m.Tutorial.seenConcepts.Add("saw");var copy=Copy(m);
            Assert.That(copy.Tutorial.orientation,Is.EqualTo(2));copy.Tutorial.seenConcepts.Add("materials");
            Assert.That(m.Tutorial.seenConcepts,Does.Not.Contain("materials"));
        }
        [Test]public void IdleResidentsStayWithoutDeliveryAfterJsonRoundtrip()
        {
            var snapshot=Fresh().Capture();snapshot.powered=true;
            var m=PrototypeSimulation.Restore(snapshot);Assert.That(m.Recruit().Success,Is.True);
            Assert.That(m.Capture().residents[0].delivery,Is.Null);
            var copy=Copy(m);Assert.That(copy.Capture().residents[0].delivery,Is.Null);
            Assert.DoesNotThrow(()=>copy.Advance(3));
        }
    }
}
