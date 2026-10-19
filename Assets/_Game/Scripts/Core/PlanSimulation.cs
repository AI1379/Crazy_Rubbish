using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public readonly PlanConfig Config;public PlanState State {get;private set;}
        private SerializableRandom random;
        public PlanSimulation(PlanConfig config,int seed=4271,PlanState restored=null)
        {
            Config=config;Config.Validate();
            if(restored!=null)
            {
                Require(restored.schema==1&&restored.version==config.version,"这是旧版存档；旧文件保留，请开启新版新游戏。");
                State=restored;random=new SerializableRandom(1){State=State.rng};
                foreach(var disabled in State.sites.Where(s=>!s.enabled)){ReleaseOperators(disabled);if(State.heroSite==disabled.id)State.heroSite=0;disabled.status="已停工";}
                foreach(var person in State.people.Concat(new[]{State.player}))if(person.trip!=null&&(string.IsNullOrEmpty(person.trip.source)||string.IsNullOrEmpty(person.trip.target)))person.trip=null;
                State.sites.First(s=>s.community&&s.type=="warehouse").input=State.warehouse;
                RestoreTerrainClearings();RestoreWaterSources();NormalizeBeltCrossings();RestoreProgression();
                if(State.mapRevision<PlanTerrain.Revision){foreach(var person in State.people.Concat(new[]{State.player}).Where(p=>!Owned((int)Math.Round(p.x),(int)Math.Round(p.y)))){var place=PlanTerrain.NearestOpen(person.x,person.y,State.region);person.x=place.x;person.y=place.y;}State.mapRevision=PlanTerrain.Revision;State.revision++;}
                return;
            }
            State=new PlanState{version=config.version,rng=(uint)seed,mapRevision=PlanTerrain.Revision};random=new SerializableRandom(seed);
            var warehouse=Community("warehouse",11,2);warehouse.input=State.warehouse;
            Community("housing",16,2);Community("streetlamp",20,5);
            UnlockRegion(1);State.nextInput=InputInterval();
        }
        private PlanSite Community(string type,int x,int y)
        {var s=new PlanSite{id=State.nextId++,type=type,x=x,y=y,community=true};State.sites.Add(s);return s;}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public IEnumerable<PlanPerson> Alive=>State.people.Where(p=>!p.dead);
        public IEnumerable<PlanStock> Warehouses=>State.sites.Where(s=>s.type=="warehouse"&&!s.pending).Select(s=>s.input);
        public int Population=>Alive.Count()+1;
        public int Beds=>6+State.sites.Where(s=>s.type=="housing"&&!s.community&&!s.pending).Sum(s=>Config.Facility(s.type).beds);
        public long Debt=>State.bills.Sum(b=>b.unpaid.Sum());
        private double DebtCycles=>State.bills.Sum(b=>(double)b.unpaid.Sum()/b.original);
        public long Stored(string id)=>Warehouses.Sum(s=>s.Count(id));
        public long Protected(string id)=>id=="water"?PlanStock.Quantity(State.waterReserveCycles*Population):id=="food"?PlanStock.Quantity(State.foodReserveCycles*Population):id=="seed_tuber"?PlanStock.Quantity(State.sites.Count(s=>s.type=="growing")):0;
        public long Surplus(string id)=>Math.Max(0,Stored(id)-Protected(id));
        public PlanSite Site(int id)=>State.sites.FirstOrDefault(s=>s.id==id);
        public void Discover(string id)
        {
            if(!State.acquired.Contains(id))State.acquired.Add(id);
            bool changed;
            do{changed=false;foreach(var t in Config.technologies)
                if(!State.technologies.Contains(t.id)&&t.region_required<=State.region&&t.parents.All(State.technologies.Contains)&&t.acquired_all.All(State.acquired.Contains)&&(t.acquired_any.Length==0||t.acquired_any.Any(State.acquired.Contains)))
                {State.technologies.Add(t.id);changed=true;}}
            while(changed);
        }
        public bool RecipeAvailable(PlanRecipe r)=>r.region_required<=State.region&&r.technologies.All(State.technologies.Contains);
        public void BuyRegion(int id)
        {
            Require(id==State.region+1&&id<=3,"请按顺序开放相邻地区。");
            var r=Config.Region(id);Require(State.cash>=Cents(r.purchase_cash),"扩地资金不足。");State.cash-=Cents(r.purchase_cash);UnlockRegion(id);
        }
        private void UnlockRegion(int id)
        {
            State.region=id;var r=Config.Region(id);State.springs.Add(new PlanSpringState{region=id,body=id==1?PlanTerrain.StarterPondBody:0,remaining=r.freshwater_source.initial_liters});
            foreach(var a in r.fixed_items)SpawnGround(a.item,a.Ticks,id,true);Discover("freshwater_source");State.revision++;
        }
        public static long Cents(double value)=>(long)Math.Round(value*100,MidpointRounding.AwayFromZero);
        public bool Owned(int x,int y)=>PlanTerrain.Available(x,y,State.region)||TerrainCleared(x,y)&&PlanTerrain.ChamberAt(x,y)>0&&PlanTerrain.ChamberAt(x,y)<=State.region;
        public bool Receiving(int x,int y)=>Config.regions.Any(r=>r.id<=State.region&&InRect(x,y,r.garbage_rectangle));
        public static bool InRect(int x,int y,int[] r)=>x>=r[0]&&y>=r[1]&&x<=r[2]&&y<=r[3];
        public (int width,int height) Size(PlanSite s){var a=Config.Facility(s.type).size;return s.rotation%2==0?(a[0],a[1]):(a[1],a[0]);}
        public (int x,int y) Front(PlanSite s)
        {
            var size=Size(s);
            if(s.community)return(s.x+size.width/2,s.y+size.height);
            switch(s.rotation%4){case 0:return(s.x+size.width/2,s.y-1);case 1:return(s.x-1,s.y+size.height/2);case 2:return(s.x+size.width/2,s.y+size.height);default:return(s.x+size.width,s.y+size.height/2);}
        }
        public bool Covers(PlanSite s,int x,int y){var z=Size(s);return x>=s.x&&y>=s.y&&x<s.x+z.width&&y<s.y+z.height;}
        public bool Overlay(string type)=>type=="power_line"||type=="steam_pipe"||type=="belt"||type=="port";
        private static double Distance(double ax,double ay,double bx,double by)=>Math.Sqrt((ax-bx)*(ax-bx)+(ay-by)*(ay-by));
        public void Pick(int groundId,string item,long amount)
        {
            Require(!HeroAboard,"主角正在升降舱内，无法拾取物品。");
            Require(!State.paused&&!State.riot.active,"暂停或暴动期间无法人工拾取。");
            Require(State.player.trip==null,"先完成当前拾取。");
            var g=State.ground.First(p=>p.id==groundId);Require(Distance(State.player.x,State.player.y,g.x,g.y)<=Config.Rule("workers.pickup_range"),"走近垃圾后拾取。");
            amount=Math.Min(amount,g.stock.Count(item));Require(amount>0&&State.backpack.Fits(Config,item,amount),"背包放不下或物品已预约，减少数量或先入库。");
            var selected=g.stock.stacks.First(s=>s.item==item&&s.ticks>s.reserved);int instance=selected.instance>0?selected.instance:-1;if(instance>0)amount=Math.Min(amount,selected.ticks-selected.reserved);
            g.stock.Reserve(item,amount,instance);State.player.trip=new PlanTrip{source="ground:"+g.id,target="backpack",item=item,ticks=amount,instance=instance};
        }
        public void Deposit(PlanSite target)
        {Require(!HeroAboard,"主角正在升降舱内，不能搬运物品。");
            var f=Front(target);Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5,"先到设施操作侧。");
            PlanStock dest=target.type=="warehouse"?target.input:target.input;
            foreach(var stack in State.backpack.stacks.ToArray())
            {
                var item=Config.Item(stack.item);var stock=!string.IsNullOrEmpty(item.tool_family)&&target.type!="warehouse"?target.tools:dest;
                if(stock==dest&&!AcceptsInput(target,stack.item))continue;
                long amount=stack.ticks;if(target.type=="belt"||target.type=="port"){if(target.input.stacks.Count>0)continue;amount=Math.Min(amount,PlanStock.Scale);}
                double occupied=stock==target.input&&target.type!="warehouse"?PlanStock.CombinedUsed(Config,target.input,target.reserved):stock.Used(Config);
                while(amount>0&&occupied+item.capacity*(item.uses_per_item>0?Math.Ceiling(amount/(double)PlanStock.Scale):amount/(double)PlanStock.Scale)>stock.capacity+1e-8)amount=Math.Max(0,amount-PlanStock.Scale);
                if(target.type!="warehouse"&&stack.item=="water")amount=Math.Min(amount,Math.Max(0,Stored("water")+State.backpack.Count("water")-Protected("water")));
                if(!string.IsNullOrEmpty(item.tool_family)&&target.type!="warehouse")amount=Math.Min(amount,Math.Max(0,2*PlanStock.Scale-stock.stacks.Where(v=>Config.Item(v.item).tool_family==item.tool_family).Sum(v=>v.ticks)));
                if(amount>0){stock.Put(State.backpack.Take(stack.item,amount));Discover(stack.item);}
            }
            State.revision++;
        }
        private bool AcceptsInput(PlanSite s,string item)
        {
            if(s.type=="warehouse")return true;if(s.type=="belt"||s.type=="port")return string.IsNullOrEmpty(s.filter)||s.filter==item;
            if(s.type=="landfill")return CanDiscard(item);
            if(s.type=="generator"||s.type=="boiler")return item=="wood_chips"||item=="waste_wood"||s.type=="boiler"&&item=="water";
            return Config.recipes.Where(r=>r.facility==s.type&&(string.IsNullOrEmpty(s.recipe)||r.id==s.recipe)).Any(r=>r.inputs.Any(a=>a.item==item));
        }
        public void TakeOutput(PlanSite site,string item,long ticks)
        {Require(!HeroAboard,"主角正在升降舱内，不能搬运物品。");var f=Front(site);Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5,"走到设施正面取物。");var from=site.type=="warehouse"?site.input:site.output;ticks=Math.Min(ticks,from.Count(item));if(site.type=="warehouse"&&(item=="food"||item=="water"))ticks=Math.Min(ticks,Surplus(item));Require(State.backpack.Fits(Config,item,ticks),"背包容量不足。");State.backpack.Put(from.Take(item,ticks));Discover(item);State.revision++;}
        private bool HasBuild(PlanAmount[] costs)=>costs.All(a=>State.backpack.Count(a.item)+Stored(a.item)>=a.Ticks);
        private void PayBuild(PlanAmount[] costs,PlanStock held)
        {foreach(var a in costs){long remaining=a.Ticks,n=Math.Min(remaining,State.backpack.Count(a.item));held.Put(State.backpack.Take(a.item,n));remaining-=n;foreach(var w in Warehouses){n=Math.Min(remaining,w.Count(a.item));held.Put(w.Take(a.item,n));remaining-=n;if(remaining==0)break;}}}
        private PlanSite ValidateBuild(string type,int x,int y,int rotation,bool crossing)
        {
            ValidateProgressionPlacement(type,x,y,rotation);var d=Config.Facility(type);Require(State.technologies.Contains(d.technology)&&d.region_required<=State.region,"需要取得对应材料解锁科技。");
            Require(!State.riot.active||RepairType(type),"暴动期间仅可兑现生活修复承诺。");
            var s=new PlanSite{id=State.nextId,type=type,x=x,y=y,rotation=rotation,direction=rotation,crossing=crossing,pending=true,durability=d.hasDurability?d.durability:100};
            ValidateOtherOperationSides(s);
            var size=Size(s);
            for(int px=x;px<x+size.width;px++)for(int py=y;py<y+size.height;py++)
            {
                Require(Owned(px,py)&&TerrainBuildable(px,py),"须在已开放洞室的可用地面上放置。");
                Require(Overlay(type)||!Receiving(px,py),"固定来料落点需保留用于接收垃圾，不能建设机器或填埋。");
                Require(!State.sites.Any(a=>a.permanent&&Covers(a,px,py)),"填埋土地已永久失去，不能再建造或铺线。");
                Require(!State.sites.Any(a=>Covers(a,px,py)&&!(type=="power_line"&&a.type!="power_line"||type=="steam_pipe"&&a.type!="steam_pipe"||type=="port"&&!Overlay(a.type)||type=="belt"&&a.type=="power_line"||type=="belt"&&a.type=="belt"&&crossing&&a.direction%2!=rotation%2)),"设施或永久填埋已占用该位置。");
                Require(Overlay(type)||GroundCapacity(px,py)==0,"地面物品尚未搬走。");
            }
            var front=Front(s);Require(Overlay(type)||Walkable(front.x,front.y)&&Path(State.player.x,State.player.y,front.x,front.y)!=null,"操作侧被阻挡，换位置或旋转。");
            Require(Overlay(type)||!Alive.Concat(new[]{State.player}).Any(p=>Covers(s,(int)Math.Round(p.x),(int)Math.Round(p.y))),"角色仍站在设施范围内。");
            var missing=d.build.Where(v=>State.backpack.Count(v.item)+Stored(v.item)<v.Ticks).ToArray();
            Require(missing.Length==0,"建材不足："+string.Join("、",missing.Select(v=>Config.Item(v.item).name+"缺"+((v.Ticks-State.backpack.Count(v.item)-Stored(v.item))/(double)PlanStock.Scale).ToString("0.####"))));
            int available=Alive.Count(p=>MayWork(p)&&p.assigned==0&&p.maintaining==0&&p.trip==null)+(!State.riot.active&&!HeroAboard&&State.heroSite==0?1:0);
            Require(available>=d.builders,"空闲建设人员不足：需要"+d.builders+"人，当前"+available+"人（含空闲主角）。");
            Require(!IsWaterIntake(type)||CanPlaceWaterIntake(s),"采水设施须临水，且小水源不能重复占用。");
            return s;
        }
        public string BuildProblem(string type,int x,int y,int rotation=0,bool crossing=false)
        {try{ValidateBuild(type,x,y,rotation,crossing);return null;}catch(InvalidOperationException e){return e.Message;}}
        public PlanSite Build(string type,int x,int y,int rotation=0,bool crossing=false)
        {
            var s=ValidateBuild(type,x,y,rotation,crossing);var d=Config.Facility(type);
            if(IsWaterIntake(type))BindWaterSource(s);
            PayBuild(d.build,s.builtMaterials);State.nextId++;State.sites.Add(s);if(type=="belt"&&crossing)NormalizeBeltCrossings();SetBuffers(s);if(!State.paused)FinishConstruction(s);State.revision++;return s;
        }
        private void SetBuffers(PlanSite s)
        {
            var d=Config.Facility(s.type);var recipes=Config.recipes.Where(r=>r.facility==s.type&&(string.IsNullOrEmpty(s.recipe)||r.id==s.recipe)).ToArray();
            s.input.capacity=s.type=="belt"||s.type=="port"?double.MaxValue:s.type=="landfill"?20:s.type=="warehouse"?d.capacity:s.type=="generator"||s.type=="boiler"?d.fuel_buffer>0?d.fuel_buffer:10:Math.Max(1,recipes.Select(r=>r.inputs.Sum(a=>Config.Item(a.item).capacity*(Config.Item(a.item).uses_per_item>0?Math.Ceiling(a.quantity*2):a.quantity*2))).DefaultIfEmpty(1).Max());
            s.output.capacity=Math.Max(1,recipes.Select(r=>OutputVolume(s,r)).DefaultIfEmpty(1).Max()*2);s.tools.capacity=24;
        }
        private void FinishConstruction(PlanSite s)
        {
            s.pending=false;var d=Config.Facility(s.type);
            if(d.maintainers>0){var p=Alive.FirstOrDefault(a=>MayWork(a)&&a.assigned==0&&a.maintaining==0&&a.trip==null);if(p!=null){p.maintaining=s.id;s.maintainer=p.id;}}
        }
        private double Capacity(PlanAmount[] values)=>values.Sum(a=>Config.Item(a.item).capacity*a.quantity);
        public void SelectRecipe(PlanSite s,string id)
        {Require(State.aboardSite!=s.id,"载人返程中不能切换配方。");Require(id!="lift_ascent","请通过确认登舱启动返程。");var r=Config.Recipe(id);Require(ProgressionProblem(s,r)==null,ProgressionProblem(s,r));Require(r.facility==s.type&&RecipeAvailable(r),"配方未解锁或设施不匹配。");Require((r.addons??new PlanAmount[0]).All(a=>s.addons.Count(a.item)>=a.Ticks),"此配方需要安装附件。");if(s.type=="survey"||s.type=="lift_launch")s.enabled=true;s.autoTools=false;if(s.active)s.pendingRecipe=id;else{s.recipe=id;s.progress=0;s.stage=0;SetBuffers(s);}State.revision++;}
        public void AddAttachment(PlanSite s,string item)
        {Require(Config.recipes.Where(r=>r.facility==s.type).SelectMany(r=>r.addons).Any(a=>a.item==item),"不是该设备的工装附件。");Require(Alive.Any(p=>MayWork(p)&&p.assigned==0&&p.maintaining==0&&p.trip==null)||!State.riot.active&&!HeroAboard&&State.heroSite==0,"安装附件需要空闲人员。");Require(Stored(item)>=PlanStock.Scale,"仓库没有该附件。");Require(s.addons.Count(item)==0,"已装有该附件。");var w=Warehouses.First(a=>a.Count(item)>=PlanStock.Scale);s.addons.Put(w.Take(item,PlanStock.Scale));State.revision++;}
        public void InstallEffluentCapture(PlanSite s)
        {
            var r=Config.recipes.FirstOrDefault(a=>a.facility==s.type&&a.effluent_fraction>0);Require(r!=null&&!s.captureEffluent,"该设施不支持收集附件，或已安装。");Require(Alive.Any(p=>MayWork(p)&&p.assigned==0&&p.maintaining==0&&p.trip==null)||!State.riot.active&&!HeroAboard&&State.heroSite==0,"安装需要空闲人员。");Require(HasBuild(r.capture_cost),"附件需要容器1、电线1。");PayBuild(r.capture_cost,s.addons);s.captureEffluent=true;SetBuffers(s);State.revision++;
        }
        public void AssignHero(int id){Require(!HeroAboard,"主角在升降舱内，不能同时参与劳动。");Require(!State.riot.active,"暴动期间不能参与商业生产。");State.player.directed=false;State.heroSite=State.heroSite==id?0:id;}
        public void Recruit()
        {
            Require(State.recognized,"主角完成发电机并实际发电后，居民才会加入。");Require(Population<Beds,"住房床位不足。");
            long fee=State.totalHired<Config.Region(1).free_hire_maximum?0:Cents(Config.Region(1).hire_fee_later);Require(State.cash>=fee,"招募资金不足。");State.cash-=fee;
            var p=new PlanPerson{id=State.nextId++,x=17,y=5,wallet=Cents(Config.Rule("life.new_resident_wallet")),nextMeal=State.seconds+300,nextServices=State.seconds+600,clothes=4800*Config.Rule("life.new_resident_clothes_min_durability")};State.people.Add(p);State.totalHired++;
            if(State.nextPayroll<0){State.firstPayroll=State.seconds+Config.Rule("payroll.period");State.nextPayroll=State.firstPayroll;}State.revision++;
        }
        public void Buy(string item,long ticks)
        {
            var d=Config.Item(item);Require(d.canBuy&&ticks>0,"这种来源或品质的物品不能直接购买，或数量无效。");Require((d.durability==0&&d.uses_per_item==0&&string.IsNullOrEmpty(d.instance_state))||ticks%PlanStock.Scale==0,"工具、设备与完整耗材按整件购买。");long cost=Cents(d.buy*ticks/PlanStock.Scale);Require(State.cash>=cost,"资金不足。");
            var warehouse=Warehouses.FirstOrDefault(w=>w.Fits(Config,item,ticks));Require(warehouse!=null,"仓库容量不足。");State.cash-=cost;
            if(d.durability>0||d.uses_per_item>0||!string.IsNullOrEmpty(d.instance_state)){for(long n=0;n<ticks;n+=PlanStock.Scale)warehouse.Add(item,PlanStock.Scale,State.region,d.durability,State.nextId++,null,string.IsNullOrEmpty(d.instance_state)?d.quality_tag:"repairable");}
            else warehouse.Add(item,ticks,State.region,0,string.IsNullOrEmpty(d.instance_state)?0:State.nextId++,null,string.IsNullOrEmpty(d.instance_state)?d.quality_tag:"repairable");
            Discover(item);State.revision++;
        }
        private bool AtFloor(string item)=>Config.sales.Any(g=>State.soldKnowledge.Contains(g.id)&&g.market_groups.Contains(Config.Item(item).market_group));
        private PlanMarket Market(string item){string group=Config.Item(item).market_group;var m=State.markets.FirstOrDefault(p=>p.group==group);if(m==null){m=new PlanMarket{group=group};State.markets.Add(m);}return m;}
        public double Price(string id)=>AtFloor(id)?Config.Item(id).sell_floor:Math.Max(Config.Item(id).sell_floor,Config.Item(id).sell_base*Math.Exp(-Market(id).pressure/Config.Rule("market.decay_constant")));
        public void Sell(string item,long ticks)
        {
            var d=Config.Item(item);Require(d.canSell,"该物品没有普通出售价格。");ticks=Math.Min(ticks,Surplus(item));Require(ticks>0,"库存已被生活、种薯或运输预约保护。");
            double revenue=0;long remaining=ticks;foreach(var w in Warehouses){long n=Math.Min(remaining,w.Count(item));foreach(var stack in w.Take(item,n)){revenue+=Price(item)*stack.ticks/PlanStock.Scale*(d.durability>0?stack.durability/d.durability:1);Market(item).pressure+=stack.ticks/(double)PlanStock.Scale;}remaining-=n;if(remaining==0)break;}
            double exact=revenue*100+State.saleRemainder;long paid=(long)Math.Floor(exact+1e-9);State.saleRemainder=exact-paid;State.cash+=paid;State.externalSaleIncome+=paid;State.revision++;
        }
        public void SellKnowledge(string id)
        {var g=Config.sales.First(a=>a.id==id);Require(!State.soldKnowledge.Contains(id)&&g.requires_technologies.All(State.technologies.Contains),"该知识未解锁或已出售。");State.soldKnowledge.Add(id);State.cash+=Cents(g.cash);}
        private void ValidateMove(PlanSite s,int x,int y)
        {
            Require(s!=null,"设施已不存在。");
            Require(s.cabinId==0,"装有升降舱的平台不能移动。");ValidateProgressionPlacement(s.type,x,y,s.rotation,s.id);Require(!s.community&&!s.permanent,"社区或已填埋土地不能移动。");int oldX=s.x,oldY=s.y;var z=Size(s);
            for(int px=x;px<x+z.width;px++)for(int py=y;py<y+z.height;py++)Require(Owned(px,py)&&TerrainBuildable(px,py)&&GroundCapacity(px,py)==0&&!State.sites.Any(a=>a!=s&&Covers(a,px,py)),"新位置被占用或不属于玩家。");
            Require(Overlay(s.type)||!Enumerable.Range(x,z.width).Any(px=>Enumerable.Range(y,z.height).Any(py=>Receiving(px,py))),"不能将机器移动到固定来料落点。");
            Require(!IsWaterIntake(s.type),"采水设备需先拆除，再在新的岸边重建。");
            var candidate=new PlanSite{type=s.type,x=x,y=y,rotation=s.rotation};Require(!Alive.Concat(new[]{State.player}).Any(p=>Covers(candidate,(int)Math.Round(p.x),(int)Math.Round(p.y))),"角色仍站在新设施范围内。");
            ValidateOtherOperationSides(candidate,s.id);
            s.x=x;s.y=y;try{var front=Front(s);Require(Overlay(s.type)||Path(State.player.x,State.player.y,front.x,front.y)!=null,"新位置的操作侧无法到达。");}finally{s.x=oldX;s.y=oldY;}
        }
        public string MoveProblem(PlanSite s,int x,int y)
        {try{ValidateMove(s,x,y);return null;}catch(InvalidOperationException e){return e.Message;}}
        private void ValidateOtherOperationSides(PlanSite candidate,int ignoreId=0)
        {
            if(Overlay(candidate.type))return;
            foreach(var other in State.sites.Where(s=>s.id!=ignoreId&&!Overlay(s.type)))
            {var front=Front(other);Require(!Covers(candidate,front.x,front.y),"会堵住"+Config.Facility(other.type).name+" #"+other.id+"的操作侧，请留出通行位置。");}
        }
        public void MoveSite(PlanSite s,int x,int y)
        {
            ValidateMove(s,x,y);int oldX=s.x,oldY=s.y;
            AbortTripsForSite(s.id);CancelBatch(s);CancelDisposal(s.id);Evacuate(s.input,oldX,oldY);Evacuate(s.output,oldX,oldY);s.x=x;s.y=y;State.revision++;
        }
        public void RemoveSite(PlanSite s,bool sell=false)
        {
            Require(s.cabinId==0&&State.aboardSite!=s.id,"已装舱的平台不能拆除或出售。");Require(!s.community&&!s.permanent,"社区资产不能变现；已填埋的地块永久失去。");AbortTripsForSite(s.id);CancelBatch(s);
            CancelDisposal(s.id);
            double attachmentValue=s.addons.stacks.Sum(a=>Config.Item(a.item).buy*a.ticks/PlanStock.Scale);
            foreach(var stock in new[]{s.input,s.output,s.tools})Evacuate(stock,s.x,s.y);
            if(sell){State.cash+=Cents((Config.Facility(s.type).sell_at_full_durability+attachmentValue*.45)*(Config.Facility(s.type).hasDurability?0.5+s.durability/200:1));s.builtMaterials.stacks.Clear();s.addons.stacks.Clear();}
            else{Evacuate(s.builtMaterials,s.x,s.y);Evacuate(s.addons,s.x,s.y);}
            foreach(var p in Alive){if(p.maintaining==s.id)p.maintaining=0;if(p.assigned==s.id)p.assigned=0;if(p.manualAssignment==s.id)p.manualAssignment=0;}
            foreach(var source in State.springs.Where(a=>a.site==s.id))source.site=0;State.sites.Remove(s);State.revision++;
            if(State.heroSite==s.id)State.heroSite=0;
        }
        private void Evacuate(PlanStock stock,int x,int y)
        {foreach(var item in stock.stacks.ToArray()){DropAt(item,x,y);stock.stacks.Remove(item);}State.revision++;}
        public void SetSiteEnabled(PlanSite site,bool enabled)
        {
            Require(site!=null,"设施已不存在。");site.enabled=enabled;
            if(!enabled){ReleaseOperators(site);if(State.heroSite==site.id)State.heroSite=0;site.status="已停工";}
            State.revision++;
        }
        public void SetPaused(bool paused){State.paused=paused;}
        public PlanState Capture(){State.rng=random.State;return State;}
    }
}
