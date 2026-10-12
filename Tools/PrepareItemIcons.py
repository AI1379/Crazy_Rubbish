"""Draw item silhouettes in the prototype's existing 64px geometric sprite style."""
from pathlib import Path
import re
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
out = root / "Assets/_Game/Art/Prototype/Items"
out.mkdir(parents=True, exist_ok=True)
source = (root / "Assets/_Game/Scripts/Core/ItemId.cs").read_text(encoding="utf-8-sig")
items = re.search(r"public enum ItemId\s*\{([^}]+)\}", source).group(1).replace("\n", "").split(",")
existing = {"WasteWood", "MixedPlastic", "RecoveredFastener", "WoodHandSaw", "Plank", "WoodHandle", "WoodChips", "SortedWood"}

for name in map(str.strip, items):
    if name in existing:
        continue
    image = Image.new("RGBA", (64, 64))
    d = ImageDraw.Draw(image)
    ink = "#273d40"
    steel = "#a8bec1"
    copper = "#d29868"
    gold = "#dfbc76"
    d.ellipse((8, 46, 57, 58), fill=(13, 26, 27, 95))
    if name == "Food":
        d.rounded_rectangle((9, 19, 55, 48), 11, fill="#cda26b", outline=ink, width=3)
        for x in (19, 31, 43):
            d.line((x, 25, x - 4, 37), fill="#f0d4a0", width=3)
    elif name == "DrinkingWater":
        d.rounded_rectangle((20, 15, 45, 54), 6, fill="#69aabd", outline=ink, width=3)
        d.rectangle((25, 7, 40, 16), fill="#d1d9cc", outline=ink, width=2)
        d.rectangle((23, 31, 42, 41), fill="#d0e6df")
        d.line((27, 20, 27, 27), fill="#c5e6e4", width=3)
    elif name == "Soap":
        d.rounded_rectangle((10, 23, 54, 48), 8, fill="#a4c6a1", outline=ink, width=3)
        d.ellipse((23, 28, 41, 41), outline="#e3e5c1", width=3)
        d.ellipse((13, 10, 21, 18), fill="#d0e2d4")
    elif name in ("WasteTextile", "OldClothes"):
        if name == "OldClothes":
            d.polygon([(20, 14), (27, 20), (37, 20), (44, 14), (57, 27), (48, 37), (44, 31), (44, 53), (20, 53), (20, 31), (16, 37), (7, 27)], fill="#b58aab", outline=ink, width=3)
        else:
            for x, y, color in ((9, 17, "#977ba4"), (19, 27, "#bea0b5")):
                d.polygon([(x, y), (x + 34, y + 4), (x + 28, y + 24), (x - 3, y + 19)], fill=color, outline=ink, width=2)
            d.line((20, 37, 40, 40), fill="#e0c4c5", width=2)
    elif name in ("Screwdriver", "ScrewdriverRod", "Wrench", "Hammer", "HammerHead", "Scissors", "SewingKit", "BrokenWoodTool", "BroomHead"):
        if name in ("Screwdriver", "ScrewdriverRod"):
            d.line((19, 43, 45, 14), fill=steel, width=5)
            d.line((43, 15, 50, 10), fill=steel, width=3)
            if name == "Screwdriver":
                d.line((13, 51, 27, 36), fill=copper, width=12)
                d.line((15, 49, 25, 38), fill=gold, width=3)
        elif name == "Wrench":
            d.line((15, 49, 44, 20), fill=steel, width=10)
            d.ellipse((32, 7, 56, 30), fill=steel, outline=ink, width=2)
            d.polygon([(44, 5), (44, 17), (54, 17), (60, 9)], fill=(0, 0, 0, 0))
            d.ellipse((11, 43, 21, 53), fill=ink)
        elif name in ("Hammer", "HammerHead", "BrokenWoodTool"):
            if name != "HammerHead":
                d.line((18, 51, 38, 22), fill=copper, width=8)
            d.polygon([(24, 12), (35, 8), (52, 21), (45, 33), (36, 25), (24, 25)], fill=steel, outline=ink, width=2)
            if name == "BrokenWoodTool":
                d.line((14, 12, 51, 50), fill="#d77561", width=4)
        elif name == "Scissors":
            d.line((21, 40, 44, 9), fill=steel, width=5)
            d.line((41, 40, 20, 9), fill=steel, width=5)
            d.ellipse((8, 34, 28, 55), outline=copper, width=6)
            d.ellipse((35, 34, 55, 55), outline=copper, width=6)
        elif name == "BroomHead":
            d.polygon([(22, 17), (40, 17), (53, 51), (10, 51)], fill=gold, outline=ink, width=3)
            for x in (19, 27, 35, 43):
                d.line((31, 24, x, 48), fill=copper, width=2)
        else:
            d.rounded_rectangle((9, 17, 55, 51), 5, fill="#ae929b", outline=ink, width=3)
            d.line((20, 39, 41, 23), fill="#e8dbcd", width=3)
            d.ellipse((39, 21, 44, 26), outline=ink, width=2)
            d.rectangle((17, 24, 24, 41), fill=gold)
    elif name in ("RecoveredWire", "WireBundle"):
        for box in ((11, 11, 50, 50), (17, 17, 44, 44), (23, 23, 38, 38)):
            d.ellipse(box, outline=copper, width=5)
        d.line((45, 40, 55, 49), fill=gold, width=4)
        if name == "WireBundle":
            d.rectangle((10, 27, 51, 34), fill="#688d83", outline=ink, width=2)
    elif name in ("SteelPart", "CopperPart", "SteelMaterial", "CopperMaterial", "AluminumPart", "AluminumMaterial", "ToolSteelWaste", "Fixture", "FurnaceLining"):
        color = copper if "Copper" in name else "#ccd4cb" if "Aluminum" in name else steel
        for y in (17, 34):
            d.polygon([(9, y + 5), (43, y - 3), (56, y + 6), (48, y + 18), (12, y + 19)], fill=color, outline=ink, width=2)
            d.line((13, y + 8, 43, y + 1), fill="#e4ddd0", width=2)
        if "Part" in name or name == "Fixture":
            d.ellipse((23, 26, 40, 43), fill=ink, outline=color, width=3)
    elif name in ("SawBlade", "MachineBlade"):
        d.ellipse((9, 9, 55, 55), fill=steel, outline=ink, width=3)
        for x, y in ((9, 28), (29, 9), (49, 29), (29, 49)):
            d.rectangle((x - 3, y - 3, x + 6, y + 6), fill=steel)
        d.ellipse((26, 26, 38, 38), fill=ink)
    elif name in ("Motor", "DamagedMotor", "GeneratorModule"):
        d.rounded_rectangle((13, 16, 49, 47), 8, fill="#78979a", outline=ink, width=3)
        for x in (21, 28, 35, 42):
            d.line((x, 20, x, 42), fill=steel, width=2)
        d.rectangle((48, 28, 59, 35), fill=steel, outline=ink, width=2)
        if name == "DamagedMotor":
            d.line((17, 16, 42, 45), fill="#d77561", width=4)
    elif name in ("ThermalModule", "HeatingElement", "FurnaceModule", "OldFurnace"):
        d.rounded_rectangle((12, 12, 52, 53), 4, fill="#86755e", outline=ink, width=3)
        d.rectangle((19, 19, 45, 46), fill=ink)
        d.polygon([(26, 42), (22, 33), (30, 20), (33, 31), (40, 25), (42, 36), (36, 43)], fill="#e9af64")
    elif name in ("GrowingLight", "LightingModule", "OldGrowingLamp", "OldLamp"):
        d.line((32, 38, 32, 52), fill=steel, width=5)
        d.ellipse((19, 8, 45, 36), fill=gold, outline=ink, width=3)
        d.rectangle((23, 34, 41, 40), fill=steel, outline=ink, width=2)
        d.line((16, 52, 49, 52), fill=steel, width=4)
    elif name in ("Container", "OldContainer", "MouldBox", "HandleMould", "BoxMould"):
        d.polygon([(10, 18), (47, 14), (55, 26), (51, 52), (13, 53)], fill=copper, outline=ink, width=3)
        d.polygon([(10, 18), (47, 14), (55, 26), (17, 31)], fill=gold, outline=ink, width=2)
        d.line((17, 31, 13, 53), fill=ink, width=2)
        if "Mould" in name:
            d.rectangle((23, 35, 41, 45), fill=ink)
    elif name == "PowerAsh":
        d.polygon([(7, 48), (19, 30), (29, 36), (36, 25), (55, 48)], fill="#9ba7a0", outline=ink, width=3)
    else:
        # Distinct machine silhouettes: washer drum, cutter blade, feeder hopper,
        # control-panel lights and a moulding press rather than a workbench icon.
        d.rounded_rectangle((10, 13, 54, 53), 4, fill="#749193", outline=ink, width=3)
        if name == "OldWasher":
            d.ellipse((18, 24, 46, 49), fill=ink, outline=steel, width=4)
            d.rectangle((17, 17, 38, 21), fill=gold)
        elif name == "OldCutter":
            d.ellipse((24, 18, 49, 43), fill=steel, outline=ink, width=3)
            d.ellipse((33, 27, 40, 34), fill=ink)
            d.rectangle((7, 40, 57, 47), fill=copper, outline=ink, width=2)
        elif name in ("OldFeeder", "FeedingModule"):
            d.polygon([(8, 11), (56, 11), (40, 34), (24, 34)], fill=gold, outline=ink, width=3)
            d.rectangle((25, 32, 39, 46), fill=steel, outline=ink, width=2)
        elif name == "OldFormer":
            d.rectangle((17, 18, 23, 45), fill=steel)
            d.rectangle((41, 18, 47, 45), fill=steel)
            d.rectangle((20, 20, 44, 29), fill=gold, outline=ink, width=2)
            d.rectangle((23, 42, 42, 49), fill=copper)
        elif name == "OldMetalTable":
            d.rectangle((6, 17, 58, 30), fill=steel, outline=ink, width=3)
            d.rectangle((13, 30, 21, 53), fill=steel, outline=ink, width=2)
            d.rectangle((43, 30, 51, 53), fill=steel, outline=ink, width=2)
        else:
            d.rectangle((17, 19, 47, 35), fill=ink)
            for x, color in ((21, gold), (32, "#9ebd9d"), (43, copper)):
                d.ellipse((x - 3, 41, x + 3, 47), fill=color)
    image.save(out / f"{name}.png")
print(f"Prepared {len(list(out.glob('*.png')))} item icons")
