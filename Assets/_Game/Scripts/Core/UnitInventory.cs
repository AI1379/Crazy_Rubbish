using System;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    // Prototype only: every unit takes one capacity slot, including a usable tool.
    public sealed class UnitInventory
    {
        private static readonly int ItemCount = Enum.GetValues(typeof(ItemId)).Length;
        private readonly int[] quantities = new int[ItemCount];
        public int Capacity { get; }
        public int Used { get; private set; }
        public int Free => Capacity - Used;

        public UnitInventory(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        private static int Index(ItemId item)
        {
            int index = (int)item;
            if (index < 0 || index >= ItemCount)
                throw new ArgumentOutOfRangeException(nameof(item));
            return index;
        }

        public int Count(ItemId item) => quantities[Index(item)];

        public bool TryAdd(ItemId item, int amount)
        {
            int index = Index(item);
            if (amount <= 0 || amount > Free) return false;
            quantities[index] += amount;
            Used += amount;
            return true;
        }

        public bool TryRemove(ItemId item, int amount)
        {
            int index = Index(item);
            if (amount <= 0 || amount > quantities[index]) return false;
            quantities[index] -= amount;
            Used -= amount;
            return true;
        }

        public bool TryConsume(params ItemAmount[] costs)
        {
            if (costs == null || costs.Length == 0) return false;
            var totals = new Dictionary<ItemId, int>();
            foreach (ItemAmount cost in costs)
            {
                Index(cost.Item);
                if (cost.Amount <= 0) return false;
                totals.TryGetValue(cost.Item, out int previous);
                long combined = (long)previous + cost.Amount;
                if (combined > int.MaxValue) return false;
                totals[cost.Item] = (int)combined;
            }
            foreach (var cost in totals)
                if (Count(cost.Key) < cost.Value) return false;
            foreach (var cost in totals) TryRemove(cost.Key, cost.Value);
            return true;
        }

        public int TransferTo(UnitInventory target, ItemId item, int requested)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (ReferenceEquals(this, target) || requested <= 0) return 0;
            int moved = Math.Min(Math.Min(Count(item), requested), target.Free);
            if (moved <= 0) return 0;
            // Both operations are guaranteed to succeed after the checks above.
            target.TryAdd(item, moved);
            TryRemove(item, moved);
            return moved;
        }
    }
}
