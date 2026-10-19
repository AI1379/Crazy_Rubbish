using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class CommunityDemoTests
    {
        private static PrototypeSimulation Fresh()=>new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
        private static PrototypeSimulation Copy(PrototypeSimulation m)=>PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
        private static PrototypeSimulation Community(int population=1)
        {
            var m=Fresh();var save=m.Capture();save.powered=true;m=PrototypeSimulation.Restore(save);
            for(int i=0;i<population;i++)Assert.That(m.Recruit().Success,Is.True);
            m.SetLivingDelivery(false);return m;
        }
        [Test]public void MealsConsumeRealStockAndFirstPeriodIsFree()
        {
            var m=Community(2);m.Warehouse.TryAdd(ItemId.Food,10);m.Warehouse.TryAdd(ItemId.DrinkingWater,10);
            m.Advance(300);
            Assert.That(m.Warehouse.Count(ItemId.Food),Is.EqualTo(7));Assert.That(m.Warehouse.Count(ItemId.DrinkingWater),Is.EqualTo(7));
            Assert.That(m.MealsConsumed,Is.EqualTo(3));Assert.That(m.Residents.Sum(r=>r.CashCents),Is.EqualTo(1000));Assert.That(m.CashCents,Is.Zero);
        }
        [Test]public void RetailUsesOneSharedStockAndTransfersRealMoney()
        {
            var m=Community();m.Warehouse.TryAdd(ItemId.Food,10);m.Warehouse.TryAdd(ItemId.DrinkingWater,10);m.Advance(600);
            Assert.That(m.RetailIncomeCents,Is.EqualTo(200));Assert.That(m.CashCents,Is.EqualTo(200));Assert.That(m.Residents[0].CashCents,Is.EqualTo(300));
            Assert.That(m.CommunityCashCents,Is.Zero);Assert.That(m.Warehouse.Count(ItemId.Food),Is.EqualTo(6));
        }
        [Test]public void ResidentExternalPurchasesDoNotDeductPlayerCashOrCreateStock()
        {
            var m=Community();m.PlayerNeeds.nextMeal=double.MaxValue;m.Advance(300);
            Assert.That(m.ExternalLivingSpentCents,Is.EqualTo(200));Assert.That(m.CashCents,Is.Zero);Assert.That(m.Residents[0].CashCents,Is.EqualTo(300));Assert.That(m.Warehouse.UsedTicks,Is.Zero);
        }
        [Test]public void DisabledExternalSupplyPreservesWalletAndShowsShortage()
        {
            var m=Community();m.SetExternalSupply(false);m.Advance(300);
            Assert.That(m.Residents[0].CashCents,Is.EqualTo(500));Assert.That(m.MissingSupplyPeople,Is.EqualTo(2));Assert.That(m.Residents[0].Needs.waterReason,Does.Contain("外购已关闭"));
        }
        [Test]public void FreePolicyConsumesPhysicalGoodsAndDoesNotPayWages()
        {
            var m=Community();m.SetSupplyPolicy(ItemId.Food,SupplyPolicy.Free);m.SetSupplyPolicy(ItemId.DrinkingWater,SupplyPolicy.Free);
            m.Warehouse.TryAdd(ItemId.Food,10);m.Warehouse.TryAdd(ItemId.DrinkingWater,10);m.Advance(600);
            Assert.That(m.RetailIncomeCents,Is.Zero);Assert.That(m.UnpaidWages,Is.EqualTo(10));Assert.That(m.Residents[0].CashCents+m.CommunityCashCents,Is.EqualTo(500));
        }
        [Test]public void SubsidyReducesRetailPriceWithoutDuplicatingStock()
        {
            var m=Community();m.SetSupplyPolicy(ItemId.Food,SupplyPolicy.Subsidized);m.SetSupplyPolicy(ItemId.DrinkingWater,SupplyPolicy.Subsidized);
            m.Warehouse.TryAdd(ItemId.Food,10);m.Warehouse.TryAdd(ItemId.DrinkingWater,10);m.Advance(600);
            Assert.That(m.RetailIncomeCents,Is.EqualTo(100));Assert.That(m.Residents[0].CashCents+m.CommunityCashCents+m.CashCents,Is.EqualTo(500));
        }
        [Test]public void WaterExternalSaleCannotSpendThePopulationReserve()
        {
            var m=Community(5);m.Warehouse.TryAdd(ItemId.DrinkingWater,20);Assert.That(m.Sell(ItemId.DrinkingWater,int.MaxValue).Amount,Is.EqualTo(8));
            Assert.That(m.Warehouse.Count(ItemId.DrinkingWater),Is.EqualTo(12));Assert.That(m.Sell(ItemId.DrinkingWater,1).Success,Is.False);
        }
        [Test]public void MissingNeedsResumeIndependentlyAndDoNotDisappearOnLoad()
        {
            var m=Fresh();m.SetExternalSupply(false);m.Advance(600);var copy=Copy(m);
            Assert.That(copy.PlayerNeeds.foodDebt,Is.EqualTo(2));Assert.That(copy.PlayerNeeds.waterDebt,Is.EqualTo(2));
            copy.Warehouse.TryAdd(ItemId.DrinkingWater,1);copy.Advance(10);
            Assert.That(copy.PlayerNeeds.waterDebt,Is.EqualTo(1));Assert.That(copy.PlayerNeeds.waterSince,Is.EqualTo(300));Assert.That(copy.PlayerNeeds.foodDebt,Is.EqualTo(2));
            copy.Warehouse.TryAdd(ItemId.DrinkingWater,1);copy.Advance(10);Assert.That(copy.PlayerNeeds.waterSince,Is.EqualTo(-1));
        }
        [Test]public void PauseFreezesNutritionAndDeathAndFailureCanBeRestored()
        {
            var m=Fresh();m.SetExternalSupply(false);m.Advance(300);m.SetPaused(true);m.Advance(5000);Assert.That(m.GameSeconds,Is.EqualTo(300).Within(1e-6));
            m.SetPaused(false);m.Advance(1201);Assert.That(m.Failed,Is.True);var copy=Copy(m);Assert.That(copy.Failed,Is.True);Assert.That(copy.FailureReason,Does.Contain("缺水"));
        }
        [Test]public void EmergencyWaterUsesOnlyStockAndItsQuotaSurvivesLoad()
        {
            var m=Community();var s=m.Capture();s.community.firstPayroll=0;s.residents[0].cash=0;s.community.player.nextMeal=double.MaxValue;s.residents[0].needs.nextMeal=1;m=PrototypeSimulation.Restore(s);
            m.Warehouse.TryAdd(ItemId.DrinkingWater,10);m.Advance(1);
            Assert.That(m.Residents[0].Needs.emergencyWater,Is.EqualTo(1));m.Residents[0].Needs.waterDebt=2;m.Residents[0].Needs.waterSince=m.GameSeconds;m.SetEmergencyWater(true);m.Advance(10);
            Assert.That(m.Warehouse.Count(ItemId.DrinkingWater),Is.EqualTo(8));Assert.That(m.Residents[0].Needs.waterDebt,Is.EqualTo(1));Assert.That(m.CashCents,Is.Zero);
            var copy=Copy(m);copy.Advance(10);Assert.That(copy.Warehouse.Count(ItemId.DrinkingWater),Is.EqualTo(8));Assert.That(copy.Residents[0].Needs.emergencyWater,Is.EqualTo(2));
        }
        [Test]public void NutritionalRecoveryTakes180SecondsAndStopsDuringAnotherShortage()
        {
            var m=Fresh();m.SetExternalSupply(false);m.PlayerNeeds.nextMeal=1;m.Advance(181);m.Warehouse.TryAdd(ItemId.DrinkingWater,1);m.Advance(10);
            Assert.That(m.PlayerNeeds.WaterMultiplier(m.GameSeconds),Is.EqualTo(0.85).Within(0.002));m.Advance(90);
            Assert.That(m.PlayerNeeds.WaterMultiplier(m.GameSeconds),Is.EqualTo(0.925).Within(0.002));
            m.PlayerNeeds.waterSince=m.GameSeconds;m.PlayerNeeds.waterDebt=1;double recovery=m.PlayerNeeds.waterRecovery;m.Advance(10);
            Assert.That(m.PlayerNeeds.waterRecovery,Is.EqualTo(recovery).Within(1e-8));Assert.That(m.PlayerNeeds.WaterMultiplier(m.GameSeconds),Is.LessThan(1));
        }
        [Test]public void RemovingACompletedWorkshopKeepsTheUniqueCraftedTool()
        {
            var m=Fresh();var s=m.Capture();s.table=true;s.playerX=PrototypeSimulation.TableX;s.playerY=PrototypeSimulation.TableY;m=PrototypeSimulation.Restore(s);
            m.TableInput.TryAdd(ItemId.WoodHandle,1);m.TableInput.TryAdd(ItemId.SawBlade,1);m.ToggleAssembly();m.Advance(21);m.RemoveFacility(4,true);
            Assert.That(m.AssembledTools,Is.EqualTo(1));Assert.That(m.Warehouse.Count(ItemId.WoodHandSaw),Is.EqualTo(1));Assert.That(m.Tools.Count(t=>t.Item==ItemId.WoodHandSaw&&t.Location=="warehouse"),Is.EqualTo(1));Assert.That(m.TableOutput.UsedTicks,Is.Zero);
        }
        [Test]public void CompletedBlockedAssemblyCannotRefundAndRemovalCommitsOnlyOnce()
        {
            var m=Fresh();var s=m.Capture();s.table=true;s.playerX=PrototypeSimulation.TableX;s.playerY=PrototypeSimulation.TableY;m=PrototypeSimulation.Restore(s);
            m.TableInput.TryAdd(ItemId.WoodHandle,1);m.TableInput.TryAdd(ItemId.SawBlade,1);m.ToggleAssembly();m.Advance(1);m.TableOutput.TryAdd(ItemId.Plank,4);m.Advance(25);
            Assert.That(m.CancelAssembly().Success,Is.False);Assert.That(m.TableInput.Count(ItemId.WoodHandle),Is.EqualTo(1));Assert.That(m.AssembledTools,Is.Zero);
            m=Copy(m);Assert.That(m.RemoveFacility(4,true).Success,Is.True);Assert.That(m.TableInput.UsedTicks,Is.Zero);Assert.That(m.Warehouse.Count(ItemId.WoodHandSaw),Is.EqualTo(1));Assert.That(m.AssembledTools,Is.EqualTo(1));Assert.That(m.RemoveFacility(4,true).Success,Is.False);
        }
        [Test]public void DeadResidentsKeepTheirOriginalWageReceivableInCommunity()
        {
            var m=Community();m.SetExternalSupply(false);m.PlayerNeeds.nextMeal=double.MaxValue;m.Advance(1501);
            Assert.That(m.Residents.Count,Is.Zero);Assert.That(m.UnpaidWages,Is.EqualTo(20));
            m.SellTechnology(Technology.Wood);Assert.That(m.PayWages().Success,Is.True);Assert.That(m.CommunityCashCents,Is.EqualTo(2500));Assert.That(m.UnpaidWages,Is.Zero);
        }
        [Test]public void WageRiotUsesFixedPaymentAndDoesNotRestartTheCountdown()
        {
            var m=Community(2);m.PlayerNeeds.nextMeal=double.MaxValue;foreach(var r in m.Residents)r.Needs.nextMeal=double.MaxValue;
            m.Advance(1800);Assert.That(m.RiotActive,Is.True);Assert.That(m.RiotTargetCents,Is.EqualTo(2000));double deadline=m.RiotDeadline;
            var copy=Copy(m);copy.Warehouse.TryAdd(ItemId.Plank,1);copy.Sell(ItemId.Plank,1);copy.PayWages();
            Assert.That(copy.RiotTargetCents,Is.EqualTo(2000));Assert.That(copy.RiotPaidCents,Is.EqualTo(300));Assert.That(copy.RiotDeadline,Is.EqualTo(deadline).Within(1e-8));
            copy.Advance(241);Assert.That(copy.Failed,Is.True);
        }
        [Test]public void PayingTheRiotTargetKeepsRemainingDebtAndStopsTheRiot()
        {
            var m=Community(2);m.PlayerNeeds.nextMeal=double.MaxValue;foreach(var r in m.Residents)r.Needs.nextMeal=double.MaxValue;m.Advance(1800);
            m.Warehouse.TryAdd(ItemId.Plank,8);m.Sell(ItemId.Plank,8);m.PayWages();Assert.That(m.RiotActive,Is.False);Assert.That(m.UnpaidWageCents,Is.GreaterThan(0));
            m.Advance(1);Assert.That(m.RiotActive,Is.False);
        }
        [Test]public void WorkshopReservationsPauseCancelAndRestoreWithoutDoubleOutput()
        {
            var m=Fresh();var s=m.Capture();s.table=true;s.playerX=PrototypeSimulation.TableX;s.playerY=PrototypeSimulation.TableY;m=PrototypeSimulation.Restore(s);
            m.TableInput.TryAdd(ItemId.WoodHandle,1);m.TableInput.TryAdd(ItemId.SawBlade,1);m.ToggleAssembly();m.Advance(5);
            Assert.That(m.TableInput.Count(ItemId.WoodHandle),Is.EqualTo(1));Assert.That(m.TableInput.Available(ItemId.WoodHandle),Is.Zero);
            var copy=Copy(m);copy.SetPaused(true);copy.Advance(60);Assert.That(copy.AssemblyProgress,Is.EqualTo(m.AssemblyProgress));copy.SetPaused(false);copy.Advance(30);
            Assert.That(copy.AssembledTools,Is.EqualTo(1));Assert.That(copy.TableInput.UsedTicks,Is.Zero);Assert.That(copy.Tools.Count(t=>t.Location=="tableOutput"&&t.Item==ItemId.WoodHandSaw),Is.EqualTo(1));
            m.CancelAssembly();Assert.That(m.TableInput.Available(ItemId.WoodHandle),Is.EqualTo(1));Assert.That(m.AssembledTools,Is.Zero);
        }
        [Test]public void RemovingAWorkshopReturnsReservedInputsAndKeepsItsTools()
        {
            var m=Fresh();var s=m.Capture();s.table=true;s.playerX=PrototypeSimulation.TableX;s.playerY=PrototypeSimulation.TableY;m=PrototypeSimulation.Restore(s);
            m.TableInput.TryAdd(ItemId.WoodHandle,1);m.TableInput.TryAdd(ItemId.SawBlade,1);m.ToggleAssembly();m.Advance(1);m.RemoveFacility(4,true);
            Assert.That(m.Warehouse.Count(ItemId.WoodHandle),Is.EqualTo(1));Assert.That(m.Warehouse.Count(ItemId.SawBlade),Is.EqualTo(1));Assert.That(m.AssembledTools,Is.Zero);Assert.That(m.AssemblyActive,Is.False);
        }
        [Test]public void AutomaticToolsCannotSpendNextPayrollAndTheirBudgetSurvivesLoad()
        {
            var m=Community(5);var s=m.Capture();s.bench=true;s.runWood=true;s.cash=6000;m=PrototypeSimulation.Restore(s);m.SetAutomaticToolPurchase(true);m.Advance(1);
            Assert.That(m.CashCents,Is.EqualTo(6000));Assert.That(m.ToolSupplyNotice,Does.Contain("工资"));
            m.Warehouse.TryAdd(ItemId.Plank,10);m.Sell(ItemId.Plank,10);m.Advance(10);
            Assert.That(m.Tools.Count(t=>t.Item==ItemId.WoodHandSaw&&t.Location=="warehouse"),Is.EqualTo(1));
            var copy=Copy(m);copy.Warehouse.TryRemove(ItemId.WoodHandSaw,1);var snapshot=copy.Capture();snapshot.tools.RemoveAll(t=>t.item==(int)ItemId.WoodHandSaw&&t.location=="warehouse");copy=PrototypeSimulation.Restore(snapshot);copy.Advance(10);
            Assert.That(copy.Warehouse.Count(ItemId.WoodHandSaw),Is.Zero);
        }
        [Test]public void SchemaTwoMigratesWithoutRetroactiveNeedsOrLosingMoney()
        {
            var m=Fresh();m.SellTechnology(Technology.Wood);var s=m.Capture();s.schema=2;s.community=null;s.workshop=null;s.seconds=3000;
            var copy=PrototypeSimulation.Restore(s);Assert.That(copy.CashCents,Is.EqualTo(6000));Assert.That(copy.PlayerNeeds.nextMeal,Is.EqualTo(3300));Assert.That(copy.Failed,Is.False);
            Assert.That(copy.Capture().schema,Is.EqualTo(SimulationSnapshot.CurrentSchema));
        }
        [Test]public void DefaultNewGameReachesEveryDemoGoalThroughNormalActions()
        {
            var m=new PrototypeSimulation(new PrototypeSettings());PrototypeDemoScenario.Run(m);
            Assert.That(m.DemoCompleted,Is.True);Assert.That(m.DemoMilestones.All(done=>done),Is.True);Assert.That(m.Failed,Is.False);Assert.That(m.TotalWagesPaidCents,Is.GreaterThanOrEqualTo(5000));
            var copy=Copy(m);Assert.That(copy.DemoCompleted,Is.True);Assert.That(copy.DemoCompletedAt,Is.EqualTo(m.DemoCompletedAt));
            Debug.Log($"DEMO_ACCEPTANCE: {m.GameSeconds/60:0.0} minutes, {m.Money:0.00} money, {m.CompletedBatches} batches, {m.TotalWagesPaidCents/100m:0.00} wages, {m.MealsConsumed} meals, {m.DeliveredTrips} deliveries.");
        }
    }
}
