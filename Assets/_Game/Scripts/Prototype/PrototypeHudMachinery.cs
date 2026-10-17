using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [SerializeField] private RectTransform transportPanel;
        [SerializeField] private Text transportTitle,transportInfo;
        [SerializeField] private Image transportArtwork;
        private int inspectedBelt,inspectedPort;
        private float AddAutomationButtons(Transform parent,float y)
        {
            SectionHeading(parent,"电动生产与输送","01",y);y+=40;
            ButtonAt(parent,"PlaceKind64","新建电动木工",12,y,166,36);ButtonAt(parent,"PlaceKind128","新建自动木工",188,y,166,36);y+=44;
            ButtonAt(parent,"LayConveyor","铺传送带",12,y,166,36);ButtonAt(parent,"EraseConveyor","拆输送设施",188,y,166,36);y+=44;
            ButtonAt(parent,"WarehouseOutPort","仓库输出接口",12,y,166,36);ButtonAt(parent,"WarehouseInPort","仓库输入接口",188,y,166,36);y+=44;
            ButtonAt(parent,"MachineAutoStaff","机器自动填岗：开",12,y,342,36);return y+48;
        }
        private void BuildMachineryHud(bool upgrade)
        {
            if(upgrade)
            {
                var content=pages[0].GetComponent<ScrollRect>().content;var old=content.Cast<Transform>().OfType<RectTransform>().ToArray();
                float shift=AddAutomationButtons(content,360)-360;foreach(var rect in old)if(rect.anchoredPosition.y<=-350)rect.anchoredPosition-=new Vector2(0,shift);
                foreach(int i in Enumerable.Range(0,onsiteSectionY.Length))if(onsiteSectionY[i]>=350)onsiteSectionY[i]+=shift;content.sizeDelta+=new Vector2(0,shift);
            }

            if(facilityPages.Length<8)Array.Resize(ref facilityPages,8);
            for(int i=6;i<8;i++)
            {
                var page=At(facilityPanel,"Machine "+i,0,204,396,384);facilityPages[i]=page;
                ButtonAt(page,"MachineRun"+i,"开始 / 停止生产",20,0,356,38,true);
                TextAt(page,"Machine Detail","人工准备10秒 + 机器15秒；自动机仅机器阶段。\n使用现场输入输出；缺电、维护不足会保留进度。",20,46,356,54,14,Muted);
                string[] labels={"投适用原料","取出产物","主角操作","前往维护侧","主角维护","安排/释放维护","安排/释放操作","取回未预约料","取消未完批次","自动填岗开关"};
                string[] commands={"MachineFeed","MachineTake","MachineOperate","MachineGoMaintain","MachineMaintain","MachineAssignMaintain","MachineAssignOperate","MachineTakeInput","MachineCancel","MachineAutoStaff"};
                for(int j=0;j<labels.Length;j++)ButtonAt(page,commands[j]+(commands[j]=="MachineAutoStaff"?"":"@"+i),labels[j],20+j%2*184,112+j/2*46,172,36);
                page.gameObject.SetActive(false);
            }
            facilityPanel.sizeDelta=new Vector2(396,712);
            ButtonAt(facilityPanel,"OwnerInPort","新增输入接口",20,660,172,36);ButtonAt(facilityPanel,"OwnerOutPort","新增输出接口",204,660,172,36);
            transportPanel=Surface(root,"Transport Inspection",0,86,396,548,Panel);transportPanel.anchorMin=transportPanel.anchorMax=Vector2.one;transportPanel.anchoredPosition=new Vector2(-412,-86);
            transportPanel.GetComponent<Image>().raycastTarget=true;
            transportTitle=TextAt(transportPanel,"Title","输送设施",20,18,302,40,24,Ink);ButtonAt(transportPanel,"CloseTransport","×",350,18,28,30);
            Artwork(transportPanel,"Artwork",game.appearance.conveyor,20,72,72,72);transportArtwork=transportPanel.Find("Artwork").GetComponent<Image>();
            transportInfo=TextAt(transportPanel,"Info","",20,156,356,190,15,Muted);transportInfo.alignment=TextAnchor.UpperLeft;
            ButtonAt(transportPanel,"TransportEnable","启用 / 停止",20,354,356,36,true);
            ButtonAt(transportPanel,"TransportFilterPrevious","上一种过滤",20,402,172,36);ButtonAt(transportPanel,"TransportFilterNext","下一种过滤",204,402,172,36);
            ButtonAt(transportPanel,"TransportFilterAll","允许全部 / 切换交叉线",20,448,356,36);
            ButtonAt(transportPanel,"TransportRemove","拆除并返还",20,494,356,36);transportPanel.gameObject.SetActive(false);
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
        }
        public void ShowTransport(int beltId,int portId)
        {ClosePanels();inspectedBelt=beltId;inspectedPort=portId;transportPanel.gameObject.SetActive(true);RefreshMachineryHud(game.Model);}
        private int SelectedMachineId=>SelectedFacility==7?game.Model.SelectedFacility(64).Id:SelectedFacility==8?game.Model.SelectedFacility(128).Id:0;
        private void RefreshMachineryHud(PrototypeSimulation m)
        {
            foreach(var b in buttonBindings)
            {
                if(b.command=="PlaceKind64"){b.button.interactable=m.ElectricWoodUnlocked;b.button.GetComponentInChildren<Text>().text=m.ElectricWoodUnlocked?"新建电动木工":"电动木工 · 需电机与机锯片";}
                if(b.command=="PlaceKind128"){b.button.interactable=m.AutomaticWoodUnlocked;b.button.GetComponentInChildren<Text>().text=m.AutomaticWoodUnlocked?"新建自动木工":"自动木工 · 需送料组件";}
                if(new[]{"LayConveyor","WarehouseOutPort","WarehouseInPort"}.Contains(b.command))b.button.interactable=m.ConveyorsUnlocked;
                if(b.command=="MachineAutoStaff")b.button.GetComponentInChildren<Text>().text="机器自动填岗："+(m.AutomaticMachineStaffing?"开":"关");
                if(b.command=="OwnerInPort"||b.command=="OwnerOutPort")b.button.interactable=m.ConveyorsUnlocked&&SelectedFacility>0&&SelectedFacility!=5&&SelectedFacility!=6;
            }
            if(SelectedMachineId!=0)
            {
                var f=m.GetFacility(SelectedMachineId);facilitySummary.text=$"耐久 {f.Durability:0.#} · 供电 {f.PowerRatio:P0} · 进度 {f.MachineProgress:0.#}/15\n"+m.ProductionDiagnosis(f);
                facilitySummary.resizeTextForBestFit=true;facilitySummary.resizeTextMinSize=12;facilitySummary.resizeTextMaxSize=15;
                facilityPages[SelectedFacility-1].Find("Machine Detail").GetComponent<Text>().text=
                    (f.Kind==128?$"自动机无需人工准备 · 机器 {f.MachineProgress:0.#}/15秒":$"人工准备 {f.HumanProgress:0.#}/10秒 · 机器 {f.MachineProgress:0.#}/15秒")+
                    $"\n现场输入 {f.WoodInputStock.Used:0.#}/8 · 输出 {f.Output.Used:0.#}/8";
            }
            if(transportPanel==null||!transportPanel.gameObject.activeSelf)return;
            if(game.MenuOpen||game.ResultVisible){transportPanel.gameObject.SetActive(false);return;}
            if(inspectedPort!=0)
            {
                var p=m.GetConveyorPort(inspectedPort);if(p==null){transportPanel.gameObject.SetActive(false);return;}
                transportArtwork.sprite=p.Output?game.appearance.outputPort:game.appearance.inputPort;
                transportTitle.text=(p.Output?"输出接口 #":"输入接口 #")+p.Id;
                string owner=p.OwnerId==-1?"旧仓库":p.OwnerId==0?"已断开，拆除后在新址重建":PrototypeSimulation.FacilityName(m.GetFacility(p.OwnerId)?.Kind??0)+" #"+p.OwnerId;
                transportInfo.text=$"所属：{owner}\n过滤：{(p.Filter<0?"全部适用物品":ItemCatalog.Name((ItemId)p.Filter))}\n状态：{(p.Blueprint?"等待施工":!p.Enabled?"已停用":"已启用")}\n外侧只连接一条带。接口不远程吸取地面物品。\n仓库饮水保护仍有效，机器输入只接受适用原料。";
            }
            else
            {
                var b=m.GetConveyor(inspectedBelt);if(b==null){transportPanel.gameObject.SetActive(false);return;}
                transportArtwork.sprite=b.Item>=0?game.appearance.ItemSprite((ItemId)b.Item):game.appearance.conveyor;
                transportTitle.text="传送带 #"+b.Id;
                transportInfo.text=$"方向：{new[]{"向右","向上","向左","向下"}[b.Direction]}\n供电：{b.PowerRatio:P0} · 单格缓存1实体\n实物：{(b.Item<0?"空":ItemCatalog.Name((ItemId)b.Item))}\n进度：{b.Progress:P0}\n{m.ConveyorStatus(b.Id)}\n带上大件仍是一件，保留原实例和耐久。";
            }
        }
        private bool ExecuteMachineryCommand(string command)
        {
            if(command.Contains("@"))command=command.Split('@')[0];
            var world=game.GetComponent<PrototypeWorld>();var m=game.Model;int id=SelectedMachineId;
            if(command.StartsWith("MachineRun")){game.Report(m.ToggleMachine(id));return true;}
            switch(command)
            {
                case "LayConveyor":world.BeginConveyor();return true;
                case "EraseConveyor":world.BeginConveyor(true);return true;
                case "WarehouseOutPort":world.BeginPort(-1,true);return true;
                case "WarehouseInPort":world.BeginPort(-1,false);return true;
                case "OwnerOutPort":if(SelectedOwnedId!=0)world.BeginPort(SelectedOwnedId,true);return true;
                case "OwnerInPort":if(SelectedOwnedId!=0)world.BeginPort(SelectedOwnedId,false);return true;
                case "MachineFeed":game.Report(m.FeedMachine(id));return true;
                case "MachineTake":game.Report(m.TakeMachineStock(id));return true;
                case "MachineTakeInput":game.Report(m.TakeMachineStock(id,true));return true;
                case "MachineOperate":game.Report(m.ToggleMachinePlayerRole(id,false));return true;
                case "MachineMaintain":game.Report(m.ToggleMachinePlayerRole(id,true));return true;
                case "MachineGoMaintain":var side=PrototypeSimulation.MaintenanceCell(m.GetFacility(id));game.GoTo(side.x,side.y);return true;
                case "MachineAssignMaintain":game.Report(m.AssignMachineWorker(id,true));return true;
                case "MachineAssignOperate":game.Report(m.AssignMachineWorker(id,false));return true;
                case "MachineCancel":game.Report(m.CancelMachineBatch(id));return true;
                case "MachineAutoStaff":m.SetAutomaticMachineStaffing(!m.AutomaticMachineStaffing);return true;
                case "CloseTransport":ClosePanels();return true;
                case "TransportEnable":if(inspectedPort!=0){var p=m.GetConveyorPort(inspectedPort);game.Report(m.ConfigurePort(p.Id,p.Filter,!p.Enabled));}else{var b=m.GetConveyor(inspectedBelt);game.Report(m.SetConveyorEnabled(b.Id,!b.Enabled));}return true;
                case "TransportRemove":game.Report(inspectedPort!=0?m.RemovePort(inspectedPort):m.RemoveConveyor(inspectedBelt));ClosePanels();return true;
                case "TransportFilterAll":if(inspectedPort!=0){var p=m.GetConveyorPort(inspectedPort);game.Report(m.ConfigurePort(p.Id,-1,p.Enabled));}else{var b=m.GetConveyor(inspectedBelt);var other=m.Conveyors.FirstOrDefault(a=>a.X==b.X&&a.Y==b.Y&&a.Id!=b.Id);if(other!=null)inspectedBelt=other.Id;}return true;
                case "TransportFilterPrevious":case "TransportFilterNext":
                    if(inspectedPort!=0){var p=m.GetConveyorPort(inspectedPort);var options=PortFilterOptions(p);int at=Array.IndexOf(options,p.Filter),step=command.EndsWith("Next")?1:-1;game.Report(m.ConfigurePort(p.Id,options[(at+step+options.Length)%options.Length],p.Enabled));}return true;
                default:return false;
            }
        }
        private int[] PortFilterOptions(ConveyorPort p)
        {
            var m=game.Model;
            if(p.OwnerId==-1)return new[]{-1,(int)ItemId.WasteWood,(int)ItemId.SortedWood,(int)ItemId.Plank,(int)ItemId.WoodChips}.Concat(Enum.GetValues(typeof(ItemId)).Cast<ItemId>().Where(i=>m.Warehouse.Count(i)>0).Select(i=>(int)i)).Distinct().ToArray();
            var f=m.GetFacility(p.OwnerId);if(f==null)return new[]{-1};
            return new[]{-1,(int)ItemId.WasteWood,(int)ItemId.SortedWood,(int)ItemId.Plank,(int)ItemId.WoodChips}.Concat(Enum.GetValues(typeof(ItemId)).Cast<ItemId>().Where(i=>f.Output.Count(i)>0||f.TableOutput.Count(i)>0||f.DismantleOutput.Count(i)>0).Select(i=>(int)i)).Distinct().ToArray();
        }
    }
}
