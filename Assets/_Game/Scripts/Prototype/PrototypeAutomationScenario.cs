using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;

namespace CrazyRubbish.Prototype
{
    // Acceptance route: uses purchases, sales, walking, real construction and real workers.
    // No stocks, money, actor positions, durability or production progress are injected.
    public static class PrototypeAutomationScenario
    {
        public static int Run(PrototypeSimulation m)
        {
            void Require(ActionResult result)=>PrototypeStartingScenario.Require(result);
            m.SetPaused(false);m.LeaveManualRole();PrototypeStartingScenario.MoveTo(m,8,5);
            if(m.RunRequested)Require(m.ToggleWork());if(m.Woodworker!=null)Require(m.AssignWoodworker());
            m.SetAutomaticToolPurchase(false);m.SetAutomaticMachineStaffing(true);
            Ensure(m,ItemId.Motor,1);Ensure(m,ItemId.MachineBlade,1);
            foreach(var cost in DesignBaseline.BuildCost(64))Ensure(m,cost.Item,cost.Amount);
            var placed=m.PlaceFacility(64,20,12);Require(placed);int id=placed.Amount;
            void Port(int owner,int x,int y,bool output,int filter=-1)
            {
                Ensure(m,ItemId.SteelPart,1);PrototypeStartingScenario.MoveTo(m,8,5);
                for(int attempt=0;attempt<100;attempt++)
                {var result=m.PlacePort(owner,x,y,output,filter);if(result.Success)return;if(!result.Message.Contains("有人"))throw new InvalidOperationException(result.Message);m.Advance(0.5);}
                throw new InvalidOperationException("接口位置持续被占用。");
            }
            Port(-1,14,3,true,(int)ItemId.WasteWood);Port(id,19,13,false,(int)ItemId.WasteWood);
            Port(id,23,13,true);Port(-1,10,2,false);
            var input=m.FindConveyorRoute(15,3,18,13,0);
            if(input==null)throw new InvalidOperationException("验收产线无法形成连续输入路线。");
            void BuildPath(List<ConveyorLane> path)
            {
                for(int start=0;start<path.Count;start+=6)
                {
                    var chunk=path.Skip(start).Take(6).ToArray();int pairs=(chunk.Count(b=>!m.Conveyors.Any(a=>a.X==b.X&&a.Y==b.Y&&a.Direction==b.Direction))+1)/2;
                    if(pairs>0){Ensure(m,ItemId.Plank,pairs);Ensure(m,ItemId.SteelPart,pairs);}
                    PrototypeStartingScenario.MoveTo(m,8,5);
                    for(int attempt=0;;attempt++)
                    {var result=m.PlaceConveyor(chunk);if(result.Success)break;if(attempt>=100||!result.Message.Contains("有人"))throw new InvalidOperationException(result.Message);m.Advance(0.5);}
                }
            }
            BuildPath(input);var output=m.FindConveyorRoute(24,13,9,2,0);
            if(output==null)throw new InvalidOperationException("验收产线无法形成独立输出路线。");BuildPath(output);
            // Wire follows both transport routes; devices need their own perimeter connection.
            void Wire(List<WireCell> path)
            {
                if(path==null)throw new InvalidOperationException("验收电路无法连接。");
                for(int start=0;start<path.Count;start+=12)
                {var chunk=path.Skip(start).Take(12).ToArray();int count=chunk.Count(w=>!m.Wires.Any(a=>a.X==w.X&&a.Y==w.Y));int pieces=Math.Max(0,(count-m.WireCredit+3)/4);if(pieces>0)Ensure(m,ItemId.RecoveredWire,pieces);Require(m.PlaceWire(chunk));}
            }
            Wire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,input[0].X,input[0].Y));
            Wire(input.Select(b=>new WireCell{X=b.X,Y=b.Y}).ToList());Wire(output.Select(b=>new WireCell{X=b.X,Y=b.Y}).ToList());
            Wire(m.FindWireRoute(m.GeneratorFrontX,m.GeneratorFrontY,19,13));
            PrototypeStartingScenario.MoveTo(m,8,5);Require(m.ToggleMachine(id));
            var f=m.GetFacility(id);double deadline=m.GameSeconds+300;
            while((f.MachineBatches<3||f.RepairedDurability<=0||m.ConveyorDeliveredItems<20)&&m.GameSeconds<deadline&&!m.Failed)
            {
                m.Advance(1);
                if(m.Warehouse.Available(ItemId.Plank)>0)Require(m.Sell(ItemId.Plank,m.Warehouse.Available(ItemId.Plank)));
                if(m.UnpaidWageCents>0&&m.CashCents>=m.UnpaidWageCents)Require(m.PayWages());
            }
            if(m.Failed||f.MachineBatches<3||f.RepairedDurability<=0||m.ConveyorDeliveredItems<20)
                throw new InvalidOperationException($"电动产线未完成真实经营验收：批次{f.MachineBatches}，维护{f.RepairedDurability:0.##}，输送{m.ConveyorDeliveredItems}，状态{f.State}，现金{m.Money}。");
            return id;
        }
        private static readonly HashSet<ItemId> Essential=new HashSet<ItemId>{ItemId.Plank,ItemId.SteelPart,ItemId.RecoveredWire,ItemId.Motor,ItemId.MachineBlade,ItemId.WasteWood,ItemId.WoodChips,ItemId.Food,ItemId.DrinkingWater,ItemId.WoodHandSaw,ItemId.SawBlade,ItemId.Screwdriver};
        private static void FreeSpace(PrototypeSimulation m,int ticks)
        {
            foreach(ItemId item in ItemCatalog.All)
            {
                if(m.Warehouse.FreeTicks>=ticks)return;
                if(Essential.Contains(item)||ItemCatalog.Price(item).sale<=0||m.Warehouse.Available(item)==0)continue;
                m.Sell(item,m.Warehouse.Available(item));
            }
        }
        private static void Ensure(PrototypeSimulation m,ItemId item,int target)
        {
            for(int attempt=0;attempt<30;attempt++)
            {
                int missing=target-m.Warehouse.Available(item)-m.Carry.Available(item);if(missing<=0)return;
                FreeSpace(m,missing*ItemCatalog.CapacityTicks(item));
                var pile=m.Ground.Where(p=>p.Item==item&&p.Amount>p.Reserved).OrderBy(p=>PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)).FirstOrDefault();
                if(pile!=null)
                {
                    if(m.Carry.AvailableSlots(item)==0)
                    {PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);PrototypeStartingScenario.Act(m,()=>m.Deposit());continue;}
                    PrototypeStartingScenario.MoveTo(m,pile.X,pile.Y);PrototypeStartingScenario.Act(m,()=>m.PickUp(missing,item,pile.Id));
                    if(m.Warehouse.Available(item)+m.Carry.Available(item)>=target)return;
                    PrototypeStartingScenario.MoveTo(m,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);PrototypeStartingScenario.Act(m,()=>m.Deposit());continue;
                }
                var bought=m.Buy(item,missing);if(!bought.Success)throw new InvalidOperationException("正常采购不足："+ItemCatalog.Name(item)+" / "+bought.Message);
            }
            throw new InvalidOperationException("无法通过正常搬运取得建设材料："+ItemCatalog.Name(item));
        }
    }
}
