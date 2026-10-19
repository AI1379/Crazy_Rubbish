using System;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private LineRenderer placementOutline;
        private SpriteRenderer placementFront;
        private Text placementHint;
        private void HidePlacementPreview()
        {
            if(preview!=null)preview.gameObject.SetActive(false);
            if(placementOutline!=null)placementOutline.gameObject.SetActive(false);
            if(placementFront!=null)placementFront.gameObject.SetActive(false);
            if(placementHint!=null)placementHint.text="";
            if(portPreviewLink!=null)portPreviewLink.gameObject.SetActive(false);
        }
        private void UpdatePreview(int x,int y)
        {
            if(beltGesture&&pointerDragged)return;
            var moving=Model.Site(movingSite);string type=movingSite>0?moving?.type:buildType;
            if(type==null){HidePlacementPreview();return;}
            if(moving==null)SnapJourneyPlacement(type,ref x,ref y);
            int rotation=moving!=null?moving.rotation:buildRotation;
            var candidate=new PlanSite{type=type,x=x,y=y,rotation=rotation};var size=Model.Size(candidate);
            string problem=moving!=null?Model.MoveProblem(moving,x,y):Model.BuildProblem(type,x,y,rotation,beltCrossing);
            if(moving==null&&type=="belt")problem=Model.InspectBeltRoute(x,y,x,y,false,beltCrossing,rotation).problem;
            if(moving==null&&type=="port"&&problem==null)problem=PortPreviewProblem(x,y);
            bool available=problem==null;Color tint=available?new Color(.7f,1,.7f,.55f):new Color(1,.4f,.4f,.55f);
            preview.gameObject.SetActive(true);var image=preview.GetComponent<SpriteRenderer>();if(image==null)image=preview.gameObject.AddComponent<SpriteRenderer>();
            image.sprite=appearance.PlanSprite(type,true);image.sortingOrder=60;image.color=tint;Scale(image,Mathf.Max(size.width,size.height));
            preview.position=new Vector3(x+(size.width-1)/2f,y+(size.height-1)/2f,0);preview.localRotation=Quaternion.Euler(0,0,type=="belt"?rotation*90:0);
            if(placementOutline==null)
            {
                placementOutline=Root("Placement footprint",planWorld).gameObject.AddComponent<LineRenderer>();placementOutline.useWorldSpace=false;placementOutline.loop=true;placementOutline.positionCount=4;placementOutline.widthMultiplier=.06f;placementOutline.sortingOrder=62;
                var material=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.DontSave};terrainResources.Add(material);placementOutline.sharedMaterial=material;
                placementFront=Draw("Placement operation side",appearance.marker,planWorld,0,0,.45f,64);
            }
            placementOutline.gameObject.SetActive(true);placementOutline.transform.position=preview.position;placementOutline.startColor=placementOutline.endColor=available?new Color(.5f,1,.6f):new Color(1,.35f,.3f);
            float w=size.width/2f,h=size.height/2f;placementOutline.SetPositions(new[]{new Vector3(-w,-h),new Vector3(-w,h),new Vector3(w,h),new Vector3(w,-h)});
            var front=Model.Front(candidate);placementFront.gameObject.SetActive(!Model.Overlay(type));placementFront.transform.position=new Vector3(front.x,front.y,0);placementFront.color=placementOutline.startColor;
            placementHint.color=available?Ink:Accent;placementHint.text=(moving!=null?"移动 ":"建设 ")+Model.Config.Facility(type).name+" · "+(moving!=null?"左键确认，右键取消":"R旋转，左键放置，右键取消")+"\n"+(available?"可以放置；标记处为操作侧。":"无法放置："+problem);
            if(type=="belt"&&moving==null)placementHint.text="传送带向"+PlanSimulation.DirectionName(rotation)+" · 左键拖拽整段铺设，Shift换拐角，R旋转\n"+(available?"单击铺设一格；重用同向传送带不重复消耗材料。":"无法放置："+problem);
            if(type=="port"&&moving==null)
            {
                DrawPortPreview(x,y);var machine=PreviewPortMachine(x,y);placementHint.text=(portPlacementMode=="input"?"输入接口":"输出接口向"+PlanSimulation.DirectionName(rotation))+" · R旋转，右键取消\n"+(available?"连接 "+Model.Config.Facility(machine.type).name+" #"+machine.id+"；放置后自动绑定。":"无法放置："+problem);
            }
            else if(portPreviewLink!=null)portPreviewLink.gameObject.SetActive(false);
        }
        private bool TryPlaceAt(int x,int y)
        {
            if(movingSite==0&&buildType==null)return false;
            if(movingSite==0)SnapJourneyPlacement(buildType,ref x,ref y);
            try
            {
                if(movingSite>0){Model.MoveSite(Model.Site(movingSite),x,y);movingSite=0;HidePlacementPreview();Notice="设施已移动。";}
                else if(buildType=="belt"){selectedSite=Model.BuildBeltRoute(x,y,x,y,false,beltCrossing,buildRotation)[0].id;Notice="传送带已放置，沿箭头输送。";}
                else if(buildType=="port")
                {
                    string problem=PortPreviewProblem(x,y);if(problem!=null)throw new InvalidOperationException(problem);var machine=PreviewPortMachine(x,y);
                    var port=Model.Build("port",x,y,buildRotation);Model.ConfigurePort(port,machine,portPlacementMode);selectedSite=port.id;Notice="接口已连接到 "+Model.Config.Facility(machine.type).name+" #"+machine.id;
                }
                else{selectedSite=Model.Build(buildType,x,y,buildRotation,beltCrossing).id;Notice="设施已放置。";}
            }
            catch(Exception e){Notice=e.Message;}
            RebuildPanel();RefreshPresentation();if(movingSite>0||buildType!=null)UpdatePreview(x,y);return true;
        }
    }
}
