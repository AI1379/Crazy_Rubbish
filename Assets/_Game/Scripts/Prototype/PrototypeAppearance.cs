using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeAppearance : ScriptableObject
    {
        public Font font;
        public Sprite worker, wasteWood, mixedPlastic, fastener, saw, plank, woodHandle, warehouse, workbench, tile;
        public Sprite ItemSprite(ItemId item)
        {
            switch (item)
            {
                case ItemId.WasteWood: return wasteWood;
                case ItemId.MixedPlastic: return mixedPlastic;
                case ItemId.RecoveredFastener: return fastener;
                case ItemId.WoodHandSaw: return saw;
                case ItemId.Plank: return plank;
                default: return woodHandle;
            }
        }
    }
}
