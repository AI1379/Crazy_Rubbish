using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class PlanGridSlot { public string key; public long ticks; }
    [Serializable] public sealed class PlanInventoryGrid
    {
        public int site;
        public List<PlanGridSlot> slots = new List<PlanGridSlot>();
    }
    public sealed class PlanInventoryHand
    {
        public int site, origin;
        public string key;
        public long ticks;
        internal readonly List<PlanHandPart> parts = new List<PlanHandPart>();
    }
    internal sealed class PlanHandPart { public PlanStack stack; public long ticks; }
    public readonly struct PlanInventoryQuote
    {
        public readonly long ticks, cents;
        public PlanInventoryQuote(long ticks, long cents) { this.ticks = ticks; this.cents = cents; }
    }
    public static class PlanInventoryIdentity
    {
        public static string Key(PlanStack s)
        {
            var b = new StringBuilder(); Append(b, s); return b.ToString();
        }
        private static void Token(StringBuilder b, string s) { s = s ?? ""; b.Append(s.Length).Append(':').Append(s); }
        private static void Amounts(StringBuilder b, PlanAmount[] values)
        {
            b.Append('['); foreach (var a in values ?? new PlanAmount[0]) { Token(b, a.item); Token(b, a.quantity.ToString("R", CultureInfo.InvariantCulture)); } b.Append(']');
        }
        private static void Append(StringBuilder b, PlanStack s)
        {
            Token(b, s.item); b.Append('|').Append(s.origin).Append('|').Append(s.instance).Append('|').Append(s.resumeStage);
            Token(b, s.quality); Token(b, s.durability.ToString("R", CultureInfo.InvariantCulture)); Token(b, s.resumeProgress.ToString("R", CultureInfo.InvariantCulture)); Amounts(b, s.composition);
            b.Append('{'); foreach (var a in s.payload ?? new PlanPackedStack[0]) { b.Append(a.ticks).Append(':'); Append(b, a.ToStack()); } b.Append('}');
        }
        public static PlanStack Copy(PlanStack s, long ticks) => new PlanStack { item=s.item,ticks=ticks,origin=s.origin,instance=s.instance,durability=s.durability,quality=s.quality,composition=s.composition,resumeStage=s.resumeStage,resumeProgress=s.resumeProgress,payload=s.payload };
        public static void Put(PlanStock stock, IEnumerable<PlanStack> values)
        {
            foreach (var s in values)
            {
                string key = Key(s); var existing = stock.stacks.FirstOrDefault(v => Key(v) == key);
                if (existing == null) stock.stacks.Add(Copy(s, s.ticks)); else existing.ticks += s.ticks;
            }
        }
    }
    public sealed partial class PlanSimulation
    {
        public const long InventoryStackLimit = 64 * PlanStock.Scale;
        public PlanInventoryHand InventoryHand { get; private set; }
        public PlanStock InventoryStock(int site)
        {
            if (site == 0) return State.backpack;
            var s = Site(site); Require(s != null && !s.pending && s.type == "warehouse", "请选择已建成的仓库。"); return s.input;
        }
        public int NearbyInventoryWarehouse()
        {
            return State.sites.Where(s => s.type == "warehouse" && !s.pending).OrderBy(s => { var f=Front(s);return Distance(State.player.x,State.player.y,f.x,f.y); })
                .Where(s => { var f=Front(s);return Distance(State.player.x,State.player.y,f.x,f.y)<=1.5; }).Select(s=>s.id).FirstOrDefault();
        }
        private void InventoryAccess(int site)
        {
            Require(!HeroAboard, "主角正在升降舱内，不能搬运物品。");
            if (site == 0) return; var f=Front(Site(site)); Require(Distance(State.player.x,State.player.y,f.x,f.y)<=1.5, "请先走到这座仓库的操作侧。");
        }
        public PlanInventoryGrid InventoryGrid(int site)
        {
            var stock=InventoryStock(site); if(State.inventoryGrids==null)State.inventoryGrids=new List<PlanInventoryGrid>();
            var g=State.inventoryGrids.FirstOrDefault(v=>v.site==site);if(g==null){g=new PlanInventoryGrid{site=site};State.inventoryGrids.Add(g);}
            if(g.slots==null)g.slots=new List<PlanGridSlot>();int page=site==0?27:54;while(g.slots.Count<page)g.slots.Add(new PlanGridSlot());
            var totals=stock.stacks.Where(s=>s.ticks>0).GroupBy(PlanInventoryIdentity.Key).ToDictionary(a=>a.Key,a=>a.Sum(v=>v.ticks));
            if(InventoryHand!=null&&InventoryHand.site==site&&totals.ContainsKey(InventoryHand.key))totals[InventoryHand.key]-=InventoryHand.ticks;
            foreach(var slot in g.slots)if(slot==null||slot.ticks<0)throw new InvalidOperationException("物品栏布局损坏。");
            foreach(var group in g.slots.Where(s=>s.ticks>0).GroupBy(s=>s.key).ToArray())
            {
                long excess=group.Sum(s=>s.ticks)-(totals.TryGetValue(group.Key??"",out long n)?n:0);
                foreach(var slot in group.Reverse()){long cut=Math.Min(slot.ticks,Math.Max(0,excess));slot.ticks-=cut;excess-=cut;}
            }
            foreach(var pair in totals)
            {
                long left=pair.Value-g.slots.Where(s=>s.key==pair.Key).Sum(s=>s.ticks);
                while(left>0){var slot=g.slots.FirstOrDefault(s=>s.key==pair.Key&&s.ticks>0&&s.ticks<InventoryStackLimit)??g.slots.FirstOrDefault(s=>s.ticks==0);if(slot==null){slot=new PlanGridSlot();g.slots.Add(slot);}long n=Math.Min(left,InventoryStackLimit-slot.ticks);slot.key=pair.Key;slot.ticks+=n;left-=n;}
            }
            while(g.slots.Count%page!=0)g.slots.Add(new PlanGridSlot());return g;
        }
        public PlanStack InventorySlotStack(int site,int index)
        {
            var g=InventoryGrid(site);if(index<0||index>=g.slots.Count||g.slots[index].ticks<=0)return null;
            string key=g.slots[index].key;var s=InventoryStock(site).stacks.FirstOrDefault(v=>PlanInventoryIdentity.Key(v)==key);return s==null?null:PlanInventoryIdentity.Copy(s,g.slots[index].ticks);
        }
        private long InventoryMovable(int site,string key)
        {
            var stock=InventoryStock(site);long n=stock.stacks.Where(v=>PlanInventoryIdentity.Key(v)==key).Sum(v=>v.ticks-v.reserved);
            if(site>0){var s=stock.stacks.FirstOrDefault(v=>PlanInventoryIdentity.Key(v)==key);if(s!=null&&(s.item=="food"||s.item=="water"))n=Math.Min(n,Surplus(s.item));}return Math.Max(0,n);
        }
        private PlanInventoryHand ReserveInventoryHand(int site,int origin,string key,long amount)
        {
            Require(amount>0&&InventoryMovable(site,key)>=amount,"库存已被生产、搬运或生活保留，不能取走。");
            var h=new PlanInventoryHand{site=site,origin=origin,key=key,ticks=amount};long remaining=amount;
            foreach(var s in InventoryStock(site).stacks.Where(v=>PlanInventoryIdentity.Key(v)==key)){long n=Math.Min(remaining,s.ticks-s.reserved);if(n<=0)continue;s.reserved+=n;h.parts.Add(new PlanHandPart{stack=s,ticks=n});remaining-=n;if(remaining==0)break;}return h;
        }
        private bool InventoryInstance(PlanStack s)=>s.instance>0||Config.Item(s.item).durability>0||Config.Item(s.item).uses_per_item>0||Config.Item(s.item).payload.Length>0||!string.IsNullOrEmpty(Config.Item(s.item).instance_state);
        private long HalfInventory(PlanStack s,long n)
        {if(InventoryInstance(s))return n;return s.item.Contains("water")||s.item.Contains("effluent")||n%PlanStock.Scale!=0?(n+1)/2:((n/PlanStock.Scale+1)/2)*PlanStock.Scale;}
        public void TakeInventorySlot(int site,int index,bool half=false)
        {
            Require(InventoryHand==null,"请先放下手中物品。");InventoryAccess(site);Require(State.player.trip==null,"先完成当前拾取。");var stack=InventorySlotStack(site,index);Require(stack!=null,"这是空格。");
            long n=Math.Min(stack.ticks,InventoryMovable(site,PlanInventoryIdentity.Key(stack)));Require(n>0,"库存已被预约或生活保留。");if(half)n=HalfInventory(stack,n);
            InventoryHand=ReserveInventoryHand(site,index,PlanInventoryIdentity.Key(stack),n);InventoryGrid(site);State.revision++;
        }
        private void ReleaseInventoryParts(PlanInventoryHand h,long amount)
        {
            foreach(var part in h.parts){long n=Math.Min(amount,part.ticks);part.stack.reserved-=n;part.ticks-=n;h.ticks-=n;amount-=n;if(amount==0)break;}
        }
        public void ReturnInventoryHand()
        {
            var h=InventoryHand;if(h==null)return;var g=InventoryGrid(h.site);long amount=h.ticks;if(h.origin>=0&&h.origin<g.slots.Count){var slot=g.slots[h.origin];if(slot.ticks==0||slot.key==h.key){slot.key=h.key;slot.ticks+=Math.Min(amount,InventoryStackLimit-slot.ticks);}}ReleaseInventoryParts(h,amount);InventoryHand=null;InventoryGrid(h.site);State.revision++;
        }
        private List<PlanStack> ConsumeInventoryParts(PlanInventoryHand h,long amount)
        {
            var stock=InventoryStock(h.site);Require(h.parts.All(p=>p.ticks==0||stock.stacks.Contains(p.stack)&&p.stack.reserved>=p.ticks&&p.stack.ticks>=p.ticks),"物品预约已改变，请关闭背包后重试。");
            var result=new List<PlanStack>();foreach(var p in h.parts){long n=Math.Min(amount,p.ticks);if(n==0)continue;result.Add(PlanInventoryIdentity.Copy(p.stack,n));p.stack.ticks-=n;p.stack.reserved-=n;p.ticks-=n;h.ticks-=n;amount-=n;if(p.stack.ticks==0)stock.stacks.Remove(p.stack);if(amount==0)break;}return result;
        }
        private bool FitsInventoryChange(PlanStock stock,string removeKey,long remove,IEnumerable<PlanStack> add)
        {
            var copy=new PlanStock{capacity=stock.capacity,stacks=stock.stacks.Select(s=>PlanInventoryIdentity.Copy(s,s.ticks)).ToList()};
            foreach(var s in copy.stacks.Where(v=>PlanInventoryIdentity.Key(v)==removeKey)){long n=Math.Min(remove,s.ticks);s.ticks-=n;remove-=n;}
            copy.stacks.RemoveAll(s=>s.ticks==0);PlanInventoryIdentity.Put(copy,add);return copy.Used(Config)<=copy.capacity+1e-8;
        }
        private long InventoryFit(PlanStock target,PlanStack template,long wanted)
        {
            if(InventoryInstance(template))return FitsInventoryChange(target,null,0,new[]{PlanInventoryIdentity.Copy(template,wanted)})?wanted:0;
            long step=template.item.Contains("water")||template.item.Contains("effluent")||wanted%PlanStock.Scale!=0?1:PlanStock.Scale;
            long lo=0,hi=wanted/step;while(lo<hi){long mid=(lo+hi+1)/2;if(FitsInventoryChange(target,null,0,new[]{PlanInventoryIdentity.Copy(template,mid*step)}))lo=mid;else hi=mid-1;}return lo*step;
        }
        public void PlaceInventorySlot(int site,int index,bool one=false)
        {
            var hand=InventoryHand;Require(hand!=null,"当前没有手持物品。");InventoryAccess(hand.site);InventoryAccess(site);var grid=InventoryGrid(site);Require(index>=0&&index<grid.slots.Count,"无效格子。");var slot=grid.slots[index];
            var template=hand.parts.First(p=>p.ticks>0).stack;long n=one&&!InventoryInstance(template)?Math.Min(hand.ticks,PlanStock.Scale):hand.ticks;
            if(slot.ticks>0&&slot.key!=hand.key)
            {
                Require(!one,"交换物品请使用左键。");var other=InventorySlotStack(site,index);long otherAmount=slot.ticks;
                Require(InventoryMovable(site,slot.key)>=otherAmount,"目标物品已预约或生活保留，不能交换。");
                if(site!=hand.site)
                {
                    Require(FitsInventoryChange(InventoryStock(site),slot.key,otherAmount,new[]{PlanInventoryIdentity.Copy(template,n)})&&FitsInventoryChange(InventoryStock(hand.site),hand.key,n,new[]{PlanInventoryIdentity.Copy(other,otherAmount)}),"交换后容器容量不足。");
                    var opposite=ReserveInventoryHand(site,index,slot.key,otherAmount);var outgoing=ConsumeInventoryParts(hand,n);var incoming=ConsumeInventoryParts(opposite,otherAmount);
                    PlanInventoryIdentity.Put(InventoryStock(site),outgoing);PlanInventoryIdentity.Put(InventoryStock(hand.site),incoming);InventoryHand=ReserveInventoryHand(hand.site,hand.origin,PlanInventoryIdentity.Key(other),otherAmount);
                }
                else {ReleaseInventoryParts(hand,hand.ticks);InventoryHand=ReserveInventoryHand(site,index,slot.key,otherAmount);}
                slot.key=PlanInventoryIdentity.Key(template);slot.ticks=n;
            }
            else
            {
                n=Math.Min(n,InventoryStackLimit-slot.ticks);if(site!=hand.site)n=Math.Min(n,InventoryFit(InventoryStock(site),template,n));Require(n>0,"目标格或容器已满。");
                if(site==hand.site)ReleaseInventoryParts(hand,n);else PlanInventoryIdentity.Put(InventoryStock(site),ConsumeInventoryParts(hand,n));
                slot.key=hand.key;slot.ticks+=n;if(hand.ticks==0)InventoryHand=null;
            }
            InventoryGrid(hand.site);if(site!=hand.site)InventoryGrid(site);Discover(template.item);State.revision++;
        }
        public long QuickInventoryTransfer(int site,int index,int targetSite)
        {
            Require(InventoryHand==null,"请先放下手持物品。");Require((site==0)!=(targetSite==0),"请选择背包与一座仓库进行转移。");InventoryAccess(site);InventoryAccess(targetSite);
            var s=InventorySlotStack(site,index);Require(s!=null,"这是空格。");long n=InventoryFit(InventoryStock(targetSite),s,Math.Min(s.ticks,InventoryMovable(site,PlanInventoryIdentity.Key(s))));Require(n>0,"库存已保留或目标容量不足。");
            var h=ReserveInventoryHand(site,index,PlanInventoryIdentity.Key(s),n);PlanInventoryIdentity.Put(InventoryStock(targetSite),ConsumeInventoryParts(h,n));InventoryGrid(site);InventoryGrid(targetSite);Discover(s.item);State.revision++;return n;
        }
        internal void ReleaseInventoryTrip(PlanTrip trip)
        {
            var stock=Stock(trip.source);if(stock==null)return;
            if(string.IsNullOrEmpty(trip.stackKey)){stock.Release(trip.item,trip.ticks,trip.instance);return;}
            long left=trip.ticks;foreach(var s in stock.stacks.Where(v=>PlanInventoryIdentity.Key(v)==trip.stackKey)){long n=Math.Min(left,s.reserved);s.reserved-=n;left-=n;if(left==0)break;}
        }
        internal List<PlanStack> TakeInventoryTrip(PlanTrip trip)
        {
            if(string.IsNullOrEmpty(trip.stackKey))return Stock(trip.source).Take(trip.item,trip.ticks,true,trip.instance);
            var stock=Stock(trip.source);Require(stock!=null,"原物品已不存在。");long left=trip.ticks;
            Require(stock.stacks.Where(s=>PlanInventoryIdentity.Key(s)==trip.stackKey).Sum(s=>s.reserved)>=left,"原物品预约已改变。");var result=new List<PlanStack>();
            foreach(var s in stock.stacks.Where(v=>PlanInventoryIdentity.Key(v)==trip.stackKey).ToArray()){long n=Math.Min(left,s.reserved);if(n==0)continue;result.Add(PlanInventoryIdentity.Copy(s,n));s.reserved-=n;s.ticks-=n;left-=n;if(s.ticks==0)stock.stacks.Remove(s);if(left==0)break;}return result;
        }
        internal void CompleteInventoryPickup(PlanTrip trip,PlanStock target)
        {var values=TakeInventoryTrip(trip);if(string.IsNullOrEmpty(trip.stackKey))target.Put(values);else PlanInventoryIdentity.Put(target,values);}
        public void PickInventoryGround(int groundId,int stackIndex,long requested)
        {
            Require(InventoryHand==null,"先放下手持物品。");Require(!HeroAboard&&!State.paused&&!State.riot.active,"暂停、暴动或返程中无法拾取。");Require(State.player.trip==null,"先完成当前拾取。");
            var g=State.ground.FirstOrDefault(a=>a.id==groundId);Require(g!=null&&stackIndex>=0&&stackIndex<g.stock.stacks.Count,"原物品已不存在。");Require(Distance(State.player.x,State.player.y,g.x,g.y)<=Config.Rule("workers.pickup_range"),"请先走近物品。");
            var s=g.stock.stacks[stackIndex];long n=InventoryFit(State.backpack,s,Math.Min(requested,s.ticks-s.reserved));Require(n>0,"背包已满或物品已预约。");s.reserved+=n;
            State.player.trip=new PlanTrip{source="ground:"+groundId,target="backpack",item=s.item,ticks=n,instance=s.instance>0?s.instance:-1,stackKey=PlanInventoryIdentity.Key(s)};State.revision++;
        }
        public PlanInventoryQuote QuoteInventorySale(string item,long requested)
        {
            var d=Config.Item(item);if(!d.canSell)return new PlanInventoryQuote(0,0);long remaining=Math.Min(Math.Max(0,requested),Surplus(item)),sold=0;double pressure=State.markets.FirstOrDefault(m=>m.group==d.market_group)?.pressure??0,revenue=0;
            foreach(var w in Warehouses)foreach(var s in w.stacks.Where(v=>v.item==item))
            {long n=Math.Min(remaining,s.ticks-s.reserved);if(n<=0)continue;double price=AtFloor(item)?d.sell_floor:Math.Max(d.sell_floor,d.sell_base*Math.Exp(-pressure/Config.Rule("market.decay_constant")));revenue+=price*n/PlanStock.Scale*(d.durability>0?s.durability/d.durability:1);pressure+=n/(double)PlanStock.Scale;remaining-=n;sold+=n;}
            return new PlanInventoryQuote(sold,(long)Math.Floor(revenue*100+State.saleRemainder+1e-9));
        }
    }
}
