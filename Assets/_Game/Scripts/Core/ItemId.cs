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
    public enum WorkState { NotBuilt, Idle, Paused, NeedsOperator, NeedsTool, NeedsMaterials, Working, OutputBlocked, NeedsPower, NeedsMaintenance, MachineRunning, Preparing }
    public enum WoodRecipe { Plank, WoodHandle, SortedWood }
    public enum WorkerJob { Idle, Woodwork, MachineOperator, Maintenance }

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
        public int FacilityId { get; internal set; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public long CashCents { get; internal set; } = 500;
        public decimal Cash => CashCents / 100m;
        public UnitInventory Cargo { get; } = new UnitInventory(8);
        public string Activity { get; internal set; } = "空闲";
        public bool PickingUp => Delivery!=null&&!Delivery.Picked&&Delivery.Ground!=null&&PrototypeSimulation.Distance(X,Y,Delivery.SourceX,Delivery.SourceY)<0.001;
        public LivingNeeds Needs { get; internal set; } = new LivingNeeds();
        internal DeliveryTask Delivery;
        internal int[] Path; internal int PathIndex,PathTargetX,PathTargetY,PathRevision;
        internal Resident(int id) { Id = id; X = 15 + (id - 1) % 5; Y = 5; }
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
        public static int CapacityTicks(ItemId item) => DesignBaseline.CapacityTicks(item);
        public static bool IsTool(ItemId item) =>
            item == ItemId.WoodHandSaw || item == ItemId.Screwdriver || item == ItemId.Wrench ||
            item == ItemId.Hammer || item == ItemId.Scissors || item == ItemId.SewingKit;
    }
}
