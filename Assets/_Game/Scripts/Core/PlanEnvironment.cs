using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private int pressureRevision=-1;private readonly Dictionary<int,double> groundCapacity=new Dictionary<int,double>(),exposedCapacity=new Dictionary<int,double>();private readonly Dictionary<(double x,double y),double> pressures=new Dictionary<(double x,double y),double>();
        private void RefreshGroundIndex()
        {
            if(pressureRevision==State.revision)return;pressureRevision=State.revision;groundCapacity.Clear();exposedCapacity.Clear();pressures.Clear();
            foreach(var g in State.ground){double full=g.stock.Used(Config)/g.width,exposed=g.stock.stacks.Where(s=>!Config.Item(s.item).pressure_exempt&&s.item!="ash").Sum(s=>Config.Item(s.item).capacity*s.ticks/PlanStock.Scale)/g.width;for(int x=g.x;x<g.x+g.width;x++){int cell=g.y*PlanTerrain.Width+x;groundCapacity[cell]=(groundCapacity.TryGetValue(cell,out var old)?old:0)+full;exposedCapacity[cell]=(exposedCapacity.TryGetValue(cell,out old)?old:0)+exposed;}}
        }
        public double GroundCapacity(int x,int y){RefreshGroundIndex();return groundCapacity.TryGetValue(y*PlanTerrain.Width+x,out var v)?v:0;}
        private void DropAt(PlanStack stack,int x,int y)
        {int width=Config.Item(stack.item).capacity>5?2:1;if(!Enumerable.Range(x,width).All(px=>Owned(px,y)&&TerrainBuildable(px,y))){int oldX=x,oldY=y;var spot=Enumerable.Range(1,State.region).SelectMany(PlanTerrain.Cells).Where(c=>Enumerable.Range(c.x,width).All(px=>Owned(px,c.y)&&TerrainBuildable(px,c.y))).OrderBy(c=>(c.x-oldX)*(c.x-oldX)+(c.y-oldY)*(c.y-oldY)).ThenBy(c=>c.y).ThenBy(c=>c.x).First();x=spot.x;y=spot.y;}var existing=State.ground.FirstOrDefault(g=>g.x==x&&g.y==y&&g.width==width);if(existing==null){existing=new PlanGround{id=State.nextId++,x=x,y=y,width=width};State.ground.Add(existing);}existing.stock.Put(new[]{stack});State.revision++;}
        private void SpawnGround(string item,long ticks,int region,bool guaranteed=false)
        {
            var d=Config.Item(item);var r=Config.Region(region);int width=d.capacity>5?2:1;long remaining=ticks;
            while(remaining>0)
            {
                int bx=r.garbage_rectangle[0],by=r.garbage_rectangle[1];double best=double.MaxValue;
                for(int y=r.garbage_rectangle[1];y<=r.garbage_rectangle[3];y++)for(int x=r.garbage_rectangle[0];x+width-1<=r.garbage_rectangle[2];x++)
                {double used=Enumerable.Range(x,width).Sum(px=>GroundCapacity(px,y));if(used<best){best=used;bx=x;by=y;}}
                if(width*5-best+1e-8<d.capacity)
                {
                    foreach(var area in Config.regions.Where(a=>a.id<=State.region).OrderBy(a=>a.id==region?0:1))
                    {
                        var cells=PlanTerrain.Cells(area.id).Where(c=>Enumerable.Range(c.x,width).All(px=>Owned(px,c.y)&&TerrainBuildable(px,c.y)&&!Receiving(px,c.y)&&GroundCapacity(px,c.y)+d.capacity/width<=5+1e-8&&!State.sites.Any(s=>!Overlay(s.type)&&Covers(s,px,c.y)))).ToList();
                        var found=cells.OrderBy(c=>Math.Abs(c.x-(r.garbage_rectangle[0]+r.garbage_rectangle[2])/2.0)+Math.Abs(c.y-r.garbage_rectangle[3])).ThenBy(c=>c.y).ThenBy(c=>c.x).FirstOrDefault();
                        if(cells.Count>0){bx=found.x;by=found.y;best=Enumerable.Range(bx,width).Sum(px=>GroundCapacity(px,by));break;}
                    }
                }
                long placed=d.durability>0||d.uses_per_item>0||!string.IsNullOrEmpty(d.instance_state)?PlanStock.Scale:Math.Min(remaining,Math.Max(PlanStock.Scale,PlanStock.Quantity(Math.Floor(Math.Max(0,width*5-best)/Math.Max(.1,d.capacity)))));
                placed=Math.Min(placed,remaining);
                string quality=d.quality_tag;
                if(!string.IsNullOrEmpty(d.instance_state))quality=guaranteed?"repairable":random.NextDouble()<.5?"usable":random.NextDouble()<.5?"repairable":"scrap_only";
                var stack=new PlanStack{item=item,ticks=placed,origin=region,durability=d.durability,instance=d.durability>0||d.uses_per_item>0||!string.IsNullOrEmpty(d.instance_state)?State.nextId++:0,quality=quality,composition=item=="plastic_mix"?Config.Region(region).plastic_parts_per_10:item=="organic_mix"?new[]{new PlanAmount{item="organic_feed",quantity=4},new PlanAmount{item="organic_residual",quantity=1}}:null};
                DropAt(stack,bx,by);remaining-=placed;
            }
        }
        public double Pressure(double x,double y)
        {
            RefreshGroundIndex();if(pressures.TryGetValue((x,y),out double cached))return cached;
            double radius=Config.Rule("pressure.radius"),overflow=0;int eligible=0,occupied=0;
            for(int py=(int)Math.Floor(y-radius);py<=y+radius;py++)for(int px=(int)Math.Floor(x-radius);px<=x+radius;px++)
            {
                if(Distance(x,y,px,py)>radius||px<0||py<0||px>=PlanTerrain.Width||py>=PlanTerrain.Height)continue;
                bool usable=Owned(px,py)&&TerrainBuildable(px,py)||Config.regions.Any(r=>r.id<=State.region&&InRect(px,py,r.garbage_rectangle));
                if(!usable||State.sites.Any(s=>!Overlay(s.type)&&Covers(s,px,py)))continue;eligible++;
                double exposed=exposedCapacity.TryGetValue(py*PlanTerrain.Width+px,out var value)?value:0;
                if(exposed>0){occupied++;overflow+=Math.Max(0,exposed-Config.Rule("storage.floor_cell_capacity"));}
            }
            double fraction=eligible==0?0:(double)occupied/eligible;
            double area=fraction<=.3?1:fraction<=.6?1-(fraction-.3)*(.2/.3):.8-(fraction-.6)*.5;
            double height=occupied==0?1:Math.Max(.4,1/(1+overflow/(occupied*5)));
            double result=area*height;if(pressures.Count>256)pressures.Clear();pressures[(x,y)]=result;return result;
        }
        private double Rate(double time)=>time<600?8:time<1800?12:time<3600?15:time<5400?18.75:18.75*Math.Pow(1.25,1+Math.Floor((time-5400)/1800));
        private double InputInterval(){double t=State.seconds,mean=t<600?60:t<1800?54:t<3600?48:t<5400?42:Math.Max(10,42*Math.Pow(.9,1+Math.Floor((t-5400)/1800)));return mean*(2.0/3+random.NextDouble()*2.0/3);}
        private string Weighted(PlanAmount[] weights)
        {double n=random.NextDouble()*weights.Sum(a=>a.quantity);foreach(var a in weights){n-=a.quantity;if(n<0)return a.item;}return weights.Last().item;}
        private string IncomingWreck(PlanRegion region)
        {
            if(region.id==3&&region.wreck_templates.Contains("old_structure"))
            {if(random.NextDouble()<region.critical_structure_wreck_share_of_wreck_budget)return "old_structure";var others=region.wreck_templates.Where(id=>id!="old_structure").ToArray();return others[random.Next(others.Length)];}
            return region.wreck_templates[random.Next(region.wreck_templates.Length)];
        }
        private string IncomingItem(PlanRegion r,string category)
        {
            switch(category)
            {
                case "wood":return "waste_wood";case "plastic":return "plastic_mix";case "paper":return "waste_paper";case "glass":return "waste_glass";
                case "textile":return Weighted(r.textile_subweights);case "organic":return random.NextDouble()<.8?"organic_feed":"organic_residual";
                case "life":return new[]{"food","water","soap"}[random.Next(3)];
                case "metal_devices":string metal=Weighted(r.metal_subweights);return metal=="wreck"?r.wreck_templates[random.Next(r.wreck_templates.Length)]:metal;
                default:throw new InvalidOperationException("未知来料分组："+category);
            }
        }
        private void TickEnvironment(double dt)
        {
            foreach(var spring in State.springs)spring.remaining=Math.Min(WaterMaximum(spring),spring.remaining+WaterRecharge(spring)*dt/60);
            foreach(var region in Config.regions.Where(r=>r.id<=State.region))foreach(var weight in region.category_weights)
            {
                var sub=weight.item=="metal_devices"?region.metal_subweights:weight.item=="textile"?region.textile_subweights:weight.item=="life"?new[]{new PlanAmount{item="food",quantity=100.0/3},new PlanAmount{item="water",quantity=100.0/3},new PlanAmount{item="soap",quantity=100.0/3}}:weight.item=="organic"?new[]{new PlanAmount{item="organic_feed",quantity=80},new PlanAmount{item="organic_residual",quantity=20}}:new[]{new PlanAmount{item=weight.item,quantity=100}};
                foreach(var share in sub)
                {
                    string category=weight.item=="metal_devices"&&share.item=="wreck"?"metal_wreck":weight.item=="metal_devices"||weight.item=="textile"||weight.item=="life"||weight.item=="organic"?"exact_"+share.item:share.item;
                    var b=State.budgets.FirstOrDefault(v=>v.region==region.id&&v.category==category);
                    if(b==null){b=new PlanBudget{region=region.id,category=category};State.budgets.Add(b);}
                    b.integrated+=Rate(State.seconds-dt/2)*dt/60*(region.id==1?1:region.id==2?.6:.4)*weight.quantity/100*share.quantity/100;
                }
            }
            if(State.seconds>=State.nextInput)
            {
                foreach(var b in State.budgets)
                {
                    b.capacity+=b.integrated*(.85+random.NextDouble()*.3);b.integrated=0;var r=Config.Region(b.region);
                    while(b.capacity>0)
                    {
                        if(string.IsNullOrEmpty(b.pendingItem))b.pendingItem=b.category.StartsWith("exact_")?b.category.Substring(6):b.category=="metal_wreck"?IncomingWreck(r):IncomingItem(r,b.category);
                        double capacity=Config.Item(b.pendingItem).capacity;if(b.capacity+1e-9<capacity)break;
                        b.capacity-=capacity;SpawnGround(b.pendingItem,PlanStock.Scale,b.region);b.pendingItem=null;
                    }
                }
                State.nextInput=State.seconds+InputInterval();
            }
            foreach(var market in State.markets)market.pressure*=Math.Pow(.5,dt/Config.Rule("market.pressure_half_life"));
        }
    }
}
