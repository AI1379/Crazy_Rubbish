using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform inventoryOverlay,inventoryCard,inventoryHandView;
        private Text inventoryCaption,inventoryStatus,inventoryDetails,inventoryHandCount,inventoryBagCaption;
        private Image inventoryHandImage;
        private Button inventorySellButton,inventoryBuyOne,inventoryBuyFive;
        private int inventoryWarehouse,inventoryBagPage,inventoryStockPage,awaitInventoryWarehouse;
        private string inventoryItem;
        private PlanStack inventoryInspected;
        private int inventoryInspectContainer,inventoryInspectIndex;
        private long inventorySaleQuantity=PlanStock.Scale;
        private readonly List<(int ground,PlanStack stack)> inventoryGround=new List<(int,PlanStack)>();
        private sealed class InventoryCellView {public PlanInventoryCell input;public Image image;public Text count,reserved;public string key;}
        private InventoryCellView[] inventoryBagCells,inventoryStockCells;
        public bool InventoryOpen=>inventoryOverlay!=null&&inventoryOverlay.gameObject.activeSelf;
        private void BuildInventoryWindow()
        {
            inventoryOverlay=Box(planCanvas,"Inventory Window",0,0,0,0);inventoryOverlay.anchorMin=Vector2.zero;inventoryOverlay.anchorMax=Vector2.one;inventoryOverlay.offsetMin=inventoryOverlay.offsetMax=Vector2.zero;
            inventoryOverlay.GetComponent<Image>().color=new Color(0,0,0,.65f);inventoryOverlay.GetComponent<Image>().raycastTarget=true;
            inventoryCard=Box(inventoryOverlay,"Card",0,0,1100,780);inventoryCard.anchorMin=inventoryCard.anchorMax=inventoryCard.pivot=new Vector2(.5f,.5f);inventoryCard.anchoredPosition=Vector2.zero;inventoryCard.GetComponent<Image>().raycastTarget=true;
            Text(inventoryCard,"Title","背包与仓库",24,16,800,40,26);Button(inventoryCard,"关闭 I / Esc",918,16,158,36,CloseInventoryWindow);
            inventoryStatus=Text(inventoryCard,"Status","",24,60,1050,45,15);
            inventoryCaption=Text(inventoryCard,"Stock caption","附近地面",24,111,470,28,19);
            Button(inventoryCard,"上一页",474,108,88,30,()=>{inventoryStockPage=Math.Max(0,inventoryStockPage-1);RefreshInventoryWindow();});Button(inventoryCard,"下一页",570,108,88,30,()=>{inventoryStockPage++;RefreshInventoryWindow();});
            inventoryStockCells=BuildInventoryCells(inventoryCard,1,54,24,148);
            inventoryBagCaption=Text(inventoryCard,"Bag caption","背包",24,478,430,30,19);
            Button(inventoryCard,"背包上一页",422,477,112,30,()=>{inventoryBagPage=Math.Max(0,inventoryBagPage-1);RefreshInventoryWindow();});Button(inventoryCard,"背包下一页",542,477,116,30,()=>{inventoryBagPage++;RefreshInventoryWindow();});
            inventoryBagCells=BuildInventoryCells(inventoryCard,0,27,24,518);
            var detail=Box(inventoryCard,"Item details",690,111,386,575);detail.GetComponent<Image>().color=new Color32(20,33,36,255);
            inventoryDetails=Text(detail,"Description","指向格子查看物品。",16,14,354,220,17);
            Button(detail,"售1份",16,246,110,34,()=>{inventorySaleQuantity=PlanStock.Scale;RefreshInventoryDetails();});Button(detail,"售5份",134,246,110,34,()=>{inventorySaleQuantity=5*PlanStock.Scale;RefreshInventoryDetails();});Button(detail,"售可用",252,246,118,34,()=>{inventorySaleQuantity=long.MaxValue;RefreshInventoryDetails();});
            inventorySellButton=Button(detail,"确认全仓出售",16,290,354,38,()=>InventoryTrade(()=>Model.Sell(inventoryItem,inventorySaleQuantity)));
            inventoryBuyOne=Button(detail,"商店买1份",16,340,170,38,()=>InventoryTrade(()=>Model.Buy(inventoryItem,PlanStock.Scale)));inventoryBuyFive=Button(detail,"商店买5份",198,340,172,38,()=>InventoryTrade(()=>Model.Buy(inventoryItem,5*PlanStock.Scale)));
            Button(detail,"物品图鉴 / 来源查阅",16,392,354,38,()=>{CloseInventoryWindow();ShowManagement(3);});
            Button(detail,"前往最近仓库",16,444,354,38,()=>
            {
                var p=Model.State.player;var s=Model.State.sites.Where(v=>v.type=="warehouse"&&!v.pending).OrderBy(v=>{var f=Model.Front(v);return Vector2.Distance(new Vector2((float)p.x,(float)p.y),new Vector2(f.x,f.y));}).FirstOrDefault();
                if(s!=null)RequestWarehouseInventory(s);
            });
            Text(detail,"Protection","出售汇总所有仓库的可用余量；生活、种薯和运输保留仍按当前规则执行。",16,498,354,64,15);
            Text(inventoryCard,"Controls","左键拿放 / 交换；右键拆半 / 放1份；拖拽转移；Shift点击快速存取。\n品质、来源和工具实例分别保留。关闭窗口会放回手持物品。空格可暂停。",24,708,1050,52,16);
            inventoryHandView=Rect(inventoryOverlay,"Held stack",0,0,54,54);inventoryHandView.pivot=new Vector2(.5f,.5f);inventoryHandImage=inventoryHandView.gameObject.AddComponent<Image>();inventoryHandImage.preserveAspect=true;inventoryHandImage.raycastTarget=false;
            inventoryHandCount=Text(inventoryHandView,"Quantity","",0,32,88,24,16);inventoryHandView.gameObject.SetActive(false);inventoryOverlay.gameObject.SetActive(false);
        }
        private InventoryCellView[] BuildInventoryCells(Transform parent,int container,int count,float x,float y)
        {
            var result=new InventoryCellView[count];for(int i=0;i<count;i++)
            {
                var r=Box(parent,(container==0?"Bag":"Stock")+" Slot "+i,x+i%9*71,y+i/9*53,65,47);r.GetComponent<Image>().raycastTarget=true;r.GetComponent<Image>().color=new Color32(48,65,68,255);
                var icon=Rect(r,"Icon",12,3,40,40).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;
                var input=r.gameObject.AddComponent<PlanInventoryCell>();input.game=this;input.container=container;input.index=i;
                var amount=Text(r,"Quantity","",2,23,60,22,14);amount.alignment=TextAnchor.LowerRight;amount.gameObject.AddComponent<Shadow>();
                result[i]=new InventoryCellView{input=input,image=icon,count=amount,reserved=Text(r,"Reserved","",2,0,60,17,11)};
            }return result;
        }
        public void OpenInventoryWindow(int warehouse=0)
        {
            CancelMapGesture();CloseContext();CloseSettings();if(MenuOpen||SaveDialogOpen)return;
            Model.ReturnInventoryHand();awaitInventoryWarehouse=0;inventoryWarehouse=warehouse>0?warehouse:Model.NearbyInventoryWarehouse();inventoryBagPage=inventoryStockPage=0;inventoryItem=null;inventoryInspected=null;inventorySellButton.interactable=inventoryBuyOne.interactable=inventoryBuyFive.interactable=false;
            inventoryOverlay.gameObject.SetActive(true);inventoryOverlay.SetAsLastSibling();RefreshInventoryWindow();
        }
        public void CloseInventoryWindow()
        {Model?.ReturnInventoryHand();if(inventoryOverlay!=null)inventoryOverlay.gameObject.SetActive(false);if(inventoryHandView!=null)inventoryHandView.gameObject.SetActive(false);}
        private void ToggleInventoryWindow(){if(InventoryOpen)CloseInventoryWindow();else OpenInventoryWindow();}
        private void RequestWarehouseInventory(PlanSite site)
        {
            CloseInventoryWindow();var f=Model.Front(site);if(Vector2.Distance(new Vector2((float)Model.State.player.x,(float)Model.State.player.y),new Vector2(f.x,f.y))<=1.5f)OpenInventoryWindow(site.id);
            else {GoToSite(site);awaitInventoryWarehouse=site.id;Notice="到达仓库操作侧后打开该仓库。";}
        }
        private void CheckInventoryArrival()
        {
            if(awaitInventoryWarehouse<=0)return;var site=Model.Site(awaitInventoryWarehouse);if(site==null){awaitInventoryWarehouse=0;return;}var f=Model.Front(site);
            if(Vector2.Distance(new Vector2((float)Model.State.player.x,(float)Model.State.player.y),new Vector2(f.x,f.y))<=1.5f){awaitInventoryWarehouse=0;OpenInventoryWindow(site.id);}
        }
        private int InventorySite(int container)=>container==0?0:inventoryWarehouse;
        private int InventoryIndex(int container,int index)=>index+(container==0?inventoryBagPage*27:inventoryStockPage*54);
        private PlanStack InventoryCellStack(int container,int index)
        {
            int n=InventoryIndex(container,index);if(container==1&&inventoryWarehouse==0)
            {if(n<0||n>=inventoryGround.Count)return null;var a=inventoryGround[n];var g=Model.State.ground.FirstOrDefault(v=>v.id==a.ground);return g!=null&&g.stock.stacks.Contains(a.stack)?a.stack:null;}
            int site=InventorySite(container);var grid=Model.State.inventoryGrids?.FirstOrDefault(v=>v.site==site);if(grid==null||n<0||n>=grid.slots.Count||grid.slots[n].ticks<=0)return null;var slot=grid.slots[n];var value=Model.InventoryStock(site).stacks.FirstOrDefault(v=>PlanInventoryIdentity.Key(v)==slot.key);return value==null?null:PlanInventoryIdentity.Copy(value,slot.ticks);
        }
        private string InventoryQuantity(long ticks)=>(ticks/(double)PlanStock.Scale).ToString("0.####");
        private void InventoryOperation(Action action)
        {try{action();Notice="物品操作完成。";}catch(Exception e){Notice=e.Message;}RefreshInventoryWindow();RefreshPresentation(true);}
        public void ClickPlanInventory(int container,int index,bool right,bool shift)
        {
            if(!InventoryOpen)return;
            if(container==1&&inventoryWarehouse==0)
            {
                int n=InventoryIndex(container,index);if(n>=inventoryGround.Count)return;var a=inventoryGround[n];var s=InventoryCellStack(container,index);if(s==null)return;
                InventoryOperation(()=>Model.PickInventoryGround(a.ground,Model.State.ground.First(g=>g.id==a.ground).stock.stacks.IndexOf(a.stack),right?Math.Min(PlanStock.Scale,s.ticks-s.reserved):s.ticks-s.reserved));return;
            }
            int site=InventorySite(container),slotIndex=InventoryIndex(container,index);string visibleKey=(container==0?inventoryBagCells:inventoryStockCells)[index].key;
            InventoryOperation(()=>
            {
                var current=Model.InventorySlotStack(site,slotIndex);if((current==null?null:PlanInventoryIdentity.Key(current))!=visibleKey)throw new InvalidOperationException("库存已变化，请重新选择格子。");
                if(shift&&Model.InventoryHand==null){int target=site==0?inventoryWarehouse:0;if(site==0&&target==0)throw new InvalidOperationException("请先靠近仓库操作侧。");Model.QuickInventoryTransfer(site,slotIndex,target);}
                else if(Model.InventoryHand==null)Model.TakeInventorySlot(site,slotIndex,right);else Model.PlaceInventorySlot(site,slotIndex,right);
            });
        }
        public void BeginPlanInventoryDrag(int container,int index)
        {if(Model.InventoryHand==null)ClickPlanInventory(container,index,false,false);}
        public void EndPlanInventoryDrag(int container,int index)
        {if(Model.InventoryHand!=null&&!(container==1&&inventoryWarehouse==0))InventoryOperation(()=>Model.PlaceInventorySlot(InventorySite(container),InventoryIndex(container,index)));}
        public void MovePlanInventoryHand(Vector2 point)
        {
            if(!InventoryOpen||inventoryHandView==null)return;var h=Model.InventoryHand;inventoryHandView.gameObject.SetActive(h!=null);if(h==null)return;
            var s=Model.InventoryStock(h.site).stacks.FirstOrDefault(v=>PlanInventoryIdentity.Key(v)==h.key);if(s==null)return;
            inventoryHandImage.sprite=appearance.PlanSprite(s.item);inventoryHandCount.text=InventoryQuantity(h.ticks);var canvas=(RectTransform)planCanvas;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,point,null,out var local);inventoryHandView.anchoredPosition=new Vector2(local.x-canvas.rect.xMin,local.y-canvas.rect.yMax);inventoryHandView.SetAsLastSibling();
        }
        public void InspectPlanInventory(int container,int index)
        {var s=InventoryCellStack(container,index);if(s==null)return;inventoryItem=s.item;inventoryInspected=PlanInventoryIdentity.Copy(s,s.ticks);inventoryInspectContainer=container;inventoryInspectIndex=index;RefreshInventoryDetails();}
        private string InventoryQuality(string quality)
        {switch(quality){case "qualified_fresh_source":return "合格原水";case "industrial":return "非合格原水";case "repairable":return "可修复";case "usable":return "可直接使用";case "scrap":return "仅可拆解";case "recognized":return "已识别";case "unrecognized":return "待识别";case "worn":return "穿坏";default:return "单独保存品质";}}
        private string DescribeInventoryStack(PlanStack s)
        {
            var d=Model.Config.Item(s.item);return d.name+"\n数量 "+InventoryQuantity(s.ticks)+" · 占用 "+((d.uses_per_item>0?Math.Ceiling(s.ticks/(double)PlanStock.Scale):s.ticks/(double)PlanStock.Scale)*d.capacity).ToString("0.####")+"\n来源区域 "+s.origin+(string.IsNullOrEmpty(s.quality)?"":" · "+InventoryQuality(s.quality))+(d.durability>0?" · 耐久 "+s.durability.ToString("0.#")+" / "+d.durability:"")+"\n\n"+d.category;
        }
        private void InventoryTrade(Action action)
        {if(inventoryItem==null){Notice="先指向一种物品。";RefreshInventoryWindow();return;}InventoryOperation(()=>{if(Model.InventoryHand!=null)throw new InvalidOperationException("先放下手持物品再交易。");action();});}
        private void RefreshInventoryDetails()
        {
            if(inventoryDetails==null||inventoryItem==null)return;var candidate=InventoryCellStack(inventoryInspectContainer,inventoryInspectIndex);var s=inventoryInspected;
            if(s==null)s=new PlanStack{item=inventoryItem};else if(candidate!=null&&PlanInventoryIdentity.Key(candidate)==PlanInventoryIdentity.Key(s))s=candidate;else s=PlanInventoryIdentity.Copy(s,0);var quote=Model.QuoteInventorySale(inventoryItem,inventorySaleQuantity);var definition=Model.Config.Item(inventoryItem);inventorySellButton.interactable=quote.ticks>0&&Model.InventoryHand==null;inventorySellButton.transform.Find("Label").GetComponent<Text>().text="确认出售 "+InventoryQuantity(quote.ticks)+" 份";inventoryBuyOne.interactable=inventoryBuyFive.interactable=definition.canBuy&&Model.InventoryHand==null;
            inventoryDetails.text=DescribeInventoryStack(s)+"\n\n全仓可售 "+InventoryQuantity(quote.ticks)+"\n预计收入 "+(quote.cents/100m).ToString("0.00")+" 钱";
        }
        private void RefreshInventoryWindow()
        {
            if(!InventoryOpen||Model==null)return;
            if(inventoryWarehouse>0&&Model.Site(inventoryWarehouse)==null)inventoryWarehouse=0;
            var bag=Model.InventoryGrid(0);int bagPages=Math.Max(1,bag.slots.Count/27);inventoryBagPage=Mathf.Clamp(inventoryBagPage,0,bagPages-1);
            int stockCount;if(inventoryWarehouse>0)stockCount=Model.InventoryGrid(inventoryWarehouse).slots.Count;
            else{inventoryGround.Clear();var p=Model.State.player;foreach(var g in Model.State.ground.Where(g=>Vector2.Distance(new Vector2(g.x,g.y),new Vector2((float)p.x,(float)p.y))<=1.5f))for(int i=0;i<g.stock.stacks.Count;i++)if(g.stock.stacks[i].ticks>0)inventoryGround.Add((g.id,g.stock.stacks[i]));stockCount=inventoryGround.Count;}
            int pages=Math.Max(1,(stockCount+53)/54);inventoryStockPage=Mathf.Clamp(inventoryStockPage,0,pages-1);
            inventoryCaption.text=(inventoryWarehouse>0?"仓库 #"+inventoryWarehouse:"附近地面")+" · "+(inventoryStockPage+1)+" / "+pages;
            inventoryBagCaption.text="背包 · "+(inventoryBagPage+1)+" / "+bagPages+"    占用 "+Model.State.backpack.Used(Model.Config).ToString("0.####")+" / "+Model.State.backpack.capacity;
            inventoryStatus.text=Notice+(Model.InventoryHand!=null?" · 手持物品已预约，保存将在放下后进行。":"")+(inventoryWarehouse>0?" · 仅操作这座仓库；食水保留仍生效。":" · 地面拾取沿用正常距离与耗时。");
            void Render(InventoryCellView[] cells,int container)
            {
                for(int i=0;i<cells.Length;i++){var cell=cells[i];var stack=InventoryCellStack(container,i);cell.key=stack==null?null:PlanInventoryIdentity.Key(stack);cell.image.enabled=stack!=null;cell.image.sprite=stack==null?null:appearance.PlanSprite(stack.item);cell.count.text=stack==null?"":InventoryQuantity(stack.ticks);long reserved=0;
                    if(stack!=null){if(container==1&&inventoryWarehouse==0)reserved=stack.reserved;else{string key=PlanInventoryIdentity.Key(stack);reserved=Model.InventoryStock(InventorySite(container)).stacks.Where(v=>PlanInventoryIdentity.Key(v)==key).Sum(v=>v.reserved);}}
                    cell.reserved.text=reserved>0?"有预约":"";
                }
            }
            Render(inventoryBagCells,0);Render(inventoryStockCells,1);RefreshInventoryDetails();MovePlanInventoryHand(Input.mousePosition);
        }
    }
}
