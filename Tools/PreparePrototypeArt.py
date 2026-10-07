"""Generate the original geometric placeholder sprites used by the M1 prototype."""
from pathlib import Path
import random
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/_Game/Art/Prototype"
OUT.mkdir(parents=True, exist_ok=True)


def canvas():
    image = Image.new("RGBA", (64, 64))
    return image, ImageDraw.Draw(image)


def save(name, image):
    image.save(OUT / f"{name}.png")


image, d = canvas()
d.rectangle((0, 0, 63, 63), fill="#3a4b46")
d.rectangle((1, 1, 62, 62), outline="#43534b", width=1)
rng = random.Random(42)
for _ in range(18):
    x, y = rng.randrange(4, 60), rng.randrange(4, 60)
    d.rectangle((x, y, x + 1, y + 1), fill=rng.choice(["#3d4e47", "#354740", "#4a5649"]))
save("Tile", image)

image, d = canvas()
d.ellipse((10, 45, 54, 59), fill=(13, 26, 27, 110))
d.rounded_rectangle((20, 25, 44, 51), 7, fill="#426873", outline="#253b45", width=2)
d.rectangle((18, 31, 24, 43), fill="#cda779")
d.rectangle((41, 31, 47, 43), fill="#cda779")
d.rounded_rectangle((20, 47, 29, 56), 2, fill="#272e32")
d.rounded_rectangle((35, 47, 44, 56), 2, fill="#272e32")
d.ellipse((17, 7, 48, 34), fill="#d5b18d", outline="#293a3b", width=2)
d.pieslice((14, 2, 50, 35), 175, 360, fill="#df9c4c", outline="#654c30", width=2)
d.rounded_rectangle((14, 17, 50, 24), 3, fill="#ecc06a", outline="#856137", width=1)
d.rectangle((23, 8, 29, 17), fill="#f1cc83")
save("Worker", image)

image, d = canvas()
d.ellipse((4, 44, 60, 60), fill=(15, 25, 22, 120))
for points, color in [([(7, 22), (47, 10), (53, 25), (13, 38)], "#b18856"),
                       ([(6, 37), (44, 24), (52, 40), (15, 53)], "#d1a565"),
                       ([(24, 26), (55, 37), (50, 51), (19, 40)], "#9b764d")]:
    d.polygon(points, fill=color, outline="#4f4736")
d.line((13, 42, 45, 31), fill="#e2bd82", width=2)
d.line((15, 28, 44, 19), fill="#c9a66f", width=2)
save("WasteWood", image)

image, d = canvas()
d.ellipse((5, 44, 60, 59), fill=(15, 27, 30, 120))
d.polygon([(8, 26), (18, 14), (39, 17), (37, 35), (17, 41)], fill="#719aa5", outline="#314c55")
d.polygon([(27, 30), (46, 18), (57, 33), (47, 51), (24, 46)], fill="#507c88", outline="#263e4a")
d.rectangle((11, 32, 31, 47), fill="#c6bda2", outline="#625f50", width=2)
d.ellipse((33, 33, 54, 52), fill="#7facad", outline="#375659", width=2)
d.line((15, 20, 34, 22), fill="#99b8ba", width=3)
save("MixedPlastic", image)

image, d = canvas()
d.ellipse((8, 43, 57, 55), fill=(15, 25, 22, 120))
d.polygon([(7, 35), (40, 15), (49, 29), (16, 47)], fill="#b6c1b8", outline="#34494a")
for x, y in [(12, 35), (20, 30), (28, 25), (36, 20)]:
    d.polygon([(x, y), (x + 5, y + 3), (x + 1, y + 8)], fill="#cdd4c4")
d.polygon([(39, 12), (53, 13), (58, 26), (49, 32), (41, 26)], fill="#b67b46", outline="#583f2c")
d.polygon([(46, 18), (51, 18), (52, 24), (48, 27)], fill="#2d433f")
save("Saw", image)

image, d = canvas()
for x, y in [(15, 10), (37, 28), (14, 39)]:
    d.line((x + 7, y + 4, x + 1, y + 16), fill="#bbc6c0", width=5)
    d.rounded_rectangle((x, y, x + 12, y + 6), 2, fill="#8eaba9", outline="#345452")
save("Fastener", image)

image, d = canvas()
for y, color in [(17, "#d4ac73"), (32, "#b88c57")]:
    d.polygon([(7, y + 4), (49, y - 4), (58, y + 8), (15, y + 16)], fill=color, outline="#604e35")
    d.line((15, y + 7, 48, y), fill="#e0bc80", width=2)
save("Plank", image)

image, d = canvas()
for x, y in [(14, 12), (32, 25)]:
    d.rounded_rectangle((x, y, x + 12, y + 32), 5, fill="#cfa46e", outline="#5c4d37", width=2)
    d.line((x + 4, y + 6, x + 4, y + 24), fill="#e1bc88", width=2)
save("WoodHandle", image)

image, d = canvas()
d.rectangle((4, 12, 60, 54), fill="#586f61", outline="#293f3b", width=3)
d.rectangle((8, 16, 56, 50), fill="#6d8570", outline="#91a78b", width=2)
for x in [12, 28, 44]:
    d.rectangle((x, 20, x + 8, 46), fill="#768c74", outline="#536d5a")
d.rectangle((25, 22, 40, 44), fill="#263f38", outline="#a3b79b", width=2)
save("Warehouse", image)

image, d = canvas()
d.rectangle((11, 38, 18, 55), fill="#74593e")
d.rectangle((46, 38, 53, 55), fill="#74593e")
d.rectangle((6, 18, 58, 43), fill="#b48b58", outline="#544b36", width=3)
d.line((9, 29, 55, 29), fill="#cca36a", width=2)
d.polygon([(21, 26), (40, 23), (43, 28), (23, 32)], fill="#c9cebb", outline="#56675d")
d.rectangle((39, 23, 47, 28), fill="#765439")
save("Workbench", image)
print(f"Generated 10 original prototype sprites in {OUT}")
