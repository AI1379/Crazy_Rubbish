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
        public static (decimal buy,decimal sale,decimal minimum) Price(ItemId item) => DesignBaseline.Price(item);
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
