using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [SerializeField] private RectTransform management, facilityPanel, guidePrompt, toast;
        [SerializeField] private RectTransform[] facilityPages = new RectTransform[4];
        [SerializeField] private Button[] launchers = new Button[4];
        [SerializeField] private Button alerts, zones;
        [SerializeField] private Text facilityTitle, facilitySummary;
        [SerializeField] private Image facilityArtwork;
        private int activeTab, selectedFacility;
        private bool showZones;
        private int lastNoticeRevision;
        private float noticeUntil;
        private string toolWarningLabel;
        private int operationalIssueId;
        public bool ManagementOpen => management != null && management.gameObject.activeSelf;
        public int SelectedFacility => facilityPanel != null && facilityPanel.gameObject.activeSelf ? selectedFacility : 0;
        public bool ZonesVisible => showZones || ManagementOpen && activeTab == 0;

        private void BuildWorldOverlay()
        {
            var navigation = Surface(root, "Navigation", 16, 0, 690, 54, new Color32(26, 31, 34, 242));
            navigation.anchorMin = navigation.anchorMax = Vector2.zero;
            navigation.anchoredPosition = new Vector2(16, 70);
            string[] captions = { "建造  B", "人员  P", "背包  I", "教程  H" };
            for (int i = 0; i < 4; i++) launchers[i] = ButtonAt(navigation, "OpenTab" + i, captions[i], 8 + 112 * i, 8, 104, 38);
            zones = ButtonAt(navigation, "Zones", "区域", 456, 8, 110, 38);
            ButtonAt(navigation, "OpenDevelopment", "产业  G", 572, 8, 110, 38);

            BuildTutorialCoach();BuildConstructionNotice();

            alerts = ButtonAt(root, "Alerts", "", 16, 80, 410, 36);
            alerts.GetComponent<Image>().color = new Color32(73, 38, 32, 245);
            alerts.GetComponentInChildren<Text>().color = Danger;
            alerts.gameObject.SetActive(false);

            toast = Surface(root, "Action Notice", 0, 0, 740, 36, new Color32(26, 31, 34, 242));
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0);
            toast.pivot = new Vector2(0.5f, 0);
            toast.anchoredPosition = new Vector2(-130, 84);
            message = TextAt(toast, "Notice", "", 14, 0, 712, 36, 15, Ink);
            message.resizeTextForBestFit = true; message.resizeTextMinSize = 12; message.resizeTextMaxSize = 15;
            toast.gameObject.SetActive(false);

            target = TextAt(root, "Hint", "点击设施查看 · 右键移动 · 滚轮缩放", 0, 0, 520, 24, 13, Muted);
            target.rectTransform.anchorMin = target.rectTransform.anchorMax = new Vector2(1, 0);
            target.rectTransform.anchoredPosition = new Vector2(-540, 46);
            target.alignment = TextAnchor.MiddleRight;
        }

        private void BuildFacilityPanel()
        {
            facilityPanel = Surface(root, "Facility Panel", 0, 86, 396, 660, Panel);
            facilityPanel.anchorMin = facilityPanel.anchorMax = Vector2.one;
            facilityPanel.anchoredPosition = new Vector2(-412, -86);
            facilityPanel.GetComponent<Image>().raycastTarget = true;
            Artwork(facilityPanel, "Artwork", game.appearance.woodwork, 18, 14, 50, 50);
            facilityArtwork = facilityPanel.Find("Artwork").GetComponent<Image>();
            facilityTitle = TextAt(facilityPanel, "Title", "木工台", 82, 20, 240, 32, 24, Ink);
            ButtonAt(facilityPanel, "CloseFacility", "×", 350, 18, 28, 30);
            facilitySummary = TextAt(facilityPanel, "Summary", "", 20, 80, 356, 64, 15, Muted);
            facilitySummary.resizeTextForBestFit=true;facilitySummary.resizeTextMinSize=12;facilitySummary.resizeTextMaxSize=15;facilitySummary.verticalOverflow=VerticalWrapMode.Truncate;
            ButtonAt(facilityPanel, "GoToFacility", "前往作业点", 20, 150, 356, 38, true);
            for (int i = 0; i < 4; i++) facilityPages[i] = At(facilityPanel, i == 0 ? "Woodwork" : "Facility " + (i + 1), 0, 204, 396, 384);

            var wood = facilityPages[0];
            build = ButtonAt(wood, "Build", "搭建木工台", 20, 0, 356, 38);
            workState = TextAt(wood, "State", "", 20, 46, 356, 26, 16, Ink);
            progressFill = Bar(wood, "Progress", 20, 80, 356, 5, Accent);
            plankRecipe = ButtonAt(wood, "Plank", "木板", 20, 98, 112, 38);
            handleRecipe = ButtonAt(wood, "Handle", "木柄", 142, 98, 112, 38);
            sortedRecipe = ButtonAt(wood, "Sorted", "整理", 264, 98, 112, 38);
            recipeInfo = TextAt(wood, "Recipe", "", 20, 145, 356, 44, 14, Muted);
            work = ButtonAt(wood, "Work", "进入 / 退出木工岗位  F", 20, 198, 356, 38, true);
            FacilityCommands(wood, 246, new[] { 10, 11, 12, 16 });
            pressure = TextAt(wood, "Pressure", "", 20, 342, 356, 42, 14, Green);

            ButtonAt(facilityPages[1], "Command0", "搭建拆解地点", 20, 0, 356, 38);
            FacilityCommands(facilityPages[1], 54, new[] { 14, 15, 13, 8, 9, 17 });
            TextAt(facilityPages[1], "Help", "把设备与螺丝刀带到正面，再投料作业。\n成品需取出并运回仓库。", 20, 222, 356, 64, 15, Muted);
            ButtonAt(facilityPages[2], "Command1", "搭建工作台", 20, 0, 356, 38);
            FacilityCommands(facilityPages[2], 54, new[] { 29, 30, 31, 32, 33, 34, 35, 36 });
            TextAt(facilityPages[2], "Help", "木柄 1 + 对应工作端 1 → 工具 1\n需要主角到场投料和作业。", 20, 260, 356, 64, 15, Muted);
            ButtonAt(facilityPages[3], "Command2", "组装发电机", 20, 0, 356, 38);
            FacilityCommands(facilityPages[3], 54, new[] { 6, 7, 25, 23 });
            TextAt(facilityPages[3], "Help", "投入木料或木料碎屑作为燃料。\n发电取得认可后，可以招募居民。", 20, 164, 356, 64, 15, Muted);
            ButtonAt(facilityPanel,"MoveOwned","搬迁 / 旋转",20,610,112,36);
            ButtonAt(facilityPanel,"RemoveOwned","拆除返料",142,610,112,36);
            ButtonAt(facilityPanel,"SellOwned","出售",264,610,112,36);
            facilityPanel.gameObject.SetActive(false);
        }

        private void FacilityCommands(Transform parent, float y, int[] commands)
        {
            for (int i = 0; i < commands.Length; i++)
                ButtonAt(parent, "FacilityCommand" + commands[i], CommandLabels[commands[i]], 20 + i % 2 * 184, y + i / 2 * 48, 172, 38);
        }

        public void ClosePanels()
        {
            CloseExperiencePanels();
            CloseInventory();
            if (management != null) management.gameObject.SetActive(false);
            if (facilityPanel != null) facilityPanel.gameObject.SetActive(false);
            if (developmentPanel != null) developmentPanel.gameObject.SetActive(false);armedSale=-1;
            selectedFacility = 0;inspectedBelt=inspectedPort=0;if(transportPanel!=null)transportPanel.gameObject.SetActive(false);
            if (inspection != null) inspection.gameObject.SetActive(false);
            UpdateLaunchers();
        }

        public void ResetForSession()
        {
            ClosePanels(); showZones = false; showAllItems = false; noticeUntil = 0;
            lastNoticeRevision = game.NoticeRevision;
            tutorialModel = null; reviewLesson = -1; displayedLesson = -1;
            currentKnowledge=null;manualKnowledge=null;dumpConfirm=false;
            if(knowledgePanel!=null)knowledgePanel.gameObject.SetActive(false);
            guideCollapsed = false; lastGuideStep = -1; guideCelebrationUntil = 0;
            if (toast != null) toast.gameObject.SetActive(false);
        }

        public bool TryCloseTopPanel()
        {
            if(CloseExperiencePanels())return true;
            if(transportPanel!=null&&transportPanel.gameObject.activeSelf){ClosePanels();return true;}
            if(InventoryOpen){CloseInventory();return true;}
            if(KnowledgeOpen){AcknowledgeKnowledge();return true;}
            if (tradeConfirmation != null && tradeConfirmation.gameObject.activeSelf)
            { pendingTechnology = -1; tradeConfirmation.gameObject.SetActive(false); return true; }
            if (!ManagementOpen && SelectedFacility == 0 && !DevelopmentOpen) return false;
            ClosePanels(); return true;
        }

        public void ToggleManagement(int page)
        {
            if(page==2){if(InventoryOpen)CloseInventory();else OpenInventory(game.Model.NearWarehouse);return;}
            if (ManagementOpen && activeTab == page) ClosePanels();
            else SelectTab(page);
        }

        public void OpenManagement(int page) => SelectTab(page);

        public void HandlePanelShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.B)) ToggleManagement(0);
            if (Input.GetKeyDown(KeyCode.P)) ToggleManagement(1);
            if (Input.GetKeyDown(KeyCode.I)) ToggleManagement(2);
            if (Input.GetKeyDown(KeyCode.H)) ToggleManagement(3);
            if (Input.GetKeyDown(KeyCode.G)) ToggleDevelopment();
        }

        public void ShowFacility(int facility)
        {
            if (facility < 1 || facility > 8 || facilityPanel == null) return;
            ClosePanels(); selectedFacility = facility;
            facilityPanel.gameObject.SetActive(true);
            for (int i = 0; i < facilityPages.Length; i++) if(facilityPages[i]!=null)facilityPages[i].gameObject.SetActive(i == facility - 1);
            string[] names = { "木工台", "拆解地点", "工作台", "发电机", "新住房", "路灯", "电动木工", "自动木工" };
            facilityTitle.text = names[facility - 1];
            facilityTitle.resizeTextForBestFit=true;facilityTitle.resizeTextMinSize=16;facilityTitle.resizeTextMaxSize=24;
            Sprite[] artwork = { game.appearance.woodwork, game.appearance.dismantle, game.appearance.workbench, game.appearance.generator, game.appearance.housing, game.appearance.streetlamp, game.appearance.electricWoodwork,game.appearance.automaticWoodwork };
            facilityArtwork.sprite = artwork[facility - 1];
            if (game.Model != null) RefreshOverlay(game.Model);
            RefreshProcessMode();
        }

        private void UpdateLaunchers()
        {
            for (int i = 0; i < launchers.Length; i++)
                if (launchers[i] != null) SetSelected(launchers[i], ManagementOpen && activeTab == i);
        }

        private void RefreshOverlay(PrototypeSimulation m)
        {
            bool blocked = game.MenuOpen || game.ResultVisible;
            RefreshAvailableMechanics(m);
            RefreshTutorialCoach(m);
            var issue=m.Facilities.FirstOrDefault(f=>f.Built&&f.RunRequested&&(f.Kind==1||PrototypeSimulation.IsWoodMachine(f.Kind))&&
                (f.State==WorkState.NeedsTool||f.State==WorkState.NeedsMaterials||f.State==WorkState.NeedsPower||f.State==WorkState.NeedsMaintenance||f.State==WorkState.OutputBlocked));
            operationalIssueId=issue?.Id??0;
            bool warning = m.RiotActive || m.UnpaidWageCents > 0 || m.MissingSupplyPeople > 0 || toolWarningLabel != null || issue!=null;
            alerts.gameObject.SetActive(!blocked && warning);
            alerts.GetComponentInChildren<Text>().text = m.RiotActive ? $"!  欠薪暴动 · 剩余 {m.RiotSeconds:0} 秒  >" :
                m.MissingSupplyPeople > 0 ? $"!  {m.MissingSupplyPeople} 人缺少生活供应  >" : m.UnpaidWageCents > 0 ? $"!  待付工资 {m.UnpaidWages:0.00}  >" : toolWarningLabel!=null?$"!  {toolWarningLabel}耐久低，请准备替补  >":issue==null?"":$"!  {PrototypeSimulation.FacilityName(issue.Kind)}：{PrototypeGame.StateName(issue.State)}  >";
            SetSelected(zones, showZones);
            if (lastNoticeRevision != game.NoticeRevision)
            { lastNoticeRevision = game.NoticeRevision; noticeUntil = Time.unscaledTime + 4.5f; }
            toast.gameObject.SetActive(!blocked && Time.unscaledTime < noticeUntil);
            target.gameObject.SetActive(!blocked && !ManagementOpen && SelectedFacility == 0);
            switch (SelectedFacility)
            {
                case 5:facilitySummary.text=$"床位4 · 供电 {m.SelectedFacility(16).PowerRatio:P0}\n住房总容量 {m.HousingCapacity}";break;
                case 6:facilitySummary.text=$"照明供电 {m.SelectedFacility(32).PowerRatio:P0}\n覆盖中心距6格内的住房";break;
                case 1:
                    facilitySummary.text = m.ProductionDiagnosis(m.SelectedFacility(1)); break;
                case 2:
                    facilitySummary.text = m.DismantleBuilt ? $"{PrototypeGame.StateName(m.DismantleState)}\n当前拆解：{ItemCatalog.Name(m.SelectedDevice)}" : "待建设\n需要废木料 4 + 紧固件 2"; break;
                case 3:
                    facilitySummary.text = m.TableBuilt ? $"装配：{ItemCatalog.Name(m.AssemblyProduct)} · {m.AssemblyProgress:P0}\n木柄 1 + {ItemCatalog.Name(m.AssemblyEnd)} 1" : "待建设\n需要木板 4 + 钢铁零件 2"; break;
                case 4:
                    facilitySummary.text = m.GeneratorBuilt ? m.ProductionDiagnosis(m.SelectedFacility(8)) : "待建设 · 钢铁零件 6 + 电线 2\n热动力组件 1 + 发电组件 1"; break;
            }
        }

        public void GoToSelectedFacility()
        {
            if (game.Model.Tutorial.IntroOpen)
            { game.Report(new ActionResult(false, "先选择开始教程或自由经营，再前往作业点。")); return; }
            switch (SelectedFacility)
            {
                case 5:case 6:case 7:case 8:var side=PrototypeSimulation.ServiceCell(game.Model.SelectedFacility(FacilityKind(SelectedFacility)));game.GoTo(side.x,side.y);break;
                case GenericFacilityPage:var generic=game.Model.GetFacility(genericFacilityId);if(generic!=null){var front=PrototypeSimulation.ServiceCell(generic);game.GoTo(front.x,front.y);}break;
                case 1: game.GoTo(game.Model.BenchFrontX, game.Model.BenchFrontY); break;
                case 2: game.GoTo(game.Model.DismantleFrontX, game.Model.DismantleFrontY); break;
                case 3: game.GoTo(game.Model.TableFrontX, game.Model.TableFrontY); break;
                case 4: game.GoTo(game.Model.GeneratorFrontX, game.Model.GeneratorFrontY); break;
            }
        }
    }
}
