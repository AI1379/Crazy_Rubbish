using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeMenuDragging(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("菜单拖动："+message);}
            Vector2 Centre(RectTransform r)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            void Fits(){var corners=new Vector3[4];contextMenu.GetWorldCorners(corners);Check(corners.All(p=>p.x>=0&&p.y>=0&&p.x<=Screen.width+.1f&&p.y<=Screen.height+.1f),"窗口移出屏幕");}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var site=Model.State.sites.First(s=>s.type=="warehouse");MenuOpen=false;CloseSettings();HideManagement();ClearMapSelection();
            boxedSites.AddRange(Model.State.sites.Select(s=>s.id));
            var screen=new Vector2(Screen.width*.6f,Screen.height*.72f);OpenBoxedSites(screen);yield return null;
            Check(contextMenu.Find("Title").GetComponent<Text>().text=="框内设施","未打开框内设施列表");
            var handle=contextMenu.Find("Drag title").GetComponent<PlanMenuDragHandle>();Check(handle!=null,"标题缺少拖动手柄");
            var start=Centre((RectTransform)handle.transform);var cameraBefore=mapCamera.transform.position;
            BeginMapPointer(0,start);Check(pointerButton<0,"标题按下开始拖动地图");
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=start},hits);
            Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject)==handle.gameObject,"标题拖动事件未命中手柄");
            var contentButton=contextBody.GetComponentInChildren<Button>();Check(ExecuteEvents.GetEventHandler<IDragHandler>(contentButton.gameObject)==contextMenu.gameObject,"内容拖动不再交给滚动列表");
            Check(!contextMenu.Find("×").IsChildOf(handle.transform),"关闭按钮被拖动手柄覆盖");
            var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=start,pressPosition=start};
            ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.initializePotentialDrag);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.beginDragHandler);
            var before=contextMenu.anchoredPosition;e.position=start+new Vector2(-Screen.width*.16f,Screen.height*.08f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)planCanvas,start,null,out var a);RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)planCanvas,e.position,null,out var b);
            ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.endDragHandler);
            Check(Vector2.Distance(contextMenu.anchoredPosition,before+b-a)<1,"拖动距离未正确适配画布缩放");Fits();
            Check(mapCamera.transform.position==cameraBefore&&pointerButton<0&&ContextOpen,"拖动菜单改变了地图或关闭菜单");
            var moved=contextMenu.anchoredPosition;var scroll=contextMenu.GetComponent<ScrollRect>();scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-1)});Check(contextMenu.anchoredPosition==moved,"滚动内容移动了窗口");
            yield return null;Shot("-boxed-menu-dragged");
            foreach(var edge in new[]{new Vector2(-Screen.width*4,Screen.height*4),new Vector2(Screen.width*4,Screen.height*4),new Vector2(-Screen.width*4,-Screen.height*4),new Vector2(Screen.width*4,-Screen.height*4)})
            {
                e.position=Centre((RectTransform)handle.transform);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.beginDragHandler);e.position+=edge;ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.endDragHandler);Fits();
            }
            // Reopen and move again before following an actual facility action.
            OpenBoxedSites(screen);handle=contextMenu.Find("Drag title").GetComponent<PlanMenuDragHandle>();e.position=Centre((RectTransform)handle.transform);
            ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.beginDragHandler);e.position+=new Vector2(-Screen.width*.16f,Screen.height*.08f);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.endDragHandler);
            moved=contextMenu.anchoredPosition;contextBody.GetComponentsInChildren<Button>().First(b1=>b1.name.EndsWith(" #"+site.id)).onClick.Invoke();
            Check(selectedSite==site.id&&Vector2.Distance(contextMenu.anchoredPosition,moved)<1,"设施详情未沿用拖动后的菜单位置");
            handle=contextMenu.Find("Drag title").GetComponent<PlanMenuDragHandle>();e.position=Centre((RectTransform)handle.transform);
            ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.beginDragHandler);e.position+=new Vector2(50,0);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(handle.gameObject,e,ExecuteEvents.endDragHandler);Fits();yield return null;Shot("-facility-menu-dragged");
            bool enabled=site.enabled;contextBody.GetComponentsInChildren<Button>().First(b1=>b1.name==(enabled?"停止运行":"恢复运行")).onClick.Invoke();Check(site.enabled!=enabled&&!ContextOpen,"移动后的设施操作失效");site.enabled=enabled;
            OpenWorldContext(site.x,site.y,screen,false,site.id);contextMenu.Find("×").GetComponent<Button>().onClick.Invoke();Check(!ContextOpen,"关闭按钮失效");
            OpenBoxedSites(screen);EscapeInterface();Check(!ContextOpen&&!MenuOpen,"Esc未关闭移动菜单");ClearMapSelection();RefreshPresentation(true);
        }
    }
}
