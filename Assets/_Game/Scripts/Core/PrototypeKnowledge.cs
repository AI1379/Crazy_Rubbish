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
                case ItemId.GeneratorModule:return "把动力变成电力的发电组件。组装发电机后，还要接线到用电设备并投入燃料；完全空载不发电、不烧燃料。";
                case ItemId.RecoveredWire:return "组装发电机和铺设电网所需的电线。每份可铺4格，从设备外沿接入；要让发电机、用电设备和线路实际连通。";
                case ItemId.Screwdriver:return "拆解旧设备用的工具。主角需带到拆解地点正面；设备先投进现场缓冲，才能开始拆解。";
                case ItemId.SawBlade:return "手锯的工作端。在工作台与一份木柄组合成备用手锯；不能替代机器锯片。";
                case ItemId.MachineBlade:return "与电机一起解锁电动木工，用于建造机器。它不是制作手锯所需的手锯锯片；机器内置刀具随整机维护。";
                case ItemId.Motor:return "与机器锯片一起解锁电动木工；与钢铁零件一起解锁传送带和独立接口。电动木工先人工准备10秒，再进行15秒机器作业。";
                case ItemId.PowerAsh:return "发电后留下的灰渣，需要从发电机取走，避免输出堵塞。它不计入附近垃圾的效率惩罚。";
                case ItemId.MixedPlastic:return "混合或受污染的塑料，需要分选、清洗和再加工后才能变成制品。先在人工分选地点分成PP、HDPE、PET和其他复合料，再经塑料预处理、再加工制成握柄、收纳盒或打包带；它不能直接当成木料或建材。";
                case ItemId.Soap:return "清洁用品，每块含100清洁单位：社区每人每600秒用1单位，清洗地点和塑料清洗也会按配方消耗零头。它不能替代食物或饮用水。";
                case ItemId.WasteTextile:return "废弃布料，在工作台用剪刀裁成补丁，或在清洗地点洗成干净抹布供社区清洁。";
                case ItemId.OldClothes:return "需要清洗和修补的旧衣服，不能直接当成新衣供应。在清洗地点洗净后，于工作台用补丁和针线修成可穿衣服。";
                case ItemId.SortedWood:return "手工整理得到的自动木工原料。自动木工每批用4份整理木料产木板3和碎屑1；它不是建造工作台所需的木板。";
                case ItemId.BrokenWoodTool:return "耐久耗尽的旧工具，不能继续提供工作能力。给岗位补充新的可用工具，再把坏工具搬走。";
                case ItemId.CopperPart:case ItemId.AluminumPart:return "已经成形的金属零件，用于对应设备或后续制造。材质不同，不能替代建设配方中明确要求的钢铁零件。";
                case ItemId.SteelMaterial:case ItemId.CopperMaterial:case ItemId.AluminumMaterial:case ItemId.ToolSteelWaste:return "等待加工的金属原料，不是现成的建造零件。送到金属原料处理地点熔成坯料（需夹钳和供电），再到零件制造地点加工；材质不能混用。";
                case ItemId.HeatingElement:return "回收的加热部件，用于后续塑料再加工设备。取得后可解锁并建设塑料再加工地点。";
                case ItemId.Container:return "可重复利用的容器，首次取得解锁清洗地点；也是工艺废水收集附件的部件。它本身没有装好饮用水，不能作为饮水供应。";
                case ItemId.HandleMould:case ItemId.BoxMould:return "决定塑料制品形状的模具。用于塑料再加工地点制作握柄与收纳盒。";
                case ItemId.FeedingModule:return "在电动木工解锁后，首次取得送料组件可解锁自动木工。自动机用整理木料，不需要操作工，但仍需维护、实际供电和运输。";
                case ItemId.GrowingLight:return "种植照明组件，与种子、基质一起解锁太阳灯种植：太阳灯接电后照亮相邻种植地，芽苗清洗后成为食物。";
                case ItemId.FurnaceModule:return "高温电炉的核心组件，用于后续金属原料处理。用于建设金属原料处理地点，熔炼需要实际供电。";
                case ItemId.FurnaceLining:return "炉内的耐热衬材，是后续高温加工设备的建材。不能作为发电机燃料。";
                case ItemId.Fixture:return "加工金属时固定工件的夹具，用于建设金属零件制造与自动结构零件地点。";
                case ItemId.LightingModule:return "首次取得后解锁自建路灯。社区路灯和自建路灯都要接入发电机电网；可在设备面板检查实际供电情况。";
                case ItemId.HammerHead:return "锤子的工作端。在工作台选择木柄锤，投入一份木柄和一份锤头，再作业组装。";
                case ItemId.ScrewdriverRod:return "螺丝刀的金属工作端。在工作台与木柄组合，得到拆解设备所需的螺丝刀。";
                case ItemId.BroomHead:return "扫帚的工作端，在工作台与木柄组装成扫帚，供社区清洁使用。";
            }
            if(ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))
                return (ItemCatalog.IsOpenable(item)?"可直接打开的回收物。":"可在拆解地点拆开的旧设备，需要携带相应工具。")+
                    "可用时能回收："+string.Join("、",ItemCatalog.DeviceYield(item).Select(p=>ItemCatalog.Name(p.Item)+" ×"+p.Amount))+"。损坏物品的产出可能不同。";
            if(ItemCatalog.IsTool(item))return "可反复使用的工具，使用会消耗耐久。放进对应地点的工具槽，加工时按配方磨损；耐久耗尽变为损坏工具，可在拆解地点回收。";
            var uses=DesignCatalog.Recipes.Where(r=>r.Inputs.Any(q=>q.Item==item)).Select(r=>r.Name).Distinct().Take(4).ToArray();
            var sources=DesignCatalog.Recipes.Where(r=>r.Outputs.Any(q=>q.Item==item)).Select(r=>r.Name).Distinct().Take(3).ToArray();
            return "可以拾取、搬运和保存在仓库中的物品。"+(uses.Length>0?"可用于："+string.Join("、",uses)+"。":"")+(sources.Length>0?"来源："+string.Join("、",sources)+"。":"")+(PrototypeSimulation.DisposalAccepted(item)?"无用时可送去填埋或投弃。":"");
        }
    }
}
