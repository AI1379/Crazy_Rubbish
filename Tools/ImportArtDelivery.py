"""Copy the October 13 art delivery and prepare Unity's import manifest.

Requires Pillow. PNGs and supporting documents are preserved byte-for-byte.
Existing different files are rejected; Unity owns asset and folder .meta files.
"""
import argparse
import csv
import hashlib
import io
import json
import statistics
import uuid
import zipfile
from collections import defaultdict
from pathlib import Path, PurePosixPath

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/_Game/Art/Delivery_20261013"
SOURCE = ROOT / "Docs/Source/ArtDelivery_2026-10-13"
CHARACTER_HEIGHT = 1.4


def copy_original(path, data):
    if path.exists() and path.read_bytes() != data:
        raise ValueError(f"Refusing to replace an existing different file: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        path.write_bytes(data)


def read_json(payload, name):
    return json.loads(payload[name].decode("utf-8-sig"))


def foreground(image):
    if image.mode != "RGBA":
        return (0, 0, image.width, image.height)
    return image.getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()


def ground_pivot(image, box):
    """Initial ground anchor from the bottom foreground band, for later visual tuning."""
    left, top, right, bottom = box
    if image.mode == "RGBA":
        band = image.getchannel("A").crop((left, max(top, bottom - 12), right, bottom))
        band_box = band.point(lambda value: 255 if value >= 128 else 0).getbbox()
        if band_box:
            left, right = left + band_box[0], left + band_box[2]
    return ((left + right) / (2 * image.width), (image.height - bottom) / image.height)


def sprite(name, rect, pivot, source_path):
    x, y, width, height = rect
    return {
        "name": name, "x": x, "y": y, "width": width, "height": height,
        "pivotX": pivot[0], "pivotY": pivot[1],
        "spriteId": uuid.uuid5(uuid.NAMESPACE_URL, "CrazyRubbish/Delivery_20261013/" + source_path + "/" + name).hex,
    }


def main(archive):
    with zipfile.ZipFile(archive) as package:
        files = [entry for entry in package.infolist() if not entry.is_dir()]
        roots = {PurePosixPath(entry.filename).parts[0] for entry in files}
        if len(roots) != 1:
            raise ValueError("Expected a single delivery root")
        payload = {}
        for entry in files:
            parts = PurePosixPath(entry.filename).parts
            if any(part in ("..", ".") or ":" in part or "\\" in part for part in parts):
                raise ValueError(f"Unsafe archive path: {entry.filename}")
            rel = PurePosixPath(*parts[1:]).as_posix()
            if not rel or rel.startswith("/") or rel in payload:
                raise ValueError(f"Invalid or duplicate archive path: {entry.filename}")
            if not rel.endswith(".zip"):
                payload[rel] = package.read(entry)

    images = {}
    for rel, data in payload.items():
        if rel.endswith(".png"):
            image = Image.open(io.BytesIO(data))
            image.load()
            if image.format != "PNG" or foreground(image) is None:
                raise ValueError(f"Invalid or empty PNG: {rel}")
            images[rel] = image
    if len(images) != 71:
        raise ValueError(f"Expected 71 PNGs, received {len(images)}")

    slices = defaultdict(list)
    rows = csv.DictReader(io.StringIO(payload["人物/交互姿势/切片参考.csv"].decode("utf-8-sig")))
    for row in rows:
        rel = "人物/交互姿势/" + row["图集"]
        slices[rel].append((row["精灵名称"], tuple(int(row[key]) for key in ("PNG左上X", "PNG左上Y", "宽", "高"))))
    rows = csv.DictReader(io.StringIO(payload["补充贴图_20261012/图集切片参考.csv"].decode("utf-8-sig")))
    for row in rows:
        rel = "补充贴图_20261012/" + row["atlas"]
        slices[rel].append((row["name"], tuple(int(row[key]) for key in ("x", "y_from_top", "width", "height"))))
    if sum(map(len, slices.values())) != 111:
        raise ValueError("Expected 111 atlas slices")

    footprints = {}
    for item in read_json(payload, "机器/生产设备/素材信息.json"):
        footprints[item["filename"]] = item["footprint"]
    for item in read_json(payload, "场景/首区基础/素材元数据.json")["assets"]:
        footprints[item["file"]] = item["logical_footprint"]
    footprints.update(bld_survey_idle_v1=[2, 2], bld_mine_idle_v1=[3, 3], veh_return_lift_idle_v1=[3, 2])

    textures = []
    for rel, image in sorted(images.items()):
        stem = PurePosixPath(rel).stem
        box = foreground(image)
        character = rel.startswith("人物/")
        pivot = ground_pivot(image, box)
        if stem.startswith("env_waste_"):
            # Both pile heights share a horizontal ground center even when a bottom
            # stick or scrap projects farther to one side than the rest of the heap.
            pivot = ((box[0] + box[2]) / (2 * image.width), (image.height - box[3]) / image.height)
        footprint = footprints.get(PurePosixPath(rel).name, footprints.get(stem, [0, 0]))
        ppu = (box[3] - box[1]) / CHARACTER_HEIGHT if character else 64.0
        wrap_repeat = stem == "env_ground_plain_v1"
        if wrap_repeat:
            ppu = float(image.width)
            pivot = (0.5, 0.5)
        elif rel.startswith("宣传/"):
            ppu = 100.0
            pivot = (0.5, 0.5)
        elif footprint[0]:
            ppu = (box[2] - box[0]) / footprint[0]
            if stem == "env_hole_east":
                pivot = ((box[0] + box[2]) / (2 * image.width), 1 - (box[1] + box[3]) / (2 * image.height))

        parts = []
        if rel in slices:
            if character:
                ppu = statistics.median(height - 12 for name, (_, _, _, height) in slices[rel] if "_carry_" in name) / CHARACTER_HEIGHT
            else:
                ppu = float(max(max(width, height) for _, (_, _, width, height) in slices[rel]))
            for name, (x, top, width, height) in slices[rel]:
                if x < 0 or top < 0 or width <= 0 or height <= 0 or x + width > image.width or top + height > image.height:
                    raise ValueError(f"Invalid slice: {rel} / {name}")
                crop = image.crop((x, top, x + width, top + height))
                slice_pivot = ground_pivot(crop, foreground(crop)) if character else (0.5, 0.5)
                parts.append(sprite(name, (x, image.height - top - height, width, height), slice_pivot, rel))
        else:
            parts.append(sprite(stem, (0, 0, image.width, image.height), pivot, rel))
        if len({part["name"] for part in parts}) != len(parts):
            raise ValueError(f"Duplicate sprite names: {rel}")
        textures.append({
            "path": (ART.relative_to(ROOT) / rel).as_posix(), "sourcePath": rel,
            "sha256": hashlib.sha256(payload[rel]).hexdigest(),
            "width": image.width, "height": image.height, "hasAlpha": image.mode == "RGBA",
            "multiple": rel in slices, "repeat": wrap_repeat, "pixelsPerUnit": round(ppu, 6),
            "footprintWidth": footprint[0], "footprintHeight": footprint[1], "sprites": parts,
        })

    for rel, data in payload.items():
        copy_original((ART if rel.endswith(".png") else SOURCE) / rel, data)
    manifest = {
        "deliveryDate": "2026-10-13", "archiveName": archive.name,
        "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
        "sourceDirectory": SOURCE.relative_to(ROOT).as_posix(),
        "textureCount": len(textures), "spriteCount": sum(len(texture["sprites"]) for texture in textures),
        "atlasSliceCount": 111, "characterHeightUnits": CHARACTER_HEIGHT,
        "anchorStatus": "Initial foreground-based anchors; tune contact points in the actual scene.",
        "textures": textures,
    }
    (ART / "ImportManifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Copied {len(images)} original PNGs and {len(payload) - len(images)} source files.")
    print(f"Prepared {manifest['spriteCount']} sprites, including {manifest['atlasSliceCount']} atlas slices.")
    print(f"Unity manifest: {ART / 'ImportManifest.json'}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    main(parser.parse_args().archive)
