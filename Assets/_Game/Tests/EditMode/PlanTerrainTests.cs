using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using NUnit.Framework;
using UnityEngine;

namespace CrazyRubbish.Tests
{
    public sealed class PlanTerrainTests
    {
        private static PlanSimulation Fresh(){var c=JsonUtility.FromJson<PlanConfig>(File.ReadAllText("Assets/_Game/Resources/Plan/RuntimeConfig.json"));return new PlanSimulation(c);}
        [Test]public void NaturalChambersRetainExistingConstructionAndReceivingCoordinates()
        {var m=Fresh();foreach(var r in m.Config.regions)foreach(var rect in new[]{r.land_rectangle,r.garbage_rectangle})for(int y=rect[1];y<=rect[3];y++)for(int x=rect[0];x<=rect[2];x++)if(PlanTerrain.StarterPondAt(x,y)){Assert.That(PlanTerrain.ChamberAt(x,y),Is.EqualTo(1));Assert.That(PlanTerrain.WaterAt(x,y),Is.GreaterThan(0));}else Assert.That(PlanTerrain.RegionAt(x,y),Is.InRange(1,r.id),x+","+y);}
        [Test]public void WallsAreClosedAndOwnedLandExtendsOutsideOldRectangles()
        {var m=Fresh();Assert.That(m.Owned(12,27),Is.True);Assert.That(m.Walkable(12,27),Is.True);Assert.That(m.Owned(-1,30),Is.False);Assert.That(m.Walkable(-1,30),Is.False);Assert.That(m.Walkable(2,20),Is.False);Assert.That(PlanTerrain.CellCount(1),Is.GreaterThan(324));}
        [Test]public void PaymentOpensActualGroundAndRoutesWithoutExtraResources()
        {var m=Fresh();Assert.That(m.Path(27,14,30,14),Is.Null);int acquired=m.State.acquired.Count;double before=m.State.ground.Sum(g=>g.stock.Used(m.Config));m.State.cash=20000;m.BuyRegion(2);Assert.That(m.Path(27,14,30,14),Is.Not.Null);Assert.That(m.Owned(41,29),Is.True);Assert.That(m.State.cash,Is.Zero);Assert.That(m.State.ground.Sum(g=>g.stock.Used(m.Config))-before,Is.EqualTo(201).Within(.00001));}
        [Test]public void AllOpenGroundIsConnectedWithoutCrossingFutureChambers()
        {for(int region=1;region<=2;region++){var cells=Enumerable.Range(1,region).SelectMany(PlanTerrain.Cells).ToArray();var reached=new HashSet<int>();var queue=new Queue<int>();int start=cells[0].y*PlanTerrain.Width+cells[0].x;reached.Add(start);queue.Enqueue(start);while(queue.Count>0){int at=queue.Dequeue();foreach(int n in new[]{at-1,at+1,at-PlanTerrain.Width,at+PlanTerrain.Width})if(Math.Abs(n%PlanTerrain.Width-at%PlanTerrain.Width)+Math.Abs(n/PlanTerrain.Width-at/PlanTerrain.Width)==1&&PlanTerrain.Available(n%PlanTerrain.Width,n/PlanTerrain.Width,region)&&reached.Add(n))queue.Enqueue(n);}Assert.That(reached.Count,Is.EqualTo(cells.Length),string.Join(";",cells.Where(c=>!reached.Contains(c.y*PlanTerrain.Width+c.x)).Take(15).Select(c=>c.x+","+c.y)));}}
        [Test]public void EdgeGroundCanBeBuiltWhileTheReceivingZoneStaysReserved()
        {var m=Fresh();foreach(var a in m.Config.Facility("wood").build){m.State.warehouse.Add(a.item,a.Ticks*2);m.Discover(a.item);}var s=m.Build("wood",12,25);Assert.That(s.pending,Is.True);Assert.Throws<InvalidOperationException>(()=>m.Build("wood",4,2));}
        [Test]public void OldSaveOutsideNewWallMigratesWithoutLosingInventoryOrCash()
        {var m=Fresh();m.State.player.x=85;m.State.player.y=31;m.State.cash=12345;m.State.backpack.Add("steel_parts",PlanStock.Scale);string legacy=JsonUtility.ToJson(m.Capture()).Replace("\"mapRevision\":"+PlanTerrain.Revision+",","");Assert.That(legacy,Does.Not.Contain("mapRevision"));var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(legacy));Assert.That(copy.Walkable((int)copy.State.player.x,(int)copy.State.player.y),Is.True);Assert.That(copy.State.cash,Is.EqualTo(12345));Assert.That(copy.State.backpack.Count("steel_parts"),Is.EqualTo(PlanStock.Scale));Assert.That(copy.State.mapRevision,Is.EqualTo(PlanTerrain.Revision));}
        [Test]public void ExpandedChambersHaveSubstantialUsableGroundAndRemainPaid()
        {var m=Fresh();Assert.That(PlanTerrain.CellCount(1),Is.GreaterThan(1200));Assert.That(PlanTerrain.CellCount(2),Is.GreaterThan(1200));Assert.That(m.Owned(12,54),Is.True);Assert.That(m.Owned(43,57),Is.False);m.State.cash=20000;m.BuyRegion(2);Assert.That(m.Owned(43,57),Is.True);Assert.That(m.State.cash,Is.Zero);Assert.That(m.Path(12,54,43,57),Is.Not.Null);}
        [Test]public void SolidTerrainBlocksPeopleBuildingsAndLogisticsWithoutTakingMaterials()
        {var m=Fresh();foreach(var i in m.Config.items)m.Discover(i.id);foreach(var f in m.Config.facilities)foreach(var a in f.build)m.State.warehouse.Add(a.item,a.Ticks*100);var before=JsonUtility.ToJson(m.State.warehouse);foreach(var f in PlanTerrain.Obstacles.Where(f=>f.region==1)){var cell=Enumerable.Range(f.MinY,f.MaxY-f.MinY+1).SelectMany(y=>Enumerable.Range(f.MinX,f.MaxX-f.MinX+1).Select(x=>(x,y))).First(c=>f.Covers(c.x,c.y));Assert.That(m.Owned(cell.x,cell.y),Is.False);Assert.That(m.Path(12,27,cell.x,cell.y),Is.Null);foreach(string type in new[]{"wood","belt","power_line"})Assert.Throws<InvalidOperationException>(()=>m.Build(type,cell.x,cell.y));}Assert.That(JsonUtility.ToJson(m.State.warehouse),Is.EqualTo(before));}
        [Test]public void PillarForcesAReachableDetourWithNoSolidCellsInRoute()
        {var m=Fresh();var route=m.Path(7,38,14,38);Assert.That(route,Is.Not.Null);Assert.That(route.Length,Is.GreaterThan(7));foreach(int cell in route)Assert.That(m.Walkable(cell%PlanTerrain.Width,cell/PlanTerrain.Width),Is.True);}
        [Test]public void RevisionTwoFactoryAndResourceLedgerSurviveExpansion()
        {var m=Fresh();foreach(var a in m.Config.Facility("wood").build){m.State.warehouse.Add(a.item,a.Ticks*2);m.Discover(a.item);}var site=m.Build("wood",12,25);site.input.Add("waste_wood",PlanStock.Scale);m.State.mapRevision=2;var copy=new PlanSimulation(m.Config,1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(m.Capture())));var restored=copy.Site(site.id);Assert.That(restored.x,Is.EqualTo(12));Assert.That(restored.y,Is.EqualTo(25));Assert.That(restored.input.Count("waste_wood"),Is.EqualTo(PlanStock.Scale));Assert.That(copy.State.mapRevision,Is.EqualTo(PlanTerrain.Revision));}
        [Test]public void FeaturesAreContainedInTheirRoomAndCentersAreUsable()
        {Assert.That(PlanTerrain.Obstacles.Select(f=>f.kind).Distinct().Count(),Is.EqualTo(3));foreach(var f in PlanTerrain.Obstacles)for(int y=f.MinY;y<=f.MaxY;y++)for(int x=f.MinX;x<=f.MaxX;x++)if(f.Covers(x,y))Assert.That(PlanTerrain.ChamberAt(x,y),Is.EqualTo(f.region));for(int r=1;r<=3;r++){var c=PlanTerrain.Center(r);Assert.That(PlanTerrain.RegionAt(c.x,c.y),Is.EqualTo(r));}}
    }
}
