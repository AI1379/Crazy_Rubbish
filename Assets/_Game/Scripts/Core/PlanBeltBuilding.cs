using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed class PlanBeltCell {public int x,y,direction,existing;public string problem;}
    public sealed class PlanBeltRoute
    {public PlanBeltCell[] cells;public PlanAmount[] costs;public int newCount;public string problem;public bool Valid=>problem==null;}
    public sealed partial class PlanSimulation
    {
        public static PlanBeltCell[] BeltPath(int x0,int y0,int x1,int y1,bool verticalFirst,int endDirection=0)
        {
            Require(x0>=0&&x0<PlanTerrain.Width&&x1>=0&&x1<PlanTerrain.Width&&y0>=0&&y0<PlanTerrain.Height&&y1>=0&&y1<PlanTerrain.Height,"铺设范围须在地图内。");
            var cells=new List<PlanBeltCell>{new PlanBeltCell{x=x0,y=y0}};int x=x0,y=y0;
            Action horizontal=()=>{while(x!=x1){x+=Math.Sign(x1-x);cells.Add(new PlanBeltCell{x=x,y=y});}};
            Action vertical=()=>{while(y!=y1){y+=Math.Sign(y1-y);cells.Add(new PlanBeltCell{x=x,y=y});}};
            if(verticalFirst){vertical();horizontal();}else{horizontal();vertical();}
            for(int i=0;i<cells.Count-1;i++)cells[i].direction=cells[i+1].x>cells[i].x?0:cells[i+1].y>cells[i].y?1:cells[i+1].x<cells[i].x?2:3;
            cells[cells.Count-1].direction=cells.Count>1?cells[cells.Count-2].direction:((endDirection%4)+4)%4;return cells.ToArray();
        }
        public PlanBeltRoute InspectBeltRoute(int x0,int y0,int x1,int y1,bool verticalFirst=false,bool crossing=false,int endDirection=0)
        {
            var result=new PlanBeltRoute{cells=new PlanBeltCell[0],costs=new PlanAmount[0]};
            try{result.cells=BeltPath(x0,y0,x1,y1,verticalFirst,endDirection);}catch(InvalidOperationException e){result.problem=e.Message;return result;}
            for(int i=0;i<result.cells.Length;i++)
            {
                var cell=result.cells[i];var occupants=State.sites.Where(s=>s.x==cell.x&&s.y==cell.y).ToArray();
                var port=occupants.FirstOrDefault(s=>s.type=="port");
                if(port!=null&&(i==0&&port.portMode=="output"&&port.direction==cell.direction||i==result.cells.Length-1&&port.portMode=="input"))cell.existing=port.id;
                else
                {
                    var same=occupants.FirstOrDefault(s=>s.type=="belt"&&s.direction==cell.direction);
                    if(same!=null)cell.existing=same.id;
                    else {cell.problem=BuildProblem("belt",cell.x,cell.y,cell.direction,crossing);result.newCount++;}
                }
                if(i>0&&result.cells[i-1].direction%2!=cell.direction%2&&occupants.Any(s=>s.type=="belt"&&s.direction%2!=cell.direction%2)&&(crossing||occupants.Any(s=>s.crossing)))cell.problem="独立交叉处不能转弯，请在旁格转向或绕行。";
                if(cell.problem!=null&&result.problem==null)result.problem="（"+cell.x+","+cell.y+"）"+cell.problem;
            }
            result.costs=Config.Facility("belt").build.Select(a=>new PlanAmount{item=a.item,quantity=a.quantity*result.newCount}).ToArray();
            var missing=result.costs.Where(a=>Stored(a.item)+State.backpack.Count(a.item)<a.Ticks).ToArray();
            if(result.problem==null&&missing.Length>0)result.problem="整段建材不足："+string.Join("、",missing.Select(a=>Config.Item(a.item).name+"缺"+((a.Ticks-Stored(a.item)-State.backpack.Count(a.item))/(double)PlanStock.Scale).ToString("0.##")));
            return result;
        }
        public PlanSite[] BuildBeltRoute(int x0,int y0,int x1,int y1,bool verticalFirst=false,bool crossing=false,int endDirection=0)
        {
            var route=InspectBeltRoute(x0,y0,x1,y1,verticalFirst,crossing,endDirection);Require(route.Valid,route.problem);
            // All cells and the combined bill are checked before any stock or facility is changed.
            return route.cells.Select(c=>c.existing>0?Site(c.existing):Build("belt",c.x,c.y,c.direction,crossing)).ToArray();
        }
        private void NormalizeBeltCrossings()
        {
            foreach(var group in State.sites.Where(s=>s.type=="belt").GroupBy(s=>(s.x,s.y)))
                if(group.Select(s=>s.direction%2).Distinct().Count()>1)foreach(var belt in group)belt.crossing=true;
        }
        public PlanSite PortMachineAt(int x,int y,string mode)
        {
            var candidate=new PlanSite{type="port",x=x,y=y};
            return State.sites.FirstOrDefault(s=>!Overlay(s.type)&&PortConnectionProblem(candidate,s,mode)==null);
        }
        public string PortConnectionProblem(PlanSite port,PlanSite machine,string mode)
        {try{ValidatePortConnection(port,machine,mode);return null;}catch(InvalidOperationException e){return e.Message;}}
        private void ValidatePortConnection(PlanSite port,PlanSite machine,string mode)
        {
            Require(port!=null&&machine!=null&&port.type=="port"&&!Overlay(machine.type),"将接口放在机器或仓库的周边格上。");
            Require(mode=="input"||mode=="output","接口需选择输入或输出。");var z=Size(machine);
            Require(Covers(machine,port.x,port.y)&&(port.x==machine.x||port.y==machine.y||port.x==machine.x+z.width-1||port.y==machine.y+z.height-1),"接口须在机器周边格上。");
            Require(State.sites.Count(p=>p!=port&&p.type=="port"&&p.portSite==machine.id)<Config.Facility(machine.type).ports,"该机器接口数量已满。");
        }
    }
}
