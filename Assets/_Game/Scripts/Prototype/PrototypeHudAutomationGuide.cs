using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        private FacilityInstance GuideMachine=>game.Model.Facilities.FirstOrDefault(f=>f.Built&&PrototypeSimulation.IsWoodMachine(f.Kind));
        private int AutomationStage
        {
            get
            {
                var f=GuideMachine;if(f==null)return 0;if(f.MachineBatches==0)return 1;
                return f.MachineBatches>=3&&game.Model.ConveyorDeliveredItems>=20&&game.Model.HasMachineConveyorLoop(f.Id)?3:2;
            }
        }
        private void BeginAutomationGuide()
        {
            var t=game.Model.Tutorial;t.automationStarted=true;t.automationDismissed=false;t.active=true;
            ClosePanels();game.Report(new ActionResult(true,"自动化入门已开始；按提示建设，进度随存档保存。"));
        }
        private void RefreshAutomationCoach(PrototypeSimulation m)
        {
            int stage=AutomationStage;var f=GuideMachine;
            guideCaption.text=stage==3?"自动化入门已完成":"自动化入门  "+(stage+1)+" / 3";
            objective.text=new[]{"建起第一台电动木工","接通电力，完成第一批","连接仓库，形成输送循环","产线已经能连续运转"}[stage];
            objective.rectTransform.sizeDelta=new Vector2(398,54);
            guideProgress.rectTransform.anchorMax=new Vector2(stage/3f,1);
            guideProgress.transform.parent.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-106);
            guideBody.anchoredPosition=new Vector2(0,-122);guideBody.gameObject.SetActive(!guideCollapsed);
            guideCollapse.GetComponentInChildren<Text>().text=guideCollapsed?"展开":"收起";
            guideChecklist.text=f==null?"电机与机用锯片解锁方案\n在建材采购页核对数量与缺口":$"电动加工 {Mathf.Min(3,f.MachineBatches)}/3批 · 输送 {System.Math.Min(20,m.ConveyorDeliveredItems)}/20件";
            guideInstruction.text=stage==0?"先查看建材，备齐后在建设区放置机器。金色是作业面，蓝色是维护侧，两侧都要留通路。":stage==1?m.ProductionDiagnosis(f):stage==2?"仓库输出过滤为废木料，接机器输入；机器输出接仓库输入。接口外侧接传送带，带格也要通电。木板与碎屑都需有去处。":"已完成连续加工与真实输送。继续照看工资、生活物资、燃料和维护；自动木工可用整理木料进一步升级。";
            guideStatus.text=m.Paused?"已暂停 · 空格继续":stage==0?"缺材料时可逐项采购；不会自动扣款。":$"耐久 {f.Durability:0.#}/100 · 供电 {f.PowerRatio:P0} · 空格暂停";
            guideAction.GetComponentInChildren<Text>().text=stage==3?"完成，继续经营":m.Paused?"继续游戏":stage==0?m.HasBuildMaterials(64)&&m.ElectricWoodUnlocked?"放置电动木工":"查看所需建材":stage==1?!m.ConnectedToGenerator(f.Id)?"铺线连接发电机":!f.RunRequested?"启动机器并自动分岗":"查看机器与停产原因":"设置接口 / 输送线路";
            guideAction.interactable=true;guideLocate.interactable=f!=null;
            guideLocate.GetComponentInChildren<Text>().text="定位机器";
            guideChecklist.rectTransform.anchoredPosition=new Vector2(16,0);guideChecklist.rectTransform.sizeDelta=new Vector2(398,48);
            guideInstruction.rectTransform.anchoredPosition=new Vector2(16,-56);
            float instructionHeight=Mathf.Max(100,guideInstruction.preferredHeight);guideInstruction.rectTransform.sizeDelta=new Vector2(398,instructionHeight);
            float statusY=66+instructionHeight;guideStatus.rectTransform.anchoredPosition=new Vector2(16,-statusY);
            guideAction.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-(statusY+36));guideLocate.GetComponent<RectTransform>().anchoredPosition=new Vector2(274,-(statusY+36));
            guideBody.Find("OpenGuide").GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-(statusY+86));
            float height=guideCollapsed?122:122+statusY+126;guidePrompt.sizeDelta=new Vector2(430,height);guidePrompt.anchoredPosition=new Vector2(16,130+height);
        }
        private void FocusAutomationGuide()
        {var f=GuideMachine;if(f!=null)game.GetComponent<PrototypeWorld>().FocusPoint(f.X,f.Y,7);}
        private void ExecuteAutomationGuide()
        {
            var m=game.Model;var world=game.GetComponent<PrototypeWorld>();var f=GuideMachine;
            if(m.Paused){m.SetPaused(false);return;}
            if(AutomationStage==3){game.SkipTutorial();return;}
            if(f==null){if(m.HasBuildMaterials(64)&&m.ElectricWoodUnlocked)world.BeginPlacement(64);else OpenShop(64);return;}
            if(!m.ConnectedToGenerator(f.Id)){world.BeginWire();FocusAutomationGuide();return;}
            if(!f.RunRequested){m.SetAutomaticMachineStaffing(true);game.Report(m.ToggleMachine(f.Id));return;}
            if(f.MachineBatches==0){ShowOwnedFacility(f);return;}
            if(m.Warehouse.Available(ItemId.SteelPart)+m.Carry.Available(ItemId.SteelPart)<1){OpenShop();return;}
            if(!m.ConveyorPorts.Any(p=>p.OwnerId==-1&&p.Output)){world.BeginPort(-1,true);return;}
            if(!m.ConveyorPorts.Any(p=>p.OwnerId==f.Id&&!p.Output)){world.BeginPort(f.Id,false);return;}
            if(!m.ConveyorPorts.Any(p=>p.OwnerId==f.Id&&p.Output)){world.BeginPort(f.Id,true);return;}
            if(!m.ConveyorPorts.Any(p=>p.OwnerId==-1&&!p.Output)){world.BeginPort(-1,false);return;}
            world.BeginConveyor();
        }
    }
}
