using System.Linq;

namespace CrazyRubbish.Core
{
    public static class PrototypeKnowledge
    {
        public static string Purpose(ItemId item)
        {
            switch(item)
            {
                case ItemId.WoodHandSaw:return "手工锯木的工具。带在身上到木工台作业；招到居民后，要交进木工台的工具槽。每次加工都会消耗耐久。";
                case ItemId.WasteWood:return "可以再利用的废木料。和紧固件搭建木工台，也能加工成木板、木柄；发电机建好后还能用作燃料。";
                case ItemId.RecoveredFastener:return "从废品中回收的连接件。与废木料一起搭建木工台或拆解地点，不能当作钢铁零件使用。";
                case ItemId.Food:return "可直接食用的未开封食物。送进旧仓库后供应主角和居民；每人每300秒需要一份食物。";
                case ItemId.DrinkingWater:return "可饮用的水。送进旧仓库后供应生活需要，每人每300秒需要一升。先留足饮水再考虑出售。";
                case ItemId.Plank:return "由废木料锯成的建材。建设工作台需要木板；送入仓库后也可出售，成为工坊的第一笔收入。";
                case ItemId.WoodHandle:return "工具握柄。到工作台与手锯锯片、锤头或螺丝刀杆组合，才能得到完整工具。";
                case ItemId.WoodChips:return "锯木的副产物。可作为发电机燃料，不能替代建造用的木板。";
                case ItemId.SteelPart:return "已经可用的钢铁零件。首次取得后知道如何建发电机；再取得木板，便能建设通用工作台。";
                case ItemId.ThermalModule:return "发电机的热动力组件。与发电组件、钢铁零件、电线一起组装；单独放在地上不会发电。";
                case ItemId.GeneratorModule:return "把动力变成电力的发电组件。先收集齐其他建材、组装发电机，再投入燃料。";
                case ItemId.RecoveredWire:return "可再利用的电线。组装发电机需要它；仅拾取电线并不等于已经供电。";
                case ItemId.Screwdriver:return "拆解旧设备用的工具。主角需带到拆解地点正面；设备先投进现场缓冲，才能开始拆解。";
                case ItemId.SawBlade:return "手锯的工作端。在工作台与一份木柄组合成备用手锯；不能替代机器锯片。";
                case ItemId.MachineBlade:return "旧设备里的机器锯片，面向后续电动木工。它不是制作手锯所需的手锯锯片。";
                case ItemId.Motor:return "拆解得到的回收电机，面向后续电动设备。本试玩可以回收、保存或交易，电动产线尚未开放。";
                case ItemId.PowerAsh:return "发电后留下的灰渣，需要从发电机取走，避免输出堵塞。它不计入附近垃圾的效率惩罚。";
                case ItemId.MixedPlastic:return "混合或受污染的塑料，需要分选、清洗和再加工后才能变成制品。相关产线还未在本试玩开放，暂时不能直接当成木料或建材。";
                case ItemId.Soap:return "清洁用品，规划用于居民清洁与清洗工序。本试玩的清洗产线尚未开放；它不能替代食物或饮用水。";
                case ItemId.WasteTextile:return "废弃布料，规划用于裁剪补丁或清洗成抹布。当前试玩可收集保存，实际纺织工序尚未开放。";
                case ItemId.OldClothes:return "需要清洗和修补的旧衣服，不能直接当成新衣供应。衣物修复是后续工序，当前试玩暂未开放。";
                case ItemId.SortedWood:return "经过整理的木料，面向后续自动木工。当前可以在木工台选择整理配方；它不是建设工作台所需的木板。";
                case ItemId.BrokenWoodTool:return "耐久耗尽的旧工具，不能继续提供工作能力。给岗位补充新的可用工具，再把坏工具搬走。";
                case ItemId.CopperPart:case ItemId.AluminumPart:return "已经成形的金属零件，用于对应设备或后续制造。材质不同，不能替代建设配方中明确要求的钢铁零件。";
                case ItemId.SteelMaterial:case ItemId.CopperMaterial:case ItemId.AluminumMaterial:case ItemId.ToolSteelWaste:return "等待加工的金属原料，不是现成的建造零件。可储存或出售；相应熔炼、零件加工产线尚未在本试玩开放。";
                case ItemId.HeatingElement:return "回收的加热部件，用于后续塑料再加工设备。当前可保存或交易，对应设备尚未开放。";
                case ItemId.Container:return "可重复利用的容器，规划用于清洗和塑料预处理设备。它本身没有装好饮用水，不能作为饮水供应。";
                case ItemId.HandleMould:case ItemId.BoxMould:return "决定塑料制品形状的模具。需要相应加工设备和原料才能使用；当前试玩的塑料再加工尚未开放。";
                case ItemId.FeedingModule:return "自动送料组件，面向后续自动木工。拿到组件不代表已经自动化，还需要建设设备和运输连接。";
                case ItemId.GrowingLight:return "种植照明组件，面向后续太阳灯和种植地。当前种植工序尚未开放，不能把这个零件直接当作食物。";
                case ItemId.FurnaceModule:return "高温电炉的核心组件，用于后续金属原料处理。它需要其他建材和电力；当前试玩未开放这条产线。";
                case ItemId.FurnaceLining:return "炉内的耐热衬材，是后续高温加工设备的建材。不能作为发电机燃料。";
                case ItemId.Fixture:return "加工金属时固定工件的夹具，用于后续零件制造地点。当前可以回收保存，相关地点尚未开放。";
                case ItemId.LightingModule:return "普通照明部件，面向后续路灯和供电建设。本试玩的社区路灯仅作场景展示，完整照明服务尚未接入。";
                case ItemId.HammerHead:return "锤子的工作端。在工作台选择木柄锤，投入一份木柄和一份锤头，再作业组装。";
                case ItemId.ScrewdriverRod:return "螺丝刀的金属工作端。在工作台与木柄组合，得到拆解设备所需的螺丝刀。";
                case ItemId.BroomHead:return "扫帚的工作端，面向后续清洁工具装配。当前工作台尚未开放扫帚配方。";
            }
            if(ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))
                return (ItemCatalog.IsOpenable(item)?"可直接打开的回收物。":"可在拆解地点拆开的旧设备，需要携带相应工具。")+
                    "可用时能回收："+string.Join("、",ItemCatalog.DeviceYield(item).Select(p=>ItemCatalog.Name(p.Item)+" ×"+p.Amount))+"。损坏物品的产出可能不同。";
            if(ItemCatalog.IsTool(item))return "可反复使用的工具，使用会消耗耐久。保留供对应岗位使用；当前引导用到手锯和螺丝刀，其余工具面向后续工序。";
            return "可以拾取、搬运和保存在仓库中的回收物。物品种类决定用途；部分后续加工产线尚未在本试玩开放，可先保留，不必立刻处理全部物资。";
        }
    }
}
