using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeAppearance : ScriptableObject
    {
        public Font font;
        public Sprite worker, wasteWood, mixedPlastic, fastener, saw, plank, woodHandle, warehouse, workbench, tile;
        public Sprite marker;
        public Sprite ItemSprite(ItemId item)
        {
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
