using System;

namespace CrazyRubbish.Core
{
    public enum ItemId { WasteWood, MixedPlastic, RecoveredFastener, WoodHandSaw, Plank, WoodHandle }
    public enum WorkState { NotBuilt, Idle, Paused, NeedsOperator, NeedsTool, NeedsMaterials, Working, OutputBlocked }
    public enum WoodRecipe { Plank, WoodHandle }

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
        public readonly int X;
        public readonly int Y;
        public readonly ItemId Item;
        public int Amount { get; internal set; }
        internal GroundPile(int x, int y, ItemId item, int amount)
        { X = x; Y = y; Item = item; Amount = amount; }
    }
}
