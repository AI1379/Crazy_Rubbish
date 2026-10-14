using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeWorld : MonoBehaviour
    {
        public PrototypeGame game;
        public Camera worldCamera;
        private readonly Dictionary<int, PileView> piles = new Dictionary<int, PileView>();
        [SerializeField] private List<SpriteRenderer> floor = new List<SpriteRenderer>();
        [SerializeField] private Transform visualRoot, pileRoot, worker;
        [SerializeField] private SpriteRenderer bench;
        [SerializeField] private TextMesh benchLabel, playerLabel, generatorLabel, tableLabel, dismantleLabel;
        [SerializeField] private SpriteRenderer generator, table, dismantle;
        public bool HasLayout => visualRoot != null && worker != null && floor.Count > 0;
        public Transform VisualRoot => visualRoot;
        private readonly Dictionary<int, Transform> residentViews = new Dictionary<int, Transform>();
        private PrototypeSimulation previousModel;
        private int lastRevision = -1;
        private int screenWidth, screenHeight;
        private Transform tutorialTarget;
        private readonly List<CellView> cells = new List<CellView>();
        [SerializeField] private int layoutVersion;
        public bool HasCurrentLayout => HasLayout && layoutVersion == 4;

        private sealed class PileView
        { public GameObject Root; public SpriteRenderer Icon, Highlight, Badge; public TextMesh Count; }
        private sealed class CellView
        {
            public int X, Y, Types;
            public PileView View;
        }

        private void Start()
        {
            PrototypeSimulation model = game.Model;
            BuildLayout(model);
            ConfigureLabels();
            // The saved scene contains the starting items for editing. Runtime owns their state.
            previousModel = null;
            lastRevision = -1;
            FitCamera(model);
        }

        public void BuildLayout(PrototypeSimulation model)
        {
            if (HasCurrentLayout) { ConfigureLabels(); return; }
            if (HasLayout)
            {
                if (layoutVersion < 3) ApplyDeliveryToExistingLayout();
                FitCamera(model);
                ConfigureLabels();
                return;
            }
            if (visualRoot != null) RemoveView(visualRoot.gameObject);
            piles.Clear(); cells.Clear(); residentViews.Clear(); previousModel = null; lastRevision = -1;
            layoutVersion = 3;
            visualRoot = new GameObject("World").transform;
            visualRoot.SetParent(transform, false);
            pileRoot = new GameObject("Ground Items").transform;
            pileRoot.SetParent(visualRoot, false);
            floor.Clear();
            worldCamera.backgroundColor = new Color32(30, 29, 27, 255);
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.orthographic = true;
            FitCamera(model);
            worldCamera.transform.position = new Vector3((model.Settings.mapWidth - 1) / 2f,
                (model.Settings.mapHeight - 1) / 2f, -10);
            for (int x = 0; x < model.Settings.mapWidth; x++)
                for (int y = 0; y < model.Settings.mapHeight; y++)
                {
                    var tile = Sprite($"Ground {x},{y}", game.appearance.tile, x, y, 1, -2000);
                    tile.color = GroundColor(x, y);
                    floor.Add(tile);
                }
            ZoneOutline("Construction Boundary", 9.5f, 5.5f, 18, 18, new Color32(130, 170, 141, 255));
            ZoneOutline("Receiving Boundary", -0.5f, 5.5f, 8, 6, new Color32(223, 164, 94, 255));
            StationPad("Woodwork Pad", PrototypeSimulation.BenchX, PrototypeSimulation.BenchY, new Color32(171, 133, 78, 255));
            StationPad("Dismantle Pad", PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY, new Color32(87, 138, 153, 255));
            StationPad("Workbench Pad", PrototypeSimulation.TableX, PrototypeSimulation.TableY, new Color32(135, 160, 113, 255));
            StationPad("Generator Pad", PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY, new Color32(192, 150, 76, 255));
            Sprite("Warehouse", game.appearance.warehouse, PrototypeSimulation.WarehouseX, 1.65f, 0.94f, SortAt(4.5f));
            Line("Warehouse Interaction Front", PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY, 0.9f, 0.12f, new Color32(230, 179, 102, 190));
            bench = Sprite("WoodworkBench", game.appearance.woodwork,
                PrototypeSimulation.BenchX + 0.5f, PrototypeSimulation.BenchY + 0.5f, 0.92f, SortAt(PrototypeSimulation.BenchY + 0.5f));
            benchLabel = StationLabel("BenchLabel", "木工台", PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            generator = Sprite("Generator", game.appearance.generator, PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 0.5f, 0.92f, SortAt(PrototypeSimulation.GeneratorY + 0.5f));
            generatorLabel = StationLabel("GeneratorLabel", "发电机", PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY);
            table = Sprite("Workbench", game.appearance.workbench, PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 0.5f, 0.92f, SortAt(PrototypeSimulation.TableY + 0.5f));
            tableLabel = StationLabel("TableLabel", "工作台", PrototypeSimulation.TableX, PrototypeSimulation.TableY);
            dismantle = Sprite("Dismantle", game.appearance.dismantle, PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 0.5f, 0.92f, SortAt(PrototypeSimulation.DismantleY + 0.5f));
            dismantleLabel = StationLabel("DismantleLabel", "拆解台", PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY);
            Sprite("OldHousing", game.appearance.housing, PrototypeSimulation.HousingX, 1.65f, 0.94f, SortAt(4.5f));
            Label("HousingLabel", "旧住房 · 6人", PrototypeSimulation.HousingX, 1.5f, 0.13f);
            Label("OwnedLandLabel", "建设区 / 18×18", 20, 23.8f, 0.18f, new Color32(166, 199, 169, 255));
            Sprite("Hole", game.appearance.hole, 2.5f, 20.5f, 1, -1500);
            Label("HoleLabel", "洞 · 去向不明", 2.5f, 19.2f, 0.12f);
            Label("WarehouseLabel", "仓库", PrototypeSimulation.WarehouseX, 5.8f, 0.13f);
            Label("DropLabel", "来料区 / 8×6", 3.5f, 12.3f, 0.15f, new Color32(232, 185, 121, 255));

            Sprite("CommunityLamp", game.appearance.streetlamp, 20, 4.6f, 1, SortAt(5));
            Label("LampLabel", "社区路灯", 20, 6, 0.11f);
            Label("MeetingPoint", "会面点", 16, -0.5f, 0.11f);
            worker = Actor("Protagonist", 0).transform;
            playerLabel = Label("PlayerLabel", "主角", (float)model.PlayerX, (float)model.PlayerY - 0.8f, 0.10f, new Color32(255, 206, 123, 255));
            ConfigureLabels();
            RefreshView(model);
        }

        private void LateUpdate()
        {
            var model = game.Model;
            if (!HasLayout || model == null) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) FitCamera(model);
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            Vector2 mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height));
            bool available = worldCamera.rect.Contains(mouse) && !overUi && !game.MenuOpen && !game.ResultVisible;
            float scroll = Input.mouseScrollDelta.y;
            if (available && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                HandleMapClick(worldCamera.ScreenToWorldPoint(Input.mousePosition), Input.GetMouseButtonDown(1));
            if (available && scroll != 0)
            {
                Vector3 before = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize * Mathf.Pow(0.85f, scroll), 4, FullMapSize(model));
                Vector3 after = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                worldCamera.transform.position += before - after;
                ClampCamera(model);
            }
            if (available && Input.GetMouseButton(2))
            {
                float units = worldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height * worldCamera.rect.height);
                worldCamera.transform.position -= new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0) * units * 12;
                ClampCamera(model);
            }
            RefreshView(model);
            UpdateHover(available && scroll == 0 && !Input.GetMouseButton(2) ? (Vector2?)worldCamera.ScreenToWorldPoint(Input.mousePosition) : null, Time.unscaledTimeAsDouble);
        }

        public void RefreshView(PrototypeSimulation model)
        {
            if (previousModel != model)
            {
                for (int i = pileRoot.childCount - 1; i >= 0; i--) RemoveView(pileRoot.GetChild(i).gameObject);
                piles.Clear();
                cells.Clear();
                foreach (Transform view in residentViews.Values) RemoveView(view.gameObject);
                residentViews.Clear();
                previousModel = model;
                lastRevision = -1;
                worker.GetComponent<PrototypeActorView>().ResetMotion();
            }
            if (lastRevision != model.GroundRevision)
            {
                ReconcilePiles(model);
                lastRevision = model.GroundRevision;
                int index = 0;
                for (int x = 0; x < model.Settings.mapWidth; x++)
                    for (int y = 0; y < model.Settings.mapHeight; y++)
                    {
                        float weight = Mathf.Clamp01(1 - (float)PrototypeSimulation.Distance(x, y,
                            PrototypeSimulation.BenchX, PrototypeSimulation.BenchY) / model.Settings.pressureRadius);
                        Color baseColor = GroundColor(x, y);
                        floor[index++].color = Color.Lerp(baseColor, new Color32(188, 128, 91, 255),
                            weight * (1 - model.HumanEfficiency) * 1.05f);
                    }
            }
            GroundPile nearest = model.NearestPickup();
            foreach (CellView cell in cells)
            {
                cell.View.Highlight.enabled = nearest != null && nearest.X == cell.X && nearest.Y == cell.Y;
                cell.View.Count.gameObject.SetActive(false);
                cell.View.Badge.enabled = false;
            }
            RefreshMapOverlays();
            int playerPose = model.PlayerSawing ? 3 : model.PickingUp ? 2 : model.Carry.UsedTicks > 0 ? 1 : 0;
            worker.GetComponent<PrototypeActorView>().Refresh(new Vector2((float)model.PlayerX, (float)model.PlayerY), playerPose, model.Paused, model.GameSeconds);
            playerLabel.transform.position = worker.position + new Vector3(0, -0.75f, 0);
            bench.color = model.BenchBuilt ? Color.white : new Color32(195, 180, 155, 130);
            benchLabel.text = "木工台";
            benchLabel.color = model.BenchBuilt ? Color.white : new Color32(212, 187, 137, 255);
            generator.color = model.GeneratorBuilt ? Color.white : new Color32(195, 180, 155, 130);
            table.color = model.TableBuilt ? Color.white : new Color32(195, 180, 155, 130);
            dismantle.color = model.DismantleBuilt ? Color.white : new Color32(195, 180, 155, 130);
            generatorLabel.text = "发电机";
            tableLabel.text = "工作台";
            dismantleLabel.text = "拆解台";
            foreach (TextMesh label in new[] { benchLabel, generatorLabel, tableLabel, dismantleLabel })
                label.gameObject.SetActive(false);
            foreach(int id in residentViews.Keys.Where(id=>!model.Residents.Any(r=>r.Id==id)).ToArray())
            {RemoveView(residentViews[id].gameObject);residentViews.Remove(id);}
            foreach (Resident resident in model.Residents)
            {
                if (!residentViews.TryGetValue(resident.Id, out Transform view))
                {
                    view = Actor("Resident_" + resident.Id, 2 + (resident.Id - 1) % 4).transform;
                    residentViews.Add(resident.Id, view);
                }
                bool sawing = resident.Job == WorkerJob.Woodwork && model.WorkerAtBench && model.State == WorkState.Working && model.SelectedRecipe != WoodRecipe.SortedWood;
                int pose = sawing ? 3 : resident.PickingUp ? 2 : resident.Cargo.UsedTicks > 0 ? 1 : 0;
                view.GetComponent<PrototypeActorView>().Refresh(new Vector2((float)resident.X, (float)resident.Y), pose, model.Paused, model.GameSeconds);
            }
            if(Application.isPlaying)RefreshTutorialTarget(model);
        }

        public void FocusPoint(int x,int y,float zoom = 8)
        {
            if(game.Model==null)return;
            worldCamera.orthographicSize=Mathf.Clamp(zoom,4,FullMapSize(game.Model));
            worldCamera.transform.position=new Vector3(x,y,-10);ClampCamera(game.Model);
        }
        private void RefreshTutorialTarget(PrototypeSimulation model)
        {
            if(tutorialTarget==null)
            {
                tutorialTarget=new GameObject("Tutorial Guide Target").transform;tutorialTarget.SetParent(visualRoot,false);
                var marker=game.appearance.marker!=null?game.appearance.marker:game.appearance.tile;
                foreach(var edge in new[]{new Vector4(-0.49f,0,0.045f,1),new Vector4(0.49f,0,0.045f,1),new Vector4(0,-0.49f,1,0.045f),new Vector4(0,0.49f,1,0.045f)})
                {
                    var part=Sprite("Tutorial Target Edge",marker,0,0,1,2200);part.transform.SetParent(tutorialTarget,false);part.transform.localPosition=new Vector3(edge.x,edge.y,0);
                    part.transform.localScale=new Vector3(edge.z/marker.bounds.size.x,edge.w/marker.bounds.size.y,1);part.color=new Color32(255,196,98,255);
                }
            }
            var action=PrototypeTutorial.NextAction(model,model.Tutorial.step);
            tutorialTarget.gameObject.SetActive(model.Tutorial.active && model.Tutorial.started && !model.Tutorial.Completed && action.hasTarget && !game.MenuOpen && !game.ResultVisible);
            if(tutorialTarget.gameObject.activeSelf)tutorialTarget.position=new Vector3(action.x,action.y,0);
        }

        private void FitCamera(PrototypeSimulation model)
        {
            screenWidth = Screen.width; screenHeight = Screen.height;
            float canvasHeight = PrototypeHud.ReferenceWidth * Mathf.Max(1, Screen.height) / Mathf.Max(1, Screen.width);
            worldCamera.rect = new Rect(PrototypeHud.MapLeft / PrototypeHud.ReferenceWidth, PrototypeHud.MapBottom / canvasHeight,
                PrototypeHud.MapWidth / PrototypeHud.ReferenceWidth,
                Mathf.Max(1, canvasHeight - PrototypeHud.MapTop - PrototypeHud.MapBottom) / canvasHeight);
            worldCamera.orthographicSize = Mathf.Min(10, FullMapSize(model));
            worldCamera.transform.position = new Vector3((model.Settings.mapWidth - 1) / 2f, Mathf.Min(9.5f, (model.Settings.mapHeight - 1) / 2f), -10);
            ClampCamera(model);
        }

        private float FullMapSize(PrototypeSimulation model)
        {
            float aspect = Mathf.Max(0.1f, Screen.width * worldCamera.rect.width / Mathf.Max(1, Screen.height * worldCamera.rect.height));
            return Mathf.Max(model.Settings.mapHeight / 2f + 1.4f, model.Settings.mapWidth / (2f * aspect) + 1.4f);
        }

        private void ClampCamera(PrototypeSimulation model)
        {
            Vector3 p = worldCamera.transform.position;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.pixelWidth / Mathf.Max(1f, worldCamera.pixelHeight);
            p.x = halfWidth * 2 >= model.Settings.mapWidth ? (model.Settings.mapWidth - 1) / 2f
                : Mathf.Clamp(p.x, halfWidth - 0.5f, model.Settings.mapWidth - halfWidth - 0.5f);
            p.y = halfHeight * 2 >= model.Settings.mapHeight ? (model.Settings.mapHeight - 1) / 2f
                : Mathf.Clamp(p.y, halfHeight - 0.5f, model.Settings.mapHeight - halfHeight - 0.5f);
            worldCamera.transform.position = p;
        }

        private static Color GroundColor(int x, int y)
        {
            if (x <= 7 && y >= 6 && y <= 11) return new Color32(238, 221, 191, 255);
            if (x >= 10 && x <= 27 && y >= 6 && y <= 23) return new Color32(238, 237, 218, 255);
            if (y >= 3 && y <= 5 && x >= 8 && x <= 23) return new Color32(250, 241, 224, 255);
            return new Color32(205, 209, 205, 255);
        }

        private void ZoneOutline(string name, float x, float y, float width, float height, Color color)
        {
            Line(name + " South", x + width / 2, y, width, 0.12f, color);
            Line(name + " North", x + width / 2, y + height, width, 0.12f, color);
            Line(name + " West", x, y + height / 2, 0.12f, height, color);
            Line(name + " East", x + width, y + height / 2, 0.12f, height, color);
        }

        private void Line(string name, float x, float y, float width, float height, Color color)
        {
            var marker = game.appearance.marker != null ? game.appearance.marker : game.appearance.tile;
            var line = Sprite(name, marker, x, y, 1, -1400);
            line.transform.localScale = new Vector3(width / marker.bounds.size.x, height / marker.bounds.size.y, 1);
            line.color = color;
        }

        private void StationPad(string name, int x, int y, Color color)
        {
            ZoneOutline(name, x - 0.5f, y - 0.5f, 2, 3, color);
            Line(name + " Front", x, y - 0.3f, 0.65f, 0.13f, color);
        }

        private TextMesh StationLabel(string name, string caption, int x, int y)
        {
            TextMesh label = Label(name, caption, x + 0.5f, y + 2.15f, 0.095f);
            var plate = Sprite(name + " Plate", game.appearance.marker, x + 0.5f, y + 2.15f, 1, 1998);
            SetSpriteSize(plate, 1.9f, 0.48f);
            plate.color = new Color32(24, 38, 40, 240);
            plate.transform.SetParent(label.transform, true);
            return label;
        }

        private void ConfigureLabels()
        {
            // Preserve saved object IDs while retiring all world-space typography and name plates.
            foreach (var label in visualRoot.GetComponentsInChildren<TextMesh>(true)) label.gameObject.SetActive(false);
            overlayLines = visualRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                r.name.Contains("Boundary ") || r.name.Contains(" Pad ") || r.name == "Warehouse Interaction Front").ToArray();
            foreach (var line in overlayLines) line.enabled = false;
            layoutVersion = 4;
        }

        private static void SetSpriteSize(SpriteRenderer renderer, float width, float height)
        {
            renderer.transform.localScale = new Vector3(width / renderer.sprite.bounds.size.x, height / renderer.sprite.bounds.size.y, 1);
        }

        private static void RemoveView(GameObject obj)
        {
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        private static int PileKey(GroundPile pile, PrototypeSimulation model) => pile.Id;

        private void ReconcilePiles(PrototypeSimulation model)
        {
            var alive = new HashSet<int>();
            foreach (GroundPile pile in model.Ground)
            {
                int key = PileKey(pile, model);
                alive.Add(key);
                if (!piles.TryGetValue(key, out PileView view))
                {
                    var root = new GameObject("GroundPile_" + key);
                    root.transform.SetParent(pileRoot, false);
                    root.transform.position = new Vector3(pile.X, pile.Y, 0);
                    view = new PileView { Root = root };
                    view.Highlight = Sprite("PickupHighlight", game.appearance.marker, pile.X, pile.Y, 1, -1300);
                    SetSpriteSize(view.Highlight, 0.92f, 0.92f);
                    view.Highlight.color = new Color32(230, 177, 86, 55);
                    view.Icon = Sprite("Item", game.appearance.ItemSprite(pile.Item), pile.X, pile.Y, 1, SortAt(pile.Y - 0.3f));
                    view.Count = Label("Quantity", "", pile.X + 0.24f, pile.Y - 0.24f, 0.055f);
                    view.Badge = Sprite("Cell Badge", game.appearance.marker, pile.X + 0.24f, pile.Y - 0.24f, 1, 1999);
                    view.Badge.color = new Color32(29, 45, 48, 255);
                    view.Highlight.transform.SetParent(root.transform, true);
                    view.Icon.transform.SetParent(root.transform, true);
                    view.Count.transform.SetParent(root.transform, true);
                    view.Badge.transform.SetParent(root.transform, true);
                    piles.Add(key, view);
                }
                view.Icon.sprite = game.appearance.ItemSprite(pile.Item);
                view.Icon.enabled = false;
                view.Highlight.enabled = false;
                view.Count.gameObject.SetActive(false);
                view.Badge.enabled = false;
                bool overloaded = model.GroundCapacityAt(pile.X, pile.Y) > model.Settings.pileCapacity;
                view.Count.color = overloaded ? new Color32(242, 166, 92, 255) : new Color32(225, 222, 202, 255);
            }
            var removed = new List<int>();
            foreach (var entry in piles) if (!alive.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (int key in removed) { RemoveView(piles[key].Root); piles.Remove(key); }
            cells.Clear();
            foreach (var group in model.Ground.GroupBy(p => (p.X, p.Y)))
            {
                GroundPile[] content = group.OrderBy(p => p.Id).ToArray();
                // Tools remain recognizable even when mixed into a larger heap.
                GroundPile primary = content.OrderByDescending(p => ItemCatalog.IsTool(p.Item)).ThenBy(p => p.Id).First();
                PileView view = piles[primary.Id];
                int types = content.Select(p => p.Item).Distinct().Count();
                int amount = content.Sum(p => p.Amount);
                bool high = model.GroundCapacityAt(primary.X, primary.Y) > model.Settings.pileCapacity * 0.5m;
                Sprite image = game.appearance.GroundSprite(primary.Item, high);
                view.Icon.sprite = image;
                bool heap = primary.Item == ItemId.WasteWood || primary.Item == ItemId.SortedWood || primary.Item == ItemId.MixedPlastic ||
                    primary.Item == ItemId.PowerAsh || primary.Item == ItemId.SteelMaterial || primary.Item == ItemId.CopperMaterial || primary.Item == ItemId.ToolSteelWaste;
                // Supplied pile textures use ground pivots and one-cell visible widths;
                // atlas icons instead use centered, tightly sliced rectangles.
                float size = heap ? 0.88f : 0.78f / Mathf.Max(image.bounds.size.x, image.bounds.size.y);
                view.Icon.transform.localScale = new Vector3(size, size, 1);
                view.Icon.transform.position = new Vector3(primary.X, primary.Y - (heap ? 0.34f : 0), 0);
                view.Icon.enabled = true;
                view.Icon.color = Color.white;
                view.Count.text = types > 1 ? "+" + (types - 1) : amount.ToString();
                cells.Add(new CellView { X = primary.X, Y = primary.Y, Types = types, View = view });
            }
        }

        private static int SortAt(float y) => 1000 - Mathf.RoundToInt(y * 10);

        private void ApplyDeliveryToExistingLayout()
        {
            // Preserve authored object IDs and UI references while upgrading the saved whitebox.
            worldCamera.backgroundColor = new Color32(30, 29, 27, 255);
            foreach (var tile in floor)
            {
                tile.sprite = game.appearance.tile;
                tile.sortingOrder = -2000;
                tile.transform.localScale = Vector3.one;
                tile.color = GroundColor(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y));
            }
            void Replace(string oldName, string newName, Sprite image, float x, float y, float scale, int order)
            {
                var renderer = visualRoot.Find(oldName).GetComponent<SpriteRenderer>();
                renderer.name = newName; renderer.sprite = image; renderer.color = Color.white;
                renderer.transform.position = new Vector3(x, y, 0);
                renderer.transform.localScale = Vector3.one * scale;
                renderer.sortingOrder = order;
            }
            Replace("WarehouseWhitebox", "Warehouse", game.appearance.warehouse, PrototypeSimulation.WarehouseX, 1.65f, 0.94f, SortAt(4.5f));
            Replace("OldHousingWhitebox", "OldHousing", game.appearance.housing, PrototypeSimulation.HousingX, 1.65f, 0.94f, SortAt(4.5f));
            Replace("HoleWhitebox", "Hole", game.appearance.hole, 2.5f, 20.5f, 1, -1500);
            Replace("CommunityLamp", "CommunityLamp", game.appearance.streetlamp, 20, 4.6f, 1, SortAt(5));
            Replace("WoodworkBench", "WoodworkBench", game.appearance.woodwork, PrototypeSimulation.BenchX + 0.5f, PrototypeSimulation.BenchY + 0.5f, 0.92f, SortAt(PrototypeSimulation.BenchY + 0.5f));
            Replace("GeneratorWhitebox", "Generator", game.appearance.generator, PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 0.5f, 0.92f, SortAt(PrototypeSimulation.GeneratorY + 0.5f));
            Replace("TableWhitebox", "Workbench", game.appearance.workbench, PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 0.5f, 0.92f, SortAt(PrototypeSimulation.TableY + 0.5f));
            Replace("DismantleWhitebox", "Dismantle", game.appearance.dismantle, PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 0.5f, 0.92f, SortAt(PrototypeSimulation.DismantleY + 0.5f));
            foreach (var renderer in visualRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer => renderer.sprite == game.appearance.marker))
                renderer.sortingOrder = renderer.name.Contains("Plate") ? 1998 : -1400;
            foreach (var label in visualRoot.GetComponentsInChildren<TextMesh>(true)) label.GetComponent<MeshRenderer>().sortingOrder = 2000;
            Line("Warehouse Interaction Front", PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY, 0.9f, 0.12f, new Color32(230, 179, 102, 190));
            var oldRenderer = worker.GetComponent<SpriteRenderer>();
            if (Application.isPlaying) Destroy(oldRenderer); else DestroyImmediate(oldRenderer);
            worker.localScale = Vector3.one;
            worker.gameObject.AddComponent<PrototypeActorView>().Configure(game.appearance, 0);
            piles.Clear(); cells.Clear(); residentViews.Clear(); previousModel = null; lastRevision = -1;
            layoutVersion = 3;
        }

        private PrototypeActorView Actor(string objectName, int skin)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(visualRoot, false);
            var actor = obj.AddComponent<PrototypeActorView>();
            actor.Configure(game.appearance, skin);
            return actor;
        }

        private SpriteRenderer Sprite(string objectName, Sprite image, float x, float y, float scale, int order)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(visualRoot, false);
            obj.transform.position = new Vector3(x, y, 0);
            obj.transform.localScale = new Vector3(scale, scale, 1);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = image;
            renderer.sortingOrder = order;
            return renderer;
        }

        private TextMesh Label(string objectName, string value, float x, float y, float size, Color? color = null)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(visualRoot, false);
            obj.transform.position = new Vector3(x, y, -0.1f);
            var mesh = obj.AddComponent<TextMesh>();
            mesh.font = game.appearance.font;
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.lineSpacing = 0.8f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color ?? new Color32(225, 222, 202, 255);
            mesh.text = value;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = 2000;
            return mesh;
        }
    }
}
