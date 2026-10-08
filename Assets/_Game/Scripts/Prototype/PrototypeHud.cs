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
        private static readonly int ItemCount = Enum.GetValues(typeof(ItemId)).Length;
        private readonly Text[] carryCounts = new Text[ItemCount], warehouseCounts = new Text[ItemCount];
        private Text community;
        private readonly Button[] withdraw = new Button[ItemCount], drop = new Button[ItemCount];
        private Button buildDismantle, buildTable, buildGenerator, recruit, assign, pay;
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
            TextAt(header, "Subtitle", "拾取 → 发电 → 招募 → 居民生产 → 手动发薪", 20, 43, 450, 18, 15, Muted);
            clock = TextAt(header, "Clock", "", 645, 19, 200, 30, 21, Ink);
            pause = ButtonAt(header, "Pause", "暂停", 885, 14, 100, 39, () => game.Model.SetPaused(!game.Model.Paused));
            for (int index = 0; index < 3; index++)
            {
                int speed = index == 0 ? 1 : index == 1 ? 2 : 4;
                ButtonAt(header, "Speed" + speed, speed + "×", 995 + index * 70, 14, 60, 39, () => game.Model.SetSpeed(speed));
            }
            ButtonAt(header, "Restart", "重开", 1230, 14, 90, 39, game.ResetGame);
            TextAt(header, "PrototypeMark", "M2 · 起步切片", 1340, 23, 190, 24, 17, Accent);

            objective = TextAt(root, "Objective", "", 38, 106, 1130, 34, 23, Ink);
            target = TextAt(root, "PickupHint", "", 38, 144, 1120, 28, 17, Muted);

            RectTransform sidebar = StretchPanel(root, "WarehousePanel", new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-384, 22), new Vector2(-24, -106), new Color32(30, 43, 46, 255));
            sidebar = ScrollContent(sidebar, 1750);
            TextAt(sidebar, "CommunityTitle", "建设 / 居民", 20, 15, 320, 30, 24, Ink);
            community = TextAt(sidebar, "Community", "", 20, 55, 320, 100, 16, Muted);
            buildDismantle = ButtonAt(sidebar, "BuildDismantle", "拆解台 4木+2件", 20, 162, 153, 36, () => game.Report(game.Model.BuildDismantle()));
            buildTable = ButtonAt(sidebar, "BuildTable", "工作台 4板+2钢", 187, 162, 153, 36, () => game.Report(game.Model.BuildTable()));
            buildGenerator = ButtonAt(sidebar, "BuildGenerator", "组装发电机", 20, 208, 153, 36, () => game.Report(game.Model.BuildGenerator()), true);
            recruit = ButtonAt(sidebar, "Recruit", "招募1人", 187, 208, 153, 36, () => game.Report(game.Model.Recruit()));
            assign = ButtonAt(sidebar, "Assign", "安排居民木工", 20, 254, 153, 36, () => game.Report(game.Model.AssignWoodworker()));
            pay = ButtonAt(sidebar, "Pay", "手动支付工资", 187, 254, 153, 36, () => game.Report(game.Model.PayWages()), true);
            ButtonAt(sidebar, "FuelWood", "补木料", 20, 300, 100, 36, () => game.Report(game.Model.AddFuel(ItemId.WasteWood, int.MaxValue)));
            ButtonAt(sidebar, "FuelChips", "补碎屑", 130, 300, 100, 36, () => game.Report(game.Model.AddFuel(ItemId.WoodChips, int.MaxValue)));
            ButtonAt(sidebar, "Ash", "取灰渣", 240, 300, 100, 36, () => game.Report(game.Model.TakeAsh()));
            ButtonAt(sidebar, "Dismantle", "开工 / 停止拆解", 20, 346, 320, 36, () => game.Report(game.Model.ToggleDismantle()));
            TextAt(sidebar, "GeneratorCost", "发电机：6钢+热动力+发电组件+2电线\n先建工作台；背包燃料需送到机旁。", 20, 393, 320, 45, 15, Muted);
            TextAt(sidebar, "InventoryTitle", "仓储 / 选择附近物品", 20, 456, 320, 30, 22, Ink);
            capacity = TextAt(sidebar, "Capacity", "", 20, 495, 320, 27, 16, Muted);
            capacityFill = Bar(sidebar, "CapacityBar", 20, 532, 320, 8, Green);
            TextAt(sidebar, "Headings", "物品           背包  仓库    拾 / 取 / 放", 20, 548, 320, 22, 14, Muted);
            for (int i = 0; i < ItemCount; i++)
            {
                ItemId item = (ItemId)i;
                float y = 580 + i * 35;
                RectTransform icon = At(sidebar, "Icon" + i, 18, y + 3, 24, 24);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.sprite = game.appearance.ItemSprite(item);
                iconImage.raycastTarget = false;
                TextAt(sidebar, "Item" + i, PrototypeGame.ItemName(item), 50, y + 2, 105, 27, 14, Ink);
                carryCounts[i] = TextAt(sidebar, "Carry" + i, "", 156, y + 3, 32, 26, 15, Accent);
                warehouseCounts[i] = TextAt(sidebar, "Stored" + i, "", 196, y + 3, 32, 26, 15, Ink);
                ButtonAt(sidebar, "PickItem" + i, "拾", 237, y, 30, 30, () => game.Report(game.Model.PickUp(int.MaxValue, item)));
                withdraw[i] = ButtonAt(sidebar, "Withdraw" + i, "取", 273, y, 30, 30, () => game.Report(game.Model.Withdraw(item)));
                drop[i] = ButtonAt(sidebar, "Drop" + i, "放", 309, y, 30, 30, () => game.Report(game.Model.Drop(item)));
            }
            float footer = 580 + ItemCount * 35;
            TextAt(sidebar, "InventoryHelp", "拾：附近同类整堆；取：仓库1件。", 20, footer + 5, 320, 25, 14, Muted);
            sellPlank = ButtonAt(sidebar, "SellPlank", "售 1 木板", 20, footer + 44, 153, 40,
                () => game.Report(game.Model.Sell(ItemId.Plank, 1)));
            sellHandle = ButtonAt(sidebar, "SellHandle", "售 1 木柄", 187, footer + 44, 153, 40,
                () => game.Report(game.Model.Sell(ItemId.WoodHandle, 1)));
            sellAll = ButtonAt(sidebar, "SellAll", "出售全部木制品", 20, footer + 94, 320, 40, game.SellAll, true);
            money = TextAt(sidebar, "Money", "", 20, footer + 148, 320, 30, 20, Accent);
            TextAt(sidebar, "ScopeHint", "本轮：起步、发电、招募、木工与工资。\n生活消费、实体物流和电网待接入。", 20, footer + 191, 320, 48, 14, Muted);

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
            for (int i = 0; i < ItemCount; i++)
            {
                ItemId item = (ItemId)i;
                carryCounts[i].text = model.Carry.Count(item).ToString();
                warehouseCounts[i].text = model.Warehouse.Count(item).ToString();
                withdraw[i].interactable = !model.Paused && model.NearWarehouse && model.Carry.AvailableSlots(item) > 0 && model.Warehouse.Count(item) > 0;
                drop[i].interactable = !model.Paused && model.Carry.Count(item) > 0;
            }
            money.text = $"经营现金 {model.Money:0.00} / 加工{model.CompletedBatches}批";
            community.text = $"经营现金 {model.Money:0.00} · 电力{model.AvailablePower:0.#}\n燃料余{model.FuelSeconds:0}秒 · 居民{model.Residents.Count}/5\n居民钱包 {Wallets(model)}\n发薪 {model.SecondsToWage:0}秒 · 欠薪{model.UnpaidWages:0.00}\n拆解：{PrototypeGame.StateName(model.DismantleState)}";
            buildDismantle.interactable = !model.DismantleBuilt;
            buildTable.interactable = !model.TableBuilt;
            buildGenerator.interactable = model.TableBuilt && !model.GeneratorBuilt;
            recruit.interactable = model.HasBeenPowered && model.Residents.Count < model.Settings.firstResidents;
            assign.interactable = model.Residents.Count > 0;
            assign.GetComponentInChildren<Text>().text = model.Woodworker == null ? "安排居民木工" : "释放木工居民";
            pay.interactable = model.UnpaidWageCents > 0 && model.CashCents > 0;
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
            recipeInfo.text = $"{model.WoodInput(model.SelectedRecipe)} 废木料 → {model.ProductAmount(model.SelectedRecipe)} {recipeName} + 碎屑1\n基础 {model.Duration(model.SelectedRecipe):0.#} 秒 / 批";
            pickup.interactable = !model.Paused && model.NearestPickup() != null && model.Carry.Free > 0;
            deposit.interactable = !model.Paused && model.NearWarehouse && model.Warehouse.Free > 0;
            sellPlank.interactable = model.Warehouse.Count(ItemId.Plank) > 0;
            sellHandle.interactable = model.Warehouse.Count(ItemId.WoodHandle) > 0;
            sellAll.interactable = sellPlank.interactable || sellHandle.interactable;
            sellPlank.GetComponentInChildren<Text>().text = $"售木板 约{model.SalePrice(ItemId.Plank):0.00}";
            sellHandle.GetComponentInChildren<Text>().text = $"售木柄 约{model.SalePrice(ItemId.WoodHandle):0.00}";
            GroundPile nearest = model.NearestPickup();
            target.text = "WASD / 方向键移动 · E 拾取 · Shift+E 整堆 · Q 入库 · F 开工 · 空格暂停";
            if (nearest != null) target.text += $"    附近：{PrototypeGame.ItemName(nearest.Item)} ×{nearest.Amount}";
            target.text += " · 重叠堆可用右侧“拾”选类型";
            objective.text = Objective(model);
        }

        private static string Wallets(PrototypeSimulation model)
        {
            string result = "";
            foreach (Resident resident in model.Residents) result += $"{resident.Id}:{resident.Cash:0.##} ";
            return result.Length == 0 ? "暂无居民" : result.TrimEnd();
        }
        private static string Objective(PrototypeSimulation model)
        {
            if (!model.HasSaw) return "01 / 拾取手锯，把4份木料与2件紧固件送入仓库，搭建木工台。";
            if (!model.BenchBuilt) return "02 / 木工台用4木料+2紧固件；材料需入库。右侧“拾”可选择附近物品。";
            if (!model.TableBuilt) return "03 / 锯出至少6份木板，停工后建工作台；也可搭拆解台试拆损坏电机。";
            if (!model.GeneratorBuilt) return "04 / 收集6钢铁零件、热动力组件、发电组件和2电线，组装发电机。";
            if (!model.HasBeenPowered) return "05 / 把背包里的木料或碎屑送到发电机旁，点击补料，获得居民认可。";
            if (model.Residents.Count == 0) return "06 / 招募首位居民，600秒后工资到期；招募免费，工资每人10钱。";
            if (model.Woodworker == null) return "07 / 指定居民做木工，开启木工；居民到岗后主角可以离开。";
            if (model.UnpaidWageCents > 0) return $"08 / 工资已到期，共欠{model.UnpaidWages:0.00}钱；出售木制品后手动支付。";
            if (model.TotalWagesPaidCents > 0) return "起步流程已完成：居民持续木工、经营现金发工资。继续整理库存并补充燃料。";
            return $"08 / 木工持续生产，出售木制品；距首次工资还有{model.SecondsToWage:0}秒。";
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
