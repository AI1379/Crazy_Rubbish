using System;
using System.Linq;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private bool HasInput(PlanSite s,PlanRecipe r)
        {
            if(r.id=="lift_test")return s.cabinId>0;var inputs=r.consumption=="staged_at_start"&&r.stages[0].commit_inputs.Length>0?r.stages[0].commit_inputs:r.inputs;
            return inputs.All(a=>s.input.stacks.Where(v=>v.item==a.item&&(string.IsNullOrEmpty(r.requires_state)||v.quality==r.requires_state)&&(r.quality_required!="qualified_fresh_source_only"||a.item!="raw_freshwater"||v.quality=="qualified_fresh_source")).Sum(v=>v.ticks-v.reserved)>=a.Ticks);
        }
        private List<PlanStack> Tools(PlanSite s,PlanRecipe r,bool hero)
        {
            var result=new List<PlanStack>();foreach(var tool in r.tools)
            {
                var candidates=s.tools.stacks.Concat(hero?State.backpack.stacks:Enumerable.Empty<PlanStack>()).Where(t=>Config.Item(t.item).tool_family==tool.item&&t.durability>0&&t.reserved==0).OrderByDescending(t=>Config.Item(t.item).efficiency).ThenByDescending(t=>t.durability);
                var chosen=candidates.FirstOrDefault();if(chosen==null)return null;result.Add(chosen);
            }
            return result;
        }
        private void BeginBatch(PlanSite s,PlanRecipe r)
        {
            string progression=ProgressionProblem(s,r);if(progression!=null){s.status=progression;return;}
            if(!HasInput(s,r)){s.status="需要原料";return;}
            if(r.requires_unrecognized_composition&&s.input.stacks.Where(a=>a.item=="plastic_other"&&a.quality=="unrecognized"&&a.composition!=null).Sum(a=>a.ticks-a.reserved)<10*PlanStock.Scale){s.status="没有足量带成分记录的未识别旧塑料";return;}
            if(s.output.Used(Config)+OutputVolume(s,r)>s.output.capacity+1e-8){s.status="输出已满";return;}
            if(!string.IsNullOrEmpty(r.requires_state)&&r.inputs.Any(a=>!s.input.stacks.Any(v=>v.item==a.item&&v.quality==r.requires_state))){s.status="实例状态不符合该配方";return;}
            if(r.source_liters>0)
            {
                var spring=WaterSource(s);
                if(spring==null||spring.remaining-spring.reserved+1e-8<r.source_liters){s.status="水源补充中";return;}
                if(r.sourceAtStart){spring.remaining-=r.source_liters;s.sourceDrawn=true;}else{spring.reserved+=r.source_liters;s.sourceReserved=r.source_liters;}
            }
            if(r.consumption!="staged_at_start"&&r.id!="lift_test")foreach(var a in r.inputs)
            {
                // Keep acquired instance quality; never consume an unusable wreck in a repair recipe.
                if(!string.IsNullOrEmpty(r.requires_state)||r.requires_unrecognized_composition||r.quality_required=="qualified_fresh_source_only"&&a.item=="raw_freshwater")s.input.stacks=s.input.stacks.OrderByDescending(v=>v.item==a.item&&(v.quality==r.requires_state||v.quality=="qualified_fresh_source"||v.quality=="unrecognized")).ToList();
                s.reserved.Put(s.input.Take(a.item,a.Ticks));
            }
            s.active=true;s.stage=0;s.progress=0;s.stageCommitted=false;s.batchId=State.nextId++;
            if(r.consumption=="feed_at_start")
            {var packet=s.reserved.stacks.FirstOrDefault(a=>a.item=="immature_compost");if(packet!=null){s.batchId=packet.instance;s.stage=packet.resumeStage;s.progress=packet.resumeProgress;}s.reserved.stacks.Clear();}
        }
        private void ReleaseOperators(PlanSite s){foreach(var p in Alive.Where(p=>p.assigned==s.id)){p.assigned=0;p.nextSeek=0;p.activity="空闲";}s.operators.Clear();}
        private bool HumanReady(PlanSite s,PlanRecipe r,out double efficiency)
        {
            efficiency=0;if(State.riot.active&&!RepairAllowed(s))return false;var front=Front(s);var d=Config.Facility(s.type);
            bool hero=!HeroAboard&&!State.riot.active&&State.heroSite==s.id&&Distance(State.player.x,State.player.y,front.x,front.y)<=.1;
            var tools=Tools(s,r,hero);if(tools==null){s.status="需要工具";return false;}
            int wanted=Math.Max(1,d.operators)-(hero?1:0);
            foreach(var p in Alive.Where(p=>MayWork(p)&&p.trip==null&&p.maintaining==0&&(p.assigned==0||p.assigned==s.id)&&(p.manualAssignment==0||p.manualAssignment==s.id||Site(p.manualAssignment)==null||!Site(p.manualAssignment).enabled)).OrderByDescending(p=>p.manualAssignment==s.id).ThenBy(p=>Distance(p.x,p.y,front.x,front.y)))
            {if(Alive.Count(a=>a.assigned==s.id)>=wanted)break;p.assigned=s.id;}
            var crew=Alive.Where(p=>MayWork(p)&&p.assigned==s.id&&Distance(p.x,p.y,front.x,front.y)<=.1).ToList();if(hero)crew.Add(State.player);
            if(crew.Count<Math.Max(1,d.operators)){s.status="等待操作工到岗";return false;}
            double toolFactor=tools.Select(t=>Config.Item(t.item).efficiency).DefaultIfEmpty(1).Max();
            efficiency=crew.Average(p=>Efficiency(p,toolFactor));return true;
        }
        private bool CommitStage(PlanSite s,PlanRecipe r,PlanStage stage)
        {
            if(s.stageCommitted)return true;
            if(r.consumption=="staged_at_start")
            {
                var commit=stage.commit_inputs.Length>0?stage.commit_inputs:s.stage==0?r.inputs.Where(a=>a.item!="water").Concat(stage.water>0?new[]{new PlanAmount{item="water",quantity=stage.water}}:new PlanAmount[0]).ToArray():stage.water>0?new[]{new PlanAmount{item="water",quantity=stage.water}}:new PlanAmount[0];
                if(commit.Any(a=>s.input.Count(a.item)<a.Ticks)){s.status="等待阶段补水或播种材料";return false;}
                foreach(var a in commit)s.input.Take(a.item,a.Ticks);
            }
            s.stageCommitted=true;return true;
        }
        private double GrowRatio(PlanSite s)=>State.sites.Where(l=>l.type=="grow_lamp"&&l.lampBed==s.id&&!l.pending&&l.enabled).Select(l=>l.powerRatio).DefaultIfEmpty(0).Max();
        private void CompleteBatch(PlanSite s,PlanRecipe r)
        {
            if(r.source_liters>0&&!r.sourceAtStart)
            {var spring=WaterSource(s);spring.reserved-=s.sourceReserved;spring.remaining-=s.sourceReserved;s.sourceReserved=0;}
            if(r.facility=="survey"||r.id=="build_lift"||r.id=="lift_test"||r.id=="lift_ascent")FinishProgressionBatch(s,r);
            else if(r.preserve_payload&&r.id.StartsWith("pack_"))
            {
                var product=r.outputs.First();var payload=s.reserved.stacks.Where(a=>a.item!="pet_strap").Select(PlanPackedStack.FromStack).ToArray();
                s.output.Put(new[]{new PlanStack{item=product.item,ticks=product.Ticks,instance=State.nextId++,origin=payload.First().origin,payload=payload}});Discover(product.item);
            }
            else if(r.preserve_payload&&r.id.StartsWith("unpack_"))
            {
                foreach(var packet in s.reserved.stacks.Where(a=>a.item.StartsWith("pack_")))
                {
                    if(packet.payload!=null&&packet.payload.Length>0)s.output.Put(packet.payload.Select(p=>p.ToStack()));
                    else foreach(var a in Config.Item(packet.item).payload.Where(a=>a.item!="pet_strap"))s.output.Add(a.item,a.Ticks,packet.origin);
                }
                s.output.Add("sorted_pet",PlanStock.Scale);foreach(var a in r.outputs)Discover(a.item);
            }
            else if(r.id=="recognize_old_plastic")
            {
                foreach(var stack in s.reserved.stacks){double sum=stack.composition.Sum(a=>a.quantity);foreach(var part in stack.composition)s.output.Add(part.item,PlanStock.Quantity(stack.ticks/(double)PlanStock.Scale*part.quantity/sum),stack.origin,0,0,null,"recognized");}
                foreach(var a in s.output.stacks)Discover(a.item);
            }
            else if(r.actual_composition)
            {
                foreach(var stack in s.reserved.stacks)
                {
                    var parts=stack.composition!=null&&stack.composition.Length>0?stack.composition:stack.item=="plastic_mix"?Config.Region(stack.origin).plastic_parts_per_10:r.outputs;
                    double total=parts.Sum(a=>a.quantity);foreach(var a in parts){s.output.Add(a.item,PlanStock.Quantity(stack.ticks/(double)PlanStock.Scale*a.quantity/total),stack.origin,0,0,null,"recognized");Discover(a.item);}
                }
            }
            else foreach(var a in r.outputs)
            {
                // Keep a qualified water batch together, with its own quality/reservation identity.
                // Durable tools and physical consumables still have one instance per whole item.
                var d=Config.Item(a.item);if(d.durability>0||d.uses_per_item>0)for(long n=0;n<a.Ticks;n+=PlanStock.Scale)s.output.Add(a.item,Math.Min(PlanStock.Scale,a.Ticks-n),State.region,d.durability,State.nextId++,null,d.quality_tag);
                else if(!string.IsNullOrEmpty(d.quality_tag))s.output.Add(a.item,a.Ticks,State.region,0,State.nextId++,null,d.quality_tag);
                else s.output.Add(a.item,a.Ticks,State.region,0,0,null,d.quality_tag);Discover(a.item);
            }
            if(s.captureEffluent&&r.effluent_fraction>0){s.output.Add("industrial_effluent",PlanStock.Quantity(r.inputs.Where(a=>a.item=="water"||a.item=="industrial_water").Sum(a=>a.quantity)*r.effluent_fraction));Discover("industrial_effluent");}
            s.reserved.stacks.Clear();s.active=false;s.sourceDrawn=false;s.progress=0;s.stage=0;s.stageCommitted=false;ReleaseOperators(s);State.revision++;
            if(!string.IsNullOrEmpty(s.pendingRecipe)){s.recipe=s.pendingRecipe;s.pendingRecipe=null;SetBuffers(s);}
        }
        private double OutputVolume(PlanSite s,PlanRecipe r)=>Capacity(r.outputs)+(s.captureEffluent?r.effluent_fraction*r.inputs.Where(a=>a.item=="water"||a.item=="industrial_water").Sum(a=>a.quantity)*Config.Item("industrial_effluent").capacity:0);
        public void CancelBatch(PlanSite s)
        {
            Require(State.aboardSite!=s.id,"载人返程不可取消，可暂停平台后恢复。");if(!s.active)return;var r=Config.Recipe(s.recipe);
            if(s.sourceReserved>0){WaterSource(s).reserved-=s.sourceReserved;s.sourceReserved=0;}
            if(s.sourceDrawn){s.reserved.Add("raw_freshwater",PlanStock.Quantity(r.source_liters),s.springRegion,0,0,null,"qualified_fresh_source");s.sourceDrawn=false;}
            if(r.consumption=="staged_at_start"&&(s.stage>0||s.stageCommitted))s.reserved.Add("spent_substrate",PlanStock.Scale);
            if(r.consumption=="feed_at_start")s.reserved.stacks.Add(new PlanStack{item=r.cancel_package,ticks=PlanStock.Scale,origin=State.region,instance=s.batchId,resumeStage=s.stage,resumeProgress=s.progress,composition=r.inputs});
            foreach(var stack in s.reserved.stacks.ToArray()){if(s.input.Fits(Config,stack.item,stack.ticks))s.input.Put(new[]{stack});else DropAt(stack,s.x,s.y);s.reserved.stacks.Remove(stack);}
            s.active=false;s.progress=0;s.stage=0;s.stageCommitted=false;ReleaseOperators(s);State.revision++;
        }
        private void TickProduction(double dt,bool allowStart)
        {
            foreach(var s in State.sites.OrderByDescending(WorkPriority).ThenBy(s=>s.id))
            {
                if(s.pending){FinishConstruction(s);continue;}if(!s.enabled){ReleaseOperators(s);if(State.heroSite==s.id)State.heroSite=0;s.status="已停工";continue;}if(string.IsNullOrEmpty(s.recipe))continue;
                var r=Config.Recipe(s.recipe);if(!RecipeAvailable(r))continue;
                if(!s.active&&allowStart)BeginBatch(s,r);if(!s.active)continue;
                if(s.stopped&&s.durability>=Config.Rule("maintenance.restart_at"))s.stopped=false;
                if(s.stopped){ReleaseOperators(s);s.status="耐久耗尽，等待维护至20";continue;}
                var stage=r.stages[s.stage];double rate=1;
                if(stage.kind=="human"){if(r.id=="lift_ascent"){if(State.riot.active){s.status="暴动期间准备暂停";continue;}rate=Efficiency(State.player);}else if(!HumanReady(s,r,out rate))continue;}
                else{ReleaseOperators(s);if(stage.kind=="machine")rate=r.power>0?s.powerRatio:1;else if(stage.kind=="lit_growth")rate=GrowRatio(s);}
                if(rate<=0){s.status=stage.kind=="lit_growth"?"等待太阳灯供电":"等待供电";continue;}
                if(!s.stageCommitted&&!allowStart)continue;if(!CommitStage(s,r,stage))continue;
                double advanced=Math.Min(dt*rate,stage.seconds-s.progress);s.progress+=advanced;s.status=stage.kind=="human"?"人工处理":stage.kind=="machine"?"机器运行":"自然成熟";
                if(stage.kind=="human")
                {
                    var held=Tools(s,r,State.heroSite==s.id);if(held!=null)foreach(var tool in held)
                    {
                        double loss=r.tools.First(a=>a.item==Config.Item(tool.item).tool_family).quantity*advanced/Math.Max(1,r.human_seconds);tool.durability=Math.Max(0,tool.durability-loss);
                        if(tool.durability==0){var stock=s.tools.stacks.Contains(tool)?s.tools:State.backpack;stock.stacks.Remove(tool);s.output.Add("broken_"+tool.item,PlanStock.Scale,tool.origin,0,tool.instance);}
                    }
                }
                if(Config.Facility(s.type).hasDurability&&stage.kind!="natural"&&stage.kind!="lit_growth")
                {s.durability=Math.Max(0,s.durability-Config.Rule("maintenance.active_decay_per_second")*dt);if(s.durability==0)s.stopped=true;}
                if(s.progress+1e-8>=stage.seconds){s.progress=0;s.stage++;s.stageCommitted=false;ReleaseOperators(s);if(s.stage==r.stages.Length)CompleteBatch(s,r);}
            }
        }
        private int WorkPriority(PlanSite s)
        {
            if(s.priority!=3)return s.priority;
            if((s.type=="water_intake"||s.type=="water_purifier"||s.type=="auto_water_plant")&&Stored("water")<Protected("water"))return 5;
            if((s.type=="kitchen"||s.recipe=="wash_food")&&Stored("food")<Protected("food"))return 5;
            if(s.type=="growing"&&s.active&&Config.Recipe(s.recipe).stages[s.stage].kind=="human")return 4;return 3;
        }
    }
}
