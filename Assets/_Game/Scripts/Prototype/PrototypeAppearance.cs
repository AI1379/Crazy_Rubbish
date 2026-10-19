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
        public string localArtVersion;
        public Sprite generator, housing, hole, lamp, assemblyTable, dismantleTable, gameCover;
        public Sprite woodHeapHigh, metalHeap, metalHeapHigh, mixedHeapHigh;
        public Sprite[] bedrockTiles, gravelTiles, compactedTiles;
        public Sprite[] caveRocks;
        public Sprite cavePillar;
        public CharacterSprites male = new CharacterSprites(), female = new CharacterSprites();
        public CharacterSprites[] residents;
        public NamedPlanSprite[] planItems,planFacilities,planLogistics;
        public Sprite PlanSprite(string id,bool facility=false)
        {
            var bank=facility?planFacilities:planItems;
            if(bank!=null)foreach(var entry in bank)if(entry.id==id)return entry.sprite;
            return facility?workbench:mixedPlastic;
        }

        public Sprite GroundTile(int x, int y)
        {
            var tiles = x <= 7 && y >= 6 && y <= 11 ? gravelTiles
                : (x >= 10 && x <= 27 && y >= 6 && y <= 23) || (y >= 3 && y <= 5) ? compactedTiles : bedrockTiles;
            return tiles != null && tiles.Length == 16 ? tiles[(y % 4) * 4 + x % 4] : tile;
        }

        public CharacterSprites ResidentSprites(int id) => residents != null && residents.Length > 0
            ? residents[Mathf.Abs(id - 1) % residents.Length] : male;

        public Color ItemTint(ItemId item) => item == ItemId.PowerAsh ? new Color32(150, 150, 150, 255) : Color.white;

        public Sprite GroundSprite(ItemId item, bool high)
        {
            if (item == ItemId.WasteWood || item == ItemId.SortedWood) return high ? woodHeapHigh : wasteWood;
            if (item == ItemId.MixedPlastic) return high ? mixedHeapHigh : mixedPlastic;
            if (item == ItemId.ToolSteelWaste) return high ? metalHeapHigh : metalHeap;
            return ItemSprite(item);
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
    [System.Serializable] public sealed class NamedPlanSprite { public string id;public Sprite sprite; }

    // Each bank uses down, up, left, right. Poses are illustrations, not frame animations.
    [System.Serializable]
    public sealed class CharacterSprites
    {
        public Sprite[] idle, carry, pickup, saw;
        public Sprite Get(int facing, string pose = null)
        {
            Sprite[] bank = pose == "carry" ? carry : pose == "pickup" ? pickup : pose == "saw" ? saw : idle;
            if (bank != null && bank.Length > facing && bank[facing] != null) return bank[facing];
            return idle != null && idle.Length > facing ? idle[facing] : null;
        }
    }
}
