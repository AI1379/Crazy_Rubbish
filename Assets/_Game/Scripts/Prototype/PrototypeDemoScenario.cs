using System;
using CrazyRubbish.Core;

namespace CrazyRubbish.Prototype
{
    // A full demo acceptance route: only normal game actions, no injected stock or money.
    public static class PrototypeDemoScenario
    {
        public static void Run(PrototypeSimulation model)
        {
            PrototypeStartingScenario.Run(model);
            RunAfterStarting(model);
        }
        public static void RunAfterStarting(PrototypeSimulation model)
        {
            PrototypeStartingScenario.Require(model.SelectRecipe(WoodRecipe.WoodHandle));
            PrototypeStartingScenario.WaitUntil(model,()=>model.Warehouse.Available(ItemId.WoodHandle)>0,240);
            PrototypeStartingScenario.Require(model.Buy(ItemId.SawBlade));
            PrototypeStartingScenario.MoveTo(model,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
            PrototypeStartingScenario.Act(model,()=>model.Withdraw(ItemId.WoodHandle));
            PrototypeStartingScenario.Act(model,()=>model.Withdraw(ItemId.SawBlade));
            PrototypeStartingScenario.MoveTo(model,PrototypeSimulation.TableX,PrototypeSimulation.TableY);
            PrototypeStartingScenario.Act(model,()=>model.FeedAssembly());
            PrototypeStartingScenario.Require(model.ToggleAssembly());
            PrototypeStartingScenario.WaitUntil(model,()=>model.AssembledTools>=1,60);
            model.LeaveManualRole();
            PrototypeStartingScenario.Require(model.SelectRecipe(WoodRecipe.Plank));
            // Selling licenses is an explicit S5 financing choice with a lasting price penalty.
            // Keep wood technology, so the core production margin still recovers.
            PrototypeStartingScenario.Require(model.SellTechnology(Technology.Tools));
            PrototypeStartingScenario.Collect(model,ItemId.WasteTextile,1);
            PrototypeStartingScenario.Require(model.SellTechnology(Technology.Textile));
            PrototypeStartingScenario.Collect(model,ItemId.SteelMaterial,1);
            PrototypeStartingScenario.Require(model.SellTechnology(Technology.Metal));
            model.SetAutomaticToolPurchase(true);
            PrototypeStartingScenario.MoveTo(model,8,5);
            double end=model.GameSeconds+3600;
            while(!model.DemoCompleted&&!model.Failed&&model.GameSeconds<end)
            {
                model.Advance(5);
                foreach(var item in new[]{ItemId.Plank,ItemId.WoodHandle})
                    if(model.Warehouse.Available(item)>0)PrototypeStartingScenario.Require(model.Sell(item,int.MaxValue));
                if(model.UnpaidWageCents>0&&model.CashCents>0)PrototypeStartingScenario.Require(model.PayWages());
            }
            if(model.Failed)throw new InvalidOperationException(model.FailureReason);
            if(!model.DemoCompleted)throw new InvalidOperationException($"完整demo未达成：现金{model.Money}，木工{model.CompletedBatches}，欠薪{model.UnpaidWages}，缺供{model.MissingSupplyPeople}。");
        }
    }
}
