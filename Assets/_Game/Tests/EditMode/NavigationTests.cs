using System;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;

namespace CrazyRubbish.Tests
{
    public sealed class NavigationTests
    {
        private static PrototypeSimulation Fresh(double x=8,double y=6)
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false});
            m.PlayerNeeds.nextMeal=double.MaxValue;m.SetLivingDelivery(false);
            foreach(var item in new[]{new ItemAmount(ItemId.WasteWood,8),new ItemAmount(ItemId.RecoveredFastener,4),
                new ItemAmount(ItemId.Plank,4),new ItemAmount(ItemId.SteelPart,8),new ItemAmount(ItemId.ThermalModule,1),
                new ItemAmount(ItemId.GeneratorModule,1),new ItemAmount(ItemId.RecoveredWire,2)})
                Assert.That(m.Warehouse.TryAdd(item.Item,item.Amount),Is.True);
            var s=m.Capture();s.playerX=x;s.playerY=y;s.discovered=new[]{(int)ItemId.Plank,(int)ItemId.SteelPart};
            return PrototypeSimulation.Restore(s);
        }
        private static (int x,int y) Site(int flag)
        {
            switch(flag)
            {
                case 1:return (PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                case 2:return (PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);
                case 4:return (PrototypeSimulation.TableX,PrototypeSimulation.TableY);
                default:return (PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY);
            }
        }
        private static ActionResult Build(PrototypeSimulation m,int flag)
        {
            switch(flag)
            {
                case 1:return m.BuildBench();case 2:return m.BuildDismantle();
                case 4:return m.BuildTable();default:return m.BuildGenerator();
            }
        }
        private static void MarkBuilt(SimulationSnapshot s,int flag)
        {
            s.bench=flag==1;s.dismantle=flag==2;s.table=flag==4;s.generator=flag==8;
        }
        private static ResidentSave ResidentAt(int x,int y)=>new ResidentSave
        {
            id=1,x=x,y=y,cash=500,cargo=new UnitInventory(8).Capture(),
            needs=new LivingNeeds{nextMeal=double.MaxValue}
        };
        [TestCase(1)][TestCase(2)][TestCase(4)][TestCase(8)]
        public void ConstructionCannotEnclosePlayerOrReserveMaterials(int flag)
        {
            var site=Site(flag);var m=Fresh(site.x,site.y+1);int stock=m.Warehouse.UsedTicks;
            var result=Build(m,flag);
            Assert.That(result.Success,Is.False,result.Message);
            Assert.That(m.AnyBuildPending,Is.False);Assert.That(m.Warehouse.UsedTicks,Is.EqualTo(stock));
            foreach(ItemId item in Enum.GetValues(typeof(ItemId)))Assert.That(m.Warehouse.Reserved(item),Is.Zero);
            m.MovePlayer(-1,0,1.25);Assert.That(m.PlayerX,Is.LessThan(site.x-0.9));
        }
        [Test]public void ConstructionCannotEncloseResident()
        {
            var s=Fresh().Capture();s.residents.Add(ResidentAt(PrototypeSimulation.BenchX,PrototypeSimulation.BenchY+1));
            var m=PrototypeSimulation.Restore(s);
            Assert.That(m.BuildBench().Success,Is.False);Assert.That(m.Warehouse.Reserved(ItemId.WasteWood),Is.Zero);
        }
        [Test]public void PendingConstructionWaitsForClearSiteAndOtherBlueprintsComplete()
        {
            var m=Fresh();m.SetPaused(true);Assert.That(m.BuildBench().Success,Is.True);Assert.That(m.BuildDismantle().Success,Is.True);
            var s=m.Capture();s.playerX=PrototypeSimulation.BenchX;s.playerY=PrototypeSimulation.BenchY+1;
            m=PrototypeSimulation.Restore(s);m.SetPaused(false);m.Advance(0.05);
            Assert.That(m.BenchBuilt,Is.False);Assert.That(m.BuildPending,Is.True);Assert.That(m.DismantleBuilt,Is.True);
            Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.EqualTo(4));Assert.That(m.Warehouse.Reserved(ItemId.WasteWood),Is.EqualTo(4));
            m.MovePlayer(-1,0,1.25);m.Advance(0.05);
            Assert.That(m.BenchBuilt,Is.True);Assert.That(m.AnyBuildPending,Is.False);Assert.That(m.Warehouse.Count(ItemId.WasteWood),Is.Zero);
        }
        [TestCase(1)][TestCase(2)][TestCase(4)][TestCase(8)]
        public void LoadingPreviouslyTrappedPlayerRecoversMovementAndKeepsStock(int flag)
        {
            var site=Site(flag);var original=Fresh();int stock=original.Warehouse.UsedTicks;var s=original.Capture();MarkBuilt(s,flag);
            s.playerX=site.x;s.playerY=site.y+1;s.paused=true;
            var m=PrototypeSimulation.Restore(s);
            Assert.That(m.IsWalkable((int)Math.Floor(m.PlayerX+0.5),(int)Math.Floor(m.PlayerY+0.5)),Is.True);
            Assert.That(PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,s.playerX,s.playerY),Is.LessThanOrEqualTo(2));
            Assert.That(m.Paused,Is.True);Assert.That(m.Warehouse.UsedTicks,Is.EqualTo(stock));
            m.SetPaused(false);Assert.DoesNotThrow(()=>PrototypeStartingScenario.MoveTo(m,site.x,site.y));
            Assert.That(PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,site.x,site.y),Is.LessThan(0.001));
        }
        [Test]public void LoadingTrappedResidentKeepsCargoAndRestartsPath()
        {
            var s=Fresh().Capture();s.bench=true;var resident=ResidentAt(PrototypeSimulation.BenchX,PrototypeSimulation.BenchY+1);
            var cargo=new UnitInventory(8);cargo.TryAdd(ItemId.WasteWood,2);resident.cargo=cargo.Capture();
            resident.path=new[]{(PrototypeSimulation.BenchY+1)*s.settings.mapWidth+PrototypeSimulation.BenchX};
            s.residents.Add(resident);var m=PrototypeSimulation.Restore(s);var person=m.Residents[0];
            Assert.That(m.IsWalkable((int)Math.Floor(person.X+0.5),(int)Math.Floor(person.Y+0.5)),Is.True);
            Assert.That(person.Cargo.Count(ItemId.WasteWood),Is.EqualTo(2));Assert.That(m.Capture().residents[0].path,Is.Null);
        }
        [Test]public void TwoDirectionInputCanSlideAlongWallWithoutPassingThroughIt()
        {
            var m=Fresh(10.4,3);m.MovePlayer(1,1,0.25);
            Assert.That(m.PlayerY,Is.GreaterThan(3));Assert.That(m.PlayerX,Is.LessThan(10.5));
            Assert.That(m.IsWalkable((int)Math.Floor(m.PlayerX+0.5),(int)Math.Floor(m.PlayerY+0.5)),Is.True);
        }
        [Test]public void BlockedVerticalInputCanFallBackToHorizontalDirection()
        {
            var m=Fresh(11,1.4);m.MovePlayer(1,2,0.25);
            Assert.That(m.PlayerX,Is.GreaterThan(11));Assert.That(m.PlayerY,Is.LessThan(1.5));
        }
        [Test]public void UnblockedTwoDirectionInputStillMovesInOneCardinalDirection()
        {
            var m=Fresh();m.MovePlayer(1,1,0.25);
            Assert.That(m.PlayerX,Is.EqualTo(8.2).Within(1e-6));Assert.That(m.PlayerY,Is.EqualTo(6));
        }
        [Test]public void PathStartUsesSameHalfCellBoundaryAsCollision()
        {
            var m=Fresh(12,4.5);
            Assert.That(m.IsWalkable(12,5),Is.True);Assert.That(m.FindPath(m.PlayerX,m.PlayerY,13,5),Is.Not.Null);
        }
    }
}
