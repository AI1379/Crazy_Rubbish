using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed class PlanTransportReadout
    {public string status,issue,flow;public bool blocked;public int machine,nextSite;}
    public sealed partial class PlanSimulation
    {
        public static string DirectionName(int direction)=>new[]{"东","北","西","南"}[((direction%4)+4)%4];
        public PlanTransportReadout InspectTransport(PlanSite s)
        {
            Require(s!=null&&(s.type=="belt"||s.type=="port"),"请选择传送带或接口。");
            var result=new PlanTransportReadout{flow=s.type=="port"&&s.portMode=="input"?"相邻输送带 → 机器输入":"向"+DirectionName(s.direction)+"输送"};
            Action<string,string> block=(code,message)=>{result.issue=code;result.status=message;result.blocked=true;};
            if(s.pending){block("construction","待建成，恢复经营后完成建设");return result;}
            if(!s.enabled){block("disabled","已停止输送，缓存物品保留");return result;}
            if(s.type=="port")
            {
                var machine=Site(s.portSite);result.machine=machine?.id??0;
                if(machine==null||!Covers(machine,s.x,s.y)){block("connection","接口未连接有效机器，请配置接口");return result;}
                if(machine.pending){block("construction","连接的机器尚未建成");return result;}
                if(s.portMode=="input")
                {
                    if(s.input.stacks.Count==0){result.status="输入接口等待来料";return result;}
                    var stack=s.input.stacks[0];
                    if(!AcceptsInput(machine,stack.item)){block("filter","机器不接收缓存物品，请检查配方");return result;}
                    double size=Config.Item(stack.item).capacity*(Config.Item(stack.item).uses_per_item>0?Math.Ceiling(stack.ticks/(double)PlanStock.Scale):stack.ticks/(double)PlanStock.Scale);
                    if(PlanStock.CombinedUsed(Config,machine.input,machine.reserved)+size>machine.input.capacity+1e-8){block("output","机器输入已满（含在制），等待腾出空间");return result;}
                    result.status="输入接口可向机器交付";return result;
                }
                if(s.input.stacks.Count==0)
                {
                    var stock=machine.type=="warehouse"?machine.input:machine.output;
                    var matched=stock.stacks.Where(a=>a.ticks>a.reserved&&(string.IsNullOrEmpty(s.filter)||a.item==s.filter)).ToArray();
                    result.status=matched.Length==0?"输出接口等待匹配产物":machine.type=="warehouse"&&!matched.Any(a=>Surplus(a.item)>0)?"仓库物品受生活保留量保护":"输出接口等待取货";return result;
                }
            }
            if(s.type=="belt"&&s.powerRatio<=0){block("power","传送带没有供电");return result;}
            if(s.input.stacks.Count==0){result.status="等待来料";return result;}
            var next=BeltNext(s);var target=BeltTarget(s);result.nextSite=target?.id??0;
            if(target==null)
            {
                var neighbors=State.sites.Where(a=>a.x==next.x&&a.y==next.y&&(a.type=="belt"||a.type=="port")).ToArray();
                block("connection",neighbors.Length==0?"末端未连接："+DirectionName(s.direction)+"侧缺少传送带或输入接口":neighbors.All(a=>a.pending)?"下游尚未建成":neighbors.All(a=>!a.enabled)?"下游已停止运行":"下游方向或接口模式不匹配");return result;
            }
            if(!string.IsNullOrEmpty(target.filter)&&target.filter!=s.input.stacks[0].item){block("filter","下游过滤不接收该物品");return result;}
            if(target.input.stacks.Count>0){block("output","下游缓存已满，等待疏通");return result;}
            result.status=s.beltProgress>=1?"下游可接收，等待本轮交付":"正在输送";
            if(s.type=="belt"&&s.powerRatio<1)result.status+=" · 低供电减速";return result;
        }
        public string StockLabel(string code)
        {
            if(code=="hole")return "洞口";if(code=="backpack")return "随身物品";
            var parts=(code??"").Split(':');if(parts.Length!=2||!int.TryParse(parts[1],out int id))return "目标不存在";
            if(parts[0]=="ground")return State.ground.Any(g=>g.id==id)?"地面物品 #"+id:"原垃圾堆已清空";
            var site=Site(id);if(site==null)return "设施已移除";
            return Config.Facility(site.type).name+" #"+id+(parts[0]=="tools"?"（工具）":parts[0]=="output"?"（产物）":"（输入）");
        }
        public string WorkerTask(PlanPerson p)
        {
            if(p==null||p.dead)return "人物已离开";
            string prefix=State.paused?"已暂停 · ":"";
            if(p.directed)prefix+=(Distance(p.x,p.y,p.commandX,p.commandY)<.05?"待命 · 可恢复工作":"按指令前往（"+p.commandX+","+p.commandY+"）")+" · 原工作挂起\n";
            else if(!MayWork(p))prefix+="暴动停工 · 原工作挂起\n";
            if(p.trip!=null)
            {
                var t=p.trip;return prefix+(t.picked?"送货 / 卸货：":"取货 / 装货：")+Config.Item(t.item).name+" ×"+(t.ticks/(double)PlanStock.Scale).ToString("0.##")+"\n"+StockLabel(t.source)+" → "+StockLabel(t.target)+"\n"+(t.picked?"已携带货物":"已预约货物，尚未取走");
            }
            if(p.maintaining>0)return prefix+"维护："+StockLabel("input:"+p.maintaining);
            int assigned=p.id==0?State.heroSite:p.assigned;if(assigned>0)return prefix+"操作："+StockLabel("input:"+assigned);
            return prefix+(p.directed?"可在人物菜单恢复工作":string.IsNullOrEmpty(p.activity)?"暂无工作任务":p.activity)+ (p.cargo.stacks.Count>0?" · 携货等待仓库空间":"");
        }
    }
}
