using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public PlanPerson Person(int id)=>id==0&&!State.player.dead?State.player:Alive.FirstOrDefault(p=>p.id==id);
        public void CommandMove(int[] ids,int x,int y)
        {
            Require(!State.riot.active,"暴动期间不能指挥人员移动。");
            Require(Walkable(x,y),"目标是障碍、深水或未开放土地，无法移动。");
            Require(ids!=null,"请选择在世人物。");Require(!HeroAboard||!ids.Contains(0),"主角正在返程，无法离开升降舱。");
            var people=ids.Distinct().Select(Person).ToArray();Require(people.Length>0&&people.All(p=>p!=null),"请选择在世人物。");
            int radius=(int)Math.Ceiling(Math.Sqrt(people.Length))+1;
            var cells=Enumerable.Range(y-radius,radius*2+1).SelectMany(cy=>Enumerable.Range(x-radius,radius*2+1).Select(cx=>(x:cx,y:cy)))
                .Where(c=>Walkable(c.x,c.y)).OrderBy(c=>(c.x-x)*(c.x-x)+(c.y-y)*(c.y-y)).ThenBy(c=>c.y).ThenBy(c=>c.x).ToList();
            var orders=new List<(PlanPerson person,int x,int y)>();
            foreach(var p in people)
            {
                Require(Path(p.x,p.y,x,y)!=null,"选中人物无法抵达目标，请重新选择位置。");
                int index=cells.FindIndex(c=>Path(p.x,p.y,c.x,c.y)!=null);Require(index>=0,"选中人物无法抵达目标附近，请重新选择位置。");
                var cell=cells[index];cells.RemoveAt(index);orders.Add((p,cell.x,cell.y));
            }
            // Validate the entire group before changing any existing order or reservation.
            foreach(var order in orders)
            {
                var p=order.person;p.directed=true;p.commandX=order.x;p.commandY=order.y;p.activity="按指令移动";
                if(p.id==0)
                {
                    State.heroSite=0;
                    if(p.trip!=null){Stock(p.trip.source)?.Release(p.trip.item,p.trip.ticks,p.trip.instance);p.trip=null;}
                }
            }
            State.revision++;
        }
        public void ResumePeople(int[] ids)
        {foreach(int id in ids.Distinct()){var p=Person(id);if(p==null)continue;p.directed=false;p.nextSeek=0;p.activity="恢复工作";}State.revision++;}
        private void TickCommands(double dt)
        {
            foreach(var p in Alive.Concat(new[]{State.player}).Where(p=>p.directed&&!p.dead))
            {
                if(State.riot.active){p.activity="暴动停工";continue;}
                if(Distance(p.x,p.y,p.commandX,p.commandY)<.05){p.activity="待命 · 可恢复工作";continue;}
                p.activity=Walk(p,p.commandX,p.commandY,dt)?"按指令移动":"路径受阻 · 等待新指令";
            }
        }
    }
}
