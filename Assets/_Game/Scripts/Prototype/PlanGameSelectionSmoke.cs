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
        private IEnumerator SmokeSelectionInterface(string screenshot)
        {
            void Check(bool value,string message){if(!value)throw new InvalidOperationException("框选与拖图："+message);}
            Vector2 Point(float x,float y)=>mapCamera.WorldToScreenPoint(new Vector3(x,y,0));
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            void Click(int button,Vector2 point){BeginMapPointer(button,point);ReleaseMapPointer(button,point);}
            void Box(float x0,float y0,float x1,float y1){BeginMapPointer(1,Point(x0,y0));DragMapPointer(Point(x1,y1));ReleaseMapPointer(1,Point(x1,y1));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.State.recognized=true;Model.State.cash=100000;Model.Recruit();Model.Recruit();Model.State.paused=true;
            Model.State.player.x=10;Model.State.player.y=14;int offset=0;foreach(var p in Model.Alive){p.x=12+offset++*2;p.y=14;}
            var pile=new PlanGround{id=Model.State.nextId++,x=16,y=14};pile.stock.Add("waste_wood",2*PlanStock.Scale);pile.stock.Add("water",PlanStock.Scale);Model.State.ground.Add(pile);
            BuildPresentation();mapCamera.orthographicSize=12;mapCamera.transform.position=new Vector3(20,14,-10);RefreshPresentation(true);moveX=-1;
            yield return null;
            var start=Point(20,20);var cameraBefore=mapCamera.transform.position;int siteCount=Model.State.sites.Count;
            BeginMapPointer(0,start);DragMapPointer(start+new Vector2(-64,24));ReleaseMapPointer(0,start+new Vector2(-64,24));
            Check(Vector3.Distance(cameraBefore,mapCamera.transform.position)>.2f,"左拖没有平移镜头");Check(moveX==-1&&Model.State.sites.Count==siteCount,"拖图误触移动或建设");
            Click(0,Point(20,16));Check(moveX==20&&moveY==16,"左键短按失效");moveX=-1;
            buildType="wood";cameraBefore=mapCamera.transform.position;start=Point(22,20);BeginMapPointer(0,start);DragMapPointer(start+new Vector2(-32,8));ReleaseMapPointer(0,start+new Vector2(-32,8));
            Check(Model.State.sites.Count==siteCount&&buildType=="wood"&&Vector3.Distance(cameraBefore,mapCamera.transform.position)>.1f,"建设预览拖图误放置");CancelPlacement();
            BeginMapPointer(1,Point(8.5f,12.5f));DragMapPointer(Point(15,15.5f));Check(selectionBox!=null&&selectionBox.gameObject.activeSelf,"右拖缺少框选边框");Shot("-selection-box");ReleaseMapPointer(1,Point(15,15.5f));
            Check(selectedPeople.Count==3&&ContextOpen,"框选主角和居民失败");Check(peopleMarkers.Count==3,"人物选中标记缺失");Shot("-selected-people");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="右键地面移动（保持选中）").onClick.Invoke();Click(1,Point(18,16));
            Check(VisiblePeople.All(p=>p.directed),"右键没有指挥所选人物");Check(VisiblePeople.Select(p=>(p.commandX,p.commandY)).Distinct().Count()==3,"群体移动目标重叠");
            Model.SetPaused(false);for(int i=0;i<600&&!VisiblePeople.All(p=>Math.Abs(p.x-p.commandX)+Math.Abs(p.y-p.commandY)<.05);i++)Model.Advance(.1);Model.SetPaused(true);RefreshPresentation(true);
            Check(VisiblePeople.All(p=>Math.Abs(p.x-p.commandX)+Math.Abs(p.y-p.commandY)<.05),"人物未抵达指令目标");
            Box(15.4f,13.4f,16.6f,14.6f);Check(selectedPeople.Count==0&&boxedGround.SequenceEqual(new[]{pile.id})&&ContextOpen,"物品框选失败");
            Check(contextBody.GetComponentsInChildren<Button>().Count(b=>b.name.Contains("· 拾取"))==2,"物品菜单未展示所选堆的具体物品");Shot("-boxed-items");
            CloseContext();Box(15,13,20,18);Check(selectedPeople.Count==3&&boxedGround.Contains(pile.id),"混合框选遗漏目标");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("查看框内物品（")).onClick.Invoke();Check(selectedPeople.Count==0&&ContextOpen,"混合框选无法切换物品菜单");
            CloseContext();ClearMapSelection();var worker=Model.Alive.First();Click(1,Point((float)worker.x,(float)worker.y));Check(selectedPeople.SequenceEqual(new[]{worker.id})&&ContextOpen,"右键单选人物失败");
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="恢复工作").onClick.Invoke();Check(!worker.directed,"人物菜单不能恢复工作");
            worker.dead=true;RefreshSelectionMarkers();OpenPeopleContext(Point(20,18));Check(selectedPeople.Count==0&&!ContextOpen,"人物失效后遗留错误菜单");
            ClearMapSelection();var button=(RectTransform)planCanvas.Find("Operations/管理 Tab");var uiPoint=RectTransformUtility.WorldToScreenPoint(null,button.TransformPoint(button.rect.center));BeginMapPointer(0,uiPoint);Check(pointerButton==-1,"从按钮开始的拖动穿透到地图");
            BeginMapPointer(1,Point(21,18));DragMapPointer(Point(24,21));CancelMapGesture();Check(pointerButton==-1&&!selectionBox.gameObject.activeSelf,"取消后残留框选手势");
            Model=saved;moveX=-1;BuildPresentation();Notice="左键拖动地图；右键拖框选择。选中人物后右键地面移动，在人物菜单中恢复工作。";RefreshPresentation(true);
        }
    }
}
