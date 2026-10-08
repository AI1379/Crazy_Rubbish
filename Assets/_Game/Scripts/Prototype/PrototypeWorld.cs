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
        private readonly List<SpriteRenderer> floor = new List<SpriteRenderer>();
        private Transform worker;
        private SpriteRenderer bench;
        private TextMesh benchLabel, playerLabel, generatorLabel, tableLabel, dismantleLabel;
        private SpriteRenderer generator, table, dismantle;
        private readonly Dictionary<int, Transform> residentViews = new Dictionary<int, Transform>();
        private PrototypeSimulation previousModel;
        private int lastRevision = -1;
        private int screenWidth, screenHeight;

        private sealed class PileView
        { public GameObject Root; public SpriteRenderer Icon, Highlight; public TextMesh Count; }

        private void Start()
        {
            PrototypeSimulation model = game.Model;
            worldCamera.backgroundColor = new Color32(18, 28, 31, 255);
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            FitCamera(model);
            worldCamera.transform.position = new Vector3((model.Settings.mapWidth - 1) / 2f,
                (model.Settings.mapHeight - 1) / 2f, -10);
            worldCamera.rect = new Rect(0.024f, 0.21f, 0.71f, 0.60f);
            for (int x = 0; x < model.Settings.mapWidth; x++)
                for (int y = 0; y < model.Settings.mapHeight; y++)
                    floor.Add(Sprite("Ground", game.appearance.tile, x, y, 1, -20));
            SpriteRenderer depot = Sprite("WarehouseWhitebox", game.appearance.warehouse,
                PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY, 2.8f, 4);
            depot.color = new Color32(165, 194, 174, 255);
            bench = Sprite("WoodworkBench", game.appearance.workbench,
                PrototypeSimulation.BenchX, PrototypeSimulation.BenchY, 1.7f, 5);
            benchLabel = Label("BenchLabel", "木工台 · 待搭建", PrototypeSimulation.BenchX + 0.5f, PrototypeSimulation.BenchY + 2, 0.13f);
            generator = Sprite("GeneratorWhitebox", game.appearance.workbench, PrototypeSimulation.GeneratorX, PrototypeSimulation.GeneratorY, 1.8f, 5);
            generatorLabel = Label("GeneratorLabel", "发电机 · 待组装", PrototypeSimulation.GeneratorX + 0.5f, PrototypeSimulation.GeneratorY + 2, 0.13f);
            table = Sprite("TableWhitebox", game.appearance.workbench, PrototypeSimulation.TableX, PrototypeSimulation.TableY, 1.8f, 5);
            tableLabel = Label("TableLabel", "工作台 · 待建", PrototypeSimulation.TableX + 0.5f, PrototypeSimulation.TableY + 2, 0.13f);
            dismantle = Sprite("DismantleWhitebox", game.appearance.workbench, PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY, 1.8f, 5);
            dismantleLabel = Label("DismantleLabel", "拆解台 · 待建", PrototypeSimulation.DismantleX + 0.5f, PrototypeSimulation.DismantleY + 2, 0.13f);
            var housing = Sprite("OldHousingWhitebox", game.appearance.warehouse, PrototypeSimulation.HousingX, PrototypeSimulation.HousingY, 2.8f, 4);
            housing.color = new Color32(214, 177, 136, 255);
            Label("HousingLabel", "旧住房 · 6人", PrototypeSimulation.HousingX, PrototypeSimulation.HousingY + 2, 0.13f);
            Label("OwnedLandLabel", "可建设空地 18×18", 21, 18.5f, 0.18f);
            var hole = Sprite("HoleWhitebox", game.appearance.tile, 4, 2, 1.8f, -5);
            hole.color = new Color32(38, 43, 43, 255);
            Label("HoleLabel", "洞 · 去向不明", 4, 0.8f, 0.12f);
            Label("WarehouseLabel", "仓库", PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY + 2, 0.13f);
            Label("DropLabel", "上层垃圾落点", PrototypeSimulation.DropX, 12, 0.15f);

            worker = Sprite("Protagonist", game.appearance.worker, 4, 4, 1.1f, 40).transform;
            playerLabel = Label("PlayerLabel", "主角", 4, 3.25f, 0.08f);
        }

        private void LateUpdate()
        {
            PrototypeSimulation model = game.Model;
            if (screenWidth != Screen.width || screenHeight != Screen.height) FitCamera(model);
            if (previousModel != model)
            {
                foreach (PileView view in piles.Values) Destroy(view.Root);
                piles.Clear();
                foreach (Transform view in residentViews.Values) Destroy(view.gameObject);
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
                        Color baseColor = PrototypeSimulation.IsOwnedGround(x, y) ? Color.white : new Color32(95, 114, 110, 255);
                        floor[index++].color = Color.Lerp(baseColor, new Color32(188, 128, 91, 255),
                            weight * (1 - model.HumanEfficiency) * 1.05f);
                    }
            }
            GroundPile nearest = model.NearestPickup();
            foreach (var entry in piles)
                entry.Value.Highlight.enabled = nearest != null && entry.Key == PileKey(nearest, model);
            worker.position = new Vector3((float)model.PlayerX, (float)model.PlayerY, 0);
            playerLabel.transform.position = worker.position + new Vector3(0, -0.75f, 0);
            bench.color = model.BenchBuilt ? Color.white : new Color32(195, 180, 155, 85);
            benchLabel.text = model.BenchBuilt ? "木工台" : model.BuildPending ? "木工台 · 蓝图" : "木工台 · 待搭建";
            generator.color = model.GeneratorBuilt ? new Color32(226, 165, 91, 255) : new Color32(226, 165, 91, 65);
            table.color = model.TableBuilt ? Color.white : new Color32(195, 180, 155, 65);
            dismantle.color = model.DismantleBuilt ? new Color32(147, 184, 201, 255) : new Color32(147, 184, 201, 65);
            generatorLabel.text = model.GeneratorBuilt ? $"发电机 · {model.AvailablePower:0.#}电力" : "发电机 · 待组装";
            tableLabel.text = model.TableBuilt ? "通用工作台" : "工作台 · 待建";
            dismantleLabel.text = model.DismantleBuilt ? "拆解台" : "拆解台 · 待建";
            foreach (Resident resident in model.Residents)
            {
                if (!residentViews.TryGetValue(resident.Id, out Transform view))
                {
                    view = Sprite("Resident_" + resident.Id, game.appearance.worker, (float)resident.X, (float)resident.Y, 1, 35).transform;
                    view.GetComponent<SpriteRenderer>().color = new Color32(143, 200, 179, 255);
                    var label = Label("ResidentLabel", "居民" + resident.Id, (float)resident.X, (float)resident.Y - 0.7f, 0.11f);
                    label.transform.SetParent(view, true);
                    residentViews.Add(resident.Id, view);
                }
                view.position = new Vector3((float)resident.X, (float)resident.Y, 0);
            }
        }

        private void FitCamera(PrototypeSimulation model)
        {
            screenWidth = Screen.width; screenHeight = Screen.height;
            float aspect = Mathf.Max(0.1f, Screen.width * 0.71f / Mathf.Max(1, Screen.height * 0.60f));
            worldCamera.orthographicSize = Math.Max(model.Settings.mapHeight / 2f + 0.65f,
                model.Settings.mapWidth / (2f * aspect) + 0.65f);
        }

        private static int PileKey(GroundPile pile, PrototypeSimulation model) =>
            (pile.Y * model.Settings.mapWidth + pile.X) * Enum.GetValues(typeof(ItemId)).Length + (int)pile.Item;

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
                    root.transform.SetParent(transform, false);
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
                bool overloaded = model.Ground.Where(p => p.X == pile.X && p.Y == pile.Y)
                    .Sum(p => p.Amount * ItemCatalog.CapacityTicks(p.Item)) > model.Settings.pileCapacity * 20;
                view.Count.text = "×" + pile.Amount + (overloaded ? "↑" : "");
                view.Count.color = overloaded ? new Color32(242, 166, 92, 255) : new Color32(225, 222, 202, 255);
            }
            var removed = new List<int>();
            foreach (var entry in piles) if (!alive.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (int key in removed) { Destroy(piles[key].Root); piles.Remove(key); }
        }

        private SpriteRenderer Sprite(string objectName, Sprite image, float x, float y, float scale, int order)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(transform, false);
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
            obj.transform.SetParent(transform, false);
            obj.transform.position = new Vector3(x, y, -0.1f);
            var mesh = obj.AddComponent<TextMesh>();
            mesh.font = game.appearance.font;
            mesh.fontSize = 48;
            mesh.characterSize = size;
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
