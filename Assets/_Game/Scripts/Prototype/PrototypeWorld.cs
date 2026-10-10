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

        private sealed class PileView
        { public GameObject Root; public SpriteRenderer Icon, Highlight; public TextMesh Count; }

        private void Start()
        {
            PrototypeSimulation model = game.Model;
            BuildLayout(model);
            // The saved scene contains the starting items for editing. Runtime owns their state.
            foreach (Transform child in pileRoot) Destroy(child.gameObject);
            piles.Clear();
            previousModel = null;
            lastRevision = -1;
            FitCamera(model);
        }

        public void BuildLayout(PrototypeSimulation model)
        {
            if (HasLayout) return;
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
            benchLabel = Label("BenchLabel", "木工台\n待搭建", PrototypeSimulation.BenchX + 0.5f, PrototypeSimulation.BenchY + 3.5f, 0.16f);
            generator = Sprite("GeneratorWhitebox", game.appearance.workbench, PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 1.5f, 1.8f, 5);
            generatorLabel = Label("GeneratorLabel", "发电机\n待组装", PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 3.5f, 0.16f);
            table = Sprite("TableWhitebox", game.appearance.workbench, PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 1.5f, 1.8f, 5);
            tableLabel = Label("TableLabel", "工作台\n待搭建", PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 3.5f, 0.16f);
            dismantle = Sprite("DismantleWhitebox", game.appearance.workbench, PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 1.5f, 1.8f, 5);
            dismantleLabel = Label("DismantleLabel", "拆解台\n待搭建", PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 3.5f, 0.16f);
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
            RefreshView(model);
        }

        private void LateUpdate()
        {
            PrototypeSimulation model = game.Model;
            if (!HasLayout || model == null) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) FitCamera(model);
            float scroll = Input.mouseScrollDelta.y;
            Vector2 mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height));
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
        }

        public void RefreshView(PrototypeSimulation model)
        {
            if (previousModel != model)
            {
                foreach (PileView view in piles.Values) RemoveView(view.Root);
                piles.Clear();
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
            foreach (var entry in piles)
                entry.Value.Highlight.enabled = nearest != null && entry.Key == PileKey(nearest, model);
            worker.position = new Vector3((float)model.PlayerX, (float)model.PlayerY, 0);
            playerLabel.transform.position = worker.position + new Vector3(0, -0.75f, 0);
            bench.color = model.BenchBuilt ? Color.white : new Color32(195, 180, 155, 130);
            benchLabel.text = model.BenchBuilt ? "木工台" : model.BuildPending ? "木工台\n蓝图" : "木工台\n待搭建";
            generator.color = model.GeneratorBuilt ? new Color32(226, 165, 91, 255) : new Color32(226, 165, 91, 130);
            table.color = model.TableBuilt ? Color.white : new Color32(195, 180, 155, 130);
            dismantle.color = model.DismantleBuilt ? new Color32(147, 184, 201, 255) : new Color32(147, 184, 201, 130);
            generatorLabel.text = model.GeneratorBuilt ? $"发电机\n{model.AvailablePower:0.#}电力" : "发电机\n待组装";
            tableLabel.text = model.TableBuilt ? "工作台" : "工作台\n待搭建";
            dismantleLabel.text = model.DismantleBuilt ? "拆解台" : "拆解台\n待搭建";
            foreach (Resident resident in model.Residents)
            {
                if (!residentViews.TryGetValue(resident.Id, out Transform view))
                {
                    view = Sprite("Resident_" + resident.Id, game.appearance.worker, (float)resident.X, (float)resident.Y, 1, 35).transform;
                    view.GetComponent<SpriteRenderer>().color = new Color32(143, 200, 179, 255);
                    var label = Label("ResidentLabel", resident.Id.ToString(), (float)resident.X, (float)resident.Y - 0.7f, 0.10f);
                    label.transform.SetParent(view, true);
                    residentViews.Add(resident.Id, view);
                }
                view.position = new Vector3((float)resident.X, (float)resident.Y, 0);
            }
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
            p.x = Mathf.Clamp(p.x, 0, model.Settings.mapWidth - 1);
            p.y = Mathf.Clamp(p.y, 0, model.Settings.mapHeight - 1);
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
                    view.Highlight = Sprite("PickupHighlight", game.appearance.tile, pile.X, pile.Y, 1.03f, -3);
                    view.Highlight.color = new Color32(230, 177, 86, 255);
                    view.Icon = Sprite("Item", game.appearance.ItemSprite(pile.Item), pile.X, pile.Y, 0.88f, 6);
                    view.Count = Label("Quantity", "", pile.X + 0.25f, pile.Y - 0.35f, 0.07f);
                    view.Highlight.transform.SetParent(root.transform, true);
                    view.Icon.transform.SetParent(root.transform, true);
                    view.Count.transform.SetParent(root.transform, true);
                    piles.Add(key, view);
                }
                view.Icon.sprite = game.appearance.ItemSprite(pile.Item);
                int index = 0;
                foreach (GroundPile other in model.Ground)
                    if (other.X == pile.X && other.Y == pile.Y && (int)other.Item < (int)pile.Item) index++;
                float dx = (index % 2) * 0.37f - 0.18f;
                float dy = (index / 2) * 0.37f;
                view.Icon.transform.position = new Vector3(pile.X + dx, pile.Y + dy, 0);
                view.Icon.transform.localScale = new Vector3(0.55f, 0.55f, 1);
                view.Count.transform.position = new Vector3(pile.X + dx, pile.Y + dy - 0.28f, -0.1f);
                bool overloaded = model.GroundCapacityAt(pile.X, pile.Y) > model.Settings.pileCapacity;
                view.Count.text = "×" + pile.Amount + (overloaded ? "↑" : "") + (ItemCatalog.IsDevice(pile.Item) ? (pile.Usable ? "✓" : "×") : "");
                view.Icon.color = ItemCatalog.IsDevice(pile.Item) ? new Color32(203, 178, 139, 255) : Color.white;
                view.Count.color = overloaded ? new Color32(242, 166, 92, 255) : new Color32(225, 222, 202, 255);
            }
            var removed = new List<int>();
            foreach (var entry in piles) if (!alive.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (int key in removed) { RemoveView(piles[key].Root); piles.Remove(key); }
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
