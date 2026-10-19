using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform guideHud,guideViewport,guideContent;
        private Text guideTitle,guideGoal;
        private Button guideFold,guideDetails,guideLocate;
        private bool guideCollapsed,guideExpanded;
        private string guideContentKey;
        private static readonly string[] GuideSteps={
            "先收好食物、水和木柄手锯。",
            "取得木料与紧固件，建设木工地点。",
            "选择锯木板配方，投入原料，让主角到操作侧参加劳动。",
            "建设发电机、铺线连接住房或路灯，并投入燃料。",
            "实际供电获得认可后，在居民页招募第一名员工。",
            "逐步建立采水净化与太阳灯种植；先留出食水和工资。",
            "留出生活库存与工资，筹集200钱购买二区。",
            "二区已开放，可继续优化自动产线。"
        };
        private string GuideGoal()
        {
            int step=Mathf.Clamp(Model.State.tutorial,0,7);
            if(step==7)return Model.State.region<3?"后续目标：筹集600钱开放三区，建立勘探、开采和返程工程。":"返程目标：完成矿料加工、升降舱总装和带载测试，到检修平台确认登舱。";
            return "步骤 "+(step+1)+" / 7："+GuideSteps[step];
        }
        private void BuildGuideHud()
        {
            guideHud=Box(planCanvas,"Tutorial HUD",0,94,300,214);
            guideHud.anchorMin=guideHud.anchorMax=guideHud.pivot=new Vector2(1,1);
            guideHud.anchoredPosition=new Vector2(-20,-94);guideHud.GetComponent<Image>().raycastTarget=true;
            guideTitle=Text(guideHud,"Title","新手教程",12,7,210,28,17);
            guideFold=Button(guideHud,"收起教程",238,5,54,28,()=>{guideCollapsed=!guideCollapsed;guideExpanded=false;RefreshGuideHud();});
            guideGoal=Text(guideHud,"Current goal","",12,44,276,76,16);
            guideLocate=Button(guideHud,"查看当前目标",12,128,276,32,LocateGuideGoal);
            guideDetails=Button(guideHud,"完整教程 / 操作速查",12,170,276,32,()=>{guideExpanded=!guideExpanded;guideCollapsed=false;RefreshGuideHud();});
            guideViewport=Rect(guideHud,"Details viewport",8,216,384,360);
            guideViewport.gameObject.AddComponent<Image>().color=Color.clear;guideViewport.gameObject.AddComponent<RectMask2D>();
            guideContent=Rect(guideViewport,"Contents",0,0,384,2000);
            var scroll=guideViewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=guideViewport;scroll.content=guideContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            guideContentKey=null;
        }
        private void OpenGuideDetails()
        {
            CancelMapGesture();CloseSettings();CloseContext();guideCollapsed=false;guideExpanded=true;HideManagement();RefreshGuideHud();
        }
        private void RefreshGuideHud()
        {
            if(guideHud==null||Model==null)return;
            guideHud.gameObject.SetActive(!MenuOpen&&!SaveDialogOpen&&!managementOpen);
            float width=guideExpanded?400:300;
            guideTitle.text=Model.State.tutorial<7?"新手教程 · "+(Mathf.Clamp(Model.State.tutorial,0,6)+1)+" / 7":"教程 · 后续目标";
            guideFold.GetComponent<RectTransform>().anchoredPosition=new Vector2(width-62,-5);
            guideFold.GetComponentInChildren<Text>().text=guideCollapsed?"展开":"收起";
            guideGoal.text=GuideGoal();guideGoal.rectTransform.sizeDelta=new Vector2(width-24,76);
            float goalHeight=Mathf.Max(42,guideGoal.preferredHeight+4),locateY=52+goalHeight,detailsY=locateY+42,compactHeight=detailsY+44;
            guideGoal.rectTransform.sizeDelta=new Vector2(width-24,goalHeight);
            guideHud.sizeDelta=new Vector2(width,guideCollapsed?38:guideExpanded?compactHeight+374:compactHeight);
            guideLocate.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-locateY);guideDetails.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-detailsY);
            guideViewport.anchoredPosition=new Vector2(8,-compactHeight-2);
            guideGoal.gameObject.SetActive(!guideCollapsed);guideLocate.gameObject.SetActive(!guideCollapsed);guideDetails.gameObject.SetActive(!guideCollapsed);
            guideLocate.GetComponent<RectTransform>().sizeDelta=new Vector2(width-24,32);guideDetails.GetComponent<RectTransform>().sizeDelta=new Vector2(width-24,32);
            guideDetails.GetComponentInChildren<Text>().text=guideExpanded?"收起完整说明":"完整教程 / 操作速查";
            guideViewport.gameObject.SetActive(guideExpanded&&!guideCollapsed);
            string key=Model.State.tutorial+":"+Model.State.region;
            if(guideExpanded&&guideContentKey!=key)
            {
                var priorContent=panelContent;float priorY=rowY;
                try
                {
                    for(int i=guideContent.childCount-1;i>=0;i--)Remove(guideContent.GetChild(i).gameObject);
                    panelContent=guideContent;rowY=0;GuidePanel();guideContent.sizeDelta=new Vector2(384,Mathf.Max(360,rowY+20));guideContent.anchoredPosition=Vector2.zero;guideContentKey=key;
                }
                finally{panelContent=priorContent;rowY=priorY;}
            }
        }
        private void LocateGuideGoal()
        {
            CancelMapGesture();CloseContext();int step=Mathf.Clamp(Model.State.tutorial,0,7);
            if(step<=1)
            {
                var ids=step==0?new[]{"water","food","handsaw_wood"}:new[]{"waste_wood","fasteners"};
                string item=ids.FirstOrDefault(id=>!Model.State.acquired.Contains(id));
                if(item!=null)
                {
                    var pile=Model.State.ground.FirstOrDefault(g=>g.stock.Count(item)>0);
                    if(pile!=null){mapOverview=false;mapCamera.transform.position=new Vector3(pile.x,pile.y,-10);mapCamera.orthographicSize=12;OpenWorldContext(pile.x,pile.y,new Vector2(Screen.width*.5f,Screen.height*.65f),false);Notice="已定位："+Model.Config.Item(item).name+"。走近后拾取。";return;}
                    Notice="地面暂未找到："+Model.Config.Item(item).name+"，可查看库存或采购。";return;
                }
            }
            if(step==2||step==3)
            {
                var site=Model.State.sites.FirstOrDefault(s=>s.type==(step==2?"wood":"generator"));if(site!=null){LocateSite(site.id);return;}
            }
            ShowManagement(step==4?4:step>=6?6:1);
        }
    }
}
