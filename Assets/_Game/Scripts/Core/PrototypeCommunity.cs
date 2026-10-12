using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public enum SupplyPolicy { Charged, Free, Subsidized }

    [Serializable]
    public sealed class LivingNeeds
    {
        public double nextMeal = 300, nextRetry, foodSince = -1, waterSince = -1;
        public double foodRecovery, waterRecovery;
        public double foodRecoveryRate, waterRecoveryRate;
        public int foodDebt, waterDebt, emergencyWater;
        public string foodReason = "充足", waterReason = "充足";
        public LivingNeeds Copy() => (LivingNeeds)MemberwiseClone();
        public double FoodMultiplier(double now) => Math.Min(1-foodRecovery,foodSince<0?1:now-foodSince>=900?0.7:now-foodSince>=300?0.9:1);
        public double WaterMultiplier(double now) => Math.Min(1-waterRecovery,waterSince<0?1:now-waterSince>=600?0.6:now-waterSince>=180?0.85:1);
    }

    public sealed partial class PrototypeSimulation
    {
        public LivingNeeds PlayerNeeds { get; private set; } = new LivingNeeds();
        public SupplyPolicy FoodPolicy { get; private set; }
        public SupplyPolicy WaterPolicy { get; private set; }
        public bool ExternalSupply { get; private set; } = true;
        public bool EmergencyWater { get; private set; } = true;
        public bool AutomaticLivingSupply { get; private set; } = true;
        public long CommunityCashCents { get; private set; }
        public long ExternalLivingSpentCents { get; private set; }
        public long RetailIncomeCents { get; private set; }
        public int MealsConsumed { get; private set; }
        public int WaterConsumed { get; private set; }
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; }
        public bool RiotActive { get; private set; }
        public long RiotTargetCents { get; private set; }
        public long RiotPaidCents { get; private set; }
        public double RiotDeadline { get; private set; }
        public double RiotSeconds => Math.Max(0, RiotDeadline-GameSeconds);
        private double firstPayrollAt = double.MaxValue, nextLivingPeriod = 600;
        private int livingTurn;
        public int DrinkingReserve => 2*(residents.Count+1);
        public int MissingSupplyPeople => residents.Count(r=>r.Needs.foodDebt>0||r.Needs.waterDebt>0)+(PlayerNeeds.foodDebt>0||PlayerNeeds.waterDebt>0?1:0);

        public ActionResult SetSupplyPolicy(ItemId item, SupplyPolicy policy)
        {
            if ((item!=ItemId.Food&&item!=ItemId.DrinkingWater)||!Enum.IsDefined(typeof(SupplyPolicy),policy)) return Fail("选择食物或饮水供应政策。");
            if(item==ItemId.Food)FoodPolicy=policy;else WaterPolicy=policy;
            WakeNeeds();return Ok("政策已更新；首期发薪前仍免费，下一次需求重试会按新政策结算。");
        }
        public void SetExternalSupply(bool enabled) { ExternalSupply=enabled;WakeNeeds(); }
        public void SetEmergencyWater(bool enabled) { EmergencyWater=enabled;WakeNeeds(); }
        public void SetLivingDelivery(bool enabled) { AutomaticLivingSupply=enabled; }
        private void WakeNeeds() { PlayerNeeds.nextRetry=0;foreach(var r in residents)r.Needs.nextRetry=0; }

        private long RetailPrice(ItemId item)
        {
            var p=ItemCatalog.Price(item);
            decimal value=Math.Min(p.buy,MinimumPriceOnly(item)?p.minimum:Math.Max(p.minimum,p.buy*(decimal)Math.Exp(-salesPressure[(int)item]/100)));
            if((item==ItemId.Food?FoodPolicy:WaterPolicy)==SupplyPolicy.Subsidized)value*=0.5m;
            return (long)decimal.Round(value*100,0,MidpointRounding.AwayFromZero);
        }
        private bool Consume(Resident resident, LivingNeeds need, ItemId item, out string reason)
        {
            bool player=resident==null, free=player||GameSeconds+1e-6<firstPayrollAt||(item==ItemId.Food?FoodPolicy:WaterPolicy)==SupplyPolicy.Free;
            long wallet=player?cashCents:resident.CashCents;
            long retail=free?0:RetailPrice(item), external=(long)(ItemCatalog.Price(item).buy*100);
            bool stock=Warehouse.Available(item)>0;
            if(stock&&(retail==0||wallet>=retail)&&(!ExternalSupply||retail<=external))
            {
                if(!Warehouse.TryRemove(item,1))throw new InvalidOperationException("生活领取台账不一致。");
                if(!player&&retail>0){resident.CashCents-=retail;cashCents+=retail;RetailIncomeCents+=retail;salesPressure[(int)item]+=1;}
                reason=free?"库存免费领取":"库存购买";return true;
            }
            if(ExternalSupply&&wallet>=external)
            {
                if(player)cashCents-=external;else resident.CashCents-=external;
                ExternalLivingSpentCents+=external;reason="个人外购";return true;
            }
            if(!player&&item==ItemId.DrinkingWater&&EmergencyWater&&stock&&wallet<retail&&need.emergencyWater<2)
            {Warehouse.TryRemove(item,1);need.emergencyWater++;reason="紧急免费饮水";return true;}
            reason=stock?"买不起；调整免费政策或补发工资":ExternalSupply&&wallet<external?"缺库存且买不起外购":"仓库缺货；外购已关闭";
            return false;
        }
        private void TickNeeds(Resident person, LivingNeeds need, double dt)
        {
            if(need.foodSince<0)need.foodRecovery=Math.Max(0,need.foodRecovery-dt*need.foodRecoveryRate);
            if(need.waterSince<0)need.waterRecovery=Math.Max(0,need.waterRecovery-dt*need.waterRecoveryRate);
            while(GameSeconds+1e-6>=need.nextMeal)
            {need.foodDebt++;need.waterDebt++;if(need.foodSince<0)need.foodSince=need.nextMeal;if(need.waterSince<0)need.waterSince=need.nextMeal;need.nextMeal+=300;need.nextRetry=0;}
            if((need.foodDebt>0||need.waterDebt>0)&&GameSeconds+1e-6>=need.nextRetry)
            {
                double foodFactor=need.FoodMultiplier(GameSeconds),waterFactor=need.WaterMultiplier(GameSeconds);
                while(need.waterDebt>0&&Consume(person,need,ItemId.DrinkingWater,out need.waterReason)){need.waterDebt--;WaterConsumed++;}
                while(need.foodDebt>0&&Consume(person,need,ItemId.Food,out need.foodReason)){need.foodDebt--;MealsConsumed++;}
                if(need.waterDebt==0&&need.waterSince>=0){need.waterSince=-1;need.waterRecovery=1-waterFactor;need.waterRecoveryRate=need.waterRecovery/180;}
                if(need.foodDebt==0&&need.foodSince>=0){need.foodSince=-1;need.foodRecovery=1-foodFactor;need.foodRecoveryRate=need.foodRecovery/180;}
                need.nextRetry=GameSeconds+10;
            }
        }
        private bool Starved(LivingNeeds need) => need.foodSince>=0&&GameSeconds-need.foodSince>=1800||need.waterSince>=0&&GameSeconds-need.waterSince>=1200;
        private void TickCommunity(double seconds)
        {
            bool rentDue=GameSeconds+1e-6>=nextLivingPeriod;
            if(rentDue)
            {
                nextLivingPeriod+=600;PlayerNeeds.emergencyWater=0;
                foreach(var r in residents)
                {
                    r.Needs.emergencyWater=0;
                }
                livingTurn++;
            }
            TickNeeds(null,PlayerNeeds,seconds);
            for(int i=0;i<residents.Count;i++)
            {var r=residents[(i+livingTurn)%residents.Count];TickNeeds(r,r.Needs,seconds);}
            if(rentDue&&GameSeconds>=firstPayrollAt)
                foreach(var r in residents)
                    if(r.CashCents>=500){r.CashCents-=100;CommunityCashCents+=100;}
            if(Starved(PlayerNeeds)){EndGame(PlayerNeeds.waterSince>=0&&GameSeconds-PlayerNeeds.waterSince>=1200?"主角长期缺水，经营失败。":"主角长期缺食，经营失败。");return;}
            foreach(var r in residents.Where(r=>Starved(r.Needs)).ToArray())
            {
                if(r.Delivery!=null)
                {
                    if(!r.Delivery.Picked){if(r.Delivery.Source!=null)r.Delivery.Source.Release(r.Delivery.Token);else r.Delivery.Ground.Reserved-=r.Delivery.Amount;}
                    foreach(ItemId item in Enum.GetValues(typeof(ItemId)))ReturnAtOldSite(item,r.Cargo.Available(item),"worker:"+r.Id,(int)Math.Round(r.X),(int)Math.Round(r.Y));
                    ClearDelivery(r);
                }
                CommunityCashCents+=r.CashCents;residents.Remove(r);
            }
            if(RiotActive&&RiotPaidCents>=RiotTargetCents)RiotActive=false;
            if(RiotActive&&GameSeconds>=RiotDeadline)EndGame("欠薪暴动未在期限内平息，经营失败。");
        }
        private void CheckWageRiot()
        {
            if(!RiotActive&&EquivalentUnpaidPeriods>=3-1e-6)
            {RiotActive=true;RiotTargetCents=DesignRules.RiotPayment(UnpaidWageCents);RiotPaidCents=0;RiotDeadline=GameSeconds+240;manualStation=0;}
        }
        private void EndGame(string reason){Failed=true;FailureReason=reason;Paused=true;CancelInteraction();manualStation=0;}
        private double LivingEfficiency(Resident person) => (person?.Needs??PlayerNeeds).FoodMultiplier(GameSeconds)*(person?.Needs??PlayerNeeds).WaterMultiplier(GameSeconds);
    }
}
