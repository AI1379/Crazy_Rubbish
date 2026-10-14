using System;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    // Reference-space dimensions are shared with the world camera so the map never sits under UI.
    public sealed partial class PrototypeHud
    {
        private const int CurrentLayoutVersion = 8;
        public const float ReferenceWidth = 1600, MapLeft = 0, MapWidth = 1600, MapTop = 0, MapBottom = 0;
        [SerializeField] private Text powerValue, populationValue, foodValue, waterValue, menuNotice;
        [SerializeField] private float[] onsiteSectionY = new float[4];
        [SerializeField] private Text[] milestoneLabels = new Text[8];
        [SerializeField] private Image[] milestoneMarks = new Image[8];
        [SerializeField] private Text[] residentLabels = new Text[5];
        [SerializeField] private RectTransform[] residentCards = new RectTransform[5];
        [SerializeField] private RectTransform peopleContent;
        [SerializeField] private float residentStart;

        private void BuildInterface()
        {
            if (HasLayout) { RefreshItemArtwork(); return; }
            if (root != null)
            {
                if (Application.isPlaying) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
                else DestroyImmediate(root.gameObject);
            }
            buttonBindings.Clear();
            layoutVersion = CurrentLayoutVersion;
            pages = new RectTransform[4]; tabs = new Button[4];
            inspection = null;
            var canvas = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false); root = canvas.GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, 900); scaler.matchWidthOrHeight = 0;
            if (GetComponentInChildren<EventSystem>(true) == null && EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

            BuildTopBar();
            BuildWorldOverlay();
            var sidebar = FramedPanel(root, "Management", new Vector2(1, 0), Vector2.one,
                new Vector2(-412, 90), new Vector2(-16, -86), Panel);
            management = sidebar; sidebar.GetComponent<Image>().raycastTarget = true;
            TextAt(sidebar, "Title", "工坊管理", 18, 12, 200, 32, 23, Ink);
            ButtonAt(sidebar, "CloseManagement", "×", 350, 16, 28, 30);
            string[] names = { "现场", "人员", "仓库", "教程" };
            for (int i = 0; i < 4; i++) tabs[i] = ButtonAt(sidebar, "Tab" + i, names[i], 12 + i * 94, 56, 90, 40);
            for (int i = 0; i < 4; i++)
                pages[i] = StretchPanel(sidebar, names[i] + " Page", Vector2.zero, Vector2.one,
                    new Vector2(8, 76), new Vector2(-8, -108), Color.clear);
            var status = FramedPanel(sidebar, "Community Status", Vector2.zero, new Vector2(1, 0),
                new Vector2(12, 12), new Vector2(-12, 66), Inset);
            community = TextAt(status, "Community", "", 12, 6, 348, 42, 14, Muted);
            BuildOnsitePage();
            BuildPeoplePage();
            BuildInventoryPage();
            BuildTutorialPage();
            BuildActionDock();
            BuildMenu();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            SelectTab(0);
            ClosePanels();
        }

        private void BuildTopBar()
        {
            var header = Surface(root, "Header", 16, 16, 584, 52, new Color32(26, 31, 34, 235));
            money = Metric(header, "Money", "现金", 18, 104, Accent);
            powerValue = Metric(header, "Power", "电力", 136, 94, Green);
            populationValue = Metric(header, "Population", "居民", 246, 92, Ink);
            foodValue = Metric(header, "Food", "食物", 358, 92, Ink);
            waterValue = Metric(header, "Water", "饮水", 470, 92, Ink);
            var time = Surface(root, "Time Controls", 0, 16, 404, 52, new Color32(26, 31, 34, 235));
            time.anchorMin = time.anchorMax = Vector2.one; time.anchoredPosition = new Vector2(-420, -16);
            clock = TextAt(time, "Clock", "00:00", 14, 8, 86, 36, 20, Ink);
            pause = ButtonAt(time, "Pause", "暂停", 106, 8, 66, 36);
            for (int i = 1; i <= 3; i++) speedButtons[i - 1] = ButtonAt(time, "Speed" + i, i + "×", 180 + (i - 1) * 44, 8, 38, 36);
            ButtonAt(time, "Menu", "菜单", 322, 8, 68, 36);
        }

        private Text Metric(Transform parent, string name, string caption, float x, float width, Color color)
        {
            TextAt(parent, name + " Caption", caption, x, 4, width, 18, 11, Muted);
            return TextAt(parent, name, "0", x, 22, width, 25, 20, color);
        }

        private void BuildOnsitePage()
        {
            var content = ScrollContent(pages[0], 1600);
            var scroll = pages[0].GetComponent<ScrollRect>();
            scroll.viewport.offsetMax = new Vector2(-12, -46);
            string[] sections = { "建设", "搬运", "装配", "交易" };
            for (int i = 0; i < sections.Length; i++) ButtonAt(pages[0], "OnsiteJump" + i, sections[i], 8 + i * 90, 2, 84, 32);
            onsiteSectionY[0] = 0; onsiteSectionY[1] = 370;
            SectionHeading(content, "建设与设施", "01", 4);
            FacilityCard(content, "木工台", "木料加工与配方", game.appearance.woodwork, 1, 44);
            FacilityCard(content, "拆解地点", "回收旧设备中的零件", game.appearance.dismantle, 2, 120);
            FacilityCard(content, "工作台", "装配工具与替补部件", game.appearance.workbench, 3, 196);
            FacilityCard(content, "发电机", "补充燃料，获得居民认可", game.appearance.generator, 4, 272);
            float y = CommandGroup(content, "投料与搬运", 370, new[] { 10, 11, 12, 13, 14, 15, 6, 7, 24, 25 });
            y = CommandGroup(content, "拆解与批次", y, new[] { 8, 9, 16, 17, 18 });
            onsiteSectionY[2] = y;
            y = CommandGroup(content, "工具装配", y, new[] { 29, 30, 31, 32, 33, 34, 35, 36 });
            var info = Surface(content, "Assembly Card", 8, y, 356, 92, Inset);
            assemblyInfo = TextAt(info, "Assembly Info", "", 12, 8, 332, 76, 15, Muted);
            onsiteSectionY[3] = y + 112;
            y = CommandGroup(content, "采购与交易", onsiteSectionY[3], new[] { 19, 20, 21, 28, 40, 41, 22, 37, 38, 39, 23 });
            TextAt(content, "Onsite Help", "投料、取物和交接工具，需要主角到场。", 12, y, 340, 48, 14, Muted);
            content.sizeDelta = new Vector2(0, y + 60);
        }

        private void FacilityCard(Transform parent, string title, string description, Sprite sprite, int command, float y)
        {
            var card = Surface(parent, title + " Card", 8, y, 356, 68, Inset);
            Artwork(card, "Artwork", sprite, 8, 8, 48, 48);
            TextAt(card, "Title", title, 66, 7, 176, 27, 18, Ink);
            TextAt(card, "Description", description, 66, 35, 180, 22, 12, Muted);
            ButtonAt(card, "OpenFacility" + command, "查看", 260, 16, 84, 36);
        }

        private void BuildPeoplePage()
        {
            peopleContent = ScrollContent(pages[1], 1550);
            float y = CommandGroup(peopleContent, "居民与工资", 4, new[] { 3, 4, 5 });
            SectionHeading(peopleContent, "生活供应", "02", y); y += 42;
            foodPolicy = ButtonAt(peopleContent, "FoodPolicy", "食物：收费", 12, y, 166, 40);
            waterPolicy = ButtonAt(peopleContent, "WaterPolicy", "饮水：收费", 188, y, 166, 40); y += 48;
            externalSupply = ButtonAt(peopleContent, "ExternalSupply", "个人外购：开", 12, y, 166, 40);
            emergencyWater = ButtonAt(peopleContent, "EmergencyWater", "紧急水：开", 188, y, 166, 40); y += 48;
            autoTools = ButtonAt(peopleContent, "AutoTools", "替补工具自动采购：关", 12, y, 342, 40); y += 52;
            var supply = Surface(peopleContent, "Supply Summary", 8, y, 356, 186, Inset);
            lifeInfo = TextAt(supply, "Life Info", "", 12, 10, 332, 166, 14, Muted);
            lifeInfo.alignment = TextAnchor.UpperLeft; y += 202;
            y = CommandGroup(peopleContent, "生活及工具采购", y, new[] { 26, 27, 28 });
            residentsInfo = TextAt(peopleContent, "Residents", "居民名册", 12, y, 340, 54, 17, Ink);
            residentStart = y + 66;
            for (int i = 0; i < residentCards.Length; i++)
            {
                var card = Surface(peopleContent, "Resident " + i, 8, residentStart + i * 112, 356, 102, Inset);
                residentCards[i] = card;
                Artwork(card, "Portrait", game.appearance.characters[(i % 4) + 2].idle[1], 6, 12, 44, 70);
                residentLabels[i] = TextAt(card, "Resident Info", "", 60, 10, 284, 84, 14, Ink);
                residentLabels[i].alignment = TextAnchor.UpperLeft;
            }
        }

        private void BuildInventoryPage()
        {
            inventoryContent = ScrollContent(pages[2], 260 + ItemCount * 76);
            TextAt(inventoryContent, "Inventory Title", "物资清单", 12, 4, 200, 30, 21, Ink);
            inventoryFilter = ButtonAt(inventoryContent, "InventoryFilter", "全部物品", 238, 4, 116, 34);
            capacity = TextAt(inventoryContent, "Capacity", "", 12, 50, 342, 24, 14, Muted);
            capacityFill = Bar(inventoryContent, "CapacityBar", 12, 82, 342, 5, Green);
            TextAt(inventoryContent, "Item Caption", "物品 / 操作", 12, 104, 184, 24, 13, Muted);
            TextAt(inventoryContent, "Carry Caption", "背包", 238, 104, 52, 24, 13, Accent).alignment = TextAnchor.MiddleCenter;
            TextAt(inventoryContent, "Stock Caption", "仓库", 298, 104, 60, 24, 13, Muted).alignment = TextAnchor.MiddleCenter;
            inventoryEmpty = TextAt(inventoryContent, "Empty Inventory", "暂无入库物资\n\n到来料区拾取，再送回仓库。\n按 E 拾取 · 按 Q 入库\n\n“全部物品”可查看完整物品目录。", 20, 164, 326, 210, 17, Muted);
            inventoryEmpty.alignment = TextAnchor.UpperLeft;
            for (int i = 0; i < ItemCount; i++)
            {
                var row = Surface(inventoryContent, "ItemRow" + i, 8, 140 + i * 76, 356, 68, Inset);
                inventoryRows[i] = row;
                Artwork(row, "Icon", game.appearance.ItemSprite((ItemId)i), 8, 12, 40, 40);
                TextAt(row, "Item", PrototypeGame.ItemName((ItemId)i), 60, 2, 170, 30, 15, Ink);
                carryCounts[i] = TextAt(row, "Carry", "0", 230, 2, 52, 30, 17, Accent);
                warehouseCounts[i] = TextAt(row, "Stock", "0", 288, 2, 64, 30, 17, Ink);
                carryCounts[i].alignment = warehouseCounts[i].alignment = TextAnchor.MiddleCenter;
                warehouseCounts[i].resizeTextForBestFit = true; warehouseCounts[i].resizeTextMinSize = 11; warehouseCounts[i].resizeTextMaxSize = 17;
                ButtonAt(row, "Pick" + i, "拾取", 60, 35, 64, 26);
                ButtonAt(row, "Take" + i, "取出", 132, 35, 64, 26);
                ButtonAt(row, "Drop" + i, "放下", 204, 35, 64, 26);
                ButtonAt(row, "Sell" + i, "出售", 276, 35, 68, 26);
            }
        }

        private void BuildActionDock() => BuildFacilityPanel();

        private void BuildWorkshopMenu()
        {
            modal = StretchPanel(root, "Demo Menu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.045f, 0.055f, 0.06f, 0.91f));
            // The overlay, including empty space, consumes pointer input.
            modal.GetComponent<Image>().raycastTarget = true;
            var card = Surface(modal, "Card", 0, 0, 940, 650, Panel);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = Vector2.zero;
            Stripe(card, "Accent", 0, 0, 940, 3, Accent);
            TextAt(card, "Eyebrow", "CRAZY RUBBISH    /    第一区域", 32, 24, 700, 20, 13, Accent);
            modalTitle = TextAt(card, "Title", "从废料开始，建起一座工坊。", 32, 52, 876, 50, 32, Ink);
            modalTitle.fontStyle = FontStyle.Bold;
            modalInfo = TextAt(card, "Info", "", 32, 113, 876, 62, 17, Muted); modalInfo.alignment = TextAnchor.UpperLeft;
            goals = TextAt(card, "Goals", "阶段目标", 32, 184, 876, 26, 14, Accent);
            for (int i = 0; i < 8; i++)
            {
                var goal = Surface(card, "Milestone " + i, 32 + i % 2 * 446, 222 + i / 2 * 59, 430, 49, Inset);
                var number = TextAt(goal, "Number", (i + 1).ToString("00"), 12, 0, 32, 49, 17, Muted);
                milestoneMarks[i] = At(goal, "Mark", 0, 0, 3, 49).gameObject.AddComponent<Image>();
                milestoneMarks[i].raycastTarget = false;
                milestoneLabels[i] = TextAt(goal, "Label", "", 54, 0, 364, 49, 16, Ink);
            }
            menuNotice = TextAt(card, "Notice", "", 32, 454, 876, 22, 14, Muted);
            menuResume = ButtonAt(card, "Resume", "开始试玩", 32, 484, 210, 46, true);
            ButtonAt(card, "MenuLoad", "继续存档", 254, 484, 210, 46);
            ButtonAt(card, "MenuSave", "保存进度", 476, 484, 210, 46);
            ButtonAt(card, "MenuNew", "新游戏", 698, 484, 210, 46);
            Stripe(card, "Controls Divider", 32, 552, 876, 1, Border);
            TextAt(card, "Controls", "WASD / 右键行走    左键查看设施    E 交互    Q 入库    F 木工\n空格 暂停    1 / 2 / 3 倍速    滚轮 缩放    Esc 关闭面板 / 菜单", 32, 570, 742, 58, 14, Muted);
            ButtonAt(card, "Quit", "退出", 804, 580, 104, 34);
            modal.gameObject.SetActive(false);
            tradeConfirmation = Surface(root, "Trade Confirmation", 0, 0, 720, 290, Panel);
            tradeConfirmation.GetComponent<Image>().raycastTarget = true;
            tradeConfirmation.anchorMin = tradeConfirmation.anchorMax = new Vector2(0.5f, 0.5f);
            tradeConfirmation.pivot = new Vector2(0.5f, 0.5f); tradeConfirmation.anchoredPosition = Vector2.zero;
            Stripe(tradeConfirmation, "Accent", 0, 0, 720, 3, Accent);
            TextAt(tradeConfirmation, "Title", "技术交易 / 永久价格影响", 26, 18, 668, 30, 21, Accent);
            tradeInfo = TextAt(tradeConfirmation, "Info", "", 26, 65, 668, 140, 18, Ink); tradeInfo.alignment = TextAnchor.UpperLeft;
            ButtonAt(tradeConfirmation, "ConfirmTrade", "确认出售", 26, 222, 230, 42, true);
            ButtonAt(tradeConfirmation, "CancelTrade", "取消", 274, 222, 230, 42);
            tradeConfirmation.gameObject.SetActive(false);
        }

        private void SectionHeading(Transform parent, string caption, string number, float y)
        {
            TextAt(parent, caption + " Number", number, 12, y, 32, 30, 12, Accent);
            TextAt(parent, caption, caption, 48, y, 296, 30, 18, Ink);
            Stripe(parent, caption + " Divider", 12, y + 34, 342, 1, Border);
        }

        private RectTransform Surface(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = At(parent, name, x, y, width, height);
            var background = rect.gameObject.AddComponent<Image>(); background.color = color; background.raycastTarget = false;
            Frame(rect); return rect;
        }

        private RectTransform FramedPanel(Transform parent, string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = StretchPanel(parent, name, min, max, offsetMin, offsetMax, color); Frame(rect); return rect;
        }

        private static void Frame(RectTransform rect)
        {
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = true;
        }

        private void Artwork(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
        {
            var image = At(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        }
    }
}
