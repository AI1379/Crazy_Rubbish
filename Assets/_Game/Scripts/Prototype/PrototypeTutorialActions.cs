using System;
using CrazyRubbish.Core;

namespace CrazyRubbish.Prototype
{
    public static class PrototypeTutorialActions
    {
        // Tutorial shortcuts use exactly the normal game actions and timed interactions.
        public static ActionResult Execute(PrototypeSimulation m,TutorialAction action)
        {
            switch(action.operation)
            {
                case TutorialOperation.Pick: return m.PickUp(action.amount,action.item);
                case TutorialOperation.Deposit: return m.Deposit();
                case TutorialOperation.Withdraw: return m.Withdraw(action.item,action.amount);
                case TutorialOperation.BuildBench: return m.BuildBench();
                case TutorialOperation.FeedWood: return m.DeliverInput();
                case TutorialOperation.WorkWood: return m.ToggleWork();
                case TutorialOperation.TakeWood: return m.TakeOutput();
                case TutorialOperation.TakeWoodInput: return m.TakeInput();
                case TutorialOperation.BuildTable: return m.BuildTable();
                case TutorialOperation.BuildDismantle: return m.BuildDismantle();
                case TutorialOperation.FeedDevice: return m.DeliverInput(true);
                case TutorialOperation.WorkDismantle:
                    if(m.DismantleRequested && m.DismantleState==WorkState.NeedsOperator)m.ToggleDismantle();
                    return m.ToggleDismantle();
                case TutorialOperation.TakeDeviceOutput: return m.TakeOutput(true);
                case TutorialOperation.BuildGenerator: return m.BuildGenerator();
                case TutorialOperation.ConnectPower: return m.ConnectCommunityPower();
                case TutorialOperation.Fuel: return m.AddFuel(action.item,action.amount);
                case TutorialOperation.Recruit: return m.Recruit();
                case TutorialOperation.HandSaw: return m.HandOffTool(ItemId.WoodHandSaw);
                case TutorialOperation.AssignWorker: return m.AssignWoodworker();
                case TutorialOperation.Pay: return m.PayWages();
                case TutorialOperation.SelectHandle: return m.SelectRecipe(WoodRecipe.WoodHandle);
                case TutorialOperation.SelectPlank: return m.SelectRecipe(WoodRecipe.Plank);
                case TutorialOperation.BuyBlade: return m.Buy(ItemId.SawBlade);
                case TutorialOperation.BuySupply: return m.Buy(action.item,action.amount);
                case TutorialOperation.StartWood: return m.RunRequested?new ActionResult(true,"生产请求已开启。"):m.ToggleWork();
                case TutorialOperation.SellResource: return m.Sell(action.item,action.amount);
                case TutorialOperation.FeedAssembly: return m.FeedAssembly();
                case TutorialOperation.WorkAssembly: return m.ToggleAssembly();
                case TutorialOperation.Sell:
                    decimal previous=m.Money;
                    if(m.Warehouse.Available(ItemId.Plank)>0)m.Sell(ItemId.Plank,int.MaxValue);
                    if(m.Warehouse.Available(ItemId.WoodHandle)>0)m.Sell(ItemId.WoodHandle,int.MaxValue);
                    return new ActionResult(m.Money>previous,$"已出售仓库木制品，获得 {m.Money-previous:0.00} 钱。");
                default: return new ActionResult(false,"请按本课说明选择操作。");
            }
        }
    }
}
