using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        public bool HeroAboard=>State.aboardSite>0;
        public static readonly string[] MineralKinds={"iron","copper","clay"};
        public static string MineralName(string kind)=>kind=="iron"?"铁矿":kind=="copper"?"铜矿":"陶土矿";
        public PlanVein Vein(string kind)
        {var map=Config.Region(3).veins;return kind=="iron"?map.iron:kind=="copper"?map.copper:kind=="clay"?map.clay:null;}
        public string MineKind(PlanSite site)=>MineralKinds.FirstOrDefault(k=>Vein(k).anchor[0]==site.x&&Vein(k).anchor[1]==site.y);
        private void RestoreProgression()
        {if(State.surveyedVeins==null)State.surveyedVeins=new System.Collections.Generic.List<string>();Require(State.surveyedVeins.All(MineralKinds.Contains)&&State.sites.Count(s=>s.cabinId>0)<=1,"矿脉或唯一载具记录无效。");Require(State.aboardSite==0||State.sites.Any(s=>s.id==State.aboardSite&&s.type=="lift_launch"&&s.cabinId>0&&s.cabinTested&&(s.active||s.stage==0&&s.progress==0)&&s.recipe=="lift_ascent"),"返程载具记录不完整，原存档已保留。");}
        private void ValidateProgressionPlacement(string type,int x,int y,int rotation,int ignored=0)
        {
            if(type=="survey"||type=="mine"||type=="lift_launch"||type=="lift_assembly")Require(State.region>=3,"需先开放第三区域。");
            if(type=="mine")
            {
                var kind=MineralKinds.FirstOrDefault(k=>Vein(k).anchor[0]==x&&Vein(k).anchor[1]==y);
                Require(kind!=null&&State.surveyedVeins.Contains(kind),"采掘机须放在已勘探矿点的指定锚点。");Require(rotation%4==0,"矿点地基固定，请使用初始朝向。");
                Require(!State.sites.Any(s=>s.id!=ignored&&s.type=="mine"&&s.x==x&&s.y==y),"该矿脉已有采掘机。");
            }
            if(type=="lift_launch")
            {
                var anchor=Config.Region(3).launch_platform_anchor;Require(x==anchor[0]&&y==anchor[1]&&rotation%4==0,"检修平台须建在旧竖井固定地基（"+anchor[0]+","+anchor[1]+"），使用初始朝向。");
                Require(!State.sites.Any(s=>s.id!=ignored&&s.type==type),"只能建设一座返程检修平台。");
            }
        }
        public string ProgressionProblem(PlanSite s,PlanRecipe r)
        {
            if(r.facility=="survey")return State.region<3?"需先开放第三区域":State.surveyedVeins.Contains(r.id.Substring("survey_".Length))?"该矿脉已勘探完成，无需重复勘探":null;
            if(r.facility=="mine")
            {
                string kind=MineKind(s);if(kind==null||!State.surveyedVeins.Contains(kind))return "采掘机未连接已勘探矿脉";
                if(!r.id.EndsWith("_"+kind))return "此采掘机只能开采"+MineralName(kind);return null;
            }
            if(r.facility!="lift_launch")return null;
            var anchor=Config.Region(3).launch_platform_anchor;if(State.region<3||s.x!=anchor[0]||s.y!=anchor[1])return "需在第三区域旧竖井地基建设检修平台";
            if(r.id=="build_lift"&&s.cabinId>0)return "平台已有唯一升降舱，不能重复总装";
            if(r.id=="lift_test"&&s.cabinId==0)return "先完成升降舱总装";
            if(r.id=="lift_ascent")return s.cabinId==0?"先完成升降舱总装":!s.cabinTested?"升降舱尚未通过带载测试":State.aboardSite!=s.id?"请让主角到操作侧，确认登舱返程":null;
            return null;
        }
        public string ProgressionDescription(PlanSite s)
        {
            if(s.type=="survey")return "已勘探："+(State.surveyedVeins.Count==0?"无":string.Join("、",State.surveyedVeins.Select(MineralName)))+"\n完成勘探后，在区域页定位采掘锚点。";
            if(s.type=="mine")return "矿脉："+(MineKind(s)==null?"未连接":MineralName(MineKind(s)))+" · 暂定无限，需持续供电和维护。";
            if(s.type=="lift_launch")return s.cabinId==0?"尚未总装升降舱":(State.aboardSite==s.id?"主角已登舱，其他经营继续\n":"唯一升降舱 #"+s.cabinId+" · ")+(s.cabinTested?"已通过带载测试":"尚未通过带载测试");
            return "";
        }
        public void BoardLift(PlanSite s)
        {
            Require(s!=null&&s.type=="lift_launch"&&!s.pending,"先建成检修平台。");Require(!s.active,"先等待当前批次结束。");Require(!State.riot.active&&!HeroAboard,"暴动期间或已登舱时不能再次登舱。");Require(s.cabinId>0&&s.cabinTested,"先总装升降舱并完成带载测试。");
            Require(!State.player.dead&&string.IsNullOrEmpty(State.ending),"当前经营已结束。");ValidateProgressionPlacement(s.type,s.x,s.y,s.rotation,s.id);
            var f=Front(s);Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5,"主角需先走到平台操作侧。");Require(State.player.trip==null&&!(State.disposalWorker==0&&State.disposalHeld.stacks.Count>0),"先完成主角当前拾取或处置工作。");
            Require(RecipeAvailable(Config.Recipe("lift_ascent")),"返程科技尚未解锁。");s.enabled=true;State.aboardSite=s.id;State.heroSite=0;State.player.directed=false;State.player.activity="升降舱内";s.recipe="lift_ascent";s.pendingRecipe=null;s.progress=0;s.stage=0;s.autoTools=false;SetBuffers(s);State.revision++;
        }
        private void FinishProgressionBatch(PlanSite s,PlanRecipe r)
        {
            if(r.facility=="survey")
            {string kind=r.id.Substring("survey_".Length);if(!State.surveyedVeins.Contains(kind))State.surveyedVeins.Add(kind);s.enabled=false;s.status="勘探完成，请安排下一矿脉";}
            if(r.id=="build_lift"){s.cabinId=State.nextId++;s.cabinTested=false;Discover("lift_cabin");s.enabled=false;s.status="升降舱已总装，等待试车";}
            if(r.id=="lift_test"){s.cabinTested=true;s.enabled=false;s.status="带载测试通过，等待主角确认登舱";}
            if(r.id=="lift_ascent"){State.returnArrivalPending=true;s.enabled=false;s.status="已抵达上层";}
        }
        private void ResolveReturnArrival()
        {
            if(!State.returnArrivalPending||!string.IsNullOrEmpty(State.ending))return;State.returnArrivalPending=false;State.endingCode=State.dumped<=0?"good":"normal";
            State.ending=State.endingCode=="good"?"好结局：主角回到上层，中层的回收产线仍在运转。你没有向下一层转嫁垃圾。":"普通结局：主角回到上层。中层洞口仍在，倾倒带来的影响没有随返程消失。";State.paused=true;State.revision++;
        }
    }
}
