using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed class PlanBuildPurchaseQuote
    {
        public PlanAmount[] missing;
        public long cents;
        public string problem;
    }
    public sealed partial class PlanSimulation
    {
        public PlanBuildPurchaseQuote QuoteMissingBuildMaterials(string facility)
        {
            var quote=new PlanBuildPurchaseQuote { missing=Config.Facility(facility).build.GroupBy(a=>a.item).Select(g=>new PlanAmount{item=g.Key,quantity=Math.Max(0,g.Sum(a=>a.quantity)-(State.backpack.Count(g.Key)+Stored(g.Key))/(double)PlanStock.Scale)}).Where(a=>a.Ticks>0).ToArray() };
            var warehouses=Warehouses.Select(w=>new PlanStock{capacity=w.capacity,stacks=w.stacks.Select(s=>PlanInventoryIdentity.Copy(s,s.ticks)).ToList()}).ToArray();long cash=State.cash;
            foreach(var amount in quote.missing)
            {
                var d=Config.Item(amount.item);long cost=Cents(d.buy*amount.Ticks/PlanStock.Scale);quote.cents+=cost;
                if(!d.canBuy){quote.problem="无法购买："+d.name;continue;}
                if((d.durability>0||d.uses_per_item>0||!string.IsNullOrEmpty(d.instance_state))&&amount.Ticks%PlanStock.Scale!=0){quote.problem="工具与完整耗材需按整件购买。";continue;}
                if(cash<cost){quote.problem="现金不足。";continue;}
                var target=warehouses.FirstOrDefault(w=>w.Fits(Config,amount.item,amount.Ticks));if(target==null){quote.problem="仓库容量不足。";continue;}
                cash-=cost;target.Add(amount.item,amount.Ticks,State.region,d.durability,int.MinValue);
            }
            return quote;
        }
        public void BuyMissingBuildMaterials(string facility)
        {
            Require(InventoryHand==null,"先放下手持物品。");var quote=QuoteMissingBuildMaterials(facility);Require(quote.missing.Length>0,"建材已经齐全。");Require(quote.problem==null,quote.problem);
            foreach(var amount in quote.missing)Buy(amount.item,amount.Ticks);
        }
    }
}
