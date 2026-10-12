using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeWorld : MonoBehaviour
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
        private readonly List<TextMesh> secondaryLabels = new List<TextMesh>();
        [SerializeField] private int layoutVersion;
        public bool HasCurrentLayout => HasLayout && layoutVersion == 2;

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
            if (HasLayout) { ConfigureLabels(); return; }
            layoutVersion = 2;
            visualRoot = new GameObject("World").transform;
            visualRoot.SetParent(transform, false);
            pileRoot = new GameObject("Ground Items").transform;
            pileRoot.SetParent(visualRoot, false);
            floor.Clear();
            worldCamera.backgroundColor = new Color32(17, 27, 30, 255);
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.orthographic = true;
            FitCamera(model);
            worldCamera.transform.position = new Vector3((model.Settings.mapWidth - 1) / 2f,
                (model.Settings.mapHeight - 1) / 2f, -10);
            for (int x = 0; x < model.Settings.mapWidth; x++)
                for (int y = 0; y < model.Settings.mapHeight; y++)
                {
                    var tile = Sprite($"Ground {x},{y}", game.appearance.tile, x, y, 1, -20);
                    tile.color = GroundColor(x, y);
                    floor.Add(tile);
                }
            ZoneOutline("Construction Boundary", 9.5f, 5.5f, 18, 18, new Color32(130, 170, 141, 255));
            ZoneOutline("Receiving Boundary", -0.5f, 5.5f, 8, 6, new Color32(223, 164, 94, 255));
            StationPad("Woodwork Pad", PrototypeSimulation.BenchX, PrototypeSimulation.BenchY, new Color32(171, 133, 78, 255));
            StationPad("Dismantle Pad", PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY, new Color32(87, 138, 153, 255));
            StationPad("Workbench Pad", PrototypeSimulation.TableX, PrototypeSimulation.TableY, new Color32(135, 160, 113, 255));
            StationPad("Generator Pad", PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY, new Color32(192, 150, 76, 255));
            SpriteRenderer depot = Sprite("WarehouseWhitebox", game.appearance.warehouse,
                PrototypeSimulation.WarehouseX, 3, 2.8f, 4);
            depot.color = new Color32(165, 194, 174, 255);
            bench = Sprite("WoodworkBench", game.appearance.workbench,
                PrototypeSimulation.BenchX + 0.5f, PrototypeSimulation.BenchY + 1.5f, 1.7f, 5);
            benchLabel = StationLabel("BenchLabel", "木工台", PrototypeSimulation.BenchX, PrototypeSimulation.BenchY);
            generator = Sprite("GeneratorWhitebox", game.appearance.workbench, PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 1.5f, 1.8f, 5);
            generatorLabel = StationLabel("GeneratorLabel", "发电机", PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY);
            table = Sprite("TableWhitebox", game.appearance.workbench, PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 1.5f, 1.8f, 5);
            tableLabel = StationLabel("TableLabel", "工作台", PrototypeSimulation.TableX, PrototypeSimulation.TableY);
            dismantle = Sprite("DismantleWhitebox", game.appearance.workbench, PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 1.5f, 1.8f, 5);
            dismantleLabel = StationLabel("DismantleLabel", "拆解台", PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY);
            var housing = Sprite("OldHousingWhitebox", game.appearance.warehouse, PrototypeSimulation.HousingX, 3, 2.8f, 4);
            housing.color = new Color32(214, 177, 136, 255);
            Label("HousingLabel", "旧住房 · 6人", PrototypeSimulation.HousingX, 1.5f, 0.13f);
            Label("OwnedLandLabel", "建设区 / 18×18", 20, 23.8f, 0.18f, new Color32(166, 199, 169, 255));
            var hole = Sprite("HoleWhitebox", game.appearance.tile, 2.5f, 20.5f, 1.8f, -5);
            hole.color = new Color32(38, 43, 43, 255);
            Label("HoleLabel", "洞 · 去向不明", 2.5f, 19.2f, 0.12f);
            Label("WarehouseLabel", "仓库", PrototypeSimulation.WarehouseX, 5.8f, 0.13f);
            Label("DropLabel", "来料区 / 8×6", 3.5f, 12.3f, 0.15f, new Color32(232, 185, 121, 255));

            var lamp = Sprite("CommunityLamp", game.appearance.fastener, 20, 5, 0.8f, 5);
            lamp.color = new Color32(143, 142, 112, 255);
            Label("LampLabel", "社区路灯", 20, 6, 0.11f);
            Label("MeetingPoint", "会面点", 16, -0.5f, 0.11f);
            worker = Sprite("Protagonist", game.appearance.worker, (float)model.PlayerX, (float)model.PlayerY, 1.25f, 40).transform;
            playerLabel = Label("PlayerLabel", "主角", (float)model.PlayerX, (float)model.PlayerY - 0.8f, 0.10f, new Color32(255, 206, 123, 255));
            ConfigureLabels();
            RefreshView(model);
        }

        private void LateUpdate()
        {
            PrototypeSimulation model = game.Model;
            if (!HasLayout || model == null) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) FitCamera(model);
            float scroll = Input.mouseScrollDelta.y;
            Vector2 mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height));
            if(Input.GetMouseButtonDown(0)&&worldCamera.rect.Contains(mouse)&&!game.MenuOpen&&!game.ResultVisible&&(UnityEngine.EventSystems.EventSystem.current==null||!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                Vector3 point=worldCamera.ScreenToWorldPoint(Input.mousePosition);
                int x=Mathf.RoundToInt(point.x),y=Mathf.RoundToInt(point.y);
                // Facility sprites sit above their reachable interaction fronts.
                foreach(var station in new[]{(PrototypeSimulation.BenchX,PrototypeSimulation.BenchY),(PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY),(PrototypeSimulation.TableX,PrototypeSimulation.TableY),(PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY)})
                    if(x>=station.Item1&&x<station.Item1+2&&y>station.Item2&&y<=station.Item2+2){x=station.Item1;y=station.Item2;break;}
                if(x>=11&&x<=13&&y>=2&&y<=4){x=PrototypeSimulation.WarehouseX;y=PrototypeSimulation.WarehouseY;}
                game.GoTo(x,y);
            }
            if (scroll != 0 && worldCamera.rect.Contains(mouse))
            {
                Vector3 before = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize * Mathf.Pow(0.85f, scroll), 4, FullMapSize(model));
                Vector3 after = worldCamera.ScreenToWorldPoint(Input.mousePosition);
                worldCamera.transform.position += before - after;
                ClampCamera(model);
            }
            if (Input.GetMouseButton(2) && worldCamera.rect.Contains(mouse))
            {
                float units = worldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height * worldCamera.rect.height);
                worldCamera.transform.position -= new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0) * units * 12;
                ClampCamera(model);
            }
            RefreshView(model);
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            InspectAt(worldCamera.rect.Contains(mouse) && !overUi && !game.MenuOpen && !game.ResultVisible
                ? (Vector2?)worldCamera.ScreenToWorldPoint(Input.mousePosition) : null);
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
            float pixelsPerCell = Screen.height * worldCamera.rect.height / (worldCamera.orthographicSize * 2);
            foreach (CellView cell in cells)
            {
                cell.View.Highlight.enabled = nearest != null && nearest.X == cell.X && nearest.Y == cell.Y;
                bool detailed = pixelsPerCell >= 42;
                cell.View.Count.gameObject.SetActive(detailed);
                cell.View.Badge.enabled = detailed || cell.Types > 1;
                cell.View.Badge.color = detailed ? new Color32(29, 45, 48, 255) : new Color32(221, 180, 109, 255);
                SetSpriteSize(cell.View.Badge, detailed ? 0.42f : 0.15f, detailed ? 0.24f : 0.15f);
            }
            foreach (TextMesh label in secondaryLabels) label.gameObject.SetActive(pixelsPerCell >= 26 && LabelFitsViewport(label));
            worker.position = new Vector3((float)model.PlayerX, (float)model.PlayerY, 0);
            playerLabel.transform.position = worker.position + new Vector3(0, -0.75f, 0);
            bench.color = model.BenchBuilt ? Color.white : new Color32(195, 180, 155, 130);
            benchLabel.text = "木工台";
            benchLabel.color = model.BenchBuilt ? Color.white : new Color32(212, 187, 137, 255);
            generator.color = model.GeneratorBuilt ? new Color32(226, 165, 91, 255) : new Color32(226, 165, 91, 130);
            table.color = model.TableBuilt ? Color.white : new Color32(195, 180, 155, 130);
            dismantle.color = model.DismantleBuilt ? new Color32(147, 184, 201, 255) : new Color32(147, 184, 201, 130);
            generatorLabel.text = "发电机";
            tableLabel.text = "工作台";
            dismantleLabel.text = "拆解台";
            foreach (TextMesh label in new[] { benchLabel, generatorLabel, tableLabel, dismantleLabel })
                label.gameObject.SetActive(LabelFitsViewport(label));
            foreach(int id in residentViews.Keys.Where(id=>!model.Residents.Any(r=>r.Id==id)).ToArray())
            {RemoveView(residentViews[id].gameObject);residentViews.Remove(id);}
            foreach (Resident resident in model.Residents)
            {
                if (!residentViews.TryGetValue(resident.Id, out Transform view))
                {
                    view = Sprite("Resident_" + resident.Id, game.appearance.worker, (float)resident.X, (float)resident.Y, 1, 35).transform;
                    view.GetComponent<SpriteRenderer>().color = new Color32(143, 200, 179, 255);
                    residentViews.Add(resident.Id, view);
                }
                view.position = new Vector3((float)resident.X, (float)resident.Y, 0);
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
                    var part=Sprite("Tutorial Target Edge",marker,0,0,1,45);part.transform.SetParent(tutorialTarget,false);part.transform.localPosition=new Vector3(edge.x,edge.y,0);
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
            float canvasHeight = 1600f * Mathf.Max(1, Screen.height) / Mathf.Max(1, Screen.width);
            worldCamera.rect = new Rect(24 / 1600f, 188 / canvasHeight, 1136 / 1600f,
                Mathf.Max(1, canvasHeight - 366) / canvasHeight);
            worldCamera.orthographicSize = FullMapSize(model);
            worldCamera.transform.position = new Vector3((model.Settings.mapWidth - 1) / 2f, (model.Settings.mapHeight - 1) / 2f, -10);
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
            if (x <= 7 && y >= 6 && y <= 11) return new Color32(255, 221, 163, 255);
            if (x >= 10 && x <= 27 && y >= 6 && y <= 23) return new Color32(217, 248, 221, 255);
            if (y >= 3 && y <= 5 && x >= 8 && x <= 23) return new Color32(238, 230, 205, 255);
            return (x + y) % 2 == 0 ? new Color32(131, 171, 157, 255) : new Color32(138, 178, 163, 255);
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
            var line = Sprite(name, marker, x, y, 1, -12);
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
            var plate = Sprite(name + " Plate", game.appearance.marker, x + 0.5f, y + 2.15f, 1, 48);
            SetSpriteSize(plate, 1.9f, 0.48f);
            plate.color = new Color32(24, 38, 40, 240);
            plate.transform.SetParent(label.transform, true);
            return label;
        }

        private void ConfigureLabels()
        {
            // Also migrate older saved scenes without destroying hand-authored scenery.
            playerLabel.gameObject.SetActive(false);
            secondaryLabels.Clear();
            foreach (string name in new[] { "HousingLabel", "HoleLabel", "WarehouseLabel", "LampLabel", "MeetingPoint" })
            {
                Transform label = visualRoot.Find(name);
                if (label != null) secondaryLabels.Add(label.GetComponent<TextMesh>());
            }
            Transform warehouseName = visualRoot.Find("WarehouseLabel");
            warehouseName.position = new Vector3(PrototypeSimulation.WarehouseX, 3.9f, -0.1f);
            warehouseName.GetComponent<TextMesh>().characterSize = 0.09f;
            if (warehouseName.childCount == 0)
            {
                var plate = Sprite("Warehouse Name Plate", game.appearance.marker, PrototypeSimulation.WarehouseX, 3.9f, 1, 48);
                SetSpriteSize(plate, 1.45f, 0.44f); plate.color = new Color32(24, 38, 40, 240);
                plate.transform.SetParent(warehouseName, true);
            }
            visualRoot.Find("LampLabel").position = new Vector3(20, 4.2f, -0.1f);
            foreach (var station in new[] { (benchLabel, PrototypeSimulation.BenchX, PrototypeSimulation.BenchY),
                (tableLabel, PrototypeSimulation.TableX, PrototypeSimulation.TableY),
                (dismantleLabel, PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY),
                (generatorLabel, PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY) })
            {
                station.Item1.characterSize = 0.095f;
                station.Item1.transform.position = new Vector3(station.Item2 + 0.5f, station.Item3 + 2.15f, -0.1f);
                if (station.Item1.transform.childCount == 0)
                {
                    var plate = Sprite(station.Item1.name + " Plate", game.appearance.marker, station.Item2 + 0.5f, station.Item3 + 2.15f, 1, 48);
                    SetSpriteSize(plate, 1.9f, 0.48f); plate.color = new Color32(24, 38, 40, 240);
                    plate.transform.SetParent(station.Item1.transform, true);
                }
            }
            layoutVersion = 2;
        }

        private static void SetSpriteSize(SpriteRenderer renderer, float width, float height)
        {
            renderer.transform.localScale = new Vector3(width / renderer.sprite.bounds.size.x, height / renderer.sprite.bounds.size.y, 1);
        }

        private bool LabelFitsViewport(TextMesh label)
        {
            Bounds bounds = label.GetComponent<MeshRenderer>().bounds;
            Vector3 min = worldCamera.WorldToViewportPoint(bounds.min);
            Vector3 max = worldCamera.WorldToViewportPoint(bounds.max);
            return min.x >= 0.01f && min.y >= 0.01f && max.x <= 0.99f && max.y <= 0.99f;
        }

        public void InspectAt(Vector2? point)
        {
            var hud = game.GetComponent<PrototypeHud>();
            if (hud == null) return;
            if (!point.HasValue || game.MenuOpen || game.ResultVisible) { hud.ShowInspection(null, null, Vector2.zero); return; }
            int x = Mathf.RoundToInt(point.Value.x), y = Mathf.RoundToInt(point.Value.y);
            var model = game.Model;
            string title = null, body = null;
            foreach (var station in new[] { ("木工台", PrototypeSimulation.BenchX, PrototypeSimulation.BenchY, model.BenchBuilt),
                ("拆解台", PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY, model.DismantleBuilt),
                ("工作台", PrototypeSimulation.TableX, PrototypeSimulation.TableY, model.TableBuilt),
                ("发电机", PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY, model.GeneratorBuilt) })
                if (x >= station.Item2 && x < station.Item2 + 2 && y > station.Item3 && y <= station.Item3 + 2)
                {
                    title = station.Item1;
                    body = station.Item4 ? "已建成 · 点击前往操作点" : "待建设 · 在现场页安排建设";
                    if (station.Item1 == "发电机" && station.Item4) body += $"\n当前电力 {model.AvailablePower:0.#} · 燃料 {model.FuelCount}";
                    break;
                }
            var content = model.Ground.Where(p => y == p.Y && x >= p.X && x < p.X + p.Width).ToArray();
            if (content.Length > 0)
            {
                if (title == null) title = $"地面物品 · ({x}, {y})";
                var lines = content.GroupBy(p => p.Item).OrderBy(g => g.Key).Select(g =>
                {
                    int unavailable = ItemCatalog.IsTool(g.Key) || ItemCatalog.IsDevice(g.Key) || ItemCatalog.IsOpenable(g.Key)
                        ? g.Where(p => !p.Usable).Sum(p => p.Amount) : 0;
                    int reserved = g.Sum(p => p.Reserved);
                    return ItemCatalog.Name(g.Key) + " ×" + g.Sum(p => p.Amount)
                        + (reserved > 0 ? $" · 预约 {reserved}" : "")
                        + (unavailable > 0 ? $" · 损坏 {unavailable}" : "");
                });
                body = (body == null ? "" : body + "\n") + string.Join("\n", lines);
                body += "\n走近后拾取 · 仓库页可指定物品";
            }
            hud.ShowInspection(title, body, worldCamera.WorldToScreenPoint(point.Value));
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
                    view.Highlight = Sprite("PickupHighlight", game.appearance.marker, pile.X, pile.Y, 1, -3);
                    SetSpriteSize(view.Highlight, 0.92f, 0.92f);
                    view.Highlight.color = new Color32(230, 177, 86, 55);
                    view.Icon = Sprite("Item", game.appearance.ItemSprite(pile.Item), pile.X, pile.Y, 0.74f, 6);
                    view.Count = Label("Quantity", "", pile.X + 0.24f, pile.Y - 0.24f, 0.055f);
                    view.Badge = Sprite("Cell Badge", game.appearance.marker, pile.X + 0.24f, pile.Y - 0.24f, 1, 49);
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
                view.Icon.enabled = true;
                view.Icon.color = Color.white;
                view.Count.text = types > 1 ? "+" + (types - 1) : amount.ToString();
                cells.Add(new CellView { X = primary.X, Y = primary.Y, Types = types, View = view });
            }
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
            renderer.sortingOrder = 50;
            return mesh;
        }
    }
}
