using System;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    // Capacity uses exact integer twentieths; quantities remain whole item instances/litres.
    public sealed class UnitInventory
    {
        private static readonly int ItemCount = Enum.GetValues(typeof(ItemId)).Length;
        private readonly int[] quantities = new int[ItemCount];
        public int Capacity { get; private set; }
        public int UsedTicks { get; private set; }
        public int FreeTicks => Capacity * ItemCatalog.CapacityScale - UsedTicks;
        public decimal Used => UsedTicks / (decimal)ItemCatalog.CapacityScale;
        public decimal Free => FreeTicks / (decimal)ItemCatalog.CapacityScale;

        public UnitInventory(int capacity)
        {
            if (capacity <= 0 || capacity > 1000000) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }
        private static int Index(ItemId item)
        {
            int index = (int)item;
            if (index < 0 || index >= ItemCount) throw new ArgumentOutOfRangeException(nameof(item));
            return index;
        }
        public int Count(ItemId item) => quantities[Index(item)];
        public int AvailableSlots(ItemId item) { Index(item); return FreeTicks / ItemCatalog.CapacityTicks(item); }
        public bool TryAdd(ItemId item, int amount) => TryAddBatch(new ItemAmount(item, amount));
        public bool TryAddBatch(params ItemAmount[] items)
        {
            if (!Aggregate(items, out Dictionary<ItemId, int> totals)) return false;
            long ticks = 0;
            foreach (var pair in totals)
            {
                ticks += (long)pair.Value * ItemCatalog.CapacityTicks(pair.Key);
                if ((long)Count(pair.Key) + pair.Value > int.MaxValue) return false;
            }
            if (ticks > FreeTicks) return false;
            foreach (var pair in totals) quantities[Index(pair.Key)] += pair.Value;
            UsedTicks += (int)ticks;
            return true;
        }
        public bool TryRemove(ItemId item, int amount)
        {
            int index = Index(item);
            if (amount <= 0 || amount > quantities[index]) return false;
            quantities[index] -= amount;
            UsedTicks -= amount * ItemCatalog.CapacityTicks(item);
            return true;
        }
        public bool TryConsume(params ItemAmount[] costs)
        {
            if (!Aggregate(costs, out Dictionary<ItemId, int> totals)) return false;
            foreach (var pair in totals) if (Count(pair.Key) < pair.Value) return false;
            foreach (var pair in totals) TryRemove(pair.Key, pair.Value);
            return true;
        }
        private static bool Aggregate(ItemAmount[] items, out Dictionary<ItemId, int> totals)
        {
            totals = new Dictionary<ItemId, int>();
            if (items == null || items.Length == 0) return false;
            foreach (var item in items)
            {
                Index(item.Item);
                if (item.Amount <= 0) return false;
                totals.TryGetValue(item.Item, out int previous);
                long sum = (long)previous + item.Amount;
                if (sum > int.MaxValue) return false;
                totals[item.Item] = (int)sum;
            }
            return true;
        }
        public int TransferTo(UnitInventory target, ItemId item, int requested)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (ReferenceEquals(this, target) || requested <= 0) return 0;
            int moved = Math.Min(Math.Min(Count(item), requested), target.AvailableSlots(item));
            if (moved <= 0) return 0;
            target.TryAdd(item, moved); TryRemove(item, moved);
            return moved;
        }
        public void SetCapacity(int capacity)
        {
            if (capacity <= 0 || capacity > 1000000 || (long)capacity * ItemCatalog.CapacityScale < UsedTicks)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }
    }
}
