using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public enum AssemblyRecipe { Saw, Hammer, Screwdriver }
    public sealed partial class PrototypeSimulation
    {



        public bool AssemblyActive => assemblyToken!=0;
        public bool AssemblyWorking => AssemblyActive && ManualAt(4) && AtFront(TableFrontX,TableFrontY) && !Paused && !RiotActive;
        public double AssemblyProgress => assemblyProgress/AssemblySeconds;
        public int AssembledTools { get; private set; }
        public bool AutomaticToolPurchase { get; private set; }
        public string ToolSupplyNotice { get; private set; } = "自动采购关闭；可手动购买或在工作台制作。";

        private double toolPurchaseAt, toolBudgetReset=600;
        private long toolBudgetSpent;
        private int toolsBoughtMask;
        public ItemId AssemblyProduct => SelectedAssembly==AssemblyRecipe.Saw?ItemId.WoodHandSaw:SelectedAssembly==AssemblyRecipe.Hammer?ItemId.Hammer:ItemId.Screwdriver;
        public ItemId AssemblyEnd => SelectedAssembly==AssemblyRecipe.Saw?ItemId.SawBlade:SelectedAssembly==AssemblyRecipe.Hammer?ItemId.HammerHead:ItemId.ScrewdriverRod;
        public double AssemblySeconds => SelectedAssembly==AssemblyRecipe.Screwdriver?15:20;
        public void SetAutomaticToolPurchase(bool enabled){AutomaticToolPurchase=enabled;toolPurchaseAt=0;}
        public ActionResult SelectAssembly(AssemblyRecipe recipe)
        {
            if(!Enum.IsDefined(typeof(AssemblyRecipe),recipe))return Fail("无此装配配方。");
            if(AssemblyActive||TableInput.UsedTicks>0||TableOutput.UsedTicks>0)return Fail("先完成或取消批次，清空工作台输入和输出后切换。");
            SelectedAssembly=recipe;return Ok("工作台配方已切换。");
        }
        public ActionResult FeedAssembly()
        {
            if(!TableBuilt||!Near(TableFrontX,TableFrontY))return Fail("到工作台正面后投料。");
            return QueueInteraction(()=>{
                int count=0;
                foreach(var item in new[]{ItemId.WoodHandle,AssemblyEnd})count+=MoveStock(Carry,TableInput,"carry",StockKey("tableInput"),item,Math.Max(0,2-TableInput.Count(item)));
                return count>0?Ok("握柄和工作端已投入工作台。",count):Fail("背包需要木柄和对应工具工作端。");
            });
        }
        public ActionResult TakeAssembly(bool input=false)
        {
            if(!Near(TableFrontX,TableFrontY))return Fail("到工作台正面后取物。");
            return QueueInteraction(()=>{
                var stock=input?TableInput:TableOutput;int count=0;
                foreach(ItemId item in ItemCatalog.All)count+=MoveStock(stock,Carry,input?StockKey("tableInput"):StockKey("tableOutput"),"carry",item,stock.Available(item));
                return count>0?Ok("工作台物品已进入背包。",count):Fail("没有可取物品或背包已满。");
            });
        }
        public ActionResult ToggleAssembly()
        {
            if(!TableBuilt||!AtFront(TableFrontX,TableFrontY))return Fail("到工作台正面进入装配岗位。");
            if(RiotActive||Failed)return Fail("当前人工岗位停工。");
            manualStation=ManualAt(4)?0:3;return Ok(ManualAt(4)?"主角进入工作台岗位；需要木柄1和工作端1。":"主角退出工作台，进度保留。");
        }
        public ActionResult CancelAssembly()
        {
            if(AssemblyActive&&assemblyProgress>=AssemblySeconds-1e-6)return Fail("已完成待出料的装配批次不能退同批原料。");
            if(assemblyToken!=0)TableInput.Release(assemblyToken);assemblyToken=0;assemblyProgress=0;return Ok("装配批次取消，输入预约释放。");
        }
        private void RemoveAssemblyBatch()
        {
            if(AssemblyActive&&assemblyProgress>=AssemblySeconds-1e-6)
            {
                TableInput.Commit(assemblyToken);
                tools.Add(new ToolInstance{Id=nextToolId++,Item=AssemblyProduct,Location="tableCompleted"});
                ReturnAtOldSite(AssemblyProduct,1,"tableCompleted",Station(4).X,Station(4).Y);discovered.Add(AssemblyProduct);AssembledTools++;
                assemblyToken=0;assemblyProgress=0;
            }
            CancelAssembly();
        }
        private void TickWorkshop(double seconds)
        {
            if(!TableBuilt)return;
            if(ManualAt(4)&&AtFront(TableFrontX,TableFrontY)&&!RiotActive)
            {
                if(!AssemblyActive&&TableOutput.AvailableSlots(AssemblyProduct)>0)
                    assemblyToken=TableInput.Reserve(new ItemAmount(ItemId.WoodHandle,1),new ItemAmount(AssemblyEnd,1));
                if(AssemblyActive)assemblyProgress=Math.Min(AssemblySeconds,assemblyProgress+seconds*Math.Max(0.25,PressureAt(TableFrontX,TableFrontY)*LivingEfficiency(null)));
            }
            if(AssemblyActive&&assemblyProgress>=AssemblySeconds-1e-6&&TableOutput.TryAdd(AssemblyProduct,1))
            {
                TableInput.Commit(assemblyToken);tools.Add(new ToolInstance{Id=nextToolId++,Item=AssemblyProduct,Location=StockKey("tableOutput")});
                discovered.Add(AssemblyProduct);AssembledTools++;assemblyToken=0;assemblyProgress=0;
            }
        }
        private void TickToolPurchase()
        {
            if(!AutomaticToolPurchase||GameSeconds<toolPurchaseAt)return;
            toolPurchaseAt=GameSeconds+10;
            if(GameSeconds>=toolBudgetReset){toolBudgetReset=GameSeconds+600;toolBudgetSpent=0;toolsBoughtMask=0;}
            ItemId[] items={ItemId.WoodHandSaw,ItemId.Screwdriver,ItemId.Wrench};
            for(int i=0;i<items.Length;i++)
            {
                ItemId item=items[i];bool required=item==ItemId.WoodHandSaw?facilities.Any(f=>f.Built&&f.Kind==1&&f.RunRequested):facilities.Any(f=>f.Built&&f.Kind==2&&f.DismantleRequested&&(item!=ItemId.Wrench||ItemCatalog.NeedsWrench(f.SelectedDevice)));
                if(!required||tools.Any(t=>t.Item==item&&t.Durability>60&&(t.Location=="warehouse"||t.Location.StartsWith("benchTools")||t.Location.StartsWith("dismantleTools"))))continue;
                long cost=(long)(ItemCatalog.Price(item).buy*100),wageReserve=UnpaidWageCents+(long)(residents.Count*Settings.wagePerResident*100);
                if((toolsBoughtMask&(1<<i))!=0){ToolSupplyNotice="本周期该工具已补1把。";continue;}
                if(toolBudgetSpent+cost>2000){ToolSupplyNotice="本周期20钱工具采购预算不足。";continue;}
                if(cashCents-wageReserve<cost){ToolSupplyNotice="工具采购等待：优先保留欠薪和下期工资。";continue;}
                if(Warehouse.AvailableSlots(item)==0){ToolSupplyNotice="工具采购等待仓库空间。";continue;}
                if(Buy(item).Success){toolsBoughtMask|=1<<i;toolBudgetSpent+=cost;ToolSupplyNotice="已采购替补"+ItemCatalog.Name(item)+"，等待配送。";}
            }
        }
    }
}
