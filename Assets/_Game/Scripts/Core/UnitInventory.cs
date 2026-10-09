using System;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    // Capacity uses exact integer twentieths; quantities remain whole item instances/litres.
    public sealed class UnitInventory
    {
        private static readonly int ItemCount = Enum.GetValues(typeof(ItemId)).Length;
        private readonly int[] quantities = new int[ItemCount];
        private readonly Dictionary<int, Dictionary<ItemId,int>> reservations = new Dictionary<int, Dictionary<ItemId,int>>();
        private int nextReservation = 1;
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
        public int Reserved(ItemId item) { Index(item); int result=0; foreach(var token in reservations.Values) if(token.TryGetValue(item,out int amount)) result+=amount; return result; }
        public int Available(ItemId item) => Count(item)-Reserved(item);
        public int Reserve(params ItemAmount[] costs)
        {
            if(!Aggregate(costs,out var totals)) return 0;
            foreach(var pair in totals) if(Available(pair.Key)<pair.Value) return 0;
            int id=nextReservation++;reservations[id]=totals;return id;
        }
        public bool Release(int token) => reservations.Remove(token);
        public bool Commit(int token)
        {
            if(!reservations.TryGetValue(token,out var totals)) return false;
            reservations.Remove(token);
            foreach(var pair in totals) { quantities[Index(pair.Key)]-=pair.Value;UsedTicks-=pair.Value*ItemCatalog.CapacityTicks(pair.Key); }
            return true;
        }
        public bool TransferReserved(int token,UnitInventory target)
        {
            if(target==null || ReferenceEquals(target,this) || !reservations.TryGetValue(token,out var totals)) return false;
            var amounts=new List<ItemAmount>();foreach(var pair in totals) amounts.Add(new ItemAmount(pair.Key,pair.Value));
            if(!target.TryAddBatch(amounts.ToArray())) return false;
            return Commit(token);
        }
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
            if (amount <= 0 || amount > Available(item)) return false;
            quantities[index] -= amount;
            UsedTicks -= amount * ItemCatalog.CapacityTicks(item);
            return true;
        }
        public bool TryConsume(params ItemAmount[] costs)
        {
            if (!Aggregate(costs, out Dictionary<ItemId, int> totals)) return false;
            foreach (var pair in totals) if (Available(pair.Key) < pair.Value) return false;
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
            int moved = Math.Min(Math.Min(Available(item), requested), target.AvailableSlots(item));
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
        public InventorySave Capture()
        {
            var saved=new InventorySave{capacity=Capacity,nextToken=nextReservation,counts=(int[])quantities.Clone(),reservations=new List<ReservationSave>()};
            foreach(var token in reservations){var row=new ReservationSave{id=token.Key,items=new int[token.Value.Count],amounts=new int[token.Value.Count]};int i=0;foreach(var part in token.Value){row.items[i]=(int)part.Key;row.amounts[i++]=part.Value;}saved.reservations.Add(row);}
            return saved;
        }
        public void Restore(InventorySave saved)
        {
            if(saved.counts.Length!=ItemCount)throw new ArgumentException("存档物品数不同。");
            reservations.Clear();UsedTicks=0;Capacity=saved.capacity;nextReservation=saved.nextToken;
            for(int i=0;i<ItemCount;i++){if(saved.counts[i]<0)throw new ArgumentException("非法库存数量。");quantities[i]=saved.counts[i];UsedTicks+=quantities[i]*ItemCatalog.CapacityTicks((ItemId)i);}
            if(UsedTicks>Capacity*20)throw new ArgumentException("存档库存超过容量。");
            foreach(var row in saved.reservations){var token=new Dictionary<ItemId,int>();for(int i=0;i<row.items.Length;i++)token[(ItemId)row.items[i]]=row.amounts[i];reservations[row.id]=token;}
            foreach(ItemId item in Enum.GetValues(typeof(ItemId)))if(Reserved(item)>Count(item))throw new ArgumentException("存档预约超过库存。");
        }
    }
}
