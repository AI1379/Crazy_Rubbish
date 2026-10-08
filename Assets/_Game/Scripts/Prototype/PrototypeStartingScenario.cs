using System;
using System.Linq;
using CrazyRubbish.Core;

namespace CrazyRubbish.Prototype
{
    // Deterministic acceptance path. Only ordinary player actions; never injects stock or money.
    public static class PrototypeStartingScenario
    {
        public static void Run(PrototypeSimulation model)
        {
            Collect(model, ItemId.WoodHandSaw, 1);
            Collect(model, ItemId.RecoveredFastener, 4);
            Collect(model, ItemId.WasteWood, 16);
            Require(model.BuildBench()); Require(model.BuildDismantle());
            MoveTo(model, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            Require(model.ToggleWork()); model.Advance(81); Require(model.ToggleWork());
            if (model.Warehouse.Count(ItemId.Plank) < 6) throw new InvalidOperationException("手工两批未产出6木板。");
            Collect(model, ItemId.SteelPart, 8);
            Require(model.BuildTable());
            Collect(model, ItemId.ThermalModule, 1);
            Collect(model, ItemId.GeneratorModule, 1);
            Collect(model, ItemId.RecoveredWire, 2);
            Require(model.BuildGenerator());
            Collect(model, ItemId.Screwdriver, 1);
            Collect(model, ItemId.DamagedMotor, 1);
            MoveTo(model, PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY);
            Require(model.ToggleDismantle()); model.Advance(36); Require(model.ToggleDismantle());
            if (model.Warehouse.Count(ItemId.SteelMaterial) != 3 || model.Warehouse.Count(ItemId.CopperMaterial) != 1)
                throw new InvalidOperationException("损坏电机预设拆解产物不符。");
            Collect(model, ItemId.WasteWood, 42);
            MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Require(model.Withdraw(ItemId.WasteWood, 2));
            MoveTo(model, PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY);
            Require(model.AddFuel(ItemId.WasteWood, 2));
            for (int i = 0; i < 5; i++) Require(model.Recruit());
            Require(model.AssignWoodworker()); Require(model.ToggleWork());
            MoveTo(model, 3, 2);
            model.Advance(model.SecondsToWage + 0.1);
            if (model.UnpaidWages != 50 || model.Warehouse.Count(ItemId.Plank) < 30)
                throw new InvalidOperationException("首期工资或居民产量不符。");
            Require(model.Sell(ItemId.Plank, int.MaxValue)); Require(model.PayWages());
            if (model.UnpaidWageCents != 0 || model.Residents.Sum(r => r.CashCents) != 7500)
                throw new InvalidOperationException("工资没有真实进入5名居民钱包。");
            Collect(model, ItemId.WasteWood, 2);
            MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Require(model.Withdraw(ItemId.WasteWood, 2));
            MoveTo(model, PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY);
            Require(model.AddFuel(ItemId.WasteWood, 2));
        }

        public static void Collect(PrototypeSimulation model, ItemId item, int target)
        {
            for (int trip = 0; trip < 100; trip++)
            {
                int total = model.Warehouse.Count(item) + model.Carry.Count(item);
                if (total >= target)
                {
                    MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
                    if (item != ItemId.WoodHandSaw && model.Carry.Count(item) > 0) Require(model.Deposit());
                    return;
                }
                GroundPile pile = model.Ground.FirstOrDefault(p => p.Item == item);
                if (pile == null) throw new InvalidOperationException("起步缺少地面物品：" + item);
                MoveTo(model, pile.X, pile.Y);
                int count = Math.Min(target - total, model.Carry.AvailableSlots(item));
                if (count > 0) Require(model.PickUp(count, item));
                MoveTo(model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
                if (item != ItemId.WoodHandSaw) Require(model.Deposit());
            }
            throw new InvalidOperationException("起步搬运未完成：" + item);
        }
        public static void MoveTo(PrototypeSimulation model, double x, double y)
        {
            for (int step = 0; step < 4000; step++)
            {
                double dx = x - model.PlayerX, dy = y - model.PlayerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 0.001) return;
                double delta = Math.Min(0.05, distance / (model.Settings.moveSpeed * model.Speed));
                model.MovePlayer(dx / distance, dy / distance, delta); model.Advance(delta);
            }
            throw new InvalidOperationException("未能到达交互点。");
        }
        public static void Require(ActionResult result)
        { if (!result.Success) throw new InvalidOperationException(result.Message); }
    }
}
