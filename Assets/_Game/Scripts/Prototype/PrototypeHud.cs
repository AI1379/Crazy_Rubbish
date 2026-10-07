using System;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public PrototypeGame game;
        private static readonly Color Ink = new Color32(232, 228, 207, 255);
        private static readonly Color Muted = new Color32(147, 168, 166, 255);
        private static readonly Color Accent = new Color32(226, 165, 91, 255);
        private static readonly Color Green = new Color32(127, 185, 143, 255);
        private Text clock, objective, capacity, money, pressure, workState, message, target, recipeInfo;
        private readonly Text[] carryCounts = new Text[6], warehouseCounts = new Text[6];
        private readonly Button[] withdraw = new Button[6], drop = new Button[6];
        private Button pause, build, work, pickup, deposit, plankRecipe, handleRecipe;
        private Button sellPlank, sellHandle, sellAll;
        private Image capacityFill, progressFill;
        private RectTransform root;

        private void Start()
        {
            var canvasObject = new GameObject("PrototypeHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

            RectTransform header = StretchPanel(root, "Header", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -88), new Vector2(-24, -20), new Color32(30, 43, 46, 255));
            TextAt(header, "Title", "涌现 / 废料工坊", 18, 8, 400, 36, 29, Ink);
            TextAt(header, "Subtitle", "拾取 → 加工 → 入库 → 出售", 20, 43, 450, 18, 15, Muted);
            clock = TextAt(header, "Clock", "", 645, 19, 200, 30, 21, Ink);
            pause = ButtonAt(header, "Pause", "暂停", 885, 14, 100, 39, () => game.Model.SetPaused(!game.Model.Paused));
            for (int index = 0; index < 3; index++)
            {
                int speed = index == 0 ? 1 : index == 1 ? 2 : 4;
                ButtonAt(header, "Speed" + speed, speed + "×", 995 + index * 70, 14, 60, 39, () => game.Model.SetSpeed(speed));
            }
            ButtonAt(header, "Restart", "重开", 1230, 14, 90, 39, game.ResetGame);
            TextAt(header, "PrototypeMark", "M1 · 实验参数", 1340, 23, 190, 24, 17, Accent);

            objective = TextAt(root, "Objective", "", 38, 106, 1130, 34, 23, Ink);
            target = TextAt(root, "PickupHint", "", 38, 144, 1120, 28, 17, Muted);

            RectTransform sidebar = StretchPanel(root, "WarehousePanel", new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-384, 22), new Vector2(-24, -106), new Color32(30, 43, 46, 255));
            sidebar = ScrollContent(sidebar, 760);
            TextAt(sidebar, "InventoryTitle", "仓储 / 物品", 20, 18, 320, 32, 25, Ink);
            capacity = TextAt(sidebar, "Capacity", "", 20, 59, 320, 27, 18, Muted);
            capacityFill = Bar(sidebar, "CapacityBar", 20, 94, 320, 8, Green);
            TextAt(sidebar, "Headings", "物品               背包   仓库", 20, 115, 260, 22, 15, Muted);
            for (int i = 0; i < 6; i++)
            {
                ItemId item = (ItemId)i;
                float y = 148 + i * 39;
                RectTransform icon = At(sidebar, "Icon" + i, 18, y + 3, 29, 29);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = game.appearance.ItemSprite(item);
                iconImage.raycastTarget = false;
                TextAt(sidebar, "Item" + i, PrototypeGame.ItemName(item), 54, y + 3, 92, 28, 18, Ink);
                carryCounts[i] = TextAt(sidebar, "Carry" + i, "", 151, y + 4, 35, 26, 18, Accent);
                warehouseCounts[i] = TextAt(sidebar, "Stored" + i, "", 192, y + 4, 35, 26, 18, Ink);
                withdraw[i] = ButtonAt(sidebar, "Withdraw" + i, "取1", 237, y, 47, 31, () => game.Report(game.Model.Withdraw(item)));
                drop[i] = ButtonAt(sidebar, "Drop" + i, "放1", 291, y, 47, 31, () => game.Report(game.Model.Drop(item)));
            }
            TextAt(sidebar, "InventoryHelp", "取出需靠近仓库；手锯默认随身保留。", 20, 387, 320, 42, 15, Muted);
            TextAt(sidebar, "SaleTitle", "产品出售", 20, 440, 320, 28, 22, Ink);
            sellPlank = ButtonAt(sidebar, "SellPlank", "售 1 木板", 20, 480, 153, 42,
                () => game.Report(game.Model.Sell(ItemId.Plank, 1)));
            sellHandle = ButtonAt(sidebar, "SellHandle", "售 1 木柄", 187, 480, 153, 42,
                () => game.Report(game.Model.Sell(ItemId.WoodHandle, 1)));
            sellAll = ButtonAt(sidebar, "SellAll", "出售全部产品", 20, 532, 320, 42, game.SellAll, true);
            money = TextAt(sidebar, "Money", "", 20, 590, 320, 34, 23, Accent);
            TextAt(sidebar, "ScopeHint", "混杂塑料先暂存。清理露天堆积，\n能改善木工效率。", 20, 642, 320, 57, 16, Muted);
            TextAt(sidebar, "BalanceHint", "配方、售价与容量是原型实验值。", 20, 709, 320, 26, 14, Muted);

            RectTransform actions = StretchPanel(root, "WorkPanel", new Vector2(0, 0), new Vector2(0.735f, 0),
                new Vector2(24, 22), new Vector2(0, 165), new Color32(30, 43, 46, 255));
            pickup = ButtonAt(actions, "Pickup", "拾取 E", 16, 14, 102, 39, () => game.Report(game.Model.PickUp()));
            deposit = ButtonAt(actions, "Deposit", "入库 Q", 128, 14, 102, 39, () => game.Report(game.Model.Deposit()));
            build = ButtonAt(actions, "Build", "搭建木工台", 246, 14, 166, 39, () => game.Report(game.Model.BuildBench()), true);
            plankRecipe = ButtonAt(actions, "PlankRecipe", "木板", 428, 14, 92, 39, () => game.Report(game.Model.SelectRecipe(WoodRecipe.Plank)));
            handleRecipe = ButtonAt(actions, "HandleRecipe", "木柄", 530, 14, 92, 39, () => game.Report(game.Model.SelectRecipe(WoodRecipe.WoodHandle)));
            work = ButtonAt(actions, "Work", "开工 F", 638, 14, 141, 39, () => game.Report(game.Model.ToggleWork()), true);
            pressure = TextAt(actions, "Pressure", "", 795, 15, 320, 48, 18, Green);
            workState = TextAt(actions, "WorkState", "", 18, 67, 395, 28, 18, Ink);
            progressFill = Bar(actions, "WorkProgress", 428, 73, 351, 9, Accent);
            recipeInfo = TextAt(actions, "RecipeInfo", "", 795, 68, 330, 33, 16, Muted);
            message = TextAt(actions, "Notice", "", 18, 108, 1095, 24, 16, Muted);
        }

        private void Update()
        {
            if (root == null) return;
            PrototypeSimulation model = game.Model;
            clock.text = $"{(int)model.GameSeconds / 60:00}:{(int)model.GameSeconds % 60:00}  ·  {(model.Paused ? "暂停" : model.Speed + "×速度")}";
            pause.GetComponentInChildren<Text>().text = model.Paused ? "恢复" : "暂停";
            capacity.text = $"仓库 {model.Warehouse.Used}/{model.Warehouse.Capacity}    背包 {model.Carry.Used}/{model.Carry.Capacity}";
            capacityFill.rectTransform.anchorMax = new Vector2((float)model.Warehouse.Used / model.Warehouse.Capacity, 1);
            for (int i = 0; i < 6; i++)
            {
                ItemId item = (ItemId)i;
                carryCounts[i].text = model.Carry.Count(item).ToString();
                warehouseCounts[i].text = model.Warehouse.Count(item).ToString();
                withdraw[i].interactable = !model.Paused && model.NearWarehouse && model.Carry.Free > 0 && model.Warehouse.Count(item) > 0;
                drop[i].interactable = !model.Paused && model.Carry.Count(item) > 0;
            }
            money.text = $"金钱 {model.Money}   /   已加工 {model.CompletedBatches} 批";
            pressure.text = $"人工效率 {model.HumanEfficiency:P0}\n附近占地 {model.NearbyPressureTiles} 格";
            workState.text = PrototypeGame.StateName(model.State) + (model.JobActive ? $" · {model.Progress:P0}" : "");
            workState.color = model.State == WorkState.Working ? Green : Ink;
            progressFill.rectTransform.anchorMax = new Vector2(model.Progress, 1);
            message.text = game.Notice;
            message.color = game.LastActionSucceeded ? Muted : Accent;
            build.interactable = !model.BenchBuilt && !model.BuildPending;
            build.GetComponentInChildren<Text>().text = model.BenchBuilt ? "木工台已建成" : model.BuildPending ? "等待恢复后搭建" : "搭建木工台";
            work.interactable = model.BenchBuilt;
            work.GetComponentInChildren<Text>().text = model.RunRequested ? "停工 F" : "开工 F";
            plankRecipe.interactable = !model.JobActive;
            handleRecipe.interactable = !model.JobActive;
            plankRecipe.GetComponent<Image>().color = model.SelectedRecipe == WoodRecipe.Plank ? new Color32(93, 103, 83, 255) : new Color32(51, 69, 70, 255);
            handleRecipe.GetComponent<Image>().color = model.SelectedRecipe == WoodRecipe.WoodHandle ? new Color32(93, 103, 83, 255) : new Color32(51, 69, 70, 255);
            string recipeName = model.SelectedRecipe == WoodRecipe.Plank ? "木板" : "木柄";
            recipeInfo.text = $"{model.Settings.recipeWoodInput} 废木料 → {model.ProductAmount(model.SelectedRecipe)} {recipeName}\n基础 {model.Duration(model.SelectedRecipe):0.#} 秒 / 批";
            pickup.interactable = !model.Paused && model.NearestPickup() != null && model.Carry.Free > 0;
            deposit.interactable = !model.Paused && model.NearWarehouse && model.Warehouse.Free > 0;
            sellPlank.interactable = model.Warehouse.Count(ItemId.Plank) > 0;
            sellHandle.interactable = model.Warehouse.Count(ItemId.WoodHandle) > 0;
            sellAll.interactable = sellPlank.interactable || sellHandle.interactable;
            sellPlank.GetComponentInChildren<Text>().text = $"售 1 木板 +{model.Settings.plankPrice}";
            sellHandle.GetComponentInChildren<Text>().text = $"售 1 木柄 +{model.Settings.handlePrice}";
            GroundPile nearest = model.NearestPickup();
            target.text = "WASD / 方向键移动 · E 拾取 · Shift+E 整堆 · Q 入库 · F 开工 · 空格暂停";
            if (nearest != null) target.text += $"    附近：{PrototypeGame.ItemName(nearest.Item)} ×{nearest.Amount}";
            if (model.IncomingUnits > 0) target.text = $"落点积压 {model.IncomingUnits} 单位，空出地面后继续落入；" + target.text;
            objective.text = Objective(model);
        }

        private static string Objective(PrototypeSimulation model)
        {
            if (model.Money > 0) return "流程已跑通！清走台旁垃圾，观察人工效率与加工速度的变化。";
            if (!model.HasSaw) return "01 / 在落点附近找到手锯，靠近后按 E 拾取。";
            if (!model.BenchBuilt && !model.BuildPending)
                return $"02 / 把 {model.Settings.benchWoodCost} 废木料 + {model.Settings.benchFastenerCost} 连接件送入仓库，再搭建木工台。";
            if (model.CompletedBatches == 0) return "03 / 木料放进仓库，带着手锯靠近木工台，按 F 连续加工。";
            return "04 / 产品已入库，从右侧出售木板或木柄，完成第一条回收流程。";
        }

        private RectTransform At(Transform parent, string objectName, float x, float y, float width, float height)
        {
            var obj = new GameObject(objectName, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform ScrollContent(RectTransform panel, float height)
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(panel, false);
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, height);
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.content = contentRect; scroll.viewport = viewportRect;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            return contentRect;
        }

        private RectTransform StretchPanel(Transform parent, string objectName, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            obj.GetComponent<Image>().color = color;
            obj.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private Text TextAt(Transform parent, string objectName, string value, float x, float y,
            float width, float height, int size, Color color)
        {
            RectTransform rect = At(parent, objectName, x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = game.appearance.font;
            text.fontSize = size;
            text.lineSpacing = 0.8f;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button ButtonAt(Transform parent, string objectName, string caption, float x, float y,
            float width, float height, Action clicked, bool primary = false)
        {
            RectTransform rect = At(parent, objectName, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? new Color32(140, 98, 61, 255) : new Color32(51, 69, 70, 255);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            button.colors = colors;
            Text label = TextAt(rect, "Label", caption, 4, 0, width - 8, height, height < 35 ? 15 : 18, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => { clicked(); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); });
            return button;
        }

        private Image Bar(Transform parent, string objectName, float x, float y, float width, float height, Color color)
        {
            RectTransform background = At(parent, objectName, x, y, width, height);
            var track = background.gameObject.AddComponent<Image>();
            track.color = new Color32(49, 61, 62, 255);
            track.raycastTarget = false;
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(background, false);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fill = fillObject.GetComponent<Image>();
            fill.color = color; fill.raycastTarget = false;
            return fill;
        }
    }
}
