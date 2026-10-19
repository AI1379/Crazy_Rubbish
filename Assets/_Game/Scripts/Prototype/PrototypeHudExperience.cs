using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [Serializable] private sealed class ShopRow
        {
            public RectTransform rect;
            public Text description;
            public Button purchase;
            public ItemId item;
        }
        [SerializeField] private RectTransform shopWindow, shopContent;
        [SerializeField] private Text shopSummary, shopMaterials;
        [SerializeField] private ShopRow[] shopRows;
        [SerializeField] private Button shopFilter, shopMissing, interfaceScale;
        private int shopQuantity = 1, shopCategory, shoppingKind, interfaceScaleMode;
        private bool scalePreferenceLoaded;
        public bool ShopOpen => shopWindow != null && shopWindow.gameObject.activeSelf;

        private void BuildExperienceHud()
        {
            shopWindow = StretchPanel(root,"Material Shop",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0.03f,0.04f,0.04f,0.7f));
            shopWindow.GetComponent<Image>().raycastTarget = true;
            var card = Surface(shopWindow,"Card",0,0,920,744,Panel);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f,0.5f);card.anchoredPosition=Vector2.zero;
            TextAt(card,"Title","建材与物资采购",24,16,800,36,26,Ink);
            ButtonAt(card,"CloseShop","×",858,18,36,32);
            shopSummary=TextAt(card,"Budget","",24,60,872,54,16,Accent);
            shopMaterials=TextAt(card,"Materials","",24,118,872,98,15,Ink);shopMaterials.alignment=TextAnchor.UpperLeft;
            shopFilter=ButtonAt(card,"ShopCategory","建材与燃料",24,226,240,36);
            shopMissing=ButtonAt(card,"ShopQuantityMissing","按缺口逐项补货",280,226,212,36);
            ButtonAt(card,"ShopQuantity1","买1份",510,226,112,36);
            ButtonAt(card,"ShopQuantity5","买5份",634,226,112,36);
            ButtonAt(card,"ShopQuantity10","买10份",758,226,136,36);
            var body=At(card,"Items",16,276,888,400);shopContent=ScrollContent(body,1000);
            var items=ItemCatalog.All.Where(i=>ItemCatalog.Price(i).buy>0).ToArray();
            shopRows=new ShopRow[items.Length];
            for(int i=0;i<items.Length;i++)
            {
                var row=new ShopRow{item=items[i]};shopRows[i]=row;
                row.rect=Surface(shopContent,"Shop Item "+(int)row.item,8,i*72,844,64,Inset);
                Artwork(row.rect,"Icon",game.appearance.ItemSprite(row.item),10,10,44,44);
                row.description=TextAt(row.rect,"Description","",70,4,542,56,16,Ink);
                row.purchase=ButtonAt(row.rect,"ShopBuy"+(int)row.item,"采购",630,12,200,40);
            }
            TextAt(card,"Help","采购按固定进价结算；优先入库，满仓余货送至产权地。选好数量后逐项购买。",24,690,872,32,14,Muted);
            shopWindow.gameObject.SetActive(false);
            var go=facilityPanel.Find("GoToFacility").GetComponent<RectTransform>();go.sizeDelta=new Vector2(172,38);
            go.Find("Label").GetComponent<RectTransform>().sizeDelta=new Vector2(160,38);
            foreach(string name in new[]{"Bevel","Selected"})
            {var line=go.Find(name).GetComponent<RectTransform>();line.sizeDelta=new Vector2(172,line.sizeDelta.y);}
            ButtonAt(facilityPanel,"ShopForSelected","建材 / 采购",204,150,172,38);
            ButtonAt(pages[3].GetComponent<ScrollRect>().content,"BeginAutomationGuide","自动化入门：机器与输送",12,0,340,38).gameObject.SetActive(false);
            BuildSessionControls();
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
        }

        public void OpenShop(int kind=0)
        {
            ClosePanels();game.StopWalkingForInventory();shoppingKind=kind;shopCategory=kind==0?0:3;shopQuantity=kind==0?1:0;
            shopWindow.gameObject.SetActive(true);shopWindow.SetAsLastSibling();RefreshShop(game.Model);
        }

        private bool CloseExperiencePanels()
        {
            bool closed=false;
            foreach(var panel in new[]{shopWindow,saveBrowser,newSessionConfirmation})
                if(panel!=null&&panel.gameObject.activeSelf){panel.gameObject.SetActive(false);closed=true;}
            return closed;
        }

        private void RefreshExperienceHud(PrototypeSimulation m)
        {
            if(!scalePreferenceLoaded)
            {interfaceScaleMode=Application.isPlaying?Mathf.Clamp(PlayerPrefs.GetInt("InterfaceScale",0),0,2):0;scalePreferenceLoaded=true;}
            float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
            float minimumWidth=Mathf.Max(1120,820*aspect);
            float width=interfaceScaleMode==1?Mathf.Max(1600,minimumWidth):interfaceScaleMode==2?minimumWidth:Mathf.Max(minimumWidth,Mathf.Min(1600,Screen.width));
            root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(width,900);
            if(interfaceScale!=null)interfaceScale.GetComponentInChildren<Text>().text="界面："+new[]{"自适应","标准","大号"}[Mathf.Clamp(interfaceScaleMode,0,2)];
            if(ShopOpen){if(game.MenuOpen||game.ResultVisible)shopWindow.gameObject.SetActive(false);else RefreshShop(m);}
            RefreshSessionControls();
        }

        private bool ShopMatches(ItemId item)
        {
            if(shopCategory==3)return shoppingKind==0||DesignBaseline.BuildCost(shoppingKind).Any(p=>p.Item==item);
            if(shopCategory==0)return new[]{ItemId.WasteWood,ItemId.Plank,ItemId.RecoveredFastener,ItemId.SteelPart,ItemId.CopperPart,ItemId.RecoveredWire,ItemId.WoodChips,ItemId.SortedWood}.Contains(item);
            if(shopCategory==1)return new[]{ItemId.Motor,ItemId.MachineBlade,ItemId.FeedingModule,ItemId.ThermalModule,ItemId.GeneratorModule}.Contains(item);
            return item==ItemId.Food||item==ItemId.DrinkingWater||ItemCatalog.IsTool(item)||new[]{ItemId.SawBlade,ItemId.HammerHead,ItemId.ScrewdriverRod,ItemId.WoodHandle}.Contains(item);
        }

        private void RefreshShop(PrototypeSimulation m)
        {
            shopSummary.text=$"现金 {m.Money:0.00}  · 欠薪 {m.UnpaidWages:0.00}  · 下期工资 {m.NextPayrollCents/100m:0.00}\n"+(shopQuantity==0?"按本次建设缺口逐项采购":$"当前每项采购 {shopQuantity} 份")+$" · 仓库剩余 {m.Warehouse.FreeTicks/20m:0.##} 容量";
            shopMaterials.text=shoppingKind==0?"先选择分类和数量。建设缺料可从设施的“建材 / 采购”进入，逐项查看已有数量与缺口。":PrototypeSimulation.FacilityName(shoppingKind)+"所需建材："+string.Concat(DesignBaseline.BuildCost(shoppingKind).Select((p,i)=>
            {
                int available=m.Warehouse.Available(p.Item)+m.Carry.Available(p.Item);
                return (i%3==0?"\n":"    ·    ")+$"{ItemCatalog.Name(p.Item)} {available}/{p.Amount}"+(available<p.Amount?$" 缺{p.Amount-available}":" 已齐");
            }));
            shopMissing.interactable=shoppingKind!=0;
            SetSelected(shopMissing,shopQuantity==0);
            shopMaterials.resizeTextForBestFit=true;shopMaterials.resizeTextMinSize=12;shopMaterials.resizeTextMaxSize=15;
            shopFilter.GetComponentInChildren<Text>().text=new[]{"建材与燃料","机器部件","生活与工具",shoppingKind==0?"全部物资":"本次建设所需"}[shopCategory];
            int visible=0;
            foreach(var row in shopRows)
            {
                bool show=ShopMatches(row.item);row.rect.gameObject.SetActive(show);if(!show)continue;
                row.rect.anchoredPosition=new Vector2(8,-visible++*72);
                int available=m.Warehouse.Available(row.item)+m.Carry.Available(row.item);
                int quantity=ShopPurchaseAmount(row.item);var cost=ItemCatalog.Price(row.item).buy*quantity;
                row.description.text=$"{ItemCatalog.Name(row.item)}  · 已有 {available}\n进价 {ItemCatalog.Price(row.item).buy:0.##}/份";
                row.purchase.GetComponentInChildren<Text>().text=quantity==0?"本次建材已备齐":cost>m.Money?$"需 {cost:0.##} 钱 · 现金不足":$"买 {quantity} 份 · {cost:0.##}钱";
                row.purchase.interactable=quantity>0&&cost<=m.Money&&!m.Failed;
            }
            shopContent.sizeDelta=new Vector2(0,Mathf.Max(400,visible*72));
        }

        private int ShopPurchaseAmount(ItemId item)
        {
            if(shopQuantity>0)return shopQuantity;
            int target=DesignBaseline.BuildCost(shoppingKind).Where(p=>p.Item==item).Select(p=>p.Amount).FirstOrDefault();
            return Mathf.Max(0,target-game.Model.Warehouse.Available(item)-game.Model.Carry.Available(item));
        }

        private bool ExecuteExperienceCommand(string command)
        {
            if(ExecuteSessionCommand(command))return true;
            if(command.StartsWith("ShopBuy")&&int.TryParse(command.Substring(7),out int item))
            {game.Report(game.Model.Buy((ItemId)item,ShopPurchaseAmount((ItemId)item)));return true;}
            if(command.StartsWith("ShopQuantity")&&int.TryParse(command.Substring(12),out int count))
            {shopQuantity=count;return true;}
            switch(command)
            {
                case "OpenShop":OpenShop();return true;
                case "ShopForSelected":OpenShop(FacilityKind(SelectedFacility));return true;
                case "CloseShop":shopWindow.gameObject.SetActive(false);return true;
                case "ShopCategory":shopCategory=(shopCategory+1)%4;shopContent.anchoredPosition=Vector2.zero;return true;
                case "ShopQuantityMissing":if(shoppingKind!=0)shopQuantity=0;return true;
                case "InterfaceScale":interfaceScaleMode=(interfaceScaleMode+1)%3;PlayerPrefs.SetInt("InterfaceScale",interfaceScaleMode);PlayerPrefs.Save();return true;
                case "BeginAutomationGuide":BeginAutomationGuide();return true;
                case "InventoryQuantity1":inventorySaleQuantity=1;return true;
                case "InventoryQuantity5":inventorySaleQuantity=5;return true;
                case "InventoryQuantityAll":inventorySaleQuantity=int.MaxValue;return true;
                default:return false;
            }
        }
    }
}
