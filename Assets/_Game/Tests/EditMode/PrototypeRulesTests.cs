using System;
using System.Linq;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor;
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
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 0) model.MovePlayer(dx / distance, dy / distance, distance / (model.Settings.moveSpeed * model.Speed));
        }

        private static PrototypeSimulation Ready(PrototypeSettings settings = null)
        {
            var model = new PrototypeSimulation(settings ?? Settings());
            Assert.That(model.Warehouse.TryAdd(ItemId.WasteWood, model.Settings.benchWoodCost + model.Settings.recipeWoodInput * 2), Is.True);
            Assert.That(model.Warehouse.TryAdd(ItemId.RecoveredFastener, model.Settings.benchFastenerCost), Is.True);
            Assert.That(model.Carry.TryAdd(ItemId.WoodHandSaw, 1), Is.True);
            Assert.That(model.BuildBench().Success, Is.True);
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            return model;
        }

        [Test]
        public void InventoryRejectsOverflowAndInvalidAmountsWithoutMutation()
        {
            var inventory = new UnitInventory(3);
            Assert.That(inventory.TryAdd(ItemId.WasteWood, 2), Is.True);
            Assert.That(inventory.TryAdd(ItemId.Plank, 2), Is.False);
            Assert.That(inventory.TryAdd(ItemId.Plank, -1), Is.False);
            Assert.That(inventory.TryRemove(ItemId.WasteWood, 3), Is.False);
            Assert.That(inventory.Used, Is.EqualTo(2));
            Assert.That(inventory.Count(ItemId.WasteWood), Is.EqualTo(2));
        }

        [Test]
        public void BatchConsumptionAggregatesDuplicateCostsAtomically()
        {
            var inventory = new UnitInventory(10);
            inventory.TryAdd(ItemId.WasteWood, 4);
            inventory.TryAdd(ItemId.RecoveredFastener, 1);
            Assert.That(inventory.TryConsume(new ItemAmount(ItemId.WasteWood, 3),
                new ItemAmount(ItemId.WasteWood, 2), new ItemAmount(ItemId.RecoveredFastener, 1)), Is.False);
            Assert.That(inventory.Used, Is.EqualTo(5));
            Assert.That(inventory.TryConsume(new ItemAmount(ItemId.WasteWood, 2),
                new ItemAmount(ItemId.WasteWood, 2), new ItemAmount(ItemId.RecoveredFastener, 1)), Is.True);
            Assert.That(inventory.Used, Is.Zero);
        }

        [Test]
        public void PartialTransferPreservesUnmovedItems()
        {
            var source = new UnitInventory(10);
            var target = new UnitInventory(2);
            source.TryAdd(ItemId.MixedPlastic, 5);
            Assert.That(source.TransferTo(target, ItemId.MixedPlastic, 5), Is.EqualTo(2));
            Assert.That(source.Count(ItemId.MixedPlastic), Is.EqualTo(3));
            Assert.That(target.Count(ItemId.MixedPlastic), Is.EqualTo(2));
            Assert.That(source.TransferTo(target, ItemId.MixedPlastic, 1), Is.Zero);
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
        public void FailedBuildDoesNotConsumePartialMaterials()
        {
            var model = new PrototypeSimulation(Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, model.Settings.benchWoodCost);
            Assert.That(model.BuildBench().Success, Is.False);
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(model.Settings.benchWoodCost));
            Assert.That(model.BenchBuilt, Is.False);
        }

        [Test]
        public void PausedBuildReservesMaterialsAndCompletesOnlyAfterResume()
        {
            var model = new PrototypeSimulation(Settings());
            model.Warehouse.TryAdd(ItemId.WasteWood, model.Settings.benchWoodCost);
            model.Warehouse.TryAdd(ItemId.RecoveredFastener, model.Settings.benchFastenerCost);
            model.SetPaused(true);
            Assert.That(model.BuildBench().Success, Is.True);
            model.Advance(100);
            Assert.That(model.BenchBuilt, Is.False);
            Assert.That(model.BuildPending, Is.True);
            Assert.That(model.Warehouse.Used, Is.Zero);
            model.SetPaused(false); model.Advance(0.05);
            Assert.That(model.BenchBuilt, Is.True);
            Assert.That(model.BuildPending, Is.False);
        }

        [Test]
        public void MissingToolStopsBeforeReservingRawMaterialsAndRecovers()
        {
            var model = Ready();
            model.Carry.TryRemove(ItemId.WoodHandSaw, 1);
            model.ToggleWork();
            int wood = model.Warehouse.Count(ItemId.WasteWood);
            model.Advance(2);
            Assert.That(model.State, Is.EqualTo(WorkState.NeedsTool));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(wood));
            model.Carry.TryAdd(ItemId.WoodHandSaw, 1); model.Advance(0.1);
            Assert.That(model.State, Is.EqualTo(WorkState.Working));
            Assert.That(model.ReservedWood, Is.EqualTo(model.Settings.recipeWoodInput));
        }

        [Test]
        public void PauseStopsInputAndProductionUsingTheSameClock()
        {
            PrototypeSettings settings = Settings(); settings.automaticInput = true;
            var model = Ready(settings);
            model.ToggleWork(); model.Advance(0.5);
            double seconds = model.GameSeconds; float progress = model.Progress;
            long spawned = model.SpawnedUnits;
            model.SetPaused(true); model.SetSpeed(4); model.Advance(200);
            Assert.That(model.GameSeconds, Is.EqualTo(seconds));
            Assert.That(model.Progress, Is.EqualTo(progress));
            Assert.That(model.SpawnedUnits, Is.EqualTo(spawned));
        }

        [Test]
        public void StopAndLeavingTheBenchPreserveReservedMaterialsAndProgress()
        {
            var model = Ready();
            model.ToggleWork(); model.Advance(1);
            float progress = model.Progress; int stored = model.Warehouse.Count(ItemId.WasteWood);
            model.ToggleWork(); model.Advance(20);
            Assert.That(model.Progress, Is.EqualTo(progress));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(stored));
            model.ToggleWork(); MoveTo(model, 1, 1); model.Advance(2);
            Assert.That(model.State, Is.EqualTo(WorkState.NeedsOperator));
            Assert.That(model.Progress, Is.EqualTo(progress));
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY); model.Advance(0.5);
            Assert.That(model.Progress, Is.GreaterThan(progress));
        }

        [Test]
        public void BlockedOutputDoesNotConsumeTheNextBatchAndResumesWhenSpaceIsFreed()
        {
            var settings = Settings(); settings.warehouseCapacity = 10;
            settings.outputCapacity = 2; settings.plankOutput = 2;
            var model = Ready(settings);
            model.ToggleWork(); model.Advance(0.05);
            model.Warehouse.TryAdd(ItemId.MixedPlastic, model.Warehouse.Free);
            model.Advance(30);
            Assert.That(model.State, Is.EqualTo(WorkState.OutputBlocked));
            Assert.That(model.CompletedBatches, Is.EqualTo(1));
            Assert.That(model.Output.Count(ItemId.Plank), Is.EqualTo(2));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(2));
            MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Assert.That(model.Withdraw(ItemId.MixedPlastic, 2).Success, Is.True);
            model.Advance(0.05);
            Assert.That(model.Output.Used, Is.Zero);
            Assert.That(model.Warehouse.Count(ItemId.Plank), Is.EqualTo(2));
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY); model.Advance(0.1);
            Assert.That(model.State, Is.EqualTo(WorkState.Working));
        }

        [Test]
        public void PressureUsesOccupiedTilesAndIgnoresWarehouseStock()
        {
            var model = new PrototypeSimulation(Settings());
            float original = model.HumanEfficiency;
            model.Warehouse.TryAdd(ItemId.MixedPlastic, 10);
            Assert.That(model.HumanEfficiency, Is.EqualTo(original));
            MoveTo(model, 8, 7);
            Assert.That(model.PickUp().Success, Is.True);
            Assert.That(model.HumanEfficiency, Is.EqualTo(original));
            Assert.That(model.PickUp(4).Success, Is.True);
            Assert.That(model.HumanEfficiency, Is.GreaterThan(original));
        }

        [Test]
        public void FullMapRetainsIncomingWasteInsteadOfDiscardingIt()
        {
            var settings = Settings(); settings.automaticInput = true;
            settings.firstInputSeconds = 0.1f; settings.inputGrowthSeconds = 100000;
            settings.initialWaveUnits = 100;
            var model = new PrototypeSimulation(settings);
            long initial = model.Ground.Sum(p => (long)p.Amount);
            model.Advance(2);
            Assert.That(model.IncomingUnits, Is.GreaterThan(0));
            Assert.That(model.Ground.Sum(p => (long)p.Amount) + model.IncomingUnits, Is.EqualTo(initial + model.SpawnedUnits));
            Assert.That(model.Ground.Select(p => (p.X, p.Y)).Distinct().Count(), Is.EqualTo(model.Ground.Count));
            Assert.That(model.Ground.All(p => !PrototypeSimulation.IsReservedCell(p.X, p.Y)), Is.True);
        }

        [Test]
        public void FixedClockProducesTheSameResultAcrossFramePartitions()
        {
            var first = Ready(); var second = Ready();
            first.ToggleWork(); second.ToggleWork();
            first.Advance(15);
            for (int i = 0; i < 300; i++) second.Advance(0.05);
            Assert.That(first.CompletedBatches, Is.EqualTo(second.CompletedBatches));
            Assert.That(first.Warehouse.Count(ItemId.Plank), Is.EqualTo(second.Warehouse.Count(ItemId.Plank)));
            Assert.That(first.GameSeconds, Is.EqualTo(second.GameSeconds).Within(0.0001));
        }

        [Test]
        public void AccelerationScalesMovementInputAndProductionTogether()
        {
            PrototypeSettings firstSettings = Settings(); firstSettings.automaticInput = true;
            PrototypeSettings secondSettings = Settings(); secondSettings.automaticInput = true;
            var normal = Ready(firstSettings); var fast = Ready(secondSettings);
            normal.ToggleWork(); fast.ToggleWork(); fast.SetSpeed(4);
            normal.Advance(24); fast.Advance(6);
            Assert.That(fast.GameSeconds, Is.EqualTo(normal.GameSeconds).Within(0.0001));
            Assert.That(fast.SpawnedUnits, Is.EqualTo(normal.SpawnedUnits));
            Assert.That(fast.CompletedBatches, Is.EqualTo(normal.CompletedBatches));
            normal.MovePlayer(0, 1, 0.2); fast.MovePlayer(0, 1, 0.05);
            Assert.That(fast.PlayerY, Is.EqualTo(normal.PlayerY).Within(0.0001));
        }

        [Test]
        public void FreshGameCanPickBuildProduceAndSellWithoutInjectedResources()
        {
            var model = new PrototypeSimulation(Settings());
            MoveTo(model, 4, 5); Assert.That(model.PickUp().Success, Is.True);
            MoveTo(model, 5, 5); Assert.That(model.PickUp(int.MaxValue).Success, Is.True);
            MoveTo(model, 5, 6); Assert.That(model.PickUp(int.MaxValue).Success, Is.True);
            MoveTo(model, 6, 6); Assert.That(model.PickUp(int.MaxValue).Success, Is.True);
            MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Assert.That(model.Deposit().Success, Is.True);
            Assert.That(model.Carry.Count(ItemId.WoodHandSaw), Is.EqualTo(1));
            Assert.That(model.BuildBench().Success, Is.True);
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            model.ToggleWork(); model.Advance(25);
            Assert.That(model.CompletedBatches, Is.GreaterThan(0));
            Assert.That(model.Sell(ItemId.Plank, 1).Success, Is.True);
            Assert.That(model.Money, Is.EqualTo(model.Settings.plankPrice));
        }

        [Test]
        public void OnlyProductsInTheWarehouseCanBeSoldAndPauseAllowsSales()
        {
            var model = new PrototypeSimulation(Settings());
            model.Carry.TryAdd(ItemId.Plank, 1);
            model.Warehouse.TryAdd(ItemId.WasteWood, 1);
            Assert.That(model.Sell(ItemId.Plank, 1).Success, Is.False);
            Assert.That(model.Sell(ItemId.WasteWood, 1).Success, Is.False);
            model.Warehouse.TryAdd(ItemId.WoodHandle, 2);
            model.SetPaused(true);
            Assert.That(model.Sell(ItemId.WoodHandle, int.MaxValue).Success, Is.True);
            Assert.That(model.Money, Is.EqualTo(2 * model.Settings.handlePrice));
            Assert.That(model.Warehouse.Count(ItemId.WasteWood), Is.EqualTo(1));
        }

        [Test]
        public void SavedSceneHasConfigurationArtAndPortableChineseFont()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");
            PrototypeGame game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.balance, Is.Not.Null);
            Assert.That(game.appearance, Is.Not.Null);
            Assert.That(game.appearance.worker, Is.Not.Null);
            Assert.That(game.appearance.font.HasCharacter('锯'), Is.True);
            Assert.DoesNotThrow(() => game.balance.values.Validate());
        }
    }
}
