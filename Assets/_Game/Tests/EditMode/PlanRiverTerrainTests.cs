using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanRiverTerrainTests
    {
        private static PlanSimulation Fresh()=>new PlanSimulation(JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json")));
        [Test]public void MapHasColonyScaleUsableTerrainAndRiversInEveryChamber()
        {Assert.That(PlanTerrain.Width,Is.EqualTo(250));Assert.That(PlanTerrain.Height,Is.EqualTo(250));for(int r=1;r<=3;r++){Assert.That(PlanTerrain.CellCount(r),Is.GreaterThan(7000));Assert.That(PlanTerrain.WaterCellCount(r),Is.GreaterThan(400));}}
        [Test]public void HistoricalGroundIsPreservedExceptForTheNewStarterPond()
        {foreach(string line in File.ReadAllLines("Assets/_Game/Tests/Fixtures/terrain-revision3.csv")){var n=line.Split(',').Select(int.Parse).ToArray();if(PlanTerrain.StarterPondAt(n[0],n[1])){Assert.That(PlanTerrain.WaterAt(n[0],n[1]),Is.GreaterThan(0),line);continue;}Assert.That(PlanTerrain.RegionAt(n[0],n[1]),Is.EqualTo(n[2]),line);Assert.That(PlanTerrain.Buildable(n[0],n[1]),Is.True,line);}}
        [Test]public void RiversHaveBlockedDeepWaterAndWalkableButUnbuildableShallows()
        {var m=Fresh();var deep=Enumerable.Range(90,150).SelectMany(y=>Enumerable.Range(2,70).Select(x=>(x,y))).First(c=>PlanTerrain.ChamberAt(c.x,c.y)==1&&PlanTerrain.WaterAt(c.x,c.y)==2&&!PlanTerrain.BridgeAt(c.x,c.y));var shallow=PlanTerrain.Cells(1).First(c=>PlanTerrain.WaterAt(c.x,c.y)==1&&!PlanTerrain.BridgeAt(c.x,c.y));Assert.That(m.Walkable(deep.x,deep.y),Is.False);Assert.That(m.Walkable(shallow.x,shallow.y),Is.True);Assert.That(PlanTerrain.Buildable(shallow.x,shallow.y),Is.False);foreach(var i in m.Config.items)m.Discover(i.id);foreach(string type in new[]{"wood","belt","power_line"}){Assert.Throws<InvalidOperationException>(()=>m.Build(type,deep.x,deep.y));Assert.Throws<InvalidOperationException>(()=>m.Build(type,shallow.x,shallow.y));}}
        [Test]public void ARouteCanCrossTheRiverAtTheNaturalFord()
        {var m=Fresh();var route=m.Path(30,129,57,129);Assert.That(route,Is.Not.Null);Assert.That(route.Any(c=>PlanTerrain.WaterAt(c%250,c/250)==1),Is.True);foreach(int c in route)Assert.That(PlanTerrain.WaterAt(c%250,c/250)!=2||PlanTerrain.BridgeAt(c%250,c/250),Is.True);}
        [Test]public void ShallowWaterActuallyReducesWalkingSpeed()
        {var m=Fresh();var a=PlanTerrain.Cells(1).First(c=>PlanTerrain.WaterAt(c.x,c.y)==1&&PlanTerrain.WaterAt(c.x+1,c.y)==1&&!PlanTerrain.BridgeAt(c.x,c.y)&&!PlanTerrain.BridgeAt(c.x+1,c.y));var wet=new PlanPerson{x=a.x,y=a.y};var dry=new PlanPerson{x=12,y=27};Assert.That(m.Walk(wet,a.x+1,a.y,.1),Is.True);Assert.That(m.Walk(dry,13,27,.1),Is.True);Assert.That(wet.x-a.x,Is.EqualTo((dry.x-12)/2.2).Within(.000001));}
        [Test]public void RepeatedWalkingRetainsItsRouteUntilGeometryChanges()
        {var m=Fresh();var destination=PlanTerrain.Cells(1).Where(c=>m.Walkable(c.x,c.y)).OrderBy(c=>(c.x-60)*(c.x-60)+(c.y-230)*(c.y-230)).First();Assert.That(m.Path(9,4,destination.x,destination.y),Is.Not.Null);int before=m.NavigationSearches;for(int i=0;i<20;i++)Assert.That(m.Walk(m.State.player,destination.x,destination.y,.1),Is.True);Assert.That(m.NavigationSearches-before,Is.LessThanOrEqualTo(1));var site=new PlanSite{id=900000,type="wood",x=12,y=25};m.State.sites.Add(site);Assert.That(m.Walk(m.State.player,destination.x,destination.y,.1),Is.True);Assert.That(m.NavigationSearches,Is.GreaterThan(before));}
        [Test]public void BuildingAndRemovingAnObstacleInvalidateCachedPaths()
        {var m=Fresh();var before=m.Path(11,26,17,26);Assert.That(before,Is.Not.Null);var site=new PlanSite{id=900000,type="wood",x=12,y=25};m.State.sites.Add(site);var around=m.Path(11,26,17,26);Assert.That(around,Is.Not.Null);Assert.That(around.Any(c=>m.Covers(site,c%250,c/250)),Is.False);m.State.sites.Remove(site);Assert.That(m.Path(11,26,17,26).Length,Is.EqualTo(before.Length));}
        [Test]public void LongRouteHasNoRowWrapsOrSolidWaterCells()
        {var m=Fresh();m.State.cash=20000;m.BuyRegion(2);var destination=PlanTerrain.Cells(2).Where(c=>m.Walkable(c.x,c.y)).OrderBy(c=>(c.x-145)*(c.x-145)+(c.y-230)*(c.y-230)).First();var route=m.Path(9,4,destination.x,destination.y);Assert.That(route,Is.Not.Null);int previous=4*250+9;foreach(int c in route){Assert.That(Math.Abs(c%250-previous%250)+Math.Abs(c/250-previous/250),Is.EqualTo(1));Assert.That(m.Walkable(c%250,c/250),Is.True);previous=c;}}
        [Test]public void EntireThreeChamberLayoutRemainsConnectedThroughFords()
        {var queue=new Queue<int>();var reached=new HashSet<int>();int first=PlanTerrain.Cells(1).First().y*250+PlanTerrain.Cells(1).First().x;queue.Enqueue(first);reached.Add(first);while(queue.Count>0){int c=queue.Dequeue();foreach(int n in new[]{c-1,c+1,c-250,c+250})if(Math.Abs(n%250-c%250)+Math.Abs(n/250-c/250)==1&&PlanTerrain.Available(n%250,n/250,3)&&reached.Add(n))queue.Enqueue(n);}Assert.That(reached.Count,Is.EqualTo(Enumerable.Range(1,3).Sum(r=>PlanTerrain.Cells(r).Count())),string.Join(";",Enumerable.Range(1,3).SelectMany(PlanTerrain.Cells).Where(c=>!reached.Contains(c.y*250+c.x)).Take(15).Select(c=>c.x+","+c.y)));}
        [Test]public void DismantlingABankSideBeltKeepsItsLargeCargoOnDryGround()
        {var m=Fresh();foreach(var i in m.Config.items)m.Discover(i.id);foreach(var a in m.Config.Facility("belt").build)m.State.warehouse.Add(a.item,a.Ticks);var bank=PlanTerrain.Cells(1).First(c=>PlanTerrain.Buildable(c.x,c.y)&&PlanTerrain.WaterAt(c.x+1,c.y)>0);var belt=m.Build("belt",bank.x,bank.y);string item=m.Config.items.First(i=>i.capacity>5).id;belt.input.Add(item,PlanStock.Scale);m.RemoveSite(belt);var cargo=m.State.ground.First(g=>g.stock.Count(item)>0);for(int x=cargo.x;x<cargo.x+cargo.width;x++)Assert.That(PlanTerrain.Buildable(x,cargo.y),Is.True);}
    }
}
