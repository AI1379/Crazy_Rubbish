using System;
using System.Collections.Generic;
using System.Linq;
namespace CrazyRubbish.Core
{
    // Delivery quantities may be fractional (litres, soap cleaning units, sludge); whole stock is drawn into a site credit.
    public readonly struct ItemQuantity
    {
        public readonly ItemId Item;public readonly double Amount;
        public ItemQuantity(ItemId item,double amount){Item=item;Amount=amount;}
        public int Whole=>(int)Math.Ceiling(Amount-1e-9);
    }
    public readonly struct ToolNeed
    {
        public readonly string Family;public readonly int Wear;
        public ToolNeed(string family,int wear){Family=family;Wear=wear;}
    }
    public sealed class FacilityDef
    {
        public readonly int Index,Width,Height,Operators,Maintainers,Ports,BuffersIn,BuffersOut,RegionRequired;
        public readonly string Key,Name,Technology;public readonly ItemQuantity[] Build;
        public readonly double Power,Durability,Capacity,Beds,Radius,OutputPower,HeatOutput;
        public FacilityDef(int index,string key,string name,int width,int height,ItemQuantity[] build,int operators,int maintainers,double power,double durability,int ports,int buffersIn,int buffersOut,string technology,double capacity,double beds,double radius,double outputPower,double heatOutput,int region)
        {
            Index=index;Key=key;Name=name;Width=width;Height=height;Build=build;Operators=operators;Maintainers=maintainers;Power=power;Durability=durability;Ports=ports;
            BuffersIn=buffersIn;BuffersOut=buffersOut;Technology=technology;Capacity=capacity;Beds=beds;Radius=radius;OutputPower=outputPower;HeatOutput=heatOutput;RegionRequired=region;
        }
        public int Kind=>DesignCatalog.GenericKindBase+Index;
        public ItemAmount[] BuildCost=>Build.Select(p=>new ItemAmount(p.Item,p.Whole)).ToArray();
    }
    public sealed class RecipeDef
    {
        public readonly int Index,RegionRequired;public readonly string Key,Name,Facility,RequiresState,Vein;
        public readonly ItemQuantity[] Inputs,Outputs;public readonly double HumanSeconds,MachineSeconds,NaturalSeconds,Power;
        public readonly ToolNeed[] Tools;public readonly string[] Technologies,Flags;
        public readonly ItemQuantity[] Addons,EffluentAddon,Region2Composition;public readonly RecipeStage[] Stages;public readonly double EffluentFraction;public readonly string CancelPackage;
        public RecipeDef(int index,string key,string name,string facility,ItemQuantity[] inputs,ItemQuantity[] outputs,double human,double machine,double natural,double power,ToolNeed[] tools,string[] technologies,int region,string requiresState,string vein,string[] flags,
            ItemQuantity[] addons,RecipeStage[] stages,ItemQuantity[] effluentAddon,double effluentFraction,string cancelPackage,ItemQuantity[] region2Composition)
        {
            Index=index;Key=key;Name=name;Facility=facility;Inputs=inputs;Outputs=outputs;HumanSeconds=human;MachineSeconds=machine;NaturalSeconds=natural;Power=power;
            Tools=tools;Technologies=technologies;RegionRequired=region;RequiresState=requiresState;Vein=vein;Flags=flags;
            Addons=addons;EffluentAddon=effluentAddon;EffluentFraction=effluentFraction;CancelPackage=cancelPackage;Region2Composition=region2Composition;
            // Recipes without explicit stages run their human, machine and natural time in that order.
            var list=new List<RecipeStage>();if(human>0)list.Add(new RecipeStage("human",human));if(machine>0)list.Add(new RecipeStage("machine",machine));if(natural>0)list.Add(new RecipeStage("natural",natural));
            Stages=stages??list.ToArray();
        }
        public bool Has(string flag)=>Array.IndexOf(Flags,flag)>=0;
        public int OutputTicks=>Outputs.Sum(p=>p.Whole*ItemCatalog.CapacityTicks(p.Item));
    }
    public readonly struct RecipeStage
    {
        public readonly string Kind;public readonly double Seconds;
        public RecipeStage(string kind,double seconds){Kind=kind;Seconds=seconds;}
        public bool Human=>Kind=="human";public bool Lit=>Kind=="lit_growth";public bool Natural=>Kind=="natural"||Kind=="lit_growth";
    }
    public sealed class TechnologyDef
    {
        public readonly int Index,RegionRequired;public readonly string Key,Name,Notes;public readonly ItemId[] AcquiredAll,AcquiredAny;public readonly string[] Parents,Facilities;
        public TechnologyDef(int index,string key,string name,ItemId[] all,ItemId[] any,string[] parents,string[] facilities,int region,string notes)
        {Index=index;Key=key;Name=name;AcquiredAll=all;AcquiredAny=any;Parents=parents;Facilities=facilities;RegionRequired=region;Notes=notes;}
    }
    public sealed class TechnologySaleDef
    {
        public readonly int Index;public readonly string Key,Name;public readonly decimal Cash;public readonly ItemId[] Items;public readonly string[] RequiresTechnologies;
        public TechnologySaleDef(int index,string key,string name,decimal cash,ItemId[] items,string[] requires){Index=index;Key=key;Name=name;Cash=cash;Items=items;RequiresTechnologies=requires;}
    }
    public sealed class VeinDef
    {
        public readonly string Key;public readonly int X0,Y0,X1,Y1;
        public VeinDef(string key,int x0,int y0,int x1,int y1){Key=key;X0=x0;Y0=y0;X1=x1;Y1=y1;}
        public bool Contains(int x,int y)=>x>=X0&&x<=X1&&y>=Y0&&y<=Y1;
    }
    public sealed class RegionDef
    {
        public readonly int Number,LandX0,LandY0,LandX1,LandY1,GarbageX0,GarbageY0,GarbageX1,GarbageY1;public readonly string Name;public readonly decimal Price;
        public readonly ItemQuantity[] FixedItems;public readonly VeinDef[] Veins;
        public RegionDef(int number,string name,decimal price,int lx0,int ly0,int lx1,int ly1,int gx0,int gy0,int gx1,int gy1,ItemQuantity[] fixedItems,VeinDef[] veins)
        {Number=number;Name=name;Price=price;LandX0=lx0;LandY0=ly0;LandX1=lx1;LandY1=ly1;GarbageX0=gx0;GarbageY0=gy0;GarbageX1=gx1;GarbageY1=gy1;FixedItems=fixedItems;Veins=veins;}
        public bool InLand(int x,int y)=>x>=LandX0&&x<=LandX1&&y>=LandY0&&y<=LandY1;
        public bool InGarbage(int x,int y)=>x>=GarbageX0&&x<=GarbageX1&&y>=GarbageY0&&y<=GarbageY1;
    }
    public static partial class DesignCatalog
    {
        // Facilities with dedicated legacy behaviour keep their original kind codes; every other v1 facility uses GenericKindBase+index.
        public const int GenericKindBase=1000;
        private static Dictionary<string,FacilityDef> facilityByKey;
        private static Dictionary<string,RecipeDef> recipeByKey;
        private static Dictionary<string,TechnologyDef> technologyByKey;
        private static Dictionary<string,ItemId> itemByKey;
        public static FacilityDef Facility(string key){if(facilityByKey==null)facilityByKey=Facilities.ToDictionary(f=>f.Key);return facilityByKey.TryGetValue(key,out var f)?f:null;}
        public static RecipeDef Recipe(string key){if(recipeByKey==null)recipeByKey=Recipes.ToDictionary(r=>r.Key);return key!=null&&recipeByKey.TryGetValue(key,out var r)?r:null;}
        public static TechnologyDef Technology(string key){if(technologyByKey==null)technologyByKey=Technologies.ToDictionary(t=>t.Key);return technologyByKey.TryGetValue(key,out var t)?t:null;}
        public static ItemId? Item(string key){if(itemByKey==null){itemByKey=new Dictionary<string,ItemId>();for(int i=0;i<ItemKeys.Length;i++)itemByKey[ItemKeys[i]]=(ItemId)i;}return itemByKey.TryGetValue(key,out var item)?item:(ItemId?)null;}
        public static FacilityDef GenericFacility(int kind)=>kind>=GenericKindBase&&kind<GenericKindBase+Facilities.Length?Facilities[kind-GenericKindBase]:null;
        public static IEnumerable<RecipeDef> RecipesAt(string facility)=>Recipes.Where(r=>r.Facility==facility);
        public static int ItemCount=>ItemKeys.Length;
    }
}
