using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class PlanAmount { public string item; public double quantity; public long Ticks => PlanStock.Quantity(quantity); }
    [Serializable] public sealed class PlanItem
    {
        public string id,name,category,market_group,tool_family,quality_tag,provenance,instance_state;
        public double capacity,buy,sell_base,sell_floor,efficiency,disposal_capacity;
        public int durability,uses_per_item; public bool canBuy,canSell,pressure_exempt; public PlanAmount[] payload;
    }
    [Serializable] public sealed class PlanFacility
    {
        public string id,name,technology; public int[] size,interaction_offset;
        public PlanAmount[] build; public int builders,operators,maintainers,ports,beds,region_required,buffers_batches_in,buffers_batches_out;
        public double power,durability,capacity,output_power,fuel_buffer,radius,sell_at_full_durability;
        public bool hasDurability;
    }
    [Serializable] public sealed class PlanStage { public string kind; public double seconds,water; public PlanAmount[] commit_inputs; }
    [Serializable] public sealed class PlanRecipe
    {
        public string id,name,facility,consumption,cancel_package,quality_required,requires_state; public string[] technologies;
        public PlanAmount[] inputs,outputs,tools,addons,composition_region2,capture_cost; public PlanStage[] stages;
        public double human_seconds,machine_seconds,natural_seconds,power,source_liters,effluent_fraction;
        public bool sourceAtStart,actual_composition,requires_grow_lamp,preserve_payload,requires_unrecognized_composition; public int region_required;
    }
    [Serializable] public sealed class PlanTechnology { public string id,name,tier;public string[] acquired_all,acquired_any,parents,facilities,recipes;public int region_required; }
    [Serializable] public sealed class PlanSale { public string id,name;public double cash;public string[] items,market_groups,requires_technologies; }
    [Serializable] public sealed class PlanSpring { public string id,quality;public int[] source,manual_anchor,auto_anchor;public double recharge_liters_per_minute,maximum_liters,initial_liters; }
    [Serializable] public sealed class PlanVein {public int[] rectangle,anchor;}
    [Serializable] public sealed class PlanVeins {public PlanVein iron,copper,clay;}
    [Serializable] public sealed class PlanRegion
    {
        public int id,prerequisite_region,free_hire_maximum; public string name; public int[] land_rectangle,garbage_rectangle;
        public double purchase_cash,hire_fee_later; public PlanAmount[] fixed_items,category_weights,metal_subweights,textile_subweights,plastic_parts_per_10;
        public string[] wreck_templates; public PlanSpring freshwater_source;
        public PlanVeins veins;public int[] launch_platform_anchor;public double critical_structure_wreck_share_of_wreck_budget;
    }
    [Serializable] public sealed class PlanRule { public string key,text;public double number; }
    [Serializable] public sealed class PlanConfig
    {
        public string version; public PlanItem[] items;public PlanFacility[] facilities;public PlanRecipe[] recipes;public PlanTechnology[] technologies;
        public PlanSale[] sales;public PlanRegion[] regions;public PlanRule[] rules;
        private Dictionary<string,PlanItem> itemIndex; private Dictionary<string,PlanFacility> facilityIndex; private Dictionary<string,PlanRecipe> recipeIndex;
        private Dictionary<string,double> numbers;
        public void Validate()
        {
            itemIndex=items.ToDictionary(i=>i.id); facilityIndex=facilities.ToDictionary(i=>i.id);recipeIndex=recipes.ToDictionary(i=>i.id);numbers=rules.ToDictionary(r=>r.key,r=>r.number);
            foreach(var r in recipes)
            {
                if(!facilityIndex.ContainsKey(r.facility))throw new ArgumentException("配方设施不存在："+r.id);
                foreach(var a in r.inputs.Concat(r.outputs))if(!itemIndex.ContainsKey(a.item)||a.Ticks<0)throw new ArgumentException("配方物品无效："+r.id);
                if(r.source_liters>0&&r.facility!="water_intake"&&r.facility!="auto_water_plant")throw new ArgumentException("采水源绑定无效。");
            }
        }
        public PlanItem Item(string id)=>itemIndex[id]; public PlanFacility Facility(string id)=>facilityIndex[id]; public PlanRecipe Recipe(string id)=>recipeIndex[id];
        public double Rule(string key)=>numbers.TryGetValue(key,out double n)?n:throw new ArgumentException("策划参数未登记："+key);
        public PlanRegion Region(int id)=>regions.First(r=>r.id==id);
    }
    // 1/10000 units preserves soap/filter units and fractional residues without float balance comparisons.
    // A package cannot contain another package. A flat payload avoids Unity's recursive serializer depth limit.
    [Serializable] public sealed class PlanPackedStack
    {
        public string item,quality;public long ticks;public int origin,instance,resumeStage;public double durability,resumeProgress;public PlanAmount[] composition;
        public static PlanPackedStack FromStack(PlanStack s)=>new PlanPackedStack{item=s.item,quality=s.quality,ticks=s.ticks,origin=s.origin,instance=s.instance,resumeStage=s.resumeStage,durability=s.durability,resumeProgress=s.resumeProgress,composition=s.composition};
        public PlanStack ToStack()=>new PlanStack{item=item,quality=quality,ticks=ticks,origin=origin,instance=instance,resumeStage=resumeStage,durability=durability,resumeProgress=resumeProgress,composition=composition};
    }
    [Serializable] public sealed class PlanStack { public string item;public long ticks,reserved; public int origin=1,instance,resumeStage; public double durability,resumeProgress;public string quality;public PlanAmount[] composition;public PlanPackedStack[] payload; }
    [Serializable] public sealed class PlanStock
    {
        public const long Scale=10000;public double capacity=double.MaxValue;public List<PlanStack> stacks=new List<PlanStack>();
        public static long Quantity(double value)=>(long)Math.Round(value*Scale,MidpointRounding.AwayFromZero);
        public long Count(string id)=>stacks.Where(s=>s.item==id).Sum(s=>s.ticks-s.reserved);
        public long Total(string id)=>stacks.Where(s=>s.item==id).Sum(s=>s.ticks);
        public void Reserve(string id,long ticks,int instance=-1)
        {
            if(stacks.Where(s=>s.item==id&&(instance<0||s.instance==instance)).Sum(s=>s.ticks-s.reserved)<ticks)throw new InvalidOperationException("无法预约缺少的库存。");
            foreach(var s in stacks.Where(s=>s.item==id&&(instance<0||s.instance==instance))){long n=Math.Min(ticks,s.ticks-s.reserved);s.reserved+=n;ticks-=n;if(ticks==0)break;}
        }
        public void Release(string id,long ticks,int instance=-1){foreach(var s in stacks.Where(s=>s.item==id&&(instance<0||s.instance==instance))){long n=Math.Min(ticks,s.reserved);s.reserved-=n;ticks-=n;if(ticks==0)break;}}
        public double Used(PlanConfig c)=>stacks.Sum(s=>c.Item(s.item).capacity*(c.Item(s.item).uses_per_item>0?Math.Ceiling(s.ticks/(double)Scale):s.ticks/(double)Scale));
        public static double CombinedUsed(PlanConfig c,params PlanStock[] stocks)
        {
            var all=stocks.SelectMany(s=>s.stacks).ToArray();double regular=all.Where(s=>c.Item(s.item).uses_per_item==0).Sum(s=>c.Item(s.item).capacity*s.ticks/Scale);
            return regular+all.Where(s=>c.Item(s.item).uses_per_item>0).GroupBy(s=>s.item+":"+s.instance).Sum(g=>c.Item(g.First().item).capacity*Math.Ceiling(g.Sum(s=>s.ticks)/(double)Scale));
        }
        public bool Fits(PlanConfig c,string id,long ticks)=>ticks>=0&&Used(c)+c.Item(id).capacity*(c.Item(id).uses_per_item>0?Math.Ceiling(ticks/(double)Scale):ticks/(double)Scale)<=capacity+1e-8;
        public void Add(string id,long ticks,int origin=1,double durability=0,int instance=0,PlanAmount[] composition=null,string quality=null)
        {
            if(ticks<=0)return;
            var stack=stacks.FirstOrDefault(s=>s.item==id&&s.origin==origin&&s.instance==instance&&(instance>0||s.composition==composition)&&s.quality==quality);
            if(stack==null){stack=new PlanStack{item=id,origin=origin,durability=durability,instance=instance,composition=composition,quality=quality};stacks.Add(stack);}stack.ticks+=ticks;
        }
        public List<PlanStack> Take(string id,long ticks,bool reserved=false,int instance=-1)
        {
            if(ticks<0||stacks.Where(s=>s.item==id&&(instance<0||s.instance==instance)).Sum(s=>reserved?s.reserved:s.ticks-s.reserved)<ticks)throw new InvalidOperationException("库存不足："+id);
            var result=new List<PlanStack>();
            foreach(var s in stacks.Where(s=>s.item==id&&(instance<0||s.instance==instance)).ToArray())
            {
                long n=Math.Min(ticks,reserved?s.reserved:s.ticks-s.reserved);if(n==0)continue;
                result.Add(new PlanStack{item=id,ticks=n,origin=s.origin,instance=s.instance,durability=s.durability,quality=s.quality,composition=s.composition,resumeStage=s.resumeStage,resumeProgress=s.resumeProgress,payload=s.payload});
                s.ticks-=n;if(reserved)s.reserved-=n;ticks-=n;if(s.ticks==0)stacks.Remove(s);if(ticks==0)break;
            }
            return result;
        }
        public void Put(IEnumerable<PlanStack> values){foreach(var s in values){Add(s.item,s.ticks,s.origin,s.durability,s.instance,s.composition,s.quality);var target=stacks.Last(t=>t.item==s.item&&t.instance==s.instance&&t.origin==s.origin);target.resumeStage=s.resumeStage;target.resumeProgress=s.resumeProgress;target.payload=s.payload;}}
    }
}
