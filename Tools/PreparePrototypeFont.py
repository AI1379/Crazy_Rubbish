"""Create a portable static Chinese font subset; requires fontTools.

Input is Google's Noto Sans SC variable font, kept outside Assets in Artifacts/Fonts.
The bundled derivative uses its own family name and preserves the adjacent OFL license.
Rerun after introducing new UI characters.
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root = Path(__file__).resolve().parents[1]
original = root / "Artifacts/Fonts/NotoSansSC-variable.ttf"
out = root / "Assets/_Game/Art/Fonts/RubbishPrototypeSans.ttf"
if not original.exists():
    raise SystemExit("Download the upstream font as described in Docs/Development/03_M1原型说明.md.")
characters = set(chr(value) for value in range(32, 127))
characters.update("×→·％，。；：！？（）【】—“”‘’/\n")
for folder in [root / "Assets/_Game/Scripts", root / "Docs", root / "README.md"]:
    files = [folder] if folder.is_file() else folder.rglob("*")
    for path in files:
        if path.suffix in {".cs", ".md"}:
            characters.update(path.read_text(encoding="utf-8-sig"))
static_cache = original.with_name("NotoSansSC-Regular-full.ttf")
if static_cache.exists():
    font = TTFont(static_cache)
else:
    font = instantiateVariableFont(TTFont(original), {"wght": 400}, inplace=True)
    font.save(static_cache)
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]
options.name_legacy = True
options.name_languages = [0x409]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes={ord(character) for character in characters})
subsetter.subset(font)
for record in font["name"].names:
    names = {1: "Rubbish Prototype Sans", 2: "Regular", 3: "RubbishPrototypeSans-Regular-M1",
             4: "Rubbish Prototype Sans Regular", 6: "RubbishPrototypeSans-Regular",
             16: "Rubbish Prototype Sans", 17: "Regular"}
    if record.nameID in names:
        record.string = names[record.nameID].encode(record.getEncoding())
out.parent.mkdir(parents=True, exist_ok=True)
font.save(out)
print(f"Created {out.name}: {out.stat().st_size:,} bytes, {len(font.getBestCmap())} mapped characters")
