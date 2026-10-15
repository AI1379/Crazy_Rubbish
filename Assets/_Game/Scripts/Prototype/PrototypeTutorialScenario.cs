using System;
using System.Linq;
using CrazyRubbish.Core;

namespace CrazyRubbish.Prototype
{
    public static class PrototypeTutorialScenario
    {
        // Follows the same suggested actions as the tutorial player, without stock or cash injection.
        public static void Run(PrototypeSimulation m)
        {
            m.Tutorial.Begin();
            double end=m.GameSeconds+7200;
            for(int turn=0;turn<6000 && !m.Tutorial.Completed && !m.Failed && m.GameSeconds<end;turn++)
            {
                m.Tutorial.Refresh(m);
                var action=PrototypeTutorial.NextAction(m,m.Tutorial.step);
                if(action.operation==TutorialOperation.Wait){m.Advance(2);continue;}
                if(action.hasTarget && !action.AtTarget(m))PrototypeStartingScenario.MoveTo(m,action.x,action.y);
                if(action.operation==TutorialOperation.Trade)
                {
                    // Choose the visible financing option, as a player explicitly confirming each trade.
                    if(!m.IsTechnologySold(Technology.Tools))PrototypeStartingScenario.Require(m.SellTechnology(Technology.Tools));
                    else if(!m.IsTechnologySold(Technology.Textile))
                    {PrototypeStartingScenario.Collect(m,ItemId.WasteTextile,1);PrototypeStartingScenario.Require(m.SellTechnology(Technology.Textile));}
                    else if(!m.IsTechnologySold(Technology.Metal))
                    {
                        if(!m.Ground.Any(p=>p.Item==ItemId.SteelMaterial && p.Amount>p.Reserved) && m.Warehouse.Available(ItemId.SteelMaterial)==0){m.Advance(10);continue;}
                        PrototypeStartingScenario.Collect(m,ItemId.SteelMaterial,1);PrototypeStartingScenario.Require(m.SellTechnology(Technology.Metal));
                    }
                    else m.Advance(5);
                }
                else
                {
                    var result=PrototypeTutorialActions.Execute(m,action);
                    if(!result.Success)throw new InvalidOperationException($"教程第{m.Tutorial.step}课 / {action.operation}: {result.Message}");
                    if(m.Interacting){m.Advance(m.Settings.interactionSeconds+0.05);PrototypeStartingScenario.Require(m.LastInteraction);}
                }
                m.Advance(0.1);
            }
            if(m.Failed || !m.Tutorial.Completed || !m.DemoCompleted)
                throw new InvalidOperationException($"教程未完成：课{m.Tutorial.step}，{PrototypeTutorial.NextAction(m,m.Tutorial.step).operation} / {PrototypeTutorial.NextAction(m,m.Tutorial.step).instruction}，木工{m.State}，仓库{m.Warehouse.Used}，现场输出{m.Output.Used}，木柄{m.Warehouse.Count(ItemId.WoodHandle)}，木板{m.Warehouse.Count(ItemId.Plank)}，现金{m.Money}，时间{m.GameSeconds}，{m.FailureReason}");
        }
    }
}
