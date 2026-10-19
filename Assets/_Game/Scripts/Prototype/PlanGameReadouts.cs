using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private Text contextLiveText,contextPeopleText,payrollStatus;
        private readonly Dictionary<int,Text> workerReadouts=new Dictionary<int,Text>();
        private int contextSiteId;
        private RectTransform contextOutputs,riskStrip;
        private readonly Dictionary<string,Button> outputButtons=new Dictionary<string,Button>();
        private readonly List<Button> riskButtons=new List<Button>();
        private sealed class RiskNotice {public string key,caption,description;public int priority;public Action open;}
        private List<RiskNotice> riskNotices=new List<RiskNotice>();
        private static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0,seconds))).ToString(@"mm\:ss");
        private static string ToolFamily(string family)
        {switch(family){case "broom":return "扫帚";case "file":return "锉刀";case "hammer":return "锤子";case "handsaw":return "手锯";case "scissors":return "剪刀";case "screwdriver":return "螺丝刀";case "sewing":return "缝纫工具";case "tongs":return "夹钳";case "wrench":return "扳手";default:return "所需工具";}}
        private string PayrollDescription()
        {
            var p=Model.InspectPayroll();
            return (p.scheduled?"下次发薪 "+Clock(p.remaining)+" 后，预计 "+(p.expected/100m).ToString("0.00")+"钱":"尚无员工发薪计划")+"\n已到期欠薪 "+(p.unpaid/100m).ToString("0.00")+"钱 · 需手动支付";
        }
        private string FacilityDescription(PlanSite s)
        {
            var d=Model.InspectFacility(s);var f=Model.Config.Facility(s.type);
            string text=(Model.State.paused?"已暂停 · ":"")+d.status;string progression=Model.ProgressionDescription(s);if(progression!="")text+="\n"+progression;
            if(s.type=="warehouse")return text+"\n仓储 "+s.input.Used(Model.Config).ToString("0.##")+" / "+CapacityLabel(s.input.capacity)+"\n操作侧 "+Model.Front(s);
            if(s.type=="belt"||s.type=="port")
            {
                var flow=Model.InspectTransport(s);text+="\n"+flow.flow+"\n实体缓存 "+s.input.stacks.Count+" / 1";
                if(s.input.stacks.Count>0)text+=" · "+string.Join("、",s.input.stacks.Select(a=>Model.Config.Item(a.item).name+"×"+(a.ticks/(double)PlanStock.Scale).ToString("0.##")));
                if(flow.machine>0)text+="\n绑定："+Model.Config.Facility(Model.Site(flow.machine).type).name+" #"+flow.machine;
                if(flow.nextSite>0)text+="\n下游 #"+flow.nextSite;
                text+="\n过滤："+(string.IsNullOrEmpty(s.filter)?"不限物品":Model.Config.Item(s.filter).name);
                if(s.type=="belt")text+="\n供电 "+s.powerRatio.ToString("0%")+" · 输送进度 "+Math.Min(1,s.beltProgress).ToString("0%")+(s.crossing?"\n交叉处按轴独立输送":"\n可接收相邻传送带合流");
                return text;
            }
            if(!string.IsNullOrEmpty(s.recipe))text+="\n"+Model.Config.Recipe(s.recipe).name+"\n"+d.phase+" "+d.progress.ToString("0.#")+" / "+d.duration.ToString("0.#")+"秒";
            if(!string.IsNullOrEmpty(s.pendingRecipe))text+="\n下一批改用："+Model.Config.Recipe(s.pendingRecipe).name;
            text+="\n输入含在制 "+PlanStock.CombinedUsed(Model.Config,s.input,s.reserved).ToString("0.##")+" / "+CapacityLabel(s.input.capacity)+"；输出 "+s.output.Used(Model.Config).ToString("0.##")+" / "+CapacityLabel(s.output.capacity);
            if(d.missing.Length>0)text+="\n缺额："+Amounts(d.missing);
            if(d.workersNeeded>0)text+="\n操作工 到岗 "+d.workersHere+" / 需求 "+d.workersNeeded+"（已安排 "+d.workersAssigned+"）";
            if(d.workersNeeded>0)foreach(var t in d.tools)text+="\n工具："+(t.item==null?ToolFamily(t.family)+" 缺少可用工具":Model.Config.Item(t.item).name+" 耐久 "+t.durability.ToString("0.#")+" / "+t.maximum.ToString("0.#"));
            if(d.powerDemand>0)text+="\n供电 "+s.powerRatio.ToString("0%")+" · 当前需求 "+d.powerDemand.ToString("0.##");
            if(f.hasDurability)text+="\n耐久 "+s.durability.ToString("0.#")+" / 100 · "+(s.maintainer>0?"维护者 #"+s.maintainer:"未安排维护");
            text+="\n操作侧 "+Model.Front(s);
            if(PlanSimulation.IsWaterIntake(s.type))text+="\n"+Model.WaterSourceDescription(s)+"\n原水需要净化后饮用。";
            return text;
        }
        private static float ReadoutHeight(Text text)=>Mathf.Ceil(text.preferredHeight)+8;
        private string PersonDescription(PlanPerson p)=>p==null?"人物已离开":(p.id==0?"主角":"员工 #"+p.id)+" · 幸福度 "+p.happiness.ToString("0")+" · 财产 "+(p.wallet/100m).ToString("0.00")+"\n人效 "+Model.PersonEfficiency(p).ToString("0%")+" · "+Model.WorkerTask(p);
        private string SelectedPeopleDescription()=>string.Join("\n\n",selectedPeople.Take(12).Select(id=>PersonDescription(Model.Person(id))));
        private void AddPeopleReadout()
        {contextPeopleText=Text(contextBody,"Live people",SelectedPeopleDescription(),4,contextY,302,10,15);float height=ReadoutHeight(contextPeopleText);contextPeopleText.rectTransform.sizeDelta=new Vector2(302,height);contextY+=height+5;}
        // Resize only the information block: later controls shift, the dragged window and scroll do not reset.
        private float ResizeReadout(RectTransform block,float height)
        {
            float delta=height-block.sizeDelta.y;if(Mathf.Abs(delta)<.1f)return 0;
            var parent=block.parent;int index=block.GetSiblingIndex();
            for(int i=index+1;i<parent.childCount;i++){var sibling=parent.GetChild(i) as RectTransform;if(sibling!=null)sibling.anchoredPosition+=new Vector2(0,-delta);}
            block.sizeDelta=new Vector2(block.sizeDelta.x,height);return delta;
        }
        private void AddContextReadout(PlanSite s)
        {
            contextSiteId=s.id;contextLiveText=Text(contextBody,"Live facility",FacilityDescription(s),4,contextY,302,10,15);
            float height=ReadoutHeight(contextLiveText);contextLiveText.rectTransform.sizeDelta=new Vector2(302,height);contextY+=height+5;
        }
        private void AddContextOutputs(PlanSite s)
        {contextOutputs=Rect(contextBody,"Live outputs",0,contextY,314,0);RefreshOutputButtons(s);}
        private void RefreshOutputButtons(PlanSite s)
        {
            var stock=s.type=="warehouse"?s.input:s.output;var ids=stock.stacks.Select(a=>a.item).Distinct().ToArray();
            foreach(var id in outputButtons.Keys.Where(id=>!ids.Contains(id)).ToArray()){Remove(outputButtons[id].gameObject);outputButtons.Remove(id);}
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];if(!outputButtons.TryGetValue(id,out var button))
                {button=Button(contextOutputs,"取1："+id,4,i*42,302,36,()=>{CloseContext();Act(()=>Model.TakeOutput(s,id,Math.Min(PlanStock.Scale,stock.Count(id))));});outputButtons.Add(id,button);}
                button.GetComponent<RectTransform>().anchoredPosition=new Vector2(4,-i*42);
                button.GetComponentInChildren<Text>().text="取1："+Model.Config.Item(id).name+"（共"+(stock.Count(id)/(double)PlanStock.Scale).ToString("0.##")+"）";
            }
            contextY+=ResizeReadout(contextOutputs,ids.Length*42);
        }
        private void RefreshReadouts()
        {
            var s=Model.Site(selectedSite);
            if(siteStatus!=null&&s!=null){siteStatus.text=FacilityDescription(s);rowY+=ResizeReadout(siteStatus.rectTransform,ReadoutHeight(siteStatus));panelContent.sizeDelta=new Vector2(384,Mathf.Max(614,rowY+20));}
            if(payrollStatus!=null)payrollStatus.text=PayrollDescription();
            foreach(var pair in workerReadouts){pair.Value.text=PersonDescription(Model.Person(pair.Key));rowY+=ResizeReadout(pair.Value.rectTransform,ReadoutHeight(pair.Value));}
            if(workerReadouts.Count>0)panelContent.sizeDelta=new Vector2(384,Mathf.Max(614,rowY+20));
            if(ContextOpen&&contextPeopleText!=null){contextPeopleText.text=SelectedPeopleDescription();contextY+=ResizeReadout(contextPeopleText.rectTransform,ReadoutHeight(contextPeopleText));}
            if(ContextOpen&&contextLiveText!=null)
            {
                var current=Model.Site(contextSiteId);if(current==null){CloseContext();Notice="该设施已不存在。";return;}
                contextLiveText.text=FacilityDescription(current);contextY+=ResizeReadout(contextLiveText.rectTransform,ReadoutHeight(contextLiveText));
                if(contextOutputs!=null)RefreshOutputButtons(current);
            }
            if(ContextOpen&&(contextLiveText!=null||contextPeopleText!=null))
            {
                float height=Mathf.Min(contextY+80,Mathf.Min(570,((RectTransform)planCanvas).rect.height-32));contextMenu.sizeDelta=new Vector2(330,height);
                contextBody.sizeDelta=new Vector2(314,contextY);((RectTransform)contextBody.parent).sizeDelta=new Vector2(314,height-74);
                var canvas=(RectTransform)planCanvas;var pos=contextMenu.anchoredPosition;
                contextMenu.anchoredPosition=new Vector2(Mathf.Clamp(pos.x,8,canvas.rect.width-338),-Mathf.Clamp(-pos.y,8,canvas.rect.height-height-8));
            }
        }
        private void BuildRiskStrip()
        {
            riskButtons.Clear();riskStrip=Rect(planCanvas,"Risk alerts",24,142,1134,38);
            for(int i=0;i<4;i++){int slot=i;riskButtons.Add(Button(riskStrip,"风险提示",i*284,0,276,38,()=>OpenRiskSlot(slot)));}
            riskStrip.gameObject.SetActive(false);
        }
        private void LocateSite(int id)
        {
            var s=Model.Site(id);if(s==null){Notice="该设施已不存在。";return;}
            CancelMapGesture();CancelPlacement();HideManagement();selectedSite=id;mapOverview=false;
            mapCamera.transform.position=new Vector3(s.x,s.y,-10);mapCamera.orthographicSize=Mathf.Min(mapCamera.orthographicSize,20);
            OpenWorldContext(s.x,s.y,new Vector2(Screen.width*.55f,Screen.height*.72f),false,s.id);
        }
        private void OpenSupplyRisk(string item)
        {
            CancelMapGesture();CancelPlacement();var p=Model.Alive.Concat(new[]{Model.State.player}).FirstOrDefault(a=>!a.dead&&(item=="water"?a.waterDebt:a.foodDebt)>1e-8);
            if(p!=null)mapCamera.transform.position=new Vector3((float)p.x,(float)p.y,-10);
            else
            {
                var g=Model.State.ground.FirstOrDefault(a=>a.stock.Count(item)>0);
                var site=Model.State.sites.FirstOrDefault(a=>!string.IsNullOrEmpty(a.recipe)&&Model.Config.Recipe(a.recipe).outputs.Any(o=>o.item==item));
                if(g!=null)mapCamera.transform.position=new Vector3(g.x,g.y,-10);else if(site!=null)mapCamera.transform.position=new Vector3(site.x,site.y,-10);
            }
            ShowManagement(4);Notice="查看生活供应与库存；1轮为每人一份，仅统计仓库，未计外购、背包和在制品。";
        }
        private void OpenBlockedRisk()=>OpenStoppedSites(null);
        private void OpenToolRisk()=>OpenStoppedSites("tool");
        private void OpenStoppedSites(string issue)
        {
            CancelMapGesture();CancelPlacement();BeginContext(issue=="tool"?"缺工具设施":"停机设施",new Vector2(Screen.width*.5f,Screen.height*.75f));
            var sites=Model.State.sites.Where(s=>s.enabled&&!s.pending&&!string.IsNullOrEmpty(s.recipe)).Select(s=>new{s,d=Model.InspectFacility(s)}).Where(a=>a.d.blocked&&(issue==null||a.d.issue==issue)).ToArray();
            ContextLine("共 "+sites.Length+" 处。列表最多显示12处；点击定位查看缺额、工具和供电。",64);
            foreach(var pair in sites.Take(12)){int id=pair.s.id;ContextAction(Model.Config.Facility(pair.s.type).name+" #"+id,()=>LocateSite(id));ContextLine(pair.d.status,52);}
            EndContext();
        }
        private List<RiskNotice> CollectRisks()
        {
            var list=new List<RiskNotice>();
            foreach(string item in new[]{"water","food"})
            {
                string id=item;var d=Model.InspectSupply(item);if(d.affected==0&&d.cycles>=1)continue;string name=item=="water"?"饮水":"食物";
                list.Add(new RiskNotice{key=item,priority=d.affected>0?(item=="water"?0:1):4,caption=d.affected>0?name+"不足 · "+d.affected+"人":name+"库存不足1轮",description="仓库覆盖 "+d.cycles.ToString("0.0")+"轮；未计外购、背包、原料和在制品。",open=()=>OpenSupplyRisk(id)});
            }
            var pay=Model.InspectPayroll();if(pay.unpaid>0||pay.scheduled&&pay.remaining<=120)
                list.Add(new RiskNotice{key="payroll",priority=pay.unpaid>0?2:3,caption=pay.unpaid>0?"欠薪 "+(pay.unpaid/100m).ToString("0.00")+"钱":"发薪将至 "+Clock(pay.remaining),description=PayrollDescription(),open=()=>{CancelMapGesture();CancelPlacement();ShowManagement(4);}});
            var homes=Model.UnpoweredHomes();if(homes.Length>0)list.Add(new RiskNotice{key="homes",priority=5,caption="住房供电不足 · "+homes.Length+"处",description="已入住住房供电低于50%，会影响生活。",open=()=>LocateSite(homes[0])});
            int tools=Model.State.sites.Count(s=>s.enabled&&!s.pending&&!string.IsNullOrEmpty(s.recipe)&&Model.InspectFacility(s).issue=="tool");
            if(tools>0)list.Add(new RiskNotice{key="tools",priority=5,caption="缺少工具 · "+tools+"处",description="损坏的工具无法继续工作。补充工具或让主角携带；点击查看设施，可查找工具制作方法。",open=OpenToolRisk});
            int blocked=Model.State.sites.Count(s=>s.enabled&&!s.pending&&!string.IsNullOrEmpty(s.recipe)&&Model.InspectFacility(s).blocked);
            if(blocked>=3)list.Add(new RiskNotice{key="production",priority=6,caption="多处停机 · "+blocked+"处",description="检查原料、工具、人员、输出和供电；手动停工不计入。",open=OpenBlockedRisk});
            return list.OrderBy(a=>a.priority).ToList();
        }
        private void OpenRiskSlot(int index)
        {
            riskNotices=CollectRisks();if(riskNotices.Count>4&&index==3)
            {
                CancelMapGesture();CancelPlacement();
                BeginContext("当前提醒",new Vector2(Screen.width*.5f,Screen.height*.75f));
                foreach(var r in riskNotices){var notice=r;ContextAction(r.caption,()=>notice.open());ContextLine(r.description,72);}EndContext();return;
            }
            if(index<riskNotices.Count)riskNotices[index].open();
        }
        private void RefreshRisks()
        {
            if(riskStrip==null)return;riskNotices=CollectRisks();riskStrip.gameObject.SetActive(!MenuOpen&&!SaveDialogOpen&&riskNotices.Count>0);
            for(int i=0;i<riskButtons.Count;i++)
            {
                var b=riskButtons[i];b.gameObject.SetActive(i<riskNotices.Count);if(i>=riskNotices.Count)continue;
                b.GetComponentInChildren<Text>().text=riskNotices.Count>4&&i==3?"还有 "+(riskNotices.Count-3)+" 项提醒":riskNotices[i].caption;
                b.image.color=riskNotices[i].priority<=2?new Color32(115,51,42,255):new Color32(88,72,42,255);
            }
        }
    }
}
