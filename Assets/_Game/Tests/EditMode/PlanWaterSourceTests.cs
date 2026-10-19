using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanWaterSourceTests
    {
        private static PlanSimulation Fresh()
        {
            var m=new PlanSimulation(JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json")));
            m.State.cash=100000;m.BuyRegion(2);foreach(var item in m.Config.items)m.Discover(item.id);m.State.warehouse.capacity=10000;
            foreach(string type in new[]{"water_intake","auto_water_plant","water_purifier"})foreach(var a in m.Config.Facility(type).build)m.State.warehouse.Add(a.item,10*a.Ticks);return m;
        }
        private static PlanSite Bank(PlanSimulation m,string type)
        {
            foreach(var c in PlanTerrain.Cells(1))
            {
                if(c.x<26&&c.y<30)continue;var site=new PlanSite{type=type,x=c.x,y=c.y};var z=m.Size(site);var f=m.Front(site);
                if(!m.CanPlaceWaterIntake(site)||!m.Walkable(f.x,f.y)||m.State.sites.Any(s=>m.Covers(s,f.x,f.y)))continue;
                if(!Enumerable.Range(site.x,z.width).All(x=>Enumerable.Range(site.y,z.height).All(y=>m.Owned(x,y)&&m.TerrainBuildable(x,y)&&m.GroundCapacity(x,y)==0&&!m.Receiving(x,y)&&!m.State.sites.Any(s=>m.Covers(s,x,y)))))continue;
                if(m.Path(m.State.player.x,m.State.player.y,f.x,f.y)==null)continue;return site;
            }
            throw new InvalidOperationException("测试未找到可用河岸。");
        }
        [Test]public void StarterPondHasShallowBanksDeepCenterAndDryLegacyAnchors()
        {
            Assert.That(PlanTerrain.WaterAt(20,24),Is.EqualTo(2));Assert.That(PlanTerrain.StarterPondBody,Is.GreaterThan(0));
            Assert.That(PlanTerrain.WaterAt(18,24),Is.EqualTo(1));for(int y=21;y<=23;y++)for(int x=22;x<=23;x++)Assert.That(PlanTerrain.Buildable(x,y),Is.True);
            var m=Fresh();Assert.That(m.Path(9,4,23,21),Is.Not.Null);Assert.That(m.CanPlaceWaterIntake(new PlanSite{type="water_intake",x=22,y=22}),Is.True);
        }
        [Test]public void RiverBankCollectsRawWaterAndRequiresPurification()
        {
            var m=Fresh();var bank=Bank(m,"water_intake");var s=m.Build(bank.type,bank.x,bank.y);m.SelectRecipe(s,"collect_freshwater");var f=m.Front(s);
            m.State.player.x=f.x;m.State.player.y=f.y;m.AssignHero(s.id);m.SetPaused(false);m.Advance(35);
            Assert.That(s.output.Count("raw_freshwater"),Is.EqualTo(6*PlanStock.Scale));Assert.That(s.output.Count("water"),Is.Zero);Assert.That(s.output.stacks.All(a=>a.quality=="qualified_fresh_source"),Is.True);
            Assert.That(s.output.stacks.Count,Is.EqualTo(1));
            Assert.That(m.WaterSource(s).kind,Is.EqualTo("surface"));Assert.That(s.springBody,Is.Not.EqualTo(PlanTerrain.StarterPondBody));
        }
        [Test]public void InlandAndWaterSurfaceConstructionFailWithoutTakingMaterialsOrCreatingBudget()
        {
            var m=Fresh();string stock=JsonUtility.ToJson(m.State.warehouse);int count=m.State.springs.Count;
            Assert.Throws<InvalidOperationException>(()=>m.Build("water_intake",8,8));Assert.Throws<InvalidOperationException>(()=>m.Build("water_intake",20,24));
            Assert.That(JsonUtility.ToJson(m.State.warehouse),Is.EqualTo(stock));Assert.That(m.State.springs.Count,Is.EqualTo(count));
        }
        [Test]public void RiverIntakesShareBudgetAndCannotDoubleReserveTheLastBatch()
        {
            var m=Fresh();var a=Bank(m,"water_intake");var first=m.Build(a.type,a.x,a.y);var b=Bank(m,"water_intake");var second=m.Build(b.type,b.x,b.y);
            Assert.That(first.springBody,Is.EqualTo(second.springBody));Assert.That(m.WaterSource(first),Is.SameAs(m.WaterSource(second)));
            m.SelectRecipe(first,"collect_freshwater");m.SelectRecipe(second,"collect_freshwater");var source=m.WaterSource(first);source.remaining=6;
            m.SetPaused(false);m.Advance(.1);Assert.That(source.reserved,Is.EqualTo(6));Assert.That(new[]{first,second}.Count(s=>s.active),Is.EqualTo(1));
            m.CancelBatch(first);m.CancelBatch(second);Assert.That(source.reserved,Is.Zero);Assert.That(source.remaining,Is.LessThan(6.2));
        }
        [Test]public void AutomaticRiverIntakeCancellationReturnsOnlyDrawnWaterOnce()
        {
            var m=Fresh();var bank=Bank(m,"auto_water_plant");var s=m.Build(bank.type,bank.x,bank.y);m.SelectRecipe(s,"auto_drinking_water");s.input.Add("wood_chips",2500);s.input.Add("filter_media",PlanStock.Scale,1,0,m.State.nextId++);
            m.SetPaused(false);m.Advance(.1);var source=m.WaterSource(s);Assert.That(source.remaining,Is.EqualTo(114).Within(.001));
            long before=s.input.Count("raw_freshwater")+m.State.ground.Sum(g=>g.stock.Count("raw_freshwater"));m.CancelBatch(s);m.CancelBatch(s);Assert.That(s.input.Count("raw_freshwater")+m.State.ground.Sum(g=>g.stock.Count("raw_freshwater"))-before,Is.EqualTo(6*PlanStock.Scale));Assert.That(source.remaining,Is.EqualTo(114).Within(.001));Assert.That(s.output.Count("water"),Is.Zero);
        }
        [Test]public void RebuildingAndReloadingRiverIntakesDoNotRefillWaterBudgets()
        {
            var m=Fresh();var b=Bank(m,"water_intake");var s=m.Build(b.type,b.x,b.y);var source=m.WaterSource(s);source.remaining=17;int body=source.body;m.RemoveSite(s);
            // Demolition leaves physical materials on the ground; move them back for this reconstruction check.
            foreach(var g in m.State.ground.Where(g=>g.x>=b.x&&g.x<b.x+2&&g.y>=b.y&&g.y<b.y+2))g.stock.stacks.Clear();
            s=m.Build(b.type,b.x,b.y);Assert.That(m.WaterSource(s).remaining,Is.EqualTo(17));var restored=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));
            Assert.That(restored.WaterSource(restored.Site(s.id)).body,Is.EqualTo(body));Assert.That(restored.WaterSource(restored.Site(s.id)).remaining,Is.EqualTo(17));
        }
        [Test]public void StarterPondShoreSharesTheOriginalSmallSourceLimit()
        {
            var m=Fresh();var s=m.Build("water_intake",22,22);Assert.That(m.WaterSource(s),Is.SameAs(m.State.springs.First(p=>p.region==1)));
            Assert.That(m.CanPlaceWaterIntake(new PlanSite{type="water_intake",x=20,y=20}),Is.False);Assert.That(m.WaterSource(s).remaining,Is.EqualTo(12));
        }
        [Test]public void RevisionFiveFactoryOnTheNewPondKeepsItsInventoryAndFoundation()
        {
            var m=Fresh();var site=new PlanSite{id=99991,type="wood",x=19,y=24};site.input.Add("waste_wood",3*PlanStock.Scale);m.State.sites.Add(site);m.State.mapRevision=5;
            var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(copy.Site(site.id).input.Count("waste_wood"),Is.EqualTo(3*PlanStock.Scale));
            for(int y=24;y<=25;y++)for(int x=19;x<=20;x++){Assert.That(copy.TerrainBuildable(x,y),Is.True);Assert.That(copy.Owned(x,y),Is.True);Assert.That(PlanTerrain.WaterAt(x,y),Is.GreaterThan(0));}
            Assert.That(copy.State.mapRevision,Is.EqualTo(6));int count=copy.State.terrainClearings.Count;var again=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(copy.Capture())));Assert.That(again.State.terrainClearings.Count,Is.EqualTo(count));
        }
    }
}
