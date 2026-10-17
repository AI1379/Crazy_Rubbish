using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeWorld
    {
        private int placingKind,placingRotation,movingId;
        private int inputActivatedFrame=-1;
        private bool placingWire,removingWire;
        private Vector2Int? wireStart;
        [SerializeField] private Transform constructionVisuals,wireVisuals;
        private Transform previewVisuals;
        private string previewKey;
        private bool drawPreview;
        private readonly Dictionary<int,SpriteRenderer> facilityViews=new Dictionary<int,SpriteRenderer>();
        private int shownConstructionRevision=-1;
        public bool ConstructionActive=>placingKind!=0||placingWire||removingWire||placingConveyor||erasingTransport||placingPortOwner!=0;
        public string ConstructionHint {get;private set;}
        public void BeginPlacement(int kind,int moveId=0)
        {
            if(game.Model.Interacting){game.Report(new ActionResult(false,"先完成或取消当前交互。"));return;}
            CancelConstruction();CancelMapDrag();game.StopWalkingForInventory();game.Model.LeaveManualRole();
            placingKind=kind;movingId=moveId;placingRotation=moveId==0?0:game.Model.GetFacility(moveId).Rotation;
            inputActivatedFrame=Time.frameCount;
            game.GetComponent<PrototypeHud>().ClosePanels();
            ConstructionHint=$"{(moveId==0?"建造":"搬迁")} {PrototypeSimulation.FacilityName(kind)} · 左键确认 · R旋转 · 右键 / Esc取消";
        }
        public void BeginWire(bool remove=false)
        {
            CancelConstruction();game.StopWalkingForInventory();game.Model.LeaveManualRole();
            placingWire=!remove;removingWire=remove;game.GetComponent<PrototypeHud>().ClosePanels();
            inputActivatedFrame=Time.frameCount;
            ConstructionHint=remove?"拆线 · 左键选择电线格 · 右键 / Esc取消":"铺线 · 左键选起点，再选终点 · 自动绕开建筑 · 每件电线4格";
        }
        public bool CancelConstruction()
        {
            bool active=ConstructionActive;ResetTransportConstruction();placingKind=movingId=placingRotation=0;placingWire=removingWire=false;wireStart=null;ConstructionHint=null;previewKey=null;
            if(previewVisuals!=null)ClearConstructionChildren(previewVisuals);return active;
        }
        private void ClearConstructionChildren(Transform parent)
        {for(int i=parent.childCount-1;i>=0;i--)RemoveView(parent.GetChild(i).gameObject);}
        private Sprite ConstructionSprite(int kind)
        {
            switch(kind){case 1:return game.appearance.woodwork;case 2:return game.appearance.dismantle;case 4:return game.appearance.workbench;case 8:return game.appearance.generator;case 64:return game.appearance.electricWoodwork;case 128:return game.appearance.automaticWoodwork;case 16:return game.appearance.housing;default:return game.appearance.streetlamp;}
        }
        private void PreviewCell(int x,int y,Color color,float size=0.92f)
        {
            if(!drawPreview)return;
            var r=Sprite("Preview cell",game.appearance.tile,x,y,1,2150);r.transform.SetParent(previewVisuals,true);
            r.transform.localScale=new Vector3(size/r.sprite.bounds.size.x,size/r.sprite.bounds.size.y,1);r.color=color;
        }
        public string PreviewPlacementAtCell(int x,int y)
        {
            if(placingKind==0)return "先选择要建设的设施。";
            if(previewVisuals==null){previewVisuals=new GameObject("Construction Preview").transform;previewVisuals.SetParent(visualRoot,false);}
            ClearConstructionChildren(previewVisuals);drawPreview=true;
            string error=game.Model.PlacementError(placingKind,x,y,placingRotation,movingId);
            var f=new FacilityInstance{Kind=placingKind,X=x,Y=y,Rotation=placingRotation};var size=PrototypeSimulation.Footprint(f);var front=PrototypeSimulation.ServiceCell(f);
            Color c=error==null?new Color(0.35f,0.8f,0.5f,0.45f):new Color(0.95f,0.3f,0.2f,0.45f);
            for(int px=x;px<x+size.width;px++)for(int py=y;py<y+size.height;py++)PreviewCell(px,py,c);
            PreviewCell(front.x,front.y,new Color(1,0.75f,0.25f,0.7f),0.6f);
            string costs=movingId!=0?"搬迁不重复收取建材":string.Join(" + ",DesignBaseline.BuildCost(placingKind).Select(p=>ItemCatalog.Name(p.Item)+p.Amount));
            ConstructionHint=$"{PrototypeSimulation.FacilityName(placingKind)} · {placingRotation*90}° · R旋转 · 右键取消\n"+(error??"位置可用 · 金色格为作业面 · 左键确认")+"\n"+costs;
            return error;
        }
        public ActionResult ConfirmPlacementAtCell(int x,int y)
        {
            if(placingKind==0)return new ActionResult(false,"先选择要建设的设施。");
            var result=movingId==0?game.Model.PlaceFacility(placingKind,x,y,placingRotation):game.Model.MoveFacility(movingId,x,y,placingRotation);
            game.Report(result);if(result.Success)CancelConstruction();return result;
        }
        public void RefreshConstructionInput(bool pointerAvailable)
        {
            if(!ConstructionActive||Time.frameCount==inputActivatedFrame)return;
            if(game.MenuOpen||game.ResultVisible||game.Model!=previousModel){CancelConstruction();return;}
            if(Input.GetKeyDown(KeyCode.Escape)||pointerAvailable&&Input.GetMouseButtonDown(1)){CancelConstruction();return;}
            if(placingKind!=0&&Input.GetKeyDown(KeyCode.R))placingRotation=(placingRotation+1)%4;
            if(previewVisuals==null){previewVisuals=new GameObject("Construction Preview").transform;previewVisuals.SetParent(visualRoot,false);}
            if(!pointerAvailable){if(previewKey!=null)ClearConstructionChildren(previewVisuals);previewKey=null;return;}
            Vector3 point=worldCamera.ScreenToWorldPoint(Input.mousePosition);int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
            string actorCells=string.Join(";",game.Model.Residents.Select(r=>$"{Mathf.FloorToInt((float)r.X+0.5f)},{Mathf.FloorToInt((float)r.Y+0.5f)}"));
            string nextKey=$"{x},{y},{placingKind},{placingRotation},{wireStart},{game.Model.ConstructionRevision},{game.Model.GroundRevision},{Mathf.FloorToInt((float)game.Model.PlayerX+0.5f)},{Mathf.FloorToInt((float)game.Model.PlayerY+0.5f)}:{actorCells}";
            drawPreview=nextKey!=previewKey;if(drawPreview){ClearConstructionChildren(previewVisuals);previewKey=nextKey;}
            if(TransportConstructionInput(x,y))return;
            if(placingKind!=0)
            {
                if(drawPreview)PreviewPlacementAtCell(x,y);
                if(Input.GetMouseButtonDown(0))ConfirmPlacementAtCell(x,y);
            }
            else if(removingWire)
            {PreviewCell(x,y,new Color(0.9f,0.3f,0.2f,0.5f));if(Input.GetMouseButtonDown(0))game.Report(game.Model.RemoveWire(x,y));}
            else
            {
                var path=wireStart.HasValue?game.Model.FindWireRoute(wireStart.Value.x,wireStart.Value.y,x,y):new List<WireCell>{new WireCell{X=x,Y=y}};
                string error=game.Model.WirePlacementError(path);
                if(path!=null)foreach(var cell in path)PreviewCell(cell.X,cell.Y,error==null?new Color(1,0.7f,0.2f,0.7f):new Color(0.9f,0.2f,0.2f,0.6f),0.24f);
                int count=path==null?0:path.Count(w=>!game.Model.Wires.Any(p=>p.X==w.X&&p.Y==w.Y));
                ConstructionHint=error??(wireStart.HasValue?$"{count} 格新线 · 余料 {game.Model.WireCredit}/4 · 左键确认终点 · 右键取消":"左键选择起点 · 电线从设备边缘接入，不能穿过机身");
                if(Input.GetMouseButtonDown(0)&&error==null)
                {if(!wireStart.HasValue)wireStart=new Vector2Int(x,y);else{game.Report(game.Model.PlaceWire(path));wireStart=null;}}
            }
        }
        private void RefreshConstructionView(PrototypeSimulation m)
        {
            if(constructionVisuals==null){constructionVisuals=new GameObject("Player Facilities").transform;constructionVisuals.SetParent(visualRoot,false);}
            if(wireVisuals==null){wireVisuals=new GameObject("Electrical Network").transform;wireVisuals.SetParent(visualRoot,false);}
            foreach(var f in m.Facilities)
            {
                if(!facilityViews.TryGetValue(f.Id,out var view)||view==null)
                {view=Sprite("Facility "+f.Id,ConstructionSprite(f.Kind),f.X,f.Y,0.92f,0);view.transform.SetParent(constructionVisuals,true);facilityViews[f.Id]=view;}
                view.gameObject.SetActive(f.Built||f.Blueprint);if(!view.gameObject.activeSelf)continue;
                var size=PrototypeSimulation.Footprint(f);float cx=f.X+(size.width-1)/2f,cy=f.Y+(size.height-1)/2f;
                // Images use bottom anchors; rotating the whole image would put the base
                // through the floor. The service-side marker communicates orientation.
                view.transform.position=new Vector3(cx,cy-0.5f,0);view.sortingOrder=SortAt(f.Y-0.5f);
                view.color=f.Blueprint?new Color(0.8f,0.85f,0.9f,0.4f):PrototypeSimulation.IsWoodMachine(f.Kind)?(f.MachineTripped?new Color(1,0.42f,0.3f):f.State==WorkState.MachineRunning?new Color(1,0.94f,0.78f):f.State==WorkState.NeedsPower?new Color(0.55f,0.6f,0.65f):Color.white):f.Kind==32?(f.PowerRatio>=0.5?new Color(1,0.92f,0.65f):new Color(0.55f,0.58f,0.62f)):Color.white;
            }
            if(shownConstructionRevision!=m.ConstructionRevision||previousModel!=m)
            {
                ClearConstructionChildren(wireVisuals);
                foreach(var w in m.Wires)
                {
                    var r=Sprite("Wire "+w.X+","+w.Y,game.appearance.tile,w.X,w.Y,1,-400);r.transform.SetParent(wireVisuals,true);
                    r.transform.localScale=new Vector3(0.2f/r.sprite.bounds.size.x,0.2f/r.sprite.bounds.size.y,1);
                    foreach(var p in m.Wires.Where(p=>p.X==w.X+1&&p.Y==w.Y||p.X==w.X&&p.Y==w.Y+1))
                    {var line=Sprite("Wire span",game.appearance.tile,(w.X+p.X)/2f,(w.Y+p.Y)/2f,1,-401);line.transform.SetParent(r.transform,true);line.transform.localScale=new Vector3((w.X==p.X?0.09f:1)/line.sprite.bounds.size.x,(w.Y==p.Y?0.09f:1)/line.sprite.bounds.size.y,1);}
                }
                foreach(var f in m.Facilities.Where(f=>f.Built||f.Blueprint))
                {var front=PrototypeSimulation.ServiceCell(f);var r=Sprite("Service "+f.Id,game.appearance.tile,front.x,front.y,1,-350);r.transform.SetParent(wireVisuals,true);r.transform.localScale=new Vector3(0.45f/r.sprite.bounds.size.x,0.12f/r.sprite.bounds.size.y,1);r.color=new Color(0.95f,0.72f,0.3f,0.8f);if(PrototypeSimulation.IsWoodMachine(f.Kind)){var side=PrototypeSimulation.MaintenanceCell(f);var mark=Sprite("Maintenance "+f.Id,game.appearance.tile,side.x,side.y,1,-349);mark.transform.SetParent(wireVisuals,true);mark.transform.localScale=new Vector3(0.45f/mark.sprite.bounds.size.x,0.12f/mark.sprite.bounds.size.y,1);mark.color=new Color(0.35f,0.8f,1,0.9f);}}
                shownConstructionRevision=m.ConstructionRevision;
            }
            foreach(Transform child in wireVisuals)
            {
                if(!child.name.StartsWith("Wire "))continue;
                var p=child.name.Substring(5).Split(',');int x=int.Parse(p[0]),y=int.Parse(p[1]);
                var c=m.WirePowerRatio(x,y)>0?new Color(1,0.78f,0.3f):new Color(0.4f,0.48f,0.55f);
                foreach(var r in child.GetComponentsInChildren<SpriteRenderer>())r.color=c;
            }
            var lamp=visualRoot.Find("CommunityLamp");if(lamp!=null){lamp.gameObject.SetActive(true);lamp.GetComponent<SpriteRenderer>().color=m.CommunityLampPowered?new Color(1,0.95f,0.7f):new Color(0.5f,0.55f,0.6f);}
            var housing=visualRoot.Find("OldHousing");if(housing!=null)housing.GetComponent<SpriteRenderer>().color=m.HousingPowered?Color.white:new Color(0.7f,0.73f,0.75f);
        }
    }
}
