using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public void RotateTransport(PlanSite site)
        {
            Require(site!=null&&(site.type=="belt"||site.type=="port"),"请选择传送带或接口。");
            bool paired=site.type=="belt"&&State.sites.Any(s=>s!=site&&s.type=="belt"&&s.x==site.x&&s.y==site.y);
            site.direction=(site.direction+(paired?2:1))%4;State.revision++;
        }
        public void ConfigurePort(PlanSite port,PlanSite machine,string mode,string filter=null)
        {
            ValidatePortConnection(port,machine,mode);
            port.portSite=machine.id;port.portMode=mode;port.filter=filter;State.revision++;
        }
        private (int x,int y) BeltNext(PlanSite s)
        {switch(s.direction%4){case 0:return(s.x+1,s.y);case 1:return(s.x,s.y+1);case 2:return(s.x-1,s.y);default:return(s.x,s.y-1);}}
        private PlanSite BeltTarget(PlanSite belt)
        {var next=BeltNext(belt);return State.sites.FirstOrDefault(s=>!s.pending&&s.enabled&&s.x==next.x&&s.y==next.y&&(s.type=="belt"&&(!IndependentCrossing(s)||s.direction%2==belt.direction%2)||s.type=="port"&&s.portMode=="input"));}
        private bool IndependentCrossing(PlanSite s)=>s.crossing&&State.sites.Any(other=>other!=s&&other.type=="belt"&&other.x==s.x&&other.y==s.y&&other.direction%2!=s.direction%2);
        private void TickBelts(double dt)
        {
            foreach(var port in State.sites.Where(s=>s.type=="port"&&!s.pending&&s.enabled))
            {
                var machine=Site(port.portSite);if(machine==null||machine.pending||!Covers(machine,port.x,port.y))continue;
                if(port.portMode=="output"&&port.input.stacks.Count==0)
                {
                    var source=machine.type=="warehouse"?machine.input:machine.output;
                    var stack=source.stacks.FirstOrDefault(a=>a.ticks>a.reserved&&(string.IsNullOrEmpty(port.filter)||a.item==port.filter));
                    if(stack!=null){long n=Math.Min(PlanStock.Scale,stack.ticks-stack.reserved);if(machine.type=="warehouse")n=Math.Min(n,Surplus(stack.item));if(n>0)port.input.Put(source.Take(stack.item,n));}
                }
                if(port.portMode=="input"&&port.input.stacks.Count>0)
                {var stack=port.input.stacks[0];if(AcceptsInput(machine,stack.item)&&PlanStock.CombinedUsed(Config,machine.input,machine.reserved)+Config.Item(stack.item).capacity*(Config.Item(stack.item).uses_per_item>0?Math.Ceiling(stack.ticks/(double)PlanStock.Scale):stack.ticks/(double)PlanStock.Scale)<=machine.input.capacity+1e-8)machine.input.Put(port.input.Take(stack.item,stack.ticks));}
            }
            var proposals=new List<(PlanSite from,PlanSite to)>();
            foreach(var belt in State.sites.Where(s=>(s.type=="belt"||s.type=="port"&&s.portMode=="output")&&!s.pending&&s.enabled&&s.input.stacks.Count>0))
            {
                double power=belt.type=="belt"?belt.powerRatio:1;if(power<=0)continue;belt.beltProgress+=dt*power;if(belt.beltProgress<1)continue;
                var target=BeltTarget(belt);
                if(target!=null&&target.input.stacks.Count==0&&(string.IsNullOrEmpty(target.filter)||belt.input.stacks[0].item==target.filter))proposals.Add((belt,target));
                belt.beltProgress=Math.Min(belt.beltProgress,1);
            }
            foreach(var group in proposals.GroupBy(p=>p.to.id))
            {
                var target=group.First().to;var options=group.OrderBy(p=>p.from.id).ToArray();var chosen=options.FirstOrDefault(p=>p.from.id>target.mergeTurn);if(chosen.from==null)chosen=options[0];
                var stack=chosen.from.input.stacks[0];target.input.Put(chosen.from.input.Take(stack.item,stack.ticks));chosen.from.beltProgress=0;target.mergeTurn=chosen.from.id;State.revision++;
            }
        }
    }
}
