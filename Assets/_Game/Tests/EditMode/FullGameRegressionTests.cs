using System;
using System.Linq;
using System.Reflection;
using CrazyRubbish.Core;
using CrazyRubbish.Prototype;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Tests
{
    public sealed partial class FullGameTests
    {
        [TestCase(2,"open_old_boiler")]
        [TestCase(4,"repair_clothes")]
        public void LegacyFacilitiesExposeExtendedRecipesAndKeepTheirOriginalControls(int kind,string recipe)
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Main.unity");
            try
            {
                var game=UnityEngine.Object.FindObjectOfType<PrototypeGame>();
                var hud=game.GetComponent<PrototypeHud>();
                Assert.That(hud.HasLayout,Is.True,"exercise the serialized scene layout upgrade");
                typeof(PrototypeGame).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                game.StartNewGame();
                var m=Fresh();var f=Place(m,PrototypeSimulation.FacilityKey(kind),21,12);
                m=PlayerAt(m,PrototypeSimulation.ServiceCell(f));f=m.GetFacility(f.Id);
                typeof(PrototypeGame).GetProperty("Model").SetValue(game,m);
                typeof(PrototypeHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                Button Find(string command)=>hud.VisualRoot.GetComponentsInChildren<Button>(true).Single(b=>b.name==command);
                void Click(string command)
                {
                    var button=Find(command);Assert.That(button.gameObject.activeInHierarchy,Is.True,command);
                    Assert.That(button.interactable,Is.True,command);button.onClick.Invoke();hud.RefreshView(m);
                }
                int legacyPage=kind==2?2:3;
                hud.ShowOwnedFacility(f);hud.RefreshView(m);
                Assert.That(hud.SelectedFacility,Is.EqualTo(legacyPage));
                Click("ProcessMode");Assert.That(hud.SelectedFacility,Is.EqualTo(9));
                Click("ProcessMode");Assert.That(hud.SelectedFacility,Is.EqualTo(legacyPage));
                Assert.That(Find(kind==2?"FacilityCommand9":"FacilityCommand29").gameObject.activeInHierarchy,Is.True);
                Click("ProcessMode");
                for(int i=0;i<PrototypeSimulation.ProcessRecipes(kind).Length&&f.ProcessRecipe!=recipe;i++)Click("GenericNext");
                Assert.That(f.ProcessRecipe,Is.EqualTo(recipe));
                var r=DesignCatalog.Recipe(recipe);
                foreach(var input in r.Inputs)Assert.That(m.Carry.TryAdd(input.Item,input.Whole),Is.True);
                foreach(var tool in r.Tools)
                    GiveTool(m,f,ItemCatalog.All.First(i=>ItemCatalog.IsTool(i)&&ItemCatalog.ToolFamily(i)==tool.Family));
                Click("GenericFeed");m.Advance(0.6);Require(m.LastInteraction);
                Click("GenericOperate");m.Advance(r.HumanSeconds+1);
                Assert.That(f.ProcessBatches,Is.EqualTo(1),m.ProcessStatus(f));
                foreach(var output in r.Outputs)Assert.That(f.ProcessOutput.Count(output.Item),Is.EqualTo(output.Whole));
                Click("GenericTake");m.Advance(0.6);Require(m.LastInteraction);
                Assert.That(f.ProcessOutput.UsedTicks,Is.Zero);
                hud.BuildLayout();
                Assert.That(hud.VisualRoot.GetComponentsInChildren<Button>(true).Count(b=>b.name=="ProcessMode"),Is.EqualTo(1));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CancelingAscentReleasesTheHeroAndSurvivesARealSaveRoundtrip(int stage)
        {
            var m=Fresh();Require(m.PurchaseRegion());Require(m.PurchaseRegion());
            var f=Place(m,"lift_launch",PrototypeSimulation.LaunchX,PrototypeSimulation.LaunchY);int id=f.Id;
            var side=PrototypeSimulation.ServiceCell(f);var s=m.Capture();s.powered=true;s.expansion.liftStage=2;
            m=PrototypeSimulation.Restore(s);Require(m.Recruit());Require(m.AssignProcessWorker(id,false));
            s=m.Capture();s.residents[0].x=side.x;s.residents[0].y=side.y;s.residents[0].needs.nextMeal=double.MaxValue;
            m=PrototypeSimulation.Restore(s);m=Operate(m,m.GetFacility(id),"lift_ascent");m.Advance(stage==0?1:11);
            f=m.GetFacility(id);Assert.That(f.ProcessActive,Is.True);Assert.That(f.ProcessStage,Is.EqualTo(stage));Assert.That(m.HeroAboard,Is.True);
            Require(m.CancelProcess(id));Assert.That(f.ProcessActive,Is.False);Assert.That(m.HeroAboard,Is.False);
            double x=m.PlayerX;m.MovePlayer(1,0,1);Assert.That(m.PlayerX,Is.GreaterThan(x));
            var copy=PrototypeSimulation.Restore(JsonUtility.FromJson<SimulationSnapshot>(JsonUtility.ToJson(m.Capture())));
            Assert.That(copy.HeroAboard,Is.False);Assert.That(copy.LiftStage,Is.EqualTo(2));
            x=copy.PlayerX;copy.MovePlayer(1,0,1);Assert.That(copy.PlayerX,Is.GreaterThan(x));
        }

        [TestCase(ItemId.WasteWood)]
        [TestCase(ItemId.WoodChips)]
        [TestCase(ItemId.DrinkingWater)]
        [TestCase(ItemId.IndustrialWater)]
        public void BoilerAcceptsManualFuelAndWaterButLeavesOtherItemsInTheBackpack(ItemId item)
        {
            var m=Fresh();var f=Place(m,"boiler",21,12);int id=f.Id;
            m=PlayerAt(m,PrototypeSimulation.ServiceCell(f));f=m.GetFacility(id);
            Assert.That(m.Carry.TryAdd(item,2),Is.True);Assert.That(m.Carry.TryAdd(ItemId.SteelPart,1),Is.True);
            Require(m.FeedProcess(id));m.Advance(0.6);Require(m.LastInteraction);
            Assert.That(f.ProcessInput.Count(item),Is.EqualTo(2));Assert.That(m.Carry.Count(item),Is.Zero);
            Assert.That(f.ProcessInput.Count(ItemId.SteelPart),Is.Zero);Assert.That(m.Carry.Count(ItemId.SteelPart),Is.EqualTo(1));
        }

        [TestCase(ItemId.DrinkingWater)]
        [TestCase(ItemId.IndustrialWater)]
        public void ThermalStationRunsOnItsInitialEightLitresAndStopsWhenTheLoopDries(ItemId water)
        {
            var m=Fresh();var b=Place(m,"boiler",21,12);var t=Place(m,"turbine",21,17);var c=Place(m,"condenser",25,17);
            b.ProcessInput.TryAdd(ItemId.WasteWood,10);b.ProcessInput.TryAdd(water,7);
            Power(m,t);Require(m.ConnectCommunityPower());Require(m.ToggleThermal(t.Id));m.Advance(1);
            Assert.That(t.ThermalRunning,Is.False,"startup still needs eight litres");Assert.That(t.ThermalStartup,Is.Zero);
            b.ProcessInput.TryAdd(water,1);m.Advance(20);Assert.That(t.ThermalRunning,Is.True);
            m.Advance(60);
            Assert.That(t.ThermalRunning,Is.True,m.ProcessStatus(t));Assert.That(b.WaterLoop,Is.EqualTo(7.9).Within(1e-6));
            Assert.That(b.ProcessInput.Count(water),Is.Zero);Assert.That(m.PowerNetworks.First(n=>n.FacilityIds.Contains(t.Id)).Output,Is.EqualTo(19).Within(1e-6));
            int bid=b.Id,tid=t.Id;m=Copy(m);b=m.GetFacility(bid);t=m.GetFacility(tid);m.Advance(1);
            Assert.That(t.ThermalRunning,Is.True,"partially depleted loop survives loading");
            b.WaterLoop=7;b.ProcessInput.TryAdd(water,1);m.Advance(0.05);
            Assert.That(b.ProcessInput.Count(water),Is.Zero);Assert.That(b.WaterLoop,Is.LessThanOrEqualTo(8));Assert.That(b.WaterLoop,Is.GreaterThan(7.99));
            b.WaterLoop=0;m.Advance(0.05);Assert.That(t.ThermalRunning,Is.False,"a dry loop must stop generation");
        }
    }
}
