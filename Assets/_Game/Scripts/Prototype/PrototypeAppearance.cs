using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeAppearance : ScriptableObject
    {
        public Font font;
        public Sprite worker, wasteWood, mixedPlastic, fastener, saw, plank, woodHandle, warehouse, workbench, tile;
        public Sprite marker;
        public Sprite[] itemIcons;
        public Sprite housing, hole, streetlamp, woodwork, generator, dismantle;
        public Sprite wasteWoodLow, wasteWoodHigh, wasteMetalLow, wasteMetalHigh, wasteMixedLow, wasteMixedHigh;
        public CharacterSprites[] characters;

        [System.Serializable]
        public sealed class CharacterSprites
        {
            // Directions: up, down, left, right. Each action is a single supplied pose.
            public Sprite[] idle, carry, pickup, saw;
            public Sprite Get(int direction, int pose)
            {
                Sprite[] images = pose == 1 ? carry : pose == 2 ? pickup : pose == 3 ? saw : idle;
                return images != null && direction < images.Length && images[direction] != null ? images[direction] : idle[direction];
            }
        }

        public Sprite GroundSprite(ItemId item, bool high)
        {
            switch (item)
            {
                case ItemId.WasteWood: case ItemId.SortedWood: return high ? wasteWoodHigh : wasteWoodLow;
                case ItemId.SteelMaterial: case ItemId.CopperMaterial: case ItemId.ToolSteelWaste:
                    return high ? wasteMetalHigh : wasteMetalLow;
                case ItemId.MixedPlastic: case ItemId.PowerAsh: return high ? wasteMixedHigh : wasteMixedLow;
                default: return ItemSprite(item);
            }
        }
        public Sprite ItemSprite(ItemId item)
        {
            if (itemIcons != null && (int)item < itemIcons.Length && itemIcons[(int)item] != null) return itemIcons[(int)item];
            if(ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))return workbench;
            if(item==ItemId.SortedWood)return wasteWood;
            switch (item)
            {
                case ItemId.WasteWood: return wasteWood;
                case ItemId.MixedPlastic: return mixedPlastic;
                case ItemId.RecoveredFastener: return fastener;
                case ItemId.WoodHandSaw: return saw;
                case ItemId.Plank: return plank;
                case ItemId.WoodHandle: return woodHandle;
                case ItemId.WoodChips: return wasteWood;
                case ItemId.DamagedMotor: case ItemId.ThermalModule: case ItemId.GeneratorModule: return workbench;
                case ItemId.SteelPart: case ItemId.CopperPart: case ItemId.RecoveredWire:
                case ItemId.SteelMaterial: case ItemId.CopperMaterial: return fastener;
                default: return ItemCatalog.IsTool(item) ? saw : mixedPlastic;
            }
        }
    }
}
