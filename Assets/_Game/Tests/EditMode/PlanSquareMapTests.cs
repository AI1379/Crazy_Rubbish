using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanSquareMapTests
    {
        private static PlanSimulation Fresh()=>new PlanSimulation(JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json")));
        [Test]public void EntireSquareHasTerrainApartFromTheOriginalHole()
        {int floor=0;for(int y=0;y<250;y++)for(int x=0;x<250;x++){int r=PlanTerrain.ChamberAt(x,y);if(x>=2&&x<=3&&y>=20&&y<=21)Assert.That(r,Is.Zero);else{Assert.That(r,Is.InRange(1,3),x+","+y);floor++;}}Assert.That(floor,Is.EqualTo(62496));Assert.That(PlanTerrain.ChamberAt(120,20),Is.GreaterThan(0));}
        [Test]public void RiverNetworkRemainsContinuousApartFromTheStarterPond()
        {var wet=new HashSet<int>();for(int y=0;y<250;y++)for(int x=0;x<250;x++)if(PlanTerrain.WaterAt(x,y)>0&&!PlanTerrain.StarterPondAt(x,y))wet.Add(y*250+x);var reached=new HashSet<int>();var queue=new Queue<int>();int first=wet.First();reached.Add(first);queue.Enqueue(first);while(queue.Count>0){int c=queue.Dequeue();foreach(int n in new[]{c-1,c+1,c-250,c+250})if(Math.Abs(n%250-c%250)+Math.Abs(n/250-c/250)==1&&wet.Contains(n)&&reached.Add(n))queue.Enqueue(n);}Assert.That(reached.Count,Is.EqualTo(wet.Count));}
        [Test]public void RiverNetworkContinuesToAllFourMapEdges()
        {Assert.That(Enumerable.Range(0,250).Any(x=>PlanTerrain.WaterAt(x,249)>0),Is.True);Assert.That(Enumerable.Range(0,250).Any(x=>PlanTerrain.WaterAt(x,0)>0),Is.True);Assert.That(Enumerable.Range(0,250).Any(y=>PlanTerrain.WaterAt(0,y)>0),Is.True);Assert.That(Enumerable.Range(0,250).Any(y=>PlanTerrain.WaterAt(249,y)>0),Is.True);}
        [Test]public void BridgesKeepTheRiverbedWetAndAllowConstructionAboveIt()
        {int count=0;for(int y=0;y<250;y++)for(int x=0;x<250;x++)if(PlanTerrain.BridgeAt(x,y)&&PlanTerrain.WaterAt(x,y)>0){count++;Assert.That(PlanTerrain.RegionAt(x,y),Is.GreaterThan(0));Assert.That(PlanTerrain.Buildable(x,y),Is.True);Assert.That(PlanTerrain.MoveCost(x,y),Is.EqualTo(1));}Assert.That(count,Is.GreaterThan(40));}
        [Test]public void PhysicalRockPartitionsAreNotReplacedByEmptyTerrain()
        {Assert.That(PlanTerrain.BarrierCellCount,Is.GreaterThan(500));int checkedCells=0;for(int y=0;y<250;y++)for(int x=0;x<250;x++)if(PlanTerrain.BarrierAt(x,y)){checkedCells++;Assert.That(PlanTerrain.ChamberAt(x,y),Is.GreaterThan(0));Assert.That(PlanTerrain.RegionAt(x,y),Is.Zero);Assert.That(PlanTerrain.WaterAt(x,y),Is.Zero);}Assert.That(checkedCells,Is.EqualTo(PlanTerrain.BarrierCellCount));}
        [Test]public void OldFactoriesAndCargoReceiveFoundationsWithoutLosingTheirContents()
        {var m=Fresh();var site=new PlanSite{id=99000,type="wood",x=42,y=128};site.input.Add("waste_wood",PlanStock.Scale);m.State.sites.Add(site);m.State.mapRevision=4;long cash=m.State.cash;var restored=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));Assert.That(restored.Site(99000).input.Count("waste_wood"),Is.EqualTo(PlanStock.Scale));var size=restored.Size(site);for(int y=site.y;y<site.y+size.height;y++)for(int x=site.x;x<site.x+size.width;x++){Assert.That(restored.Owned(x,y),Is.True);Assert.That(restored.TerrainBuildable(x,y),Is.True);}Assert.That(PlanTerrain.WaterAt(43,129),Is.GreaterThan(0));Assert.That(restored.State.cash,Is.EqualTo(cash));int count=restored.State.terrainClearings.Count;var again=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(restored.Capture())));Assert.That(again.State.terrainClearings.Count,Is.EqualTo(count));}
    }
}
