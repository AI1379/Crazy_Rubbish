using System;
using System.Collections.Generic;
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
        private TextMesh benchLabel, playerLabel;
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
            worldCamera.rect = new Rect(0.024f, 0.21f, 0.71f, 0.64f);
            for (int x = 0; x < model.Settings.mapWidth; x++)
                for (int y = 0; y < model.Settings.mapHeight; y++)
                    floor.Add(Sprite("Ground", game.appearance.tile, x, y, 1, -20));
            SpriteRenderer depot = Sprite("WarehouseWhitebox", game.appearance.warehouse,
                PrototypeSimulation.WarehouseX, PrototypeSimulation.WarehouseY, 2.8f, 4);
            depot.color = new Color32(165, 194, 174, 255);
            bench = Sprite("WoodworkBench", game.appearance.workbench,
                PrototypeSimulation.BenchX, PrototypeSimulation.BenchY, 1.7f, 5);
            benchLabel = Label("BenchLabel", "木工台 · 待搭建", PrototypeSimulation.BenchX + 1.2f, 8f, 0.10f);
            Label("WarehouseLabel", "仓库", PrototypeSimulation.WarehouseX, 7.05f, 0.11f);
            Label("DropLabel", "上层垃圾落点", PrototypeSimulation.DropX - 0.5f, 10.6f, 0.10f);
            Label("PlasticLabel", "混杂塑料 · 暂存", 7.7f, 3.2f, 0.085f, new Color32(133, 164, 188, 255));
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
                        floor[index++].color = Color.Lerp(Color.white, new Color32(188, 128, 91, 255),
                            weight * (1 - model.HumanEfficiency) * 1.05f);
                    }
            }
            GroundPile nearest = model.NearestPickup();
            foreach (var entry in piles)
                entry.Value.Highlight.enabled = nearest != null && entry.Key == nearest.Y * model.Settings.mapWidth + nearest.X;
            worker.position = new Vector3((float)model.PlayerX, (float)model.PlayerY, 0);
            playerLabel.transform.position = worker.position + new Vector3(0, -0.75f, 0);
            bench.color = model.BenchBuilt ? Color.white : new Color32(195, 180, 155, 85);
            benchLabel.text = model.BenchBuilt ? "木工台" : model.BuildPending ? "木工台 · 等待恢复" : "木工台 · 待搭建";
        }

        private void FitCamera(PrototypeSimulation model)
        {
            screenWidth = Screen.width; screenHeight = Screen.height;
            float aspect = Mathf.Max(0.1f, Screen.width * 0.71f / Mathf.Max(1, Screen.height * 0.64f));
            worldCamera.orthographicSize = Math.Max(model.Settings.mapHeight / 2f + 0.65f,
                model.Settings.mapWidth / (2f * aspect) + 0.65f);
        }

        private void ReconcilePiles(PrototypeSimulation model)
        {
            var alive = new HashSet<int>();
            foreach (GroundPile pile in model.Ground)
            {
                int key = pile.Y * model.Settings.mapWidth + pile.X;
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
                view.Count.text = "×" + pile.Amount;
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
