using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private Dictionary<int,int> Network(string lineType)
        {
            var parent=new Dictionary<int,int>();Func<int,int> root=null;root=n=>{if(!parent.ContainsKey(n))parent[n]=n;return parent[n]==n?n:parent[n]=root(parent[n]);};
            Action<int,int> join=(a,b)=>{a=root(a);b=root(b);if(a!=b)parent[a]=b;};
            var lines=State.sites.Where(s=>s.type==lineType&&!s.pending).ToArray();var cells=new HashSet<int>(lines.Select(s=>s.y*PlanTerrain.Width+s.x));
            foreach(var line in lines){int at=line.y*PlanTerrain.Width+line.x;root(at);foreach(int n in new[]{at-PlanTerrain.Width,at+PlanTerrain.Width,at-1,at+1})if(cells.Contains(n)&&Math.Abs(n%PlanTerrain.Width-at%PlanTerrain.Width)+Math.Abs(n/PlanTerrain.Width-at/PlanTerrain.Width)==1)join(at,n);}
            foreach(var site in State.sites.Where(s=>!s.pending&&s.type!=lineType))
            {int node=-1-site.id;root(node);var f=Front(site);foreach(var line in lines)if(Distance(f.x,f.y,line.x,line.y)<=1.01||Covers(site,line.x,line.y))join(node,line.y*PlanTerrain.Width+line.x);}
            return State.sites.Where(s=>!s.pending).ToDictionary(s=>s.id,s=>root(s.type==lineType?s.y*PlanTerrain.Width+s.x:-1-s.id));
        }
        private bool Fuel(PlanSite s,double dt,double woodSeconds,double chipsSeconds)
        {
            if(s.ashBudget>=20-1e-8&&!s.output.Fits(Config,"ash",PlanStock.Scale))return false;
            if(s.fuelSeconds<=0)
            {
                string fuel=s.input.Count("wood_chips")>=PlanStock.Scale?"wood_chips":s.input.Count("waste_wood")>=PlanStock.Scale?"waste_wood":null;
                if(fuel==null)return false;s.input.Take(fuel,PlanStock.Scale);s.fuelSeconds=fuel=="waste_wood"?woodSeconds:chipsSeconds;s.activeFuelRate=(fuel=="waste_wood"?1:.5)/s.fuelSeconds;
            }
            double elapsed=Math.Min(dt,s.fuelSeconds);s.fuelSeconds-=elapsed;s.ashBudget+=elapsed*s.activeFuelRate;
            while(s.ashBudget>=20&&s.output.Fits(Config,"ash",PlanStock.Scale)){s.ashBudget-=20;s.output.Add("ash",PlanStock.Scale);}return elapsed>0;
        }
        private double Request(PlanSite s)
        {
            if(!s.enabled||s.pending||s.stopped)return 0;
            if(s.type=="housing"||s.type=="streetlamp"||s.type=="belt")return Config.Facility(s.type).power;
            if(s.type=="grow_lamp")return State.sites.Any(b=>b.id==s.lampBed&&b.active&&Config.Recipe(b.recipe).stages[b.stage].kind=="lit_growth")?Config.Facility(s.type).power:0;
            if(!s.active||string.IsNullOrEmpty(s.recipe))return 0;var r=Config.Recipe(s.recipe);return r.stages[s.stage].kind=="machine"?r.power:0;
        }
        private void ComputePower(double dt)
        {
            var groups=Network("power_line");var steam=Network("steam_pipe");var net=State.sites.Where(s=>!s.pending).GroupBy(s=>groups[s.id]).ToArray();
            var stations=new List<(PlanSite boiler,PlanSite turbine,PlanSite condenser)>();var used=new HashSet<int>();
            foreach(var boiler in State.sites.Where(s=>s.type=="boiler"&&s.enabled&&!s.pending&&s.durability>0))
            {
                var turbine=State.sites.FirstOrDefault(s=>s.type=="turbine"&&!s.pending&&!used.Contains(s.id)&&steam[s.id]==steam[boiler.id]&&s.durability>0&&s.enabled);
                var condenser=State.sites.FirstOrDefault(s=>s.type=="condenser"&&!s.pending&&!used.Contains(s.id)&&steam[s.id]==steam[boiler.id]&&s.durability>0&&s.enabled);
                int pipes=State.sites.Count(s=>s.type=="steam_pipe"&&steam[s.id]==steam[boiler.id]);
                if(turbine!=null&&condenser!=null&&pipes<=12){used.Add(turbine.id);used.Add(condenser.id);stations.Add((boiler,turbine,condenser));}
            }
            foreach(var group in net)
            {
                double demand=group.Sum(Request);var linked=stations.Where(a=>groups[a.turbine.id]==group.Key).ToArray();demand+=linked.Count(a=>a.boiler.thermalStartup<20&&a.boiler.input.Count("water")>=PlanStock.Quantity(8));
                double output=0;if(demand>0)
                    foreach(var source in group.Where(s=>s.type=="generator"&&s.enabled))if(Fuel(source,dt,120,60)){output+=4;State.recognized=true;}
                foreach(var station in linked)
                {
                    var b=station.boiler;
                    if(b.thermalStartup<20)
                    {
                        if(b.input.Count("water")<PlanStock.Quantity(8)||output<=0)continue;b.thermalStartup+=dt*Math.Min(1,output/Math.Max(1,demand));
                        if(b.thermalStartup>=20)b.input.Take("water",PlanStock.Quantity(8));
                    }
                    else if(demand>0)
                    {
                        if(b.coolantTime>=60){if(b.input.Count("water")<PlanStock.Quantity(.1))continue;b.input.Take("water",PlanStock.Quantity(.1));b.coolantTime-=60;}
                        if(Fuel(b,dt,60,30)){output+=15;b.coolantTime+=dt;foreach(var part in new[]{b,station.turbine,station.condenser})part.durability=Math.Max(0,part.durability-.2*dt);}
                    }
                }
                double ratio=demand>0?Math.Min(1,output/demand):0;foreach(var s in group)s.powerRatio=ratio;
            }
        }
    }
}
