using System;
using System.Linq;
using CrazyRubbish.Core;
namespace CrazyRubbish.Prototype
{
    public static class PrototypeStartingScenario
    {
        public static void Run(PrototypeSimulation model)
        {
            Collect(model,ItemId.WoodHandSaw,1);
            Collect(model,ItemId.RecoveredFastener,4);Collect(model,ItemId.WasteWood,16);
            Require(model.BuildBench());Require(model.BuildDismantle());
            while(model.WoodInputStock.Count(ItemId.WasteWood)<8)
            {
                MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
                Act(model,()=>model.Withdraw(ItemId.WasteWood,8-model.WoodInputStock.Count(ItemId.WasteWood)));
                MoveTo(model,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(model,()=>model.DeliverInput());
            }
            Require(model.ToggleWork());WaitUntil(model,()=>model.CompletedBatches>=2,180);
            model.LeaveManualRole();
            while(model.Output.UsedTicks>0)
            {
                MoveTo(model,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(model,()=>model.TakeOutput());
                MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Deposit());
            }
            Collect(model,ItemId.SteelPart,8);Require(model.BuildTable());
            Collect(model,ItemId.ThermalModule,1);Collect(model,ItemId.GeneratorModule,1);Collect(model,ItemId.RecoveredWire,2);
            Require(model.BuildGenerator());
            Collect(model,ItemId.Screwdriver,1);Collect(model,ItemId.OldCutter,1);
            MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Withdraw(ItemId.Screwdriver,1));Act(model,()=>model.Withdraw(ItemId.OldCutter,1));
            MoveTo(model,PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);Act(model,()=>model.DeliverInput(true));Require(model.ToggleDismantle());
            WaitUntil(model,()=>model.DismantledBatches==1,120);model.LeaveManualRole();
            Act(model,()=>model.TakeOutput(true));
            MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Deposit());
            if(model.Warehouse.Count(ItemId.Motor)!=1||model.Warehouse.Count(ItemId.MachineBlade)!=1)throw new InvalidOperationException("固定旧切割装置产物不符。");
            Collect(model,ItemId.WasteWood,42);
            MoveTo(model,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);Act(model,()=>model.HandOffTool(ItemId.WoodHandSaw));
            MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Withdraw(ItemId.WasteWood,2));
            MoveTo(model,PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY);Act(model,()=>model.AddFuel(ItemId.WasteWood,2));
            for(int i=0;i<5;i++)Require(model.Recruit());
            Require(model.AssignWoodworker());if(!model.RunRequested)Require(model.ToggleWork());
            MoveTo(model,8,5);
            double due=model.GameSeconds+model.SecondsToWage;
            while(model.GameSeconds<due+0.1)
            {
                model.Advance(Math.Min(5,due+0.1-model.GameSeconds));
                if(model.Warehouse.Available(ItemId.Plank)>0)Require(model.Sell(ItemId.Plank,int.MaxValue));
            }
            if(model.UnpaidWages!=50)throw new InvalidOperationException("首期工资不符。");
            if(model.Money<50)throw new InvalidOperationException("物理运输首期收入不足50："+model.Money);
            Require(model.PayWages());
            if(model.UnpaidWages!=0||model.Residents.Sum(r=>r.CashCents)!=7500||model.DeliveredTrips==0)throw new InvalidOperationException("工资或实体配送未完成。");
            WaitUntil(model,()=>model.CompletedBatches>=12,180);
            if(model.Warehouse.Available(ItemId.Plank)>0)model.Sell(ItemId.Plank,int.MaxValue);
        }
        public static void Act(PrototypeSimulation model,Func<ActionResult> command)
        {
            Require(command());model.Advance(model.Settings.interactionSeconds+0.05);Require(model.LastInteraction);
        }
        public static void Collect(PrototypeSimulation model,ItemId item,int target)
        {
            for(int trip=0;trip<100;trip++)
            {
                if(model.Warehouse.Count(item)+model.Carry.Count(item)>=target)
                {
                    if(item!=ItemId.WoodHandSaw&&model.Carry.Count(item)>0){MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Deposit());}
                    return;
                }
                var pile=model.Ground.FirstOrDefault(p=>p.Item==item&&p.Amount>p.Reserved);
                if(pile==null)throw new InvalidOperationException("缺少开局物品："+item);
                MoveTo(model,pile.X,pile.Y);
                int need=target-model.Warehouse.Count(item)-model.Carry.Count(item);
                Act(model,()=>model.PickUp(need,item));
                if(item!=ItemId.WoodHandSaw){MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);Act(model,()=>model.Deposit());}
            }
            throw new InvalidOperationException("搬运循环未结束："+item);
        }
        public static void MoveTo(PrototypeSimulation model,double x,double y)
        {
            var path=model.FindPath(model.PlayerX,model.PlayerY,(int)x,(int)y);
            if(path==null)throw new InvalidOperationException("目标没有四邻格路径："+x+","+y);
            foreach(int cell in path)
            {
                double px=cell%model.Settings.mapWidth,py=cell/model.Settings.mapWidth;
                for(int step=0;step<1000;step++)
                {
                    double dx=px-model.PlayerX,dy=py-model.PlayerY;
                    if(Math.Abs(dx)+Math.Abs(dy)<0.001)break;
                    double distance=Math.Max(Math.Abs(dx),Math.Abs(dy));
                    double seconds=Math.Min(0.1,distance/(model.Settings.moveSpeed*model.Speed));
                    model.MovePlayer(dx,dy,seconds);model.Advance(seconds);
                    if(step==999)throw new InvalidOperationException("不能走到合法格。");
                }
            }
        }
        public static void WaitUntil(PrototypeSimulation model,Func<bool> condition,double timeout)
        {
            double end=model.GameSeconds+timeout;
            while(!condition()&&model.GameSeconds<end)model.Advance(0.5);
            if(!condition())throw new InvalidOperationException("等待超时："+model.State+" / "+model.DismantleState);
        }
        public static void Require(ActionResult result){if(!result.Success)throw new InvalidOperationException(result.Message);}
    }
}
