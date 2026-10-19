using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeSurfaceWater(string screenshot)
        {
            void Check(bool value,string message){if(!value)throw new InvalidOperationException("河岸与水塘："+message);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.State.cash=100000;Model.State.warehouse.capacity=1000;
            foreach(var item in Model.Config.items)Model.Discover(item.id);foreach(var a in Model.Config.Facility("water_intake").build)Model.State.warehouse.Add(a.item,4*a.Ticks);
            BuildPresentation();mapCamera.transform.position=new Vector3(20,23,-10);mapCamera.orthographicSize=8;RefreshPresentation(true);yield return null;
            Check(PlanTerrain.WaterAt(20,24)==2&&PlanTerrain.StarterPondBody>0,"出生地水塘缺失");
            Check(planWorld.GetComponentsInChildren<TextMesh>().Any(t=>t.text=="小水塘 · 岸边可取水"),"水塘缺少标识");
            var screen=new Vector2(Screen.width*.64f,Screen.height*.68f);OpenWorldContext(20,24,screen,false);
            Check(ContextOpen&&contextMenu.GetComponentsInChildren<Text>().Any(t=>t.text=="出生地小水塘"),"水面右键未显示取水菜单");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="建设：人工采水地点").onClick.Invoke();Check(buildType=="water_intake"&&!managementOpen,"水面菜单未进入临水建设");
            UpdatePreview(8,8);Check(preview.GetComponent<SpriteRenderer>().color.r>preview.GetComponent<SpriteRenderer>().color.g,"内陆错误显示为可建取水点");
            UpdatePreview(22,22);Check(preview.GetComponent<SpriteRenderer>().color.g>preview.GetComponent<SpriteRenderer>().color.r,"旧锚点岸边错误拒绝建设");CancelPlacement();
            var pond=Model.Build("water_intake",22,22);Model.SelectRecipe(pond,"collect_freshwater");var front=Model.Front(pond);Model.State.player.x=front.x;Model.State.player.y=front.y;Model.AssignHero(pond.id);Model.SetPaused(false);Model.Advance(35);Model.SetPaused(true);
            Check(pond.output.Count("raw_freshwater")==6*PlanStock.Scale,"水塘采水没有产出原水");RefreshPresentation(true);OpenWorldContext(pond.x,pond.y,screen,false,pond.id);Shot("-starter-pond");CloseContext();Model.AssignHero(pond.id);
            PlanSite bank=null;
            foreach(var cell in PlanTerrain.Cells(1))
            {
                if(cell.x<26&&cell.y<30)continue;var candidate=new PlanSite{type="water_intake",x=cell.x,y=cell.y};var f=Model.Front(candidate);
                if(!Model.CanPlaceWaterIntake(candidate)||!Model.Walkable(f.x,f.y)||Model.State.sites.Any(s=>Model.Covers(s,f.x,f.y)))continue;
                if(!Enumerable.Range(cell.x,2).All(x=>Enumerable.Range(cell.y,2).All(y=>Model.Owned(x,y)&&Model.TerrainBuildable(x,y)&&Model.GroundCapacity(x,y)==0&&!Model.Receiving(x,y)&&!Model.State.sites.Any(s=>Model.Covers(s,x,y)))))continue;
                if(Model.Path(Model.State.player.x,Model.State.player.y,f.x,f.y)==null)continue;bank=candidate;break;
            }
            Check(bank!=null,"开放地区没有可用河岸");var river=Model.Build(bank.type,bank.x,bank.y);Model.SelectRecipe(river,"collect_freshwater");front=Model.Front(river);Model.State.player.x=front.x;Model.State.player.y=front.y;Model.AssignHero(river.id);Model.SetPaused(false);Model.Advance(35);Model.SetPaused(true);
            Check(river.output.Count("raw_freshwater")==6*PlanStock.Scale&&river.output.Count("water")==0,"河岸产出未遵循原水与净化规则");
            Check(Model.WaterSource(river).kind=="surface"&&river.springBody!=pond.springBody,"河岸错误绑定出生地水源");
            var reloaded=new PlanSimulation(LoadConfig(),1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(Model.Capture())));
            Check(Math.Abs(reloaded.WaterSource(reloaded.Site(river.id)).remaining-Model.WaterSource(river).remaining)<.000001&&Math.Abs(reloaded.WaterSource(reloaded.Site(river.id)).reserved-Model.WaterSource(river).reserved)<.000001,"河岸预算在读档时改变");
            mapCamera.transform.position=new Vector3(river.x+2,river.y+2,-10);mapCamera.orthographicSize=10;RefreshPresentation(true);OpenWorldContext(river.x,river.y,screen,false,river.id);Shot("-river-intake");
            Model=saved;buildType=null;moveX=-1;selectedSite=0;BuildPresentation();Notice="河流与小水塘岸边均可建设取水设施；原水需净化后饮用。";RefreshPresentation(true);
        }
    }
}
