using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private long SalaryProtection=>Debt+Alive.Count()*Cents(Config.Rule("payroll.salary_per_worker"));
        private void TickRestock()
        {
            while(State.seconds>=State.autoBudgetDue){State.autoBudgetDue+=600;State.autoSupplyUsed=0;State.toolBudgetUsed=0;State.personalSubsidyUsed=0;}
            if(!State.autoSupply)return;
            foreach(string item in new[]{"water","food"})
            {
                long have=Stored(item);if(have>=Population*PlanStock.Scale)continue;
                long need=Population*2*PlanStock.Scale-have,budget=Math.Max(0,Math.Min(State.autoSupplyBudget-State.autoSupplyUsed,State.cash-SalaryProtection));
                double price=Config.Item(item).buy;long wanted=Math.Min(need,PlanStock.Quantity(budget/100.0/price));
                foreach(var warehouse in Warehouses)
                {
                    long n=Math.Min(wanted,PlanStock.Quantity(Math.Max(0,warehouse.capacity-warehouse.Used(Config))/Config.Item(item).capacity));
                    if(n<=0)continue;long cost=Cents(price*n/PlanStock.Scale);if(cost>budget)continue;
                    warehouse.Add(item,n,State.region);State.cash-=cost;State.autoSupplyUsed+=cost;State.supplySpent+=cost;wanted-=n;budget-=cost;Discover(item);State.revision++;
                }
            }
        }
        private long SpareTools(string family)=>Warehouses.Concat(State.sites.Select(s=>s.output)).Concat(State.ground.Select(g=>g.stock)).SelectMany(s=>s.stacks).Where(v=>Config.Item(v.item).tool_family==family&&v.durability>0).Sum(v=>v.ticks-v.reserved);
        private void TickSpareTools()
        {
            var families=State.sites.Where(s=>s.enabled&&!s.pending&&!string.IsNullOrEmpty(s.recipe)).SelectMany(s=>Config.Recipe(s.recipe).tools).Select(t=>t.item).Distinct().ToArray();
            foreach(string family in families.Where(f=>SpareTools(f)<PlanStock.Scale))
            {
                if(State.autoTools)
                {
                    long available=Math.Max(0,Math.Min(State.toolBudget-State.toolBudgetUsed,State.cash-SalaryProtection));
                    var buy=Config.items.Where(i=>i.tool_family==family&&i.canBuy&&Cents(i.buy)<=available).OrderByDescending(i=>i.efficiency).ThenByDescending(i=>i.durability).FirstOrDefault();
                    if(buy!=null&&Warehouses.Any(w=>w.Fits(Config,buy.id,PlanStock.Scale))){Buy(buy.id,PlanStock.Scale);State.toolBudgetUsed+=Cents(buy.buy);continue;}
                }
                if(!State.autoMakeTools)continue;
                var workshop=State.sites.FirstOrDefault(s=>s.type=="bench"&&s.autoTools&&!s.active&&!s.pending&&s.enabled&&RepairAllowed(s));if(workshop==null)continue;
                var recipe=Config.recipes.Where(r=>r.facility=="bench"&&RecipeAvailable(r)&&r.outputs.Any(a=>Config.Item(a.item).tool_family==family)&&r.inputs.All(a=>Stored(a.item)+State.ground.Sum(g=>g.stock.Count(a.item))+workshop.input.Count(a.item)>=a.Ticks)).OrderByDescending(r=>r.outputs.Max(a=>Config.Item(a.item).efficiency)).FirstOrDefault();
                if(recipe!=null){SelectRecipe(workshop,recipe.id);workshop.autoTools=true;}
            }
        }
        private void TickTutorial()
        {
            int stage=State.tutorial;
            if(stage==0&&State.acquired.Contains("handsaw_wood")&&State.acquired.Contains("water")&&State.acquired.Contains("food")||stage==1&&State.sites.Any(s=>s.type=="wood"&&!s.pending)||stage==2&&State.acquired.Contains("boards")||stage==3&&State.recognized||stage==4&&Alive.Any()||stage==5&&State.sites.Any(s=>s.type=="water_purifier")&&State.sites.Any(s=>s.type=="growing")||stage==6&&State.region>=2){State.tutorial++;State.revision++;}
        }
        public void AssignWorker(PlanPerson p,PlanSite site,bool maintenance=false)
        {
            Require(p!=null&&!p.dead&&site!=null,"选择在世员工和设施。");
            if(maintenance){Require(Config.Facility(site.type).hasDurability&&(site.maintainer==0||site.maintainer==p.id),"该设备不需要维护或已有维护人员。");}
            p.directed=false;
            // Complete the current human stage before obeying a new pin.
            if(!maintenance&&p.assigned>0){p.manualAssignment=site.id;return;}
            if(p.trip!=null&&!p.trip.picked)Stock(p.trip.source)?.Release(p.trip.item,p.trip.ticks,p.trip.instance);p.trip=null;
            var old=Site(p.maintaining);if(old!=null)old.maintainer=0;p.maintaining=0;p.assigned=0;p.manualAssignment=site.id;
            if(maintenance){p.maintaining=site.id;site.maintainer=p.id;}
        }
        public void TakeTool(PlanSite site,string item)
        {Require(!HeroAboard,"主角正在升降舱内，不能搬运物品。");
            var f=Front(site);Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5,"走到设施操作侧取回工具。");Require(!site.active||Config.Recipe(site.recipe).stages[site.stage].kind!="human","先完成当前人工阶段或取消批次。");
            Require(State.backpack.Fits(Config,item,PlanStock.Scale),"背包容量不足。");State.backpack.Put(site.tools.Take(item,PlanStock.Scale));State.revision++;
        }
        public void TakeInput(PlanSite site,string item,long ticks)
        {Require(!HeroAboard,"主角正在升降舱内，不能搬运物品。");
            var f=Front(site);Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5,"走到设施操作侧取回原料。");ticks=Math.Min(ticks,site.input.Count(item));Require(ticks>0&&State.backpack.Fits(Config,item,ticks),"库存已预约或背包容量不足。");State.backpack.Put(site.input.Take(item,ticks));State.revision++;
        }
    }
}
