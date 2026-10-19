using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // Regions 2 and 3 are bought in order; the map grows to the v1 coordinates when they are owned.
    public sealed partial class PrototypeSimulation
    {
        public int OwnedRegion {get;private set;}=1;
        private readonly double[] regionBudgets=new double[16];
        private readonly int[] regionPending=Enumerable.Repeat(-1,16).ToArray();
        private static readonly string[] RegionCategories={"wood","metal_devices","plastic","textile","organic","life","paper","glass"};
        private static readonly double[][] RegionWeights={
            new double[]{27,28,25,12,5,3,0,0},
            new double[]{22,20,25,8,5,2,8,10}};
        public static RegionDef Region(int number)=>DesignCatalog.Regions[number-1];
        public RegionDef NextRegion=>OwnedRegion<DesignCatalog.Regions.Length?Region(OwnedRegion+1):null;
        public int RegionAt(int x,int y)
        {
            for(int r=DesignCatalog.Regions.Length;r>=2;r--){var def=Region(r);if(def.InLand(x,y)||def.InGarbage(x,y))return r;}
            return 1;
        }
        public bool OwnedGround(int x,int y)
        {
            if(IsOwnedGround(x,y))return true;
            for(int r=2;r<=OwnedRegion;r++){var def=Region(r);if(def.InLand(x,y)||def.InGarbage(x,y))return true;}
            return false;
        }
        private bool RegionBuildCell(int x,int y)
        {
            for(int r=2;r<=OwnedRegion;r++)if(Region(r).InLand(x,y))return true;
            return false;
        }
        public ActionResult PurchaseRegion()
        {
            var next=NextRegion;if(next==null)return Fail("三个地区均已取得。");
            if(Failed||Ended)return Fail("经营已结束。");
            long price=(long)(next.Price*100);
            if(cashCents<price)return Fail($"需要{next.Price:0}钱购买{next.Name}；当前现金{Money:0.00}。");
            cashCents-=price;OwnedRegion=next.Number;
            int width=next.Number>=3?86:52,height=next.Number>=3?32:Settings.mapHeight;
            if(Settings.mapWidth<width||Settings.mapHeight<height){var grown=Settings.Copy();grown.mapWidth=Math.Max(width,Settings.mapWidth);grown.mapHeight=Math.Max(height,Settings.mapHeight);Settings=grown;}
            int cx=(next.GarbageX0+next.GarbageX1)/2,cy=(next.GarbageY0+next.GarbageY1)/2;
            foreach(var q in next.FixedItems)
            {
                int count=q.Whole;
                for(int i=0;i<count;i++)
                {
                    int before=nextToolId;AddNatural(q.Item,1,cx,cy,true);
                    // Fixed old motors and drives in region 2 are guaranteed repairable.
                    if(next.Number==2&&(q.Item==ItemId.DamagedMotor||q.Item==ItemId.OldDrive))foreach(var t in tools.Where(t=>t.Id>=before&&t.Item==q.Item))t.Condition=1;
                }
            }
            geometryRevision++;GroundRevision++;ConstructionRevision++;
            return Ok($"已购买{next.Name}，固定来料已落在新垃圾区。",next.Number);
        }
        private (int x0,int y0,int x1,int y1) GarbageZone(int cx,int cy)
        {
            for(int r=2;r<=DesignCatalog.Regions.Length;r++){var def=Region(r);if(def.InGarbage(cx,cy)||def.InLand(cx,cy))return(def.GarbageX0,def.GarbageY0,def.GarbageX1,def.GarbageY1);}
            return(0,6,7,11);
        }
        private ItemId DrawRegionItem(int region,int category)
        {
            var def=Region(region);
            switch(RegionCategories[category])
            {
                case "wood":return ItemId.WasteWood;
                case "plastic":return ItemId.MixedPlastic;
                case "organic":return ItemId.OrganicMix;
                case "paper":return ItemId.WastePaper;
                case "glass":return ItemId.WasteGlass;
                case "life":{int d=random.Next(3);return d==0?ItemId.Food:d==1?ItemId.DrinkingWater:ItemId.Soap;}
                case "textile":
                {
                    double d=random.NextDouble()*100;
                    double cut=region==2?50:60,waste=region==2?30:20;
                    return d<cut?ItemId.CuttableTextile:d<cut+waste?ItemId.WasteTextile:ItemId.OldClothes;
                }
                default:
                {
                    double[] w=region==2?new double[]{30,15,10,5,40}:new double[]{25,15,5,10,45};
                    double d=random.NextDouble()*100;
                    if(d<w[0])return ItemId.SteelMaterial;d-=w[0];
                    if(d<w[1])return ItemId.CopperMaterial;d-=w[1];
                    if(d<w[2])return ItemId.AluminumMaterial;d-=w[2];
                    if(d<w[3])return ItemId.ToolSteelWaste;
                    var templates=region==2?new[]{ItemId.OldController,ItemId.OldDryer,ItemId.OldPetFormer,ItemId.OldDriveBox,ItemId.OldWireMaker,ItemId.DamagedMotor,ItemId.OldDrive}:
                        new[]{ItemId.OldSurvey,ItemId.OldDrill,ItemId.OldSeparator,ItemId.OldKiln,ItemId.OldBoiler,ItemId.OldTurbine,ItemId.OldCondenser,ItemId.OldPaperMachine,ItemId.OldGlassMold,ItemId.OldWaterFilter,ItemId.DamagedMotor,ItemId.OldDrive};
                    // The critical return frame takes 15% of the third-region wreck budget.
                    if(region==3&&random.NextDouble()<0.15)return ItemId.OldStructure;
                    return templates[random.Next(templates.Length)];
                }
            }
        }
        private void GenerateRegionInput(double interval)
        {
            for(int region=2;region<=OwnedRegion;region++)
            {
                var def=Region(region);double multiplier=region==2?0.6:0.4;
                double budget=DesignRules.InputRate(GameSeconds)*interval/60*(0.85+random.NextDouble()*0.3)*multiplier;
                var weights=RegionWeights[region-2];double total=weights.Sum();
                int cx=(def.GarbageX0+def.GarbageX1)/2,cy=(def.GarbageY0+def.GarbageY1)/2;
                for(int c=0;c<RegionCategories.Length;c++)
                {
                    int slot=(region-2)*8+c;regionBudgets[slot]+=budget*weights[c]/total;
                    while(regionBudgets[slot]>=0.1)
                    {
                        var item=regionPending[slot]>=0?(ItemId)regionPending[slot]:DrawRegionItem(region,c);regionPending[slot]=(int)item;
                        double cost=ItemCatalog.CapacityTicks(item)/20.0;
                        if(regionBudgets[slot]+1e-8<cost)break;
                        regionBudgets[slot]-=cost;regionPending[slot]=-1;
                        int before=nextToolId;AddNatural(item,1,cx,cy,false);SpawnedUnits++;
                        if(item==ItemId.PlasticOther)UnrecognizedOtherPlastic++;
                        if(item==ItemId.DamagedMotor||item==ItemId.OldDrive)foreach(var t in tools.Where(t=>t.Id>=before&&t.Item==item)){double d=random.NextDouble();t.Condition=d<0.5?0:d<0.75?1:2;}
                    }
                }
            }
        }

        // ---- warehouses ----------------------------------------------------------------------
        private int WarehouseCapacityWithout(int ignoreId)=>Settings.warehouseCapacity+facilities.Where(f=>f.Built&&f.Id!=ignoreId&&FacilityKey(f.Kind)=="warehouse").Sum(f=>(int)Definition(f.Kind).Capacity);
        private void UpdateWarehouseCapacity()
        {
            int capacity=WarehouseCapacityWithout(0);
            if(Warehouse.Capacity!=capacity&&capacity*20>=Warehouse.UsedTicks)Warehouse.SetCapacity(capacity);
        }
        // Every warehouse building shares one stock; workers use whichever access point is closest.
        private (int x,int y) NearestWarehouseAccess(int x,int y)
        {
            var best=(WarehouseX,WarehouseY);double distance=Distance(x,y,WarehouseX,WarehouseY);
            foreach(var f in facilities.Where(f=>f.Built&&FacilityKey(f.Kind)=="warehouse"))
            {var side=ServiceCell(f);double d=Distance(x,y,side.x,side.y);if(d<distance){distance=d;best=(side.x,side.y);}}
            return best;
        }
        public bool NearAnyWarehouse=>Near(WarehouseX,WarehouseY)||facilities.Any(f=>f.Built&&FacilityKey(f.Kind)=="warehouse"&&NearService(f));
    }
}
