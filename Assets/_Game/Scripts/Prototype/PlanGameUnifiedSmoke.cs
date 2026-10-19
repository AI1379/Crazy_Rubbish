using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    [Serializable] public sealed class UnifiedGameReport
    {
        public bool success,seededFixtures=true,singleGame,inventoryUi,splitAndDrag,metadataAndSwap,capacityAndReservations,releaseRules,saveRoundtrip,interfaceSizes,constructionShopping;
        public string version,error;public bool development;
    }
    public sealed partial class PlanGameController
    {
        public IEnumerator SmokeUnifiedInventory(UnifiedGameReport report,string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("统一游戏验收："+message);}
            int Slot(int site,string item,string quality=null)
            {var g=Model.InventoryGrid(site);for(int i=0;i<g.slots.Count;i++){var s=Model.InventorySlotStack(site,i);if(s!=null&&s.item==item&&(quality==null||s.quality==quality))return i;}throw new InvalidOperationException("找不到物品："+item);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            Check(MenuOpen&&!session,"默认标题状态错误");Check(!menu.GetComponentsInChildren<Button>(true).Any(b=>b.name.Contains("旧版游戏")),"仍有第二套游戏入口");
            Check(FindObjectsOfType<PlanGameController>().Length==1&&FindObjectsOfType<PrototypeGame>(true).Length==0&&PlanTerrain.Width==250&&PlanTerrain.Height==250,"默认世界或活动控制器不正确");report.singleGame=true;
            menu.Find("新游戏 · 女主").GetComponent<Button>().onClick.Invoke();yield return null;
            var active=Model;var warehouse=Model.State.sites.First(s=>s.type=="warehouse");int w=warehouse.id;var front=Model.Front(warehouse);
            // Retain release E/Q semantics, independently from the added inventory gestures.
            Model.State.ground.Clear();Model.State.backpack.stacks.Clear();var pile=new PlanGround{id=Model.State.nextId++,x=9,y=4};pile.stock.Add("waste_wood",4*PlanStock.Scale);Model.State.ground.Add(pile);
            Model.State.player.x=9;Model.State.player.y=4;Model.SetPaused(false);PickupNearest();Model.Advance(.6);Check(Model.State.backpack.Count("waste_wood")==PlanStock.Scale,"E快捷键改变了release单份拾取规则");
            Model.State.player.x=front.x;Model.State.player.y=front.y;Model.State.backpack.Add("handsaw_wood",PlanStock.Scale,1,123.5,Model.State.nextId++);Model.Deposit(warehouse);
            Check(Model.State.backpack.Count("handsaw_wood")==0&&warehouse.input.Count("handsaw_wood")==PlanStock.Scale,"Q未沿用release入库规则");report.releaseRules=true;
            Model.SetPaused(true);Model.State.backpack.stacks.Clear();warehouse.input.stacks.Clear();Model.State.backpack.Add("boards",6*PlanStock.Scale);warehouse.input.Add("boards",10*PlanStock.Scale);
            OpenInventoryWindow(w);yield return null;
            Check(InventoryOpen&&ReferenceEquals(Model,active)&&inventoryBagCells.Length==27&&inventoryStockCells.Length==54,"格子背包没有使用当前游戏物资");
            if(screenshot!=null)
            {
                var cell=(RectTransform)inventoryBagCells[0].input.transform;var hits=new System.Collections.Generic.List<RaycastResult>();var point=RectTransformUtility.WorldToScreenPoint(null,cell.TransformPoint(cell.rect.center));
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<PlanInventoryCell>()==inventoryBagCells[0].input,"实际图形命中无法到达背包格子");
            }
            report.inventoryUi=true;var right=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right};inventoryBagCells[0].input.OnPointerClick(right);
            Check(Model.InventoryHand?.ticks==3*PlanStock.Scale,"右键没有拆半");Save();Check(Model.InventoryHand!=null&&Notice.Contains("手持物品"),"手持期间保存没有被保护");
            inventoryBagCells[1].input.OnPointerClick(right);Check(Model.InventoryHand.ticks==2*PlanStock.Scale,"右键没有放一份");CloseInventoryWindow();Check(Model.InventoryHand==null&&Model.State.backpack.Count("boards")==6*PlanStock.Scale,"关闭背包丢失了手持物品");
            OpenInventoryWindow(w);yield return null;var drag=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};drag.pointerCurrentRaycast=new RaycastResult{gameObject=inventoryStockCells[1].input.gameObject};
            inventoryBagCells[0].input.OnBeginDrag(drag);inventoryBagCells[0].input.OnDrag(drag);inventoryBagCells[0].input.OnEndDrag(drag);
            Check(Model.State.backpack.Count("boards")==PlanStock.Scale&&warehouse.input.Count("boards")==15*PlanStock.Scale,"实际拖动没有完成跨容器转移");
            ClickPlanInventory(1,1,false,true);Check(Model.State.backpack.Count("boards")==6*PlanStock.Scale&&warehouse.input.Count("boards")==10*PlanStock.Scale,"Shift快速转移失败");report.splitAndDrag=true;
            CloseInventoryWindow();Model.State.backpack.stacks.Clear();Model.State.inventoryGrids.Clear();
            Model.State.backpack.Add("raw_freshwater",PlanStock.Quantity(.75),1,0,0,null,"qualified_fresh_source");Model.State.backpack.Add("raw_freshwater",PlanStock.Quantity(.5),2,0,0,null,"industrial");
            int toolId=Model.State.nextId++;Model.State.backpack.Add("handsaw_wood",PlanStock.Scale,1,123.5,toolId);
            Model.QuickInventoryTransfer(0,Slot(0,"raw_freshwater","industrial"),w);Model.TakeInventorySlot(w,Slot(w,"boards"),true);Model.PlaceInventorySlot(w,3);Model.TakeInventorySlot(0,Slot(0,"raw_freshwater","qualified_fresh_source"));Model.PlaceInventorySlot(w,3);Model.ReturnInventoryHand();
            Check(warehouse.input.stacks.Count(s=>s.item=="raw_freshwater")==2&&warehouse.input.stacks.Single(s=>s.quality=="industrial").origin==2&&warehouse.input.stacks.Single(s=>s.quality=="qualified_fresh_source").ticks==PlanStock.Quantity(.75),"跨容器交换混淆了数量、品质或来源");report.metadataAndSwap=true;
            Model.TakeInventorySlot(0,Slot(0,"handsaw_wood"),true);Check(Model.InventoryHand.ticks==PlanStock.Scale&&Model.State.backpack.Count("handsaw_wood")==0,"工具实例被拆分或未预约");Model.ReturnInventoryHand();
            double capacity=warehouse.input.capacity;warehouse.input.capacity=warehouse.input.Used(Model.Config);long before=Model.State.backpack.Count("boards");
            bool refused=false;try{Model.QuickInventoryTransfer(0,Slot(0,"boards"),w);}catch(InvalidOperationException){refused=true;}Check(refused&&Model.State.backpack.Count("boards")==before,"容量不足时仍扣除了物品");warehouse.input.capacity=capacity;
            long protectedWater=Model.Protected("water");warehouse.input.Add("water",protectedWater);refused=false;try{Model.TakeInventorySlot(w,Slot(w,"water"));}catch(InvalidOperationException){refused=true;}Check(refused,"背包绕过了release生活库存保留");report.capacityAndReservations=true;
            Model.State.cash=100000;var quote=Model.QuoteMissingBuildMaterials("wood");long oldCash=Model.State.cash;Model.BuyMissingBuildMaterials("wood");Check(oldCash-Model.State.cash==quote.cents&&Model.State.sites.Count(s=>s.type=="wood")==0,"建设采购改价、免费补料或自动建造");report.constructionShopping=true;
            OpenInventoryWindow(w);yield return null;
            for(int i=0;i<3;i++)
            {
                CyclePlanInterfaceSize();yield return null;var corners=new Vector3[4];inventoryCard.GetWorldCorners(corners);Check(corners.All(v=>v.x>=-.1&&v.y>=-.1&&v.x<=Screen.width+.1&&v.y<=Screen.height+.1),"界面尺寸导致背包越界");Shot("-inventory-size"+planInterfaceSize);
            }
            report.interfaceSizes=true;CloseInventoryWindow();string expected=JsonUtility.ToJson(Model.InventoryGrid(0));long boards=Model.State.backpack.Count("boards");Save();Check(Notice=="已保存新版进度。","统一游戏保存失败");Model.State.cash+=999;Load();
            Check(Model.State.cash==oldCash-quote.cents&&Model.State.backpack.Count("boards")==boards&&JsonUtility.ToJson(Model.InventoryGrid(0))==expected&&Model.State.backpack.stacks.Single(s=>s.instance==toolId).durability==123.5,"存档未恢复布局、实物或工具耐久");
            Check(FindObjectsOfType<PrototypeGame>(true).Length==0&&FindObjectsOfType<PlanGameController>().Length==1,"存读档切换到了第二套游戏");report.saveRoundtrip=true;
            OpenInventoryWindow(w);InspectPlanInventory(1,Slot(w,"raw_freshwater","industrial"));yield return null;Check(!inventoryDetails.text.Contains("qualified_fresh_source")&&!inventoryDetails.text.Contains("industrial"),"物品界面暴露了品质内部标记");Shot("-inventory");
        }
    }
}
