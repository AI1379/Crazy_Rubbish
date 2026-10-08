using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed class PrototypeSimulation
    {
        public const int BenchX = 13, BenchY = 6, WarehouseX = 15, WarehouseY = 22;
        public const int DropX = 4, DropY = 8, HousingX = 21, HousingY = 22;
        public const int TableX = 13, TableY = 14, DismantleX = 13, DismantleY = 10;
        public const int GeneratorX = 18, GeneratorY = 6;
        private const double Step = 0.05;
        private readonly List<GroundPile> ground = new List<GroundPile>();
        private readonly Dictionary<(int, int), int> groundCapacity = new Dictionary<(int, int), int>();
        private readonly List<Resident> residents = new List<Resident>();
        private readonly List<WageBill> bills = new List<WageBill>();
        private readonly Random random;
        private readonly double[] salesPressure = new double[Enum.GetValues(typeof(ItemId)).Length];
        private double accumulatedTime, inputTimer, nextInput, jobProgress, dismantleProgress, nextWage;
        private decimal saleRemainderCents;
        private bool jobActive, dismantleActive;
        private WoodRecipe activeRecipe;
        private int pendingBuildings;
        private long cashCents;
        private int fuelWood, fuelChips;
        private double fuelRemaining, equivalentHeat;
        private double activeFuelHeatRate;
        private readonly Dictionary<(int, int), (int revision, float value)> pressureCache = new Dictionary<(int, int), (int, float)>();

        public PrototypeSettings Settings { get; }
        public UnitInventory Carry { get; }
        public UnitInventory Warehouse { get; }
        public UnitInventory Output { get; }
        public UnitInventory DismantleOutput { get; } = new UnitInventory(8);
        public UnitInventory GeneratorResidue { get; } = new UnitInventory(10);
        public IReadOnlyList<GroundPile> Ground => ground;
        public IReadOnlyList<Resident> Residents => residents;
        public IReadOnlyList<WageBill> WageBills => bills;
        public double PlayerX { get; private set; } = 4;
        public double PlayerY { get; private set; } = 4;
        public double GameSeconds { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool BenchBuilt { get; private set; }
        public bool BuildPending => (pendingBuildings & 1) != 0;
        public bool TableBuilt { get; private set; }
        public bool DismantleBuilt { get; private set; }
        public bool GeneratorBuilt { get; private set; }
        public bool HasBeenPowered { get; private set; }
        public bool RunRequested { get; private set; }
        public bool DismantleRequested { get; private set; }
        public WoodRecipe SelectedRecipe { get; private set; }
        public WorkState State { get; private set; } = WorkState.NotBuilt;
        public WorkState DismantleState { get; private set; } = WorkState.NotBuilt;
        public long CashCents => cashCents;
        public decimal Money => cashCents / 100m;
        public long TotalSold { get; private set; }
        public long TotalWagesPaidCents { get; private set; }
        public int CompletedBatches { get; private set; }
        public int DismantledBatches { get; private set; }
        public int GroundRevision { get; private set; }
        public long IncomingUnits => 0; // Natural arrivals are always represented on the ground.
        public long SpawnedUnits { get; private set; }
        public bool JobActive => jobActive;
        public int ReservedWood => jobActive ? WoodInput(activeRecipe) : 0;
        public float Progress => jobActive ? (float)Math.Min(1, jobProgress / Duration(activeRecipe)) : 0;
        public float DismantleProgress => (float)(dismantleProgress / 35);
        public bool NearWarehouse => Near(WarehouseX, WarehouseY, 2.2);
        public bool NearBench => Near(BenchX, BenchY);
        public bool NearGenerator => Near(GeneratorX, GeneratorY);
        public bool HasSaw => HasTool(ItemId.WoodHandSaw);
        public int FuelCount => fuelWood + fuelChips;
        public double FuelSeconds => fuelRemaining + fuelWood * Settings.woodFuelSeconds + fuelChips * Settings.chipsFuelSeconds;
        public float AvailablePower => GeneratorBuilt && fuelRemaining > 0 && !(equivalentHeat >= 20 - 0.000001 && GeneratorResidue.FreeTicks < 20) ? Settings.generatorOutput : 0;
        public double SecondsToWage => residents.Count == 0 ? 0 : Math.Max(0, nextWage - GameSeconds);
        public long UnpaidWageCents => bills.Sum(b => b.RemainingCents);
        public decimal UnpaidWages => UnpaidWageCents / 100m;
        public double EquivalentUnpaidPeriods => bills.Sum(b => (double)b.RemainingCents / b.OriginalCents);
        public Resident Woodworker => residents.Find(r => r.Job == WorkerJob.Woodwork);
        public bool WorkerAtBench => Woodworker != null && Distance(Woodworker.X, Woodworker.Y, BenchX, BenchY) <= 0.1;
        public bool AnyBuildPending => pendingBuildings != 0;

        public PrototypeSimulation(PrototypeSettings settings, int seed = 4271)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            random = new Random(seed);
            Carry = new UnitInventory(settings.carryCapacity);
            Warehouse = new UnitInventory(settings.warehouseCapacity);
            Output = new UnitInventory(settings.outputCapacity);
            nextInput = RandomInputInterval();
            AddInitial(4, 5, ItemId.WoodHandSaw, 1);
            AddInitial(5, 5, ItemId.RecoveredFastener, settings.initialFasteners);
            AddInitial(DropX, DropY, ItemId.WasteWood, settings.initialWood);
            AddInitial(DropX, DropY, ItemId.SteelPart, 30);
            AddInitial(DropX, DropY, ItemId.CopperPart, 2);
            AddInitial(DropX, DropY, ItemId.RecoveredWire, 8);
            AddInitial(DropX, DropY, ItemId.ThermalModule, 1);
            AddInitial(DropX, DropY, ItemId.GeneratorModule, 1);
            foreach (ItemId tool in new[] { ItemId.Screwdriver, ItemId.Wrench, ItemId.Hammer, ItemId.Scissors, ItemId.SewingKit })
                AddInitial(DropX, DropY, tool, 1);
            AddInitial(DropX, DropY, ItemId.Food, 30);
            AddInitial(DropX, DropY, ItemId.DrinkingWater, 40);
            AddInitial(DropX, DropY, ItemId.Soap, 3);
            AddInitial(DropX, DropY, ItemId.WasteTextile, 12);
            AddInitial(DropX, DropY, ItemId.OldClothes, 6);
            // A labelled damaged motor is the documented steel3/copper1 example, never a universal parts loot roll.
            AddInitial(DropX, DropY, ItemId.DamagedMotor, 1);
        }

        public static double Distance(double ax, double ay, double bx, double by)
        { double dx = ax - bx, dy = ay - by; return Math.Sqrt(dx * dx + dy * dy); }
        private bool Near(int x, int y, double radius = -1) => Distance(PlayerX, PlayerY, x, y) <= (radius < 0 ? Settings.interactionRadius : radius);
        private bool HasTool(ItemId tool) => Carry.Count(tool) + Warehouse.Count(tool) > 0;
        public static bool IsOwnedGround(int x, int y) =>
            (x >= 10 && x < 28 && y >= 3 && y < 21) || (x >= 1 && x <= 8 && y >= 5 && y <= 10);
        private static bool InBuilding(int x, int y, int bx, int by, int width = 2, int height = 2) =>
            x >= bx && x < bx + width && y >= by && y < by + height;
        public static bool IsReservedCell(int x, int y) =>
            InBuilding(x, y, BenchX, BenchY) || InBuilding(x, y, TableX, TableY) ||
            InBuilding(x, y, DismantleX, DismantleY) || InBuilding(x, y, GeneratorX, GeneratorY) ||
            InBuilding(x, y, WarehouseX - 1, WarehouseY - 1, 3, 3) ||
            InBuilding(x, y, HousingX - 1, HousingY - 1, 3, 3);

        public void SetPaused(bool paused) { Paused = paused; RefreshState(); }
        public void SetSpeed(int speed)
        {
            if (speed != 1 && speed != 2 && speed != 4) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }
        public void MovePlayer(double horizontal, double vertical, double realSeconds)
        {
            if (Paused || realSeconds <= 0) return;
            double length = Math.Sqrt(horizontal * horizontal + vertical * vertical);
            if (length > 1) { horizontal /= length; vertical /= length; }
            PlayerX = Math.Max(0.5, Math.Min(Settings.mapWidth - 1.5, PlayerX + horizontal * Settings.moveSpeed * realSeconds * Speed));
            PlayerY = Math.Max(0.5, Math.Min(Settings.mapHeight - 1.5, PlayerY + vertical * Settings.moveSpeed * realSeconds * Speed));
        }
        public GroundPile NearestPickup()
        {
            GroundPile nearest = null;
            double best = Settings.interactionRadius + 0.0001;
            foreach (GroundPile pile in ground)
            {
                double distance = Distance(PlayerX, PlayerY, pile.X, pile.Y);
                if (distance < best) { nearest = pile; best = distance; }
            }
            return nearest;
        }
        public ActionResult PickUp(int requested = 1, ItemId? selected = null)
        {
            if (Paused) return Fail("恢复运行后再拾取。");
            GroundPile pile = selected == null ? NearestPickup() : ground.Where(p => p.Item == selected.Value &&
                Distance(PlayerX, PlayerY, p.X, p.Y) <= Settings.interactionRadius).OrderBy(p => Distance(PlayerX, PlayerY, p.X, p.Y)).FirstOrDefault();
            if (pile == null) return Fail("靠近地上的物品后按E拾取。");
            int amount = Math.Min(Math.Min(Math.Max(0, requested), pile.Amount), Carry.AvailableSlots(pile.Item));
            if (amount == 0) return Fail("背包容量不足，先入库。");
            Carry.TryAdd(pile.Item, amount); pile.Amount -= amount;
            groundCapacity[(pile.X, pile.Y)] = CellTicks(pile.X, pile.Y) - amount * ItemCatalog.CapacityTicks(pile.Item);
            if (pile.Amount == 0) ground.Remove(pile);
            GroundRevision++; RefreshState();
            return Ok("已拾取，物品进入背包。", amount);
        }
        public ActionResult Deposit()
        {
            if (Paused || !NearWarehouse) return Fail("恢复运行并靠近仓库后入库。");
            int moved = 0;
            foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
                if (item != ItemId.WoodHandSaw) moved += Carry.TransferTo(Warehouse, item, Carry.Count(item));
            RefreshState();
            return moved > 0 ? Ok("已入库，手锯随身保留。", moved) : Fail("仓库容量不足或背包没有待入库物品。");
        }
        public ActionResult Withdraw(ItemId item, int requested = 1)
        {
            if (Paused || !NearWarehouse) return Fail("恢复运行并靠近仓库后取出。");
            int moved = Warehouse.TransferTo(Carry, item, requested); RefreshState();
            return moved > 0 ? Ok("已取出。", moved) : Fail("库存不足或背包容量不足。");
        }
        public ActionResult Drop(ItemId item)
        {
            if (Paused || Carry.Count(item) == 0) return Fail("恢复运行并确认背包有该物品。");
            if (!FindGroundSpace(item, (int)Math.Round(PlayerX), (int)Math.Round(PlayerY), out GroundPile pile, out int x, out int y, 2.5))
                return Fail("附近没有普通堆放空间，物品保留在背包。");
            Place(item, 1, pile, x, y); Carry.TryRemove(item, 1); RefreshState();
            return Ok("已放下。", 1);
        }

        public ActionResult BuildBench() => Build(1, BenchBuilt, new ItemAmount(ItemId.WasteWood, Settings.benchWoodCost), new ItemAmount(ItemId.RecoveredFastener, Settings.benchFastenerCost));
        public ActionResult BuildDismantle() => Build(2, DismantleBuilt, new ItemAmount(ItemId.WasteWood, 4), new ItemAmount(ItemId.RecoveredFastener, 2));
        public ActionResult BuildTable() => Build(4, TableBuilt, new ItemAmount(ItemId.Plank, 4), new ItemAmount(ItemId.SteelPart, 2));
        public ActionResult BuildGenerator()
        {
            if (!TableBuilt) return Fail("先建通用工作台，再组装发电机。");
            return Build(8, GeneratorBuilt, new ItemAmount(ItemId.SteelPart, 6), new ItemAmount(ItemId.ThermalModule, 1),
                new ItemAmount(ItemId.GeneratorModule, 1), new ItemAmount(ItemId.RecoveredWire, 2));
        }
        private ActionResult Build(int flag, bool built, params ItemAmount[] costs)
        {
            if (built || (pendingBuildings & flag) != 0) return Fail("已经建成或预留蓝图。");
            bool idlePerson = !(State == WorkState.Working && NearBench && !WorkerAtBench) && DismantleState != WorkState.Working;
            if (!idlePerson && !residents.Exists(r => r.Job == WorkerJob.Idle)) return Fail("缺空闲建设人，请先停工。");
            if (!Warehouse.TryConsume(costs)) return Fail("建设材料不足，请将所需材料入库。");
            pendingBuildings |= flag;
            if (!Paused) CompleteBuildings();
            RefreshState(); return Ok(Paused ? "材料已预留，恢复后建成。" : "建设完成。");
        }
        private void CompleteBuildings()
        {
            if ((pendingBuildings & 1) != 0) BenchBuilt = true;
            if ((pendingBuildings & 2) != 0) DismantleBuilt = true;
            if ((pendingBuildings & 4) != 0) TableBuilt = true;
            if ((pendingBuildings & 8) != 0) GeneratorBuilt = true;
            pendingBuildings = 0;
        }
        public ActionResult SelectRecipe(WoodRecipe recipe)
        {
            if (!Enum.IsDefined(typeof(WoodRecipe), recipe)) throw new ArgumentOutOfRangeException(nameof(recipe));
            if (jobActive) return Fail("当前批次完成后再切换。");
            TransferOutputs();
            if (Output.UsedTicks > 0) return Fail("旧配方输出未清空，请先腾仓库空间。");
            Output.SetCapacity(recipe == WoodRecipe.Plank ? Settings.outputCapacity : (int)Math.Ceiling(BatchTicks(recipe) * 2 / 20.0));
            SelectedRecipe = recipe; RefreshState(); return Ok("已切换配方。");
        }
        public ActionResult ToggleWork()
        {
            if (!BenchBuilt) return Fail("先搭建木工台。");
            RunRequested = !RunRequested; RefreshState();
            return Ok(RunRequested ? "连续木工已开启，主角或指定居民到岗后作业。" : "木工已停，保留进度和投入。");
        }
        public ActionResult ToggleDismantle()
        {
            if (!DismantleBuilt) return Fail("先搭建拆解台。");
            DismantleRequested = !DismantleRequested; RefreshState();
            return Ok(DismantleRequested ? "靠近拆解台，用螺丝刀拆解废旧电机。" : "拆解已停，保留进度和投入。");
        }
        public ActionResult AddFuel(ItemId item, int requested = 1)
        {
            if (!GeneratorBuilt || Paused || !NearGenerator) return Fail("恢复运行并靠近已建发电机后补燃料。");
            if (item != ItemId.WasteWood && item != ItemId.WoodChips) return Fail("只接受废木料和碎屑。");
            int moved = Math.Min(Math.Min(Math.Max(0, requested), Carry.Count(item)), 10 - FuelCount);
            if (moved <= 0) return Fail("背包缺燃料或燃料缓冲已满。");
            Carry.TryRemove(item, moved);
            if (item == ItemId.WasteWood) fuelWood += moved; else fuelChips += moved;
            Ignite(); return Ok("燃料已送入，发电机自行运行。", moved);
        }
        private void Ignite()
        {
            if (fuelRemaining > 0 || !GeneratorBuilt) return;
            if (GeneratorResidue.FreeTicks < 20 && equivalentHeat >= 20 - 0.000001) return;
            if (fuelWood > 0) { fuelWood--; fuelRemaining = Settings.woodFuelSeconds; activeFuelHeatRate = 1.0 / Settings.woodFuelSeconds; }
            else if (fuelChips > 0) { fuelChips--; fuelRemaining = Settings.chipsFuelSeconds; activeFuelHeatRate = 0.5 / Settings.chipsFuelSeconds; }
            else return;
            HasBeenPowered = true;
        }
        public ActionResult TakeAsh()
        {
            if (Paused || !NearGenerator) return Fail("恢复运行并靠近发电机后取灰渣。");
            int moved = GeneratorResidue.TransferTo(Carry, ItemId.PowerAsh, int.MaxValue);
            return moved > 0 ? Ok("灰渣进入背包，占容量但不计地面压力。", moved) : Fail("无灰渣或背包已满。");
        }
        public ActionResult Recruit()
        {
            if (!HasBeenPowered) return Fail("先完成发电机并实际补料发电，获得认可。");
            if (residents.Count >= Settings.firstResidents || residents.Count + 1 >= Settings.housingCapacity)
                return Fail("旧住房已达到首批招募容量；新住房将在后续阶段接入。");
            if (residents.Count == 0) nextWage = GameSeconds + Settings.wageSeconds;
            residents.Add(new Resident(residents.Count + 1));
            return Ok("居民已加入，自带5钱；仅免招募费，每600秒工资10钱。");
        }
        public ActionResult AssignWoodworker()
        {
            Resident assigned = Woodworker;
            if (assigned != null) { assigned.Job = WorkerJob.Idle; RefreshState(); return Ok("木工居民已释放。"); }
            Resident idle = residents.Find(r => r.Job == WorkerJob.Idle);
            if (idle == null) return Fail("没有空闲居民。");
            idle.Job = WorkerJob.Woodwork; RefreshState();
            return Ok("已指定居民，步行到木工台后开始作业。");
        }
        public ActionResult PayWages()
        {
            if (UnpaidWageCents == 0) return Fail("尚无到期工资。");
            if (cashCents == 0) return Fail("经营现金不足，先出售产品。");
            long paid = 0;
            foreach (WageBill bill in bills)
            {
                while (cashCents > 0 && bill.RemainingCents > 0)
                {
                    int owed = bill.UnpaidCents.Count(value => value > 0);
                    long share = Math.Max(1, Math.Min(cashCents, bill.RemainingCents) / owed);
                    for (int i = 0; i < bill.UnpaidCents.Length && cashCents > 0; i++)
                    {
                        long transfer = Math.Min(Math.Min(share, bill.UnpaidCents[i]), cashCents);
                        if (transfer == 0) continue;
                        Resident recipient = residents.Find(r => r.Id == bill.ResidentIds[i]);
                        if (recipient == null) throw new InvalidOperationException("工资账单收款居民不存在。");
                        recipient.CashCents += transfer; cashCents -= transfer; bill.UnpaidCents[i] -= transfer; paid += transfer;
                    }
                }
                if (cashCents == 0) break;
            }
            TotalWagesPaidCents += paid; RefreshState();
            return Ok($"实付工资{paid / 100m:0.00}，剩余欠款{UnpaidWages:0.00}。");
        }

        public double SalePrice(ItemId item)
        {
            if (item == ItemId.Plank) return Math.Max(1, Settings.plankPrice * Math.Exp(-salesPressure[(int)item] / 100));
            if (item == ItemId.WoodHandle) return Math.Max(0.7, Settings.handlePrice * Math.Exp(-salesPressure[(int)item] / 100));
            return 0;
        }
        public ActionResult Sell(ItemId item, int requested)
        {
            if (item != ItemId.Plank && item != ItemId.WoodHandle) return Fail("当前只接入木板和木柄外销。");
            int amount = Math.Min(Math.Max(0, requested), Warehouse.Count(item));
            if (amount == 0) return Fail("仓库没有该成品。");
            decimal receipt = saleRemainderCents;
            for (int i = 0; i < amount; i++) { receipt += (decimal)SalePrice(item) * 100; salesPressure[(int)item] += 1; }
            long earned = (long)decimal.Floor(receipt);
            saleRemainderCents = receipt - earned;
            Warehouse.TryRemove(item, amount); cashCents += earned; TotalSold += amount; RefreshState();
            return Ok($"出售{amount}，获得{earned / 100m:0.00}钱。", amount);
        }

        public int NearbyPressureTiles => ground.Where(p => p.Item != ItemId.PowerAsh && Distance(p.X, p.Y, BenchX, BenchY) <= Settings.pressureRadius)
            .Select(p => p.Y * Settings.mapWidth + p.X).Distinct().Count();
        public float PressureAt(double cx, double cy)
        {
            var cacheKey = ((int)Math.Round(cx), (int)Math.Round(cy));
            if (pressureCache.TryGetValue(cacheKey, out var cached) && cached.revision == GroundRevision) return cached.value;
            cx = cacheKey.Item1; cy = cacheKey.Item2;
            int available = 0;
            for (int x = 0; x < Settings.mapWidth; x++)
                for (int y = 0; y < Settings.mapHeight; y++)
                    if (IsOwnedGround(x, y) && !IsReservedCell(x, y) && Distance(x, y, cx, cy) <= Settings.pressureRadius) available++;
            var exposed = ground.Where(p => p.Item != ItemId.PowerAsh && Distance(p.X, p.Y, cx, cy) <= Settings.pressureRadius)
                .GroupBy(p => p.Y * Settings.mapWidth + p.X).ToArray();
            double ratio = available == 0 ? 0 : (double)exposed.Length / available;
            double area = ratio <= 0.3 ? 1 : ratio <= 0.6 ? 1 - (ratio - 0.3) * 0.2 / 0.3 : 0.8 - (ratio - 0.6) * 0.2 / 0.4;
            double excess = exposed.Sum(group => Math.Max(0, group.Sum(p => p.Amount * ItemCatalog.CapacityTicks(p.Item)) / 20.0 - Settings.pileCapacity));
            double reference = exposed.Length * Settings.pileCapacity;
            double overload = reference == 0 ? 1 : Math.Max(0.4, 1 / (1 + excess / reference));
            float result = (float)Math.Max(Settings.minimumEfficiency, area * overload);
            pressureCache[cacheKey] = (GroundRevision, result);
            return result;
        }
        public float HumanEfficiency => (float)Math.Max(Settings.minimumEfficiency,
            PressureAt(BenchX, BenchY) * (WorkerAtBench ? Math.Max(0.6, 1 - EquivalentUnpaidPeriods * 0.1) : 1));
        public double Duration(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? Settings.plankSeconds : Settings.handleSeconds;
        public int ProductAmount(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? Settings.plankOutput : Settings.handleOutput;
        public ItemId Product(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? ItemId.Plank : ItemId.WoodHandle;
        public int WoodInput(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? Settings.recipeWoodInput : Settings.handleWoodInput;
        private int BatchTicks(WoodRecipe recipe) => ProductAmount(recipe) * ItemCatalog.CapacityTicks(Product(recipe)) + Settings.chipsOutput * 20;

        public void Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (Paused) { RefreshState(); return; }
            accumulatedTime += realSeconds * Speed;
            while (accumulatedTime + 0.00000001 >= Step)
            {
                accumulatedTime -= Step; GameSeconds += Step;
                CompleteBuildings();
                double recovery = Math.Exp(-Math.Log(2) * Step / 600);
                for (int i = 0; i < salesPressure.Length; i++) salesPressure[i] *= recovery;
                if (fuelRemaining > 0 && (equivalentHeat < 20 - 0.000001 || GeneratorResidue.FreeTicks >= 20))
                {
                    double burned = Math.Min(Step, fuelRemaining);
                    fuelRemaining = Math.Max(0, fuelRemaining - burned);
                    if (fuelRemaining < 0.00000001) fuelRemaining = 0;
                    equivalentHeat += burned * activeFuelHeatRate;
                }
                if (equivalentHeat >= 20 - 0.000001 && GeneratorResidue.TryAdd(ItemId.PowerAsh, 1)) equivalentHeat = Math.Max(0, equivalentHeat - 20);
                Ignite();
                if (residents.Count > 0 && GameSeconds + 0.000001 >= nextWage)
                {
                    bills.Add(new WageBill(residents.Select(r => r.Id).ToArray(), (long)Math.Round(Settings.wagePerResident * 100), nextWage));
                    nextWage += Settings.wageSeconds;
                }
                foreach (Resident resident in residents)
                {
                    double tx = resident.Job == WorkerJob.Woodwork ? BenchX : HousingX;
                    double ty = resident.Job == WorkerJob.Woodwork ? BenchY : HousingY;
                    double distance = Distance(resident.X, resident.Y, tx, ty);
                    double travel = Math.Min(distance, Settings.residentMoveSpeed * Step * PressureAt(resident.X, resident.Y));
                    if (distance > 0) { resident.X += (tx - resident.X) / distance * travel; resident.Y += (ty - resident.Y) / distance * travel; }
                }
                if (Settings.automaticInput)
                {
                    inputTimer += Step;
                    if (inputTimer >= nextInput)
                    {
                        inputTimer -= nextInput;
                        double rate = GameSeconds < 600 ? 8 : GameSeconds < 1800 ? 12 : 12 * Math.Pow(1.25, 1 + Math.Floor((GameSeconds - 1800) / 1800));
                        int count = Math.Max(1, (int)Math.Round(rate * nextInput / 60 * (0.75 + random.NextDouble() * 0.5)));
                        ItemId item = random.NextDouble() < 0.635 ? ItemId.WasteWood : ItemId.MixedPlastic;
                        // The full five-category item/instance pool is the next input milestone.
                        AddNatural(item, count); SpawnedUnits += count; nextInput = RandomInputInterval();
                    }
                }
                TransferOutputs(); RefreshState();
                if (State == WorkState.Working)
                {
                    if (!jobActive)
                    {
                        Warehouse.TryRemove(ItemId.WasteWood, WoodInput(SelectedRecipe));
                        jobActive = true; activeRecipe = SelectedRecipe; jobProgress = 0;
                    }
                    jobProgress = Math.Min(Duration(activeRecipe), jobProgress + Step * HumanEfficiency);
                    if (jobProgress + 0.00000001 >= Duration(activeRecipe) && Output.TryAddBatch(
                        new ItemAmount(Product(activeRecipe), ProductAmount(activeRecipe)), new ItemAmount(ItemId.WoodChips, Settings.chipsOutput)))
                    { jobActive = false; jobProgress = 0; CompletedBatches++; }
                }
                if (DismantleState == WorkState.Working)
                {
                    if (!dismantleActive) { Warehouse.TryRemove(ItemId.DamagedMotor, 1); dismantleActive = true; dismantleProgress = 0; }
                    dismantleProgress = Math.Min(35, dismantleProgress + Step * PressureAt(DismantleX, DismantleY));
                    if (dismantleProgress + 0.00000001 >= 35 && DismantleOutput.TryAddBatch(new ItemAmount(ItemId.SteelMaterial, 3), new ItemAmount(ItemId.CopperMaterial, 1)))
                    { dismantleActive = false; dismantleProgress = 0; DismantledBatches++; }
                }
                TransferOutputs(); RefreshState();
            }
        }
        private void TransferOutputs()
        {
            foreach (ItemId item in new[] { ItemId.Plank, ItemId.WoodHandle, ItemId.WoodChips })
                Output.TransferTo(Warehouse, item, Output.Count(item));
            foreach (ItemId item in new[] { ItemId.SteelMaterial, ItemId.CopperMaterial })
                DismantleOutput.TransferTo(Warehouse, item, DismantleOutput.Count(item));
        }
        private void RefreshState()
        {
            if (!BenchBuilt) State = WorkState.NotBuilt;
            else if (Paused) State = WorkState.Paused;
            else if (!RunRequested) State = WorkState.Idle;
            else if (Output.FreeTicks < BatchTicks(jobActive ? activeRecipe : SelectedRecipe)) State = WorkState.OutputBlocked;
            else if (!NearBench && !WorkerAtBench) State = WorkState.NeedsOperator;
            else if (!HasSaw) State = WorkState.NeedsTool;
            else if (!jobActive && Warehouse.Count(ItemId.WasteWood) < WoodInput(SelectedRecipe)) State = WorkState.NeedsMaterials;
            else State = WorkState.Working;
            if (!DismantleBuilt) DismantleState = WorkState.NotBuilt;
            else if (Paused) DismantleState = WorkState.Paused;
            else if (!DismantleRequested) DismantleState = WorkState.Idle;
            else if (DismantleOutput.FreeTicks < 80) DismantleState = WorkState.OutputBlocked;
            else if (!Near(DismantleX, DismantleY)) DismantleState = WorkState.NeedsOperator;
            else if (!HasTool(ItemId.Screwdriver)) DismantleState = WorkState.NeedsTool;
            else if (!dismantleActive && Warehouse.Count(ItemId.DamagedMotor) == 0) DismantleState = WorkState.NeedsMaterials;
            else DismantleState = WorkState.Working;
        }

        private double RandomInputInterval() => Settings.firstInputSeconds * (2.0 / 3.0 + random.NextDouble() * 2.0 / 3.0);
        private void AddInitial(int x, int y, ItemId item, int count) => AddNatural(item, count, x, y);
        private void AddNatural(ItemId item, int count, int centerX = DropX, int centerY = DropY)
        {
            while (count > 0)
            {
                if (FindGroundSpace(item, centerX, centerY, out GroundPile pile, out int x, out int y))
                {
                    int free = Math.Max(1, (Settings.pileCapacity * 20 - CellTicks(x, y)) / ItemCatalog.CapacityTicks(item));
                    int placed = Math.Min(count, free);
                    Place(item, placed, pile, x, y); count -= placed;
                }
                else
                {
                    GroundPile overflow = ground.Where(p => p.Item == item)
                        .OrderBy(p => Distance(p.X, p.Y, centerX, centerY)).FirstOrDefault();
                    if (overflow == null)
                    {
                        GroundPile cell = ground.OrderBy(p => Distance(p.X, p.Y, centerX, centerY)).First();
                        Place(item, count, null, cell.X, cell.Y);
                    }
                    else Place(item, count, overflow, overflow.X, overflow.Y);
                    count = 0;
                }
            }
        }
        private int CellTicks(int x, int y) => groundCapacity.TryGetValue((x, y), out int ticks) ? ticks : 0;
        private bool FindGroundSpace(ItemId item, int centerX, int centerY, out GroundPile target,
            out int x, out int y, double maximumDistance = double.MaxValue)
        {
            target = null; x = y = -1;
            double best = maximumDistance;
            for (int px = 0; px < Settings.mapWidth; px++)
                for (int py = 0; py < Settings.mapHeight; py++)
                {
                    if (!IsOwnedGround(px, py) || IsReservedCell(px, py)) continue;
                    double distance = Distance(centerX, centerY, px, py);
                    if (distance > best) continue;
                    int used = CellTicks(px, py);
                    if (used > 0 && used + ItemCatalog.CapacityTicks(item) > Settings.pileCapacity * 20) continue;
                    GroundPile existing = ground.Find(p => p.X == px && p.Y == py && p.Item == item);
                    // Prefer a compatible stack; still allow different types to share remaining capacity.
                    if (distance == best && x >= 0 && existing == null) continue;
                    target = existing; x = px; y = py; best = distance;
                }
            return x >= 0;
        }
        private void Place(ItemId item, int amount, GroundPile target, int x, int y)
        {
            groundCapacity[(x, y)] = CellTicks(x, y) + amount * ItemCatalog.CapacityTicks(item);
            if (target == null) ground.Add(new GroundPile(x, y, item, amount)); else target.Amount += amount;
            GroundRevision++;
        }
        private static ActionResult Fail(string message) => new ActionResult(false, message);
        private static ActionResult Ok(string message, int amount = 0) => new ActionResult(true, message, amount);
    }
}
