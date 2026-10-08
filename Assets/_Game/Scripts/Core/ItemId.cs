using System;

namespace CrazyRubbish.Core
{
    public enum ItemId
    {
        WasteWood, MixedPlastic, RecoveredFastener, WoodHandSaw, Plank, WoodHandle,
        WoodChips, SteelPart, CopperPart, RecoveredWire, ThermalModule, GeneratorModule,
        Screwdriver, Wrench, Hammer, Scissors, SewingKit, Food, DrinkingWater, Soap,
        WasteTextile, OldClothes, DamagedMotor, SteelMaterial, CopperMaterial, PowerAsh
    }
    public enum WorkState { NotBuilt, Idle, Paused, NeedsOperator, NeedsTool, NeedsMaterials, Working, OutputBlocked }
    public enum WoodRecipe { Plank, WoodHandle }
    public enum WorkerJob { Idle, Woodwork }

    public readonly struct ItemAmount
    {
        public readonly ItemId Item;
        public readonly int Amount;
        public ItemAmount(ItemId item, int amount) { Item = item; Amount = amount; }
    }
    public readonly struct ActionResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly int Amount;
        public ActionResult(bool success, string message, int amount = 0)
        { Success = success; Message = message; Amount = amount; }
    }
    public sealed class GroundPile
    {
        public readonly int X, Y;
        public readonly ItemId Item;
        public int Amount { get; internal set; }
        internal GroundPile(int x, int y, ItemId item, int amount)
        { X = x; Y = y; Item = item; Amount = amount; }
    }
    public sealed class Resident
    {
        public int Id { get; }
        public WorkerJob Job { get; internal set; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public long CashCents { get; internal set; } = 500;
        public decimal Cash => CashCents / 100m;
        internal Resident(int id) { Id = id; X = PrototypeSimulation.HousingX; Y = PrototypeSimulation.HousingY; }
    }
    public sealed class WageBill
    {
        public int[] ResidentIds { get; }
        public long[] UnpaidCents { get; }
        public long OriginalCents { get; }
        public double DueAt { get; }
        public long RemainingCents
        {
            get { long sum = 0; foreach (long value in UnpaidCents) sum += value; return sum; }
        }
        internal WageBill(int[] ids, long perPerson, double dueAt)
        {
            ResidentIds = (int[])ids.Clone();
            UnpaidCents = new long[ids.Length];
            for (int i = 0; i < ids.Length; i++) UnpaidCents[i] = perPerson;
            OriginalCents = perPerson * ids.Length;
            DueAt = dueAt;
        }
    }

    public static class ItemCatalog
    {
        public const int CapacityScale = 20;
        public static int CapacityTicks(ItemId item)
        {
            switch (item)
            {
                case ItemId.SteelPart: case ItemId.CopperPart: case ItemId.WoodHandle:
                case ItemId.RecoveredFastener: case ItemId.RecoveredWire: case ItemId.Soap: return 10;
                case ItemId.DrinkingWater: return 2;
                case ItemId.ThermalModule: case ItemId.GeneratorModule: return 80;
                case ItemId.DamagedMotor: return 120;
                default: return 20;
            }
        }
        public static bool IsTool(ItemId item) =>
            item == ItemId.WoodHandSaw || item == ItemId.Screwdriver || item == ItemId.Wrench ||
            item == ItemId.Hammer || item == ItemId.Scissors || item == ItemId.SewingKit;
    }
}
