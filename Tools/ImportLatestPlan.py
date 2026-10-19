"""Import the supplied v1.1 plan without editing its source files."""
import argparse, hashlib, json, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def pairs(values):
    return [dict(item=k, quantity=v) for k,v in (values or {}).items()]

def main(source):
    data = json.loads((source/'数值配置/全局数值_v1.json').read_text(encoding='utf-8-sig'))
    snapshot = ROOT/'Docs/Source/S6_20261013'
    snapshot.mkdir(parents=True, exist_ok=True)
    for p in (source/'数值配置').glob('*.json'):
        shutil.copy2(p, snapshot/p.name)
    for name in ['喂——出来游戏策划案_合并版.md','全局数值与科技设计.md','食物与水资源策划与数值_v1_1.md']:
        shutil.copy2(source/'策划文档'/name, snapshot/name)
    runtime=dict(version=data['version'], items=[],facilities=[],recipes=[],technologies=[],sales=[],regions=[],rules=[])
    for key,v in data['items'].items():
        row=dict(v,id=key,canBuy=v.get('buy') is not None,canSell=v.get('sell_base') is not None,payload=pairs(v.get('payload')))
        if key=='soap': row['uses_per_item']=data['global']['life']['soap_units_per_bar']
        for k in ['buy','sell_base','sell_floor']: row[k]=v.get(k) or 0
        runtime['items'].append(row)
    for key,v in data['facilities'].items():
        runtime['facilities'].append(dict(v,id=key,build=pairs(v['build']),hasDurability=v.get('durability') is not None,durability=v.get('durability') or 0))
    for key,v in data['recipes'].items():
        stages=v.get('stages')
        if not stages:
            stages=[dict(kind=kind,seconds=v.get(field,0)) for kind,field in [('human','human_seconds'),('machine','machine_seconds'),('natural','natural_seconds')]]
        stages=[dict(s,commit_inputs=pairs(s.get('commit_inputs'))) for s in stages if s['seconds']>0]
        row=dict(v,id=key,inputs=pairs(v['inputs']),outputs=pairs(v['outputs']),tools=pairs(v.get('tools')),addons=pairs(v.get('addons')),stages=stages,composition_region2=pairs(v.get('composition_region2')))
        row['source_liters']=(v.get('source_reservation') or v.get('source_draw') or {}).get('liters',0)
        row['sourceAtStart']='source_draw' in v
        capture=v.get('optional_effluent_capture') or {}
        row['effluent_fraction']=capture.get('yield_water_fraction',0)
        row['capture_cost']=pairs(capture.get('addon'))
        runtime['recipes'].append(row)
    runtime['technologies']=[dict(v,id=k) for k,v in data['technologies'].items()]
    runtime['sales']=[dict(v,id=k) for k,v in data['technology_sales'].items()]
    for key,v in data['regions'].items():
        runtime['regions'].append(dict(v,id=int(key),fixed_items=pairs(v['fixed_items']),category_weights=pairs(v['category_weights']),metal_subweights=pairs(v['metal_subweights']),textile_subweights=pairs(v['textile_subweights']),plastic_parts_per_10=pairs(v['plastic_parts_per_10']),freshwater_source=v['freshwater_source']))
    def flatten(v,path=''):
        if isinstance(v,dict):
            for k,x in v.items(): flatten(x,path+'.'+k if path else k)
        elif isinstance(v,(int,float)) and not isinstance(v,bool): runtime['rules'].append(dict(key=path,number=v))
        elif isinstance(v,bool): runtime['rules'].append(dict(key=path,number=int(v),text=str(v)))
        else: runtime['rules'].append(dict(key=path,text=json.dumps(v,ensure_ascii=False)))
    flatten(data['global'])
    destination=ROOT/'Assets/_Game/Resources/Plan/RuntimeConfig.json'
    destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_text(json.dumps(runtime,ensure_ascii=False,indent=2),encoding='utf-8')
    summary={k:len(runtime[k]) for k in ['items','facilities','recipes','technologies','sales','regions']}
    assert summary==dict(items=201,facilities=53,recipes=216,technologies=54,sales=15,regions=3),summary
    print(json.dumps(dict(version=runtime['version'],counts=summary,sourceSHA256=hashlib.sha256((source/'数值配置/全局数值_v1.json').read_bytes()).hexdigest()),ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,required=True)
    main(parser.parse_args().source)
