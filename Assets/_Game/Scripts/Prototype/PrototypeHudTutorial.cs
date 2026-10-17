using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [SerializeField] private RectTransform tutorialWelcome, guideBody;
        [SerializeField] private Text guideCaption, guideChecklist, guideInstruction, guideStatus;
        [SerializeField] private Image guideProgress;
        [SerializeField] private Button guideAction, guideLocate, guideCollapse;
        private bool guideCollapsed;
        private int lastGuideStep = -1;
        private float guideCelebrationUntil;

        private void BuildTutorialWelcome()
        {
            tutorialWelcome = StretchPanel(root, "Tutorial Welcome", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.045f, 0.055f, 0.06f, 0.83f));
            tutorialWelcome.GetComponent<Image>().raycastTarget = true;
            var card = Surface(tutorialWelcome, "Card", 0, 0, 860, 568, Panel);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            Stripe(card, "Accent", 0, 0, 860, 3, Accent);
            TextAt(card, "Eyebrow", "新手入门    /    从第一把手锯开始", 32, 24, 796, 24, 14, Accent);
            TextAt(card, "Title", "一起建起第一座工坊", 32, 66, 796, 48, 32, Ink);
            TextAt(card, "Introduction", "拾取废料，亲手加工，再让居民接手生产。\n每次只跟着一个提示行动，完成后会自动进入下一步。", 32, 128, 796, 60, 18, Ink);

            string[] headings = { "01   移动与查看", "02   搬运与生产", "03   按自己的节奏" };
            string[] instructions = {
                "左键拖动：平移地图\n左键短按：查看用途\nWASD / 右键：走路",
                "E：拾取 / 交互\nQ：背包物品入库\nF：进入木工岗位",
                "空格：暂停 / 继续\n1 / 2 / 3：调整倍速\nH：查看课程与跳过"
            };
            for (int i = 0; i < headings.Length; i++)
            {
                var controls = Surface(card, "Controls " + i, 32 + i * 270, 216, 256, 144, Inset);
                TextAt(controls, "Title", headings[i], 16, 12, 224, 28, 17, Accent);
                TextAt(controls, "Keys", instructions[i], 16, 48, 224, 84, 16, Ink);
            }
            TextAt(card, "Route", "先认识两个洞、旧仓库和旧住房；新物品会分批落下。", 32, 374, 796, 28, 17, Muted);
            TextAt(card, "Time", "准备期间时间停止。进度随游戏存档保存，可以随时继续。", 32, 414, 796, 28, 16, Green);
            ButtonAt(card, "BeginGuide", "先认识这片地方", 32, 474, 388, 52, true);
            ButtonAt(card, "DismissGuide", "跳过，自由经营", 440, 474, 388, 52);
            tutorialWelcome.gameObject.SetActive(false);
        }

        private void BuildTutorialCoach()
        {
            guidePrompt = Surface(root, "Guide Prompt", 16, 0, 430, 366, new Color32(26, 31, 34, 246));
            guidePrompt.anchorMin = guidePrompt.anchorMax = Vector2.zero;
            guidePrompt.anchoredPosition = new Vector2(16, 496);
            guidePrompt.GetComponent<Image>().raycastTarget = true;
            guideCaption = TextAt(guidePrompt, "Caption", "新手引导", 16, 12, 290, 24, 14, Accent);
            guideCollapse = ButtonAt(guidePrompt, "GuideCollapse", "收起", 346, 10, 68, 28);
            objective = TextAt(guidePrompt, "Objective", "", 16, 42, 398, 54, 21, Ink);
            objective.alignment = TextAnchor.UpperLeft;
            guideProgress = Bar(guidePrompt, "Course Progress", 16, 106, 398, 4, Accent);
            guideBody = At(guidePrompt, "Body", 0, 122, 430, 244);
            guideChecklist = TextAt(guideBody, "Checklist", "", 16, 0, 398, 44, 16, Green);
            guideChecklist.alignment = TextAnchor.UpperLeft;
            guideInstruction = TextAt(guideBody, "Instruction", "", 16, 52, 398, 76, 16, Ink);
            guideInstruction.alignment = TextAnchor.UpperLeft;
            guideStatus = TextAt(guideBody, "Status", "", 16, 138, 398, 22, 13, Muted);
            guideAction = ButtonAt(guideBody, "GuideAction", "拾取手锯", 16, 174, 248, 40, true);
            guideLocate = ButtonAt(guideBody, "GuideLocate", "定位按钮", 274, 174, 140, 40);
            ButtonAt(guideBody, "OpenGuide", "课程详情 / 跳过  H", 16, 224, 398, 28);
        }

        private void RefreshTutorialCoach(PrototypeSimulation m)
        {
            bool visible = !game.MenuOpen && !game.ResultVisible && !m.Failed;
            RefreshKnowledge(m);
            tutorialWelcome.gameObject.SetActive(visible && m.Tutorial.IntroOpen && m.Tutorial.orientation==0);
            guidePrompt.gameObject.SetActive(visible && m.Tutorial.active && m.Tutorial.started && (!KnowledgeOpen||KnowledgeIsPassive) && !InventoryOpen && !ShopOpen);
            if (!guidePrompt.gameObject.activeSelf) return;
            if(m.Tutorial.Completed&&m.Tutorial.automationStarted&&!m.Tutorial.automationDismissed){RefreshAutomationCoach(m);return;}

            int step = m.Tutorial.step;
            if (step != lastGuideStep)
            {
                // Loading a save is not a newly completed lesson.
                guideCelebrationUntil = lastGuideStep > 0 && step > lastGuideStep ? Time.unscaledTime + 4 : 0;
                lastGuideStep = step;
            }
            bool completed = m.Tutorial.Completed;
            guideCaption.text = completed ? "全部 12 步已完成" :
                Time.unscaledTime < guideCelebrationUntil ? $"上一步已完成！  继续第 {step} 步" : $"新手引导    {step:00} / 12";
            objective.text = PrototypeTutorial.Titles[step];
            float titleHeight = Mathf.Max(54, objective.preferredHeight);
            objective.rectTransform.sizeDelta = new Vector2(398, titleHeight);
            float bodyY = 42 + titleHeight + 26;
            guideProgress.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -(bodyY - 16));
            guideBody.anchoredPosition = new Vector2(0, -bodyY);
            guideProgress.rectTransform.anchorMax = new Vector2(completed ? 1 : Mathf.Clamp01((step - 1) / 12f), 1);
            guideBody.gameObject.SetActive(!guideCollapsed);
            guideCollapse.GetComponentInChildren<Text>().text = guideCollapsed ? "展开" : "收起";
            float height = guideCollapsed ? bodyY : 390;
            guidePrompt.sizeDelta = new Vector2(430, height);
            guidePrompt.anchoredPosition = new Vector2(16, 130 + height);
            if (guideCollapsed) return;

            guideChecklist.text = PrototypeTutorial.ProgressSummary(m, step);
            guideChecklist.rectTransform.sizeDelta = new Vector2(398, Mathf.Max(44, guideChecklist.preferredHeight));
            float instructionY = guideChecklist.rectTransform.sizeDelta.y + 8;
            guideInstruction.rectTransform.anchoredPosition = new Vector2(16, -instructionY);
            var action = PrototypeTutorial.NextAction(m, step);
            bool travelling = action.hasTarget && !action.AtTarget(m) && game.Walking &&
                game.WalkTargetX == action.x && game.WalkTargetY == action.y;
            guideInstruction.text = completed ? "基础经营已学会，可以继续学习电动机器和输送。\n也可选择自由经营，按 H 随时重新进入。" :
                m.Paused ? "游戏已暂停。点击下方“继续游戏”或按空格，再继续当前操作。" :
                travelling ? "正在前往金色目标。到达后，再点击下方按钮完成操作。WASD 或 Esc 可以取消行走。" : action.instruction;
            guideInstruction.rectTransform.sizeDelta = new Vector2(398, Mathf.Max(76, guideInstruction.preferredHeight));
            // Instructions can grow with large balances or localized item names.
            float statusY = instructionY + guideInstruction.rectTransform.sizeDelta.y + 10;
            guideStatus.rectTransform.anchoredPosition = new Vector2(16, -statusY);
            guideAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -(statusY + 36));
            guideLocate.GetComponent<RectTransform>().anchoredPosition = new Vector2(274, -(statusY + 36));
            guideBody.Find("OpenGuide").GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -(statusY + 86));
            height = bodyY + statusY + 126;
            guidePrompt.sizeDelta = new Vector2(430, height);
            guidePrompt.anchoredPosition = new Vector2(16, 130 + height);
            guideStatus.text = completed ? "教程进度已记录 · 可继续经营" :
                m.MissingSupplyPeople > 0 ? "有人缺供：点左上角警报补充食物和饮水" :
                m.UnpaidWageCents > 0 ? "工资到期：点左上角警报处理欠薪" :
                $"背包 {m.Carry.Used:0.##} / {m.Carry.Capacity}" + (action.hasTarget ? $"    目标 ({action.x}, {action.y})" : "    空格暂停 · 3 倍速");
            guideStatus.color = m.MissingSupplyPeople > 0 || m.UnpaidWageCents > 0 ? Danger : Muted;
            guideAction.GetComponentInChildren<Text>().text = completed ? "完成入门，学习自动化" : m.Paused ? "继续游戏  空格" :
                travelling ? "正在前往…" : action.hasTarget && !action.AtTarget(m) ? "前往：" + action.label : action.label;
            guideAction.interactable = completed || m.Paused || !travelling && !m.Interacting && action.operation != TutorialOperation.Wait;
            guideLocate.GetComponentInChildren<Text>().text=completed?"自由经营":step==12?"可选：技术融资":"定位按钮";
            guideLocate.interactable = completed || action.button != null||step==12;
        }
    }
}
