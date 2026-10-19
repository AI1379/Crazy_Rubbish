using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private Transform journeyRoot;private SpriteRenderer liftView;private TextMesh journeyLabel;
        private string journeyVisualKey;
        private void SnapJourneyPlacement(string type,ref int x,ref int y)
        {
            if(type=="mine")foreach(string kind in Model.State.surveyedVeins){var v=Model.Vein(kind);if(PlanSimulation.InRect(x,y,v.rectangle)){x=v.anchor[0];y=v.anchor[1];return;}}
            if(type=="lift_launch"){var a=Model.Config.Region(3).launch_platform_anchor;if(x>=a[0]&&x<a[0]+4&&y>=a[1]&&y<a[1]+4){x=a[0];y=a[1];}}
        }
        private bool OpenJourneyContext(int x,int y,Vector2 screen)
        {
            if(Model.State.region<3)return false;
            if(Model.HeroAboard&&liftView!=null&&liftView.bounds.Contains(new Vector3(x,y,0))){var platform=Model.Site(Model.State.aboardSite);OpenWorldContext(platform.x,platform.y,screen,false,platform.id);return true;}
            foreach(string kind in Model.State.surveyedVeins)
            {
                var v=Model.Vein(kind);if(!PlanSimulation.InRect(x,y,v.rectangle))continue;BeginContext(PlanSimulation.MineralName(kind)+"地基",screen);ContextLine("指定采掘锚点（"+v.anchor[0]+","+v.anchor[1]+"），每处矿脉最多一台采掘机。",74);
                ContextAction("预览采掘机",()=>{buildType="mine";buildRotation=0;HideManagement();Notice="点击矿点范围确认建设；预览显示材料与人员要求。";});EndContext();return true;
            }
            var a=Model.Config.Region(3).launch_platform_anchor;if(x<a[0]||x>=a[0]+4||y<a[1]||y>=a[1]+4)return false;
            BeginContext("旧检修平台地基",screen);ContextLine("独立检修竖井通往上层。先制造五类总成，再在此总装升降舱并试车。",95);ContextAction("预览检修平台",()=>{buildType="lift_launch";buildRotation=0;HideManagement();});EndContext();return true;
        }
        private void FocusJourneyPoint(int x,int y){CancelMapGesture();mapOverview=false;mapCamera.transform.position=new Vector3(x,y,-10);mapCamera.orthographicSize=14;}
        private void JourneyPanel()
        {
            if(Model.State.region<3)return;Line("矿料与返程工程",21,42);
            Line("拆解地质仪器与采掘设备 → 勘探三类矿脉 → 持续开采 → 冶炼及绝缘件 → 制造五种返程总成 → 总装、试车、登舱。垃圾回收仍提供承力件与机电部件。",16,122);
            foreach(string kind in PlanSimulation.MineralKinds)
            {string mineral=kind;bool known=Model.State.surveyedVeins.Contains(kind);Line(PlanSimulation.MineralName(kind)+"："+(known?"已勘探，可建设采掘机":"尚未勘探"),16,36);if(known)Row("定位"+PlanSimulation.MineralName(kind)+"锚点",()=>{var a=Model.Vein(mineral).anchor;FocusJourneyPoint(a[0],a[1]);});}
            var anchor=Model.Config.Region(3).launch_platform_anchor;Row("定位旧检修竖井",()=>FocusJourneyPoint(anchor[0],anchor[1]));
            foreach(string item in new[]{"lift_frame","lift_drive","lift_brake","lift_control","lift_cable"})Line(Model.Config.Item(item).name+" · 仓库 "+(Model.Stored(item)/(double)PlanStock.Scale).ToString("0.##"),16,36);
            var platform=Model.State.sites.FirstOrDefault(s=>s.type=="lift_launch");if(platform!=null){Line(Model.ProgressionDescription(platform),16,82);Row("查看检修平台",()=>LocateSite(platform.id));}
            Line("升降舱保存在平台中。试车通过后让主角到操作侧，在平台菜单确认登舱。爬升需要持续供电和维护，其他经营继续。",16,110);
        }
        private void ConfirmBoardLift(PlanSite site)
        {
            BeginContext("确认登舱返程",new Vector2(Screen.width*.55f,Screen.height*.75f));
            ContextLine("主角将乘升降舱沿旧检修竖井回到上层。准备和爬升期间经营继续，缺电或耐久不足会暂停爬升。登舱后主角不能同时劳动或离舱。",148);
            ContextAction("确认登舱并启动",()=>{Act(()=>Model.BoardLift(site));if(Model.HeroAboard){moveX=-1;ClearMapSelection();}});ContextAction("暂不返程",()=>{});EndContext();
        }
        private void RefreshJourneyVisuals()
        {
            if(Model.State.region<3){if(journeyRoot!=null)journeyRoot.gameObject.SetActive(false);return;}
            var platform=Model.State.sites.FirstOrDefault(s=>s.type=="lift_launch");string key=string.Join(",",Model.State.surveyedVeins)+":"+(platform?.id??0);
            if(journeyRoot==null||key!=journeyVisualKey)
            {
                if(journeyRoot!=null)Remove(journeyRoot.gameObject);journeyRoot=Root("Mineral points and return shaft",planWorld);journeyVisualKey=key;
                foreach(string kind in Model.State.surveyedVeins){var a=Model.Vein(kind).anchor;var marker=Draw("Vein "+kind,appearance.marker,journeyRoot,a[0]+1,a[1]+1,3,3);marker.color=kind=="iron"?new Color(.6f,.7f,.75f,.55f):kind=="copper"?new Color(.9f,.55f,.25f,.55f):new Color(.7f,.65f,.45f,.55f);WorldLabel(PlanSimulation.MineralName(kind)+" · 采掘锚点",journeyRoot,a[0]+1,a[1]+3,.08f);}
                var at=Model.Config.Region(3).launch_platform_anchor;var rails=LogisticsLine("Old inspection rails",journeyRoot,1);rails.widthMultiplier=.13f;rails.positionCount=4;rails.SetPositions(new[]{new Vector3(at[0]+1,at[1]),new Vector3(at[0]+1,at[1]+13),new Vector3(at[0]+3,at[1]+13),new Vector3(at[0]+3,at[1])});rails.startColor=rails.endColor=new Color(.5f,.53f,.5f);
                journeyLabel=WorldLabel("旧检修竖井 · 通往上层",journeyRoot,at[0]+2,at[1]+5,.09f);liftView=Draw("Return lift cabin",appearance.PlanSprite("lift_cabin"),journeyRoot,at[0]+1.5f,at[1]+1.5f,3,35);
            }
            journeyRoot.gameObject.SetActive(true);liftView.gameObject.SetActive(platform!=null&&platform.cabinId>0);
            if(platform==null)return;float fraction=platform.active&&platform.recipe=="lift_ascent"&&platform.stage==1?(float)(platform.progress/180):0;liftView.transform.position=new Vector3(platform.x+1.5f,platform.y+1.5f+fraction*8,0);
            if(Model.HeroAboard)journeyLabel.text="检修竖井 · 已登舱 · 爬升 "+fraction.ToString("0%");
        }
        private void DrawEndingIllustration()
        {
            if(string.IsNullOrEmpty(Model.State.endingCode))return;var panel=Box(menu,"Return ending",32,144,830,180);panel.GetComponent<Image>().color=new Color32(36,54,54,255);
            var image=Rect(panel,"Lift illustration",10,10,210,160).gameObject.AddComponent<Image>();image.sprite=appearance.PlanSprite("lift_cabin");image.preserveAspect=true;image.raycastTarget=false;
            Text(panel,"Ending narrative",Model.State.ending+"\n\n路线：旧检修平台 → 独立检修竖井 → 上层处理场。",230,12,580,150,19);
        }
    }
}
