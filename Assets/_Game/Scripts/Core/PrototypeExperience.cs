using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public readonly struct SaleQuote
    {
        public readonly int Amount;
        public readonly long IncomeCents;
        public decimal Income => IncomeCents / 100m;
        public SaleQuote(int amount, long incomeCents) { Amount = amount; IncomeCents = incomeCents; }
    }

    public sealed partial class PrototypeSimulation
    {
        public SaleQuote QuoteSale(ItemId item, int requested)
        {
            if (ItemCatalog.Price(item).sale <= 0) return new SaleQuote(0, 0);
            int amount = Math.Min(Math.Max(0, requested), Warehouse.Available(item));
            if (item == ItemId.DrinkingWater) amount = Math.Min(amount, Math.Max(0, Warehouse.Available(item) - DrinkingReserve));
            decimal receipt = saleRemainderCents;
            var price = ItemCatalog.Price(item);
            double pressure = salesPressure[(int)item];
            for (int i = 0; i < amount; i++)
            {
                double unitPrice = MinimumPriceOnly(item) ? (double)price.minimum : Math.Max((double)price.minimum, (double)price.sale * Math.Exp(-pressure / 100));
                receipt += (decimal)unitPrice * 100 * SaleDurabilityFactor(item, i);
                pressure += 1;
            }
            return new SaleQuote(amount, (long)decimal.Floor(receipt));
        }

        public long NextPayrollCents => (long)decimal.Round(residents.Count * (decimal)Settings.wagePerResident * 100);

        public string BuildMaterialSummary(int kind)
        {
            return string.Join("\n", DesignBaseline.BuildCost(kind).Select(p =>
            {
                int available = Warehouse.Available(p.Item) + Carry.Available(p.Item);
                return $"{ItemCatalog.Name(p.Item)}  {available}/{p.Amount}" + (available < p.Amount ? $" · 缺{p.Amount - available}" : " · 已备齐");
            }));
        }

        public bool HasBuildMaterials(int kind) => DesignBaseline.BuildCost(kind).All(p => Warehouse.Available(p.Item) + Carry.Available(p.Item) >= p.Amount);

        public bool ConnectedToGenerator(int facilityId) => powerNetworks.Any(n => n.FacilityIds.Contains(facilityId) && n.FacilityIds.Any(id => GetFacility(id)?.Kind == 8 && GetFacility(id).Built));

        public bool HasMachineConveyorLoop(int facilityId)
        {
            var f=GetFacility(facilityId);
            return f!=null&&IsWoodMachine(f.Kind)
                &&ports.Any(p=>p.OwnerId==-1&&p.Output&&PoweredConveyorRoute(p,MachineFeed(f),facilityId))
                &&ports.Any(p=>p.OwnerId==facilityId&&p.Output&&PoweredConveyorRoute(p,ItemId.Plank,-1))
                &&ports.Any(p=>p.OwnerId==facilityId&&p.Output&&PoweredConveyorRoute(p,ItemId.WoodChips,-1));
        }

        public string ProductionDiagnosis(FacilityInstance f)
        {
            if (f == null || !f.Built) return "尚未建成；在建造页查看缺少的材料。";
            if (Paused) return "游戏已暂停；按空格继续。";
            if (f.Kind == 8)
            {
                double fuel = f.fuelRemaining + f.fuelWood * Settings.woodFuelSeconds + f.fuelChips * Settings.chipsFuelSeconds;
                var network = powerNetworks.FirstOrDefault(n => n.FacilityIds.Contains(f.Id));
                if (network == null || network.FacilityIds.Count <= 1 && network.BeltIds.Count == 0) return "尚未接通用电设备；从发电机外沿铺线到住房、路灯或机器。";
                if (fuel <= 0) return "缺燃料；将木料或碎屑送到正面投料，居民也会补给。";
                if (network.Requested <= 0) return "空载待机，不烧燃料；启动同网机器或接入住房、路灯。";
                return network.Ratio < 0.999 ? $"电力不足（{network.Ratio:P0}）；减少负载或增加同网发电机。" : "已带负载发电；留意燃料与灰渣容量。";
            }
            if (f.Kind != 1 && !IsWoodMachine(f.Kind)) return "原料和工具需送到现场；成品需取出并运回仓库。";
            if (!f.RunRequested) return "生产请求已关闭；点击开始生产。";
            if (RiotActive) return "欠薪暴动使人工岗位停工；先到人员页处理工资。";
            if (IsWoodMachine(f.Kind) && f.MachineTripped) return "耐久停机；安排维护工到蓝色侧，恢复到20后重启。";
            if (f.pendingRecipe.HasValue) return "配方正在等待切换；先运走旧输出，并取回多余旧原料。";
            if (f.State == WorkState.OutputBlocked || f.Output.FreeTicks < (f.Kind == 1 ? BatchTicks(f.SelectedRecipe) : 80)) return "输出空间不足；取走成品，检查仓库容量或输出接口。";
            if (IsWoodMachine(f.Kind) && !ConnectedToGenerator(f.Id)) return "机器尚未接入发电机；铺线连接两者的外沿。";
            if (IsWoodMachine(f.Kind) && f.State == WorkState.NeedsPower) return "机器缺电；检查同网发电机燃料、线路及负载。";
            ItemId feed = IsWoodMachine(f.Kind) ? MachineFeed(f) : ItemId.WasteWood;
            if (!f.jobActive && !f.MachineActive && f.WoodInputStock.Available(feed) < (f.Kind == 1 ? WoodInput(f.SelectedRecipe) : 4))
                return $"现场缺少{ItemCatalog.Name(feed)}；从地面或仓库配送，也可在建材采购页补货。";
            if (f.State == WorkState.NeedsTool) return "缺可用手锯；交接随身工具、制作替补或采购，居民会送往岗位。";
            if (f.State == WorkState.NeedsOperator)
            {
                var worker = residents.FirstOrDefault(r => r.FacilityId == f.Id && (r.Job == WorkerJob.Woodwork || r.Job == WorkerJob.MachineOperator));
                return worker == null ? "尚未安排操作工；到人员页分岗或亲自操作。" : "操作工" + worker.Activity + "；完成当前配送后返回。";
            }
            return IsWoodMachine(f.Kind) ? $"机器运转中；已完成{f.MachineBatches}批，维护与输出需保持畅通。" : "生产与配送进行中；成品入库后可出售。";
        }
    }
}
