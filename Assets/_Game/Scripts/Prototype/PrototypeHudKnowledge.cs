using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [Serializable] private sealed class Knowledge
        {
            public string id,title,body;
            public Sprite sprite;
            public ItemId? item;
            public int x,y;
            public bool target,hole;
        }
        [SerializeField] private RectTransform knowledgePanel;
        [SerializeField] private Text knowledgeTitle,knowledgeBody,knowledgeCaption,knowledgeFootnote;
        [SerializeField] private Image knowledgeImage;
        [SerializeField] private Button knowledgeNext,knowledgeFocus,knowledgeAction,holeCycle,holeDiscard;
        private Knowledge currentKnowledge,manualKnowledge;
        private ItemId dumpItem;
        private bool dumpConfirm;
        public bool KnowledgeOpen=>knowledgePanel!=null&&knowledgePanel.gameObject.activeSelf;
        private bool KnowledgeIsPassive=>KnowledgeOpen&&manualKnowledge==null&&!game.Model.Tutorial.IntroOpen;
        public bool KnowledgeTarget(out Vector2 targetPoint)
        {
            targetPoint=currentKnowledge==null?Vector2.zero:new Vector2(currentKnowledge.x,currentKnowledge.y);
            return KnowledgeOpen&&currentKnowledge!=null&&currentKnowledge.target;
        }

        private void BuildKnowledgePanel()
        {
            knowledgePanel=Surface(root,"Knowledge Card",16,0,430,430,Inset);
            knowledgePanel.anchorMin=knowledgePanel.anchorMax=Vector2.zero;
            knowledgePanel.GetComponent<Image>().raycastTarget=true;
            knowledgeCaption=TextAt(knowledgePanel,"Caption","认识这片地方",16,12,398,24,14,Accent);
            Artwork(knowledgePanel,"Icon",game.appearance.hole,16,52,76,76);
            knowledgeImage=knowledgePanel.Find("Icon").GetComponent<Image>();
            knowledgeTitle=TextAt(knowledgePanel,"Title","",108,50,306,78,24,Ink);
            knowledgeBody=TextAt(knowledgePanel,"Description","",16,148,398,144,17,Ink);
            knowledgeBody.alignment=TextAnchor.UpperLeft;
            knowledgeFootnote=TextAt(knowledgePanel,"Footnote","",16,310,398,44,13,Muted);
            knowledgeNext=ButtonAt(knowledgePanel,"KnowledgeNext","明白了",16,364,190,40,true);
            knowledgeFocus=ButtonAt(knowledgePanel,"KnowledgeFocus","定位地图",218,364,196,40);
            knowledgeAction=ButtonAt(knowledgePanel,"KnowledgeAction","前往拾取",16,414,398,36);
            holeCycle=ButtonAt(knowledgePanel,"HoleCycle","选择背包物品",16,464,398,36);
            holeDiscard=ButtonAt(knowledgePanel,"HoleDiscard","丢弃1份",16,510,398,36);
            knowledgePanel.gameObject.SetActive(false);
        }

        private Knowledge Landmark(int index)
        {
            switch(index)
            {
                case 1:return new Knowledge{id="receiving",title="落料洞：垃圾从上方落下",sprite=game.appearance.hole,target=true,x=PrototypeSimulation.ReceivingX,y=PrototypeSimulation.ReceivingY,
                    body="金色标记处是上层的落料洞。垃圾会陆续落到它下方的来料区，不是一次摆满整张地图。\n\n这些废弃物里有工具、生活物资和建材。左键短按物品可看名称与用途，右键可让主角走近。"};
                case 2:return new Knowledge{id="hole",title="投弃洞：物品有去无回",sprite=game.appearance.hole,target=true,x=PrototypeSimulation.DumpX,y=PrototypeSimulation.DumpY,
                    body="这是另一个洞，通向看不见的下方。带物品到洞口，可以把它丢下去腾出空间，但无法再取回。\n\n投弃完全由你选择，不是教程任务。先认识回收物的用途，别急着丢掉。"};
                case 3:return new Knowledge{id="warehouse",title="旧仓库：大家共用的库存",sprite=game.appearance.warehouse,target=true,x=PrototypeSimulation.WarehouseX,y=PrototypeSimulation.WarehouseY,
                    body="现成的旧仓库有80容量。主角背包只有8容量，需要分趟搬运；物品留在地上并不算已入库。\n\n走到仓库门前按 Q，把背包物品送进仓库。食物、水从这里供应，商品也从这里出售。随身手锯会保留。"};
                default:return new Knowledge{id="housing",title="旧住房：主角与居民的家",sprite=game.appearance.housing,target=true,x=PrototypeSimulation.HousingX,y=PrototypeSimulation.HousingY,
                    body="旧住房可容纳6人：主角和最多5名首批居民。现在还没有员工，需要先让发电机运转，取得居民认可。\n\n居民加入后能搬运和生产，但需要食物、饮水和工资。我们会在这些机制出现时再介绍。"};
            }
        }

        private Knowledge Concept(string id)
        {
            var m=game.Model;
            switch(id)
            {
                case "saw":return ItemKnowledge(ItemId.WoodHandSaw,id);
                case "supplies":return new Knowledge{id=id,title="食物与水：先维持生活",sprite=game.appearance.ItemSprite(ItemId.Food),body="新落下的食品和水瓶可以使用。主角也需要吃喝；先搬到旧仓库，生活需求到期时才有实物可用。\n\n背包容量8，装满就去仓库门前按 Q。屏幕顶部的食物和饮水数字指仓库库存。"};
                case "materials":return ItemKnowledge(ItemId.WasteWood,id);
                case "parts":return ItemKnowledge(ItemId.SteelPart,id);
                case "salvage":return ItemKnowledge(m.Ground.First(p=>ItemCatalog.IsDevice(p.Item)||ItemCatalog.IsOpenable(p.Item)).Item,id);
                case "woodwork":return new Knowledge{id=id,title="木工台：原料不会自动加工",sprite=game.appearance.woodwork,target=true,x=PrototypeSimulation.BenchX,y=PrototypeSimulation.BenchY,body="你建起了第一个生产地点。木料要从背包投入现场缓冲，手锯也要带到场。\n\n在台前按 F 开始工作；移动会退出岗位。成品先留在台边，需要取出并送进仓库。"};
                case "planks":return ItemKnowledge(ItemId.Plank,id);
                case "table":return new Knowledge{id=id,title="工作台：把部件组装成工具",sprite=game.appearance.workbench,target=true,x=PrototypeSimulation.TableX,y=PrototypeSimulation.TableY,body="你已经取得木板和钢铁零件，现在知道如何建工作台。它把木柄与工作端组装成工具，例如木柄 + 手锯锯片 = 手锯。\n\n首次取得材料只解锁方案，不会自动造好建筑。按 B 查看建材和建设位置。"};
                case "dismantle":return new Knowledge{id=id,title="拆解地点：旧设备里有零件",sprite=game.appearance.dismantle,target=true,x=PrototypeSimulation.DismantleX,y=PrototypeSimulation.DismantleY,body="旧机器是原料，不是已经安装的生产设施。带上螺丝刀，将旧设备投入拆解地点，再到正面作业。\n\n拆出的电机、锯片和钢铁零件留在输出处，要取走后才算完成搬运。"};
                case "power":return new Knowledge{id=id,title="发电机：接线、负载与燃料",sprite=game.appearance.generator,target=true,x=PrototypeSimulation.GeneratorX,y=PrototypeSimulation.GeneratorY,body="建好发电机后，从设备外沿铺电线，连接旧住房、路灯或机器，再到正面投入木料或碎屑。\n\n完全空载不烧燃料。要实际带负载发电才取得招募认可。设备面板会区分断线、缺燃料和电力不足。"};
                case "people":return new Knowledge{id=id,title="居民：工作、生活和工资",sprite=game.appearance.housing,target=true,x=PrototypeSimulation.HousingX,y=PrototypeSimulation.HousingY,body="发电机已经运行，居民愿意加入了。按 P 招募、分配岗位；首批免招募费，工资不免。\n\n先招2—3人便于熟悉，也可以招满5人。居民会实际走路搬运；木工需要现场工具，不能使用你远处背包里的手锯。"};
                case "wages":return new Knowledge{id=id,title="第一张工资：需要实际支付",sprite=game.appearance.housing,body=$"首位员工加入600秒后，每位居民应得10钱。现在待付 {m.UnpaidWages:0.##} 钱，下次到期还有 {m.SecondsToWage:0} 秒。\n\n出售已经入库的产品取得现金，再在人员页支付工资。查看账单不会自动付款，免费食物也不抵工资。"};
                default:return new Knowledge{id=id,title="工具耐久：准备下一把手锯",sprite=game.appearance.ItemSprite(ItemId.SawBlade),body="手锯反复加工会磨损。木工做木柄，采购手锯锯片，再带两种部件到工作台装配备用手锯。\n\n机器锯片和手锯锯片是不同的部件。完成后把工具送回仓库或交给需要它的岗位。"};
            }
        }

        private Knowledge ItemKnowledge(ItemId item,string id="item")
        {
            var pile=game.Model.Ground.Where(p=>p.Item==item&&p.Amount>p.Reserved).OrderBy(p=>PrototypeSimulation.Distance(game.Model.PlayerX,game.Model.PlayerY,p.X,p.Y)).FirstOrDefault();
            return new Knowledge{id=id,title=ItemCatalog.Name(item),body=PrototypeKnowledge.Purpose(item),sprite=game.appearance.GroundSprite(item,false),item=item,target=pile!=null,x=pile?.X??0,y=pile?.Y??0};
        }

        private string NextConcept(PrototypeSimulation m)
        {
            bool Present(ItemId i)=>m.HasDiscovered(i)||m.Ground.Any(p=>p.Item==i);
            var conditions=new[]{
                ("saw",Present(ItemId.WoodHandSaw)),("supplies",Present(ItemId.Food)||Present(ItemId.DrinkingWater)),
                ("materials",Present(ItemId.WasteWood)),("woodwork",m.BenchBuilt),("planks",m.HasDiscovered(ItemId.Plank)),
                ("parts",Present(ItemId.SteelPart)),("salvage",m.Ground.Any(p=>ItemCatalog.IsDevice(p.Item)||ItemCatalog.IsOpenable(p.Item))),
                ("table",m.TableUnlocked),("dismantle",m.DismantleBuilt),("power",m.GeneratorUnlocked),
                ("people",m.HasBeenPowered),("wages",m.Residents.Count>0&&(m.SecondsToWage<=60||m.UnpaidWageCents>0)),
                ("tools",m.TotalWagesPaidCents>=5000&&m.AssembledTools==0)};
            return conditions.FirstOrDefault(c=>c.Item2&&!m.Tutorial.seenConcepts.Contains(c.Item1)).Item1;
        }

        private void RefreshKnowledge(PrototypeSimulation m)
        {
            bool tour=m.Tutorial.IntroOpen&&m.Tutorial.orientation>0&&m.Tutorial.orientation<5;
            if(tour)currentKnowledge=Landmark(m.Tutorial.orientation);
            else if(manualKnowledge!=null)currentKnowledge=manualKnowledge;
            else if(m.Tutorial.active&&m.Tutorial.started&&!m.Tutorial.Completed)
            {string id=NextConcept(m);currentKnowledge=id==null?null:Concept(id);}
            else currentKnowledge=null;
            bool visible=currentKnowledge!=null&&!game.MenuOpen&&!game.ResultVisible&&!ManagementOpen&&SelectedFacility==0&&!InventoryOpen;
            knowledgePanel.gameObject.SetActive(visible);
            if(!visible)return;
            knowledgeCaption.text=tour?$"认识环境   {m.Tutorial.orientation} / 4 · 时间停止":manualKnowledge!=null?"认识物品与地点":"新机制出现了";
            knowledgeTitle.text=currentKnowledge.title;knowledgeBody.text=currentKnowledge.body;knowledgeImage.sprite=currentKnowledge.sprite;
            knowledgeTitle.resizeTextForBestFit=true;knowledgeTitle.resizeTextMinSize=19;knowledgeTitle.resizeTextMaxSize=24;
            float descriptionHeight=Mathf.Max(100,knowledgeBody.preferredHeight);
            knowledgeBody.rectTransform.sizeDelta=new Vector2(398,descriptionHeight);
            float footY=148+descriptionHeight+16;
            knowledgeFootnote.rectTransform.anchoredPosition=new Vector2(16,-footY);
            knowledgeFootnote.text=tour?"左键按住拖动地图，滚轮缩放。\n先认清位置，再开始拾取。":"可以随时短按物品重看用途。\n阅读时游戏继续；按空格可暂停。";
            float buttonsY=footY+56;
            knowledgeNext.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-buttonsY);
            knowledgeFocus.GetComponent<RectTransform>().anchoredPosition=new Vector2(218,-buttonsY);
            knowledgeNext.GetComponentInChildren<Text>().text=tour?(m.Tutorial.orientation==4?"认识好了，开始实操":"下一处"):"明白了，继续";
            knowledgeFocus.interactable=currentKnowledge.target;
            knowledgeAction.gameObject.SetActive(!tour&&(currentKnowledge.item.HasValue||currentKnowledge.hole));
            knowledgeAction.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-(buttonsY+50));
            knowledgeAction.GetComponentInChildren<Text>().text=currentKnowledge.hole?"前往投弃洞口":currentKnowledge.item.HasValue&&game.Model.Ground.Any(p=>p.Item==currentKnowledge.item.Value&&PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)<=m.Settings.interactionRadius)?"拾取这一种物品":"前往拾取";
            holeCycle.gameObject.SetActive(currentKnowledge.hole);holeDiscard.gameObject.SetActive(currentKnowledge.hole);
            if(currentKnowledge.hole)
            {
                holeCycle.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-(buttonsY+96));
                holeDiscard.GetComponent<RectTransform>().anchoredPosition=new Vector2(16,-(buttonsY+142));
                if(m.Carry.Available(dumpItem)==0)CycleDumpItem();
                holeCycle.GetComponentInChildren<Text>().text=m.Carry.Available(dumpItem)>0?$"背包：{ItemCatalog.Name(dumpItem)} ×{m.Carry.Available(dumpItem)} · 切换":"背包里没有可投弃物品";
                holeDiscard.GetComponentInChildren<Text>().text=dumpConfirm?"再次点击确认 · 丢弃1份，无法取回":"选择丢弃1份";
                holeDiscard.interactable=m.Carry.Available(dumpItem)>0&&!m.Interacting&&!m.Paused&&PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,PrototypeSimulation.DumpX,PrototypeSimulation.DumpY)<=m.Settings.interactionRadius;
            }
            float height=buttonsY+(currentKnowledge.hole?192:knowledgeAction.gameObject.activeSelf?100:54);
            knowledgePanel.sizeDelta=new Vector2(430,height);
            bool passive=!tour&&manualKnowledge==null;
            knowledgePanel.anchorMin=knowledgePanel.anchorMax=new Vector2(passive?1:0,0);
            knowledgePanel.anchoredPosition=new Vector2(passive?-446:16,130+height);
        }

        public void ShowObjectInfo(string title,string body,Sprite sprite)
        {ClosePanels();manualKnowledge=new Knowledge{id="object",title=title,body=body,sprite=sprite};RefreshKnowledge(game.Model);}
        public void ShowGroundItem(ItemId item)
        {ClosePanels();manualKnowledge=ItemKnowledge(item);RefreshKnowledge(game.Model);}
        public void ShowHole()
        {ClosePanels();manualKnowledge=Landmark(2);manualKnowledge.hole=true;dumpConfirm=false;RefreshKnowledge(game.Model);}
        private void FocusKnowledge()
        {if(currentKnowledge!=null&&currentKnowledge.target)game.GetComponent<PrototypeWorld>().FocusPoint(currentKnowledge.x,currentKnowledge.y-2,6);}
        private void AcknowledgeKnowledge()
        {
            var t=game.Model.Tutorial;
            if(t.IntroOpen&&t.orientation>0&&t.orientation<5)
            {
                t.orientation++;
                if(t.orientation==5)game.BeginTutorial();
                RefreshKnowledge(game.Model);if(t.orientation<5)FocusKnowledge();else game.GetComponent<PrototypeWorld>().FocusPoint(PrototypeSimulation.ReceivingX,PrototypeSimulation.ReceivingY-2,7);
            }
            else
            {
                if(manualKnowledge==null&&currentKnowledge!=null&&!t.seenConcepts.Contains(currentKnowledge.id))t.seenConcepts.Add(currentKnowledge.id);
                manualKnowledge=null;currentKnowledge=null;dumpConfirm=false;knowledgePanel.gameObject.SetActive(false);
            }
        }
        private void ExecuteKnowledgeAction()
        {
            if(currentKnowledge==null)return;
            if(currentKnowledge.hole){game.GoTo(PrototypeSimulation.DumpX,PrototypeSimulation.DumpY);return;}
            if(!currentKnowledge.item.HasValue)return;
            ItemId item=currentKnowledge.item.Value;
            if(game.Model.Ground.Any(p=>p.Item==item&&PrototypeSimulation.Distance(game.Model.PlayerX,game.Model.PlayerY,p.X,p.Y)<=game.Model.Settings.interactionRadius))game.Report(game.Model.PickUp(int.MaxValue,item));
            else game.GoToItem(item);
        }
        private void CycleDumpItem()
        {
            var items=Enum.GetValues(typeof(ItemId)).Cast<ItemId>().Where(i=>game.Model.Carry.Available(i)>0).ToArray();
            if(items.Length>0)dumpItem=items[(Array.IndexOf(items,dumpItem)+1)%items.Length];dumpConfirm=false;
        }
        private void ConfirmDump()
        {if(!dumpConfirm){dumpConfirm=true;return;}game.Report(game.Model.DumpIntoHole(dumpItem,1));dumpConfirm=false;}

        private void RefreshAvailableMechanics(PrototypeSimulation m)
        {
            foreach(var binding in buttonBindings)
            {
                string command=binding.command;
                bool? available=null;
                switch(command)
                {
                    case "OpenFacility3": available=m.TableUnlocked||m.TableBuilt;break;
                    case "OpenFacility4": available=m.GeneratorUnlocked||m.GeneratorBuilt;break;
                    case "Command1": binding.button.interactable=m.TableUnlocked&&!m.TableBuilt;break;
                    case "Command2": binding.button.interactable=m.GeneratorUnlocked&&!m.GeneratorBuilt;break;
                }
                if(available.HasValue)binding.button.transform.parent.gameObject.SetActive(available.Value);
            }
            // Only built facilities occupy the world. The selected construction preview is temporary.
            work.gameObject.SetActive(m.BenchBuilt);
            plankRecipe.gameObject.SetActive(m.BenchBuilt);handleRecipe.gameObject.SetActive(m.BenchBuilt);sortedRecipe.gameObject.SetActive(m.BenchBuilt);
            foreach(var binding in buttonBindings)
            {
                string command=binding.command.StartsWith("FacilityCommand")?"Command"+binding.command.Substring(15):binding.command;
                if(!command.StartsWith("Command")||!int.TryParse(command.Substring(7),out int index))continue;
                bool? visible=null;
                if(new[]{10,11,12,16,24}.Contains(index))visible=m.BenchBuilt;
                if(new[]{8,9,13,14,15,17}.Contains(index))visible=m.DismantleBuilt;
                if(new[]{6,7,23,25}.Contains(index))visible=m.GeneratorBuilt;
                if(index>=29&&index<=36)visible=m.TableBuilt;
                if(new[]{3,4,5}.Contains(index))visible=m.HasBeenPowered;
                if(visible.HasValue)binding.button.gameObject.SetActive(visible.Value);
            }
        }
    }
}
