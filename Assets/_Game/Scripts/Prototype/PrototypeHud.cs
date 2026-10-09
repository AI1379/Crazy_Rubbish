using System;
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
        private static readonly Color Ink=new Color32(232,228,207,255),Muted=new Color32(147,168,166,255),Accent=new Color32(226,165,91,255),Green=new Color32(127,185,143,255);
        private static readonly int ItemCount=Enum.GetValues(typeof(ItemId)).Length;
        private Text clock,objective,capacity,money,pressure,workState,message,target,recipeInfo,community;
        private readonly Text[] carryCounts=new Text[ItemCount],warehouseCounts=new Text[ItemCount];
        private Button pause,build,work,plankRecipe,handleRecipe,sortedRecipe,assign;
        private Image capacityFill,progressFill;
        private RectTransform root;
        private void Start()
        {
            var canvas=new GameObject("PrototypeHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);root=canvas.GetComponent<RectTransform>();
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);
            if(EventSystem.current==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(transform,false);
            var header=StretchPanel(root,"Header",new Vector2(0,1),Vector2.one,new Vector2(24,-88),new Vector2(-24,-20),new Color32(30,43,46,255));
            TextAt(header,"Title","涌现 / 废料工坊",18,8,400,36,29,Ink);TextAt(header,"Subtitle","工具交接 · 现场投料 · 实体运输 · 居民工资",20,43,480,18,15,Muted);
            clock=TextAt(header,"Clock","",535,19,190,30,20,Ink);
            pause=ButtonAt(header,"Pause","暂停",745,14,90,39,()=>game.Model.SetPaused(!game.Model.Paused));
            for(int i=1;i<=3;i++){int speed=i;ButtonAt(header,"Speed"+i,i+"×",840+(i-1)*66,14,60,39,()=>game.Model.SetSpeed(speed));}
            ButtonAt(header,"Save","保存",1040,14,80,39,game.SaveGame);ButtonAt(header,"Load","读取",1130,14,80,39,game.LoadLatest);
            ButtonAt(header,"Restart","重开",1220,14,80,39,game.ResetGame);TextAt(header,"PrototypeMark","S5 · 首区基础",1320,23,200,24,17,Accent);
            objective=TextAt(root,"Objective","",38,106,1130,34,22,Ink);target=TextAt(root,"Hint","",38,144,1120,28,16,Muted);
            var sidebar=StretchPanel(root,"WarehousePanel",new Vector2(1,0),Vector2.one,new Vector2(-384,22),new Vector2(-24,-106),new Color32(30,43,46,255));
            sidebar=ScrollContent(sidebar,1170+ItemCount*35);
            TextAt(sidebar,"Title","现场 / 人员 / 仓库",20,15,320,30,24,Ink);community=TextAt(sidebar,"Community","",20,55,320,120,15,Muted);
            Action[] commands={
                ()=>game.Report(game.Model.BuildDismantle()),()=>game.Report(game.Model.BuildTable()),
                ()=>game.Report(game.Model.BuildGenerator()),()=>game.Report(game.Model.Recruit()),
                ()=>game.Report(game.Model.AssignWoodworker()),()=>game.Report(game.Model.PayWages()),
                ()=>game.Report(game.Model.AddFuel(ItemId.WasteWood,int.MaxValue)),()=>game.Report(game.Model.AddFuel(ItemId.WoodChips,int.MaxValue)),
                ()=>game.Report(game.Model.ToggleDismantle()),CycleDevice,
                ()=>game.Report(game.Model.DeliverInput()),()=>game.Report(game.Model.TakeOutput()),
                ()=>game.Report(game.Model.HandOffTool(ItemId.WoodHandSaw)),()=>game.Report(game.Model.HandOffTool(ItemId.Screwdriver)),
                ()=>game.Report(game.Model.DeliverInput(true)),()=>game.Report(game.Model.TakeOutput(true)),
                ()=>game.Report(game.Model.CancelBatch()),()=>game.Report(game.Model.CancelBatch(true)),
                ()=>game.Report(game.Model.CancelBlueprints()),game.SellAll,
                ()=>game.Report(game.Model.Buy(ItemId.WoodHandSaw)),()=>game.Report(game.Model.Buy(ItemId.Screwdriver)),
                ()=>game.Report(game.Model.SellTechnology(Technology.Wood)),()=>game.Report(game.Model.RemoveFacility(8,true)),
                ()=>game.Report(game.Model.TakeInput()),()=>game.Report(game.Model.TakeAsh())
            };
            string[] labels={"建拆解台","建工作台","组装发电机","招募1人","指定/释放木工","手动支付工资","补木料","补碎屑","开停拆解","切换拆解模板","投木料","取木工产物","交接手锯","交接螺丝刀","投待拆设备","取拆解产物","取消木工批次","取消拆解批次","取消施工蓝图","出售木制品","买手锯 16","买螺丝刀 10","卖木料技术 60","卖发电机","取回未预约木料","取灰渣"};
            for(int i=0;i<commands.Length;i++){int index=i;ButtonAt(sidebar,"Command"+i,labels[i],20+(i%2)*167,185+(i/2)*42,153,34,commands[index]);}
            float inventoryTop=760;
            TextAt(sidebar,"InventoryTitle","物品：拾 / 取 / 放 / 卖",20,inventoryTop,320,30,22,Ink);
            capacity=TextAt(sidebar,"Capacity","",20,inventoryTop+40,320,26,16,Muted);capacityFill=Bar(sidebar,"CapacityBar",20,inventoryTop+76,320,8,Green);
            TextAt(sidebar,"Columns","物品          背包  仓库 / 预约",20,inventoryTop+95,320,24,14,Muted);
            for(int i=0;i<ItemCount;i++)
            {
                ItemId item=(ItemId)i;float y=inventoryTop+129+i*35;
                var icon=At(sidebar,"Icon"+i,18,y+3,24,24).gameObject.AddComponent<Image>();icon.sprite=game.appearance.ItemSprite(item);icon.raycastTarget=false;
                TextAt(sidebar,"Item"+i,PrototypeGame.ItemName(item),48,y+2,100,27,14,Ink);
                carryCounts[i]=TextAt(sidebar,"Carry"+i,"",150,y+3,35,26,15,Accent);
                warehouseCounts[i]=TextAt(sidebar,"Stock"+i,"",188,y+3,43,26,14,Ink);
                ButtonAt(sidebar,"Pick"+i,"拾",237,y,25,30,()=>game.Report(game.Model.PickUp(int.MaxValue,item)));
                ButtonAt(sidebar,"Take"+i,"取",263,y,25,30,()=>game.Report(game.Model.Withdraw(item)));
                ButtonAt(sidebar,"Drop"+i,"放",289,y,25,30,()=>game.Report(game.Model.Drop(item)));
                ButtonAt(sidebar,"Sell"+i,"卖",315,y,25,30,()=>game.Report(game.Model.Sell(item,1)));
            }
            money=TextAt(sidebar,"Money","",20,inventoryTop+135+ItemCount*35,320,30,20,Accent);
            TextAt(sidebar,"Help","成品实际运进入仓库后才能出售。\n主角随身工具不能同时借给居民。",20,inventoryTop+177+ItemCount*35,320,48,15,Muted);
            var actions=StretchPanel(root,"Actions",Vector2.zero,new Vector2(0.735f,0),new Vector2(24,22),new Vector2(0,165),new Color32(30,43,46,255));
            ButtonAt(actions,"Interact","交互 E",16,14,102,39,game.Interact);ButtonAt(actions,"Deposit","入库 Q",128,14,102,39,()=>game.Report(game.Model.Deposit()));
            build=ButtonAt(actions,"Build","建木工地点",246,14,166,39,()=>game.Report(game.Model.BuildBench()),true);
            plankRecipe=ButtonAt(actions,"Plank","木板",428,14,72,39,()=>game.Report(game.Model.SelectRecipe(WoodRecipe.Plank)));
            handleRecipe=ButtonAt(actions,"Handle","木柄",510,14,72,39,()=>game.Report(game.Model.SelectRecipe(WoodRecipe.WoodHandle)));
            sortedRecipe=ButtonAt(actions,"Sorted","整理",592,14,72,39,()=>game.Report(game.Model.SelectRecipe(WoodRecipe.SortedWood)));
            work=ButtonAt(actions,"Work","木工 F",674,14,105,39,()=>game.Report(game.Model.ToggleWork()),true);
            pressure=TextAt(actions,"Pressure","",795,15,320,48,17,Green);workState=TextAt(actions,"State","",18,67,395,28,17,Ink);
            progressFill=Bar(actions,"Progress",428,73,351,9,Accent);recipeInfo=TextAt(actions,"Recipe","",795,68,330,35,15,Muted);
            message=TextAt(actions,"Notice","",18,108,1095,24,16,Muted);
        }
        private void Update()
        {
            if(root==null)return;var m=game.Model;
            clock.text=$"{(int)m.GameSeconds/60:00}:{(int)m.GameSeconds%60:00} · {(m.Paused?"暂停":m.Speed+"×")}";
            pause.GetComponentInChildren<Text>().text=m.Paused?"恢复":"暂停";
            capacity.text=$"仓库 {m.Warehouse.Used:0.##}/80  背包 {m.Carry.Used:0.##}/8";
            capacityFill.rectTransform.anchorMax=new Vector2((float)m.Warehouse.Used/m.Warehouse.Capacity,1);
            for(int i=0;i<ItemCount;i++){var item=(ItemId)i;carryCounts[i].text=m.Carry.Count(item).ToString();warehouseCounts[i].text=m.Warehouse.Count(item)+(m.Warehouse.Reserved(item)>0?"/"+m.Warehouse.Reserved(item):"");}
            community.text=$"经营现金 {m.Money:0.00} · 电力{m.AvailablePower:0.#}\n居民 {m.Residents.Count}/5 · 运输{m.DeliveredTrips}趟\n钱包 {string.Join(" ",m.Residents.Select(r=>r.Id+":"+r.Cash.ToString("0.##")))}\n发薪 {m.SecondsToWage:0}秒 · 欠薪{m.UnpaidWages:0.00}\n拆解 {ItemCatalog.Name(m.SelectedDevice)}：{PrototypeGame.StateName(m.DismantleState)}";
            money.text=$"经营现金 {m.Money:0.00}";
            pressure.text=$"人工效率 {m.HumanEfficiency:P0}\n现场木料 {m.WoodInputStock.Count(ItemId.WasteWood)} / 预约{m.WoodInputStock.Reserved(ItemId.WasteWood)}";
            workState.text=PrototypeGame.StateName(m.State)+(m.JobActive?$" · {m.Progress:P0}":"");
            progressFill.rectTransform.anchorMax=new Vector2(m.Progress,1);
            recipeInfo.text=$"{m.WoodInput(m.SelectedRecipe)}木料 → {m.ProductAmount(m.SelectedRecipe)}{ItemCatalog.Name(m.Product(m.SelectedRecipe))}\n基础{m.Duration(m.SelectedRecipe)}秒，产物需取出";
            message.text=m.Interacting?"交互进行中，移动或Esc可取消。":game.Notice;message.color=game.LastActionSucceeded?Muted:Accent;
            build.interactable=!m.BenchBuilt&&!m.BuildPending;
            target.text="WASD移动 · E交互 · Shift+E附近拾取 · Q入库 · Esc退出 · 1/2/3倍速 · 滚轮缩放";
            var nearest=m.NearestPickup();if(nearest!=null)target.text+="  附近："+ItemCatalog.Name(nearest.Item);
            if(!m.BenchBuilt)objective.text="拾取工具和材料；建设可从背包与仓库预约，持续生产需实际投料。";
            else if(!m.HasBeenPowered)objective.text="在木工正面投木料并加工；取出产物，收集发电组件后补燃料取得认可。";
            else if(m.Residents.Count==0)objective.text="招募居民，首人加入600秒后发薪；手锯需留在木工工具槽。";
            else if(m.WoodTools.Count(ItemId.WoodHandSaw)==0)objective.text="把主角携带的手锯交接到木工工具槽，再指定居民持续生产。";
            else if(m.UnpaidWages>0)objective.text=$"到期欠薪{m.UnpaidWages:0.00}钱；出售已经运入仓库的产品后手动支付。";
            else objective.text="居民按实际路径搬运和生产；保留仓库空间，观察输入预约与工具耐久。";
            var low=m.Tools.FirstOrDefault(t=>ItemCatalog.IsTool(t.Item)&&t.Durability>0&&t.Durability<=60&&!m.Tools.Any(other=>other.Item==t.Item&&other.Id!=t.Id&&other.Durability>60));
            if(low!=null)objective.text="工具预警："+ItemCatalog.Name(low.Item)+"剩余耐久低于20%，准备替补并实际送到岗位。";
        }
        private void CycleDevice()
        {
            var choices=new[]{ItemId.OldCutter,ItemId.OldWasher,ItemId.OldFormer,ItemId.OldGrowingLamp,ItemId.OldFeeder,ItemId.OldFurnace,ItemId.OldMetalTable,ItemId.OldControlBox,ItemId.DamagedMotor,ItemId.OldLamp};
            int index=Array.IndexOf(choices,game.Model.SelectedDevice);
            game.Report(game.Model.SelectDevice(choices[(index+1)%choices.Length]));
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
