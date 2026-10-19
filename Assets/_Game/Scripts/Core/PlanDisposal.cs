using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public bool CanDiscard(string item)
        {
            if(item.Contains("water")||item=="lift_cabin"||item.StartsWith("lift_")||item=="reclaimed_structure"||item=="mined_steel_billet"||item=="mined_copper_billet"||item=="ceramic_insulator")return false;
            return item.StartsWith("waste_")||item.StartsWith("old_")||item.StartsWith("broken_")||item.Contains("residue")||item=="plastic_mix"||item=="plastic_other"||item=="ash"||item=="organic_mix"||item=="organic_residual"||item=="spent_substrate"||item=="sludge"||item=="mine_tailings"||item.StartsWith("pack_")||State.discarded.Contains(item);
        }
        public void MarkDiscard(string item){Require(!item.Contains("water")&&!item.StartsWith("lift_")&&item!="reclaimed_structure"&&!item.StartsWith("mined_")&&item!="ceramic_insulator","该物品不能作为普通弃料倾倒。");if(!State.discarded.Contains(item))State.discarded.Add(item);}
        private double DisposalVolume(PlanStack stack){var d=Config.Item(stack.item);return (d.disposal_capacity>0?d.disposal_capacity:d.capacity)*stack.ticks/PlanStock.Scale;}
        public void DumpBackpack(string item)
        {
            Require(!State.paused&&!State.riot.active,"暂停或暴动期间人工倾倒停止。");Require(Distance(State.player.x,State.player.y,2,19)<=1.5,"先到洞口外侧。");Require(CanDiscard(item),"先标记普通材料为弃料；生活水和关键返程材料不可倾倒。");Require(State.disposalHeld.stacks.Count==0,"先完成当前处置批次。");
            var d=Config.Item(item);long amount=Math.Min(State.backpack.Count(item),PlanStock.Quantity(10/(d.disposal_capacity>0?d.disposal_capacity:d.capacity)));Require(amount>0,"背包没有该弃料。");
            if(d.payload.Length>0||d.durability>0||item.StartsWith("broken_")){amount=amount/PlanStock.Scale*PlanStock.Scale;Require(amount>0,"实体容量超过单批限制，请先拆解或解包。");}
            State.disposalHeld.Put(State.backpack.Take(item,amount));State.disposalSite=-1;State.disposalWorker=0;State.heroSite=-1;State.disposalProgress=0;State.disposalCapacity=State.disposalHeld.stacks.Sum(DisposalVolume);
        }
        private void TickDisposal(double dt)
        {
            if(State.riot.active)return;
            if(State.disposalHeld.stacks.Count>0&&State.disposalWorker>0&&!Alive.Any(p=>p.id==State.disposalWorker)){CancelDisposal(State.disposalSite);return;}
            if(State.disposalHeld.stacks.Count==0)
            {
                var site=State.sites.FirstOrDefault(s=>s.type=="landfill"&&s.enabled&&!s.pending&&s.landfilled<Config.Facility(s.type).capacity&&s.input.stacks.Any(a=>CanDiscard(a.item)));
                var stock=site==null?State.holeInput:site.input;var first=stock.stacks.FirstOrDefault(s=>CanDiscard(s.item)&&s.ticks>s.reserved);if(first==null)return;
                var point=site==null?(x:2,y:19):Front(site);var worker=Alive.FirstOrDefault(p=>!p.directed&&p.maintaining==0&&p.trip==null&&(p.assigned==0||p.assigned==-1));
                bool hero=site==null?State.heroSite==-1:State.heroSite==site.id;
                if(!hero&&worker==null)return;
                if(!hero){worker.assigned=-1;Walk(worker,point.x,point.y,dt);worker.activity="处置弃料";if(Distance(worker.x,worker.y,point.x,point.y)>.1)return;}
                else if(Distance(State.player.x,State.player.y,point.x,point.y)>.1)return;
                var d=Config.Item(first.item);double volume=d.disposal_capacity>0?d.disposal_capacity:d.capacity,max=site==null?10:Math.Min(10,Config.Facility(site.type).capacity-site.landfilled);
                long n=Math.Min(first.ticks-first.reserved,PlanStock.Quantity(max/volume));if(d.payload.Length>0||d.durability>0||first.item.StartsWith("broken_"))n=n/PlanStock.Scale*PlanStock.Scale;if(n==0)return;
                State.disposalHeld.Put(stock.Take(first.item,n));State.disposalSite=site?.id??-1;State.disposalWorker=hero?0:worker.id;State.disposalProgress=0;State.disposalCapacity=State.disposalHeld.stacks.Sum(DisposalVolume);
            }
            var p=State.disposalWorker==0?State.player:Alive.FirstOrDefault(a=>a.id==State.disposalWorker);if(p==null||p.directed)return;
            var location=State.disposalSite<0?(x:2,y:19):Front(Site(State.disposalSite));if(Distance(p.x,p.y,location.x,location.y)>.1)return;
            State.disposalProgress+=dt*Efficiency(p);if(State.disposalProgress+1e-8<State.disposalCapacity)return;
            if(State.disposalSite<0)
            {State.dumped+=State.disposalCapacity;if(State.invasionAt<0&&State.dumped>=Config.Rule("disposal.dump_threshold_capacity"))State.invasionAt=State.seconds+Config.Rule("disposal.invasion_delay_seconds");}
            else{var site=Site(State.disposalSite);if(site!=null){site.landfilled+=State.disposalCapacity;site.permanent=true;}}
            State.disposalHeld.stacks.Clear();State.disposalProgress=0;if(p.id>0)p.assigned=0;State.revision++;
        }
        private void CancelDisposal(int site)
        {
            if(State.disposalSite!=site||State.disposalHeld.stacks.Count==0)return;var target=site<0?(x:2,y:19):Front(Site(site));
            foreach(var item in State.disposalHeld.stacks)DropAt(item,target.x,target.y);State.disposalHeld.stacks.Clear();State.disposalProgress=0;var worker=Alive.FirstOrDefault(p=>p.id==State.disposalWorker);if(worker!=null)worker.assigned=0;State.revision++;
        }
    }
}
