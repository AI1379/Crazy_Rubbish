using System;

namespace CrazyRubbish.Core
{
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
        public HappinessState Mood { get; internal set; } = new HappinessState();
        public double Happiness { get; internal set; } = 70;
        public bool Rioting => Mood.rioting;
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
        public static readonly ItemId[] All = (ItemId[])Enum.GetValues(typeof(ItemId));
        public static int CapacityTicks(ItemId item) => DesignCatalog.ItemTicks[(int)item];
        private static readonly bool[] toolFlags = Flags("工具"), brokenFlags = Flags("损坏工具"), deviceFlags = Flags("废旧设备");
        private static bool[] Flags(string category) => Array.ConvertAll(DesignCatalog.ItemCategories, c => c == category);
        public static bool IsTool(ItemId item) => toolFlags[(int)item];
        public static bool IsBrokenTool(ItemId item) => brokenFlags[(int)item];
        internal static bool IsCatalogDevice(ItemId item) => deviceFlags[(int)item];
        public static string ToolFamily(ItemId item) => DesignCatalog.ToolFamilies[(int)item];
        public static int ToolDurability(ItemId item) => DesignCatalog.ToolDurability[(int)item];
        public static double ToolEfficiency(ItemId item) => DesignCatalog.ToolEfficiency[(int)item];
        public static bool IsPackage(ItemId item) => DesignCatalog.PackPayloads[(int)item] != null;
    }
}
