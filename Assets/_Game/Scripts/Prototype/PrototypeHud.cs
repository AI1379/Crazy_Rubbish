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
        [SerializeField] private RectTransform[] inventoryRows = new RectTransform[ItemCount], pages = new RectTransform[4];
        [SerializeField] private Button pause, build, work, plankRecipe, handleRecipe, sortedRecipe, inventoryFilter;
        [SerializeField] private Button[] tabs = new Button[4];
        [SerializeField] private Button[] speedButtons = new Button[3];
        [Serializable] private sealed class ButtonBinding { public Button button; public string command; }
        [SerializeField] private List<ButtonBinding> buttonBindings = new List<ButtonBinding>();
        [SerializeField] private Image capacityFill, progressFill;
        [SerializeField] private RectTransform root, inventoryContent;
        [SerializeField] private int layoutVersion;
        [SerializeField] private RectTransform modal;
        [SerializeField] private Text modalTitle, modalInfo, goals, lifeInfo, assemblyInfo;
        [SerializeField] private Button foodPolicy, waterPolicy, externalSupply, emergencyWater, autoTools, menuResume;
        [SerializeField] private RectTransform tradeConfirmation;
        [SerializeField] private Text tradeInfo;
        private int pendingTechnology=-1;
        [SerializeField] private Text tutorialProgress, tutorialTitle, tutorialLesson, tutorialTip;
        [SerializeField] private Button tutorialAction, tutorialLocate, tutorialStart;
        [SerializeField] private Button tutorialSkip, tutorialPrevious, tutorialNext;
        [SerializeField] private RectTransform tutorialContent;
        [SerializeField] private Text tutorialOptional, tutorialHelp;
        private int reviewLesson=-1;
        private int displayedLesson=-1;
        private PrototypeSimulation tutorialModel;
        private bool showAllItems;
        private RectTransform inspection;
        private Text inspectionTitle, inspectionBody;
        public bool HasLayout => root != null && clock != null && layoutVersion==5;
        public RectTransform VisualRoot => root;
        public void ShowInspection(string title, string body, Vector2 screenPoint)
        {
            if (root == null) return;
            if (string.IsNullOrEmpty(title))
            {
                if (inspection != null) inspection.gameObject.SetActive(false);
                return;
            }
            if (inspection == null)
            {
                inspection = At(root, "Map Inspection", 0, 0, 330, 120);
                inspection.gameObject.AddComponent<Image>().color = new Color32(24, 38, 40, 250);
                Stripe(inspection, "Accent", 0, 0, 3, 120, Accent);
                inspectionTitle = TextAt(inspection, "Title", "", 14, 10, 300, 26, 17, Accent);
                inspectionBody = TextAt(inspection, "Contents", "", 14, 43, 300, 400, 15, Ink);
                foreach (Graphic graphic in inspection.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                foreach (Transform child in inspection.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            }
            inspection.gameObject.SetActive(true);
            inspection.SetAsLastSibling();
            inspectionTitle.text = title;
            inspectionBody.text = body;
            float height = inspectionBody.preferredHeight + 56;
            inspection.sizeDelta = new Vector2(330, height);
            inspectionBody.rectTransform.sizeDelta = new Vector2(300, height - 50);
            inspection.Find("Accent").GetComponent<RectTransform>().sizeDelta = new Vector2(3, height);
            float scale = root.GetComponent<Canvas>().scaleFactor;
            Rect map = game.GetComponent<PrototypeWorld>().worldCamera.pixelRect;
            float px = screenPoint.x + 18;
            if (px + 330 * scale > map.xMax) px = screenPoint.x - 330 * scale - 18;
            px = Mathf.Clamp(px, map.xMin + 8, Mathf.Max(map.xMin + 8, map.xMax - 330 * scale - 8));
            float py = Mathf.Clamp(screenPoint.y - 18, map.yMin + height * scale + 8, map.yMax - 8);
            inspection.anchoredPosition = new Vector2(px / scale, -(Screen.height - py) / scale);
        }
        private static readonly string[] CommandLabels = {
            "建拆解台", "建工作台", "组装发电机", "招募1人", "指定 / 释放木工", "支付工资",
            "补木料", "补碎屑", "开停拆解", "切换拆解模板", "投木料", "取木工产物",
            "交接手锯", "交接螺丝刀", "投待拆设备", "取拆解产物", "取消木工批次", "取消拆解批次",
            "取消施工蓝图", "出售木制品", "买手锯 · 16", "买螺丝刀 · 10", "卖木料技术 · 60", "卖发电机", "取回未预约木料", "取灰渣",
            "买食物 ×6 · 9", "买饮水 ×12 · 6", "买锯片 · 6", "木柄手锯", "木柄锤", "螺丝刀", "投装配材料", "工作台作业", "取工作台产物", "取消装配批次", "取回装配输入",
            "卖工具技术 · 80", "卖纺织技术 · 50", "卖金属技术 · 100", "买锤头 · 5", "买螺丝刀杆 · 4"
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
            SelectTab(game.Model.Tutorial.active?3:0);
            RefreshView(game.Model);
        }

        public void BuildLayout()
        {
            if (HasLayout) return;
            if(root!=null){if(Application.isPlaying)Destroy(root.gameObject);else DestroyImmediate(root.gameObject);}
            buttonBindings.Clear();
            layoutVersion=5;pages=new RectTransform[4];tabs=new Button[4];
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
            ButtonAt(header, "Menu", "目标 / 菜单", 760, 19, 174, 38);
            pause = ButtonAt(header, "Pause", "暂停", 946, 19, 74, 38);
            for (int i = 1; i <= 3; i++) speedButtons[i - 1] = ButtonAt(header, "Speed" + i, i + "×", 1030 + (i - 1) * 50, 19, 44, 38);
            ButtonAt(header, "Save", "保存", 1186, 19, 104, 38); ButtonAt(header, "Load", "读取", 1300, 19, 104, 38);
            ButtonAt(header, "Restart", "重开", 1414, 19, 112, 38);
            objective = TextAt(root, "Objective", "先拾取手锯，再收集木料和连接件。", 38, 108, 1110, 34, 18, Ink);
            target = TextAt(root, "Hint", "", 38, 146, 1110, 23, 14, Muted);

            var sidebar = StretchPanel(root, "Management", new Vector2(1, 0), Vector2.one, new Vector2(-420, 22), new Vector2(-24, -110), Panel);
            Stripe(sidebar, "Management Accent", 0, 0, 396, 3, Green);
            TextAt(sidebar, "Title", "工坊管理", 20, 18, 355, 32, 25, Ink);
            community = TextAt(sidebar, "Community", "", 20, 61, 355, 96, 16, Muted);
            string[] tabNames = { "现场", "人员", "仓库", "教程" };
            for (int i = 0; i < 4; i++) tabs[i] = ButtonAt(sidebar, "Tab" + i, tabNames[i], 20 + i * 90, 173, 84, 40);
            for (int i = 0; i < pages.Length; i++) pages[i] = StretchPanel(sidebar, tabNames[i] + " Page", Vector2.zero, Vector2.one, new Vector2(8, 14), new Vector2(-8, -228), Color.clear);
            var onsite = ScrollContent(pages[0], 920);
            float y = CommandGroup(onsite, "建设", 10, new[] { 0, 1, 2 });
            y = CommandGroup(onsite, "现场投料与搬运", y, new[] { 10, 11, 12, 13, 14, 15, 6, 7, 24, 25 });
            y = CommandGroup(onsite, "拆解与批次", y, new[] { 8, 9, 16, 17, 18 });
            y = CommandGroup(onsite, "工作台：握柄1 + 对应工作端1", y, new[] { 29,30,31,32,33,34,35,36 });
            assemblyInfo=TextAt(onsite,"Assembly Info","",12,y,352,72,15,Muted);y+=80;
            y = CommandGroup(onsite, "采购与交易", y, new[] { 19, 20, 21, 28, 40, 41, 22, 37, 38, 39, 23 });
            TextAt(onsite, "Onsite Help", "投料、取物和交接工具，需要主角到场。", 12, y + 4, 352, 48, 15, Muted);
            onsite.sizeDelta = new Vector2(0, y + 72);
            var people = ScrollContent(pages[1], 1550);
            float peopleY = CommandGroup(people, "居民与工资", 10, new[] { 3, 4, 5 });
            TextAt(people,"Supply Caption","生活供应 / 共用仓库",12,peopleY,352,32,21,Ink);peopleY+=42;
            foodPolicy=ButtonAt(people,"FoodPolicy","食物：收费",12,peopleY,166,38);
            waterPolicy=ButtonAt(people,"WaterPolicy","饮水：收费",186,peopleY,166,38);peopleY+=48;
            externalSupply=ButtonAt(people,"ExternalSupply","个人外购：开",12,peopleY,166,38);
            emergencyWater=ButtonAt(people,"EmergencyWater","紧急水：开",186,peopleY,166,38);peopleY+=48;
            autoTools=ButtonAt(people,"AutoTools","替补工具自动采购：关",12,peopleY,340,38);peopleY+=48;
            lifeInfo=TextAt(people,"Life Info","",12,peopleY,352,192,15,Muted);lifeInfo.alignment=TextAnchor.UpperLeft;peopleY+=200;
            peopleY=CommandGroup(people,"生活及工具采购",peopleY,new[]{26,27,28});
            residentsInfo = TextAt(people, "Residents", "暂无居民", 12, peopleY + 8, 352, 510, 16, Ink);
            residentsInfo.alignment = TextAnchor.UpperLeft;
            TextAt(people, "People Help", "食物 / 水每300秒实际消耗，首期发薪前免费。\n居民外购用个人钱包；免费供给仍扣实物。\n工具自动采购优先保护欠薪和下期工资。", 12, peopleY + 530, 352, 100, 15, Muted);
            people.sizeDelta=new Vector2(0,peopleY+650);
            BuildTutorialPage();

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
            BuildMenu();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            SelectTab(0);
        }

        private void BuildMenu()
        {
            modal=StretchPanel(root,"Demo Menu",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0.04f,0.08f,0.09f,0.94f));
            var card=At(modal,"Card",0,0,920,680);card.anchorMin=card.anchorMax=new Vector2(0.5f,0.5f);card.pivot=new Vector2(0.5f,0.5f);card.anchoredPosition=Vector2.zero;
            card.gameObject.AddComponent<Image>().color=Panel;
            Stripe(card,"Accent",0,0,920,4,Accent);
            modalTitle=TextAt(card,"Title","涌现 / 首区 Demo",36,24,848,48,34,Ink);
            modalInfo=TextAt(card,"Info","",36,82,848,82,18,Muted);modalInfo.alignment=TextAnchor.UpperLeft;
            goals=TextAt(card,"Goals","",36,184,848,330,20,Ink);goals.alignment=TextAnchor.UpperLeft;
            menuResume=ButtonAt(card,"Resume","开始试玩",36,530,190,46,true);
            ButtonAt(card,"MenuLoad","继续存档",242,530,190,46);ButtonAt(card,"MenuSave","保存进度",448,530,190,46);
            ButtonAt(card,"MenuNew","新游戏",654,530,230,46);
            TextAt(card,"Controls","WASD / 点击地图行走 · E交互 · Shift+E整堆 · Q入库 · F木工\n空格暂停 · 1/2/3倍速 · 滚轮缩放 · Esc菜单 / 取消行动",36,592,700,66,16,Muted);
            ButtonAt(card,"Quit","退出",774,605,110,38);
            modal.gameObject.SetActive(false);
            tradeConfirmation=At(root,"Trade Confirmation",0,0,720,252);tradeConfirmation.anchorMin=tradeConfirmation.anchorMax=new Vector2(0.5f,0.5f);tradeConfirmation.pivot=new Vector2(0.5f,0.5f);tradeConfirmation.anchoredPosition=Vector2.zero;
            tradeConfirmation.gameObject.AddComponent<Image>().color=Panel;
            Stripe(tradeConfirmation,"Accent",0,0,720,4,Accent);
            tradeInfo=TextAt(tradeConfirmation,"Info","",26,22,668,152,20,Ink);tradeInfo.alignment=TextAnchor.UpperLeft;
            ButtonAt(tradeConfirmation,"ConfirmTrade","确认出售",26,184,230,42,true);ButtonAt(tradeConfirmation,"CancelTrade","取消",274,184,230,42);
            tradeConfirmation.gameObject.SetActive(false);
        }
        private void Update() { if (HasLayout && game.Model != null) RefreshView(game.Model); }

        private void BuildTutorialPage()
        {
            var content=ScrollContent(pages[3],810);tutorialContent=content;
            TextAt(content,"Tutorial Caption","新手教程 / 边玩边学",12,10,352,32,21,Green);
            tutorialProgress=TextAt(content,"Tutorial Progress","",12,48,352,24,15,Muted);
            tutorialTitle=TextAt(content,"Tutorial Title","",12,82,352,58,22,Ink);tutorialTitle.alignment=TextAnchor.UpperLeft;
            tutorialLesson=TextAt(content,"Tutorial Lesson","",12,148,352,160,16,Ink);tutorialLesson.alignment=TextAnchor.UpperLeft;
            tutorialTip=TextAt(content,"Tutorial Tip","",12,324,352,100,15,Accent);tutorialTip.alignment=TextAnchor.UpperLeft;
            tutorialAction=ButtonAt(content,"TutorialAction","开始教程",12,436,164,42,true);
            tutorialSkip=ButtonAt(content,"TutorialSkip","跳过 / 收起",188,436,164,42);
            tutorialLocate=ButtonAt(content,"TutorialLocate","显示对应按钮",12,488,340,40);
            tutorialOptional=TextAt(content,"Tutorial Optional","可以使用普通按钮；无需一直留在教程页。",12,540,340,36,14,Muted);
            tutorialPrevious=ButtonAt(content,"TutorialPrevious","上一课",12,598,164,38);tutorialNext=ButtonAt(content,"TutorialNext","下一课",188,598,164,38);
            tutorialStart=ButtonAt(content,"TutorialStart","继续当前教程",12,646,340,40);
            tutorialHelp=TextAt(content,"Tutorial Help","课程按实际操作完成，可自由使用键盘和普通按钮。\n翻阅课程不重开游戏；教程进度随存档保存。\n空格暂停 · 1/2/3倍速 · 滚轮缩放",12,704,352,90,14,Muted);
        }

        public void RefreshView(PrototypeSimulation m)
        {
            if (!HasLayout) return;
            if (inspection != null && (game.MenuOpen || game.ResultVisible)) inspection.gameObject.SetActive(false);
            clock.text = $"{(int)m.GameSeconds / 60:00}:{(int)m.GameSeconds % 60:00} · {(m.Paused ? "暂停" : m.Speed + "×")}";
            pause.GetComponentInChildren<Text>().text = m.Paused ? "继续" : "暂停";
            capacity.text = $"仓库 {m.Warehouse.Used:0.##}/{m.Warehouse.Capacity}    背包 {m.Carry.Used:0.##}/{m.Carry.Capacity}";
            capacityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)m.Warehouse.Used / m.Warehouse.Capacity), 1);
            community.text = $"电力 {m.AvailablePower:0.#}    居民 {m.Residents.Count}/5    已配送 {m.DeliveredTrips} 趟\n" +
                (m.Residents.Count == 0 ? "工资：招募后开始计时" : $"下次发薪 {m.SecondsToWage:0} 秒    欠薪 {m.UnpaidWages:0.00}") +
                $"\n食物 {m.Warehouse.Count(ItemId.Food)} / 水 {m.Warehouse.Count(ItemId.DrinkingWater)}（保护{m.DrinkingReserve}）\n"+(m.RiotActive?$"暴动补款 {Math.Max(0,m.RiotTargetCents-m.RiotPaidCents)/100m:0.00} / {m.RiotSeconds:0}秒":$"生活缺供 {m.MissingSupplyPeople} 人 · 拆解 {m.DismantledBatches} 批");
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
            target.text = "点击地图行走 · WASD 移动 · E 交互 · Shift+E 整堆 · Q 入库 · 滚轮缩放";
            var nearest = m.NearestPickup(); if (nearest != null) target.text += "    附近：" + ItemCatalog.Name(nearest.Item);
            objective.text = m.CurrentDemoHint;
            var low = m.Tools.FirstOrDefault(t => ItemCatalog.IsTool(t.Item) && t.Durability > 0 && t.Durability <= 60 && !m.Tools.Any(other => other.Item == t.Item && other.Id != t.Id && other.Durability > 60));
            if (low != null) objective.text = "工具预警：" + ItemCatalog.Name(low.Item) + "耐久低于 20%，准备替补并送到岗位。";
            residentsInfo.text = m.Residents.Count == 0 ? "暂无居民\n\n发电取得认可后，可招募首批居民。\n居民会实际搬运原料和成品。" :
                string.Join("\n\n", m.Residents.Select(r => $"居民 {r.Id:00}    钱包 {r.Cash:0.00} / 保留4\n{r.Activity}\n食物：{r.Needs.foodReason} · 水：{r.Needs.waterReason}"));
            RefreshDemo(m);
            RefreshInventory(m);
            RefreshTutorial(m);
        }

        private void RefreshDemo(PrototypeSimulation m)
        {
            if(game.MenuOpen||game.ResultVisible){tradeConfirmation.gameObject.SetActive(false);pendingTechnology=-1;}
            string[] policyNames={"收费","免费","半价补贴"};
            foodPolicy.GetComponentInChildren<Text>().text="食物："+policyNames[(int)m.FoodPolicy];waterPolicy.GetComponentInChildren<Text>().text="饮水："+policyNames[(int)m.WaterPolicy];
            externalSupply.GetComponentInChildren<Text>().text="个人外购："+(m.ExternalSupply?"开":"关");emergencyWater.GetComponentInChildren<Text>().text="紧急水："+(m.EmergencyWater?"开":"关");
            autoTools.GetComponentInChildren<Text>().text="替补工具自动采购："+(m.AutomaticToolPurchase?"开":"关");
            lifeInfo.text=$"主角：食物 {m.PlayerNeeds.foodReason} / 水 {m.PlayerNeeds.waterReason}\n生活缺供 {m.MissingSupplyPeople} 人；保护水 {m.DrinkingReserve} 升\n累计消耗：食物{m.MealsConsumed} / 水{m.WaterConsumed}\n零售收入 {m.RetailIncomeCents/100m:0.00} / 外购 {m.ExternalLivingSpentCents/100m:0.00}\n社区账户（房租） {m.CommunityCashCents/100m:0.00}\n首期引导免费；后续政策可调整\n{m.ToolSupplyNotice}";
            assemblyInfo.text=$"木柄1 + {ItemCatalog.Name(m.AssemblyEnd)}1 → {ItemCatalog.Name(m.AssemblyProduct)}1\n{m.AssemblySeconds:0}秒人工 / 进度 {m.AssemblyProgress:P0}\n已制作 {m.AssembledTools} 把 · 需到工作台正面";
            modal.gameObject.SetActive(game.MenuOpen||game.ResultVisible);
            if(!modal.gameObject.activeSelf)return;
            modalTitle.text=m.Failed?"经营失败":game.ResultVisible?"首区 Demo 达成":"涌现 / 首区 Demo";
            modalInfo.text=m.Failed?m.FailureReason+"\n可读取失败前存档，或新游戏重试。":game.ResultVisible?$"完成用时 {m.DemoCompletedAt/60:0.0} 分钟 · 现金 {m.Money:0.00} · 工资已付 {m.TotalWagesPaidCents/100m:0.00}\n可以继续经营或保存。这是首区阶段目标，后续扩区及返程仍在制作。":"从零现金起步，让回收、生产、运输和生活供给运转起来。\n八个目标可自由安排顺序；新局使用独立存档槽。";
            bool[] flags=m.DemoMilestones;
            goals.text=string.Join("\n",PrototypeSimulation.DemoGoalNames.Select((name,i)=>flags[i]?"<color=#97C6A1>[完成] "+name+"</color>":"[待办] "+name))+"\n\n"+game.Notice;
            menuResume.GetComponentInChildren<Text>().text=!game.SessionStarted?"开始试玩":game.ResultVisible?"继续经营":"返回工坊";
            menuResume.interactable=!m.Failed;
        }

        private void RefreshTutorial(PrototypeSimulation m)
        {
            if(tutorialModel!=m){tutorialModel=m;reviewLesson=-1;displayedLesson=-1;}
            m.Tutorial.Refresh(m);
            int lesson=reviewLesson>=0?reviewLesson:m.Tutorial.step;
            tutorialTitle.text=PrototypeTutorial.Titles[lesson];tutorialLesson.text=PrototypeTutorial.Lessons[lesson];
            bool live=reviewLesson<0 && m.Tutorial.active;
            tutorialProgress.text=reviewLesson>=0?"翻阅课程 · 不改变当前进度":m.Tutorial.Completed?"全部课程已完成":$"当前课程 {m.Tutorial.step}/{TutorialProgress.LastStep-1} · {(m.Tutorial.active?"自动记录进度":"教程已收起")}";
            if(live && !m.Tutorial.started)tutorialProgress.text="12课入门 · 随时可跳过";
            var action=PrototypeTutorial.NextAction(m,m.Tutorial.step);
            tutorialTip.text=live && m.Tutorial.started && !m.Tutorial.Completed?action.instruction+(action.hasTarget?$"\n地图目标：({action.x}, {action.y}) · 金色标记":""):
                m.Tutorial.IntroOpen?"准备页期间时间停止。开始后，按提示操作才能推进。":"可继续当前教程，或使用上一课 / 下一课翻阅。";
            if(m.Paused)tutorialTip.text="游戏已暂停：按空格继续，再进行移动或加工。\n"+tutorialTip.text;
            if(m.MissingSupplyPeople>0)tutorialTip.text="有人缺供：先到人员页检查饮食，免费供应也需要库存。\n"+tutorialTip.text;
            string caption=!live?"回到当前教程":!m.Tutorial.started?"开始教程":m.Tutorial.Completed?"完成，继续经营":action.hasTarget && !action.AtTarget(m)?"前往："+action.label:action.label;
            tutorialAction.GetComponentInChildren<Text>().text=caption;
            tutorialAction.interactable=!m.Failed && (!live || !m.Tutorial.started || m.Tutorial.Completed || action.operation!=TutorialOperation.Wait && !m.Interacting);
            if(live && m.Tutorial.started && !m.Tutorial.Completed && m.Paused)tutorialAction.interactable=false;
            tutorialLocate.interactable=live && m.Tutorial.started && action.button!=null;
            tutorialStart.GetComponentInChildren<Text>().text=m.Tutorial.active?"回到当前步骤":"继续教程";
            LayoutTutorialText(lesson);
            foreach(var binding in buttonBindings)
            {
                var outline=binding.button.GetComponent<Outline>();
                if(outline!=null)
                {
                    bool highlighted=live && m.Tutorial.started && !m.Tutorial.Completed && binding.command==action.button;
                    outline.effectColor=highlighted?Accent:new Color32(149,180,167,38);outline.effectDistance=highlighted?new Vector2(2,-2):new Vector2(1,-1);
                }
            }
            if(live && m.Tutorial.started && !m.Tutorial.Completed && m.MissingSupplyPeople==0 && m.UnpaidWageCents==0 && !m.RiotActive)
                objective.text=$"教程 {m.Tutorial.step}/{TutorialProgress.LastStep-1} · {PrototypeTutorial.Titles[m.Tutorial.step]} —— 查看右侧“教程”页";
        }

        private void LayoutTutorialText(int lesson)
        {
            float titleHeight=Mathf.Max(30,tutorialTitle.preferredHeight);
            tutorialTitle.rectTransform.sizeDelta=new Vector2(352,titleHeight);
            float bodyY=82+titleHeight+12,bodyHeight=Mathf.Max(112,tutorialLesson.preferredHeight);
            tutorialLesson.rectTransform.anchoredPosition=new Vector2(12,-bodyY);tutorialLesson.rectTransform.sizeDelta=new Vector2(352,bodyHeight);
            float tipY=bodyY+bodyHeight+18,tipHeight=Mathf.Max(56,tutorialTip.preferredHeight);
            tutorialTip.rectTransform.anchoredPosition=new Vector2(12,-tipY);tutorialTip.rectTransform.sizeDelta=new Vector2(352,tipHeight);
            float actionY=tipY+tipHeight+16;
            tutorialAction.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-actionY);tutorialSkip.GetComponent<RectTransform>().anchoredPosition=new Vector2(188,-actionY);
            tutorialLocate.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-(actionY+52));
            tutorialOptional.rectTransform.anchoredPosition=new Vector2(12,-(actionY+104));
            tutorialPrevious.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-(actionY+162));tutorialNext.GetComponent<RectTransform>().anchoredPosition=new Vector2(188,-(actionY+162));
            tutorialStart.GetComponent<RectTransform>().anchoredPosition=new Vector2(12,-(actionY+210));tutorialHelp.rectTransform.anchoredPosition=new Vector2(12,-(actionY+268));
            tutorialContent.sizeDelta=new Vector2(0,actionY+368);
            if(displayedLesson!=lesson){displayedLesson=lesson;pages[3].GetComponent<ScrollRect>().verticalNormalizedPosition=1;}
        }

        public void ShowTutorialControl()
        {
            var m=game.Model;var action=PrototypeTutorial.NextAction(m,m.Tutorial.step);
            if(action.button==null)return;
            if(action.page==2){showAllItems=true;RefreshInventory(m);}
            SelectTab(action.page);
            var binding=buttonBindings.FirstOrDefault(b=>b.command==action.button);
            if(binding==null)return;
            var scroll=binding.button.GetComponentInParent<ScrollRect>();
            if(scroll!=null)
            {
                Canvas.ForceUpdateCanvases();
                Vector3 point=scroll.content.InverseTransformPoint(binding.button.transform.position);
                float room=Mathf.Max(1,scroll.content.rect.height-scroll.viewport.rect.height);
                scroll.verticalNormalizedPosition=1-Mathf.Clamp01((-point.y-40)/room);
            }
            if(action.hasTarget)game.GetComponent<PrototypeWorld>().FocusPoint(action.x,action.y);
            game.Report(new ActionResult(true,"金色边框标出对应按钮；完成后可返回教程页。"));
        }
        public void ShowTutorial() { reviewLesson=-1;SelectTab(3); }

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
            if((game.MenuOpen||game.ResultVisible)&&command!="Resume"&&command!="MenuLoad"&&command!="MenuSave"&&command!="MenuNew"&&command!="Quit")return;
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
                    () => AskTrade(Technology.Wood), () => game.Report(m.RemoveFacility(8, true)),
                    () => game.Report(m.TakeInput()), () => game.Report(m.TakeAsh()),
                    () => game.Report(m.Buy(ItemId.Food,6)), () => game.Report(m.Buy(ItemId.DrinkingWater,12)), () => game.Report(m.Buy(ItemId.SawBlade)),
                    () => game.Report(m.SelectAssembly(AssemblyRecipe.Saw)), () => game.Report(m.SelectAssembly(AssemblyRecipe.Hammer)), () => game.Report(m.SelectAssembly(AssemblyRecipe.Screwdriver)),
                    () => game.Report(m.FeedAssembly()), () => game.Report(m.ToggleAssembly()), () => game.Report(m.TakeAssembly()), () => game.Report(m.CancelAssembly()), () => game.Report(m.TakeAssembly(true)),
                    () => AskTrade(Technology.Tools), () => AskTrade(Technology.Textile), () => AskTrade(Technology.Metal), () => game.Report(m.Buy(ItemId.HammerHead)), () => game.Report(m.Buy(ItemId.ScrewdriverRod))
                };
                if (index >= 0 && index < commands.Length) commands[index](); return;
            }
            if (command.StartsWith("Tab") && int.TryParse(command.Substring(3), out int tab)) { SelectTab(tab); return; }
            if (command.StartsWith("Speed") && int.TryParse(command.Substring(5), out int speed)) { m.SetSpeed(speed); return; }
            foreach (string action in new[] { "Pick", "Take", "Drop", "Sell" })
            {
                if (!command.StartsWith(action) || !int.TryParse(command.Substring(action.Length), out int itemIndex)) continue;
                var item = (ItemId)itemIndex;
                if (action == "Pick"){if(m.Ground.Any(p=>p.Item==item&&PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)<=m.Settings.interactionRadius))game.Report(m.PickUp(int.MaxValue,item));else game.GoToItem(item);} if (action == "Take") game.Report(m.Withdraw(item));
                if (action == "Drop") game.Report(m.Drop(item)); if (action == "Sell") game.Report(m.Sell(item, 1)); return;
            }
            switch (command)
            {
                case "Menu": game.OpenMenu();break;
                case "Resume": if(!game.SessionStarted){game.StartNewGame();SelectTab(3);}else if(game.ResultVisible)game.ContinueAfterResult();else game.ResumeGame();break;
                case "MenuLoad": game.LoadLatest();break;case "MenuSave": game.SaveGame();break;case "MenuNew": game.StartNewGame();SelectTab(3);break;case "Quit": game.QuitGame();break;
                case "TutorialAction":
                    if(reviewLesson>=0 || !m.Tutorial.active || !m.Tutorial.started){reviewLesson=-1;game.BeginTutorial();}
                    else if(m.Tutorial.Completed)game.SkipTutorial();else game.ExecuteTutorialAction();break;
                case "TutorialStart":reviewLesson=-1;game.BeginTutorial();break;
                case "TutorialLocate":ShowTutorialControl();break;
                case "TutorialSkip":game.SkipTutorial();break;
                case "TutorialPrevious":reviewLesson=Mathf.Max(0,(reviewLesson>=0?reviewLesson:m.Tutorial.step)-1);break;
                case "TutorialNext":reviewLesson=Mathf.Min(TutorialProgress.LastStep,(reviewLesson>=0?reviewLesson:m.Tutorial.step)+1);break;
                case "FoodPolicy": game.Report(m.SetSupplyPolicy(ItemId.Food,(SupplyPolicy)(((int)m.FoodPolicy+1)%3)));break;
                case "WaterPolicy": game.Report(m.SetSupplyPolicy(ItemId.DrinkingWater,(SupplyPolicy)(((int)m.WaterPolicy+1)%3)));break;
                case "ExternalSupply": m.SetExternalSupply(!m.ExternalSupply);break;case "EmergencyWater":m.SetEmergencyWater(!m.EmergencyWater);break;
                case "AutoTools":m.SetAutomaticToolPurchase(!m.AutomaticToolPurchase);break;
                case "ConfirmTrade":if(pendingTechnology>=0)game.Report(m.SellTechnology((Technology)pendingTechnology));pendingTechnology=-1;tradeConfirmation.gameObject.SetActive(false);break;
                case "CancelTrade":pendingTechnology=-1;tradeConfirmation.gameObject.SetActive(false);break;
                case "Pause": m.SetPaused(!m.Paused); break; case "Save": game.SaveGame(); break; case "Load": game.LoadLatest(); break;
                case "Restart": game.OpenMenu(); break; case "Interact": game.Interact(); break; case "Deposit": game.Report(m.Deposit()); break;
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
        private void AskTrade(Technology technology)
        {
            if(game.Model.IsTechnologySold(technology)){game.Report(new ActionResult(false,"该技术已经出售。"));return;}
            int[] values={60,50,100,80,80,40};
            string[] affected={"木板、木柄、整理木料","可穿衣服、干净抹布","金属坯料及零件","工具部件及手工工具","再生塑料及塑料制品","食物"};
            pendingTechnology=(int)technology;
            tradeInfo.text=$"确认出售技术？一次收入 {values[(int)technology]} 钱。\n\n{affected[(int)technology]}的售价将永久降到最低价。\n保留技术使用权；此窗口不会暂停游戏。";
            tradeConfirmation.gameObject.SetActive(true);
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
