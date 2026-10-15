using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable]
    public sealed class OpeningProgress
    {
        public int batch;
        public double nextAt = 2;
        public OpeningProgress Copy() => (OpeningProgress)MemberwiseClone();
    }

    public sealed partial class PrototypeSimulation
    {
        public const int ReceivingX = 7, ReceivingY = 13, DumpX = 4, DumpY = 20;
        public OpeningProgress Opening { get; private set; }
        public long DumpedTicks { get; private set; }
        public bool HasDiscovered(ItemId item) => discovered.Contains(item);
        public bool TableUnlocked => HasDiscovered(ItemId.Plank) && HasDiscovered(ItemId.SteelPart);
        public bool GeneratorUnlocked => HasDiscovered(ItemId.SteelPart);
        private readonly List<(int stage, ItemId item, int amount)> openingShipments = new List<(int, ItemId, int)>();
        public bool AwaitingOpeningItem(ItemId item) => Opening != null && openingShipments.Skip(Opening.batch).Any(s => s.item == item);

        private void PrepareOpening()
        {
            void Add(int stage, ItemId item, int amount) { if (amount > 0) openingShipments.Add((stage, item, amount)); }
            Add(0, ItemId.WoodHandSaw, 1);
            for (int i = 0; i < 5; i++)
            {
                Add(0, ItemId.Food, 6); Add(0, ItemId.DrinkingWater, 8);
                if (i < 3) Add(0, ItemId.WasteWood, Math.Min(8, Math.Max(0, Settings.initialWood - i * 8)));
                if (i < 2) Add(0, ItemId.RecoveredFastener, Math.Min(2, Math.Max(0, Settings.initialFasteners - i * 2)));
            }
            Add(1, ItemId.SteelPart, 8); Add(1, ItemId.Screwdriver, 1); Add(1, ItemId.OldCutter, 1);
            Add(1, ItemId.WasteWood, Math.Min(16, Math.Max(0, Settings.initialWood - 24)));
            Add(1, ItemId.RecoveredFastener, Math.Max(0, Settings.initialFasteners - 4));
            Add(2, ItemId.ThermalModule, 1); Add(2, ItemId.GeneratorModule, 1); Add(2, ItemId.RecoveredWire, 8);
            Add(2, ItemId.SteelPart, 22);
            for (int wood = 40; wood < Settings.initialWood; wood += 8) Add(2, ItemId.WasteWood, Math.Min(8, Settings.initialWood - wood));
            Add(3, ItemId.CopperPart, 2); Add(3, ItemId.WasteTextile, 12); Add(3, ItemId.OldClothes, 6); Add(3, ItemId.Soap, 3);
            foreach (var item in new[] { ItemId.Hammer, ItemId.Wrench, ItemId.Scissors, ItemId.SewingKit, ItemId.OldCutter,
                ItemId.OldWasher, ItemId.OldFormer, ItemId.MouldBox, ItemId.OldContainer, ItemId.OldContainer, ItemId.WireBundle,
                ItemId.OldGrowingLamp, ItemId.OldFeeder, ItemId.OldFurnace, ItemId.OldFurnace, ItemId.OldMetalTable, ItemId.OldControlBox }) Add(3, item, 1);
        }

        private void TickOpening()
        {
            if (Opening == null || Opening.batch >= openingShipments.Count) return;
            var shipment = openingShipments[Opening.batch];
            bool ready = shipment.stage == 0 || shipment.stage == 1 && BenchBuilt || shipment.stage == 2 && CompletedBatches > 0 || shipment.stage == 3 && HasBeenPowered;
            if (!ready) { Opening.nextAt = Math.Max(Opening.nextAt, GameSeconds + 8); return; }
            if (GameSeconds + 1e-6 < Opening.nextAt) return;
            AddNatural(shipment.item, shipment.amount, ReceivingX, ReceivingY - 2, true);
            Opening.batch++; Opening.nextAt = GameSeconds + (shipment.stage == 0 ? 7 : shipment.stage == 3 ? 24 : 12);
        }

        public ActionResult DumpIntoHole(ItemId item, int amount)
        {
            if (!Near(DumpX, DumpY)) return Fail("先到投弃洞入口，再选择要丢弃的物品。");
            if (amount <= 0 || Carry.Available(item) < amount) return Fail("背包没有足够的未预约物品。");
            if(tools.Any(t=>t.Item==item&&t.Location=="carry"&&(t.UsingAt!=0||t.ReservedBy!=0)))return Fail("这件工具仍在当前批次使用，先结束或取消批次。");
            return QueueInteraction(() => {
                if (!Near(DumpX, DumpY) || Carry.Available(item) < amount) return Fail("物品或投弃位置已改变。");
                Carry.TryRemove(item, amount);
                foreach (var tool in tools.Where(t => t.Item == item && t.Location == "carry" && t.ReservedBy == 0).Take(amount).ToArray()) tools.Remove(tool);
                DumpedTicks += (long)amount * ItemCatalog.CapacityTicks(item);
                return Ok("物品已投入洞中，无法取回。", amount);
            });
        }
    }
}
