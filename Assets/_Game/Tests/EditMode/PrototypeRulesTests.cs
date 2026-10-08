using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PrototypeRulesTests
    {
        private static PrototypeSettings Settings() => new PrototypeSettings { automaticInput = false };
        private static void MoveTo(PrototypeSimulation model, double x, double y)
        {
            double dx = x - model.PlayerX, dy = y - model.PlayerY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length > 0) model.MovePlayer(dx / length, dy / length, length / (model.Settings.moveSpeed * model.Speed));
        }
        private static PrototypeSimulation Ready(PrototypeSettings settings = null)
        {
            var model = new PrototypeSimulation(settings ?? Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, model.Settings.benchWoodCost + model.Settings.recipeWoodInput * 2);
            model.Warehouse.TryAdd(ItemId.RecoveredFastener, model.Settings.benchFastenerCost);
            model.Carry.TryAdd(ItemId.WoodHandSaw, 1);
            Assert.That(model.BuildBench().Success, Is.True);
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            return model;
        }
        private static PrototypeSimulation Powered()
        {
            var model = Ready();
            model.Warehouse.TryAdd(ItemId.Plank, 4);
            model.Warehouse.TryAdd(ItemId.SteelPart, 8);
            model.Warehouse.TryAdd(ItemId.ThermalModule, 1);
            model.Warehouse.TryAdd(ItemId.GeneratorModule, 1);
            model.Warehouse.TryAdd(ItemId.RecoveredWire, 2);
            Assert.That(model.BuildTable().Success, Is.True);
            Assert.That(model.BuildGenerator().Success, Is.True);
            model.Carry.TryAdd(ItemId.WasteWood, 2);
            MoveTo(model, PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY);
            Assert.That(model.AddFuel(ItemId.WasteWood, 2).Success, Is.True);
            return model;
        }

        [Test]
        public void WeightedCapacityRejectsOverflowWithoutMutation()
        {
            var inventory = new UnitInventory(3);
            Assert.That(inventory.TryAdd(ItemId.WasteWood, 2), Is.True);
            Assert.That(inventory.TryAdd(ItemId.Plank, 2), Is.False);
            Assert.That(inventory.TryAdd(ItemId.Plank, -1), Is.False);
            Assert.That(inventory.TryRemove(ItemId.WasteWood, 3), Is.False);
            Assert.That(inventory.Used, Is.EqualTo(2));
            Assert.That(inventory.TryAdd(ItemId.SteelPart, 2), Is.True);
            Assert.That(inventory.Used, Is.EqualTo(3));
        }
        [Test]
        public void WaterAndModulesUseExactCapacity()
        {
            var inventory = new UnitInventory(8);
            Assert.That(inventory.TryAdd(ItemId.ThermalModule, 1), Is.True);
            Assert.That(inventory.TryAdd(ItemId.DrinkingWater, 40), Is.True);
            Assert.That(inventory.UsedTicks, Is.EqualTo(160));
            Assert.That(inventory.TryAdd(ItemId.DrinkingWater, 1), Is.False);
        }
        [Test]
        public void MultiProductOutputIsAtomic()
        {
            var inventory = new UnitInventory(3);
            Assert.That(inventory.TryAddBatch(new ItemAmount(ItemId.Plank, 3), new ItemAmount(ItemId.WoodChips, 1)), Is.False);
            Assert.That(inventory.Used, Is.Zero);
        }
        [Test]
        public void BatchConsumptionAggregatesDuplicateCostsAtomically()
        {
            var inventory = new UnitInventory(10);
            inventory.TryAdd(ItemId.WasteWood, 4); inventory.TryAdd(ItemId.RecoveredFastener, 1);
            Assert.That(inventory.TryConsume(new ItemAmount(ItemId.WasteWood, 3), new ItemAmount(ItemId.WasteWood, 2),
                new ItemAmount(ItemId.RecoveredFastener, 1)), Is.False);
            Assert.That(inventory.Count(ItemId.WasteWood), Is.EqualTo(4));
            Assert.That(inventory.TryConsume(new ItemAmount(ItemId.WasteWood, 2), new ItemAmount(ItemId.WasteWood, 2),
                new ItemAmount(ItemId.RecoveredFastener, 1)), Is.True);
            Assert.That(inventory.Used, Is.Zero);
        }
        [Test]
        public void PartialTransferPreservesRemainderAndUsesTargetItemWeight()
        {
            var source = new UnitInventory(10); var target = new UnitInventory(2);
            source.TryAdd(ItemId.SteelPart, 5);
            Assert.That(source.TransferTo(target, ItemId.SteelPart, 5), Is.EqualTo(4));
            Assert.That(source.Count(ItemId.SteelPart), Is.EqualTo(1));
            Assert.That(target.Count(ItemId.SteelPart), Is.EqualTo(4));
            Assert.That(source.TransferTo(target, ItemId.SteelPart, 1), Is.Zero);
        }
        [Test]
        public void FullBackpackDoesNotRemoveGroundItems()
        {
            var model = new PrototypeSimulation(Settings());
            model.Carry.TryAdd(ItemId.MixedPlastic, model.Carry.Capacity);
            int before = model.Ground.Sum(p => p.Amount);
            Assert.That(model.PickUp().Success, Is.False);
            Assert.That(model.Ground.Sum(p => p.Amount), Is.EqualTo(before));
        }
        [Test]
        public void InitialGuaranteesExistOnGroundAndWarehouseStartsEmpty()
        {
            var model = new PrototypeSimulation(Settings());
            Assert.That(model.Warehouse.Used, Is.Zero);
            Assert.That(model.Money, Is.Zero);
            Assert.That(model.Ground.Where(p => p.Item == ItemId.WasteWood).Sum(p => p.Amount), Is.EqualTo(80));
            Assert.That(model.Ground.Where(p => p.Item == ItemId.SteelPart).Sum(p => p.Amount), Is.EqualTo(30));
            Assert.That(model.Ground.Where(p => p.Item == ItemId.DrinkingWater).Sum(p => p.Amount), Is.EqualTo(40));
            Assert.That(model.Residents.Count, Is.Zero);
        }
        [Test]
        public void FailedBuildDoesNotConsumePartialMaterials()
        {
            var model = new PrototypeSimulation(Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, 4);
            Assert.That(model.BuildBench().Success, Is.False);
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(4));
        }
        [Test]
        public void PausedBlueprintReservesMaterialsAndCompletesOnResume()
        {
            var model = new PrototypeSimulation(Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, 4); model.Warehouse.TryAdd(ItemId.RecoveredFastener, 2);
            model.SetPaused(true); Assert.That(model.BuildBench().Success, Is.True); model.Advance(100);
            Assert.That(model.BenchBuilt, Is.False); Assert.That(model.BuildPending, Is.True);
            Assert.That(model.Warehouse.Used, Is.Zero);
            model.SetPaused(false); model.Advance(0.05);
            Assert.That(model.BenchBuilt, Is.True);
        }
        [Test]
        public void MissingSawDoesNotReserveWoodAndRestoringItResumes()
        {
            var model = Ready(); model.Carry.TryRemove(ItemId.WoodHandSaw, 1); model.ToggleWork();
            int before = model.Warehouse.Count(ItemId.WasteWood); model.Advance(2);
            Assert.That(model.State, Is.EqualTo(WorkState.NeedsTool));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(before));
            model.Carry.TryAdd(ItemId.WoodHandSaw, 1); model.Advance(0.1);
            Assert.That(model.ReservedWood, Is.EqualTo(4));
        }
        [Test]
        public void NewWoodRecipeProducesBothPlanksAndChips()
        {
            var model = Ready(); model.ToggleWork(); model.Advance(40.05);
            Assert.That(model.CompletedBatches, Is.EqualTo(1));
            Assert.That(model.Warehouse.Count(ItemId.Plank), Is.EqualTo(3));
            Assert.That(model.Warehouse.Count(ItemId.WoodChips), Is.EqualTo(1));
        }
        [Test]
        public void HandleRecipeUsesTwoWoodAndIncludesChips()
        {
            var model = Ready(); model.SelectRecipe(WoodRecipe.WoodHandle); model.ToggleWork(); model.Advance(30.05);
            Assert.That(model.CompletedBatches, Is.EqualTo(1));
            Assert.That(model.Warehouse.Count(ItemId.WoodHandle), Is.EqualTo(2));
            Assert.That(model.Warehouse.Count(ItemId.WoodChips), Is.EqualTo(1));
            Assert.That(model.Output.Capacity, Is.EqualTo(4));
        }
        [Test]
        public void PauseFreezesInputProductionFuelAndPayroll()
        {
            var model = Powered(); model.Recruit(); model.ToggleWork();
            double time = model.GameSeconds, fuel = model.FuelSeconds, wage = model.SecondsToWage;
            model.SetPaused(true); model.SetSpeed(4); model.Advance(200);
            Assert.That(model.GameSeconds, Is.EqualTo(time)); Assert.That(model.FuelSeconds, Is.EqualTo(fuel));
            Assert.That(model.SecondsToWage, Is.EqualTo(wage)); Assert.That(model.UnpaidWages, Is.Zero);
        }
        [Test]
        public void StopAndLeavingPreserveJobProgressAndReservedWood()
        {
            var model = Ready(); model.ToggleWork(); model.Advance(1);
            float progress = model.Progress; int stored = model.Warehouse.Count(ItemId.WasteWood);
            model.ToggleWork(); model.Advance(20);
            Assert.That(model.Progress, Is.EqualTo(progress)); Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(stored));
            model.ToggleWork(); MoveTo(model, 1, 1); model.Advance(2);
            Assert.That(model.State, Is.EqualTo(WorkState.NeedsOperator)); Assert.That(model.Progress, Is.EqualTo(progress));
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY); model.Advance(0.5);
            Assert.That(model.Progress, Is.GreaterThan(progress));
        }
        [Test]
        public void FullOutputDoesNotReserveNextBatchAndRecovers()
        {
            var settings = Settings(); settings.warehouseCapacity = 20; settings.outputCapacity = 4;
            var model = Ready(settings); model.ToggleWork(); model.Advance(0.05);
            model.Warehouse.TryAdd(ItemId.MixedPlastic, (int)model.Warehouse.Free);
            model.Advance(60);
            Assert.That(model.State, Is.EqualTo(WorkState.OutputBlocked));
            Assert.That(model.CompletedBatches, Is.EqualTo(1));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(4));
            MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Assert.That(model.Withdraw(ItemId.MixedPlastic, 4).Success, Is.True); model.Advance(0.05);
            Assert.That(model.Output.Used, Is.Zero);
            Assert.That(model.Warehouse.Count(ItemId.Plank), Is.EqualTo(3));
        }
        [Test]
        public void WarehouseContentsDoNotChangeGroundPressure()
        {
            var model = new PrototypeSimulation(Settings()); float before = model.HumanEfficiency;
            model.Warehouse.TryAdd(ItemId.MixedPlastic, 50);
            Assert.That(model.HumanEfficiency, Is.EqualTo(before));
        }
        [Test]
        public void FullGroundPhysicallyStacksAllIncomingWasteWithoutQueue()
        {
            var settings = Settings(); settings.automaticInput = true; settings.firstInputSeconds = 0.001f;
            var model = new PrototypeSimulation(settings); long initial = model.Ground.Sum(p => (long)p.Amount);
            model.Advance(120);
            Assert.That(model.IncomingUnits, Is.Zero);
            Assert.That(model.Ground.Sum(p => (long)p.Amount), Is.EqualTo(initial + model.SpawnedUnits));
            Assert.That(model.Ground.Any(p => p.Amount * ItemCatalog.CapacityTicks(p.Item) > 100), Is.True);
            Assert.That(model.Ground.All(p => PrototypeSimulation.IsOwnedGround(p.X, p.Y) && !PrototypeSimulation.IsReservedCell(p.X, p.Y)), Is.True);
            Assert.That(model.PressureAt(13, 6), Is.LessThan(1));
        }
        [Test]
        public void FixedClockIsIndependentOfFramePartitioning()
        {
            var first = Ready(); var second = Ready(); first.ToggleWork(); second.ToggleWork();
            first.Advance(81); for (int i = 0; i < 1620; i++) second.Advance(0.05);
            Assert.That(first.CompletedBatches, Is.EqualTo(second.CompletedBatches));
            Assert.That(first.Warehouse.Count(ItemId.Plank), Is.EqualTo(second.Warehouse.Count(ItemId.Plank)));
            Assert.That(first.GameSeconds, Is.EqualTo(second.GameSeconds).Within(0.00001));
        }
        [Test]
        public void AccelerationScalesTheSameSimulationClock()
        {
            var normal = Powered(); var fast = Powered(); normal.Recruit(); fast.Recruit(); fast.SetSpeed(4);
            normal.Advance(100); fast.Advance(25);
            Assert.That(fast.GameSeconds, Is.EqualTo(normal.GameSeconds).Within(0.00001));
            Assert.That(fast.FuelSeconds, Is.EqualTo(normal.FuelSeconds).Within(0.00001));
            Assert.That(fast.SecondsToWage, Is.EqualTo(normal.SecondsToWage).Within(0.00001));
        }
        [Test]
        public void RecruitingRequiresActualPowerAndDoesNotSpendWages()
        {
            var empty = new PrototypeSimulation(Settings()); Assert.That(empty.Recruit().Success, Is.False);
            var model = Powered(); Assert.That(model.Recruit().Success, Is.True);
            Assert.That(model.Money, Is.Zero); Assert.That(model.Residents[0].Cash, Is.EqualTo(5));
            Assert.That(model.SecondsToWage, Is.EqualTo(600).Within(0.0001));
        }
        [Test]
        public void OldHousingLimitsRecruitmentToFive()
        {
            var model = Powered(); for (int i = 0; i < 5; i++) Assert.That(model.Recruit().Success, Is.True);
            Assert.That(model.Recruit().Success, Is.False);
            Assert.That(model.Residents.Count, Is.EqualTo(5));
        }
        [Test]
        public void WorkerMustWalkToBenchBeforeProducingWithoutPlayer()
        {
            var model = Powered(); model.Recruit(); model.AssignWoodworker(); model.ToggleWork();
            MoveTo(model, 1, 1); model.Advance(1);
            Assert.That(model.State, Is.EqualTo(WorkState.NeedsOperator)); Assert.That(model.CompletedBatches, Is.Zero);
            model.Advance(70);
            Assert.That(model.WorkerAtBench, Is.True); Assert.That(model.CompletedBatches, Is.GreaterThan(0));
        }
        [Test]
        public void PayrollCreatesBillButNeverAutoPays()
        {
            var model = Powered(); model.Recruit(); model.Warehouse.TryAdd(ItemId.Plank, 10); model.Sell(ItemId.Plank, 10);
            decimal money = model.Money; model.Advance(600);
            Assert.That(model.UnpaidWages, Is.EqualTo(10)); Assert.That(model.Money, Is.EqualTo(money));
            Assert.That(model.Residents[0].Cash, Is.EqualTo(5));
        }
        [Test]
        public void PayrollTransfersCashAndClearsActualDebt()
        {
            var model = Powered(); model.Recruit(); model.Warehouse.TryAdd(ItemId.Plank, 10); model.Sell(ItemId.Plank, 10);
            model.Advance(600); long before = model.CashCents + model.Residents.Sum(r => r.CashCents);
            Assert.That(model.PayWages().Success, Is.True);
            Assert.That(model.UnpaidWages, Is.Zero); Assert.That(model.Residents[0].Cash, Is.EqualTo(15));
            Assert.That(model.CashCents + model.Residents.Sum(r => r.CashCents), Is.EqualTo(before));
        }
        [Test]
        public void PartialPayrollPreservesOldBillAndDistributesFairly()
        {
            var model = Powered(); model.Recruit(); model.Recruit(); model.Warehouse.TryAdd(ItemId.Plank, 1); model.Sell(ItemId.Plank, 1);
            model.Advance(600); Assert.That(model.PayWages().Success, Is.True);
            Assert.That(model.UnpaidWages, Is.EqualTo(17));
            Assert.That(model.Residents[0].Cash, Is.EqualTo(6.5m)); Assert.That(model.Residents[1].Cash, Is.EqualTo(6.5m));
            Assert.That(model.WageBills[0].OriginalCents, Is.EqualTo(2000));
            model.Recruit(); Assert.That(model.WageBills[0].ResidentIds.Length, Is.EqualTo(2));
        }
        [Test]
        public void FuelComesFromCarriedStockAndStopsWhenExhausted()
        {
            var model = Powered(); Assert.That(model.AvailablePower, Is.EqualTo(4));
            Assert.That(model.Carry.Count(ItemId.WasteWood), Is.Zero);
            model.Advance(240.1); Assert.That(model.AvailablePower, Is.Zero);
            Assert.That(model.HasBeenPowered, Is.True);
        }
        [Test]
        public void SalesApplyPressurePerItemAndRecoverOverGameTime()
        {
            var model = new PrototypeSimulation(Settings()); model.Warehouse.TryAdd(ItemId.Plank, 30);
            model.SetPaused(true); model.Sell(ItemId.Plank, 30);
            Assert.That(model.Money, Is.EqualTo(78.14m));
            double lowered = model.SalePrice(ItemId.Plank);
            model.Advance(600); Assert.That(model.SalePrice(ItemId.Plank), Is.EqualTo(lowered));
            model.SetPaused(false); model.Advance(600);
            Assert.That(model.SalePrice(ItemId.Plank), Is.GreaterThan(lowered));
        }
        [Test]
        public void AshAppearsAfterActualBurningRatherThanLoadingFuel()
        {
            var model = Powered();
            model.Carry.TryAdd(ItemId.WasteWood, 6); model.AddFuel(ItemId.WasteWood, 6); model.Advance(960);
            model.Carry.TryAdd(ItemId.WasteWood, 6); model.AddFuel(ItemId.WasteWood, 6); model.Advance(720);
            model.Carry.TryAdd(ItemId.WasteWood, 6); model.AddFuel(ItemId.WasteWood, 6);
            Assert.That(model.GeneratorResidue.Count(ItemId.PowerAsh), Is.Zero);
            model.Advance(720.1);
            Assert.That(model.GeneratorResidue.Count(ItemId.PowerAsh), Is.EqualTo(1));
        }
        [Test]
        public void SplittingSalesDoesNotCreateRoundingProfit()
        {
            var first = new PrototypeSimulation(Settings()); var second = new PrototypeSimulation(Settings());
            first.Warehouse.TryAdd(ItemId.Plank, 30); second.Warehouse.TryAdd(ItemId.Plank, 30);
            first.Sell(ItemId.Plank, 30); for (int i = 0; i < 30; i++) second.Sell(ItemId.Plank, 1);
            Assert.That(first.CashCents, Is.EqualTo(second.CashCents));
        }
        [Test]
        public void MotorDismantleUsesDocumentedPresetWithoutReroll()
        {
            var model = new PrototypeSimulation(Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, 4); model.Warehouse.TryAdd(ItemId.RecoveredFastener, 2);
            model.Warehouse.TryAdd(ItemId.DamagedMotor, 1); model.Warehouse.TryAdd(ItemId.Screwdriver, 1);
            Assert.That(model.BuildDismantle().Success, Is.True);
            MoveTo(model, PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY);
            model.ToggleDismantle(); model.Advance(36);
            Assert.That(model.DismantledBatches, Is.EqualTo(1));
            Assert.That(model.Warehouse.Count(ItemId.SteelMaterial), Is.EqualTo(3));
            Assert.That(model.Warehouse.Count(ItemId.CopperMaterial), Is.EqualTo(1));
        }
        [Test]
        public void FreshGameCompletesGeneratorRecruitmentProductionAndFirstPayrollWithoutInjection()
        {
            var model = new PrototypeSimulation(Settings());
            Assert.DoesNotThrow(() => PrototypeStartingScenario.Run(model));
            Assert.That(model.TotalWagesPaidCents, Is.EqualTo(5000));
            Assert.That(model.Residents.Count, Is.EqualTo(5));
            Assert.That(model.CompletedBatches, Is.GreaterThanOrEqualTo(12));
            Assert.That(model.DismantledBatches, Is.EqualTo(1));
        }
        [Test]
        public void SavedSceneUsesCurrentBalanceAndChineseFont()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            Assert.That(game, Is.Not.Null); Assert.That(game.balance, Is.Not.Null); Assert.That(game.appearance.worker, Is.Not.Null);
            Assert.That(game.appearance.font.HasCharacter('薪'), Is.True);
            Assert.That(game.balance.values.warehouseCapacity, Is.EqualTo(80));
            Assert.DoesNotThrow(() => game.balance.values.Validate());
        }
    }
}
