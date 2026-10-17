using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeWorld
    {
        private bool placingConveyor,erasingTransport,beltDirectionChosen;
        private int conveyorDirection,placingPortOwner;
        private bool placingPortOutput;
        private Vector2Int? conveyorStart;
        [SerializeField] private Transform transportVisuals;
        private readonly Dictionary<int,SpriteRenderer> laneViews=new Dictionary<int,SpriteRenderer>(),portViews=new Dictionary<int,SpriteRenderer>(),cargoViews=new Dictionary<int,SpriteRenderer>();
        private readonly Dictionary<int,Transform> directionViews=new Dictionary<int,Transform>();
        public void BeginConveyor(bool erase=false)
        {
            CancelConstruction();CancelMapDrag();game.StopWalkingForInventory();game.Model.LeaveManualRole();
            erasingTransport=erase;placingConveyor=!erase;inputActivatedFrame=Time.frameCount;game.GetComponent<PrototypeHud>().ClosePanels();
            ConstructionHint=erase?"左键拆输送设施；交叉格按R选择轴向；右键取消":"传送带：左键选择起点和终点，R调整末端方向；木板1+钢铁零件1铺2格";
        }
        public void BeginPort(int ownerId,bool output)
        {
            CancelConstruction();CancelMapDrag();game.StopWalkingForInventory();game.Model.LeaveManualRole();
            placingPortOwner=ownerId;placingPortOutput=output;inputActivatedFrame=Time.frameCount;game.GetComponent<PrototypeHud>().ClosePanels();
            ConstructionHint="在所属设施外沿选择独立"+(output?"输出":"输入")+"接口；保留人员作业与维护侧；钢铁零件1";
        }
        private void ResetTransportConstruction()
        {placingConveyor=erasingTransport=beltDirectionChosen=false;conveyorStart=null;conveyorDirection=0;placingPortOwner=0;}
        private bool TransportConstructionInput(int x,int y)
        {
            if(!placingConveyor&&!erasingTransport&&placingPortOwner==0)return false;
            if(Input.GetKeyDown(KeyCode.R)){conveyorDirection=(conveyorDirection+1)%4;beltDirectionChosen=true;previewKey=null;}
            if(placingPortOwner!=0)
            {
                string error=game.Model.PortPlacementError(placingPortOwner,x,y);
                PreviewCell(x,y,error==null?new Color(0.3f,0.85f,0.6f,0.65f):new Color(0.95f,0.25f,0.2f,0.6f));
                ConstructionHint=(placingPortOutput?"输出接口":"输入接口")+" · 钢铁零件1 · 右键取消\n"+(error??"贴合所属设施，外侧一格连接传送带；左键确认");
                if(Input.GetMouseButtonDown(0))
                {var result=game.Model.PlacePort(placingPortOwner,x,y,placingPortOutput);game.Report(result);if(result.Success)CancelConstruction();}
            }
            else if(erasingTransport)
            {
                PreviewCell(x,y,new Color(0.95f,0.3f,0.2f,0.6f));
                if(Input.GetMouseButtonDown(0))
                {
                    var p=game.Model.ConveyorPorts.FirstOrDefault(a=>a.X==x&&a.Y==y);
                    var b=game.Model.Conveyors.Where(a=>a.X==x&&a.Y==y).OrderBy(a=>a.Direction%2==conveyorDirection%2?0:1).FirstOrDefault();
                    game.Report(p!=null?game.Model.RemovePort(p.Id):b!=null?game.Model.RemoveConveyor(b.Id):new ActionResult(false,"该格没有输送设施。"));
                }
            }
            else
            {
                var path=conveyorStart.HasValue?game.Model.FindConveyorRoute(conveyorStart.Value.x,conveyorStart.Value.y,x,y,beltDirectionChosen?conveyorDirection:-1):new List<ConveyorLane>{new ConveyorLane{X=x,Y=y,Direction=conveyorDirection}};
                string error=game.Model.ConveyorPlacementError(path);
                if(path!=null)foreach(var b in path)PreviewCell(b.X,b.Y,error==null?new Color(0.9f,0.65f,0.2f,0.65f):new Color(0.95f,0.2f,0.2f,0.6f),0.68f);
                ConstructionHint=error??$"{path.Count} 格 · 末端{new[]{"向右","向上","向左","向下"}[path.Last().Direction]} · R改方向\n"+(conveyorStart.HasValue?"左键确认；交叉须直行，接口外侧留一格传送带。":"左键选起点，再选终点；每格缓存1个真实物品。");
                if(Input.GetMouseButtonDown(0)&&error==null)
                {if(!conveyorStart.HasValue)conveyorStart=new Vector2Int(x,y);else{var result=game.Model.PlaceConveyor(path);game.Report(result);if(result.Success){conveyorStart=null;beltDirectionChosen=false;}}}
            }
            return true;
        }
        private void ResetTransportView()
        {
            if(transportVisuals!=null)ClearConstructionChildren(transportVisuals);laneViews.Clear();portViews.Clear();cargoViews.Clear();directionViews.Clear();
        }
        private SpriteRenderer TransportSprite(string name,Sprite sprite,int x,int y,float size,int order)
        {
            var r=Sprite(name,sprite,x,y,1,order);r.transform.SetParent(transportVisuals,true);
            r.transform.localScale=Vector3.one*(size/Mathf.Max(0.01f,Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y)));return r;
        }
        private Transform BeltArrow(int id)
        {
            var arrow=new GameObject("Belt Direction "+id).transform;arrow.SetParent(transportVisuals,false);
            foreach(var segment in new[]{new Vector4(-0.18f,0,0.45f,0),new Vector4(0.12f,0.12f,0.21f,-45),new Vector4(0.12f,-0.12f,0.21f,45)})
            {
                var part=TransportSprite("Direction",game.appearance.tile,0,0,1,-245);part.transform.SetParent(arrow,false);part.transform.localPosition=new Vector3(segment.x,segment.y,0);part.transform.localRotation=Quaternion.Euler(0,0,segment.w);
                part.transform.localScale=new Vector3(segment.z/part.sprite.bounds.size.x,0.055f/part.sprite.bounds.size.y,1);part.color=new Color(1,0.83f,0.4f);
            }
            return arrow;
        }
        private void RefreshTransportView(PrototypeSimulation m)
        {
            if(transportVisuals==null){transportVisuals=new GameObject("Conveyors and Ports").transform;transportVisuals.SetParent(visualRoot,false);}
            foreach(int id in laneViews.Keys.Where(id=>m.GetConveyor(id)==null).ToArray())
            {RemoveView(laneViews[id].gameObject);laneViews.Remove(id);if(cargoViews.ContainsKey(id)){RemoveView(cargoViews[id].gameObject);cargoViews.Remove(id);}if(directionViews.ContainsKey(id)){RemoveView(directionViews[id].gameObject);directionViews.Remove(id);}}
            foreach(var b in m.Conveyors)
            {
                bool cross=m.Conveyors.Count(a=>a.X==b.X&&a.Y==b.Y)>1;
                if(!laneViews.TryGetValue(b.Id,out var view)||view==null){view=TransportSprite("Conveyor "+b.Id,cross?game.appearance.conveyorCrossing:game.appearance.conveyor,b.X,b.Y,0.91f,-260);laneViews[b.Id]=view;directionViews[b.Id]=BeltArrow(b.Id);}
                view.sprite=cross?game.appearance.conveyorCrossing:game.appearance.conveyor;view.transform.rotation=Quaternion.Euler(0,0,cross?0:(b.Direction-1)*90);
                view.color=b.Blueprint?new Color(0.7f,0.8f,0.9f,0.35f):!b.Enabled?new Color(0.5f,0.5f,0.5f):b.PowerRatio>0?Color.white:new Color(0.55f,0.6f,0.65f);
                var arrow=directionViews[b.Id];arrow.position=new Vector3(b.X,b.Y,0);arrow.rotation=Quaternion.Euler(0,0,b.Direction*90);arrow.gameObject.SetActive(!b.Blueprint&&!cross);
                if(!cargoViews.TryGetValue(b.Id,out var cargo)||cargo==null){cargo=TransportSprite("Belt Cargo "+b.Id,game.appearance.plank,b.X,b.Y,0.42f,0);cargoViews[b.Id]=cargo;}
                cargo.gameObject.SetActive(b.Item>=0&&!b.Blueprint);if(b.Item>=0)
                {cargo.sprite=game.appearance.ItemSprite((ItemId)b.Item);cargo.transform.localScale=Vector3.one*(0.42f/Mathf.Max(cargo.sprite.bounds.size.x,cargo.sprite.bounds.size.y));cargo.transform.position=new Vector3(b.X+(cross?(b.Direction%2==0?-0.14f:0.14f):0),b.Y,0);cargo.sortingOrder=SortAt(b.Y)+3;}
            }
            foreach(int id in portViews.Keys.Where(id=>m.GetConveyorPort(id)==null).ToArray()){RemoveView(portViews[id].gameObject);portViews.Remove(id);}
            foreach(var p in m.ConveyorPorts)
            {
                if(!portViews.TryGetValue(p.Id,out var view)||view==null){view=TransportSprite("Port "+p.Id,p.Output?game.appearance.outputPort:game.appearance.inputPort,p.X,p.Y,0.8f,SortAt(p.Y));portViews[p.Id]=view;}
                view.color=p.Blueprint?new Color(0.7f,0.8f,0.9f,0.35f):p.OwnerId==0?new Color(0.95f,0.35f,0.25f):!p.Enabled?Color.gray:Color.white;
            }
        }
        private bool InspectTransportAt(Vector2 point)
        {
            int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);var m=game.Model;
            var p=m.ConveyorPorts.FirstOrDefault(a=>a.X==x&&a.Y==y);var b=m.Conveyors.FirstOrDefault(a=>a.X==x&&a.Y==y);
            if(p==null&&b==null)return false;game.GetComponent<PrototypeHud>().ShowTransport(b?.Id??0,p?.Id??0);return true;
        }
    }
}
