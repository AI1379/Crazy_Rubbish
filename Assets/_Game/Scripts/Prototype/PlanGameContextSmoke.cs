using System;
using System.IO;
using System.Collections;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeContextInterface(string screenshot)
        {
            void Check(bool value,string message){if(!value)throw new InvalidOperationException("交互界面："+message);}
            string Shot(string suffix)=>Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png");
            Check(!managementOpen&&!management.gameObject.activeSelf,"右栏未默认收起");
            Check(CapacityLabel(double.MaxValue)=="不限","无限容量标签异常");
            var edgeView=mapCamera.ViewportToWorldPoint(new Vector3(0,0,10));Check(edgeView.x>=-5.1f&&edgeView.y>=-1.1f,"地图边缘出现过量空白");
            float expanded=mapCamera.rect.width;ShowManagement(3);Check(management.gameObject.activeSelf&&mapCamera.rect.width<expanded,"管理展开未改变地图宽度");
            var itemHit=panelContent.Find("Item context");Check(itemHit!=null,"库存无右键入口");
            var evt=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right,position=new Vector2(Screen.width-2,2)};
            ExecuteEvents.Execute(itemHit.gameObject,evt,ExecuteEvents.pointerClickHandler);Check(ContextOpen,"库存右键未显示菜单");
            CloseContext();HideManagement();Check(Mathf.Abs(mapCamera.rect.width-expanded)<.001f,"管理收起未还原地图宽度");
            var stock=Model.State.ground.First(g=>g.stock.stacks.Count>0);var center=new Vector2(Screen.width*.56f,Screen.height*.64f);
            OpenWorldContext(stock.x,stock.y,center,false);Check(ContextOpen&&contextBody.GetComponentsInChildren<Button>().Length>=2,"地面物品无拾取选项");
            yield return null;
            if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Shot("-context-items"));
            var ray=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,contextMenu.TransformPoint(new Vector3(20,-20,0)))},ray);
            Check(ray.Any(r=>r.gameObject.transform.IsChildOf(contextMenu)||r.gameObject==contextMenu.gameObject),"菜单没有阻挡地图点击：hits="+ray.Count+" depth="+contextMenu.GetComponent<Image>().depth+" cull="+contextMenu.GetComponent<Image>().canvasRenderer.cull+" position="+contextMenu.position+" canvas="+((RectTransform)planCanvas).rect);
            foreach(var edge in new[]{Vector2.zero,new Vector2(Screen.width,0),new Vector2(0,Screen.height),new Vector2(Screen.width,Screen.height)})
            {
                OpenWorldContext(stock.x,stock.y,edge,false);var corners=new Vector3[4];contextMenu.GetWorldCorners(corners);
                Check(corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1),"屏幕边缘菜单被裁切");
            }
            CloseContext();buildType="warehouse";Check(CancelPlacement()&&buildType==null&&!ContextOpen,"建设取消失效");
            var site=Model.State.sites.First(s=>s.type=="warehouse");OpenWorldContext(site.x,site.y,center,false);Check(selectedSite==site.id,"设施菜单选错目标");
            bool enabled=site.enabled;contextBody.GetComponentsInChildren<Button>().First(b=>b.name==(enabled?"停止运行":"恢复运行")).onClick.Invoke();Check(site.enabled!=enabled&&!ContextOpen,"设施开停机按钮失效");site.enabled=enabled;
            OpenWorldContext(site.x,site.y,center,false);if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Shot("-context-site"));
            contextBody.GetComponentsInChildren<Button>().First(b=>b.name=="配方 / 人员 / 物流等详细设置").onClick.Invoke();Check(managementOpen&&panelTab==2&&!ContextOpen,"详细设置入口失效");
            RefreshInterface();Check(siteStatus!=null&&siteStatus.text.Length<200&&!siteStatus.text.Contains(double.MaxValue.ToString("0.##")),"仓库状态超长溢出");
            if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Shot("-management"));
            EscapeInterface();Check(!managementOpen&&!MenuOpen,"Esc收起管理失效");
            OpenWorldContext(2,20,center,false);Check(selectedSite==-1&&ContextOpen,"洞口无操作菜单");EscapeInterface();Check(!ContextOpen&&!MenuOpen,"Esc关闭菜单失效");
            // A disappeared pile must produce a readable notice, rather than a stale reference exception.
            var saved=Model;Model=new PlanSimulation(LoadConfig());var temporary=Model.State.ground.First(g=>g.stock.stacks.Count>0);
            OpenWorldContext(temporary.x,temporary.y,center,false);var pick=contextBody.GetComponentsInChildren<Button>().First(b=>b.name.Contains("· 拾取"));
            Model.State.ground.RemoveAll(g=>g.x==temporary.x&&g.y==temporary.y);pick.onClick.Invoke();Check(Notice=="物品已被取走。","消失物品未安全处理");
            var available=Model.State.ground.First(g=>g.stock.stacks.Count>0);Model.State.player.x=available.x;Model.State.player.y=available.y;Model.State.paused=false;
            OpenWorldContext(available.x,available.y,center,false);contextBody.GetComponentsInChildren<Button>().First(b=>b.name.Contains("· 拾取")).onClick.Invoke();
            Check(Model.State.player.trip!=null&&Model.State.player.trip.source=="ground:"+available.id,"拾取没有指向选中的垃圾堆");
            Model=saved;moveX=-1;CloseContext();HideManagement();Notice="右键物品或设施查看操作；Tab 管理，B 建设，I 库存。";RefreshPresentation(true);
            var selection=SmokeSelectionInterface(screenshot);while(selection.MoveNext())yield return selection.Current;
            var dragging=SmokeMenuDragging(screenshot);while(dragging.MoveNext())yield return dragging.Current;
            var water=SmokeSurfaceWater(screenshot);while(water.MoveNext())yield return water.Current;
        }
    }
}
