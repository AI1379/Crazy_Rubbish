"""Copy local game art byte-for-byte and describe non-destructive Unity sprite slices.

No raster editing, recolouring or resampling is performed. Run Unity's
CrazyRubbish.Editor.PrototypeProjectSetup.Setup after this command.
"""
import argparse
import csv
import hashlib
import json
import re
import shutil
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/_Game/Art/Local'
VERSION = 'local-art-2026-10-13-v1'


def rect(name, x, y, w, h, px=.5, py=.5):
    return dict(name=name, x=int(x), y=int(y), width=int(w), height=int(h), pivotX=px, pivotY=py)


def tight(image, name='default', foot=False):
    if image.mode != 'RGBA':
        return rect(name, 0, 0, image.width, image.height)
    yy, xx = np.where(np.array(image.getchannel('A')) >= 200)
    if not len(xx):
        raise ValueError('No opaque subject')
    x0, y0 = max(0, int(xx.min())-6), max(0, int(yy.min())-6)
    x1, y1 = min(image.width, int(xx.max())+7), min(image.height, int(yy.max())+7)
    return rect(name, x0, image.height-y1, x1-x0, y1-y0, .5, .035 if foot else .5)


def import_art(source):
    files=[]
    for path in sorted(source.rglob('*.png')):
        relative=path.relative_to(source)
        name=path.name
        if name.startswith('chr_'):
            group='Characters'
        elif name.startswith('bld_'):
            group='Buildings'
        elif name.startswith('env_'):
            group='Environment'
        elif name.startswith('atlas_'):
            group='Atlases'
        elif name.startswith('veh_'):
            group='Vehicles'
        elif '头图' in name:
            group='UI';name='GameCover.png'
        else:
            raise ValueError('Unmapped local image: '+str(relative))
        target=DEST/group/name
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists() and target.read_bytes()!=path.read_bytes():
            raise ValueError('Refusing to replace a different imported image: '+str(target))
        shutil.copy2(path,target)
        with Image.open(path) as image:
            char=name.startswith('chr_')
            sprites=[tight(image,foot=char)]
            if '_interactions' in name:
                metadata=json.loads((path.parent/'切片参考.json').read_text(encoding='utf-8-sig'))
                character=next(c for c in metadata['characters'] if c['file']==name)
                sprites=[rect(s['action']+'_'+s['direction'],s['unity_rect_bottom_left']['x'],s['unity_rect_bottom_left']['y'],s['unity_rect_bottom_left']['width'],s['unity_rect_bottom_left']['height'],px=.5,py=.06) for s in character['slices']]
            elif name.startswith('atlas_'):
                rows=list(csv.DictReader((path.parent/'图集切片参考.csv').open(encoding='utf-8-sig')))
                labels={
                    'atlas_items_core_v1.png':['waste_wood','boards','wood_chips','fasteners','steel_parts','wire','thermal_module','generator_module','handsaw','food','water','money'],
                    'atlas_items_extra_v1.png':['copper_parts','soap','waste_textile','old_clothes','hammer','screwdriver','wrench','scissors','sewing'],
                    'atlas_work_status_v1.png':['needs_materials','needs_tool','output_blocked','unreachable','paused','needs_fuel','needs_power'],
                    'atlas_logistics_v1.png':['straight','corner','merge','crossing','inlet','outlet'],
                    'atlas_power_lines_v1.png':['straight','corner','terminal','tee','cross']}
                selected=[r for r in rows if r['atlas']==name]
                assert len(selected)==len(labels[name])
                sprites=[rect(label,int(r['x']),int(r['unity_y_from_bottom']),int(r['width']),int(r['height'])) for label,r in zip(labels[name],selected)]
            elif name.startswith('env_ground') or name.startswith('env_cave_'):
                # Four-cell patches reuse the original texture without squeezing all detail into one cell.
                sprites=[]
                for y in range(4):
                    for x in range(4):
                        x0,x1=round(x*image.width/4),round((x+1)*image.width/4)
                        y0,y1=round(y*image.height/4),round((y+1)*image.height/4)
                        sprites.append(rect('tile_'+str(y*4+x),x0,y0,x1-x0,y1-y0))
            for s in sprites:
                assert s['x']>=0 and s['y']>=0 and s['x']+s['width']<=image.width and s['y']+s['height']<=image.height, (name,s)
            files.append(dict(path=target.relative_to(ROOT).as_posix(),source=relative.as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),width=image.width,height=image.height,sprites=sprites))
    assert len({f['path'] for f in files})==len(files),'Duplicate destination names'
    manifest=dict(version=VERSION,sourceRoot='桌面/喂——出来游戏素材',files=files)
    DEST.mkdir(parents=True,exist_ok=True)
    (DEST/'ImportManifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(png_count=len(files),sprite_count=sum(len(f['sprites']) for f in files),version=VERSION),ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    import_art(parser.parse_args().source.resolve(strict=True))
