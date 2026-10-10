using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public PrototypeGame game;
        private static readonly Color Ink = new Color32(235, 234, 216, 255), Muted = new Color32(157, 178, 174, 255);
        private static readonly Color Accent = new Color32(236, 182, 104, 255), Green = new Color32(151, 198, 161, 255);
        private static readonly Color Panel = new Color32(29, 45, 48, 255), ButtonColor = new Color32(47, 68, 70, 255);
        private static readonly int ItemCount = Enum.GetValues(typeof(ItemId)).Length;
        [SerializeField] private Text clock, objective, capacity, money, pressure, workState, message, target, recipeInfo, community, residentsInfo, inventoryEmpty;
        [SerializeField] private Text[] carryCounts = new Text[ItemCount], warehouseCounts = new Text[ItemCount];
        [SerializeField] private RectTransform[] inventoryRows = new RectTransform[ItemCount], pages = new RectTransform[3];
        [SerializeField] private Button pause, build, work, plankRecipe, handleRecipe, sortedRecipe, inventoryFilter;
        [SerializeField] private Button[] tabs = new Button[3];
        [SerializeField] private Button[] speedButtons = new Button[3];
        [Serializable] private sealed class ButtonBinding { public Button button; public string command; }
        [SerializeField] private List<ButtonBinding> buttonBindings = new List<ButtonBinding>();
        [SerializeField] private Image capacityFill, progressFill;
        [SerializeField] private RectTransform root, inventoryContent;
        private bool showAllItems;
        public bool HasLayout => root != null && clock != null;
        public RectTransform VisualRoot => root;
        private static readonly string[] CommandLabels = {
            "建拆解台", "建工作台", "组装发电机", "招募1人", "指定 / 释放木工", "支付工资",
            "补木料", "补碎屑", "开停拆解", "切换拆解模板", "投木料", "取木工产物",
            "交接手锯", "交接螺丝刀", "投待拆设备", "取拆解产物", "取消木工批次", "取消拆解批次",
            "取消施工蓝图", "出售木制品", "买手锯 · 16", "买螺丝刀 · 10", "卖木料技术 · 60", "卖发电机", "取回未预约木料", "取灰渣"
        };

        private void Start()
        {
            BuildLayout();
            // Bind after scene loading: authoring UI must also work in a standalone player.
            foreach (ButtonBinding binding in buttonBindings)
            {
                if (binding.button == null) continue;
                string command = binding.command;
                binding.button.onClick.AddListener(() => { Execute(command); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); });
            }
            SelectTab(0);
            RefreshView(game.Model);
        }

        public void BuildLayout()
        {
            if (HasLayout) return;
            buttonBindings.Clear();
            var canvas = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false); root = canvas.GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0;
            if (GetComponentInChildren<EventSystem>(true) == null && EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
            var header = StretchPanel(root, "Header", new Vector2(0, 1), Vector2.one, new Vector2(24, -94), new Vector2(-24, -20), Panel);
            Stripe(header, "Brand Accent", 0, 0, 4, 74, Accent);
            TextAt(header, "Title", "涌现 / 废料工坊", 20, 7, 360, 36, 29, Ink);
            TextAt(header, "Subtitle", "拾起废料，建起你的工坊。", 22, 43, 350, 22, 15, Muted);
            clock = TextAt(header, "Clock", "00:00 · 1×", 394, 20, 150, 32, 21, Ink);
            TextAt(header, "Cash Caption", "经营现金", 568, 9, 155, 20, 14, Muted);
            money = TextAt(header, "Money", "0.00", 568, 30, 175, 32, 25, Accent);
            TextAt(header, "Area", "首区 / 回收工坊", 760, 23, 180, 28, 17, Green);
            pause = ButtonAt(header, "Pause", "暂停", 946, 19, 74, 38);
            for (int i = 1; i <= 3; i++) speedButtons[i - 1] = ButtonAt(header, "Speed" + i, i + "×", 1030 + (i - 1) * 50, 19, 44, 38);
            ButtonAt(header, "Save", "保存", 1186, 19, 104, 38); ButtonAt(header, "Load", "读取", 1300, 19, 104, 38);
            ButtonAt(header, "Restart", "重开", 1414, 19, 112, 38);
            objective = TextAt(root, "Objective", "先拾取手锯，再收集木料和连接件。", 38, 108, 1110, 34, 21, Ink);
            target = TextAt(root, "Hint", "", 38, 146, 1110, 23, 14, Muted);

            var sidebar = StretchPanel(root, "Management", new Vector2(1, 0), Vector2.one, new Vector2(-420, 22), new Vector2(-24, -110), Panel);
            Stripe(sidebar, "Management Accent", 0, 0, 396, 3, Green);
            TextAt(sidebar, "Title", "工坊管理", 20, 18, 355, 32, 25, Ink);
            community = TextAt(sidebar, "Community", "", 20, 61, 355, 96, 16, Muted);
            string[] tabNames = { "现场", "人员", "仓库" };
            for (int i = 0; i < 3; i++) tabs[i] = ButtonAt(sidebar, "Tab" + i, tabNames[i], 20 + i * 121, 173, 113, 40);
            for (int i = 0; i < pages.Length; i++) pages[i] = StretchPanel(sidebar, tabNames[i] + " Page", Vector2.zero, Vector2.one, new Vector2(8, 14), new Vector2(-8, -228), Color.clear);
            var onsite = ScrollContent(pages[0], 920);
            float y = CommandGroup(onsite, "建设", 10, new[] { 0, 1, 2 });
            y = CommandGroup(onsite, "现场投料与搬运", y, new[] { 10, 11, 12, 13, 14, 15, 6, 7, 24, 25 });
            y = CommandGroup(onsite, "拆解与批次", y, new[] { 8, 9, 16, 17, 18 });
            y = CommandGroup(onsite, "采购与交易", y, new[] { 19, 20, 21, 22, 23 });
            TextAt(onsite, "Onsite Help", "投料、取物和交接工具，需要主角到场。", 12, y + 4, 352, 48, 15, Muted);
            onsite.sizeDelta = new Vector2(0, y + 72);
            var people = ScrollContent(pages[1], 900);
            float peopleY = CommandGroup(people, "居民与工资", 10, new[] { 3, 4, 5 });
            residentsInfo = TextAt(people, "Residents", "暂无居民", 12, peopleY + 8, 352, 420, 17, Ink);
            residentsInfo.alignment = TextAnchor.UpperLeft;
            TextAt(people, "People Help", "先让发电机实际发电，再招募居民。\n让居民木工前，把手锯交接到现场工具槽。", 12, peopleY + 448, 352, 84, 15, Muted);

            inventoryContent = ScrollContent(pages[2], 250 + ItemCount * 40);
            TextAt(inventoryContent, "Inventory Title", "背包与仓库", 12, 10, 200, 32, 21, Ink);
            inventoryFilter = ButtonAt(inventoryContent, "InventoryFilter", "全部物品", 244, 8, 110, 34);
            capacity = TextAt(inventoryContent, "Capacity", "", 12, 51, 352, 28, 15, Muted);
            capacityFill = Bar(inventoryContent, "CapacityBar", 12, 88, 340, 6, Green);
            TextAt(inventoryContent, "Columns", "物品               背包  仓库    拾 / 取 / 放 / 卖", 12, 106, 352, 25, 13, Muted);
            inventoryEmpty = TextAt(inventoryContent, "Empty Inventory", "背包和仓库还是空的。\n走近来料区，按 E 拾取物品。\n点击“全部物品”可查看完整目录。", 12, 157, 350, 120, 17, Muted);
            inventoryEmpty.alignment = TextAnchor.UpperLeft;
            for (int i = 0; i < ItemCount; i++)
            {
                ItemId item = (ItemId)i;
                var row = At(inventoryContent, "ItemRow" + i, 8, 143 + i * 40, 356, 38); inventoryRows[i] = row;
                var background = row.gameObject.AddComponent<Image>(); background.raycastTarget = false;
                background.color = i % 2 == 0 ? new Color32(37, 54, 56, 255) : new Color32(32, 49, 51, 255);
                var icon = At(row, "Icon", 5, 7, 24, 24).gameObject.AddComponent<Image>(); icon.sprite = game.appearance.ItemSprite(item); icon.raycastTarget = false;
                TextAt(row, "Item", PrototypeGame.ItemName(item), 35, 0, 107, 38, 14, Ink);
                carryCounts[i] = TextAt(row, "Carry", "0", 144, 0, 30, 38, 14, Accent);
                warehouseCounts[i] = TextAt(row, "Stock", "0", 180, 0, 48, 38, 14, Ink);
                ButtonAt(row, "Pick" + i, "拾", 229, 5, 28, 28); ButtonAt(row, "Take" + i, "取", 260, 5, 28, 28);
                ButtonAt(row, "Drop" + i, "放", 291, 5, 28, 28); ButtonAt(row, "Sell" + i, "卖", 322, 5, 28, 28);
            }
            var actions = StretchPanel(root, "Woodwork", Vector2.zero, new Vector2(0.725f, 0), new Vector2(24, 22), new Vector2(0, 166), Panel);
            Stripe(actions, "Woodwork Accent", 0, 0, 1136, 3, Accent);
            ButtonAt(actions, "Interact", "交互  E", 18, 17, 106, 38); ButtonAt(actions, "Deposit", "入库  Q", 134, 17, 106, 38);
            build = ButtonAt(actions, "Build", "搭建木工台", 250, 17, 152, 38, true);
            plankRecipe = ButtonAt(actions, "Plank", "木板", 418, 17, 77, 38); handleRecipe = ButtonAt(actions, "Handle", "木柄", 503, 17, 77, 38);
            sortedRecipe = ButtonAt(actions, "Sorted", "整理", 588, 17, 77, 38); work = ButtonAt(actions, "Work", "木工  F", 681, 17, 125, 38, true);
            pressure = TextAt(actions, "Pressure", "", 824, 13, 294, 46, 16, Green);
            workState = TextAt(actions, "State", "等待搭建", 18, 65, 380, 34, 17, Ink);
            progressFill = Bar(actions, "Progress", 418, 80, 388, 7, Accent);
            recipeInfo = TextAt(actions, "Recipe", "", 824, 63, 294, 45, 14, Muted);
            message = TextAt(actions, "Notice", "", 18, 110, 1098, 28, 16, Muted);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            SelectTab(0);
        }

        private void Update() { if (HasLayout && game.Model != null) RefreshView(game.Model); }

        public void RefreshView(PrototypeSimulation m)
        {
            if (!HasLayout) return;
            clock.text = $"{(int)m.GameSeconds / 60:00}:{(int)m.GameSeconds % 60:00} · {(m.Paused ? "暂停" : m.Speed + "×")}";
            pause.GetComponentInChildren<Text>().text = m.Paused ? "继续" : "暂停";
            capacity.text = $"仓库 {m.Warehouse.Used:0.##}/{m.Warehouse.Capacity}    背包 {m.Carry.Used:0.##}/{m.Carry.Capacity}";
            capacityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)m.Warehouse.Used / m.Warehouse.Capacity), 1);
            community.text = $"电力 {m.AvailablePower:0.#}    居民 {m.Residents.Count}/5    已配送 {m.DeliveredTrips} 趟\n" +
                (m.Residents.Count == 0 ? "工资：招募后开始计时" : $"下次发薪 {m.SecondsToWage:0} 秒    欠薪 {m.UnpaidWages:0.00}") +
                $"\n拆解：{ItemCatalog.Name(m.SelectedDevice)}\n{PrototypeGame.StateName(m.DismantleState)}";
            money.text = m.Money.ToString("0.00");
            pressure.text = $"人工效率 {m.HumanEfficiency:P0}\n现场木料 {m.WoodInputStock.Count(ItemId.WasteWood)} / 预约 {m.WoodInputStock.Reserved(ItemId.WasteWood)}";
            workState.text = PrototypeGame.StateName(m.State) + (m.JobActive ? $" · {m.Progress:P0}" : "");
            progressFill.rectTransform.anchorMax = new Vector2(m.Progress, 1);
            recipeInfo.text = $"{m.WoodInput(m.SelectedRecipe)}木料 → {m.ProductAmount(m.SelectedRecipe)}{ItemCatalog.Name(m.Product(m.SelectedRecipe))}\n基础 {m.Duration(m.SelectedRecipe)} 秒 · 成品需搬运";
            message.text = m.Interacting ? "交互进行中，移动或 Esc 可取消。" : game.Notice; message.color = game.LastActionSucceeded ? Muted : Accent;
            build.interactable = !m.BenchBuilt && !m.BuildPending;
            SetSelected(plankRecipe, m.SelectedRecipe == WoodRecipe.Plank); SetSelected(handleRecipe, m.SelectedRecipe == WoodRecipe.WoodHandle);
            SetSelected(sortedRecipe, m.SelectedRecipe == WoodRecipe.SortedWood);
            for (int i = 1; i <= 3; i++) SetSelected(speedButtons[i - 1], !m.Paused && m.Speed == i);
            target.text = "WASD 移动 · E 交互 · Shift+E 整堆拾取 · Q 入库 · 滚轮缩放 · 中键拖动";
            var nearest = m.NearestPickup(); if (nearest != null) target.text += "    附近：" + ItemCatalog.Name(nearest.Item);
            if (!m.BenchBuilt) objective.text = "先拾取手锯，收集木料与连接件，搭建第一张木工台。";
            else if (!m.HasBeenPowered) objective.text = "到木工台前投料并加工；取出成品，收集组件并让发电机运转。";
            else if (m.Residents.Count == 0) objective.text = "工坊已有电力，招募居民来帮忙。首人加入 600 秒后需要发薪。";
            else if (m.WoodTools.Count(ItemId.WoodHandSaw) == 0) objective.text = "把手锯交接到木工台工具槽，再指定居民持续生产。";
            else if (m.UnpaidWages > 0) objective.text = $"有 {m.UnpaidWages:0.00} 工资待支付。出售仓库中的成品后，在“人员”页发薪。";
            else objective.text = "工坊正在运转。保持原料供应与仓库空间，留意工具耐久。";
            var low = m.Tools.FirstOrDefault(t => ItemCatalog.IsTool(t.Item) && t.Durability > 0 && t.Durability <= 60 && !m.Tools.Any(other => other.Item == t.Item && other.Id != t.Id && other.Durability > 60));
            if (low != null) objective.text = "工具预警：" + ItemCatalog.Name(low.Item) + "耐久低于 20%，准备替补并送到岗位。";
            residentsInfo.text = m.Residents.Count == 0 ? "暂无居民\n\n发电取得认可后，可招募首批居民。\n居民会实际搬运原料和成品。" :
                string.Join("\n\n", m.Residents.Select(r => $"居民 {r.Id:00}    钱包 {r.Cash:0.00}\n{r.Activity} · {(r.Job == WorkerJob.Woodwork ? "木工" : "配送 / 空闲")}"));
            RefreshInventory(m);
        }

        private void RefreshInventory(PrototypeSimulation m)
        {
            int visible = 0;
            for (int i = 0; i < ItemCount; i++)
            {
                ItemId item = (ItemId)i; int carry = m.Carry.Count(item), stock = m.Warehouse.Count(item), reserved = m.Warehouse.Reserved(item);
                carryCounts[i].text = carry.ToString(); warehouseCounts[i].text = stock + (reserved > 0 ? "/" + reserved : "");
                bool show = showAllItems || carry > 0 || stock > 0; inventoryRows[i].gameObject.SetActive(show);
                if (show) inventoryRows[i].anchoredPosition = new Vector2(8, -(143 + visible++ * 40));
            }
            inventoryEmpty.gameObject.SetActive(visible == 0);
            inventoryContent.sizeDelta = new Vector2(0, Mathf.Max(300, 163 + visible * 40));
            inventoryFilter.GetComponentInChildren<Text>().text = showAllItems ? "仅持有物品" : "全部物品";
        }

        private void Execute(string command)
        {
            var m = game.Model;
            if (command.StartsWith("Command") && int.TryParse(command.Substring(7), out int index))
            {
                Action[] commands = {
                    () => game.Report(m.BuildDismantle()), () => game.Report(m.BuildTable()), () => game.Report(m.BuildGenerator()),
                    () => game.Report(m.Recruit()), () => game.Report(m.AssignWoodworker()), () => game.Report(m.PayWages()),
                    () => game.Report(m.AddFuel(ItemId.WasteWood, int.MaxValue)), () => game.Report(m.AddFuel(ItemId.WoodChips, int.MaxValue)),
                    () => game.Report(m.ToggleDismantle()), CycleDevice, () => game.Report(m.DeliverInput()), () => game.Report(m.TakeOutput()),
                    () => game.Report(m.HandOffTool(ItemId.WoodHandSaw)), () => game.Report(m.HandOffTool(ItemId.Screwdriver)),
                    () => game.Report(m.DeliverInput(true)), () => game.Report(m.TakeOutput(true)),
                    () => game.Report(m.CancelBatch()), () => game.Report(m.CancelBatch(true)), () => game.Report(m.CancelBlueprints()), game.SellAll,
                    () => game.Report(m.Buy(ItemId.WoodHandSaw)), () => game.Report(m.Buy(ItemId.Screwdriver)),
                    () => game.Report(m.SellTechnology(Technology.Wood)), () => game.Report(m.RemoveFacility(8, true)),
                    () => game.Report(m.TakeInput()), () => game.Report(m.TakeAsh())
                };
                if (index >= 0 && index < commands.Length) commands[index](); return;
            }
            if (command.StartsWith("Tab") && int.TryParse(command.Substring(3), out int tab)) { SelectTab(tab); return; }
            if (command.StartsWith("Speed") && int.TryParse(command.Substring(5), out int speed)) { m.SetSpeed(speed); return; }
            foreach (string action in new[] { "Pick", "Take", "Drop", "Sell" })
            {
                if (!command.StartsWith(action) || !int.TryParse(command.Substring(action.Length), out int itemIndex)) continue;
                var item = (ItemId)itemIndex;
                if (action == "Pick") game.Report(m.PickUp(int.MaxValue, item)); if (action == "Take") game.Report(m.Withdraw(item));
                if (action == "Drop") game.Report(m.Drop(item)); if (action == "Sell") game.Report(m.Sell(item, 1)); return;
            }
            switch (command)
            {
                case "Pause": m.SetPaused(!m.Paused); break; case "Save": game.SaveGame(); break; case "Load": game.LoadLatest(); break;
                case "Restart": game.ResetGame(); break; case "Interact": game.Interact(); break; case "Deposit": game.Report(m.Deposit()); break;
                case "Build": game.Report(m.BuildBench()); break; case "Plank": game.Report(m.SelectRecipe(WoodRecipe.Plank)); break;
                case "Handle": game.Report(m.SelectRecipe(WoodRecipe.WoodHandle)); break; case "Sorted": game.Report(m.SelectRecipe(WoodRecipe.SortedWood)); break;
                case "Work": game.Report(m.ToggleWork()); break; case "InventoryFilter": showAllItems = !showAllItems; RefreshInventory(m); break;
            }
        }

        private void SelectTab(int index)
        {
            if (index < 0 || index >= pages.Length) return;
            for (int i = 0; i < pages.Length; i++) { pages[i].gameObject.SetActive(i == index); SetSelected(tabs[i], i == index); }
        }
        private static void SetSelected(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? new Color32(72, 101, 87, 255) : ButtonColor;
            button.GetComponentInChildren<Text>().color = selected ? Green : Ink;
        }
        private void CycleDevice()
        {
            var choices = new[] { ItemId.OldCutter, ItemId.OldWasher, ItemId.OldFormer, ItemId.OldGrowingLamp, ItemId.OldFeeder, ItemId.OldFurnace, ItemId.OldMetalTable, ItemId.OldControlBox, ItemId.DamagedMotor, ItemId.OldLamp };
            int index = Array.IndexOf(choices, game.Model.SelectedDevice); game.Report(game.Model.SelectDevice(choices[(index + 1) % choices.Length]));
        }
        private float CommandGroup(Transform parent, string caption, float y, int[] indices)
        {
            TextAt(parent, caption, caption, 12, y, 352, 30, 18, Green); y += 39;
            for (int i = 0; i < indices.Length; i++) ButtonAt(parent, "Command" + indices[i], CommandLabels[indices[i]], 12 + (i % 2) * 178, y + (i / 2) * 48, 168, 40);
            return y + ((indices.Length + 1) / 2) * 48 + 20;
        }
        private RectTransform At(Transform parent, string objectName, float x, float y, float width, float height)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private void Stripe(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = At(parent, name, x, y, width, height).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }
        private RectTransform ScrollContent(RectTransform panel, float height)
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewportRect = viewport.GetComponent<RectTransform>(); viewportRect.SetParent(panel, false);
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one; viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;
            var contentRect = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1); contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero; contentRect.sizeDelta = new Vector2(0, height);
            var scroll = panel.gameObject.AddComponent<ScrollRect>(); scroll.content = contentRect; scroll.viewport = viewportRect;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35; return contentRect;
        }
        private RectTransform StretchPanel(Transform parent, string objectName, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = new GameObject(objectName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            rect.GetComponent<Image>().color = color; rect.GetComponent<Image>().raycastTarget = false; return rect;
        }
        private Text TextAt(Transform parent, string objectName, string value, float x, float y, float width, float height, int size, Color color)
        {
            var text = At(parent, objectName, x, y, width, height).gameObject.AddComponent<Text>(); text.font = game.appearance.font;
            text.fontSize = size; text.lineSpacing = 1; text.color = color; text.text = value; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; return text;
        }
        private Button ButtonAt(Transform parent, string objectName, string caption, float x, float y, float width, float height, bool primary = false)
        {
            var rect = At(parent, objectName, x, y, width, height); var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? new Color32(153, 109, 63, 255) : ButtonColor;
            var border = rect.gameObject.AddComponent<Outline>(); border.effectColor = primary ? new Color32(224, 170, 98, 90) : new Color32(149, 180, 167, 38);
            border.effectDistance = new Vector2(1, -1); border.useGraphicAlpha = false;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f); colors.pressedColor = new Color(0.78f, 0.85f, 0.82f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); button.colors = colors;
            buttonBindings.Add(new ButtonBinding { button = button, command = objectName });
            var label = TextAt(rect, "Label", caption, 4, 0, width - 8, height, height < 34 ? 14 : 16, Ink); label.alignment = TextAnchor.MiddleCenter; return button;
        }
        private Image Bar(Transform parent, string objectName, float x, float y, float width, float height, Color color)
        {
            var background = At(parent, objectName, x, y, width, height); var track = background.gameObject.AddComponent<Image>();
            track.color = new Color32(49, 65, 65, 255); track.raycastTarget = false;
            var fillRect = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); fillRect.SetParent(background, false);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fill = fillRect.GetComponent<Image>(); fill.color = color; fill.raycastTarget = false; return fill;
        }
    }
}
