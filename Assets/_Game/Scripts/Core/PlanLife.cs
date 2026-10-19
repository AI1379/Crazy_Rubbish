using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private double FoodFactor(PlanPerson p)=>Math.Min(1-p.foodRecovery,p.foodSince<0?1:State.seconds-p.foodSince>=900?.7:State.seconds-p.foodSince>=300?.9:1);
        private double WaterFactor(PlanPerson p)=>Math.Min(1-p.waterRecovery,p.waterSince<0?1:State.seconds-p.waterSince>=600?.6:State.seconds-p.waterSince>=180?.85:1);
        private double Efficiency(PlanPerson p,double tool=1)=>Math.Max(.25,Math.Min(1.6,tool*(1+p.bonus)*Math.Max(.7,Math.Min(1.05,.7+.3*p.happiness/70))*Math.Max(.6,1-.1*(p.id==0?0:DebtCycles))*FoodFactor(p)*WaterFactor(p)*Pressure(p.x,p.y)));
        public double PersonEfficiency(PlanPerson p)=>Efficiency(p);
        public double RetailPrice(string item)
        {var d=Config.Item(item);double cap=item=="water"?State.waterRetail:item=="food"?State.foodRetail:item=="clothes"?State.clothesRetail:State.soapRetail;if(cap<0)cap=d.buy;cap=Math.Min(cap,d.buy);return Math.Min(cap,AtFloor(item)?d.sell_floor:Math.Max(d.sell_floor,cap*Math.Exp(-Market(item).pressure/100)))*State.retailDiscount;}
        private bool Consume(PlanPerson p,string item,long ticks,bool protectSavings=false)
        {
            bool free=p.id==0||State.firstPayroll<0||State.seconds<State.firstPayroll||item=="water"&&State.freeWater||item=="food"&&State.freeFood;
            var d=Config.Item(item);long stockPrice=free?0:Cents(RetailPrice(item)*ticks/PlanStock.Scale);
            long external=Cents(d.buy*ticks/PlanStock.Scale),wallet=p.id==0?State.cash:p.wallet,keep=protectSavings?Cents(Config.Rule("life.life_savings_protection")):0;
            var warehouse=Warehouses.FirstOrDefault(w=>w.Count(item)>=ticks);
            if(warehouse!=null&&wallet>=stockPrice+keep&&(!State.externalSupply||stockPrice<=external||free))
            {
                warehouse.Take(item,ticks);if(p.id>0){p.wallet-=stockPrice;State.cash+=stockPrice;State.retailIncome+=stockPrice;if(stockPrice>0)Market(item).pressure+=ticks/(double)PlanStock.Scale;}return true;
            }
            if(State.externalSupply&&d.canBuy&&wallet>=external+keep)
            {if(p.id==0)State.cash-=external;else p.wallet-=external;return true;}
            long subsidy=external-Math.Max(0,wallet-keep);
            if(p.id>0&&State.externalSupply&&d.canBuy&&subsidy>0&&subsidy<=Math.Min(State.personalSubsidyBudget-State.personalSubsidyUsed,Math.Max(0,State.cash-SalaryProtection)))
            {p.wallet-=external-subsidy;State.cash-=subsidy;State.personalSubsidyUsed+=subsidy;return true;}
            if(item=="water"&&p.id>0&&State.emergencyWater&&warehouse!=null&&p.rescueWater+ticks/(double)PlanStock.Scale<=2+1e-8)
            {warehouse.Take(item,ticks);p.rescueWater+=ticks/(double)PlanStock.Scale;return true;}
            return false;
        }
        private long ConsumeNeed(PlanPerson p,string item,long requested)
        {
            if(Consume(p,item,requested))return requested;
            var stocked=Warehouses.Where(w=>w.Count(item)>0).Select(w=>Math.Min(requested,w.Count(item))).OrderByDescending(n=>n).FirstOrDefault();
            if(stocked>0&&stocked<requested&&Consume(p,item,stocked))return stocked;
            long wallet=p.id==0?State.cash:p.wallet;double price=Math.Min(RetailPrice(item),State.externalSupply?Config.Item(item).buy:double.MaxValue);
            long affordable=price>0?Math.Min(requested-1,PlanStock.Quantity(wallet/100.0/price)):0;if(affordable>0&&Consume(p,item,affordable))return affordable;return 0;
        }
        private PlanSite House(PlanPerson p)
        {
            int index=p.id==0?0:Alive.ToList().FindIndex(a=>a.id==p.id)+1;
            foreach(var s in State.sites.Where(s=>s.type=="housing"&&!s.pending).OrderBy(s=>s.id))
            {int beds=s.community?6:Config.Facility(s.type).beds;if(index<beds)return s;index-=beds;}return null;
        }
        private PlanSite Lamp(PlanPerson p)
        {
            var house=House(p);if(house==null)return null;var f=Front(house);
            return State.sites.Where(s=>s.type=="streetlamp"&&!s.pending&&s.enabled&&s.powerRatio>=.5&&Distance(s.x,s.y,f.x,f.y)<=Config.Facility(s.type).radius).OrderBy(s=>s.id).FirstOrDefault();
        }
        private void ChargeService(PlanPerson p,double price,bool community)
        {long n=Cents(price);if(p.id==0||State.seconds<State.firstPayroll||p.wallet-n<Cents(Config.Rule("life.life_savings_protection")))return;p.wallet-=n;if(community)State.communityCash+=n;else{State.cash+=n;State.serviceIncome+=n;}}
        private void SetLoss(PlanPerson p,string kind,bool missing,double dt)
        {
            var loss=p.losses.FirstOrDefault(l=>l.kind==kind);if(loss==null){loss=new PlanLoss{kind=kind};p.losses.Add(loss);}
            string prefix="happiness.losses."+kind+".";
            if(missing){if(loss.since<0)loss.since=State.seconds;loss.loss=Math.Min(Config.Rule(prefix+"max_loss"),Math.Max(0,State.seconds-loss.since-Config.Rule(prefix+"grace"))*Config.Rule(prefix+"max_loss")/Config.Rule(prefix+"ramp"));loss.recovery=loss.loss/180;}
            else{loss.since=-1;loss.loss=Math.Max(0,loss.loss-loss.recovery*dt);}
        }
        private void PrepareMeals(PlanPerson p)
        {
            while(State.seconds+1e-8>=p.nextMeal){p.foodDebt=Math.Min(2,p.foodDebt+1);p.waterDebt=Math.Min(2,p.waterDebt+1);if(p.foodSince<0)p.foodSince=p.nextMeal;if(p.waterSince<0)p.waterSince=p.nextMeal;p.nextMeal+=300;p.nextRetry=0;}
        }
        private void ServeNeeds(PlanPerson p,string item)
        {
            if(State.seconds+1e-8<p.nextRetry)return;
            if(item=="water")
            {double factor=WaterFactor(p);while(p.waterDebt>1e-8){long n=ConsumeNeed(p,item,PlanStock.Quantity(p.waterDebt));if(n==0)break;p.waterDebt=Math.Max(0,p.waterDebt-n/(double)PlanStock.Scale);}if(p.waterDebt<=1e-8&&p.waterSince>=0){p.waterSince=-1;p.waterRecovery=1-factor;p.waterRecoveryRate=p.waterRecovery/180;}}
            else
            {double factor=FoodFactor(p);while(p.foodDebt>1e-8){long n=ConsumeNeed(p,item,PlanStock.Quantity(p.foodDebt));if(n==0)break;p.foodDebt=Math.Max(0,p.foodDebt-n/(double)PlanStock.Scale);}if(p.foodDebt<=1e-8&&p.foodSince>=0){p.foodSince=-1;p.foodRecovery=1-factor;p.foodRecoveryRate=p.foodRecovery/180;}}
        }
        private void TickPerson(PlanPerson p,double dt)
        {
            bool hadClothes=p.clothes>0;p.clothes=Math.Max(0,p.clothes-dt);
            if(hadClothes&&p.clothes==0&&!p.clothesReturned){p.clothesReturned=true;var house=House(p);var place=house==null?((int)Math.Round(p.x),(int)Math.Round(p.y)):Front(house);DropAt(new PlanStack{item="old_clothes",ticks=PlanStock.Scale,quality="worn"},place.Item1,place.Item2);}
            p.foodRecovery=Math.Max(0,p.foodRecovery-dt*p.foodRecoveryRate);p.waterRecovery=Math.Max(0,p.waterRecovery-dt*p.waterRecoveryRate);
            if(State.seconds+1e-8>=p.nextRetry)
            {
                if(p.clothes==0&&Consume(p,"clothes",PlanStock.Scale)){p.clothes=4800;p.clothesReturned=false;}
                if(p.soapDue&&CleaningReady(p)&&Consume(p,"soap",PlanStock.Quantity(.01))){p.soapDue=false;var debt=p.losses.FirstOrDefault(l=>l.kind=="unclean");if(debt!=null)debt.since=-1;}
                p.nextRetry=State.seconds+10;
            }
            bool clean=true;
            if(State.seconds+1e-8>=p.nextServices)
            {
                p.nextServices+=600;p.rescueWater=0;
                p.soapDue=true;clean=CleaningReady(p)&&Consume(p,"soap",PlanStock.Quantity(.01));if(clean)p.soapDue=false;
                var house=House(p);if(house!=null)ChargeService(p,State.rent,house.community);
                var lamp=Lamp(p);if(lamp!=null)ChargeService(p,.4,lamp.community);
                if(clean)ChargeService(p,.1,true);
                long rest=Cents(Config.Rule("life.optional_rest_cost_per_600s"));
                if(p.id>0&&p.wallet-rest>=Cents(Config.Rule("life.clothes_savings_goal")+Config.Rule("life.life_savings_protection"))&&p.foodDebt==0&&p.waterDebt==0&&p.clothes>0)
                {p.wallet-=rest;State.communityCash+=rest;p.restUntil=State.seconds+Config.Rule("life.rest_seconds");}
                var cleanLoss=p.losses.FirstOrDefault(l=>l.kind=="unclean");if(!clean&&cleanLoss==null){cleanLoss=new PlanLoss{kind="unclean",since=State.seconds};p.losses.Add(cleanLoss);}else if(clean&&cleanLoss!=null)cleanLoss.since=-1;
            }
            var h=House(p);SetLoss(p,"no_bed",h==null,dt);SetLoss(p,"housing_power",h!=null&&h.powerRatio<.5,dt);SetLoss(p,"no_light",h!=null&&Lamp(p)==null,dt);SetLoss(p,"no_clothes",p.clothes==0,dt);
            bool dirty=p.losses.Any(l=>l.kind=="unclean"&&l.since>=0);SetLoss(p,"unclean",dirty,dt);
            p.happiness=Math.Max(0,Math.Min(100,70-p.losses.Sum(l=>l.loss)+(State.seconds<p.restUntil?5:0)));
            if(p.happiness<25){if(p.lowSince<0)p.lowSince=State.seconds;}else p.lowSince=-1;
            if(p.waterSince>=0&&State.seconds-p.waterSince>=1200||p.foodSince>=0&&State.seconds-p.foodSince>=1800)
            {
                if(p.id==0){State.ending=p.waterSince>=0&&State.seconds-p.waterSince>=1200?"主角长期缺水，生存失败":"主角长期缺食，生存失败";State.paused=true;}
                else{if(p.trip!=null&&!p.trip.picked)ReleaseInventoryTrip(p.trip);foreach(var item in p.cargo.stacks)DropAt(item,(int)Math.Round(p.x),(int)Math.Round(p.y));p.cargo.stacks.Clear();State.communityCash+=p.wallet;p.wallet=0;p.dead=true;foreach(var s in State.sites.Where(s=>s.maintainer==p.id))s.maintainer=0;State.revision++;}
            }
        }
        private bool CleaningReady(PlanPerson p)
        {
            int index=p.id==0?0:Alive.ToList().FindIndex(a=>a.id==p.id)+1,group=index/4;
            var cleaning=State.cleaning.FirstOrDefault(c=>c.group==group);if(cleaning==null){cleaning=new PlanCleaning{group=group};State.cleaning.Add(cleaning);}
            if(State.seconds<cleaning.until)return true;
            if(State.seconds<cleaning.nextAttempt||State.riot.active&&!State.riot.promise)return false;cleaning.nextAttempt=State.seconds+10;
            var rag=Warehouses.FirstOrDefault(w=>w.Count("clean_rag")>=PlanStock.Scale);var broom=Warehouses.SelectMany(w=>w.stacks).FirstOrDefault(s=>Config.Item(s.item).tool_family=="broom"&&s.durability>=1&&s.reserved==0);
            if(rag==null||broom==null)return false;
            rag.Take("clean_rag",PlanStock.Scale);broom.durability-=1;var house=House(p);var front=house==null?((int)Math.Round(p.x),(int)Math.Round(p.y)):Front(house);DropAt(new PlanStack{item="dirty_rag",ticks=PlanStock.Scale},front.Item1,front.Item2);
            if(broom.durability<=0){Warehouses.First(w=>w.stacks.Contains(broom)).stacks.Remove(broom);DropAt(new PlanStack{item="broken_broom",ticks=PlanStock.Scale,instance=broom.instance},front.Item1,front.Item2);}cleaning.until=State.seconds+600;return true;
        }
        private bool TickPayroll()
        {
            bool due=false;
            while(State.nextPayroll>=0&&State.seconds+1e-8>=State.nextPayroll)
            {
                var ids=Alive.Select(p=>p.id).ToArray();if(ids.Length>0){long each=Cents(Config.Rule("payroll.salary_per_worker"));State.bills.Add(new PlanBill{ids=ids,unpaid=ids.Select(i=>each).ToArray(),original=each*ids.Length,at=State.nextPayroll});}
                foreach(var p in Alive)p.bonus=0;State.nextPayroll+=Config.Rule("payroll.period");due=true;
            }
            return due;
        }
        public long PayWages(long maximum=long.MaxValue)
        {
            long budget=Math.Min(State.cash,maximum),paid=0;
            foreach(var b in State.bills.OrderBy(b=>b.at))
            {
                while(budget>0&&b.unpaid.Any(a=>a>0))
                {
                    int active=b.unpaid.Count(a=>a>0);long share=Math.Max(1,budget/active);
                    for(int i=0;i<b.ids.Length&&budget>0;i++){long n=Math.Min(budget,Math.Min(share,b.unpaid[i]));b.unpaid[i]-=n;budget-=n;paid+=n;var p=Alive.FirstOrDefault(p=>p.id==b.ids[i]);if(p==null)State.communityCash+=n;else p.wallet+=n;}
                }
                if(budget==0)break;
            }
            State.cash-=paid;State.wagesPaid+=paid;if(State.riot.active)State.riot.paid+=paid;SettleRiot();return paid;
        }
        public void Bonus(int siteId=0)
        {
            Require(Debt==0,"有欠薪时不能发奖金；奖金不抵工资。");var group=Alive.Where(p=>siteId==0||p.assigned==siteId||p.maintaining==siteId||p.manualAssignment==siteId).Where(p=>p.bonus<.3-1e-8).ToArray();
            long cost=Cents(Config.Rule("payroll.bonus_per_worker_per_step"))*group.Length;Require(group.Length>0&&State.cash>=cost,"该组没有可加奖金的员工，或资金不足。");State.cash-=cost;foreach(var p in group){p.wallet+=cost/group.Length;p.bonus=Math.Min(.3,p.bonus+.1);}
        }
        private void CheckRiot(bool salaryDue)
        {
            var organizers=Alive.Where(p=>p.lowSince>=0&&State.seconds-p.lowSince>=180).Select(p=>p.id).ToArray();bool unhappy=organizers.Length>=Math.Max(2,(int)Math.Ceiling(Alive.Count()*.2));
            bool wages=salaryDue&&DebtCycles>=3-1e-8;
            if(wages||unhappy)
            {
                if(!State.riot.active){State.riot.active=true;State.riot.deadline=State.seconds+240;State.heroSite=0;foreach(var site in State.sites)ReleaseOperators(site);}
                if(wages&&!State.riot.wages){State.riot.wages=true;State.riot.target=(Debt+2)/3;State.riot.paid=0;}
                if(unhappy)foreach(int id in organizers)if(!State.riot.organizers.Contains(id))State.riot.organizers.Add(id);
            }
            SettleRiot();
            if(State.riot.active&&State.seconds>=State.riot.deadline){State.ending="未及时平息暴动或兑现承诺，主角死亡";State.paused=true;}
        }
        private void SettleRiot()
        {
            var r=State.riot;if(!r.active)return;
            bool wages=!r.wages||r.paid>=r.target;
            bool life=r.organizers.All(id=>{var p=Alive.FirstOrDefault(a=>a.id==id);return p==null||p.happiness>=40&&p.losses.All(l=>l.since<0);});
            if(wages&&life){r.active=false;r.wages=false;r.promise=false;r.organizers.Clear();}
        }
        public void Promise(string repair="power")
        {
            Require(State.riot.active&&State.riot.organizers.Count>0,"没有需要兑现生活修复的暴动群体。");Require(new[]{"power","housing","clothes","clean"}.Contains(repair),"请选择固定修复承诺。");
            State.riot.promise=true;State.riot.repair=repair;
            foreach(var p in Alive.Where(p=>State.riot.organizers.Contains(p.id)))
            {if(p.trip!=null&&!p.trip.picked)ReleaseInventoryTrip(p.trip);p.trip=null;p.assigned=0;p.manualAssignment=0;p.directed=false;}
        }
        private bool MayWork(PlanPerson p)=>!p.directed&&(!State.riot.active||State.riot.promise&&State.riot.organizers.Contains(p.id));
        private bool RepairType(string type)=>State.riot.promise&&(new[]{"warehouse","bench","wood"}.Contains(type)||State.riot.repair=="power"&&new[]{"generator","boiler","turbine","condenser","streetlamp","grow_lamp","power_line","steam_pipe","electromechanical","wire_maker","metal_parts"}.Contains(type)||State.riot.repair=="housing"&&type=="housing"||State.riot.repair=="clothes"&&new[]{"washing","sorting"}.Contains(type)||State.riot.repair=="clean"&&new[]{"washing","plastic_remake"}.Contains(type));
        private bool RepairAllowed(PlanSite s)=>!State.riot.active||RepairType(s.type);
        public void Advance(double seconds)
        {
            if(State.paused||!string.IsNullOrEmpty(State.ending))return;State.accumulated+=seconds*State.speed;
            double tick=Config.Rule("clock.tick_seconds");
            while(State.accumulated+1e-8>=tick&&!State.paused)
            {
                State.accumulated-=tick;State.seconds+=tick;bool salaryDue=TickPayroll();TickCommands(tick);
                if(State.player.trip!=null)
                {
                    var trip=State.player.trip;var pos=StockPosition(trip.source);
                    if(Distance(State.player.x,State.player.y,pos.x,pos.y)>Config.Rule("workers.pickup_range")){ReleaseInventoryTrip(trip);State.player.trip=null;}
                    else{trip.elapsed+=tick;if(trip.elapsed+1e-8>=Config.Rule("workers.pickup_seconds")){CompleteInventoryPickup(trip,State.backpack);Discover(trip.item);State.player.trip=null;State.revision++;}}
                }
                TickEnvironment(tick);ComputePower(tick);TickProduction(tick,false);TickDisposal(tick);TickWorkers(tick,false);TickBelts(tick);
                // Finish this tick's output/unloading before survival checks; reserve new inputs afterwards.
                var people=Alive.Concat(new[]{State.player}).ToArray();foreach(var p in people)PrepareMeals(p);TickRestock();
                int rotation=(int)(State.seconds/300)%Math.Max(1,people.Length);var rotated=people.Skip(rotation).Concat(people.Take(rotation)).ToArray();
                foreach(var p in rotated.OrderBy(p=>p.waterSince<0?double.MaxValue:p.waterSince))ServeNeeds(p,"water");
                foreach(var p in rotated.OrderBy(p=>p.foodSince<0?double.MaxValue:p.foodSince))ServeNeeds(p,"food");
                foreach(var p in people)TickPerson(p,tick);
                if(!string.IsNullOrEmpty(State.ending))break;
                TickSpareTools();TickProduction(0,true);TickWorkers(0,true);CheckRiot(salaryDue);TickTutorial();
                if(State.invasionAt>=0&&State.seconds>=State.invasionAt){State.ending="下层文明入侵，中层被消灭";State.paused=true;}
                ResolveReturnArrival();
                State.rng=random.State;
            }
        }
    }
}
