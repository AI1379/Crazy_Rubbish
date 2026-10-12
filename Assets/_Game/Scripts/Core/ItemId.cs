using System;

namespace CrazyRubbish.Core
{
    public enum ItemId
    {
        WasteWood, MixedPlastic, RecoveredFastener, WoodHandSaw, Plank, WoodHandle,
        WoodChips, SteelPart, CopperPart, RecoveredWire, ThermalModule, GeneratorModule,
        Screwdriver, Wrench, Hammer, Scissors, SewingKit, Food, DrinkingWater, Soap,
        WasteTextile, OldClothes, DamagedMotor, SteelMaterial, CopperMaterial, PowerAsh,
        Motor, MachineBlade, HeatingElement, Container, HandleMould, BoxMould, FeedingModule, GrowingLight,
        FurnaceModule, FurnaceLining, Fixture, AluminumPart, AluminumMaterial, ToolSteelWaste,
        OldCutter, OldWasher, OldFormer, MouldBox, OldContainer, WireBundle, OldGrowingLamp,
        OldFeeder, OldFurnace, OldMetalTable, OldControlBox, LightingModule, OldLamp,
        SawBlade, HammerHead, ScrewdriverRod, BroomHead, SortedWood, BrokenWoodTool
    }
    public enum WorkState { NotBuilt, Idle, Paused, NeedsOperator, NeedsTool, NeedsMaterials, Working, OutputBlocked }
    public enum WoodRecipe { Plank, WoodHandle, SortedWood }
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
        public int Id { get; internal set; }
        public int Reserved { get; internal set; }
        public bool Usable { get; internal set; } = true;
        public int ToolId { get; internal set; }
        public int Width { get; internal set; } = 1;
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
        public UnitInventory Cargo { get; } = new UnitInventory(8);
        public string Activity { get; internal set; } = "空闲";
        public LivingNeeds Needs { get; internal set; } = new LivingNeeds();
        internal DeliveryTask Delivery;
        internal int[] Path; internal int PathIndex,PathTargetX,PathTargetY,PathRevision;
        internal Resident(int id) { Id = id; X = 14 + id; Y = 5; }
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

    public static partial class ItemCatalog
    {
        public const int CapacityScale = 20;
        public static int CapacityTicks(ItemId item)
        {
            switch (item)
            {
                case ItemId.SteelPart: case ItemId.CopperPart: case ItemId.WoodHandle:
                case ItemId.RecoveredFastener: case ItemId.RecoveredWire: case ItemId.Soap:
                case ItemId.AluminumPart: case ItemId.HandleMould: case ItemId.BoxMould: case ItemId.SawBlade: case ItemId.MachineBlade:
                case ItemId.HammerHead: case ItemId.ScrewdriverRod: case ItemId.BroomHead: return 10;
                case ItemId.DrinkingWater: return 2;
                case ItemId.ThermalModule: case ItemId.GeneratorModule: case ItemId.Motor:
                case ItemId.FeedingModule: case ItemId.FurnaceModule: return 80;
                case ItemId.Container: case ItemId.FurnaceLining: case ItemId.Fixture:
                case ItemId.HeatingElement: case ItemId.GrowingLight: case ItemId.LightingModule: case ItemId.OldContainer: return 40;
                case ItemId.DamagedMotor: case ItemId.OldCutter: case ItemId.OldWasher: case ItemId.OldFormer:
                case ItemId.MouldBox: case ItemId.WireBundle: case ItemId.OldGrowingLamp: case ItemId.OldFeeder:
                case ItemId.OldFurnace: case ItemId.OldMetalTable: case ItemId.OldControlBox: case ItemId.OldLamp: return 120;
                default: return 20;
            }
        }
        public static bool IsTool(ItemId item) =>
            item == ItemId.WoodHandSaw || item == ItemId.Screwdriver || item == ItemId.Wrench ||
            item == ItemId.Hammer || item == ItemId.Scissors || item == ItemId.SewingKit;
    }
}
