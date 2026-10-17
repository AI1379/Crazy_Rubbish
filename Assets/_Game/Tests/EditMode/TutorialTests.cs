using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class TutorialTests
    {
        private static PrototypeSimulation Fresh()=>new PrototypeSimulation(new PrototypeSettings());
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        [Test]public void NewGameOffersAnExplicitStartWithoutGrantingResources()
        {
            var m=Fresh();Assert.That(m.Tutorial.IntroOpen,Is.True);m.Tutorial.Refresh(m);Assert.That(m.Tutorial.step,Is.Zero);
            m.Tutorial.Begin();m.Tutorial.Refresh(m);Assert.That(m.Tutorial.step,Is.EqualTo(1));Assert.That(m.Carry.UsedTicks+m.Warehouse.UsedTicks,Is.Zero);Assert.That(m.CashCents,Is.Zero);
        }
        [Test]public void PickupReservationCannotSkipTheLessonBeforeItCompletes()
        {
            var m=Fresh();m.Tutorial.Begin();m.Tutorial.Refresh(m);m.PickUp(1,ItemId.WoodHandSaw);m.Advance(0.25);
            Assert.That(m.Tutorial.step,Is.EqualTo(1));m.CancelInteraction();m.Advance(1);Assert.That(m.Tutorial.step,Is.EqualTo(1));
            m.PickUp(1,ItemId.WoodHandSaw);m.Advance(0.55);Assert.That(m.Tutorial.step,Is.EqualTo(2));
        }
        [Test]public void AFullBackpackRedirectsSupplyToTheWarehouse()
        {
            var m=Fresh();m.Tutorial.Begin();m.Tutorial.step=2;m.Carry.TryAdd(ItemId.WasteWood,8);
            var action=PrototypeTutorial.NextAction(m,2);Assert.That(action.operation,Is.EqualTo(TutorialOperation.Deposit));Assert.That(action.x,Is.EqualTo(PrototypeSimulation.WarehouseX));
            Assert.That(m.Carry.Count(ItemId.WasteWood),Is.EqualTo(8));
        }
        [Test]public void CompletedPlanksStillNeedActualTransportToFinishTheLesson()
        {
            var m=Fresh();var s=m.Capture();s.woodBatches=2;m=PrototypeSimulation.Restore(s);m.Tutorial.Begin();m.Tutorial.step=5;
            m.Output.TryAdd(ItemId.Plank,6);m.Tutorial.Refresh(m);Assert.That(m.Tutorial.step,Is.EqualTo(5));
            Assert.That(PrototypeTutorial.NextAction(m,5).operation,Is.EqualTo(TutorialOperation.TakeWood));
            m.Output.TryRemove(ItemId.Plank,6);m.Warehouse.TryAdd(ItemId.Plank,6);m.Tutorial.Refresh(m);Assert.That(m.Tutorial.step,Is.EqualTo(6));
        }
        [Test]public void SkippingAndContinuingPreservesBothTheCourseAndSimulation()
        {
            var m=Fresh();m.Tutorial.Begin();m.Tutorial.step=3;m.Tutorial.Skip();long stock=m.Ground.Sum(p=>(long)p.Amount);var copy=Copy(m);
            Assert.That(copy.Tutorial.active,Is.False);Assert.That(copy.Tutorial.step,Is.EqualTo(3));copy.Tutorial.Begin();Assert.That(copy.Tutorial.step,Is.EqualTo(3));
            Assert.That(copy.Ground.Sum(p=>(long)p.Amount),Is.EqualTo(stock));Assert.That(copy.CashCents,Is.Zero);
        }
        [Test]public void OlderDemoSavesDoNotForceAnIntroOrResetProgress()
        {
            var m=Fresh();var s=m.Capture();s.tutorial=null;s.cash=1234;var copy=PrototypeSimulation.Restore(s);
            Assert.That(copy.Tutorial.IntroOpen,Is.False);Assert.That(copy.Tutorial.active,Is.False);Assert.That(copy.CashCents,Is.EqualTo(1234));
        }
        [Test]public void AWorkingAssemblyOffersWaitInsteadOfTogglingOffTheOperator()
        {
            var m=Fresh();var s=m.Capture();s.table=true;s.playerX=PrototypeSimulation.TableX;s.playerY=PrototypeSimulation.TableY;m=PrototypeSimulation.Restore(s);
            m.TableInput.TryAdd(ItemId.WoodHandle,1);m.TableInput.TryAdd(ItemId.SawBlade,1);m.ToggleAssembly();m.Advance(1);
            Assert.That(PrototypeTutorial.NextAction(m,11).operation,Is.EqualTo(TutorialOperation.Wait));
            m.LeaveManualRole();Assert.That(PrototypeTutorial.NextAction(m,11).operation,Is.EqualTo(TutorialOperation.WorkAssembly));
        }
        [Test]public void FundingLessonNeverAutomaticallySellsTechnology()
        {
            var m=Fresh();PrototypeTutorial.NextAction(m,12);
            Assert.That(m.CashCents,Is.Zero);Assert.That(m.IsTechnologySold(Technology.Tools),Is.False);
        }
        [Test]public void FollowingOnlySuggestedTutorialActionsReachesTheFullDemo()
        {
            var m=Fresh();PrototypeTutorialScenario.Run(m);
            Assert.That(m.Tutorial.Completed,Is.True);Assert.That(m.DemoCompleted,Is.True);Assert.That(m.TotalWagesPaidCents,Is.GreaterThanOrEqualTo(5000));Assert.That(m.AssembledTools,Is.GreaterThanOrEqualTo(1));
            Assert.That(Copy(m).Tutorial.Completed,Is.True);
            Debug.Log($"TUTORIAL_ACCEPTANCE: {m.GameSeconds/60:0.0} minutes, {m.Money:0.00} money, {m.CompletedBatches} batches, {m.DeliveredTrips} deliveries.");
        }
        [Test]public void SupplyChecklistCountsWarehouseStockInsteadOfUnfinishedHauling()
        {
            var m=Fresh();m.Tutorial.Begin();m.Tutorial.step=2;
            m.Carry.TryAdd(ItemId.Food,4);m.Warehouse.TryAdd(ItemId.DrinkingWater,6);
            string checklist=PrototypeTutorial.ProgressSummary(m,2);
            Assert.That(checklist,Does.Contain("0/4"));Assert.That(checklist,Does.Contain("6/8"));
            Assert.That(m.Tutorial.step,Is.EqualTo(2));
            m.Warehouse.TryAdd(ItemId.Food,12);
            Assert.That(PrototypeTutorial.ProgressSummary(m,2),Does.Contain("4/4"));
        }
        [Test]public void ProductionChecklistSeparatesFinishedBatchesFromStockActuallyDeposited()
        {
            var snapshot=Fresh().Capture();snapshot.woodBatches=2;
            var m=PrototypeSimulation.Restore(snapshot);m.Output.TryAdd(ItemId.Plank,6);
            Assert.That(PrototypeTutorial.ProgressSummary(m,5),Does.Contain("2/2"));
            Assert.That(PrototypeTutorial.ProgressSummary(m,5),Does.Contain("0/6"));
            Assert.That(PrototypeTutorial.Satisfied(m,5),Is.False);
        }
        [Test]public void ToolLessonClearsExcessOldInputBeforeWaitingForTheHandleRecipe()
        {
            var snapshot=Fresh().Capture();snapshot.bench=true;snapshot.playerX=PrototypeSimulation.BenchX;snapshot.playerY=PrototypeSimulation.BenchY;
            var m=PrototypeSimulation.Restore(snapshot);m.WoodInputStock.TryAdd(ItemId.WasteWood,8);m.SelectRecipe(WoodRecipe.WoodHandle);
            m.Tutorial.Begin();m.Tutorial.step=11;
            var action=PrototypeTutorial.NextAction(m,11);Assert.That(action.operation,Is.EqualTo(TutorialOperation.TakeWoodInput));
            PrototypeTutorialActions.Execute(m,action);m.Advance(0.55);
            Assert.That(m.RecipeChangePending,Is.False);Assert.That(m.SelectedRecipe,Is.EqualTo(WoodRecipe.WoodHandle));
            Assert.That(PrototypeTutorial.NextAction(m,11).operation,Is.EqualTo(TutorialOperation.Deposit));
        }
    }
}
