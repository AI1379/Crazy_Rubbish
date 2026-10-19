using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private bool beltGesture,showLogistics=true;private Vector2Int beltStart;
        private string portPlacementMode="input";
        private Material logisticsMaterial;
        private Transform beltRouteRoot;
        private LineRenderer portPreviewLink;
        private readonly Dictionary<int,LineRenderer> logisticsArrows=new Dictionary<int,LineRenderer>(),portLinks=new Dictionary<int,LineRenderer>();
        private readonly List<SpriteRenderer> routeGhosts=new List<SpriteRenderer>();
        private readonly List<LineRenderer> routeArrows=new List<LineRenderer>();
        private PlanBeltRoute beltRoute;private string routeKey;private float nextRouteCheck;
        private Material LogisticsMaterial()
        {if(logisticsMaterial==null){logisticsMaterial=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.DontSave};terrainResources.Add(logisticsMaterial);}return logisticsMaterial;}
        private LineRenderer LogisticsLine(string name,Transform parent,int order=70)
        {var r=Root(name,parent).gameObject.AddComponent<LineRenderer>();r.useWorldSpace=false;r.widthMultiplier=.065f;r.sortingOrder=order;r.sharedMaterial=LogisticsMaterial();return r;}
        private static void PaintArrow(LineRenderer line,int direction,Vector3 position,Color color)
        {
            line.transform.position=position;line.transform.rotation=Quaternion.Euler(0,0,direction*90);line.positionCount=5;
            line.SetPositions(new[]{new Vector3(-.3f,0),new Vector3(.3f,0),new Vector3(.08f,.18f),new Vector3(.3f,0),new Vector3(.08f,-.18f)});line.startColor=line.endColor=color;
        }
        private void ResetAutomationViews()
        {logisticsArrows.Clear();portLinks.Clear();routeGhosts.Clear();routeArrows.Clear();logisticsMaterial=null;beltRouteRoot=null;portPreviewLink=null;beltRoute=null;routeKey=null;beltGesture=false;}
        private void HideBeltRoute()
        {if(beltGesture&&placementHint!=null)placementHint.text="";beltGesture=false;beltRoute=null;routeKey=null;if(beltRouteRoot!=null)beltRouteRoot.gameObject.SetActive(false);}
        private void BeginBeltGesture(Vector2 screen)
        {var p=mapCamera.ScreenToWorldPoint(screen);beltStart=new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y));beltGesture=true;routeKey=null;}
        private bool BeltVerticalFirst(int x,int y)
        {bool vertical=Math.Abs(y-beltStart.y)>Math.Abs(x-beltStart.x);return Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?!vertical:vertical;}
        private void PreviewBeltDrag(Vector2 screen)
        {
            var point=mapCamera.ScreenToWorldPoint(ClampMapPoint(screen));int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);bool vertical=BeltVerticalFirst(x,y);
            string key=beltStart+":"+x+":"+y+":"+vertical+":"+beltCrossing;
            if(key==routeKey&&Time.unscaledTime<nextRouteCheck)return;routeKey=key;nextRouteCheck=Time.unscaledTime+.2f;
            beltRoute=Model.InspectBeltRoute(beltStart.x,beltStart.y,x,y,vertical,beltCrossing,buildRotation);HidePlacementPreview();
            if(beltRouteRoot==null)beltRouteRoot=Root("Belt route preview",planWorld);beltRouteRoot.gameObject.SetActive(true);
            for(int i=0;i<beltRoute.cells.Length;i++)
            {
                var cell=beltRoute.cells[i];if(i>=routeGhosts.Count){routeGhosts.Add(Draw("Route tile "+i,appearance.PlanSprite("belt",true),beltRouteRoot,0,0,1,65));routeArrows.Add(LogisticsLine("Route direction "+i,beltRouteRoot,72));}
                Color color=!beltRoute.Valid?new Color(1,.4f,.35f):cell.existing>0?new Color(.45f,.8f,1):new Color(.45f,1,.65f);
                var ghost=routeGhosts[i];ghost.gameObject.SetActive(true);ghost.transform.position=new Vector3(cell.x,cell.y,0);ghost.transform.rotation=Quaternion.Euler(0,0,cell.direction*90);ghost.color=new Color(color.r,color.g,color.b,.5f);
                routeArrows[i].gameObject.SetActive(true);PaintArrow(routeArrows[i],cell.direction,new Vector3(cell.x,cell.y,0),color);
            }
            for(int i=beltRoute.cells.Length;i<routeGhosts.Count;i++){routeGhosts[i].gameObject.SetActive(false);routeArrows[i].gameObject.SetActive(false);}
            placementHint.color=beltRoute.Valid?Ink:Accent;placementHint.text="铺带 "+beltRoute.cells.Length+"格 / 新建 "+beltRoute.newCount+"格 · "+Amounts(beltRoute.costs)+"\n"+(beltRoute.Valid?"松手建设；Shift切换拐角，右键取消；离开地图或在界面上松手取消。":"无法建设："+beltRoute.problem);
        }
        private void CommitBeltDrag(Vector2Int start,int x,int y,bool vertical)
        {
            try{var route=Model.BuildBeltRoute(start.x,start.y,x,y,vertical,beltCrossing,buildRotation);if(route.Length>0)selectedSite=route.Last().id;Notice="传送带路线已建设，沿箭头输送；请连接供电与输入/输出接口。";ClearMapSelection();}
            catch(Exception e){Notice="整段未建设："+e.Message;}
            RebuildPanel();RefreshPresentation(true);
        }
        private PlanSite PreviewPortMachine(int x,int y)=>Model.State.sites.FirstOrDefault(s=>!Model.Overlay(s.type)&&Model.Covers(s,x,y));
        private void StartPortPlacement(string mode)
        {CancelPlacement();portPlacementMode=mode;buildType="port";HideManagement();CloseContext();Notice="在机器周边格上放置接口，预览线显示绑定对象；输出沿箭头方向。";}
        private string PortPreviewProblem(int x,int y)
        {var port=new PlanSite{type="port",x=x,y=y};return Model.PortConnectionProblem(port,PreviewPortMachine(x,y),portPlacementMode);}
        private void DrawPortPreview(int x,int y)
        {
            var machine=PreviewPortMachine(x,y);if(portPreviewLink==null)portPreviewLink=LogisticsLine("Port connection preview",planWorld,73);
            portPreviewLink.gameObject.SetActive(machine!=null);if(machine==null)return;
            var z=Model.Size(machine);portPreviewLink.transform.position=Vector3.zero;portPreviewLink.transform.rotation=Quaternion.identity;portPreviewLink.positionCount=2;
            portPreviewLink.SetPositions(new[]{new Vector3(x,y),new Vector3(machine.x+(z.width-1)/2f,machine.y+(z.height-1)/2f)});
            portPreviewLink.startColor=portPreviewLink.endColor=PortPreviewProblem(x,y)==null?new Color(.3f,1,.8f):new Color(1,.4f,.3f);
        }
        private void RefreshLogisticsVisuals()
        {
            var sites=Model.State.sites.Where(s=>s.type=="belt"||s.type=="port").ToArray();var ids=new HashSet<int>(sites.Select(s=>s.id));
            foreach(int id in logisticsArrows.Keys.Where(id=>!ids.Contains(id)).ToArray()){Remove(logisticsArrows[id].gameObject);logisticsArrows.Remove(id);}
            foreach(int id in portLinks.Keys.Where(id=>!ids.Contains(id)).ToArray()){Remove(portLinks[id].gameObject);portLinks.Remove(id);}
            foreach(var s in sites)
            {
                var screen=mapCamera.WorldToViewportPoint(new Vector3(s.x,s.y,0));bool visible=showLogistics&&!MenuOpen&&mapCamera.orthographicSize<=45&&screen.x>=-.05f&&screen.x<=1.05f&&screen.y>=-.05f&&screen.y<=1.05f;
                if(!logisticsArrows.TryGetValue(s.id,out var arrow)){if(!visible)continue;logisticsArrows[s.id]=arrow=LogisticsLine("Flow direction #"+s.id,planWorld);}
                var info=Model.InspectTransport(s);Color color=!s.enabled?Color.gray:info.blocked?new Color(1,.64f,.3f):s.input.stacks.Count>0?new Color(.45f,1,.65f):new Color(.5f,.78f,.88f);
                arrow.gameObject.SetActive(visible&&(s.type=="belt"||s.portMode=="output"));
                bool paired=s.type=="belt"&&sites.Any(a=>a!=s&&a.type=="belt"&&a.x==s.x&&a.y==s.y);var offset=paired?(s.direction%2==0?new Vector3(0,-.14f):new Vector3(.14f,0)):Vector3.zero;
                PaintArrow(arrow,s.direction,new Vector3(s.x,s.y,0)+offset,color);
                if(s.type!="port")continue;var machine=Model.Site(s.portSite);
                if(!portLinks.TryGetValue(s.id,out var link)){if(!visible)continue;portLinks[s.id]=link=LogisticsLine("Port binding #"+s.id,planWorld,68);}
                link.gameObject.SetActive(visible&&machine!=null&&Model.Covers(machine,s.x,s.y));if(machine==null)continue;
                var size=Model.Size(machine);link.positionCount=2;link.SetPositions(new[]{new Vector3(s.x,s.y),new Vector3(machine.x+(size.width-1)/2f,machine.y+(size.height-1)/2f)});link.startColor=link.endColor=color;link.widthMultiplier=.04f;
            }
        }
        private void SelectTransportPeople()
        {
            var people=Model.Alive.Where(p=>p.trip!=null||p.directed||p.assigned==0&&p.maintaining==0).ToArray();
            if(people.Length==0){Notice="目前没有运输或待命人员；生产、维护人员可在居民页查看。";return;}
            ClearMapSelection();selectedPeople.AddRange(people.Select(p=>p.id));boxedPeople.AddRange(selectedPeople);OpenPeopleContext(new Vector2(Screen.width*.55f,Screen.height*.75f));
        }
    }
}
