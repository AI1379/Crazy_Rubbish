using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanReturnTests
    {
        private static PlanSimulation Fresh(bool unlock=true)
        {
            var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(c);m.State.cash=1000000;m.BuyRegion(2);m.BuyRegion(3);m.State.ground.Clear();m.State.warehouse.capacity=10000;
            if(unlock)foreach(var item in c.items)m.Discover(item.id);m.State.nextInput=1e9;m.State.player.nextMeal=m.State.player.nextServices=1e9;return m;
        }
        private static PlanSite Site(PlanSimulation m,string type,string recipe,int x=55,int y=12)
        {var s=new PlanSite{id=m.State.nextId++,type=type,recipe=recipe,x=x,y=y};s.input.capacity=s.output.capacity=10000;s.tools.capacity=100;foreach(var t in m.Config.recipes.Where(r=>r.facility==type).SelectMany(r=>r.tools).Select(t=>t.item).Distinct()){var tool=m.Config.items.First(i=>i.tool_family==t);s.tools.Add(tool.id,PlanStock.Scale,1,tool.durability,m.State.nextId++);}m.State.sites.Add(s);return s;}
        private static void Power(PlanSimulation m,PlanSite s,int count=4)
        {
            var f=m.Front(s);m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="power_line",x=f.x,y=f.y});
            for(int i=0;i<count;i++)m.State.sites.Add(new PlanSite{id=m.State.nextId++,type="generator",x=f.x-1,y=f.y+1,fuelSeconds=100000});
        }
        private static void Operators(PlanSimulation m,PlanSite s,int count=3)
        {var f=m.Front(s);for(int i=0;i<count;i++){var p=new PlanPerson{id=m.State.nextId++,x=f.x,y=f.y,manualAssignment=s.id,nextMeal=1e9,nextServices=1e9};if(i==2&&m.Config.Facility(s.type).maintainers>0){p.maintaining=s.id;p.manualAssignment=0;s.maintainer=p.id;}m.State.people.Add(p);}}
        private static void AdvanceUntil(PlanSimulation m,Func<bool> condition,int seconds=1000)
        {m.SetPaused(false);for(int i=0;i<seconds&&!condition();i++)m.Advance(1);Assert.That(condition(),Is.True,"Production did not finish: "+string.Join(";",m.State.sites.Where(s=>!string.IsNullOrEmpty(s.recipe)).Select(s=>s.recipe+":"+s.status)));}
        private static PlanSite Platform(PlanSimulation m)
        {var a=m.Config.Region(3).launch_platform_anchor;return Site(m,"lift_launch","build_lift",a[0],a[1]);}
        [Test] public void RegionThreeRequiresMoneyAndRegionTwoThenKeepsAllExistingAssets()
        {
            var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(c);Assert.Throws<InvalidOperationException>(()=>m.BuyRegion(3));m.State.cash=80000;m.BuyRegion(2);var sites=m.State.sites.Select(s=>s.id).ToArray();m.BuyRegion(3);
            Assert.That(m.State.cash,Is.Zero);Assert.That(m.State.region,Is.EqualTo(3));Assert.That(sites.All(id=>m.Site(id)!=null),Is.True);Assert.That(m.State.ground.Sum(g=>g.stock.Count("old_survey")),Is.EqualTo(PlanStock.Scale));Assert.That(m.State.springs.Count,Is.EqualTo(3));Assert.Throws<InvalidOperationException>(()=>m.BuyRegion(4));
        }
        [Test] public void MineralAndPlatformGroundsAreBuildableInThirdDistrictAndReachable()
        {
            var m=Fresh();foreach(var d in m.Config.facilities.Where(d=>new[]{"mine","lift_launch"}.Contains(d.id)))foreach(var v in d.build)m.State.warehouse.Add(v.item,20*v.Ticks);m.State.surveyedVeins.AddRange(PlanSimulation.MineralKinds);m.State.people.Add(new PlanPerson{id=m.State.nextId++});
            foreach(string kind in PlanSimulation.MineralKinds){var a=m.Vein(kind).anchor;Assert.That(PlanTerrain.ChamberAt(a[0],a[1]),Is.EqualTo(3));Assert.That(m.BuildProblem("mine",a[0],a[1]),Is.Null,kind);}
            var p=m.Config.Region(3).launch_platform_anchor;Assert.That(m.BuildProblem("lift_launch",p[0],p[1]),Is.Null);Assert.That(m.Path(9,4,p[0]+2,p[1]-1),Is.Not.Null);
        }
        [Test] public void UnsurveyedAndWronglyPlacedMinesDoNotChargeAnyMaterials()
        {
            var m=Fresh();foreach(var v in m.Config.Facility("mine").build)m.State.warehouse.Add(v.item,v.Ticks);var a=m.Vein("iron").anchor;string before=JsonUtility.ToJson(m.Capture());Assert.That(m.BuildProblem("mine",a[0],a[1]),Does.Contain("已勘探"));Assert.Throws<InvalidOperationException>(()=>m.Build("mine",a[0],a[1]));Assert.That(JsonUtility.ToJson(m.Capture()),Is.EqualTo(before));m.State.surveyedVeins.Add("iron");Assert.That(m.BuildProblem("mine",a[0],a[1],1),Does.Contain("初始朝向"));
        }
        [Test] public void SurveyRequiresActualWorkAndDoesNotInventOreOrRepeat()
        {
            var m=Fresh();var s=Site(m,"survey","survey_iron");Operators(m,s);Power(m,s);AdvanceUntil(m,()=>m.State.surveyedVeins.Contains("iron"));Assert.That(s.enabled,Is.False);Assert.That(s.output.stacks,Is.Empty);Assert.That(m.ProgressionProblem(s,m.Config.Recipe("survey_iron")),Is.Not.Null);m.SelectRecipe(s,"survey_copper");Assert.That(s.enabled,Is.True);
        }
        [Test] public void MatchingMineralOnlyAndAutomaticExtractionProducesActualOre()
        {
            var m=Fresh();m.State.surveyedVeins.Add("iron");var a=m.Vein("iron").anchor;var mine=Site(m,"mine","auto_extract_iron",a[0],a[1]);mine.addons.Add("feeder",PlanStock.Scale);mine.addons.Add("controller",PlanStock.Scale);Power(m,mine);Assert.That(m.ProgressionProblem(mine,m.Config.Recipe("extract_copper")),Does.Contain("铁矿"));AdvanceUntil(m,()=>mine.output.Count("iron_ore")>=8*PlanStock.Scale);Assert.That(mine.durability,Is.LessThan(100));
        }
        [Test] public void UniqueCabinNeverEntersOrdinaryInventoryAndTestDoesNotConsumeIt()
        {
            var m=Fresh();var p=Platform(m);foreach(var a in m.Config.Recipe("build_lift").inputs)p.input.Add(a.item,a.Ticks);Operators(m,p);Power(m,p);AdvanceUntil(m,()=>p.cabinId>0);int id=p.cabinId;Assert.That(p.output.Count("lift_cabin"),Is.Zero);Assert.That(p.input.Count("lift_cabin"),Is.Zero);Assert.That(m.ProgressionProblem(p,m.Config.Recipe("build_lift")),Is.Not.Null);
            m.SelectRecipe(p,"lift_test");p.durability=100;AdvanceUntil(m,()=>p.cabinTested);Assert.That(p.cabinId,Is.EqualTo(id));Assert.That(m.State.ending,Is.Null.Or.Empty);Assert.That(m.InspectFacility(p).missing,Is.Empty);Assert.Throws<InvalidOperationException>(()=>m.RemoveSite(p));
        }
        [Test] public void BoardRequiresTestAndActualProximityAndPreventsParallelHeroLabor()
        {
            var m=Fresh();var p=Platform(m);p.cabinId=55;Assert.Throws<InvalidOperationException>(()=>m.BoardLift(p));p.cabinTested=true;Assert.Throws<InvalidOperationException>(()=>m.BoardLift(p));var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;m.BoardLift(p);Assert.That(m.HeroAboard,Is.True);Assert.Throws<InvalidOperationException>(()=>m.AssignHero(p.id));Assert.Throws<InvalidOperationException>(()=>m.CommandMove(new[]{0},9,4));Assert.That(m.Walk(m.State.player,9,4,1),Is.False);Assert.Throws<InvalidOperationException>(()=>m.CancelBatch(p));Assert.Throws<InvalidOperationException>(()=>m.MoveSite(p,80,20));Assert.Throws<InvalidOperationException>(()=>m.RemoveSite(p,true));
        }
        [TestCase(0,"good")][TestCase(1,"normal")] public void ArrivalChoosesCorrectEndingAndNeedsNoSecondResident(double dumped,string ending)
        {
            var m=Fresh();m.State.dumped=dumped;var p=Platform(m);p.cabinId=123;p.cabinTested=true;var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;Power(m,p);m.BoardLift(p);AdvanceUntil(m,()=>!string.IsNullOrEmpty(m.State.ending),260);Assert.That(m.State.endingCode,Is.EqualTo(ending));Assert.That(m.State.paused,Is.True);Assert.That(m.State.seconds,Is.GreaterThanOrEqualTo(190));
        }
        [Test] public void PowerLossDurabilityAndManualPausePreserveAscentProgress()
        {
            var m=Fresh();var p=Platform(m);p.cabinId=5;p.cabinTested=true;var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;m.BoardLift(p);AdvanceUntil(m,()=>p.active&&p.stage==1,30);m.Advance(5);Assert.That(p.progress,Is.Zero);Power(m,p);m.Advance(5);double progress=p.progress;Assert.That(progress,Is.GreaterThan(0));m.SetSiteEnabled(p,false);m.Advance(10);Assert.That(p.progress,Is.EqualTo(progress));m.SetSiteEnabled(p,true);p.durability=0;p.stopped=true;m.Advance(5);Assert.That(p.progress,Is.EqualTo(progress));p.durability=20;m.Advance(1);Assert.That(p.progress,Is.GreaterThan(progress));
        }
        [Test] public void SaveRestorePreservesSurveyUniqueCabinAndBoardedBatch()
        {
            var m=Fresh();m.State.surveyedVeins.Add("clay");var p=Platform(m);p.cabinId=987;p.cabinTested=true;var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;Power(m,p);m.BoardLift(p);m.SetPaused(false);m.Advance(20);double progress=p.progress;
            var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(copy.State.surveyedVeins,Does.Contain("clay"));Assert.That(copy.Site(p.id).cabinId,Is.EqualTo(987));Assert.That(copy.HeroAboard,Is.True);Assert.That(copy.Site(p.id).progress,Is.EqualTo(progress));AdvanceUntil(copy,()=>!string.IsNullOrEmpty(copy.State.ending),220);Assert.That(copy.State.endingCode,Is.EqualTo("good"));
        }
        [Test] public void InvasionWinsIfItCoincidesWithArrival()
        {
            var m=Fresh();var p=Platform(m);p.cabinId=1;p.cabinTested=true;var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;Power(m,p);m.BoardLift(p);m.SetPaused(false);m.Advance(20);p.stage=1;p.progress=179.95;m.State.invasionAt=m.State.seconds+.1;m.Advance(.1);Assert.That(m.State.ending,Does.Contain("入侵"));Assert.That(m.State.endingCode,Is.Null.Or.Empty);
        }
        [Test] public void PausedBoardingCanBeSavedBeforeFirstTickAndContinued()
        {
            var m=Fresh();var p=Platform(m);p.cabinId=7;p.cabinTested=true;var f=m.Front(p);m.State.player.x=f.x;m.State.player.y=f.y;m.BoardLift(p);Assert.That(p.active,Is.False);
            var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(copy.HeroAboard,Is.True);Assert.That(copy.State.paused,Is.True);Power(copy,copy.Site(p.id));AdvanceUntil(copy,()=>copy.Site(p.id).active,2);
        }
        [Test] public void RegionUnlockRechecksKnownTechnologyButSurveyAloneDoesNotDiscoverOre()
        {
            var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));var m=new PlanSimulation(c);m.Discover("survey_module");m.Discover("controller");Assert.That(m.State.technologies,Does.Not.Contain("survey"));m.State.cash=80000;m.BuyRegion(2);m.BuyRegion(3);Assert.That(m.State.technologies,Does.Contain("survey"));m.State.surveyedVeins.Add("iron");m.Discover("drill_module");m.Discover("motor");Assert.That(m.State.technologies,Does.Not.Contain("ore_pre"));m.Discover("iron_ore");Assert.That(m.State.technologies,Does.Contain("ore_pre"));
        }
        [Test] public void ThirdDistrictSmallWaterSourceKeepsItsIdentityAcrossTheNaturalBoundary()
        {
            var m=Fresh();foreach(var a in m.Config.Facility("water_intake").build)m.State.warehouse.Add(a.item,5*a.Ticks);var anchor=m.Config.Region(3).freshwater_source.manual_anchor;
            var intake=m.Build("water_intake",anchor[0],anchor[1]);Assert.That(m.WaterSource(intake).region,Is.EqualTo(3));m.WaterSource(intake).remaining=4;
            var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(copy.WaterSource(copy.Site(intake.id)).remaining,Is.EqualTo(4));
        }
        [Test] public void ActualOreToPartsToCabinChainCanReachGoodEnding()
        {
            var m=Fresh();foreach(string id in new[]{"water","wood_chips","steel_parts","wire","motor","drive","controller","tool_steel_billet"})m.State.warehouse.Add(id,1000*PlanStock.Scale);m.State.warehouse.Add("old_structure",5*PlanStock.Scale);
            Power(m,m.State.sites.First(s=>s.type=="housing"));Power(m,m.State.sites.First(s=>s.type=="streetlamp"));
            void Produce(string recipe,int batches)
            {
                var r=m.Config.Recipe(recipe);var oldSites=m.State.sites.Select(s=>s.id).ToArray();string mineral=recipe.StartsWith("extract_")?recipe.Substring(8):null;int[] at=mineral==null?new[]{55,12}:m.Vein(mineral).anchor;
                var s=Site(m,r.facility,r.id,at[0],at[1]);Operators(m,s);Power(m,s);
                foreach(var input in r.inputs)s.input.Put(m.State.warehouse.Take(input.item,input.Ticks*batches));
                var before=r.outputs.ToDictionary(a=>a.item,a=>m.Stored(a.item));
                if(r.facility=="survey")AdvanceUntil(m,()=>m.State.surveyedVeins.Contains(recipe.Substring(7)),400);
                else AdvanceUntil(m,()=>r.outputs.All(a=>m.Stored(a.item)+s.output.Total(a.item)+m.Alive.Sum(p=>p.cargo.Total(a.item))>=before[a.item]+a.Ticks*batches),batches*500);
                m.RemoveSite(s);foreach(var p in m.Alive){m.State.warehouse.Put(p.cargo.stacks.ToArray());p.cargo.stacks.Clear();}m.State.people.Clear();
                foreach(var pile in m.State.ground)m.State.warehouse.Put(pile.stock.stacks.ToArray());m.State.ground.Clear();m.State.sites.RemoveAll(site=>!oldSites.Contains(site.id));m.State.revision++;
            }
            foreach(string mineral in PlanSimulation.MineralKinds)Produce("survey_"+mineral,1);
            Produce("open_old_structure",5);Produce("extract_iron",15);Produce("ore_sort_iron",10);Produce("ore_steel",8);
            Produce("extract_copper",8);Produce("ore_sort_copper",5);Produce("ore_copper",4);
            Produce("extract_clay",9);Produce("ore_sort_clay",7);Produce("ceramics",7);
            foreach(var pair in new[]{("build_lift_frame",5),("build_lift_drive",2),("build_lift_brake",2),("build_lift_control",1),("build_lift_cable",1)})Produce(pair.Item1,pair.Item2);
            var platform=Platform(m);Operators(m,platform);Power(m,platform);foreach(var a in m.Config.Recipe("build_lift").inputs)platform.input.Put(m.State.warehouse.Take(a.item,a.Ticks));
            AdvanceUntil(m,()=>platform.cabinId>0);platform.durability=100;m.SelectRecipe(platform,"lift_test");AdvanceUntil(m,()=>platform.cabinTested);platform.durability=100;var front=m.Front(platform);m.State.player.x=front.x;m.State.player.y=front.y;m.BoardLift(platform);AdvanceUntil(m,()=>!string.IsNullOrEmpty(m.State.ending));Assert.That(m.State.endingCode,Is.EqualTo("good"));
        }
    }
}
