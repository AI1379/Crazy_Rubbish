using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // v1 technology: first acquisition unlocks instantly, free and without consuming items.
    public sealed partial class PrototypeSimulation
    {
        private readonly bool[] soldSales=new bool[DesignCatalog.TechnologySales.Length];
        private static readonly string[] LegacySaleKeys={"wood","textile","metal","tools","plastic","agriculture"};
        private readonly HashSet<string> surveyedVeins=new HashSet<string>();
        public IReadOnlyCollection<string> SurveyedVeins=>surveyedVeins;
        public bool IsSaleSold(string key)=>soldSales[Array.FindIndex(DesignCatalog.TechnologySales,t=>t.Key==key)];
        public bool TechnologyUnlocked(string key)=>TechnologyUnlocked(DesignCatalog.Technology(key),new HashSet<string>());
        private bool TechnologyUnlocked(TechnologyDef t,HashSet<string> visiting)
        {
            if(t==null||!visiting.Add(t.Key))return false;
            if(t.RegionRequired>OwnedRegion)return false;
            if(t.AcquiredAll.Any(i=>!discovered.Contains(i)))return false;
            if(t.AcquiredAny.Length>0&&!t.AcquiredAny.Any(discovered.Contains))return false;
            return t.Parents.All(p=>TechnologyUnlocked(DesignCatalog.Technology(p),new HashSet<string>(visiting)));
        }
        public IEnumerable<TechnologyDef> UnlockedTechnologies=>DesignCatalog.Technologies.Where(t=>TechnologyUnlocked(t.Key));
        // Only entries whose inputs the player has seen are listed, so the tree does not reveal lower-layer information.
        public IEnumerable<TechnologyDef> VisibleTechnologies=>DesignCatalog.Technologies.Where(t=>TechnologyUnlocked(t.Key)||t.AcquiredAll.Concat(t.AcquiredAny).Any(discovered.Contains));
        public bool FacilityUnlocked(string key)
        {
            switch(key)
            {
                case "wood":case "disassembly":return true;
                case "bench":return TableUnlocked;
                case "generator":case "housing":return GeneratorUnlocked;
                case "streetlamp":return HasDiscovered(ItemId.LightingModule);
                case "electric_wood":return ElectricWoodUnlocked;
                case "auto_wood":return AutomaticWoodUnlocked;
                case "belt":case "port":return ConveyorsUnlocked;
            }
            return DesignCatalog.Technologies.Any(t=>t.Facilities.Contains(key)&&TechnologyUnlocked(t.Key));
        }
        public bool RecipeUnlocked(RecipeDef r)=>r!=null&&r.RegionRequired<=OwnedRegion&&r.Technologies.All(TechnologyUnlocked);
        public string TechnologyRequirement(TechnologyDef t)
        {
            var parts=new List<string>();
            var missing=t.AcquiredAll.Where(i=>!discovered.Contains(i)).Select(ItemCatalog.Name).ToArray();
            if(missing.Length>0)parts.Add("首次取得："+string.Join("、",missing));
            if(t.AcquiredAny.Length>0&&!t.AcquiredAny.Any(discovered.Contains))parts.Add("任一取得："+string.Join("/",t.AcquiredAny.Select(ItemCatalog.Name)));
            var parents=t.Parents.Where(p=>!TechnologyUnlocked(p)).Select(p=>DesignCatalog.Technology(p).Name).ToArray();
            if(parents.Length>0)parts.Add("先解锁："+string.Join("、",parents));
            if(t.RegionRequired>OwnedRegion)parts.Add($"需拥有第{t.RegionRequired}区");
            return parts.Count==0?"已解锁":string.Join("；",parts);
        }

        public bool IsTechnologySold(Technology technology)=>soldSales[SaleIndex(LegacySaleKeys[(int)technology])];
        private static int SaleIndex(string key)=>Array.FindIndex(DesignCatalog.TechnologySales,t=>t.Key==key);
        private bool MinimumPriceOnly(ItemId item)
        {
            for(int i=0;i<soldSales.Length;i++)if(soldSales[i]&&Array.IndexOf(DesignCatalog.TechnologySales[i].Items,item)>=0)return true;
            return false;
        }
        public bool SaleAvailable(TechnologySaleDef sale)
        {
            if(sale.RequiresTechnologies.All(TechnologyUnlocked))return true;
            // The first six sales also keep the material triggers already confirmed for the first-region demo.
            switch(sale.Key)
            {
                case "wood":return true;
                case "textile":return discovered.Contains(ItemId.WasteTextile)||discovered.Contains(ItemId.OldClothes);
                case "metal":return discovered.Contains(ItemId.SteelMaterial)||discovered.Contains(ItemId.CopperMaterial)||discovered.Contains(ItemId.AluminumMaterial);
                case "tools":return discovered.Contains(ItemId.Plank)&&discovered.Contains(ItemId.SteelPart);
                case "plastic":return discovered.Contains(ItemId.HeatingElement);
                case "agriculture":return discovered.Contains(ItemId.GrowingLight);
                default:return false;
            }
        }
        public ActionResult SellTechnology(Technology technology)
        {
            if((int)technology<0||(int)technology>5)return Fail("技术不存在或已售出。");
            return SellTechnology(LegacySaleKeys[(int)technology]);
        }
        public ActionResult SellTechnology(string key)
        {
            int index=SaleIndex(key);if(index<0||soldSales[index])return Fail("技术不存在或已售出。");
            var sale=DesignCatalog.TechnologySales[index];
            if(!SaleAvailable(sale))return Fail("先取得对应材料解锁科技。");
            cashCents+=(long)(sale.Cash*100);soldSales[index]=true;
            return Ok("技术使用权保留，对应商品永久最低价。收入"+sale.Cash.ToString("0.##")+"钱。");
        }
        private void RestoreSales(SimulationSnapshot s)
        {
            Array.Clear(soldSales,0,soldSales.Length);
            if(s.soldSales!=null&&s.soldSales.Length==soldSales.Length)Array.Copy(s.soldSales,soldSales,soldSales.Length);
            else if(s.soldTechnologies!=null)for(int i=0;i<Math.Min(6,s.soldTechnologies.Length);i++)soldSales[SaleIndex(LegacySaleKeys[i])]=s.soldTechnologies[i];
            surveyedVeins.Clear();if(s.surveyedVeins!=null)foreach(var v in s.surveyedVeins)surveyedVeins.Add(v);
        }
        private bool[] LegacySoldTechnologies()=>LegacySaleKeys.Select(k=>soldSales[SaleIndex(k)]).ToArray();
    }
}
