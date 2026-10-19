using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // One engine runs every v1 recipe that does not already have a dedicated first-region flow.
    // A batch reserves whole inputs, runs its human / machine / natural stages in order and only
    // commits inputs, tool wear and outputs once the final stage finishes and the outputs fit.
    public sealed partial class PrototypeSimulation
    {
        public const int ProcessOperateRole=7,ProcessMaintainRole=8;
        private static readonly Dictionary<int,RecipeDef[]> processRecipeCache=new Dictionary<int,RecipeDef[]>();
        public int UnrecognizedOtherPlastic {get;private set;}
        public static bool IsGenericKind(int kind)=>kind>=DesignCatalog.GenericKindBase;
        public static string FacilityKey(int kind)
        {
            switch(kind)
            {
                case 1:return "wood";case 2:return "disassembly";case 4:return "bench";case 8:return "generator";
                case 16:return "housing";case 32:return "streetlamp";case 64:return "electric_wood";case 128:return "auto_wood";
                default:return DesignCatalog.GenericFacility(kind)?.Key??"";
            }
        }
        public static int KindOf(string key)
        {
            switch(key)
            {
                case "wood":return 1;case "disassembly":return 2;case "bench":return 4;case "generator":return 8;
                case "housing":return 16;case "streetlamp":return 32;case "electric_wood":return 64;case "auto_wood":return 128;
                default:var def=DesignCatalog.Facility(key);return def==null?0:def.Kind;
            }
        }
        public static FacilityDef Definition(int kind)=>DesignCatalog.Facility(FacilityKey(kind));
        public static ItemAmount[] BuildCost(int kind)=>IsGenericKind(kind)?DesignCatalog.GenericFacility(kind).BuildCost:DesignBaseline.BuildCost(kind);
        public static int PortLimit(int kind)=>kind==-1?8:IsGenericKind(kind)?DesignCatalog.GenericFacility(kind).Ports:DesignBaseline.PortLimit(kind);
        // Recipes already served by the original wood, machine and fixed-template dismantling flows stay there.
        private static bool LegacyRecipe(RecipeDef r)=>r.Facility=="wood"||r.Facility=="electric_wood"||r.Facility=="auto_wood"||
            r.Facility=="disassembly"&&(r.Key=="scrap_old_motor"||r.Key.StartsWith("open_old_")&&(int)r.Inputs[0].Item<59);
        public static RecipeDef[] ProcessRecipes(int kind)
        {
            if(!processRecipeCache.TryGetValue(kind,out var recipes))
                processRecipeCache[kind]=recipes=DesignCatalog.RecipesAt(FacilityKey(kind)).Where(r=>!LegacyRecipe(r)).ToArray();
            return recipes;
        }
        public static bool RunsProcesses(int kind)=>ProcessRecipes(kind).Length>0||FacilityKey(kind)=="landfill";
        public static bool HasPosts(int kind)=>!IsWoodMachine(kind)&&(RunsProcesses(kind)||HasMaintenanceSide(kind));
        private static bool HasMaintenance(FacilityInstance f)=>IsGenericKind(f.Kind)&&Definition(f.Kind).Maintainers>0;
        public static string ProcessInputKey(int id)=>"procIn:"+id;
        public static string ProcessOutputKey(int id)=>"procOut:"+id;
        public static string ProcessToolKey(int id)=>"procTools:"+id;
        public IEnumerable<RecipeDef> AvailableProcesses(FacilityInstance f)=>ProcessRecipes(f.Kind).Where(r=>RecipeUnlocked(r)&&(r.Vein==""||r.Vein==f.Vein));

        private static double Credit(List<int> items,List<double> amounts,ItemId item){int i=items.IndexOf((int)item);return i<0?0:amounts[i];}
        private static void SetCredit(List<int> items,List<double> amounts,ItemId item,double value)
        {
            int i=items.IndexOf((int)item);if(Math.Abs(value)<1e-9)value=0;
            if(i<0){if(value!=0){items.Add((int)item);amounts.Add(value);}}else if(value==0){items.RemoveAt(i);amounts.RemoveAt(i);}else amounts[i]=value;
        }
        // Whole units still needed after applying the remaining fractional credit (soap units, litres).
        private static ItemAmount[] WholeInputs(FacilityInstance f,RecipeDef r)=>r.Inputs.Select(q=>new ItemAmount(q.Item,Math.Max(0,(int)Math.Ceiling(q.Amount-Credit(f.CreditItems,f.CreditAmounts,q.Item)-1e-9)))).Where(a=>a.Amount>0).ToArray();
        private ItemQuantity[] ProcessOutputs(FacilityInstance f,RecipeDef r)
        {
            var outputs=r.Outputs;
            if(r.Region2Composition!=null&&RegionAt(f.X,f.Y)>=2)outputs=r.Region2Composition;
            if(r.EffluentFraction>0&&r.EffluentAddon.All(a=>f.Addons.Contains((int)a.Item)))
            {
                double water=r.Inputs.Where(q=>q.Item==ItemId.DrinkingWater||q.Item==ItemId.IndustrialWater).Sum(q=>q.Amount);
                if(water>0)outputs=outputs.Concat(new[]{new ItemQuantity(ItemId.IndustrialEffluent,water*r.EffluentFraction)}).ToArray();
            }
            return outputs;
        }
        private ItemAmount[] WholeOutputs(FacilityInstance f,RecipeDef r)=>ProcessOutputs(f,r).GroupBy(q=>q.Item)
            .Select(g=>new ItemAmount(g.Key,(int)Math.Floor(g.Sum(q=>q.Amount)+Credit(f.OutCreditItems,f.OutCreditAmounts,g.Key)+1e-9))).Where(a=>a.Amount>0).ToArray();
        private static int Ticks(IEnumerable<ItemAmount> items)=>items.Sum(p=>p.Amount*ItemCatalog.CapacityTicks(p.Item));
        private static bool NeedsInstance(ItemId item)=>ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item);

        public ActionResult SelectProcess(int id,string recipeKey)
        {
            var f=GetFacility(id);if(f==null||!f.Built)return Fail("请选择已建成的设施。");
            var r=DesignCatalog.Recipe(recipeKey);
            if(r==null||!ProcessRecipes(f.Kind).Contains(r))return Fail("该设施没有这个配方。");
            if(!RecipeUnlocked(r))return Fail("先满足科技条件："+string.Join("、",r.Technologies.Where(t=>!TechnologyUnlocked(t)).Select(t=>DesignCatalog.Technology(t).Name)));
            if(r.Vein!=""&&r.Vein!=f.Vein)return Fail("采掘机必须建在对应且已勘探的矿脉上。");
            if(f.Kind==2&&(f.dismantleActive||f.DismantleRequested)||f.Kind==4&&f.assemblyToken!=0)return Fail("先停止并完成该地点原有的拆解或装配批次。");
            if(f.ProcessActive){f.PendingProcess=recipeKey;return Ok("配方排队：本批完成后切换，不产生半批成品。");}
            ApplyProcessRecipe(f,r);return Ok("已选择配方："+r.Name);
        }
        private void ApplyProcessRecipe(FacilityInstance f,RecipeDef r)
        {
            f.ProcessRecipe=r.Key;f.PendingProcess="";
            int input=Math.Max(8,(int)Math.Ceiling(2*Ticks(r.Inputs.Select(q=>new ItemAmount(q.Item,q.Whole)))/20.0)+1);
            int output=Math.Max(8,(int)Math.Ceiling(2*Ticks(ProcessOutputs(f,r).Select(q=>new ItemAmount(q.Item,Math.Max(1,q.Whole))))/20.0)+1);
            f.ProcessInput.SetCapacity(Math.Max(input,(int)Math.Ceiling(f.ProcessInput.UsedTicks/20.0)));
            f.ProcessOutput.SetCapacity(Math.Max(output,(int)Math.Ceiling(f.ProcessOutput.UsedTicks/20.0)));
        }
        public ActionResult ToggleProcess(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built||Failed)return Fail("请选择已建成的设施。");
            if(FacilityKey(f.Kind)=="landfill"){if(f.Sealed)return Fail("填埋区已封场。");f.ProcessRun=!f.ProcessRun;return Ok(f.ProcessRun?"开始接收并掩埋可处置物。":"填埋暂停。");}
            if(DesignCatalog.Recipe(f.ProcessRecipe)==null)return Fail("先选择配方。");
            f.ProcessRun=!f.ProcessRun;nextMachineStaffing=0;RecomputePower();
            return Ok(f.ProcessRun?"已请求生产；需要现场原料、工具、人员和必要的供电。":"已停产，在制批次与预约保留；维护继续。");
        }
        public ActionResult CancelProcess(int id)
        {
            var f=GetFacility(id);if(f==null||!f.ProcessActive)return Fail("没有在制批次。");
            if(f.ProcessCompleted)return Fail("已完成待出料的批次不能再退同批原料。");
            var r=DesignCatalog.Recipe(f.ActiveProcess);
            if(r?.CancelPackage!=""&&f.ProcessStage>0)
            {
                // Organic batches past their first stage cannot be un-mixed; they become an immature package.
                if(f.ProcessToken!=0)f.ProcessInput.Commit(f.ProcessToken);RemoveConsumedInstances(f,r);
                var package=DesignCatalog.Item(r.CancelPackage).Value;if(!f.ProcessOutput.TryAdd(package,1))ReturnAtOldSite(package,1,"",f.X,f.Y);
            }
            else if(f.ProcessToken!=0)f.ProcessInput.Release(f.ProcessToken);
            ClearProcessBatch(f);f.ProcessRun=false;if(manualFacilityId==id&&manualStationValue==ProcessOperateRole)LeaveManualRole();
            RecomputePower();return Ok("在制预约已释放；已用人时、燃料和机器磨损不返还。");
        }
        private void ClearProcessBatch(FacilityInstance f)
        {
            if(f.ActiveProcess=="lift_ascent")HeroAboard=false;
            foreach(var tool in tools.Where(t=>f.ProcessTools.Contains(t.Id)))tool.UsingAt=0;
            f.ProcessActive=f.ProcessCompleted=false;f.ProcessToken=0;f.ProcessStage=0;f.ProcessProgress=0;f.ActiveProcess="";f.ProcessTools=new int[0];
        }

        // ---- people at the post --------------------------------------------------------------
        private IEnumerable<(Resident person,bool hero)> ProcessCrew(FacilityInstance f,bool maintenance)
        {
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            if(manualStationValue==(maintenance?ProcessMaintainRole:ProcessOperateRole)&&manualFacilityId==f.Id&&Distance(PlayerX,PlayerY,side.x,side.y)<0.51)yield return(null,true);
            foreach(var r in residents.Where(r=>r.Job==(maintenance?WorkerJob.Maintenance:WorkerJob.MachineOperator)&&r.FacilityId==f.Id&&r.Delivery==null&&Distance(r.X,r.Y,side.x,side.y)<0.1))yield return(r,false);
        }
        private double CrewEfficiency(FacilityInstance f,bool maintenance,double tool,out int present)
        {
            var crew=ProcessCrew(f,maintenance).ToArray();present=crew.Length;if(present==0)return 0;
            var p=maintenance?MaintenanceCell(f):ServiceCell(f);
            return crew.Min(c=>{var needs=c.person?.Needs??PlayerNeeds;return DesignRules.HumanEfficiency(tool,BonusFactor,c.person==null?70:c.person.Happiness,c.person==null?0:EquivalentUnpaidPeriods,needs.FoodMultiplier(GameSeconds),needs.WaterMultiplier(GameSeconds),PressureAt(p.x,p.y),1);});
        }
        private int RequiredOperators(FacilityInstance f)=>Math.Max(1,Definition(f.Kind)?.Operators??1);
        public ActionResult ToggleProcessPlayerRole(int id,bool maintenance)
        {
            var f=GetFacility(id);if(f==null||!f.Built||Failed||RiotActive)return Fail("当前不能进入岗位。");
            if(maintenance&&!HasMaintenance(f))return Fail("该设施没有专职维护工序。");
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            if(Distance(PlayerX,PlayerY,side.x,side.y)>0.51)return Fail(maintenance?"先前往维护侧。":"先到设施作业面。");
            int role=maintenance?ProcessMaintainRole:ProcessOperateRole;
            if(manualStationValue==role&&manualFacilityId==id){LeaveManualRole();return Ok("已退出岗位，批次进度保留。");}
            CancelInteraction();manualStationValue=role;manualFacilityId=id;if(!maintenance&&DesignCatalog.Recipe(f.ProcessRecipe)!=null)f.ProcessRun=true;
            return Ok(maintenance?"主角开始维护；移动或Esc退出。":"主角进入岗位；人工阶段按实际效率推进。");
        }
        public ActionResult AssignProcessWorker(int id,bool maintenance,int residentId=0)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!HasPosts(f.Kind))return Fail("没有该加工设施。");
            if(maintenance&&!HasMaintenance(f))return Fail("该设施不需要维护工。");
            WorkerJob job=maintenance?WorkerJob.Maintenance:WorkerJob.MachineOperator;
            int assigned=residents.Count(r=>r.Job==job&&r.FacilityId==id),limit=maintenance?1:RequiredOperators(f);
            if(assigned>=limit)
            {
                foreach(var r in residents.Where(r=>r.Job==job&&r.FacilityId==id))
                {r.Job=WorkerJob.Idle;r.FacilityId=0;}
                if(maintenance)f.SuppressAutoMaintenance=true;else f.SuppressAutoOperator=true;nextMachineStaffing=GameSeconds+2;
                return Ok("已手动释放该岗位，自动填岗不会立即填回；已携货仍会完成。");
            }
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            var candidate=residents.Where(r=>r.Job==WorkerJob.Idle&&r.Delivery==null&&!r.Rioting&&(residentId==0||r.Id==residentId)&&FindPath(r.X,r.Y,side.x,side.y)!=null)
                .OrderBy(r=>Distance(r.X,r.Y,side.x,side.y)).ThenBy(r=>r.Id).FirstOrDefault();
            if(candidate==null)return Fail("没有可到达岗位的空闲居民；正在搬运者先送完货。");
            candidate.Job=job;candidate.FacilityId=id;candidate.Path=null;
            if(maintenance)f.SuppressAutoMaintenance=false;else f.SuppressAutoOperator=false;
            return Ok(maintenance?"维护工正在前往维护侧。":"操作工正在前往作业面。");
        }
        private void StaffProcesses()
        {
            foreach(var f in facilities.Where(f=>f.Built&&HasPosts(f.Kind)).OrderBy(f=>f.Id))
            {
                if(HasMaintenance(f)&&!f.SuppressAutoMaintenance&&!residents.Any(r=>r.FacilityId==f.Id&&r.Job==WorkerJob.Maintenance)&&!(manualStationValue==ProcessMaintainRole&&manualFacilityId==f.Id))
                    AssignProcessWorker(f.Id,true);
                var r=DesignCatalog.Recipe(f.ProcessActive?f.ActiveProcess:f.ProcessRecipe);
                bool needsCrew=f.ProcessRun&&(r!=null&&r.Stages.Any(s=>s.Human)||FacilityKey(f.Kind)=="landfill");
                if(!needsCrew||f.SuppressAutoOperator)continue;
                int have=residents.Count(p=>p.FacilityId==f.Id&&p.Job==WorkerJob.MachineOperator)+(manualStationValue==ProcessOperateRole&&manualFacilityId==f.Id?1:0);
                if(have<RequiredOperators(f))AssignProcessWorker(f.Id,false);
            }
        }
        private bool TickProcessWorker(Resident person,double seconds)
        {
            if(person.Job!=WorkerJob.Maintenance&&person.Job!=WorkerJob.MachineOperator)return false;
            var f=GetFacility(person.FacilityId);
            if(f==null||!HasPosts(f.Kind))return false;
            if(!f.Built){person.Job=WorkerJob.Idle;person.FacilityId=0;return true;}
            if(person.Delivery!=null)return false;
            bool maintenance=person.Job==WorkerJob.Maintenance;
            var r=DesignCatalog.Recipe(f.ProcessActive?f.ActiveProcess:f.ProcessRecipe);
            bool working=maintenance||f.ProcessRun&&(FacilityKey(f.Kind)=="landfill"&&f.ProcessInput.UsedTicks>0||
                f.ProcessActive&&!f.ProcessCompleted&&r!=null&&r.Stages[f.ProcessStage].Human||!f.ProcessActive&&r!=null&&r.Stages.Length>0&&r.Stages[0].Human&&ProcessInputsReady(f,r));
            if(!working)return false;
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            person.Activity=maintenance?$"维护 #{f.Id} · 耐久 {f.Durability:0.#}":$"{Definition(f.Kind).Name} #{f.Id} 操作";
            WalkResident(person,side.x,side.y,seconds);
            if(person.Path==null)person.Activity="岗位无路径，等待恢复";
            return true;
        }

        // ---- tools --------------------------------------------------------------------------
        private bool ChooseProcessTools(FacilityInstance f,RecipeDef r,out int[] ids,out double efficiency)
        {
            var chosen=new List<int>();efficiency=1;ids=new int[0];
            bool hero=manualStationValue==ProcessOperateRole&&manualFacilityId==f.Id;
            foreach(var need in r.Tools)
            {
                var tool=tools.Where(t=>!chosen.Contains(t.Id)&&ItemCatalog.ToolFamily(t.Item)==need.Family&&t.Durability>0&&t.ReservedBy==0&&
                        (t.Location==ProcessToolKey(f.Id)||hero&&t.Location=="carry")&&(t.UsingAt==0||t.UsingAt==-f.Id))
                    .OrderByDescending(t=>ItemCatalog.ToolEfficiency(t.Item)).ThenByDescending(t=>t.Durability).ThenBy(t=>t.Id).FirstOrDefault();
                if(tool==null)return false;
                chosen.Add(tool.Id);
            }
            if(chosen.Count>0)efficiency=chosen.Max(id=>ItemCatalog.ToolEfficiency(tools.First(t=>t.Id==id).Item));
            ids=chosen.ToArray();return true;
        }
        private double ActiveToolEfficiency(FacilityInstance f)=>f.ProcessTools.Length==0?1:f.ProcessTools.Select(id=>tools.FirstOrDefault(t=>t.Id==id)).Where(t=>t!=null).Select(t=>ItemCatalog.ToolEfficiency(t.Item)).DefaultIfEmpty(1).Max();

        // ---- batch lifecycle ----------------------------------------------------------------
        private string ProcessBlocker(FacilityInstance f,RecipeDef r)
        {
            if(r==null)return "未选择配方";
            if(!RecipeUnlocked(r))return "科技条件不足";
            if(r.Vein!=""&&(r.Vein!=f.Vein||!surveyedVeins.Contains(r.Vein)))return "矿脉尚未勘探或采掘机不在矿脉上";
            if(r.Has("one_time_per_vein")&&surveyedVeins.Contains(SurveyVein(r)))return "该矿脉已经勘探";
            if(r.Addons.Any(a=>!f.Addons.Contains((int)a.Item)))return "缺少附件："+string.Join("、",r.Addons.Where(a=>!f.Addons.Contains((int)a.Item)).Select(a=>ItemCatalog.Name(a.Item)));
            if(r.Key=="build_lift"&&LiftStage>=1)return "平台上已有唯一升降舱";
            if(r.Key=="lift_test"&&LiftStage!=1)return LiftStage==0?"先完成升降舱总装":"升降舱已通过测试";
            if(r.Key=="lift_ascent"&&LiftStage!=2)return "升降舱需先通过测试";
            if(r.Key=="recognize_old_plastic"&&UnrecognizedOtherPlastic<10)return "没有足量未识别的其他塑料";
            if(!ProcessInputsReady(f,r))return "缺少现场原料";
            if(f.ProcessOutput.FreeTicks<Ticks(WholeOutputs(f,r)))return "输出缓冲已满";
            if(!ChooseProcessTools(f,r,out _,out _))return "缺少工具："+string.Join("、",r.Tools.Select(t=>ToolFamilyName(t.Family)));
            if(HasMaintenance(f)&&(f.MachineTripped||f.Durability<=0))return "耐久停机，等待维护";
            if(r.Stages.Length>0&&r.Stages[0].Human&&ProcessCrew(f,false).Count()<RequiredOperators(f))return RequiredOperators(f)>1?$"需要{RequiredOperators(f)}名操作人员到岗":"缺少操作人员";
            if(r.Key=="lift_ascent"&&!ProcessCrew(f,false).Any(c=>c.hero))return "主角必须亲自登舱";
            return null;
        }
        private bool ProcessInputsReady(FacilityInstance f,RecipeDef r)
        {
            foreach(var need in WholeInputs(f,r))if(f.ProcessInput.Available(need.Item)<need.Amount)return false;
            if(r.RequiresState!="")
            {
                int state=r.RequiresState=="usable"?0:r.RequiresState=="repairable"?1:2;
                var item=r.Inputs[0].Item;
                if(!tools.Any(t=>t.Item==item&&t.Location==ProcessInputKey(f.Id)&&t.ReservedBy==0&&t.Condition==state))return false;
            }
            return true;
        }
        private static string SurveyVein(RecipeDef r)=>r.Key.StartsWith("survey_")?r.Key.Substring(7):"";
        public static string ToolFamilyName(string family)
        {
            switch(family){case "handsaw":return "手锯";case "screwdriver":return "螺丝刀";case "wrench":return "扳手";case "scissors":return "剪刀";
                case "sewing":return "针线";case "tongs":return "夹钳";case "file":return "锉刀";case "hammer":return "锤子";case "broom":return "扫帚";default:return family;}
        }
        private void TryStartProcess(FacilityInstance f)
        {
            if(!string.IsNullOrEmpty(f.PendingProcess)){var pending=DesignCatalog.Recipe(f.PendingProcess);if(pending!=null)ApplyProcessRecipe(f,pending);}
            var r=DesignCatalog.Recipe(f.ProcessRecipe);
            if(!f.ProcessRun||RiotActive||ProcessBlocker(f,r)!=null)return;
            ChooseProcessTools(f,r,out var ids,out _);
            var whole=WholeInputs(f,r);
            int token=whole.Length==0?0:f.ProcessInput.Reserve(whole);
            if(whole.Length>0&&token==0)return;
            f.ProcessToken=token;f.ProcessActive=true;f.ProcessCompleted=false;f.ActiveProcess=r.Key;f.ProcessStage=0;f.ProcessProgress=0;f.ProcessTools=ids;
            foreach(var tool in tools.Where(t=>ids.Contains(t.Id)))tool.UsingAt=-f.Id;
            if(r.RequiresState!="")
            {
                int state=r.RequiresState=="usable"?0:r.RequiresState=="repairable"?1:2;
                tools.First(t=>t.Item==r.Inputs[0].Item&&t.Location==ProcessInputKey(f.Id)&&t.ReservedBy==0&&t.Condition==state).UsingAt=-f.Id;
            }
            if(r.Key=="lift_ascent")HeroAboard=true;
        }
        private double ProcessPowerRequest(FacilityInstance f)
        {
            if(!f.Built||!f.ProcessActive||f.ProcessCompleted||!f.ProcessRun||f.MachineTripped||HasMaintenance(f)&&f.Durability<=0)return 0;
            var r=DesignCatalog.Recipe(f.ActiveProcess);if(r==null||r.Power<=0)return 0;
            return r.Stages[f.ProcessStage].Kind=="machine"?r.Power:0;
        }
        private void TickProcesses(double seconds)
        {
            foreach(var f in facilities.Where(f=>f.Built&&RunsProcesses(f.Kind)&&!IsWoodMachine(f.Kind)).ToArray())
            {
                if(FacilityKey(f.Kind)=="landfill"){TickLandfill(f,seconds);continue;}
                bool maintained=HasMaintenance(f);
                if(maintained&&!RiotActive)
                {
                    double rate=CrewEfficiency(f,true,1,out int present);
                    if(present>0){double repair=Math.Min(DesignBaseline.MachineMaximum-f.Durability,seconds*DesignBaseline.MachineRepair*rate);f.Durability+=repair;f.RepairedDurability+=repair;}
                }
                if(f.MachineTripped&&f.Durability>=DesignBaseline.MachineRestart-1e-8){f.Durability=Math.Max(f.Durability,DesignBaseline.MachineRestart);f.MachineTripped=false;}
                if(!f.ProcessActive)TryStartProcess(f);
                if(f.ProcessActive&&!f.ProcessCompleted)AdvanceProcess(f,seconds,maintained);
                if(f.ProcessCompleted)CompleteProcess(f,false);
            }
        }
        private void AdvanceProcess(FacilityInstance f,double seconds,bool maintained)
        {
            var r=DesignCatalog.Recipe(f.ActiveProcess);var stage=r.Stages[f.ProcessStage];
            double rate=0,wearSeconds=0;
            bool broken=maintained&&(f.MachineTripped||f.Durability<=0);
            if(stage.Human)
            {
                double eff=CrewEfficiency(f,false,ActiveToolEfficiency(f),out int present);
                if(!broken&&f.ProcessRun&&!RiotActive&&present>=RequiredOperators(f)){rate=eff;wearSeconds=seconds;}
            }
            else if(stage.Kind=="machine")
            {
                if(!broken&&f.ProcessRun){if(r.Power<=0){rate=1;wearSeconds=seconds;}else{rate=f.StepPowerRatio;wearSeconds=seconds*f.StepPoweredFraction;}}
            }
            else if(stage.Lit)rate=GrowLampRatio(f);
            else rate=1;
            if(rate<=0)return;
            if(maintained&&wearSeconds>0)
            {
                double usable=Math.Min(wearSeconds,f.Durability/DesignBaseline.MachineDecay);
                if(usable<wearSeconds)rate*=usable/wearSeconds;
                f.Durability=Math.Max(0,f.Durability-usable*DesignBaseline.MachineDecay);if(f.Durability<=1e-8){f.Durability=0;f.MachineTripped=true;}
            }
            f.ProcessProgress=Math.Min(stage.Seconds,f.ProcessProgress+seconds*rate);
            if(f.ProcessProgress>=stage.Seconds-1e-6)
            {
                f.ProcessProgress=0;f.ProcessStage++;
                if(f.ProcessStage>=r.Stages.Length){f.ProcessStage=r.Stages.Length-1;f.ProcessProgress=stage.Seconds;f.ProcessCompleted=true;}
            }
        }
        private void RemoveConsumedInstances(FacilityInstance f,RecipeDef r)
        {
            string key=ProcessInputKey(f.Id);
            foreach(var q in r.Inputs.Where(q=>NeedsInstance(q.Item)))
            {
                var picked=tools.Where(t=>t.Item==q.Item&&t.Location==key&&t.ReservedBy==0).OrderByDescending(t=>t.UsingAt==-f.Id).ThenBy(t=>t.Id).Take(q.Whole).Select(t=>t.Id).ToArray();
                tools.RemoveAll(t=>picked.Contains(t.Id));
            }
        }
        private bool CompleteProcess(FacilityInstance f,bool evacuate)
        {
            var r=DesignCatalog.Recipe(f.ActiveProcess);
            var outputs=WholeOutputs(f,r);
            if(!evacuate&&f.ProcessOutput.FreeTicks<Ticks(outputs))return false;
            if(r.Tools.Length>0&&BrokenTools.FreeTicks<20*f.ProcessTools.Count(id=>tools.Any(t=>t.Id==id&&t.Durability<=r.Tools.Max(n=>n.Wear))))return false;
            var reserved=WholeInputs(f,r);
            if(f.ProcessToken!=0&&!f.ProcessInput.Commit(f.ProcessToken))throw new InvalidOperationException("加工在制预约丢失。");
            foreach(var q in r.Inputs)
            {
                int took=reserved.Where(a=>a.Item==q.Item).Sum(a=>a.Amount);
                SetCredit(f.CreditItems,f.CreditAmounts,q.Item,Credit(f.CreditItems,f.CreditAmounts,q.Item)+took-q.Amount);
            }
            RemoveConsumedInstances(f,r);
            foreach(var g in ProcessOutputs(f,r).GroupBy(q=>q.Item))
            {
                double total=g.Sum(q=>q.Amount)+Credit(f.OutCreditItems,f.OutCreditAmounts,g.Key);
                SetCredit(f.OutCreditItems,f.OutCreditAmounts,g.Key,total-Math.Floor(total+1e-9));
            }
            foreach(var p in outputs)
            {
                if(evacuate)ReturnAtOldSite(p.Item,p.Amount,"",f.X,f.Y);
                else if(!f.ProcessOutput.TryAdd(p.Item,p.Amount))throw new InvalidOperationException("整批输出容量未受保护。");
                if(NeedsInstance(p.Item)&&!evacuate)for(int i=0;i<p.Amount;i++)tools.Add(new ToolInstance{Id=nextToolId++,Item=p.Item,Location=ProcessOutputKey(f.Id),Durability=ItemCatalog.IsTool(p.Item)?ItemCatalog.ToolDurability(p.Item):0});
            }
            for(int i=0;i<r.Tools.Length&&i<f.ProcessTools.Length;i++)WearTool(f.ProcessTools[i],r.Tools[i].Wear);
            DiscoverProducts(outputs);
            if(r.Key=="sort_plastic")UnrecognizedOtherPlastic+=outputs.Where(p=>p.Item==ItemId.PlasticOther).Sum(p=>p.Amount);
            if(r.Key=="recognize_old_plastic")UnrecognizedOtherPlastic=Math.Max(0,UnrecognizedOtherPlastic-10);
            if(r.Has("one_time_per_vein"))surveyedVeins.Add(SurveyVein(r));
            if(r.Key=="build_lift")LiftStage=1;
            if(r.Key=="lift_test")LiftStage=2;
            if(r.Key=="lift_ascent"){HeroAboard=false;LiftStage=3;ReachUpperLayer();}
            f.ProcessBatches++;ProcessBatchesCompleted++;ClearProcessBatch(f);
            if(!string.IsNullOrEmpty(f.PendingProcess)){var next=DesignCatalog.Recipe(f.PendingProcess);if(next!=null)ApplyProcessRecipe(f,next);}
            return true;
        }
        public int ProcessBatchesCompleted {get;private set;}

        // ---- player hand-off ---------------------------------------------------------------
        private bool NearService(FacilityInstance f){var side=ServiceCell(f);return Near(side.x,side.y);}
        private IEnumerable<ItemId> AcceptedInputs(FacilityInstance f)
        {
            if(FacilityKey(f.Kind)=="landfill")return ItemCatalog.All.Where(DisposalAccepted);
            if(FacilityKey(f.Kind)=="boiler")return new[]{ItemId.WasteWood,ItemId.WoodChips,ItemId.DrinkingWater,ItemId.IndustrialWater};
            var r=DesignCatalog.Recipe(f.ProcessRecipe);if(r==null)return Enumerable.Empty<ItemId>();
            return r.Inputs.Select(q=>q.Item).Distinct();
        }
        public ActionResult FeedProcess(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built)return Fail("没有该设施。");
            if(!NearService(f))return Fail("先到设施作业面投料。");
            return QueueInteraction(()=>{
                if(!f.Built||!NearService(f))return Fail("位置或设施状态已改变。");
                int count=0;var r=DesignCatalog.Recipe(f.ProcessRecipe);
                foreach(var item in AcceptedInputs(f).ToArray())
                {
                    int limit=r==null?int.MaxValue:Math.Max(0,2*r.Inputs.Where(q=>q.Item==item).Sum(q=>q.Whole)-f.ProcessInput.Count(item));
                    count+=MoveStock(Carry,f.ProcessInput,"carry",ProcessInputKey(id),item,limit);
                }
                if(r!=null)foreach(var need in r.Tools)
                {
                    if(tools.Count(t=>t.Location==ProcessToolKey(id)&&ItemCatalog.ToolFamily(t.Item)==need.Family)>=2)continue;
                    var tool=tools.Where(t=>t.Location=="carry"&&t.ReservedBy==0&&t.UsingAt==0&&t.Durability>0&&ItemCatalog.ToolFamily(t.Item)==need.Family).OrderByDescending(t=>t.Durability).FirstOrDefault();
                    if(tool!=null)count+=MoveStock(Carry,f.ProcessToolSlot,"carry",ProcessToolKey(id),tool.Item,1);
                }
                return count>0?Ok("原料与工具已投入现场。",count):Fail("背包缺少适用原料或工具，或现场已满。");
            });
        }
        public ActionResult TakeProcessStock(int id,bool input=false)
        {
            var f=GetFacility(id);if(f==null||!f.Built)return Fail("没有该设施。");
            if(!NearService(f))return Fail("先到设施作业面取物。");
            return QueueInteraction(()=>{
                if(!NearService(f))return Fail("位置已改变。");
                var stock=input?f.ProcessInput:f.ProcessOutput;int count=0;
                foreach(var item in stock.Stocked().ToArray())count+=MoveStock(stock,Carry,input?ProcessInputKey(id):ProcessOutputKey(id),"carry",item,stock.Available(item));
                if(input)foreach(var item in f.ProcessToolSlot.Stocked().ToArray())count+=MoveStock(f.ProcessToolSlot,Carry,ProcessToolKey(id),"carry",item,f.ProcessToolSlot.Available(item));
                return count>0?Ok("物品已进入背包。",count):Fail("没有可取物品或背包已满；在制原料不能取走。");
            });
        }
        public IEnumerable<ItemId> MissingAddons(FacilityInstance f)
        {
            var r=DesignCatalog.Recipe(f.ProcessRecipe);if(r==null)return Enumerable.Empty<ItemId>();
            var wanted=r.Addons.Select(a=>a.Item);
            if(r.EffluentFraction>0&&TechnologyUnlocked("water_recovery"))wanted=wanted.Concat(r.EffluentAddon.Select(a=>a.Item));
            return wanted.Where(i=>!f.Addons.Contains((int)i)).Distinct().ToArray();
        }
        public ActionResult InstallAddons(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built)return Fail("没有该设施。");
            if(!NearService(f))return Fail("先到设施作业面安装附件。");
            var missing=MissingAddons(f).ToArray();if(missing.Length==0)return Fail("当前配方不缺附件。");
            return QueueInteraction(()=>{
                int count=0;
                foreach(var item in missing)if(Carry.Available(item)>0&&Carry.TryRemove(item,1)){f.Addons.Add((int)item);count++;}
                return count>0?Ok("附件已固定安装，拆除设施时返还。",count):Fail("背包缺少所需附件："+string.Join("、",missing.Select(ItemCatalog.Name)));
            });
        }

        // ---- deliveries ---------------------------------------------------------------------
        private DeliveryTask ProcessDelivery(Resident actor,bool output)
        {
            foreach(var f in facilities.Where(f=>f.Built&&RunsProcesses(f.Kind)&&!IsWoodMachine(f.Kind)).OrderBy(f=>f.Id))
            {
                var side=ServiceCell(f);DeliveryTask task;
                if(output)
                {
                    foreach(var item in f.ProcessOutput.Stocked().ToArray())
                    {if(PreferConveyorOutput(f.ProcessOutput,item))continue;task=Plan(actor,f.ProcessOutput,Warehouse,ProcessOutputKey(f.Id),"warehouse",item,side.x,side.y,WarehouseX,WarehouseY,f.ProcessOutput.Available(item));if(task!=null)return task;}
                    continue;
                }
                if(!f.ProcessRun)continue;
                if(FacilityKey(f.Kind)=="landfill")
                {
                    foreach(var item in Warehouse.Stocked().Where(DisposalAccepted).ToArray())
                    {task=Plan(actor,Warehouse,f.ProcessInput,"warehouse",ProcessInputKey(f.Id),item,WarehouseX,WarehouseY,side.x,side.y,Warehouse.Available(item));if(task!=null)return task;}
                    continue;
                }
                var r=DesignCatalog.Recipe(f.ProcessRecipe);if(r==null)continue;
                foreach(var need in r.Tools)
                {
                    if(tools.Any(t=>t.Location==ProcessToolKey(f.Id)&&ItemCatalog.ToolFamily(t.Item)==need.Family&&t.Durability>need.Wear))continue;
                    if(residents.Any(p=>p.Delivery?.Target==f.ProcessToolSlot))continue;
                    foreach(var item in ItemCatalog.All.Where(i=>ItemCatalog.ToolFamily(i)==need.Family).OrderByDescending(ItemCatalog.ToolEfficiency))
                    {task=Supply(actor,f.ProcessToolSlot,ProcessToolKey(f.Id),item,side.x,side.y,1);if(task!=null)return task;}
                }
                foreach(var q in r.Inputs)
                {
                    int whole=Math.Max(1,q.Whole),have=f.ProcessInput.Count(q.Item);
                    if(have>=whole||PreferConveyorInput(f.Id,q.Item))continue;
                    if(r.RequiresState!=""&&q.Item==r.Inputs[0].Item)
                    {
                        int state=r.RequiresState=="usable"?0:r.RequiresState=="repairable"?1:2;
                        if(!tools.Any(t=>t.Item==q.Item&&t.Location=="warehouse"&&t.ReservedBy==0&&t.Condition==state))continue;
                        task=Plan(actor,Warehouse,f.ProcessInput,"warehouse",ProcessInputKey(f.Id),q.Item,WarehouseX,WarehouseY,side.x,side.y,1,t=>t.Condition==state);if(task!=null)return task;
                        continue;
                    }
                    task=Supply(actor,f.ProcessInput,ProcessInputKey(f.Id),q.Item,side.x,side.y,2*whole-have);if(task!=null)return task;
                }
            }
            return null;
        }

        // ---- removal ------------------------------------------------------------------------
        private ActionResult EvacuateProcess(FacilityInstance f,bool sell,bool returnMaterials)
        {
            if(Interacting||Failed)return Fail("先结束当前交互；失败后不能拆迁。");
            if(f.Sealed)return Fail("已封场的填埋区是永久地块，不能拆除。");
            if(FacilityKey(f.Kind)=="lift_launch"&&(HeroAboard||LiftStage>=1)&&returnMaterials)return Fail("平台上有唯一升降舱，不能拆除或出售。");
            if(FacilityKey(f.Kind)=="warehouse"&&Warehouse.UsedTicks>(WarehouseCapacityWithout(f.Id))*20)return Fail("仓库存量超过拆除后的容量，先搬走物品。");
            if(f.ProcessActive)
            {
                if(f.ProcessCompleted)CompleteProcess(f,true);
                else{if(f.ProcessToken!=0)f.ProcessInput.Release(f.ProcessToken);ClearProcessBatch(f);}
            }
            foreach(var r in residents.Where(r=>r.Delivery!=null&&(r.Delivery.Source==f.ProcessOutput||r.Delivery.Source==f.ProcessInput||r.Delivery.Target==f.ProcessInput||r.Delivery.Target==f.ProcessToolSlot)))
            {
                var d=r.Delivery;if(!d.Picked){if(d.Source!=null)d.Source.Release(d.Token);else d.Ground.Reserved-=d.Amount;ClearDelivery(r);}
                else{d.Target=Warehouse;d.To="warehouse";d.TargetX=WarehouseX;d.TargetY=WarehouseY;r.Path=null;}
            }
            DetachFacilityPorts(f.Id);
            foreach(var entry in new[]{(f.ProcessInput,ProcessInputKey(f.Id)),(f.ProcessOutput,ProcessOutputKey(f.Id)),(f.ProcessToolSlot,ProcessToolKey(f.Id))})
                foreach(var item in entry.Item1.Stocked().ToArray()){int count=entry.Item1.Available(item);entry.Item1.TryRemove(item,count);ReturnAtOldSite(item,count,entry.Item2,f.X,f.Y);}
            foreach(int addon in f.Addons)ReturnAtOldSite((ItemId)addon,1,"",f.X,f.Y);f.Addons.Clear();
            if(f.fuelWood+f.fuelChips>0){ReturnAtOldSite(ItemId.WasteWood,f.fuelWood,"",f.X,f.Y);ReturnAtOldSite(ItemId.WoodChips,f.fuelChips,"",f.X,f.Y);f.fuelWood=f.fuelChips=0;}
            foreach(var r in residents.Where(r=>r.FacilityId==f.Id)){r.Job=WorkerJob.Idle;r.FacilityId=0;}
            if(manualFacilityId==f.Id&&manualStationValue>=ProcessOperateRole)LeaveManualRole();
            f.Built=false;f.ProcessRun=false;f.ThermalRunning=false;f.ThermalStartup=0;
            if(returnMaterials)
            {
                var costs=BuildCost(f.Kind);decimal durability=HasMaintenance(f)?(decimal)(0.5+f.Durability/200):1m;
                if(sell)cashCents+=(long)decimal.Round(costs.Sum(p=>ItemCatalog.Price(p.Item).buy*p.Amount)*0.45m*durability*100,0,MidpointRounding.AwayFromZero);
                else foreach(var p in costs)ReturnAtOldSite(p.Item,p.Amount,"",f.X,f.Y);
            }
            UpdateWarehouseCapacity();geometryRevision++;GroundRevision++;ConstructionRevision++;RecomputePower();
            return Ok(sell?"设施已出售，内部物品已返还。":"设施已清空，预约及岗位已释放。");
        }
        public string ProcessStatus(FacilityInstance f)
        {
            if(!f.Built)return f.Blueprint?"施工蓝图":"未建成";
            string key=FacilityKey(f.Kind);
            if(key=="landfill")return f.Sealed?"已封场 · 永久填埋":$"已埋 {f.LandfillTicks/20:0.#}/{Definition(f.Kind).Capacity:0} 容量"+(f.ProcessRun?" · 接收中":" · 暂停");
            if(key=="warehouse")return $"仓储扩容 +{Definition(f.Kind).Capacity:0}";
            if(key=="grow_lamp")return $"太阳灯 · 供电 {f.PowerRatio:P0}"+(GrowLampServing(f)!=null?" · 照明种植地":" · 附近没有生长中的种植地");
            if(key=="boiler"||key=="turbine"||key=="condenser")return ThermalStatus(f);
            var r=DesignCatalog.Recipe(f.ProcessActive?f.ActiveProcess:f.ProcessRecipe);
            if(r==null)return "未选择配方";
            string head=r.Name;
            if(f.ProcessActive)
            {
                var stage=r.Stages[f.ProcessStage];
                string kind=stage.Human?"人工":stage.Kind=="machine"?"机器":stage.Lit?"光照生长":"自然熟化";
                if(f.ProcessCompleted)return head+" · 已完成，等待输出空间或损坏工具槽";
                return $"{head} · 第{f.ProcessStage+1}/{r.Stages.Length}阶段{kind} {f.ProcessProgress:0.#}/{stage.Seconds:0}秒"+(HasMaintenance(f)&&f.MachineTripped?" · 耐久停机":"")+
                    (stage.Kind=="machine"&&r.Power>0&&f.PowerRatio<=0?" · 缺电":"")+(stage.Human&&ProcessCrew(f,false).Count()<RequiredOperators(f)?" · 等待操作人员":"");
            }
            if(!f.ProcessRun)return head+" · 已停产";
            return head+" · "+(ProcessBlocker(f,r)??"准备开工");
        }
    }
}
namespace CrazyRubbish.Core
{
    // Workbench self-supply: keeps one spare of each tool family that a running site needs.
    public sealed partial class PrototypeSimulation
    {
        public bool AutomaticToolMaking {get;private set;}
        private double toolMakingAt;
        public void SetAutomaticToolMaking(bool enabled){AutomaticToolMaking=enabled;toolMakingAt=0;}
        private System.Collections.Generic.IEnumerable<string> NeededToolFamilies()
        {
            if(facilities.Any(f=>f.Built&&f.Kind==1&&f.RunRequested))yield return "handsaw";
            if(facilities.Any(f=>f.Built&&f.Kind==2&&f.DismantleRequested))yield return "screwdriver";
            foreach(var f in facilities.Where(f=>f.Built&&f.ProcessRun))
            {var r=DesignCatalog.Recipe(f.ProcessRecipe);if(r!=null)foreach(var need in r.Tools)yield return need.Family;}
        }
        private void TickToolMaking()
        {
            if(!AutomaticToolMaking||GameSeconds<toolMakingAt)return;toolMakingAt=GameSeconds+10;
            foreach(var bench in facilities.Where(f=>f.Built&&f.Kind==4&&f.assemblyToken==0&&!f.ProcessActive))
            {
                if(bench.ProcessRun&&DesignCatalog.Recipe(bench.ProcessRecipe)?.Key.StartsWith("make_")==false)continue;
                bench.ProcessRun=false;
                foreach(var family in NeededToolFamilies().Distinct())
                {
                    bool spare=tools.Any(t=>ItemCatalog.ToolFamily(t.Item)==family&&t.Location=="warehouse"&&t.ReservedBy==0&&t.Durability>60);
                    if(spare)continue;
                    var recipe=ProcessRecipes(4).Where(r=>r.Key.StartsWith("make_")&&ItemCatalog.ToolFamily(r.Outputs[0].Item)==family&&RecipeUnlocked(r))
                        .FirstOrDefault(r=>r.Inputs.All(q=>Warehouse.Available(q.Item)+bench.ProcessInput.Available(q.Item)>=q.Whole));
                    if(recipe==null)continue;
                    ApplyProcessRecipe(bench,recipe);bench.ProcessRun=true;ToolSupplyNotice="工作台正在补制"+ToolFamilyName(family)+"；需要居民到岗并搬运握柄与工作端。";
                    break;
                }
            }
        }
    }
}
