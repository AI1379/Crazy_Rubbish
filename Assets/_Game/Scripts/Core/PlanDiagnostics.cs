using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed class PlanToolReadout
    {public string family,item;public double durability,maximum;}
    public sealed class PlanFacilityReadout
    {
        public string status,phase,issue;public bool blocked;
        public double progress,duration,powerDemand;
        public int workersHere,workersAssigned,workersNeeded;
        public PlanAmount[] missing=new PlanAmount[0];public PlanToolReadout[] tools=new PlanToolReadout[0];
    }
    public sealed class PlanSupplyReadout
    {public string item;public long available;public int affected;public double outstanding,cycles;}
    public sealed class PlanPayrollReadout
    {public bool scheduled;public double remaining;public long expected,unpaid;}

    public sealed partial class PlanSimulation
    {
        // Read-only views. They do not start batches, assign workers, reserve inputs or reveal hidden risk counters.
        public PlanSupplyReadout InspectSupply(string item)
        {
            Require(item=="water"||item=="food","这里只统计生活食物和饮水。");
            var people=Alive.Concat(new[]{State.player}).Where(p=>!p.dead).ToArray();long available=Stored(item);
            double outstanding=people.Sum(p=>item=="water"?p.waterDebt:p.foodDebt);
            return new PlanSupplyReadout{item=item,available=available,outstanding=outstanding,affected=people.Count(p=>(item=="water"?p.waterDebt:p.foodDebt)>1e-8),cycles=Math.Max(0,available/(double)PlanStock.Scale-outstanding)/Math.Max(1,people.Length)};
        }
        public PlanPayrollReadout InspectPayroll()
        {return new PlanPayrollReadout{scheduled=State.nextPayroll>=0&&Alive.Any(),remaining=Math.Max(0,State.nextPayroll-State.seconds),expected=Alive.Count()*Cents(Config.Rule("payroll.salary_per_worker")),unpaid=Debt};}
        public int[] UnpoweredHomes()
        {return Alive.Concat(new[]{State.player}).Where(p=>!p.dead).Select(House).Where(s=>s!=null&&s.powerRatio<.5).Select(s=>s.id).Distinct().ToArray();}
        private PlanAmount[] StageInputs(PlanRecipe recipe,int stage)
        {
            var part=recipe.stages[stage];
            return part.commit_inputs.Length>0?part.commit_inputs:stage==0?recipe.inputs.Where(a=>a.item!="water").Concat(part.water>0?new[]{new PlanAmount{item="water",quantity=part.water}}:new PlanAmount[0]).ToArray():part.water>0?new[]{new PlanAmount{item="water",quantity=part.water}}:new PlanAmount[0];
        }
        public PlanFacilityReadout InspectFacility(PlanSite s)
        {
            Require(s!=null,"设施已不存在。");var info=new PlanFacilityReadout{status=s.status,powerDemand=Request(s)};var definition=Config.Facility(s.type);
            if(s.type=="port"||s.type=="belt"){var transport=InspectTransport(s);info.status=transport.status;info.issue=transport.issue;info.blocked=transport.blocked;return info;}
            var recipe=string.IsNullOrEmpty(s.recipe)?null:Config.Recipe(s.recipe);
            var stage=recipe==null?null:recipe.stages[s.active?s.stage:0];
            bool hero=!HeroAboard&&!State.riot.active&&State.heroSite==s.id&&Distance(State.player.x,State.player.y,Front(s).x,Front(s).y)<=.1;
            if(recipe!=null)
            {
                info.phase=stage.kind=="human"?"人工处理":stage.kind=="machine"?"机器处理":stage.kind=="lit_growth"?"灯照生长":"自然成熟";
                info.progress=s.active?s.progress:0;info.duration=stage.seconds;info.workersNeeded=stage.kind=="human"?Math.Max(1,definition.operators):0;
                var crew=Alive.Where(p=>MayWork(p)&&p.assigned==s.id).ToArray();var front=Front(s);
                info.workersAssigned=crew.Length+(hero?1:0);info.workersHere=crew.Count(p=>Distance(p.x,p.y,front.x,front.y)<=.1)+(hero?1:0);
                info.tools=recipe.tools.Select(tool=>
                {
                    var chosen=s.tools.stacks.Concat(hero?State.backpack.stacks:Enumerable.Empty<PlanStack>()).Where(t=>Config.Item(t.item).tool_family==tool.item&&t.durability>0&&t.reserved==0).OrderByDescending(t=>Config.Item(t.item).efficiency).ThenByDescending(t=>t.durability).FirstOrDefault();
                    return new PlanToolReadout{family=tool.item,item=chosen?.item,durability=chosen?.durability??0,maximum=chosen==null?0:Config.Item(chosen.item).durability};
                }).ToArray();
                if(recipe.id=="lift_ascent"&&stage.kind=="human"){info.workersNeeded=1;info.workersAssigned=info.workersHere=State.aboardSite==s.id?1:0;}
                var required=recipe.id=="lift_test"?new PlanAmount[0]:!s.active?(recipe.consumption=="staged_at_start"&&recipe.stages[0].commit_inputs.Length>0?recipe.stages[0].commit_inputs:recipe.inputs):recipe.consumption=="staged_at_start"&&!s.stageCommitted?StageInputs(recipe,s.stage):new PlanAmount[0];
                info.missing=required.Select(a=>
                {
                    long available=s.active?s.input.Count(a.item):s.input.stacks.Where(v=>v.item==a.item&&(string.IsNullOrEmpty(recipe.requires_state)||v.quality==recipe.requires_state)&&(recipe.quality_required!="qualified_fresh_source_only"||a.item!="raw_freshwater"||v.quality=="qualified_fresh_source")).Sum(v=>v.ticks-v.reserved);
                    return new PlanAmount{item=a.item,quantity=Math.Max(0,a.Ticks-available)/(double)PlanStock.Scale};
                }).Where(a=>a.quantity>1e-8).ToArray();
            }
            Action<string,string> block=(code,text)=>{info.issue=code;info.status=text;info.blocked=true;};
            if(s.pending){block("construction","待建成，恢复经营后完成建设");return info;}
            if(!s.enabled){block("disabled","已停工，在制进度保留");return info;}
            if(s.stopped&&s.durability<Config.Rule("maintenance.restart_at")){block("maintenance","耐久耗尽，维护至20后重启");return info;}
            if(s.type=="warehouse"){info.status=s.input.Used(Config)>=s.input.capacity-1e-8?"仓库已满":"仓储可用";return info;}
            if(s.type=="housing"||s.type=="streetlamp")
            {if(s.powerRatio<.5)block("power","生活设施供电不足，请检查线路与电源");else info.status="生活设施供电正常";return info;}
            if(s.type=="generator"||s.type=="boiler")
            {
                if(s.fuelSeconds<=0&&s.input.Count("waste_wood")<PlanStock.Scale&&s.input.Count("wood_chips")<PlanStock.Scale)block("fuel","缺少燃料：投入废木料或木屑");
                else info.status=s.fuelSeconds>0?"燃料供给中":"燃料就绪，检查线路与用电负载";
                if(s.ashBudget>=20-1e-8&&!s.output.Fits(Config,"ash",PlanStock.Scale))block("output","灰渣输出已满，请搬走产物");
                if(s.type=="boiler"&&s.thermalStartup<20&&s.input.Count("water")<PlanStock.Quantity(8))block("input","锅炉启动需8升水与启动供电");return info;
            }
            if(s.type=="grow_lamp")
            {if(Site(s.lampBed)==null)block("connection","太阳灯尚未绑定种植地");else if(Request(s)>0&&s.powerRatio<=0)block("power","太阳灯等待供电");else info.status="已绑定种植地 #"+s.lampBed;return info;}
            if(s.type=="power_line"||s.type=="steam_pipe"){info.status=s.type=="power_line"?"电线连接段":"蒸汽管连接段";return info;}
            if(s.type=="turbine"||s.type=="condenser"){if(s.durability<=0)block("maintenance","部件耐久耗尽，需维护后运行");else info.status="蒸汽发电部件，需连接锅炉、汽轮机和冷凝器";return info;}
            if(recipe==null){info.status=definition.operators>0?"尚未选择配方":"设施待安排";return info;}
            string progression=ProgressionProblem(s,recipe);if(progression!=null){block("progression",progression);return info;}
            if(!RecipeAvailable(recipe)){block("technology","当前配方尚未解锁");return info;}
            if(!s.active)
            {
                if(info.missing.Length>0){block("input","原料不足，请补齐下列缺额");return info;}
                if(recipe.requires_unrecognized_composition&&s.input.stacks.Where(a=>a.item=="plastic_other"&&a.quality=="unrecognized"&&a.composition!=null).Sum(a=>a.ticks-a.reserved)<10*PlanStock.Scale){block("quality","需要带成分记录的未识别旧塑料");return info;}
                if(s.output.Used(Config)+OutputVolume(s,recipe)>s.output.capacity+1e-8){block("output","输出空间不足，请搬走产物");return info;}
                if(recipe.source_liters>0){var source=WaterSource(s);if(source==null||source.remaining-source.reserved+1e-8<recipe.source_liters){block("source","水源补充中，等待足量原水");return info;}}
            }
            if(stage.kind=="human")
            {
                if(State.riot.active&&!RepairAllowed(s)){block("riot","暴动停工，需平息暴动或兑现承诺");return info;}
                if(info.tools.Any(t=>t.item==null)){block("tool","缺少可用工具，请送入工具或让主角携带");return info;}
                if(info.workersHere<info.workersNeeded){block("worker","等待操作工到达操作侧");return info;}
            }
            if(stage.kind=="machine"&&recipe.power>0&&s.powerRatio<=0){block("power","等待供电，请检查电源和连接");return info;}
            if(stage.kind=="lit_growth"&&GrowRatio(s)<=0){block("light","等待已绑定太阳灯供电");return info;}
            if(info.missing.Length>0){block("input","当前阶段补料不足，请补齐下列缺额");return info;}
            info.status=s.active?info.phase:"条件已满足，等待开始批次";
            if(stage.kind=="machine"&&recipe.power>0&&s.powerRatio>0&&s.powerRatio<1)info.status+=" · 低供电减速";
            return info;
        }
    }
}
