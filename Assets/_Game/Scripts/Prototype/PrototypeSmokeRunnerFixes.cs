using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeSmokeRunner
    {
        [Serializable] private sealed class FixesSmokeReport
        {
            public bool success,seededFixtures,extendedRecipes,ascentCancellation,boilerFeeding,thermalWater,saveRoundtrip;
            public int extendedRecipeBatches,ascentStages,boilerInputTypes,thermalWaterTypes;
            public double thermalRunSeconds;
        }

        // These isolated save fixtures exercise the shipped player and UI without requiring a full
        // three-region playthrough. Every operation under test uses the normal buttons/actions.
        private FixesSmokeReport VerifyFullGameFixes(string screenshot)
        {
            string saveRoot=Argument("-prototypeSaveDirectory");
            if(string.IsNullOrEmpty(saveRoot))throw new InvalidOperationException("Fixes smoke requires an isolated -prototypeSaveDirectory.");
            string fixtures=Path.Combine(Path.GetFullPath(saveRoot),"fixes-fixtures");Directory.CreateDirectory(fixtures);
            var hud=game.GetComponent<PrototypeHud>();var world=game.GetComponent<PrototypeWorld>();
            var report=new FixesSmokeReport{seededFixtures=true};
            string WriteFixture(PrototypeSimulation m,string name,(int x,int y)? position=null)
            {
                var s=m.Capture();s.slot=Guid.NewGuid().ToString("N");
                if(position.HasValue){s.playerX=position.Value.x;s.playerY=position.Value.y;}
                string path=Path.Combine(fixtures,name+".json");File.WriteAllText(path,JsonUtility.ToJson(s,true));return path;
            }
            void Refresh(){hud.RefreshView(game.Model);world.RefreshView(game.Model);Canvas.ForceUpdateCanvases();}
            PrototypeSimulation Load(string path)
            {
                game.LoadSavedGame(path);if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                Refresh();return game.Model;
            }
            Button Find(string command)=>hud.VisualRoot.GetComponentsInChildren<Button>(true).Single(b=>b.name==command);
            void Click(string command)
            {
                Refresh();var button=Find(command);
                if(!button.gameObject.activeInHierarchy||!button.interactable)throw new InvalidOperationException("Unavailable fixes control: "+command);
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Refresh();
            }
            void Shot(string name)
            {
                if(string.IsNullOrEmpty(screenshot))return;
                Refresh();CapturePreview(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot)),Path.GetFileNameWithoutExtension(screenshot)+"-fix-"+name+".png"));
            }
            void Select(string recipe,FacilityInstance f)
            {
                for(int i=0;i<PrototypeSimulation.ProcessRecipes(f.Kind).Length&&f.ProcessRecipe!=recipe;i++)Click("GenericNext");
                if(f.ProcessRecipe!=recipe)throw new InvalidOperationException("Cannot select fixes recipe "+recipe+": "+game.Notice);
            }
            PrototypeSimulation Roundtrip()
            {
                game.SaveGame();if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                game.LoadLatest();if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                Refresh();return game.Model;
            }
            string original=WriteFixture(game.Model,"original-session");
            try
            {
                foreach(var pair in new[]{(kind:2,recipe:"open_old_boiler"),(kind:4,recipe:"repair_clothes")})
                {
                    var m=FixesFixture();var f=FixesPlace(m,PrototypeSimulation.FacilityKey(pair.kind),21,12);int id=f.Id;
                    var r=DesignCatalog.Recipe(pair.recipe);
                    foreach(var need in r.Tools)
                    {
                        var item=ItemCatalog.All.First(i=>ItemCatalog.IsTool(i)&&ItemCatalog.ToolFamily(i)==need.Family);
                        Require(m.Buy(item),"buy fixture tool");PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
                        PrototypeStartingScenario.Act(m,()=>m.Withdraw(item,1));
                    }
                    foreach(var q in r.Inputs)if(!m.Carry.TryAdd(q.Item,q.Whole))throw new InvalidOperationException("Fixture input does not fit: "+q.Item);
                    m=Load(WriteFixture(m,"extended-"+pair.kind,PrototypeSimulation.ServiceCell(f)));f=m.GetFacility(id);
                    world.FocusPoint(f.X+1,f.Y+1,7);
                    world.HandleMapClick(new Vector2(f.X+0.5f,f.Y+0.5f),false);Refresh();
                    int legacyPage=pair.kind==2?2:3;
                    if(hud.SelectedFacility!=legacyPage)throw new InvalidOperationException("Map did not open legacy facility.");
                    Shot("legacy-"+pair.kind);Click("ProcessMode");
                    if(hud.SelectedFacility!=9)throw new InvalidOperationException("Extended recipe mode is inaccessible.");
                    Click("ProcessMode");
                    if(hud.SelectedFacility!=legacyPage)throw new InvalidOperationException("Original facility controls cannot be restored.");
                    Click(pair.kind==2?"FacilityCommand9":"FacilityCommand30");
                    if(pair.kind==2&&f.SelectedDevice==ItemId.OldCutter||pair.kind==4&&f.SelectedAssembly!=AssemblyRecipe.Hammer)
                        throw new InvalidOperationException("Original recipe controls stopped working.");
                    Click("ProcessMode");Select(pair.recipe,f);Click("GenericFeed");m.Advance(0.55);Require(m.LastInteraction,"feed extended recipe");
                    Click("GenericOperate");m.Advance(r.HumanSeconds+1);Refresh();
                    if(f.ProcessBatches!=1||r.Outputs.Any(q=>f.ProcessOutput.Count(q.Item)!=q.Whole))throw new InvalidOperationException("Extended recipe failed: "+m.ProcessStatus(f));
                    Shot("extended-"+pair.kind);Click("GenericTake");m.Advance(0.55);Require(m.LastInteraction,"take extended outputs");
                    if(f.ProcessOutput.UsedTicks!=0)throw new InvalidOperationException("Extended recipe output could not be collected.");
                    report.extendedRecipeBatches++;
                }
                report.extendedRecipes=report.extendedRecipeBatches==2;

                {
                    var m=FixesFixture();var b=FixesPlace(m,"boiler",21,12);int id=b.Id;
                    var accepted=new[]{ItemId.WasteWood,ItemId.WoodChips,ItemId.DrinkingWater,ItemId.IndustrialWater};
                    foreach(var item in accepted)if(!m.Carry.TryAdd(item,2))throw new InvalidOperationException("Boiler fixture input does not fit.");
                    if(!m.Carry.TryAdd(ItemId.SteelPart,1))throw new InvalidOperationException("Boiler fixture control does not fit.");
                    m=Load(WriteFixture(m,"boiler-feed",PrototypeSimulation.ServiceCell(b)));b=m.GetFacility(id);
                    hud.ShowOwnedFacility(b);world.FocusPoint(b.X+1,b.Y+1,7);Click("GenericFeed");m.Advance(0.55);Require(m.LastInteraction,"manual boiler feed");
                    if(accepted.Any(i=>b.ProcessInput.Count(i)!=2||m.Carry.Count(i)!=0)||m.Carry.Count(ItemId.SteelPart)!=1||b.ProcessInput.Count(ItemId.SteelPart)!=0)
                        throw new InvalidOperationException("Boiler did not accept exactly the fuel/water inputs.");
                    report.boilerInputTypes=accepted.Length;report.boilerFeeding=true;Shot("boiler-feed");
                }

                foreach(var water in new[]{ItemId.DrinkingWater,ItemId.IndustrialWater})
                {
                    var m=FixesFixture();var b=FixesPlace(m,"boiler",21,12);var t=FixesPlace(m,"turbine",21,17);FixesPlace(m,"condenser",25,17);
                    int bid=b.Id,tid=t.Id;b.ProcessInput.TryAdd(ItemId.WasteWood,10);b.ProcessInput.TryAdd(water,8);
                    Require(m.BuildGenerator(),"build startup generator");m.SelectedFacility(8).fuelWood=10;
                    var side=PrototypeSimulation.ServiceCell(t);Require(m.PlaceWire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,side.x,side.y)),"wire thermal station");
                    Require(m.ConnectCommunityPower(),"connect real thermal load");
                    m=Load(WriteFixture(m,"thermal-"+water));b=m.GetFacility(bid);t=m.GetFacility(tid);
                    hud.ShowOwnedFacility(t);world.FocusPoint(t.X+1,t.Y,10);Click("GenericThermal");m.Advance(20);
                    if(!t.ThermalRunning)throw new InvalidOperationException("Thermal station did not start on eight litres.");
                    m.Advance(60);
                    if(!t.ThermalRunning||Math.Abs(b.WaterLoop-7.9)>1e-6||b.ProcessInput.Count(water)!=0)
                        throw new InvalidOperationException("Thermal station stopped after evaporation: "+m.ProcessStatus(t));
                    m=Roundtrip();b=m.GetFacility(bid);t=m.GetFacility(tid);m.Advance(1);
                    if(!t.ThermalRunning||Math.Abs(b.WaterLoop-(7.9-0.1/60))>1e-6)throw new InvalidOperationException("Thermal water state failed real file reload.");
                    hud.ShowOwnedFacility(t);Shot("thermal-"+water);report.thermalWaterTypes++;report.thermalRunSeconds=61;
                }
                report.thermalWater=report.thermalWaterTypes==2;

                foreach(int stage in new[]{0,1})
                {
                    var m=FixesFixture();Require(m.PurchaseRegion(),"fixture region 2");Require(m.PurchaseRegion(),"fixture region 3");
                    var f=FixesPlace(m,"lift_launch",PrototypeSimulation.LaunchX,PrototypeSimulation.LaunchY);int id=f.Id;var side=PrototypeSimulation.ServiceCell(f);
                    var s=m.Capture();s.powered=true;s.expansion.liftStage=2;m=PrototypeSimulation.Restore(s);
                    Require(m.Recruit(),"fixture second operator");Require(m.AssignProcessWorker(id,false),"assign ascent operator");
                    s=m.Capture();s.residents[0].x=side.x;s.residents[0].y=side.y;s.residents[0].needs.nextMeal=double.MaxValue;m=PrototypeSimulation.Restore(s);
                    m=Load(WriteFixture(m,"ascent-"+stage,side));f=m.GetFacility(id);hud.ShowOwnedFacility(f);world.FocusPoint(f.X+1,f.Y,8);
                    Select("lift_ascent",f);Click("GenericOperate");m.Advance(stage==0?1:11);
                    if(!m.HeroAboard||!f.ProcessActive||f.ProcessStage!=stage)throw new InvalidOperationException("Ascent fixture did not enter the requested stage.");
                    Shot("ascent-before-"+stage);Click("GenericCancel");
                    if(m.HeroAboard||f.ProcessActive)throw new InvalidOperationException("Canceling ascent did not release the hero.");
                    double x=m.PlayerX;m.MovePlayer(1,0,1);if(m.PlayerX<=x)throw new InvalidOperationException("Hero cannot move after canceling ascent.");
                    m=Roundtrip();x=m.PlayerX;m.MovePlayer(1,0,1);
                    if(m.HeroAboard||m.LiftStage!=2||m.PlayerX<=x)throw new InvalidOperationException("Canceled ascent is locked after file reload.");
                    hud.ShowOwnedFacility(m.GetFacility(id));Shot("ascent-canceled-"+stage);report.ascentStages++;
                }
                report.ascentCancellation=report.ascentStages==2;report.saveRoundtrip=true;
                report.success=report.extendedRecipes&&report.boilerFeeding&&report.thermalWater&&report.ascentCancellation;
                Debug.Log("FULL_GAME_FIXES_SMOKE_PASS: extended recipe UI, boiler feeding, thermal evaporation and ascent cancellation with file reload.");
                return report;
            }
            finally { Load(original);hud.ClosePanels();Refresh(); }
        }

        private static PrototypeSimulation FixesFixture()
        {
            var m=new PrototypeSimulation(new PrototypeSettings{automaticInput=false,warehouseCapacity=20000},4271,true);
            foreach(var item in DesignCatalog.Facilities.SelectMany(f=>f.Build).Select(q=>q.Item).Distinct())m.Warehouse.TryAdd(item,60);
            m.PlayerNeeds.nextMeal=double.MaxValue;m.Tutorial.Skip();m.SetAutomaticMachineStaffing(false);m.SetLivingDelivery(false);
            var s=m.Capture();s.gradualOpening=false;s.cash=1000000;s.discovered=ItemCatalog.All.Select(i=>(int)i).ToArray();
            return PrototypeSimulation.Restore(s);
        }
        private static FacilityInstance FixesPlace(PrototypeSimulation m,string key,int x,int y)
        {
            var result=m.PlaceFacility(PrototypeSimulation.KindOf(key),x,y);Require(result,"place fixes fixture "+key);return m.GetFacility(result.Amount);
        }
    }
}
