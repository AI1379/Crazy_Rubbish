using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private readonly List<int> selectedPeople=new List<int>(),boxedPeople=new List<int>(),boxedGround=new List<int>(),boxedSites=new List<int>();
        private readonly Dictionary<int,SpriteRenderer> peopleMarkers=new Dictionary<int,SpriteRenderer>(),groundMarkers=new Dictionary<int,SpriteRenderer>();
        private RectTransform selectionBox;
        private int pointerButton=-1;private Vector2 pointerOrigin,pointerLast;private bool pointerDragged,pointerDismissOnly;
        private const float MapDragThreshold=8;

        private void ResetMapSelection()
        {CancelMapGesture();selectedPeople.Clear();boxedPeople.Clear();boxedGround.Clear();boxedSites.Clear();peopleMarkers.Clear();groundMarkers.Clear();selectionBox=null;}
        private void ClearMapSelection()
        {selectedPeople.Clear();boxedPeople.Clear();boxedGround.Clear();boxedSites.Clear();RefreshSelectionMarkers();}
        private void CancelMapGesture(){pointerButton=-1;pointerDragged=false;HideBeltRoute();if(selectionBox!=null)selectionBox.gameObject.SetActive(false);}
        private bool InMap(Vector2 point)=>mapCamera.pixelRect.Contains(point);
        private Vector2 ClampMapPoint(Vector2 point)
        {var r=mapCamera.pixelRect;return new Vector2(Mathf.Clamp(point.x,r.xMin,r.xMax),Mathf.Clamp(point.y,r.yMin,r.yMax));}
        private void BeginMapPointer(int button,Vector2 point,bool dismissOnly=false)
        {
            if(pointerButton>=0||MenuOpen||!InMap(point)||PointerOverInterface(point))return;
            pointerButton=button;pointerOrigin=pointerLast=point;pointerDragged=false;pointerDismissOnly=dismissOnly;
            if(button==0&&buildType=="belt"&&movingSite==0&&!dismissOnly)BeginBeltGesture(point);
            if(button==1)CloseContext();
        }
        private void DragMapPointer(Vector2 point)
        {
            if(pointerButton<0)return;
            if(!pointerDragged&&(point-pointerOrigin).sqrMagnitude>=MapDragThreshold*MapDragThreshold)
            {pointerDragged=true;if(pointerButton==0)CloseContext();}
            if(pointerDragged)
            {
                if(beltGesture){PreviewBeltDrag(point);pointerLast=point;return;}
                if(pointerButton==0)
                {
                    float units=2*mapCamera.orthographicSize/Mathf.Max(1,mapCamera.pixelRect.height);
                    var delta=point-pointerLast;mapCamera.transform.position-=new Vector3(delta.x*units,delta.y*units,0);ClampCaveCamera();
                }
                else DrawSelectionBox(pointerOrigin,ClampMapPoint(point));
            }
            pointerLast=point;
        }
        private void ReleaseMapPointer(int button,Vector2 point)
        {
            if(pointerButton!=button)return;
            DragMapPointer(point);bool dragged=pointerDragged,dismiss=pointerDismissOnly;Vector2 start=pointerOrigin;
            if(beltGesture)
            {
                var origin=beltStart;var p=mapCamera.ScreenToWorldPoint(point);int routeX=Mathf.RoundToInt(p.x),routeY=Mathf.RoundToInt(p.y);bool vertical=BeltVerticalFirst(routeX,routeY);CancelMapGesture();
                if(!MenuOpen&&!SaveDialogOpen&&InMap(point)&&!PointerOverInterface(point)&&!dismiss){if(dragged)CommitBeltDrag(origin,routeX,routeY,vertical);else MapLeftClick(routeX,routeY);}else HidePlacementPreview();return;
            }
            CancelMapGesture();
            if(MenuOpen)return;
            if(dragged){if(button==1)SelectMapRectangle(start,ClampMapPoint(point));return;}
            if(!InMap(point)||PointerOverInterface(point)||dismiss)return;
            var world=mapCamera.ScreenToWorldPoint(point);int x=Mathf.RoundToInt(world.x),y=Mathf.RoundToInt(world.y);
            if(button==0)MapLeftClick(x,y);
            else
            {
                var person=PeopleAt(world).FirstOrDefault();
                if(person!=null){ClearMapSelection();selectedPeople.Add(person.id);boxedPeople.Add(person.id);OpenPeopleContext(point);}
                else if(selectedPeople.Count>0)MoveSelectedPeople(x,y);
                else{ClearMapSelection();OpenWorldContext(x,y,point);}
            }
        }
        private IEnumerable<PlanPerson> VisiblePeople=>Model.Alive.Concat(new[]{Model.State.player}).Where(p=>!p.dead&&!(p.id==0&&Model.HeroAboard));
        private IEnumerable<PlanPerson> PeopleAt(Vector2 world)=>VisiblePeople.Where(p=>Vector2.Distance(world,new Vector2((float)p.x,(float)p.y))<=.7f).OrderBy(p=>Vector2.Distance(world,new Vector2((float)p.x,(float)p.y)));
        private void SelectMapRectangle(Vector2 start,Vector2 end)
        {
            ClearMapSelection();CloseContext();
            var screen=UnityEngine.Rect.MinMaxRect(Mathf.Min(start.x,end.x),Mathf.Min(start.y,end.y),Mathf.Max(start.x,end.x),Mathf.Max(start.y,end.y));
            bool Inside(float x,float y)=>screen.Contains(mapCamera.WorldToScreenPoint(new Vector3(x,y,0)));
            selectedPeople.AddRange(VisiblePeople.Where(p=>Inside((float)p.x,(float)p.y)).Select(p=>p.id));boxedPeople.AddRange(selectedPeople);
            boxedGround.AddRange(Model.State.ground.Where(g=>g.stock.stacks.Count>0&&Inside(g.x,g.y)).Select(g=>g.id));
            boxedSites.AddRange(Model.State.sites.Where(s=>{var z=Model.Size(s);return Inside(s.x+(z.width-1)/2f,s.y+(z.height-1)/2f);}).Select(s=>s.id));
            if(selectedPeople.Count>0)OpenPeopleContext(end);
            else if(boxedGround.Count>0)OpenBoxedGround(end);
            else if(boxedSites.Count>0)OpenBoxedSites(end);
            else Notice="框内没有人物、物品或设施。";
            RefreshSelectionMarkers();
        }
        private void MoveSelectedPeople(int x,int y)
        {
            selectedPeople.RemoveAll(id=>Model.Person(id)==null);CloseContext();if(selectedPeople.Contains(0))moveX=-1;
            try{Model.CommandMove(selectedPeople.ToArray(),x,y);Notice="已下达移动指令；到达后待命，在人物菜单中恢复工作。";}
            catch(Exception e){Notice=e.Message;}
            RefreshPresentation();
        }
        private void OpenPeopleContext(Vector2 screen)
        {
            selectedPeople.RemoveAll(id=>Model.Person(id)==null);if(selectedPeople.Count==0){CloseContext();return;}
            BeginContext(selectedPeople.Count==1?(selectedPeople[0]==0?"主角":"居民 #"+selectedPeople[0]):"已选中 "+selectedPeople.Count+" 名人物",screen);
            ContextLine("右键可达地面，指挥选中人物移动。\n到达后待命；居民原任务暂时挂起。",52);
            AddPeopleReadout();
            if(selectedPeople.Count>12)ContextLine("另有 "+(selectedPeople.Count-12)+" 人一同选中。",28);
            ContextAction("右键地面移动（保持选中）",()=>Notice="右键可达地面下达移动指令；右键拖动可重新框选。");
            int[] ids=selectedPeople.ToArray();ContextAction("恢复工作",()=>Act(()=>Model.ResumePeople(ids)));
            ContextAction("居民与工资管理",()=>ShowManagement(4));
            SelectionCategoryActions(screen,false);ContextAction("取消选择",ClearMapSelection);EndContext();RefreshSelectionMarkers();
        }
        private void SelectionCategoryActions(Vector2 screen,bool peopleOption)
        {
            if(peopleOption&&boxedPeople.Count>0)ContextAction("查看框内人物（"+boxedPeople.Count+"人）",()=>{selectedPeople.Clear();selectedPeople.AddRange(boxedPeople);OpenPeopleContext(ContextScreenAnchor(screen));});
            if(boxedGround.Count>0)ContextAction("查看框内物品（"+boxedGround.Count+"堆）",()=>OpenBoxedGround(ContextScreenAnchor(screen)));
            if(boxedSites.Count>0)ContextAction("查看框内设施（"+boxedSites.Count+"座）",()=>OpenBoxedSites(ContextScreenAnchor(screen)));
        }
        private void OpenBoxedGround(Vector2 screen)
        {selectedPeople.Clear();OpenGroundContext(Model.State.ground.Where(g=>boxedGround.Contains(g.id)&&g.stock.stacks.Count>0).ToArray(),screen,true);RefreshSelectionMarkers();}
        private void OpenBoxedSites(Vector2 screen)
        {
            selectedPeople.Clear();BeginContext("框内设施",screen);
            foreach(int id in boxedSites.ToArray()){var s=Model.Site(id);if(s==null)continue;ContextAction(Model.Config.Facility(s.type).name+" #"+id,()=>OpenWorldContext(s.x,s.y,ContextScreenAnchor(screen),false,id));}
            if(boxedPeople.Count>0)ContextAction("查看框内人物",()=>{selectedPeople.AddRange(boxedPeople);OpenPeopleContext(ContextScreenAnchor(screen));});
            if(boxedGround.Count>0)ContextAction("查看框内物品",()=>OpenBoxedGround(ContextScreenAnchor(screen)));
            EndContext();RefreshSelectionMarkers();
        }
        private void OpenGroundContext(PlanGround[] ground,Vector2 screen,bool multiple=false)
        {
            BeginContext(multiple?"选中物品 · "+ground.Length+"堆":"地面物品 / 垃圾堆",screen);
            ContextLine("先走近再拾取；每次最多拾取1件。\n拾取需要时间，并受背包容量限制。",50);
            int[] ids=ground.Select(g=>g.id).ToArray();
            ContextAction("走近这堆物品",()=>{var p=Model.State.player;var g=Model.State.ground.Where(a=>ids.Contains(a.id)&&a.stock.stacks.Count>0).OrderBy(a=>Math.Abs(a.x-p.x)+Math.Abs(a.y-p.y)).FirstOrDefault();if(g==null){Notice="物品已被取走。";return;}SetHeroDestination(g.x,g.y);Notice="到达后右键选择物品，或按 E 拾取。";});
            foreach(var group in ground.SelectMany(g=>g.stock.stacks).GroupBy(s=>s.item))
            {
                string item=group.Key;long total=group.Sum(s=>s.ticks);
                ContextAction(Model.Config.Item(item).name+" ×"+(total/(double)PlanStock.Scale).ToString("0.##")+" · 拾取",()=>Act(()=>
                {
                    var p=Model.State.player;var target=Model.State.ground.Where(a=>ids.Contains(a.id)&&a.stock.Count(item)>0).OrderBy(a=>Math.Abs(a.x-p.x)+Math.Abs(a.y-p.y)).FirstOrDefault();
                    if(target==null)throw new InvalidOperationException("物品已被取走。");Model.Pick(target.id,item,Math.Min(PlanStock.Scale,target.stock.Count(item)));
                }));
            }
            if(multiple)
            {
                if(boxedPeople.Count>0)ContextAction("查看框内人物",()=>{selectedPeople.AddRange(boxedPeople);OpenPeopleContext(ContextScreenAnchor(screen));});
                if(boxedSites.Count>0)ContextAction("查看框内设施",()=>OpenBoxedSites(ContextScreenAnchor(screen)));
                ContextAction("取消选择",ClearMapSelection);
            }
            EndContext();
        }
        private void SetHeroDestination(int x,int y)
        {Model.ResumePeople(new[]{0});moveX=x;moveY=y;}
        private void DrawSelectionBox(Vector2 start,Vector2 end)
        {
            if(selectionBox==null)
            {
                selectionBox=Rect(planCanvas,"Right drag selection",0,0,1,1);var fill=selectionBox.gameObject.AddComponent<Image>();fill.color=new Color(.4f,.85f,.65f,.12f);fill.raycastTarget=false;
                foreach(string side in new[]{"Top","Bottom","Left","Right"}){var line=Rect(selectionBox,side,0,0,1,1);var image=line.gameObject.AddComponent<Image>();image.color=new Color(.5f,1,.75f,.9f);image.raycastTarget=false;}
            }
            var canvas=(RectTransform)planCanvas;RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,start,null,out var a);RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,end,null,out var b);
            float w=Mathf.Abs(a.x-b.x),h=Mathf.Abs(a.y-b.y);selectionBox.anchoredPosition=new Vector2(Mathf.Min(a.x,b.x)-canvas.rect.xMin,Mathf.Max(a.y,b.y)-canvas.rect.yMax);selectionBox.sizeDelta=new Vector2(w,h);
            void Edge(string name,float x,float y,float width,float height){var r=(RectTransform)selectionBox.Find(name);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
            Edge("Top",0,0,w,2);Edge("Bottom",0,h-2,w,2);Edge("Left",0,0,2,h);Edge("Right",w-2,0,2,h);selectionBox.SetAsLastSibling();selectionBox.gameObject.SetActive(true);
        }
        private void RefreshSelectionMarkers()
        {
            if(planWorld==null||Model==null)return;
            selectedPeople.RemoveAll(id=>Model.Person(id)==null);
            var piles=boxedGround.Count>0?Model.State.ground.Where(g=>g.stock.stacks.Count>0).ToDictionary(g=>g.id):null;
            if(piles!=null)boxedGround.RemoveAll(id=>!piles.ContainsKey(id));
            foreach(int id in peopleMarkers.Keys.Where(id=>!selectedPeople.Contains(id)).ToArray()){Remove(peopleMarkers[id].gameObject);peopleMarkers.Remove(id);}
            foreach(int id in groundMarkers.Keys.Where(id=>!boxedGround.Contains(id)).ToArray()){Remove(groundMarkers[id].gameObject);groundMarkers.Remove(id);}
            foreach(int id in selectedPeople){var p=Model.Person(id);if(!peopleMarkers.TryGetValue(id,out var marker))peopleMarkers[id]=marker=Draw("Selected person "+id,appearance.marker,planWorld,(float)p.x,(float)p.y,.9f,38);marker.color=new Color(.35f,1,.65f,.9f);marker.transform.position=new Vector3((float)p.x,(float)p.y-.3f,0);}
            foreach(int id in boxedGround){var g=piles[id];if(!groundMarkers.ContainsKey(id)){var marker=Draw("Selected ground "+id,appearance.marker,planWorld,g.x,g.y,1.05f,4);marker.color=Accent;groundMarkers[id]=marker;}}
        }
    }
}
