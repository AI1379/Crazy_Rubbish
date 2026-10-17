using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class ExperienceTests
    {
        [Test] public void SalePreviewMatchesDynamicBatchRevenueWithoutMutatingStockOrPrice()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            m.Warehouse.TryAdd(ItemId.Plank,12);m.Sell(ItemId.Plank,1);
            decimal before=m.Money;double price=m.SalePrice(ItemId.Plank);
            var quote=m.QuoteSale(ItemId.Plank,5);
            Assert.That(m.Warehouse.Count(ItemId.Plank),Is.EqualTo(11));
            Assert.That(m.Money,Is.EqualTo(before));Assert.That(m.SalePrice(ItemId.Plank),Is.EqualTo(price));
            Assert.That(m.Sell(ItemId.Plank,5).Success,Is.True);
            Assert.That(m.Money-before,Is.EqualTo(quote.Income));Assert.That(quote.Amount,Is.EqualTo(5));
        }
        [Test] public void SalePreviewHonoursReservationsAndProtectedDrinkingWater()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            m.Warehouse.TryAdd(ItemId.DrinkingWater,12);m.Warehouse.Reserve(new ItemAmount(ItemId.DrinkingWater,3));
            var quote=m.QuoteSale(ItemId.DrinkingWater,int.MaxValue);decimal before=m.Money;
            Assert.That(quote.Amount,Is.EqualTo(7));m.Sell(ItemId.DrinkingWater,int.MaxValue);
            Assert.That(m.Money-before,Is.EqualTo(quote.Income));Assert.That(m.Warehouse.Available(ItemId.DrinkingWater),Is.EqualTo(m.DrinkingReserve));
        }
        [Test] public void ConstructionMaterialListExcludesReservedStock()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            m.Warehouse.TryAdd(ItemId.WasteWood,4);m.Warehouse.TryAdd(ItemId.RecoveredFastener,2);
            Assert.That(m.HasBuildMaterials(1),Is.True);m.Warehouse.Reserve(new ItemAmount(ItemId.WasteWood,1));
            Assert.That(m.HasBuildMaterials(1),Is.False);Assert.That(m.BuildMaterialSummary(1),Does.Contain("3/4 · 缺1"));
        }
        [Test] public void SmallerOpeningReserveCanCompleteTheFullTutorialWithoutTechnologySales()
        {
            var m=new PrototypeSimulation(new PrototypeSettings(),4271,true);
            PrototypeTutorialScenario.Run(m,false);
            Assert.That(m.DemoCompleted,Is.True);Assert.That(m.Failed,Is.False);
            Assert.That(System.Enum.GetValues(typeof(Technology)).Cast<Technology>().Any(m.IsTechnologySold),Is.False);
            Assert.That(m.UnpaidWageCents,Is.Zero);Assert.That(m.MissingSupplyPeople,Is.Zero);
            Debug.Log($"NO_TECHNOLOGY_ACCEPTANCE: {m.GameSeconds/60:0.0} minutes, {m.Money:0.00} money, {m.CompletedBatches} batches.");
        }
        [Test] public void AutomationGuidePreferenceSurvivesSaveAndRemainsOptional()
        {
            var m=new PrototypeSimulation(new PrototypeSettings());
            Assert.That(m.Tutorial.automationStarted,Is.False);
            m.Tutorial.automationStarted=true;m.Tutorial.automationDismissed=true;
            var copy=PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
            Assert.That(copy.Tutorial.automationStarted,Is.True);Assert.That(copy.Tutorial.automationDismissed,Is.True);
        }
        [Test] public void PowerLessonStillRequestsAConnectionWhenOnlyUnrelatedWireExists()
        {
            var original=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            var snapshot=original.Capture();snapshot.generator=true;
            snapshot.discovered=new[]{(int)ItemId.RecoveredWire};
            var m=PrototypeSimulation.Restore(snapshot);m.Warehouse.TryAdd(ItemId.RecoveredWire,2);
            Assert.That(m.PlaceWire(new[]{new WireCell{X=10,Y=6}}).Success,Is.True);
            Assert.That(m.Wires,Is.Not.Empty);
            Assert.That(PrototypeTutorial.NextAction(m,8).operation,Is.EqualTo(TutorialOperation.ConnectPower));
        }
    }
}
