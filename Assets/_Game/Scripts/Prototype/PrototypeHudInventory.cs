using System;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [Serializable] private sealed class InventoryCell
        {
            public RectTransform rect;
            public Image icon,background;
            public Text count,locked;
            public PrototypeInventorySlot input;
        }
        [SerializeField] private RectTransform inventoryWindow,inventoryCard,upperGrid,bagGrid,handView;
        [SerializeField] private InventoryCell[] stockCells,bagCells,groundCells;
        [SerializeField] private Text inventoryTitle,upperCaption,bagCaption,inventoryStatus,itemTitle,itemDescription,itemQuantity,handCount,inventoryHints;
        [SerializeField] private Image itemArtwork,handArtwork;
        [SerializeField] private Button inventoryPrimary,inventoryGo,inventorySell,inventoryDrop,stockPrevious,stockNext,bagPrevious,bagNext;
        [SerializeField] private Text inventoryTradeInfo;
        private int inventorySaleQuantity=1;
        private bool warehouseView,awaitingWarehouse;
        private int stockPage,bagPage;
        private ItemId? inspectedItem;
        private int inspectedContainer;
        private int inventoryNoticeRevision,inventoryInteractionRevision;
        private float inventoryFeedbackUntil;
        private string inventoryFeedback;
        private bool inventoryFeedbackSuccess;
        private int[] nearbyPileIds=Array.Empty<int>();
        public bool InventoryOpen=>inventoryWindow!=null&&inventoryWindow.gameObject.activeSelf;

        private void BuildInventoryWindow()
        {
            inventoryWindow=StretchPanel(root,"Inventory Window",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0.03f,0.04f,0.04f,0.55f));
            inventoryWindow.GetComponent<Image>().raycastTarget=true;
            inventoryCard=Surface(inventoryWindow,"Card",0,0,1008,744,Panel);
            inventoryCard.anchorMin=inventoryCard.anchorMax=inventoryCard.pivot=new Vector2(0.5f,0.5f);inventoryCard.anchoredPosition=Vector2.zero;
            Stripe(inventoryCard,"Accent",0,0,1008,3,Accent);
            inventoryTitle=TextAt(inventoryCard,"Title","背包",28,16,870,36,26,Ink);
            ButtonAt(inventoryCard,"CloseInventory","×",948,18,32,32);
            inventoryStatus=TextAt(inventoryCard,"Status","",28,58,952,30,14,Muted);
            upperCaption=TextAt(inventoryCard,"Upper Caption","仓库",28,98,378,28,17,Accent);
            stockPrevious=ButtonAt(inventoryCard,"StockPrevious","上一页",412,98,76,28);
            stockNext=ButtonAt(inventoryCard,"StockNext","下一页",496,98,76,28);
            upperGrid=At(inventoryCard,"Upper Grid",28,138,544,330);
            stockCells=MakeInventoryCells(upperGrid,1,54);
            groundCells=MakeInventoryCells(upperGrid,2,18);
            bagCaption=TextAt(inventoryCard,"Bag Caption","背包",28,480,544,28,17,Accent);
            bagPrevious=ButtonAt(inventoryCard,"BagPrevious","上一页",412,480,76,28);
            bagNext=ButtonAt(inventoryCard,"BagNext","下一页",496,480,76,28);
            bagGrid=At(inventoryCard,"Backpack Grid",28,520,544,164);
            bagCells=MakeInventoryCells(bagGrid,0,27);
            var detail=Surface(inventoryCard,"Details",598,98,382,586,Inset);
            Artwork(detail,"Icon",game.appearance.saw,16,18,64,64);itemArtwork=detail.Find("Icon").GetComponent<Image>();
            itemTitle=TextAt(detail,"Name","指向格子查看物品",94,20,270,58,21,Ink);
            itemTitle.resizeTextForBestFit=true;itemTitle.resizeTextMinSize=17;itemTitle.resizeTextMaxSize=21;
            itemQuantity=TextAt(detail,"Quantity","",16,96,350,56,15,Accent);
            itemDescription=TextAt(detail,"Description","",16,164,350,192,16,Ink);itemDescription.alignment=TextAnchor.UpperLeft;
            itemDescription.resizeTextForBestFit=true;itemDescription.resizeTextMinSize=14;itemDescription.resizeTextMaxSize=16;
            itemDescription.rectTransform.sizeDelta=new Vector2(350,100);
            inventoryTradeInfo=TextAt(detail,"Sale Preview","",16,268,350,60,14,Accent);
            ButtonAt(detail,"InventoryQuantity1","售1份",16,336,106,30);
            ButtonAt(detail,"InventoryQuantity5","售5份",130,336,106,30);
            ButtonAt(detail,"InventoryQuantityAll","售全部可用",244,336,122,30);
            inventoryPrimary=ButtonAt(detail,"InventoryPrimary","拾取整叠",16,376,350,40,true);
            inventoryGo=ButtonAt(detail,"InventoryGoWarehouse","前往仓库",16,426,350,40);
            inventorySell=ButtonAt(detail,"InventorySell","出售 1 份",16,478,168,38);
            inventoryDrop=ButtonAt(detail,"InventoryDrop","放到地上 1 份",196,478,170,38);
            ButtonAt(detail,"InventoryCatalogue","物品图鉴",16,532,350,36);
            inventoryHints=TextAt(inventoryCard,"Controls","左键：整叠拿放 / 交换    右键：拆半 / 放1份    Shift点击：快速转移\n拖动到目标格也可转移。关闭时手中物品退回；容量按物品体积计算。",28,698,952,40,14,Muted);
            handView=At(inventoryWindow,"Held Stack",0,0,54,54);
            handArtwork=handView.gameObject.AddComponent<Image>();handArtwork.raycastTarget=false;handArtwork.preserveAspect=true;
            handCount=TextAt(handView,"Count","",0,30,52,22,16,Ink);handCount.alignment=TextAnchor.MiddleRight;
            handView.gameObject.SetActive(false);inventoryWindow.gameObject.SetActive(false);
        }

        private InventoryCell[] MakeInventoryCells(Transform parent,int container,int count)
        {
            var result=new InventoryCell[count];
            for(int i=0;i<count;i++)
            {
                var cell=new InventoryCell();result[i]=cell;
                cell.rect=Surface(parent,(container==0?"Bag":container==1?"Stock":"Ground")+" Slot "+i,(i%9)*61,(i/9)*56,56,50,Inset);
                cell.background=cell.rect.GetComponent<Image>();cell.background.raycastTarget=true;
                Artwork(cell.rect,"Icon",null,7,4,40,40);cell.icon=cell.rect.Find("Icon").GetComponent<Image>();
                cell.count=TextAt(cell.rect,"Count","",2,27,50,22,15,Ink);cell.count.alignment=TextAnchor.LowerRight;
                cell.count.gameObject.AddComponent<Shadow>().effectColor=Color.black;
                cell.locked=TextAt(cell.rect,"Reserved","",3,0,48,18,11,Accent);cell.locked.alignment=TextAnchor.UpperLeft;
                cell.input=cell.rect.gameObject.AddComponent<PrototypeInventorySlot>();cell.input.hud=this;cell.input.container=container;cell.input.index=i;
            }
            return result;
        }

        public void OpenInventory(bool warehouse=false)
        {
            ClosePanels();warehouseView=warehouse;awaitingWarehouse=false;stockPage=bagPage=0;inspectedItem=null;
            inventoryNoticeRevision=game.NoticeRevision;inventoryInteractionRevision=game.Model.InteractionRevision;inventoryFeedbackUntil=0;inventorySaleQuantity=1;
            game.StopWalkingForInventory();inventoryWindow.gameObject.SetActive(true);RefreshInventoryWindow(game.Model);
        }
        public void CloseInventory()
        {
            game.Model?.ReturnInventoryHand();
            if(inventoryWindow!=null)inventoryWindow.gameObject.SetActive(false);
            awaitingWarehouse=false;
        }
        public void OpenWarehouse()
        {
            if(game.Model.NearWarehouse){OpenInventory(true);return;}
            ClosePanels();game.GoTo(PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);awaitingWarehouse=game.Walking;
        }
        public void FocusInventoryItem(ItemId item,bool warehouse)
        {
            OpenInventory(warehouse);inspectedItem=item;inspectedContainer=warehouse?1:2;
            game.Model.SyncInventoryGrids();
            if(warehouse){int slot=game.Model.WarehouseGrid.slots.FindIndex(s=>s.item==item&&s.amount>0);stockPage=Math.Max(0,slot/54);}
            RefreshInventoryWindow(game.Model);
        }

        private void RefreshInventoryWindow(PrototypeSimulation m)
        {
            if(awaitingWarehouse)
            {
                if(game.MenuOpen||game.ResultVisible||m.Tutorial.IntroOpen)awaitingWarehouse=false;
                else if(m.NearWarehouse)OpenInventory(true);
                else if(!game.Walking)awaitingWarehouse=false;
            }
            if(!InventoryOpen)return;
            if(game.MenuOpen||game.ResultVisible||m.Failed){CloseInventory();return;}
            if(!m.NearWarehouse&&m.Hand!=null&&m.Hand.warehouse)m.ReturnInventoryHand();
            m.SyncInventoryGrids();
            if(inventoryNoticeRevision!=game.NoticeRevision)
            {inventoryNoticeRevision=game.NoticeRevision;inventoryFeedback=game.Notice;inventoryFeedbackSuccess=game.LastActionSucceeded;inventoryFeedbackUntil=Time.unscaledTime+4;}
            if(inventoryInteractionRevision!=m.InteractionRevision)
            {inventoryInteractionRevision=m.InteractionRevision;inventoryFeedback=m.LastInteraction.Message;inventoryFeedbackSuccess=m.LastInteraction.Success;inventoryFeedbackUntil=Time.unscaledTime+4;}
            inventoryTitle.text=warehouseView?"旧仓库  /  背包":"背包  /  附近物品";
            bool feedback=Time.unscaledTime<inventoryFeedbackUntil;
            inventoryStatus.text=m.InventoryTransferPending?"搬运中… 完成后更新库存；关闭窗口会取消。":m.Paused?"游戏已暂停；按空格继续存取。容器内整理仍可进行。":warehouseView&&!m.NearWarehouse?"远处可查看库存与出售；存取请前往仓库门前。":feedback?inventoryFeedback:m.Hand!=null?$"手中：{ItemCatalog.Name(m.Hand.item)} ×{m.Hand.amount}。点击格子放下；关闭会退回。":"物品栏打开时游戏继续。按空格暂停，I 或 Esc 关闭。";
            inventoryStatus.color=feedback&&!inventoryFeedbackSuccess?Danger:Muted;
            bagCaption.text=$"背包    {m.Carry.Used:0.##} / {m.Carry.Capacity} 容量";
            int bagPages=Math.Max(1,(m.CarryGrid.slots.FindLastIndex(s=>s.amount>0)+27)/27);
            bagPage=Mathf.Clamp(bagPage,0,bagPages-1);
            if(bagPages>1)bagCaption.text+=$"   {bagPage+1}/{bagPages}";
            bagPrevious.gameObject.SetActive(bagPages>1);bagNext.gameObject.SetActive(bagPages>1);
            bagPrevious.interactable=bagPage>0;bagNext.interactable=bagPage<bagPages-1;
            int pages=Math.Max(1,(m.WarehouseGrid.slots.FindLastIndex(s=>s.amount>0)+54)/54);
            stockPage=Mathf.Clamp(stockPage,0,pages-1);
            upperCaption.text=warehouseView?$"仓库    {m.Warehouse.Used:0.##} / {m.Warehouse.Capacity}   {stockPage+1}/{pages}":"附近地面    点击拾取，拿空即消失";
            stockPrevious.gameObject.SetActive(warehouseView&&pages>1);stockNext.gameObject.SetActive(warehouseView&&pages>1);
            stockPrevious.interactable=stockPage>0;stockNext.interactable=stockPage<pages-1;
            nearbyPileIds=m.Ground.Where(p=>p.Amount>0&&PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)<=m.Settings.interactionRadius).OrderBy(p=>p.Id).Select(p=>p.Id).Take(18).ToArray();
            for(int i=0;i<stockCells.Length;i++)
            {
                stockCells[i].rect.gameObject.SetActive(warehouseView);int index=stockPage*54+i;stockCells[i].input.index=index;
                var slot=index<m.WarehouseGrid.slots.Count?m.WarehouseGrid.slots[index]:null;
                PaintInventoryCell(stockCells[i],slot?.item??ItemId.WasteWood,slot?.amount??0,slot!=null&&m.Warehouse.Available(slot.item)<slot.amount,!m.NearWarehouse);
            }
            for(int i=0;i<groundCells.Length;i++)
            {
                groundCells[i].rect.gameObject.SetActive(!warehouseView);
                var pile=i<nearbyPileIds.Length?m.Ground.FirstOrDefault(p=>p.Id==nearbyPileIds[i]):null;
                PaintInventoryCell(groundCells[i],pile?.Item??ItemId.WasteWood,pile?.Amount??0,pile!=null&&pile.Reserved>0,false);
            }
            for(int i=0;i<bagCells.Length;i++)
            {
                int index=bagPage*27+i;bagCells[i].input.index=index;
                var slot=m.CarryGrid.slots[index];PaintInventoryCell(bagCells[i],slot.item,slot.amount,m.Carry.Available(slot.item)<slot.amount,false);
            }
            float bagY=warehouseView?520:304;
            bagCaption.rectTransform.anchoredPosition=new Vector2(28,-(bagY-40));bagGrid.anchoredPosition=new Vector2(28,-bagY);
            bagPrevious.GetComponent<RectTransform>().anchoredPosition=new Vector2(412,-(bagY-40));bagNext.GetComponent<RectTransform>().anchoredPosition=new Vector2(496,-(bagY-40));
            inventoryPrimary.interactable=inspectedItem.HasValue&&!m.Interacting&&m.Hand==null&&!m.Paused&&(!warehouseView||m.NearWarehouse);
            inventoryPrimary.GetComponentInChildren<Text>().text=warehouseView&&!m.NearWarehouse?"请先前往仓库存取":inspectedContainer==2?"拾取这一叠":warehouseView?(inspectedContainer==1?"快速取到背包":"快速存入仓库"):"打开仓库存取";
            inventoryGo.GetComponentInChildren<Text>().text=m.NearWarehouse?"打开仓库与背包":"前往仓库并打开";
            inventorySell.interactable=inspectedItem.HasValue&&inspectedContainer==1&&m.Warehouse.Available(inspectedItem.Value)>0&&m.Hand==null;
            inventoryDrop.interactable=inspectedItem.HasValue&&inspectedContainer==0&&m.Carry.Available(inspectedItem.Value)>0&&m.Hand==null&&!m.Interacting&&!m.Paused;
            if(inspectedItem.HasValue)
            {
                ItemId item=inspectedItem.Value;itemTitle.text=ItemCatalog.Name(item);itemArtwork.enabled=true;itemArtwork.sprite=game.appearance.ItemSprite(item);
                itemQuantity.text=$"背包 {m.Carry.Count(item)}   仓库 {m.Warehouse.Count(item)}\n每份占 {ItemCatalog.CapacityTicks(item)/20m:0.##} 容量 · 预约 {m.Carry.Reserved(item)+m.Warehouse.Reserved(item)}";
                itemDescription.text=PrototypeKnowledge.Purpose(item);
                var quote=m.QuoteSale(item,inventorySaleQuantity);
                inventoryTradeInfo.text=$"当前售价 {m.SalePrice(item):0.00}/份\n仓库出售 {quote.Amount} 份，预计收入 {quote.Income:0.00}钱\n欠薪 {m.UnpaidWages:0.00} · 下期工资 {m.NextPayrollCents/100m:0.00}";
                inventorySell.GetComponentInChildren<Text>().text=$"出售 {quote.Amount} 份";
                inventorySell.interactable=inventorySell.interactable&&quote.Amount>0;
            }
            else
            {itemTitle.text="指向格子查看物品";itemArtwork.enabled=false;itemQuantity.text="同类物品每格最多64份";itemDescription.text="上方是仓库或附近物品，下方是背包。左键拿整叠，右键拆半；Shift点击快速转移。容量按体积计算。";inventoryTradeInfo.text="选择物品可查看当前售价和出售预估。\n饮水保留生活保护量，预约物资不会出售。";}
            handView.gameObject.SetActive(m.Hand!=null);
            if(m.Hand!=null)
            {
                handArtwork.sprite=game.appearance.ItemSprite(m.Hand.item);handCount.text=m.Hand.amount.ToString();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(inventoryWindow,Input.mousePosition,null,out Vector2 point);
                handView.anchorMin=handView.anchorMax=new Vector2(0.5f,0.5f);handView.anchoredPosition=point+new Vector2(12,-12);handView.SetAsLastSibling();
            }
        }
        private void PaintInventoryCell(InventoryCell cell,ItemId item,int amount,bool reserved,bool distant)
        {
            cell.icon.enabled=amount>0;cell.icon.sprite=amount>0?game.appearance.ItemSprite(item):null;
            cell.count.text=amount>0?amount.ToString():"";cell.locked.text=amount>0&&reserved?"预约":"";
            cell.background.color=distant?new Color32(29,33,35,255):Inset;
            cell.icon.color=distant?new Color(1,1,1,0.5f):Color.white;
        }
        public void InspectInventorySlot(int container,int index)
        {
            if(!InventoryOpen)return;
            if(container==2)
            {
                var pile=index<nearbyPileIds.Length?game.Model.Ground.FirstOrDefault(p=>p.Id==nearbyPileIds[index]):null;
                if(pile==null)return;inspectedItem=pile.Item;
            }
            else
            {var grid=game.Model.Grid(container==1);if(index<0||index>=grid.slots.Count||grid.slots[index].amount==0)return;inspectedItem=grid.slots[index].item;}
            inspectedContainer=container;
        }
        public void ClickInventorySlot(int container,int index,bool right=false,bool shift=false)
        {
            if(!InventoryOpen||game.Model.Interacting)return;
            InspectInventorySlot(container,index);var m=game.Model;
            if(container==2)
            {
                if(m.Hand!=null){game.Report(new ActionResult(false,"先放下手中物品，再拾取地面物资。"));return;}
                if(index<nearbyPileIds.Length)game.Report(m.PickUp(right?1:int.MaxValue,null,nearbyPileIds[index]));
            }
            else if(m.Hand!=null)game.Report(m.PlaceInventoryStack(container==1,index,right));
            else if(shift)game.Report(m.QuickTransferStack(container==1,index));
            else game.Report(m.TakeInventoryStack(container==1,index,right));
            RefreshInventoryWindow(m);
        }
        public void BeginInventoryDrag(int container,int index)
        {if(container!=2&&game.Model.Hand==null)ClickInventorySlot(container,index);}
        public void EndInventoryDrag(int container,int index)
        {if(container!=2&&game.Model.Hand!=null)ClickInventorySlot(container,index);}
        private void InventoryPrimaryAction()
        {
            if(!inspectedItem.HasValue)return;var m=game.Model;ItemId item=inspectedItem.Value;
            if(inspectedContainer==2)
            {
                var pile=m.Ground.FirstOrDefault(p=>p.Item==item&&p.Amount>p.Reserved&&PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)<=m.Settings.interactionRadius);
                if(pile!=null)game.Report(m.PickUp(int.MaxValue,null,pile.Id));
                else{CloseInventory();game.GoToItem(item);}
                return;
            }
            if(!warehouseView){OpenWarehouse();return;}
            bool fromWarehouse=inspectedContainer==1;
            int slot=m.Grid(fromWarehouse).slots.FindIndex(s=>s.amount>0&&s.item==item);
            if(slot>=0)game.Report(m.QuickTransferStack(fromWarehouse,slot));
        }
    }
}
