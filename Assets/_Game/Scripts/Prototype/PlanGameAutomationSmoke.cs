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
        private IEnumerator SmokeAutomation(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("自动化操作："+message);}
            Vector2 Point(int x,int y)=>mapCamera.WorldToScreenPoint(new Vector3(x,y,0));
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.State.ground.Clear();Model.State.warehouse.capacity=10000;
            foreach(var item in Model.Config.items)Model.Discover(item.id);foreach(string item in new[]{"boards","steel_parts","waste_wood","fasteners"})Model.State.warehouse.Add(item,100*PlanStock.Scale);
            MenuOpen=false;session=true;Notice="拖拽铺带，沿箭头输送；接口连线显示绑定机器。";BuildPresentation();mapCamera.transform.position=new Vector3(14,14,-10);mapCamera.orthographicSize=12;buildType="belt";buildRotation=0;beltCrossing=false;
            var cameraBefore=mapCamera.transform.position;string before=JsonUtility.ToJson(Model.Capture());BeginMapPointer(0,Point(8,8));DragMapPointer(Point(12,10));
            Check(beltGesture&&beltRoute!=null&&beltRoute.Valid&&beltRoute.cells.Length==7,"拖拽没有生成完整路线");Check(mapCamera.transform.position==cameraBefore&&JsonUtility.ToJson(Model.Capture())==before,"预览移动地图或消耗物品");yield return null;Shot("-belt-route");
            ReleaseMapPointer(0,Point(12,10));Check(!beltGesture&&Model.State.sites.Count(s=>s.type=="belt")==7,"松手没有建设整段路线");Check(Model.Site(selectedSite).direction==1,"转弯方向不正确");
            before=JsonUtility.ToJson(Model.Capture());BeginMapPointer(0,Point(8,8));DragMapPointer(Point(12,10));ReleaseMapPointer(0,Point(12,10));Check(JsonUtility.ToJson(Model.Capture())==before,"重复铺带重复扣费");
            BeginMapPointer(0,Point(8,12));DragMapPointer(Point(12,12));CancelPlacement();ReleaseMapPointer(0,Point(12,12));Check(!beltGesture&&JsonUtility.ToJson(Model.Capture())==before,"取消铺带仍修改地图");
            buildType="belt";BeginMapPointer(0,Point(8,12));DragMapPointer(Point(12,12));var button=(RectTransform)planCanvas.Find("Operations/管理 Tab");var over=RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center));ReleaseMapPointer(0,over);Check(JsonUtility.ToJson(Model.Capture())==before,"在界面上松手仍建设");
            BeginMapPointer(0,Point(8,12));DragMapPointer(Point(12,12));ReleaseMapPointer(0,new Vector2(Screen.width+20,Screen.height+20));Check(JsonUtility.ToJson(Model.Capture())==before,"地图外松手仍建设");CancelPlacement();
            var machine=Model.Build("wood",15,10);StartPortPlacement("output");buildRotation=0;UpdatePreview(16,10);
            Check(portPreviewLink!=null&&portPreviewLink.gameObject.activeSelf&&PortPreviewProblem(16,10)==null&&placementHint.text.Contains("自动绑定"),"接口没有绑定预览");yield return null;Shot("-port-connection");MapLeftClick(16,10);
            var port=Model.Site(selectedSite);Check(port.type=="port"&&port.portSite==machine.id&&port.portMode=="output","新接口未自动绑定机器");
            StartPortPlacement("input");UpdatePreview(15,10);MapLeftClick(15,10);Check(Model.Site(selectedSite).portMode=="input"&&Model.Site(selectedSite).portSite==machine.id,"输入接口未自动绑定");CancelPlacement();
            var line=Model.BuildBeltRoute(17,10,20,10);foreach(var s in Model.State.sites){s.pending=false;if(s.type=="belt")s.powerRatio=1;}
            port.input.Add("boards",PlanStock.Scale);line[0].input.Add("boards",PlanStock.Scale);selectedSite=port.id;RefreshPresentation(true);
            Check(logisticsArrows.ContainsKey(line[0].id)&&logisticsArrows[line[0].id].gameObject.activeSelf&&portLinks[port.id].gameObject.activeSelf,"方向箭头或绑定线缺失");
            OpenWorldContext(port.x,port.y,new Vector2(Screen.width*.6f,Screen.height*.75f),false,port.id);Check(contextLiveText.text.Contains("下游缓存已满"),"缓存堵塞没有具体原因");yield return null;Shot("-flow-blocked");
            Model.SetSiteEnabled(line[0],false);RefreshInterface();Check(contextLiveText.text.Contains("下游已停止"),"下游停工没有更新原因");Model.SetSiteEnabled(line[0],true);CloseContext();
            machine.output.Add("boards",3*PlanStock.Scale);machine.output.Reserve("boards",PlanStock.Scale);var warehouse=Model.State.sites.First(s=>s.type=="warehouse");var f=Model.Front(machine);
            var person=new PlanPerson{id=Model.State.nextId++,x=f.x,y=f.y,trip=new PlanTrip{source="output:"+machine.id,target="input:"+warehouse.id,item="boards",ticks=PlanStock.Scale}};Model.State.people.Add(person);
            ClearMapSelection();selectedPeople.Add(person.id);OpenPeopleContext(new Vector2(Screen.width*.55f,Screen.height*.78f));Check(contextPeopleText!=null&&contextPeopleText.text.Contains("尚未取走"),"人物没有显示预约和运输任务");
            var window=contextMenu;contextMenu.anchoredPosition=new Vector2(300,-200);var position=contextMenu.anchoredPosition;var scroll=contextBody.anchoredPosition;
            person.cargo.Put(machine.output.Take("boards",PlanStock.Scale,true));person.trip.picked=true;RefreshInterface();
            Check(contextMenu==window&&contextMenu.anchoredPosition==position&&contextBody.anchoredPosition==scroll&&contextPeopleText.text.Contains("已携带"),"运输信息刷新丢失窗口或取送货阶段");yield return null;Shot("-transport-tasks");
            before=JsonUtility.ToJson(Model.Capture());RefreshInterface();RefreshLogisticsVisuals();Check(JsonUtility.ToJson(Model.Capture())==before,"物流提示改变任务或物品台账");
            person.directed=true;person.commandX=(int)person.x;person.commandY=(int)person.y;RefreshInterface();Check(contextPeopleText.text.Contains("待命")&&contextPeopleText.text.Contains("原工作挂起"),"手动待命没有说明挂起任务");
            showLogistics=false;RefreshLogisticsVisuals();Check(logisticsArrows.Values.All(a=>!a.gameObject.activeSelf)&&portLinks.Values.All(a=>!a.gameObject.activeSelf),"关闭物流显示仍残留箭头");showLogistics=true;
            Model=saved;buildType=null;MenuOpen=false;BuildPresentation();RefreshPresentation(true);
        }
    }
}
