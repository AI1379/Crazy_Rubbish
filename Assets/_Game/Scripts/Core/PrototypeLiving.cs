using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // Per-resident happiness causes; each loss ramps after a grace period and recovers over 180 s once fixed.
    [Serializable] public sealed class HappinessState
    {
        public double[] since={-1,-1,-1,-1,-1};
        public double[] loss=new double[5];
        public double[] recovery=new double[5];
        public double clothesSeconds=4800*0.7,lowSince=-1,restUntil,nextClothesRetry;
        public bool clothesMissing,rioting;
        public HappinessState Copy()=>new HappinessState{since=(double[])since.Clone(),loss=(double[])loss.Clone(),recovery=(double[])recovery.Clone(),clothesSeconds=clothesSeconds,lowSince=lowSince,restUntil=restUntil,nextClothesRetry=nextClothesRetry,clothesMissing=clothesMissing,rioting=rioting};
    }
    public sealed partial class PrototypeSimulation
    {
        public static readonly string[] HappinessCauses={"no_bed","housing_power","no_light","no_clothes","unclean"};
        public static readonly string[] HappinessCauseNames={"没有床位","住房缺电","缺少照明","缺少衣物","清洁不足"};
        private static readonly double[,] HappinessRules={{60,360,20},{60,300,10},{60,300,5},{120,600,20},{120,600,15}};
        public const double HappinessRiotThreshold=25,HappinessSettle=40,ClothesLifetime=4800;
        public bool HappinessRiot {get;private set;}
        public double HappinessRiotDeadline {get;private set;}
        public double HappinessRiotSeconds=>Math.Max(0,HappinessRiotDeadline-GameSeconds);
        public bool PromiseMade {get;private set;}
        public int BonusLevel {get;private set;}
        public double BonusUntil {get;private set;}
        public double BonusFactor=>GameSeconds<BonusUntil?BonusLevel*0.1:0;
        public long PlayerRentCents {get;private set;}=100;
        public bool RestService {get;private set;}
        public double HeroClothesSeconds {get;private set;}=ClothesLifetime;
        public double SoapUnits {get;private set;}
        public bool CommunityClean {get;private set;}=true;
        public long BonusPaidCents {get;private set;}
        private double nextCleaningPeriod=600;

        public double AverageHappiness=>residents.Count==0?70:residents.Average(r=>r.Happiness);
        public IEnumerable<string> ActiveCauses(Resident r)=>Enumerable.Range(0,5).Where(i=>r.Mood.since[i]>=0).Select(i=>HappinessCauseNames[i]);
        private bool CleaningRequired=>HasDiscovered(ItemId.CleanRag)||TechnologyUnlocked("washing");

        // ---- payroll bonus, rent, rest -------------------------------------------------------
        public ActionResult PayBonus(int level)
        {
            if(level<1||level>3)return Fail("奖金档位为1—3（每档每人2钱，效率+10%）。");
            if(UnpaidWageCents>0)return Fail("先补清欠薪，才能发放奖金。");
            if(residents.Count==0)return Fail("还没有居民。");
            long cost=level*200L*residents.Count;if(cashCents<cost)return Fail($"发放需要{cost/100m:0.##}钱。");
            cashCents-=cost;BonusPaidCents+=cost;foreach(var r in residents)r.CashCents+=level*200L;
            BonusLevel=level;BonusUntil=nextWage;
            return Ok($"奖金已发放，人工效率+{level*10}%，持续到下次计划发薪。");
        }
        public ActionResult SetRent(long cents)
        {
            if(cents<0||cents>200)return Fail("自建住房租金范围为0—2钱/600秒。");
            PlayerRentCents=cents;return Ok($"自建住房租金调整为{cents/100m:0.##}钱/600秒；买不起不形成欠租。");
        }
        public void SetRestService(bool enabled)=>RestService=enabled;
        public ActionResult MakePromise()
        {
            if(!HappinessRiot)return Fail("当前没有生活暴动。");
            if(PromiseMade)return Fail("承诺已作出，倒计时不会重置。");
            PromiseMade=true;return Ok("已承诺恢复住房供电、衣物与清洁；组织者只执行这些修复，不恢复普通生产。");
        }

        // ---- housing assignment --------------------------------------------------------------
        // Index 0..beds-2 lives in the old community house, the rest fill player-built houses in build order.
        private FacilityInstance HouseOf(int index,out bool hasBed)
        {
            hasBed=index<HousingCapacity-1;
            int old=Settings.housingCapacity-1;if(index<old||!hasBed)return null;
            return facilities.Where(f=>f.Built&&f.Kind==16).OrderBy(f=>f.Id).Skip((index-old)/4).FirstOrDefault();
        }
        private bool HouseLit(FacilityInstance house)
        {
            int hx=house?.X+1??17,hy=house?.Y+1??3;
            if(CommunityLampPowered&&Distance(hx,hy,20,5)<=6)return true;
            return facilities.Any(f=>f.Built&&f.Kind==32&&f.PowerRatio>=DesignBaseline.LampValidRatio&&Distance(hx,hy,f.X,f.Y)<=6);
        }

        // ---- per-tick living ------------------------------------------------------------------
        private void TickHappiness(double seconds)
        {
            HeroClothesSeconds=Math.Max(0,HeroClothesSeconds-seconds);
            if(HeroClothesSeconds<=0&&GameSeconds>=heroClothesRetry){heroClothesRetry=GameSeconds+10;if(BuyClothes(null))HeroClothesSeconds=ClothesLifetime;}
            if(GameSeconds+1e-6>=nextCleaningPeriod){nextCleaningPeriod+=600;if(CleaningRequired)RunCleaningPeriod();else CommunityClean=true;}
            for(int i=0;i<residents.Count;i++)
            {
                var r=residents[i];var m=r.Mood;
                m.clothesSeconds=Math.Max(0,m.clothesSeconds-seconds);
                if(m.clothesSeconds<=0&&!m.clothesMissing){m.clothesMissing=true;ReturnAtOldSite(ItemId.OldClothes,1,"",WarehouseX,WarehouseY);}
                if(m.clothesMissing&&GameSeconds>=m.nextClothesRetry){m.nextClothesRetry=GameSeconds+10;if(BuyClothes(r)){m.clothesMissing=false;m.clothesSeconds=ClothesLifetime;}}
                var house=HouseOf(i,out bool bed);
                bool[] active={
                    !bed,
                    bed&&(house==null?!HousingPowered:house.PowerRatio<DesignBaseline.HousingValidRatio),
                    bed&&!HouseLit(house),
                    m.clothesMissing,
                    CleaningRequired&&!CommunityClean};
                for(int c=0;c<5;c++)
                {
                    if(active[c]){if(m.since[c]<0)m.since[c]=GameSeconds;m.loss[c]=Math.Max(m.loss[c],DesignRules.HappinessLoss(GameSeconds-m.since[c],HappinessRules[c,0],HappinessRules[c,1],HappinessRules[c,2]));m.recovery[c]=0;}
                    else
                    {
                        if(m.since[c]>=0){m.since[c]=-1;m.recovery[c]=m.loss[c]/180;}
                        m.loss[c]=Math.Max(0,m.loss[c]-seconds*m.recovery[c]);
                    }
                }
                if(RestService&&GameSeconds>=m.restUntil&&r.CashCents>=400+1000+100)
                {r.CashCents-=100;cashCents+=100;m.restUntil=GameSeconds+600;}
                r.Happiness=Math.Max(0,Math.Min(100,70-m.loss.Sum()+(GameSeconds<m.restUntil?5:0)));
                if(r.Happiness<HappinessRiotThreshold){if(m.lowSince<0)m.lowSince=GameSeconds;}else m.lowSince=-1;
            }
            CheckHappinessRiot();
        }
        private double heroClothesRetry;
        private bool BuyClothes(Resident r)
        {
            long wallet=r==null?cashCents:r.CashCents;long external=(long)(ItemCatalog.Price(ItemId.Clothes).buy*100);
            if(Warehouse.Available(ItemId.Clothes)>0)
            {
                long retail=r==null||GameSeconds<firstPayrollAt?0:(long)Math.Round(Math.Min((double)ItemCatalog.Price(ItemId.Clothes).buy,SalePrice(ItemId.Clothes))*100);
                if(wallet>=retail)
                {Warehouse.TryRemove(ItemId.Clothes,1);if(r!=null){r.CashCents-=retail;cashCents+=retail;RetailIncomeCents+=retail;}return true;}
            }
            if(ExternalSupply&&wallet>=external){if(r==null)cashCents-=external;else r.CashCents-=external;ExternalLivingSpentCents+=external;return true;}
            return false;
        }
        // Every 600 s: one soap cleaning unit per person, and per four people one clean rag and one broom wear.
        private void RunCleaningPeriod()
        {
            int people=residents.Count+1,groups=(people+3)/4;
            bool ok=true;
            SoapUnits-=people;
            while(SoapUnits<0&&Warehouse.Available(ItemId.Soap)>0){Warehouse.TryRemove(ItemId.Soap,1);SoapUnits+=100;}
            if(SoapUnits<0){SoapUnits=0;ok=false;}
            for(int g=0;g<groups;g++)
            {
                if(Warehouse.Available(ItemId.CleanRag)>0){Warehouse.TryRemove(ItemId.CleanRag,1);ReturnAtOldSite(ItemId.DirtyRag,1,"",WarehouseX,WarehouseY);}else ok=false;
                var broom=tools.Where(t=>ItemCatalog.ToolFamily(t.Item)=="broom"&&t.Location=="warehouse"&&t.ReservedBy==0&&t.Durability>0).OrderBy(t=>t.Durability).FirstOrDefault();
                if(broom!=null)WearTool(broom.Id,1);else ok=false;
            }
            foreach(var r in residents)
            {
                // Community lighting 0.4 and cleaning 0.1 per 600 s, only from savings above the 4-money protection.
                long fee=50;if(GameSeconds>=firstPayrollAt&&r.CashCents-400>=fee){r.CashCents-=fee;CommunityCashCents+=fee;}
            }
            CommunityClean=ok;
        }
        private void CheckHappinessRiot()
        {
            if(HappinessRiot)
            {
                var organizers=residents.Where(r=>r.Mood.rioting).ToArray();
                bool settled=organizers.All(r=>r.Mood.since.All(s=>s<0)&&r.Happiness>=HappinessSettle);
                if(settled||organizers.Length==0){HappinessRiot=false;PromiseMade=false;foreach(var r in organizers)r.Mood.rioting=false;return;}
                if(GameSeconds>=HappinessRiotDeadline)EndGame("生活暴动未在期限内平息，组织者包围并把主角丢下了洞。");
                return;
            }
            var low=residents.Where(r=>r.Mood.lowSince>=0&&GameSeconds-r.Mood.lowSince>=180).ToArray();
            int needed=Math.Max(2,(int)Math.Ceiling(0.2*residents.Count));
            if(residents.Count>=2&&low.Length>=needed)
            {
                HappinessRiot=true;HappinessRiotDeadline=GameSeconds+240;PromiseMade=false;manualStation=0;
                foreach(var r in low)r.Mood.rioting=true;
            }
        }
    }
}
