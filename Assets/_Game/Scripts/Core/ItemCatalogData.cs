using System;
namespace CrazyRubbish.Core
{
    public static partial class ItemCatalog
    {
        private static readonly string[] Names = {
            "废木料","混杂塑料","紧固件","木柄手锯","木板","木柄","木料碎屑","钢铁零件","铜零件","回收电线","热动力组件","发电组件",
            "螺丝刀","扳手","锤子","剪刀","针线工具","食物","饮用水","肥皂","废纺织物","旧衣服","损坏电机","钢铁材料","铜材料","发电灰渣",
            "回收电机","机器锯片","加热元件","回收容器","握柄模具","盒模具","送料组件","种植灯组件","高温电炉","炉衬","回收夹具","铝零件","铝材料","工具钢废料",
            "旧切割装置","旧清洗装置","旧成形装置","模具盒","旧容器","完整线束","旧种植灯","旧送料机构","旧高温炉","旧金属台","旧电控箱","照明部件","旧普通灯",
            "手锯锯片","锤头","螺丝刀杆","扫帚头","整理木料","损坏工具"
        };
        public static string Name(ItemId item) => Names[(int)item];
        public static bool IsDevice(ItemId item) => item==ItemId.DamagedMotor || item>=ItemId.OldCutter && item<=ItemId.OldControlBox || item==ItemId.OldLamp;
        public static bool IsOpenable(ItemId item) => item==ItemId.MouldBox || item==ItemId.WireBundle || item==ItemId.OldContainer;
        public static bool NeedsWrench(ItemId item) => item==ItemId.OldWasher || item==ItemId.OldFurnace || item==ItemId.OldMetalTable || item==ItemId.OldControlBox;
        public static ItemAmount[] DeviceYield(ItemId item,bool usable=true)
        {
            if(!usable)
            {
                if(item==ItemId.OldContainer)return new[]{new ItemAmount(ItemId.MixedPlastic,2)};
                if(item==ItemId.MouldBox)return new[]{new ItemAmount(ItemId.SteelMaterial,2)};
                if(item==ItemId.WireBundle)return new[]{new ItemAmount(ItemId.CopperMaterial,2),new ItemAmount(ItemId.MixedPlastic,1)};
                return new[]{new ItemAmount(ItemId.SteelMaterial,3),new ItemAmount(ItemId.CopperMaterial,1)};
            }
            if(item==ItemId.DamagedMotor) return new[]{new ItemAmount(ItemId.SteelMaterial,3),new ItemAmount(ItemId.CopperMaterial,1)};
            switch(item)
            {
                case ItemId.OldCutter: return new[]{new ItemAmount(ItemId.Motor,1),new ItemAmount(ItemId.MachineBlade,1),new ItemAmount(ItemId.SteelPart,2)};
                case ItemId.OldWasher: return new[]{new ItemAmount(ItemId.Motor,1),new ItemAmount(ItemId.Container,1),new ItemAmount(ItemId.SteelPart,2)};
                case ItemId.OldFormer: return new[]{new ItemAmount(ItemId.HeatingElement,1),new ItemAmount(ItemId.SteelPart,2)};
                case ItemId.MouldBox: return new[]{new ItemAmount(ItemId.HandleMould,1),new ItemAmount(ItemId.BoxMould,1)};
                case ItemId.OldContainer: return new[]{new ItemAmount(ItemId.Container,1)};
                case ItemId.WireBundle: return new[]{new ItemAmount(ItemId.RecoveredWire,12)};
                case ItemId.OldGrowingLamp: return new[]{new ItemAmount(ItemId.GrowingLight,1),new ItemAmount(ItemId.SteelPart,1)};
                case ItemId.OldFeeder: return new[]{new ItemAmount(ItemId.FeedingModule,1),new ItemAmount(ItemId.SteelPart,2)};
                case ItemId.OldFurnace: return new[]{new ItemAmount(ItemId.FurnaceModule,1),new ItemAmount(ItemId.FurnaceLining,1),new ItemAmount(ItemId.SteelMaterial,2)};
                case ItemId.OldMetalTable: return new[]{new ItemAmount(ItemId.Fixture,1),new ItemAmount(ItemId.AluminumPart,2),new ItemAmount(ItemId.SteelMaterial,2)};
                case ItemId.OldControlBox: return new[]{new ItemAmount(ItemId.CopperPart,2),new ItemAmount(ItemId.RecoveredWire,2),new ItemAmount(ItemId.SteelMaterial,2)};
                case ItemId.OldLamp: return new[]{new ItemAmount(ItemId.LightingModule,1),new ItemAmount(ItemId.SteelMaterial,1)};
                default: throw new ArgumentException("该物品不是可拆/打开的模板。");
            }
        }
        // buy / base external-sale / minimum, in money units; absent prices do not imply free goods.
        public static (decimal buy,decimal sale,decimal minimum) Price(ItemId item)
        {
            switch(item)
            {
                case ItemId.Plank:return (4,3,1);case ItemId.WoodHandle:return (3,2,0.7m);
                case ItemId.WasteWood:return (0.8m,0.3m,0.1m);case ItemId.WoodChips:return(0.4m,0.15m,0.05m);case ItemId.SortedWood:return(1,0.4m,0.15m);
                case ItemId.SteelPart:return(5,4,1.2m);case ItemId.CopperPart:return(7,5,1.5m);case ItemId.AluminumPart:return(6,4,1.2m);
                case ItemId.Motor:return(12,5.4m,2.4m);case ItemId.MachineBlade:return(8,3.6m,1.6m);case ItemId.HeatingElement:return(10,4.5m,2);
                case ItemId.Container:return(4,1.8m,0.8m);case ItemId.HandleMould:return(8,3.6m,1.6m);case ItemId.BoxMould:return(6,2.7m,1.2m);
                case ItemId.FeedingModule:return(16,7.2m,3.2m);case ItemId.GrowingLight:return(8,3.6m,1.6m);case ItemId.LightingModule:return(6,2.7m,1.2m);
                case ItemId.FurnaceModule:return(28,12.6m,5.6m);case ItemId.FurnaceLining:case ItemId.Fixture:return(8,3.6m,1.6m);
                case ItemId.ThermalModule:return(18,8.1m,3.6m);case ItemId.GeneratorModule:return(24,10.8m,4.8m);
                case ItemId.RecoveredWire:return(2,0.9m,0.4m);case ItemId.RecoveredFastener:return(1,0.45m,0.2m);
                case ItemId.HammerHead:return(5,2.25m,1);case ItemId.SawBlade:return(6,2.7m,1.2m);case ItemId.ScrewdriverRod:return(4,1.8m,0.8m);case ItemId.BroomHead:return(3,1.35m,0.6m);
                case ItemId.SteelMaterial:case ItemId.AluminumMaterial:return(1,0.6m,0.2m);case ItemId.CopperMaterial:return(1.5m,0.8m,0.25m);case ItemId.ToolSteelWaste:return(2,1,0.3m);
                case ItemId.WoodHandSaw:return(16,10,3.2m);case ItemId.Hammer:return(12,7,2.4m);case ItemId.Screwdriver:return(10,6,2);
                case ItemId.Wrench:return(12,0,0);case ItemId.Scissors:return(8,0,0);case ItemId.SewingKit:return(6,0,0);
                case ItemId.Food:return(1.5m,1,0.3m);case ItemId.DrinkingWater:return(0.5m,0.25m,0.1m);case ItemId.Soap:return(4,2,0.6m);
                default:return(0,0,0);
            }
        }
    }
    public sealed class ToolInstance
    {
        public int Id {get;internal set;} public ItemId Item {get;internal set;}
        public int Durability {get;internal set;}=300;
        public string Location {get;internal set;}
        public int ReservedBy {get;internal set;}
        public int UsingAt {get;internal set;}
        public bool Usable {get;internal set;}=true;
    }
}
