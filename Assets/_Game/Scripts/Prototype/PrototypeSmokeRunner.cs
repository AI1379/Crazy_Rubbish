using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // Runs only with an explicit command-line flag. Uses the same actions as the player.
    public sealed class PrototypeSmokeRunner : MonoBehaviour
    {
        public PrototypeGame game;
        private bool failed;

        [Serializable]
        private sealed class SmokeReport
        {
            public bool success, restart, development, saveRoundtrip, demoCompleted, tutorialCompleted,constructionVerified;
            public double demoMinutes;
            public int assembledTools, mealsConsumed;
            public long demoWagesPaidCents;
            public double money;
            public int batches, buttons, residents, deliveredTrips;
            public long wagesPaidCents;
            public string version,baselineVersion;
        }

        private IEnumerator Start()
        {
            if (!game.SmokeMode) yield break;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            yield return null;
            yield return null;
            string screenshot = Argument("-prototypeScreenshot");
            decimal earnedMoney = 0;
            long wagesPaid = 0;
            int recruited = 0;
            int deliveredTrips = 0;
            bool saveRoundtrip = false;
            int batches = 0;
            bool demoCompleted=false;double demoMinutes=0;int assembledTools=0,mealsConsumed=0;long demoWages=0;
            bool tutorialCompleted=false;
            if (!string.IsNullOrEmpty(screenshot))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot)));
                // A hidden batch-mode window has a black backbuffer on some Windows drivers.
                // Render the game and its UI into our own texture instead of capturing that window.
                try { CapturePreview(screenshot); }
                catch (Exception exception) { failed = true; Debug.LogException(exception); }
            }
            try { VerifyInventoryGrid(screenshot); VerifyTutorialCoach(screenshot); }
            catch (Exception exception) { failed = true; Debug.LogException(exception); }
            // The onboarding checks reset/load sessions. Let Unity dispose their old world views
            // before counting renderers in the independent presentation checks.
            yield return null;
            yield return null;
            try
            {
                game.Model.Advance(2.05);
                if (game.GetComponent<PrototypeHud>().VisualRoot.GetComponentsInChildren<Button>(true).Length < 20 || Camera.main == null)
                    throw new InvalidOperationException("Scene HUD or camera was not initialized.");
                VerifyHudBindings(screenshot);
                VerifyWorldPresentation(screenshot);
                PrototypeSimulation model = game.Model;
                bool tutorialRoute=Array.IndexOf(Environment.GetCommandLineArgs(),"-prototypeTutorialSmoke")>=0;
                if(tutorialRoute)PrototypeTutorialScenario.Run(model);else PrototypeStartingScenario.Run(model);
                if (model.Money <= 0 || model.TotalWagesPaidCents != 5000 || model.Residents.Count != 5 || model.DeliveredTrips <= 0)
                    throw new InvalidOperationException("M2起步、招募和首期发薪验证失败。");
                wagesPaid = model.TotalWagesPaidCents;
                recruited = model.Residents.Count;
                deliveredTrips = model.DeliveredTrips;
                if (!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
                {
                    decimal savedCash = model.Money;
                    int savedTutorialStep=model.Tutorial.step;bool savedTutorialActive=model.Tutorial.active;
                    game.SaveGame();
                    if (!game.LastActionSucceeded) throw new InvalidOperationException("Runtime save failed: " + game.Notice);
                    Require(model.SellTechnology(Technology.Wood), "change cash after save");
                    model.Tutorial.Skip();
                    game.LoadLatest();
                    model = game.Model;
                    if (!game.LastActionSucceeded || model.Money != savedCash || model.DeliveredTrips != deliveredTrips || model.TotalWagesPaidCents != wagesPaid || model.Tutorial.step!=savedTutorialStep || model.Tutorial.active!=savedTutorialActive)
                        throw new InvalidOperationException("Runtime file save/load did not restore cash, transport and payroll.");
                    saveRoundtrip = true;
                }
                if(!tutorialRoute)PrototypeDemoScenario.RunAfterStarting(model);
                tutorialCompleted=model.Tutorial.Completed;
                if(!tutorialCompleted)throw new InvalidOperationException("教程实际进度未到完成，或教程按钮未绑定。");
                if(!model.DemoCompleted||model.AssembledTools<1||model.MissingSupplyPeople>0||model.UnpaidWageCents>0||model.MealsConsumed==0)
                    throw new InvalidOperationException("完整demo目标、生活供应或工具续补未完成。");
                demoCompleted=model.DemoCompleted;demoMinutes=model.DemoCompletedAt/60;assembledTools=model.AssembledTools;mealsConsumed=model.MealsConsumed;demoWages=model.TotalWagesPaidCents;
                if(!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
                {
                    game.SaveGame();if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                    model.Tutorial.Skip();
                    game.LoadLatest();model=game.Model;
                    if(!game.LastActionSucceeded||!model.DemoCompleted||model.AssembledTools!=assembledTools||model.MealsConsumed!=mealsConsumed||!model.Tutorial.Completed||!model.Tutorial.active)
                        throw new InvalidOperationException("完整demo存档恢复失败。");
                }
                VerifyConstruction(screenshot);model=game.Model;
                double time = model.GameSeconds;
                model.SetPaused(true); model.Advance(10);
                if (Math.Abs(model.GameSeconds - time) > 0.0001) throw new InvalidOperationException("Pause did not stop game time.");
                model.SetPaused(false);
                if (failed) throw new InvalidOperationException("Runtime emitted an error or exception.");
                game.Report(new ActionResult(true, "完整首区demo验证成功：生活、工具制作、200钱、发薪与存档。"));
                earnedMoney = model.Money;
                batches = model.CompletedBatches;
            }
            catch (Exception exception)
            {
                failed = true;
                Debug.LogException(exception);
            }
            // Let presentation render the completed production state before quitting.
            yield return null;
            yield return null;
            if(!failed&&!string.IsNullOrEmpty(screenshot))game.GetComponent<PrototypeHud>().OpenInventory(true);
            // GraphicRaycaster requires a rendered canvas depth. A window opened and tested in
            // the same synchronous function still has depth -1, unlike a real subsequent click.
            yield return null;
            yield return null;
            if (!failed && !string.IsNullOrEmpty(screenshot))
            {
                string completed = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot)),
                    Path.GetFileNameWithoutExtension(screenshot) + "-completed.png");
                try
                {
                    var hud = game.GetComponent<PrototypeHud>();
                    VerifyInventoryPointerBlocking();hud.CloseInventory();
                    hud.ClosePanels(); hud.RefreshView(game.Model);
                    if (!hud.VisualRoot.Find("Guide Prompt").gameObject.activeSelf ||
                        !hud.VisualRoot.Find("Guide Prompt/Body/GuideAction/Label").GetComponent<Text>().text.Contains("完成"))
                        throw new InvalidOperationException("Completed tutorial has no acknowledgement card.");
                    CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + "-guide-complete.png"));
                    VerifyManagementLayout(screenshot); CapturePreview(completed);
                }
                catch (Exception exception) { failed = true; Debug.LogException(exception); }
            }
            if(!failed&&!string.IsNullOrEmpty(screenshot))
            {
                try{game.ShowResult();game.GetComponent<PrototypeHud>().RefreshView(game.Model);CapturePreview(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(screenshot)),Path.GetFileNameWithoutExtension(screenshot)+"-result.png"));game.ContinueAfterResult();}
                catch(Exception exception){failed=true;Debug.LogException(exception);}
            }
            game.ResetGame();
            yield return null;
            yield return null;
            try
            {
                int visiblePiles = 0;
                foreach (Transform child in game.GetComponent<PrototypeWorld>().VisualRoot.Find("Ground Items"))
                    if (child.name.StartsWith("GroundPile_", StringComparison.Ordinal)) visiblePiles++;
                if (game.Model.Money != 0 || game.Model.BenchBuilt || game.Model.GeneratorBuilt || game.Model.Residents.Count != 0 || game.Model.Carry.Used != 0
                    || game.Model.Warehouse.Used != 0 || visiblePiles != game.Model.Ground.Count)
                    throw new InvalidOperationException("Restart did not reset state and visible ground piles.");
                if (failed) throw new InvalidOperationException("Runtime emitted an error or exception.");
                if (game.GetComponent<PrototypeHud>().ManagementOpen || game.GetComponent<PrototypeHud>().SelectedFacility != 0 || game.GetComponent<PrototypeHud>().ZonesVisible)
                    throw new InvalidOperationException("New game retained open panels or area overlays.");
                string resultPath = Argument("-prototypeResults");
                if (!string.IsNullOrEmpty(resultPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath)));
                    File.WriteAllText(resultPath, JsonUtility.ToJson(new SmokeReport
                    {
                        success = true, restart = true, development = Debug.isDebugBuild,
                        money = (double)earnedMoney, batches = batches, buttons = FindObjectsOfType<Button>().Length,
                        wagesPaidCents = wagesPaid, residents = recruited, version = Application.version,baselineVersion=DesignBaseline.Version,constructionVerified=true,
                        deliveredTrips = deliveredTrips, saveRoundtrip = saveRoundtrip,demoCompleted=demoCompleted,demoMinutes=demoMinutes,assembledTools=assembledTools,mealsConsumed=mealsConsumed,demoWagesPaidCents=demoWages,tutorialCompleted=tutorialCompleted
                    }));
                }
                Debug.Log("PROTOTYPE_SMOKE_PASS: pickup, weighted storage, woodwork, dismantle, generator, recruitment, resident production, payroll, pause and restart.");
            }
            catch (Exception exception) { failed = true; Debug.LogException(exception); }
            Application.logMessageReceived -= OnLog;
            Application.Quit(failed ? 2 : 0);
        }

        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }

        private void VerifyConstruction(string screenshot)
        {
            var hud=game.GetComponent<PrototypeHud>();var world=game.GetComponent<PrototypeWorld>();var m=game.Model;
            Button Find(string name)=>hud.VisualRoot.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
            int original=m.SelectedFacility(1).Id;
            foreach(var cost in DesignBaseline.BuildCost(1))
                if(m.Warehouse.Available(cost.Item)+m.Carry.Available(cost.Item)<cost.Amount)Require(m.Buy(cost.Item,cost.Amount),"buy ordinary construction materials");
            var site=(from y in Enumerable.Range(6,17) from x in Enumerable.Range(10,17) where m.PlacementError(1,x,y,0)==null select (x,y)).First();
            hud.OpenManagement(0);Find("PlaceKind1").onClick.Invoke();
            if(!world.ConstructionActive||hud.ManagementOpen)throw new InvalidOperationException("Free construction control did not open map placement.");
            if(world.PreviewPlacementAtCell(site.x,site.y)!=null)throw new InvalidOperationException("Legal construction preview was rejected.");
            world.FocusPoint(site.x,site.y,8);hud.RefreshView(m);Canvas.ForceUpdateCanvases();
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-construction.png"));
            var placed=world.ConfirmPlacementAtCell(site.x,site.y);Require(placed,"confirm map placement");
            if(world.ConstructionActive||m.Facilities.Count(f=>f.Built&&f.Kind==1)<2)throw new InvalidOperationException("Second independent woodwork site did not build.");
            var f=m.GetFacility(placed.Amount);hud.ShowOwnedFacility(f);Find("MoveOwned").onClick.Invoke();
            if(!world.ConstructionActive)throw new InvalidOperationException("Move facility control is unbound.");world.CancelConstruction();
            hud.ShowOwnedFacility(f);hud.RefreshView(m);world.RefreshView(m);Canvas.ForceUpdateCanvases();
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-facility-instance.png"));
            if(!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
            {
                game.SaveGame();if(!game.LastActionSucceeded)throw new InvalidOperationException(game.Notice);
                game.LoadLatest();m=game.Model;f=m.GetFacility(placed.Amount);
                if(!game.LastActionSucceeded||f==null||!f.Built||f.X!=site.x||f.Y!=site.y||m.Wires.Count==0)throw new InvalidOperationException("Real construction/electrical file save did not restore.");
                world.RefreshView(m);
            }
            hud.ShowOwnedFacility(f);Find("RemoveOwned").onClick.Invoke();if(f.Built)throw new InvalidOperationException("Demolition control is unbound.");
            Require(m.SelectFacility(original),"restore selected production site");
            Find("LayWire").onClick.Invoke();if(!world.ConstructionActive)throw new InvalidOperationException("Wire control is unbound.");world.CancelConstruction();
            Find("EraseWire").onClick.Invoke();if(!world.ConstructionActive)throw new InvalidOperationException("Remove-wire control is unbound.");world.CancelConstruction();
            hud.ClosePanels();hud.RefreshView(m);world.RefreshView(m);
        }

        private void VerifyInventoryGrid(string screenshot)
        {
            var hud=game.GetComponent<PrototypeHud>();var world=game.GetComponent<PrototypeWorld>();var root=hud.VisualRoot;
            void Refresh(){hud.RefreshView(game.Model);world.RefreshView(game.Model);Canvas.ForceUpdateCanvases();}
            void Capture(string suffix){if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            int Slot(bool warehouse,ItemId item){game.Model.SyncInventoryGrids();return game.Model.Grid(warehouse).slots.FindIndex(s=>s.amount>0&&s.item==item);}
            game.StartNewGame();game.BeginTutorial();game.Model.Advance(9.05);Refresh();
            var saw=game.Model.Ground.First(p=>p.Item==ItemId.WoodHandSaw);
            PrototypeStartingScenario.MoveTo(game.Model,saw.X,saw.Y);PrototypeStartingScenario.Act(game.Model,()=>game.Model.PickUp(int.MaxValue,null,saw.Id));
            var food=game.Model.Ground.First(p=>p.Item==ItemId.Food);
            PrototypeStartingScenario.MoveTo(game.Model,food.X,food.Y);world.FocusPoint(food.X,food.Y,6);Refresh();
            var oldView=world.VisualRoot.Find("Ground Items/GroundPile_"+food.Id);
            Capture("-ground-before");
            PrototypeStartingScenario.Act(game.Model,()=>game.Model.PickUp(int.MaxValue,null,food.Id));Refresh();
            if(game.Model.Ground.Any(p=>p.Id==food.Id)||oldView.gameObject.activeInHierarchy)throw new InvalidOperationException("An exhausted picked pile is still visible.");
            Capture("-ground-after");
            int totalFood=game.Model.Carry.Count(ItemId.Food);
            hud.OpenInventory(false);Refresh();
            if(!hud.InventoryOpen||!root.Find("Inventory Window/Card/Upper Grid/Ground Slot 0").gameObject.activeInHierarchy)throw new InvalidOperationException("Backpack did not show nearby stacks.");
            var card=root.Find("Inventory Window/Card").GetComponent<RectTransform>();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(root,card);
            if(bounds.min.y<root.rect.yMin||bounds.max.y>root.rect.yMax||bounds.min.x<root.rect.xMin||bounds.max.x>root.rect.xMax)throw new InvalidOperationException("Inventory window leaves the screen.");
            hud.ClickInventorySlot(0,Slot(false,ItemId.Food),true);Refresh();
            if(game.Model.Hand==null||game.Model.Hand.amount!=(totalFood+1)/2)throw new InvalidOperationException("Right-click did not take half a stack.");
            root.Find("Inventory Window/Held Stack").GetComponent<RectTransform>().anchoredPosition=new Vector2(-140,70);Capture("-inventory-held");
            hud.ClickInventorySlot(0,10,true);Refresh();
            if(game.Model.CarryGrid.slots[10].amount!=1)throw new InvalidOperationException("Right-click did not place one item.");
            game.HandleEscape();
            if(hud.InventoryOpen||game.Model.Hand!=null||game.Model.Carry.Count(ItemId.Food)!=totalFood)throw new InvalidOperationException("Closing inventory lost or retained the held stack.");
            PrototypeStartingScenario.MoveTo(game.Model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
            hud.OpenWarehouse();Refresh();
            hud.ClickInventorySlot(0,Slot(false,ItemId.Food),false,true);game.Model.Advance(0.55);Refresh();
            if(!game.Model.LastInteraction.Success||game.Model.Hand!=null)throw new InvalidOperationException("Shift transfer failed.");
            var cells=root.GetComponentsInChildren<PrototypeInventorySlot>(true);
            int remaining=Slot(false,ItemId.Food),destination=Slot(true,ItemId.Food);
            var from=cells.First(c=>c.container==0&&c.index==remaining);var to=cells.First(c=>c.container==1&&c.index==destination);
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,pointerCurrentRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=to.gameObject}};
            UnityEngine.EventSystems.ExecuteEvents.Execute(from.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(from.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
            game.Model.Advance(0.55);Refresh();
            if(game.Model.Carry.Count(ItemId.Food)!=0||game.Model.Warehouse.Count(ItemId.Food)!=totalFood)throw new InvalidOperationException("Dragging between grids did not transfer the stack.");
            hud.ClickInventorySlot(1,Slot(true,ItemId.Food),true);hud.ClickInventorySlot(0,7);game.Model.Advance(0.55);Refresh();
            if(game.Model.Carry.Count(ItemId.Food)!=(totalFood+1)/2||game.Model.Hand!=null)throw new InvalidOperationException("Taking a split stack from warehouse failed.");
            hud.InspectInventorySlot(0,7);Refresh();Capture("-inventory-chest");
            hud.ClickInventorySlot(0,7);hud.ClickInventorySlot(1,20);game.HandleEscape();game.Model.Advance(0.55);
            if(game.Model.Carry.Count(ItemId.Food)+game.Model.Warehouse.Count(ItemId.Food)!=totalFood||game.Model.Hand!=null||game.Model.Interacting)throw new InvalidOperationException("Closing during a transfer did not safely cancel it.");
            game.StartNewGame();Refresh();
        }

        private void VerifyTutorialCoach(string screenshot)
        {
            var hud = game.GetComponent<PrototypeHud>();
            var root = hud.VisualRoot;
            Button Find(string name) => root.GetComponentsInChildren<Button>(true).First(button => button.name == name);
            void Refresh() { hud.RefreshView(game.Model); game.GetComponent<PrototypeWorld>().RefreshView(game.Model); Canvas.ForceUpdateCanvases(); }
            void Capture(string suffix)
            {
                if (!string.IsNullOrEmpty(screenshot)) CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + suffix + ".png"));
            }
            void CheckCard(string path)
            {
                var card = root.Find(path).GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, card);
                if (bounds.min.x < root.rect.xMin || bounds.max.x > root.rect.xMax || bounds.min.y < root.rect.yMin || bounds.max.y > root.rect.yMax)
                    throw new InvalidOperationException("Tutorial card leaves the screen: " + path);
                foreach (var label in card.GetComponentsInChildren<Text>())
                    if (!label.resizeTextForBestFit && label.preferredHeight > label.rectTransform.rect.height + 1)
                        throw new InvalidOperationException($"Tutorial text overflows: {label.name}, {label.preferredHeight} > {label.rectTransform.rect.height}");
            }
            Refresh();
            if (!root.Find("Tutorial Welcome").gameObject.activeSelf || !game.Model.Tutorial.IntroOpen ||
                !root.Find("Tutorial Welcome").GetComponent<Image>().raycastTarget)
                throw new InvalidOperationException("New session does not show a blocking tutorial welcome.");
            CheckCard("Tutorial Welcome/Card"); Capture("-welcome");
            Find("DismissGuide").onClick.Invoke(); Refresh();
            if (game.Model.Tutorial.active || root.Find("Tutorial Welcome").gameObject.activeSelf || root.Find("Guide Prompt").gameObject.activeSelf)
                throw new InvalidOperationException("Free play did not dismiss onboarding.");
            game.StartNewGame(); Refresh();
            Find("BeginGuide").onClick.Invoke(); Refresh();
            if(game.Model.Ground.Count!=0||!hud.KnowledgeOpen)throw new InvalidOperationException("Sparse opening or orientation is missing.");
            var world=game.GetComponent<PrototypeWorld>();
            foreach(string name in new[]{"WoodworkBench","Generator","Workbench","Dismantle"})
                if(world.VisualRoot.Find(name).GetComponent<SpriteRenderer>().enabled)throw new InvalidOperationException("Unbuilt facility is visible: "+name);
            if(world.VisualRoot.Find("ReceivingHole")==null)throw new InvalidOperationException("Receiving hole is missing.");
            for(int place=1;place<=4;place++)
            {
                if(game.Model.Tutorial.orientation!=place||game.Model.GameSeconds!=0)throw new InvalidOperationException("Orientation skips places or advances time.");
                if(hud.KnowledgeTarget(out Vector2 placeTarget)&&RectTransformUtility.RectangleContainsScreenPoint(root.Find("Knowledge Card").GetComponent<RectTransform>(),world.worldCamera.WorldToScreenPoint(placeTarget)))
                    throw new InvalidOperationException("Orientation target is obscured: "+place);
                CheckCard("Knowledge Card");Capture("-orientation"+place);
                Find("KnowledgeNext").onClick.Invoke();Refresh();
            }
            game.Model.Advance(2.05);Refresh();
            if(!hud.KnowledgeOpen||!root.Find("Knowledge Card/Description").GetComponent<Text>().text.Contains("手工锯木"))throw new InvalidOperationException("First saw has no explanation.");
            CheckCard("Knowledge Card");Capture("-first-discovery");Find("KnowledgeNext").onClick.Invoke();Refresh();
            var saw=game.Model.Ground.First(p=>p.Item==ItemId.WoodHandSaw);
            PrototypeStartingScenario.MoveTo(game.Model,saw.X,saw.Y);
            Refresh();while(hud.KnowledgeOpen){Find("KnowledgeNext").onClick.Invoke();Refresh();}
            world.FocusPoint(saw.X,saw.Y,6);
            if (game.Model.Tutorial.step != 1 || hud.ManagementOpen || root.Find("Tutorial Welcome").gameObject.activeSelf || !root.Find("Guide Prompt").gameObject.activeSelf)
                throw new InvalidOperationException("Start guide did not reveal the first actionable lesson.");
            var first = PrototypeTutorial.NextAction(game.Model, game.Model.Tutorial.step);
            Vector3 marker = game.GetComponent<PrototypeWorld>().worldCamera.WorldToScreenPoint(new Vector3(first.x, first.y));
            if (RectTransformUtility.RectangleContainsScreenPoint(root.Find("Guide Prompt").GetComponent<RectTransform>(), marker))
                throw new InvalidOperationException("The first tutorial target is hidden behind the coach.");
            CheckCard("Guide Prompt"); Capture("-guide");
            game.Model.SetPaused(true); Refresh();
            if (!Find("GuideAction").interactable || !Find("GuideAction").GetComponentInChildren<Text>().text.Contains("继续"))
                throw new InvalidOperationException("Paused tutorial has no resume action.");
            Find("GuideAction").onClick.Invoke();
            if (game.Model.Paused || game.Model.Carry.UsedTicks != 0) throw new InvalidOperationException("Resume performed a gameplay action.");
            Find("GuideAction").onClick.Invoke();
            if (!game.Model.Interacting || game.Model.Tutorial.step != 1) throw new InvalidOperationException("Coach bypassed timed pickup.");
            game.Model.Advance(0.55); Refresh();
            while(hud.KnowledgeOpen){Find("KnowledgeNext").onClick.Invoke();Refresh();}
            if (game.Model.Tutorial.step != 2 || !root.Find("Guide Prompt/Body/Checklist").GetComponent<Text>().text.Contains("0/12"))
                throw new InvalidOperationException("Coach did not advance after actual pickup.");
            CheckCard("Guide Prompt"); Capture("-supplies");
            Find("GuideCollapse").onClick.Invoke(); Refresh();
            if (root.Find("Guide Prompt/Body").gameObject.activeSelf) throw new InvalidOperationException("Coach did not collapse.");
            Find("GuideCollapse").onClick.Invoke(); Find("OpenGuide").onClick.Invoke(); Find("TutorialNext").onClick.Invoke(); Refresh();
            if (!root.Find("Guide Prompt/Objective").GetComponent<Text>().text.Equals(PrototypeTutorial.Titles[2]))
                throw new InvalidOperationException("Review changed the live coach lesson.");
            Find("GuideLocate").onClick.Invoke(); Refresh();
            if (!hud.InventoryOpen || !Find("InventoryPrimary").gameObject.activeInHierarchy)
                throw new InvalidOperationException("Tutorial does not reveal the grid inventory pickup control.");
            hud.ClosePanels();
            PrototypeStartingScenario.MoveTo(game.Model, PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY);
            Refresh();while(hud.KnowledgeOpen){Find("KnowledgeNext").onClick.Invoke();Refresh();}
            Find("GuideAction").onClick.Invoke(); Refresh();
            if (!game.Walking || Find("GuideAction").interactable) throw new InvalidOperationException("Coach does not show travel in progress.");
            Capture("-walking"); game.HandleEscape();
            world.FocusPoint(16,14,6);
            Vector3 cameraBefore=world.worldCamera.transform.position;
            game.Model.SetPaused(true);world.BeginMapDrag(new Vector2(800,400));world.ContinueMapDrag(new Vector2(980,440));world.EndMapDrag(new Vector2(980,440),true);
            if(world.worldCamera.transform.position==cameraBefore||game.Walking)throw new InvalidOperationException("Dragging while paused did not pan, or incorrectly moved the player.");
            world.worldCamera.orthographicSize=16;cameraBefore=world.worldCamera.transform.position;
            world.BeginMapDrag(new Vector2(800,400));world.ContinueMapDrag(new Vector2(860,400));world.EndMapDrag(new Vector2(860,400),true);
            if(world.worldCamera.transform.position==cameraBefore)throw new InvalidOperationException("Overview camera feels locked while dragging.");
            world.FocusPoint(16,14,6);
            game.Model.SetPaused(false);Capture("-dragged");
            hud.ShowGroundItem(ItemId.Food);Refresh();CheckCard("Knowledge Card");Capture("-item-detail");Find("KnowledgeNext").onClick.Invoke();Refresh();
            if (!string.IsNullOrEmpty(Argument("-prototypeSaveDirectory")))
            {
                game.SaveGame(); if (!game.LastActionSucceeded) throw new InvalidOperationException(game.Notice);
                game.SkipTutorial(); game.LoadLatest(); Refresh();
                if (!game.LastActionSucceeded || game.Model.Tutorial.step != 2 || !root.Find("Guide Prompt").gameObject.activeSelf || root.Find("Tutorial Welcome").gameObject.activeSelf)
                    throw new InvalidOperationException("Loading tutorial progress did not restore the current coach.");
            }
            PrototypeStartingScenario.Collect(game.Model,ItemId.WasteWood,1);
            PrototypeStartingScenario.MoveTo(game.Model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
            PrototypeStartingScenario.Act(game.Model,()=>game.Model.Withdraw(ItemId.WasteWood,1));
            PrototypeStartingScenario.MoveTo(game.Model,PrototypeSimulation.DumpX,PrototypeSimulation.DumpY);
            hud.ShowHole();Refresh();CheckCard("Knowledge Card");Capture("-hole");
            int carriedWood=game.Model.Carry.Count(ItemId.WasteWood);
            Find("HoleDiscard").onClick.Invoke();Refresh();
            if(game.Model.Carry.Count(ItemId.WasteWood)!=carriedWood||game.Model.Interacting)throw new InvalidOperationException("Dumping bypassed confirmation.");
            Find("HoleDiscard").onClick.Invoke();game.Model.Advance(0.55);
            if(game.Model.Carry.Count(ItemId.WasteWood)!=carriedWood-1||game.Model.DumpedTicks!=ItemCatalog.CapacityTicks(ItemId.WasteWood))throw new InvalidOperationException("Hole controls did not perform the selected disposal.");
            game.StartNewGame(); Refresh();
        }

        private void VerifyHudBindings(string screenshot)
        {
            var root = game.GetComponent<PrototypeHud>().VisualRoot;
            if (game.transform.Find("World") == null || root == null || FindObjectsOfType<Canvas>().Length != 1)
                throw new InvalidOperationException("Runtime duplicated or lost the saved presentation.");
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Button Find(string name) => Array.Find(buttons, button => button.name == name);
            var hud = game.GetComponent<PrototypeHud>();
            if (hud.ManagementOpen || hud.SelectedFacility != 0)
                throw new InvalidOperationException("Management or facility panel is open by default.");
            Find("Tab0").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            if (root.Find("Management/现场 Page").GetComponent<ScrollRect>().verticalNormalizedPosition < 0.99f)
                throw new InvalidOperationException("The first visit to the onsite page does not start at the top.");
            Find("Menu").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!game.MenuOpen||!root.Find("Demo Menu").gameObject.activeSelf)throw new InvalidOperationException("Demo menu is inaccessible.");
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-menu.png"));
            Find("Resume").onClick.Invoke();if(game.MenuOpen)throw new InvalidOperationException("Resume button is unbound.");
            game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            Find("Tab3").onClick.Invoke();
            if(!root.Find("Management/教程 Page").gameObject.activeSelf)throw new InvalidOperationException("Tutorial page missing.");
            Find("TutorialAction").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!game.Model.Tutorial.started || game.Model.Tutorial.step!=1)throw new InvalidOperationException("Tutorial start button is unbound.");
            Find("TutorialPrevious").onClick.Invoke();Find("TutorialNext").onClick.Invoke();
            if(game.Model.Tutorial.step!=1 || game.Model.CashCents!=0 || game.Model.Carry.UsedTicks!=0)throw new InvalidOperationException("Reviewing lessons changes world state.");
            Find("TutorialStart").onClick.Invoke();Find("TutorialSkip").onClick.Invoke();
            if(game.Model.Tutorial.active)throw new InvalidOperationException("Tutorial skip button is unbound.");
            Find("TutorialStart").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            Find("TutorialLocate").onClick.Invoke();game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!hud.InventoryOpen||!Find("InventoryPrimary").gameObject.activeInHierarchy)throw new InvalidOperationException("Tutorial does not reveal the suggested inventory control.");
            Find("Tab3").onClick.Invoke();game.GetComponent<PrototypeWorld>().RefreshView(game.Model);game.GetComponent<PrototypeHud>().RefreshView(game.Model);
            if(!string.IsNullOrEmpty(screenshot))CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-tutorial.png"));
            Find("FoodPolicy").onClick.Invoke();if(game.Model.FoodPolicy!=SupplyPolicy.Free)throw new InvalidOperationException("Supply policy button is unbound.");Find("FoodPolicy").onClick.Invoke();Find("FoodPolicy").onClick.Invoke();
            Find("ExternalSupply").onClick.Invoke();if(game.Model.ExternalSupply)throw new InvalidOperationException("External supply button is unbound.");Find("ExternalSupply").onClick.Invoke();
            long beforeTrade=game.Model.CashCents;Find("Command22").onClick.Invoke();if(game.Model.CashCents!=beforeTrade||!root.Find("Trade Confirmation").gameObject.activeSelf)throw new InvalidOperationException("Technology trade confirmation is missing.");Find("CancelTrade").onClick.Invoke();
            Find("Pause").onClick.Invoke();
            if (!game.Model.Paused) throw new InvalidOperationException("Saved Pause button is unbound.");
            Find("Pause").onClick.Invoke();
            Find("Speed3").onClick.Invoke();
            if (game.Model.Speed != 3) throw new InvalidOperationException("Saved speed buttons are unbound.");
            Find("Speed1").onClick.Invoke();
            for (int i = 0; i < 4; i++)
            {
                Find("Tab" + i).onClick.Invoke();
                string[] names = { "现场 Page", "人员 Page", "仓库 Page", "教程 Page" };
                for (int j = 0; j < names.Length; j++)
                    if (root.Find("Management/" + names[j]).gameObject.activeSelf != (i == j))
                        throw new InvalidOperationException("Management tab navigation failed.");
                if (i > 0 && i<3 && !string.IsNullOrEmpty(screenshot))
                    CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + (i == 1 ? "-people.png" : "-inventory.png")));
            }
            Find("Tab2").onClick.Invoke();Find("InventoryFilter").onClick.Invoke();
            if (!Find("Pick0").gameObject.activeInHierarchy)
                throw new InvalidOperationException("Full inventory catalogue is inaccessible.");
            Find("InventoryFilter").onClick.Invoke();
            Find("Tab0").onClick.Invoke();
        }

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        // Exercise long, populated pages and scrollbar navigation at the actual player resolution.
        private void VerifyManagementLayout(string screenshot)
        {
            var hud = game.GetComponent<PrototypeHud>();
            var root = hud.VisualRoot;
            var model = game.Model;
            hud.RefreshView(model);
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Button Find(string name) => Array.Find(buttons, button => button.name == name);
            void Capture(string suffix) => CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + suffix + ".png"));
            Canvas.ForceUpdateCanvases();
            Rect map = game.GetComponent<PrototypeWorld>().worldCamera.pixelRect;
            if (Mathf.Abs(map.width - Screen.width) > 1 || Mathf.Abs(map.height - Screen.height) > 1)
                throw new InvalidOperationException("The world does not occupy the full screen.");
            if (root.Find("Header/Food").GetComponent<Text>().text != model.Warehouse.Count(ItemId.Food).ToString())
                throw new InvalidOperationException("Resource bar is not showing live warehouse quantities.");

            Find("Tab0").onClick.Invoke();
            Find("OnsiteJump3").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            var onsite = root.Find("Management/现场 Page").GetComponent<ScrollRect>();
            if (onsite.verticalNormalizedPosition > 0.5f) throw new InvalidOperationException("Trading shortcut did not scroll to its section.");
            Capture("-trading");
            Find("OnsiteJump0").onClick.Invoke();

            Find("Tab1").onClick.Invoke();
            var people = root.Find("Management/人员 Page").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            people.verticalScrollbar.value = 0;
            Canvas.ForceUpdateCanvases();
            if (people.verticalNormalizedPosition > 0.01f) throw new InvalidOperationException("Resident scrollbar cannot reach the end of the roster.");
            int portraits = people.content.GetComponentsInChildren<Image>().Count(image => image.name == "Portrait");
            if (portraits != model.Residents.Count) throw new InvalidOperationException("Resident cards do not match the recruited population.");
            Capture("-residents");

            Find("Tab2").onClick.Invoke();
            var inventory = root.Find("Management/仓库 Page").GetComponent<ScrollRect>();
            inventory.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            Capture("-stock");
            Find("InventoryFilter").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            inventory.verticalScrollbar.value = 0;
            Canvas.ForceUpdateCanvases();
            var finalItem = Find("Sell" + (Enum.GetValues(typeof(ItemId)).Length - 1));
            Vector3 point = inventory.viewport.InverseTransformPoint(finalItem.transform.position);
            if (!finalItem.gameObject.activeInHierarchy || !inventory.viewport.rect.Contains(point))
                throw new InvalidOperationException("The final inventory row is unreachable by scrollbar.");
            Capture("-catalogue");
            Find("InventoryFilter").onClick.Invoke();
            inventory.verticalNormalizedPosition = 1;
            Find("Tab0").onClick.Invoke();
            Find("OnsiteJump0").onClick.Invoke();
            Capture("-building");
            VerifyOverlayInteraction(screenshot);
            hud.ClosePanels();
            root.Find("Action Notice").gameObject.SetActive(false);
        }

        private void VerifyOverlayInteraction(string screenshot)
        {
            var hud = game.GetComponent<PrototypeHud>();
            var world = game.GetComponent<PrototypeWorld>();
            var root = hud.VisualRoot;
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            Button Find(string name) => Array.Find(buttons, button => button.name == name);
            var camera = world.worldCamera;
            Vector3 position = camera.transform.position;
            float zoom = camera.orthographicSize;
            Rect viewport = camera.rect;
            hud.ClosePanels();
            Find("OpenTab2").onClick.Invoke();
            if (!hud.InventoryOpen) throw new InvalidOperationException("Inventory launcher did not open the grid inventory.");
            Find("OpenTab2").onClick.Invoke();
            if (hud.InventoryOpen) throw new InvalidOperationException("Clicking the same launcher did not close inventory.");
            for (int facility = 1; facility <= 4; facility++)
            {
                hud.ShowFacility(facility);
                if (hud.SelectedFacility != facility || hud.ManagementOpen)
                    throw new InvalidOperationException("Facility inspection did not replace management.");
                var group = root.Find("Facility Panel/" + (facility == 1 ? "Woodwork" : "Facility " + facility));
                if (!group.gameObject.activeInHierarchy) throw new InvalidOperationException("Facility controls are not visible.");
                if (facility == 1) CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + "-woodwork.png"));
            }
            game.HandleEscape();
            if (hud.SelectedFacility != 0 || game.MenuOpen) throw new InvalidOperationException("Escape did not close the inspection first.");
            if (camera.rect != viewport || camera.orthographicSize != zoom || camera.transform.position != position)
                throw new InvalidOperationException("Opening a panel moved or resized the camera.");
            Vector2 building = world.VisualRoot.Find("WoodworkBench").GetComponent<SpriteRenderer>().bounds.center;
            world.HandleMapClick(building, false);
            if (hud.SelectedFacility != 1) throw new InvalidOperationException("Clicking a building did not select its controls.");
            world.HandleMapClick(building, true);
            if (!game.Walking || game.WalkTargetX != PrototypeSimulation.BenchX || game.WalkTargetY != PrototypeSimulation.BenchY)
                throw new InvalidOperationException("Right-click did not navigate to the building's reachable front.");
            game.HandleEscape(); // Close inspection first, then cancel the movement order.
            game.HandleEscape();
            if (game.Walking || game.MenuOpen) throw new InvalidOperationException("Escape did not cancel the pending walk after closing inspection.");
            game.HandleEscape();
            if (!game.MenuOpen) throw new InvalidOperationException("Escape without a panel or action did not open the menu.");
            game.HandleEscape();
            if (game.MenuOpen) throw new InvalidOperationException("Escape did not return from the menu.");
            hud.ShowFacility(3);
            ItemId recipe = game.Model.AssemblyProduct;
            Find("FacilityCommand30").onClick.Invoke();
            if (game.Model.AssemblyProduct == recipe) throw new InvalidOperationException("Contextual assembly recipe control is unbound.");
            Find("FacilityCommand29").onClick.Invoke();
            hud.ClosePanels();
            Find("Zones").onClick.Invoke(); world.RefreshView(game.Model);
            if (!world.VisualRoot.Find("Construction Boundary South").GetComponent<SpriteRenderer>().enabled)
                throw new InvalidOperationException("Area overlay cannot be enabled.");
            Find("Zones").onClick.Invoke(); world.RefreshView(game.Model);
            if (world.VisualRoot.Find("Construction Boundary South").GetComponent<SpriteRenderer>().enabled)
                throw new InvalidOperationException("Area overlay cannot be hidden.");
        }

        private void VerifyInventoryPointerBlocking()
        {
            var root=game.GetComponent<PrototypeHud>().VisualRoot;
            var overlay=root.Find("Inventory Window");var panel=overlay.Find("Card").GetComponent<RectTransform>();
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=panel.TransformPoint(panel.rect.center)};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
            if(!hits.Any(h=>h.gameObject.transform==overlay||h.gameObject.transform.IsChildOf(overlay)))throw new InvalidOperationException("Rendered inventory does not intercept map input.");
            var slot=overlay.GetComponentsInChildren<PrototypeInventorySlot>().First(s=>s.container==0&&s.index==0);
            var rect=slot.GetComponent<RectTransform>();data.position=rect.TransformPoint(rect.rect.center);hits.Clear();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<PrototypeInventorySlot>()!=slot)throw new InvalidOperationException("A visible inventory slot does not receive pointer input.");
        }

        private void VerifyWorldPresentation(string screenshot)
        {
            var world = game.GetComponent<PrototypeWorld>();
            var hud = game.GetComponent<PrototypeHud>();
            var model = game.Model;
            hud.ClosePanels();
            var camera = world.worldCamera;
            float zoom = camera.orthographicSize;
            Vector3 position = camera.transform.position;
            long units = model.Ground.Sum(p => (long)p.Amount);
            int piles = model.Ground.Count;
            int cellCount = model.Ground.Select(p => (p.X, p.Y)).Distinct().Count();
            camera.orthographicSize = 16;
            world.RefreshView(model);
            var items = world.VisualRoot.Find("Ground Items");
            if (items.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Item" && r.enabled) != cellCount)
                throw new InvalidOperationException("Dense ground cells are not represented by one icon per cell.");
            foreach(var quantity in items.GetComponentsInChildren<TextMesh>().Where(t=>t.name=="Quantity"))
                if(string.IsNullOrEmpty(quantity.text))throw new InvalidOperationException("Nearby pile count is empty.");
            if (world.VisualRoot.Find("PlayerLabel").gameObject.activeSelf || world.VisualRoot.Find("Tutorial Guide Target/Tutorial Target Label") != null)
                throw new InvalidOperationException("Player or tutorial text overlaps ground items.");
            var crowded = model.Ground.GroupBy(p => (p.X, p.Y)).OrderByDescending(g => g.Count()).First();
            world.FocusPoint(crowded.Key.X, crowded.Key.Y, 4);
            world.RefreshView(model);
            Vector3 targetPoint = camera.WorldToViewportPoint(new Vector3(crowded.Key.X, crowded.Key.Y, 0));
            if (!new Rect(0, 0, 1, 1).Contains(targetPoint)) throw new InvalidOperationException("Focused inspection target is outside the map viewport.");
            if (!world.VisualRoot.Find("ReceivingLabel").gameObject.activeSelf||!world.VisualRoot.Find("WarehouseLabel").gameObject.activeSelf)
                throw new InvalidOperationException("Opening landmarks have no names.");
            var inspectPoint = new Vector2(crowded.Key.X, crowded.Key.Y);
            world.UpdateHover(null, 10);
            world.UpdateHover(inspectPoint, 10);
            world.UpdateHover(inspectPoint, 10.39);
            var premature = hud.VisualRoot.Find("Map Inspection");
            if (premature != null && premature.gameObject.activeSelf)
                throw new InvalidOperationException("Hover tooltip appears before the dwell delay.");
            world.UpdateHover(inspectPoint, 10.41);
            Transform inspection = hud.VisualRoot.Find("Map Inspection");
            string details = inspection.Find("Contents").GetComponent<Text>().text;
            foreach (var item in crowded.Select(p => p.Item).Distinct().OrderBy(i => i).Take(3))
                if (!details.Contains(ItemCatalog.Name(item))) throw new InvalidOperationException("Inspection lost a mixed-cell item.");
            if (inspection.GetComponentsInChildren<Graphic>().Any(g => g.raycastTarget))
                throw new InvalidOperationException("Map inspection blocks movement clicks.");
            Canvas.ForceUpdateCanvases();
            var tooltipCorners = new Vector3[4]; inspection.GetComponent<RectTransform>().GetWorldCorners(tooltipCorners);
            Rect viewport = camera.pixelRect;
            if (tooltipCorners.Any(p => p.x < viewport.xMin || p.x > viewport.xMax || p.y < viewport.yMin || p.y > viewport.yMax))
                throw new InvalidOperationException("Map inspection extends outside the map viewport.");
            if (!string.IsNullOrEmpty(screenshot)) CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot), Path.GetFileNameWithoutExtension(screenshot) + "-inspection.png"));
            world.UpdateHover(null, 11);
            if (inspection.gameObject.activeSelf) throw new InvalidOperationException("Map inspection remains after pointer exit.");
            camera.orthographicSize = zoom; camera.transform.position = position; world.RefreshView(model);
            if (piles != model.Ground.Count || units != model.Ground.Sum(p => (long)p.Amount))
                throw new InvalidOperationException("Presentation changed the simulated resources.");
        }

        private static void CapturePreview(string path)
        {
            Camera main = Camera.main;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (main == null || canvas == null) throw new InvalidOperationException("Preview camera or canvas missing.");
            int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            Rect viewport = main.rect;
            var worldTarget = new RenderTexture(Mathf.RoundToInt(width * viewport.width),
                Mathf.RoundToInt(height * viewport.height), 24, RenderTextureFormat.ARGB32);
            worldTarget.Create();
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = main.targetTexture;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCanvasCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraObject = new GameObject("OffscreenPreviewCamera", typeof(Camera));
            var uiCamera = cameraObject.GetComponent<Camera>();
            uiCamera.enabled = false;
            uiCamera.transform.position = new Vector3(0, 0, -100);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = height / 2f;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 200;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.cullingMask = 1 << 5;
            uiCamera.targetTexture = target;
            Texture2D pixels = null;
            try
            {
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();
                main.targetTexture = worldTarget;
                main.rect = new Rect(0, 0, 1, 1);
                main.Render();
                RenderTexture.active = target;
                GL.Clear(true, true, new Color32(18, 28, 31, 255));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);
                Graphics.DrawTexture(new Rect(viewport.x * width, (1 - viewport.yMax) * height,
                    viewport.width * width, viewport.height * height), worldTarget);
                GL.PopMatrix();
                uiCamera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color32[] samples = pixels.GetPixels32();
                int visible = 0;
                foreach (Color32 pixel in samples) if (pixel.r + pixel.g + pixel.b > 120) visible++;
                if (visible < samples.Length / 100) throw new InvalidOperationException("Offscreen preview appears blank.");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                main.targetTexture = previousTarget;
                main.rect = viewport;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCanvasCamera;
                canvas.planeDistance = previousPlane;
                RenderTexture.active = previousActive;
                target.Release();
                worldTarget.Release();
                Destroy(target); Destroy(worldTarget); Destroy(cameraObject);
                if (pixels != null) Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(ActionResult result, string action)
        { if (!result.Success) throw new InvalidOperationException(action + ": " + result.Message); }

        private static void MoveTo(PrototypeSimulation model, double x, double y)
        {
            for (int step = 0; step < 400; step++)
            {
                double dx = x - model.PlayerX, dy = y - model.PlayerY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 0.001) return;
                double delta = Math.Min(0.05, distance / (model.Settings.moveSpeed * model.Speed));
                model.MovePlayer(dx / distance, dy / distance, delta);
                model.Advance(delta);
            }
            throw new InvalidOperationException("Smoke test could not reach the interaction point.");
        }
    }
}
