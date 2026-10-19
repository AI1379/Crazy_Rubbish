using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform management,contextMenu,contextBody;
        private bool managementOpen,contextDismissed,cancelledPlacement;
        private float contextY;

        private void ShowManagement(int tab)
        {if(tab==0){OpenGuideDetails();return;}CancelMapGesture();CloseSettings();CloseContext();panelTab=tab;managementOpen=true;management.gameObject.SetActive(true);RebuildPanel();FitViewport();}
        private void ToggleManagement()
        {if(managementOpen)HideManagement();else ShowManagement(panelTab);}
        private void HideManagement()
        {managementOpen=false;if(management!=null)management.gameObject.SetActive(false);FitViewport();RefreshGuideHud();}
        private bool ContextOpen=>contextMenu!=null&&contextMenu.gameObject.activeSelf;
        private void CloseContext(){if(contextMenu!=null)contextMenu.gameObject.SetActive(false);}
        private bool CancelPlacement()
        {if(buildType==null&&movingSite==0)return false;CancelMapGesture();buildType=null;movingSite=0;moveX=-1;Notice="已取消放置。";HidePlacementPreview();CloseContext();return true;}
        private void EscapeInterface()
        {
            CancelMapGesture();
            if(InventoryOpen){CloseInventoryWindow();return;}
            if(SaveDialogOpen){CancelSaveDialog();return;}
            if(MenuOpen){if(session&&string.IsNullOrEmpty(Model.State.ending)){MenuOpen=false;RebuildPanel();}return;}
            if(SettingsOpen){CloseSettings();return;}
            if(ContextOpen){CloseContext();return;}
            if(guideExpanded&&guideHud!=null&&guideHud.gameObject.activeInHierarchy){guideExpanded=false;RefreshGuideHud();return;}
            if(CancelPlacement())return;
            if(selectedPeople.Count>0||boxedGround.Count>0||boxedSites.Count>0){ClearMapSelection();return;}
            if(managementOpen){HideManagement();return;}
            MenuOpen=true;RebuildPanel();
        }
        private void BeginContext(string title,Vector2 screen)
        {
            CloseSettings();
            contextLiveText=null;contextPeopleText=null;contextSiteId=0;contextOutputs=null;outputButtons.Clear();
            if(contextMenu!=null)Remove(contextMenu.gameObject);
            contextMenu=Box(planCanvas,"Context Menu",0,0,330,120);
            contextMenu.GetComponent<Image>().raycastTarget=true;
            var handle=Rect(contextMenu,"Drag title",4,4,280,56);
            handle.gameObject.AddComponent<Image>().color=new Color32(35,53,55,255);
            handle.gameObject.AddComponent<PlanMenuDragHandle>().Bind(contextMenu);
            Text(contextMenu,"Title",title,12,9,246,52,19);
            for(int i=0;i<3;i++){var grip=Rect(handle,"Grip",260,20+i*5,12,2).gameObject.AddComponent<Image>();grip.color=new Color32(118,137,132,255);grip.raycastTarget=false;}
            Button(contextMenu,"×",288,8,32,32,CloseContext);
            var viewport=Rect(contextMenu,"Viewport",8,66,314,10);
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            contextBody=Rect(viewport,"Actions",0,0,314,10);contextY=0;
            var scroll=contextMenu.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=contextBody;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            // Keep the pointer anchor until the final height is known.
            contextMenu.anchoredPosition=screen;
        }
        private void ContextLine(string value,float height=52)
        {Text(contextBody,"Information",value,4,contextY,302,height,15);contextY+=height+5;}
        private void ContextAction(string caption,Action action)
        {Button(contextBody,caption,4,contextY,302,36,()=>{CloseContext();action();});contextY+=42;}
        private Vector2 ContextScreenAnchor(Vector2 fallback)
        {return contextMenu==null?fallback:RectTransformUtility.WorldToScreenPoint(null,contextMenu.TransformPoint(new Vector3(-12,12)));}
        private void EndContext()
        {
            Canvas.ForceUpdateCanvases();var canvas=(RectTransform)planCanvas;
            Vector2 pointer=contextMenu.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,pointer,null,out var local);
            float height=Mathf.Min(contextY+80,Mathf.Min(570,canvas.rect.height-32));
            contextMenu.sizeDelta=new Vector2(330,height);
            contextMenu.anchoredPosition=new Vector2(Mathf.Clamp(local.x-canvas.rect.xMin+12,8,canvas.rect.width-338),-Mathf.Clamp(canvas.rect.yMax-local.y+12,8,canvas.rect.height-height-8));
            contextBody.sizeDelta=new Vector2(314,contextY);
            ((RectTransform)contextBody.parent).sizeDelta=new Vector2(314,height-74);
            contextMenu.SetAsLastSibling();Canvas.ForceUpdateCanvases();
        }
        private readonly System.Collections.Generic.List<RaycastResult> pointerHits=new System.Collections.Generic.List<RaycastResult>();
        private bool PointerOverInterface()=>PointerOverInterface(Input.mousePosition);
        private bool PointerOverInterface(Vector2 point)
        {
            if(ContextOpen&&RectTransformUtility.RectangleContainsScreenPoint(contextMenu,point,null))return true;
            if(managementOpen&&RectTransformUtility.RectangleContainsScreenPoint(management,point,null))return true;
            var system=EventSystem.current;if(system==null)return false;
            pointerHits.Clear();system.RaycastAll(new PointerEventData(system){position=point},pointerHits);return pointerHits.Count>0;
        }
        private void OpenWorldContext(int x,int y,Vector2 screen,bool personPriority=true,int siteId=0)
        {
            if(MenuOpen)return;
            var character=personPriority?PeopleAt(new Vector2(x,y)).FirstOrDefault():null;
            if(character!=null){selectedPeople.Clear();selectedPeople.Add(character.id);boxedPeople.Clear();boxedPeople.Add(character.id);OpenPeopleContext(screen);return;}
            var sites=Model.State.sites.Where(s=>Model.Covers(s,x,y)).ToArray();
            if(sites.Length>0)
            {
                var s=sites.FirstOrDefault(a=>a.id==siteId)??sites.Last();selectedSite=s.id;
                BeginContext(Model.Config.Facility(s.type).name+" #"+s.id,screen);
                AddContextReadout(s);
                if(s.type=="warehouse")ContextAction("打开仓库物品栏",()=>RequestWarehouseInventory(s));
                if(s.type=="lift_launch"&&s.cabinTested&&!Model.HeroAboard)ContextAction("确认登舱返程",()=>ConfirmBoardLift(s));
                ContextAction("走到操作侧",()=>GoToSite(s));
                ContextAction("入库 / 投入背包物品",()=>Act(()=>Model.Deposit(s)));
                ContextAction("主角进入 / 退出岗位",()=>Act(()=>Model.AssignHero(s.id)));
                ContextAction(s.enabled?"停止运行":"恢复运行",()=>Act(()=>Model.SetSiteEnabled(s,!s.enabled)));
                if(s.type=="belt")ContextAction("旋转输送方向",()=>Act(()=>Model.RotateTransport(s)));
                if(s.type=="port")
                {ContextAction("切换输入 / 输出",()=>Act(()=>s.portMode=s.portMode=="input"?"output":"input"));ContextAction("旋转输出方向",()=>Act(()=>Model.RotateTransport(s)));}
                if(Model.Config.Facility(s.type).ports>0&&!Model.Overlay(s.type)&&Model.State.technologies.Contains(Model.Config.Facility("port").technology))
                {ContextAction("添加输入接口",()=>StartPortPlacement("input"));ContextAction("添加输出接口",()=>StartPortPlacement("output"));}
                AddContextOutputs(s);
                if(sites.Length>1)foreach(var other in sites.Where(a=>a!=s))
                {int id=other.id;ContextAction("设置同格："+Model.Config.Facility(other.type).name,()=>{selectedSite=id;ShowManagement(2);});}
                ContextAction("查找缺料 / 制作方法",()=>OpenSiteMaterials(s));
                ContextAction("配方 / 人员 / 物流等详细设置",()=>ShowManagement(2));
                if(!s.community&&!PlanSimulation.IsWaterIntake(s.type))ContextAction("移动设施",()=>{movingSite=s.id;buildType=null;HideManagement();Notice="点击新位置移动；右键取消。";});
                EndContext();return;
            }
            var ground=Model.State.ground.Where(g=>g.x==x&&g.y==y&&g.stock.stacks.Count>0).ToArray();
            if(ground.Length>0){OpenGroundContext(ground,screen);return;}
            if(x>=2&&x<=3&&y>=20&&y<=21)
            {
                selectedSite=-1;BeginContext("洞口 · 去向不明",screen);ContextLine("只能倾倒实际运抵的弃料。",40);
                ContextAction("走到洞口外侧",()=>SetHeroDestination(2,19));
                ContextAction("查看倾倒与运料设置",()=>ShowManagement(2));EndContext();return;
            }
            if(OpenJourneyContext(x,y,screen))return;
            if(PlanTerrain.WaterAt(x,y)>0){OpenWaterContext(x,y,screen);return;}
            BeginContext("地面（"+x+", "+y+"）",screen);
            if(Model.Walkable(x,y))ContextAction("移动到这里",()=>SetHeroDestination(x,y));
            else ContextLine("岩体、深水或未开放土地无法直接通行。",50);
            ContextAction("建设设施",()=>ShowManagement(1));ContextAction("土地与扩张",()=>ShowManagement(6));EndContext();
        }
        private void OpenWaterContext(int x,int y,Vector2 screen)
        {
            bool pond=PlanTerrain.WaterBodyAt(x,y)==PlanTerrain.StarterPondBody;
            BeginContext(pond?"出生地小水塘":"河流水面 / 湖泊",screen);
            ContextLine("在已开放地面的岸边建设采水设施。\n采出的原水需净化后才能作为饮用水。",68);
            if(pond)ContextLine("沿用原渗水点：缓存12升，每分钟补充6升；最多一台取水设施。",66);
            else ContextLine("同一连通水体共用120升缓存，每分钟补充60升，多台设备共用预算。",66);
            foreach(string type in new[]{"water_intake","auto_water_plant"})
            {
                var facility=Model.Config.Facility(type);if(!Model.State.technologies.Contains(facility.technology))continue;string id=type;
                ContextAction("建设："+facility.name,()=>{buildType=id;HideManagement();Notice="选择临水的可建设岸边，操作侧须可达；R旋转，右键取消。";});
            }
            ContextAction("查看采水建设目录",()=>ShowManagement(1));EndContext();
        }
        private void ItemRightClick(GameObject target,string item)
        {
            var trigger=target.AddComponent<EventTrigger>();var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerClick};
            entry.callback.AddListener(data=>{var p=(PointerEventData)data;if(p.button!=PointerEventData.InputButton.Right||MenuOpen)return;
                BeginContext(Model.Config.Item(item).name,p.position);
                ContextLine("背包 "+(Model.State.backpack.Count(item)/(double)PlanStock.Scale).ToString("0.##")+" · 仓库 "+(Model.Stored(item)/(double)PlanStock.Scale).ToString("0.##"));
                ContextAction("查看获取途径 / 制作方法",()=>OpenItemSources(item));
                ContextAction("寻找地面物品",()=>FindItem(item));var def=Model.Config.Item(item);
                if(def.canBuy)ContextAction("购买1",()=>Act(()=>Model.Buy(item,PlanStock.Scale)));
                if(def.canSell)ContextAction("出售5（受生活保留量限制）",()=>Act(()=>Model.Sell(item,5*PlanStock.Scale)));
                EndContext();});trigger.triggers.Add(entry);
        }
    }
}
