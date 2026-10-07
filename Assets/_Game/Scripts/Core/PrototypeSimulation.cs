using System;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    public sealed class PrototypeSimulation
    {
        public const int BenchX = 10, BenchY = 6, WarehouseX = 15, WarehouseY = 5;
        public const int DropX = 7, DropY = 8;
        private const double Step = 0.05;
        private readonly List<GroundPile> ground = new List<GroundPile>();
        private readonly Random random;
        private readonly long[] incoming = new long[2];
        private double accumulatedTime;
        private double inputTimer;
        private bool jobActive;
        private WoodRecipe activeRecipe;
        private double jobProgress;

        public PrototypeSettings Settings { get; }
        public UnitInventory Carry { get; }
        public UnitInventory Warehouse { get; }
        public UnitInventory Output { get; }
        public IReadOnlyList<GroundPile> Ground => ground;
        public double PlayerX { get; private set; } = 4;
        public double PlayerY { get; private set; } = 4;
        public double GameSeconds { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool BenchBuilt { get; private set; }
        public bool BuildPending { get; private set; }
        public bool RunRequested { get; private set; }
        public WoodRecipe SelectedRecipe { get; private set; }
        public WorkState State { get; private set; } = WorkState.NotBuilt;
        public long Money { get; private set; }
        public long TotalSold { get; private set; }
        public int CompletedBatches { get; private set; }
        public int GroundRevision { get; private set; }
        public long IncomingUnits => incoming[0] + incoming[1];
        public long SpawnedUnits { get; private set; }
        public bool JobActive => jobActive;
        public int ReservedWood => jobActive ? Settings.recipeWoodInput : 0;
        public float Progress => jobActive ? (float)Math.Min(1, jobProgress / Duration(activeRecipe)) : 0;
        public bool NearWarehouse => Distance(PlayerX, PlayerY, WarehouseX, WarehouseY) <= 2.2;
        public bool NearBench => Distance(PlayerX, PlayerY, BenchX, BenchY) <= Settings.interactionRadius;
        public bool HasSaw => Carry.Count(ItemId.WoodHandSaw) + Warehouse.Count(ItemId.WoodHandSaw) > 0;

        public PrototypeSimulation(PrototypeSettings settings, int seed = 4271)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            random = new Random(seed);
            Carry = new UnitInventory(settings.carryCapacity);
            Warehouse = new UnitInventory(settings.warehouseCapacity);
            Output = new UnitInventory(settings.outputCapacity);
            AddInitial(4, 5, ItemId.WoodHandSaw, 1);
            AddInitial(5, 5, ItemId.RecoveredFastener, settings.benchFastenerCost + 1);
            for (int i = 0; i < 3; i++) AddInitial(5 + i, 6, ItemId.WasteWood, settings.pileCapacity);
            AddInitial(6, 7, ItemId.WasteWood, settings.pileCapacity);
            for (int x = 7; x <= 8; x++)
                for (int y = 7; y <= 9; y++) AddInitial(x, y, ItemId.MixedPlastic, settings.pileCapacity);
        }

        private void AddInitial(int x, int y, ItemId item, int amount)
        {
            int left = amount;
            while (left > 0 && FindGroundSpace(item, x, y, out GroundPile pile, out int px, out int py))
            {
                int placed = Math.Min(left, pile == null ? Settings.pileCapacity : Settings.pileCapacity - pile.Amount);
                Place(item, placed, pile, px, py);
                left -= placed;
            }
            if (left > 0) throw new ArgumentException("开局物资超过原型场地容量，请调整实验配置。");
        }

        public static double Distance(double ax, double ay, double bx, double by)
        { double dx = ax - bx, dy = ay - by; return Math.Sqrt(dx * dx + dy * dy); }

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
            PlayerX = Math.Max(0.5, Math.Min(Settings.mapWidth - 1.5,
                PlayerX + horizontal * Settings.moveSpeed * realSeconds * Speed));
            PlayerY = Math.Max(0.5, Math.Min(Settings.mapHeight - 1.5,
                PlayerY + vertical * Settings.moveSpeed * realSeconds * Speed));
        }

        public GroundPile NearestPickup()
        {
            GroundPile nearest = null;
            double distance = Settings.interactionRadius + 0.0001;
            foreach (GroundPile pile in ground)
            {
                double candidate = Distance(PlayerX, PlayerY, pile.X, pile.Y);
                if (candidate < distance) { nearest = pile; distance = candidate; }
            }
            return nearest;
        }

        public ActionResult PickUp(int requested = 1)
        {
            if (Paused) return Fail("游戏已暂停，恢复后再拾取。");
            GroundPile pile = NearestPickup();
            if (pile == null) return Fail("靠近地上的物品后按 E 拾取。");
            int amount = Math.Min(Math.Min(Math.Max(0, requested), pile.Amount), Carry.Free);
            if (amount == 0) return Fail("背包已满，先去仓库入库或放下一些物品。");
            Carry.TryAdd(pile.Item, amount);
            pile.Amount -= amount;
            if (pile.Amount == 0) ground.Remove(pile);
            GroundRevision++;
            DrainIncoming();
            RefreshState();
            return Ok("已拾取，物品现在位于背包。", amount);
        }

        public ActionResult Deposit()
        {
            if (Paused) return Fail("恢复运行后再搬运入库。");
            if (!NearWarehouse) return Fail("靠近仓库后按 Q 入库。");
            int moved = 0;
            foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
                if (item != ItemId.WoodHandSaw) moved += Carry.TransferTo(Warehouse, item, Carry.Count(item));
            RefreshState();
            return moved > 0 ? Ok("已入库，手锯继续随身携带。", moved)
                : Fail(Warehouse.Free == 0 ? "仓库已满，可出售产品或取出暂存物品。" : "背包中没有待入库材料。");
        }

        public ActionResult Withdraw(ItemId item, int requested = 1)
        {
            if (Paused) return Fail("恢复运行后再取出物品。");
            if (!NearWarehouse) return Fail("靠近仓库才能取出物品。");
            int moved = Warehouse.TransferTo(Carry, item, requested);
            RefreshState();
            return moved > 0 ? Ok("已从仓库取出。", moved) : Fail("库存不足或背包已满。");
        }

        public ActionResult Drop(ItemId item)
        {
            if (Paused) return Fail("恢复运行后再放下物品。");
            if (Carry.Count(item) == 0) return Fail("背包中没有该物品。");
            if (!FindGroundSpace(item, (int)Math.Round(PlayerX), (int)Math.Round(PlayerY),
                out GroundPile pile, out int x, out int y, 2.5)) return Fail("附近没有空地，物品保留在背包。");
            Place(item, 1, pile, x, y);
            Carry.TryRemove(item, 1);
            RefreshState();
            return Ok("已放在地上；露天堆放会产生压力。", 1);
        }

        public ActionResult BuildBench()
        {
            if (BenchBuilt || BuildPending) return Fail("木工台已建成或等待恢复运行。");
            if (!Warehouse.TryConsume(new ItemAmount(ItemId.WasteWood, Settings.benchWoodCost),
                new ItemAmount(ItemId.RecoveredFastener, Settings.benchFastenerCost)))
                return Fail($"仓库需要 {Settings.benchWoodCost} 废木料和 {Settings.benchFastenerCost} 回收连接件。");
            if (Paused) BuildPending = true; else BenchBuilt = true;
            RefreshState();
            return Ok(Paused ? "材料已预留，恢复运行后搭建。" : "木工台已搭建，带上手锯并靠近它。");
        }

        public ActionResult SelectRecipe(WoodRecipe recipe)
        {
            if (!Enum.IsDefined(typeof(WoodRecipe), recipe)) throw new ArgumentOutOfRangeException(nameof(recipe));
            if (jobActive) return Fail("当前批次完成后才能切换配方；停工会保留进度。");
            SelectedRecipe = recipe;
            RefreshState();
            return Ok("已切换加工配方。");
        }

        public ActionResult ToggleWork()
        {
            if (!BenchBuilt) return Fail("先用仓库材料搭建木工台。");
            RunRequested = !RunRequested;
            RefreshState();
            return Ok(RunRequested ? "已安排连续加工，主角需要在台旁持锯作业。" : "已停工，当前材料和进度保留。");
        }

        public ActionResult Sell(ItemId item, int requested)
        {
            if (item != ItemId.Plank && item != ItemId.WoodHandle) return Fail("这里只出售仓库中的木板和木柄。");
            int amount = Math.Min(Math.Max(0, requested), Warehouse.Count(item));
            if (amount == 0) return Fail("仓库没有可出售的该产品。");
            long earned = (long)amount * (item == ItemId.Plank ? Settings.plankPrice : Settings.handlePrice);
            Warehouse.TryRemove(item, amount);
            Money += earned;
            TotalSold += amount;
            RefreshState();
            return Ok($"出售 {amount} 件产品，获得 {earned} 金钱。", amount);
        }

        public int NearbyPressureTiles
        {
            get
            {
                int count = 0;
                foreach (GroundPile pile in ground)
                    if (Distance(pile.X, pile.Y, BenchX, BenchY) <= Settings.pressureRadius) count++;
                return count;
            }
        }

        public float HumanEfficiency => 1f - (1f - Settings.minimumEfficiency)
            * Math.Min(1f, (float)NearbyPressureTiles / Settings.pressureTilesForMaximum);

        public double Duration(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? Settings.plankSeconds : Settings.handleSeconds;
        public int ProductAmount(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? Settings.plankOutput : Settings.handleOutput;
        public ItemId Product(WoodRecipe recipe) => recipe == WoodRecipe.Plank ? ItemId.Plank : ItemId.WoodHandle;

        public void Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (Paused) { RefreshState(); return; }
            accumulatedTime += realSeconds * Speed;
            while (accumulatedTime + 0.00000001 >= Step)
            {
                accumulatedTime -= Step;
                GameSeconds += Step;
                if (BuildPending) { BuildPending = false; BenchBuilt = true; }
                if (Settings.automaticInput)
                {
                    inputTimer += Step;
                    double interval = Settings.firstInputSeconds / (1 + GameSeconds / Settings.inputGrowthSeconds);
                    while (inputTimer >= interval)
                    {
                        inputTimer -= interval;
                        long units = Settings.initialWaveUnits + (long)(GameSeconds / Settings.inputGrowthSeconds);
                        incoming[random.Next(0, 2)] += units;
                        SpawnedUnits += units;
                    }
                }
                DrainIncoming();
                Output.TransferTo(Warehouse, ItemId.Plank, Output.Count(ItemId.Plank));
                Output.TransferTo(Warehouse, ItemId.WoodHandle, Output.Count(ItemId.WoodHandle));
                RefreshState();
                if (State != WorkState.Working) continue;
                if (!jobActive)
                {
                    if (!Warehouse.TryRemove(ItemId.WasteWood, Settings.recipeWoodInput)) continue;
                    jobActive = true;
                    activeRecipe = SelectedRecipe;
                    jobProgress = 0;
                }
                jobProgress = Math.Min(Duration(activeRecipe), jobProgress + Step * HumanEfficiency);
                if (jobProgress + 0.00000001 < Duration(activeRecipe)) continue;
                if (!Output.TryAdd(Product(activeRecipe), ProductAmount(activeRecipe)))
                { State = WorkState.OutputBlocked; continue; }
                jobActive = false;
                jobProgress = 0;
                CompletedBatches++;
                Output.TransferTo(Warehouse, ItemId.Plank, Output.Count(ItemId.Plank));
                Output.TransferTo(Warehouse, ItemId.WoodHandle, Output.Count(ItemId.WoodHandle));
                RefreshState();
            }
        }

        private void RefreshState()
        {
            if (!BenchBuilt) State = WorkState.NotBuilt;
            else if (Paused) State = WorkState.Paused;
            else if (!RunRequested) State = WorkState.Idle;
            else if (Output.Free < ProductAmount(jobActive ? activeRecipe : SelectedRecipe)) State = WorkState.OutputBlocked;
            else if (!NearBench) State = WorkState.NeedsOperator;
            else if (!HasSaw) State = WorkState.NeedsTool;
            else if (!jobActive && Warehouse.Count(ItemId.WasteWood) < Settings.recipeWoodInput) State = WorkState.NeedsMaterials;
            else State = WorkState.Working;
        }

        private void DrainIncoming()
        {
            for (int index = 0; index < incoming.Length; index++)
            {
                ItemId item = index == 0 ? ItemId.WasteWood : ItemId.MixedPlastic;
                while (incoming[index] > 0 && FindGroundSpace(item, DropX, DropY, out GroundPile pile, out int x, out int y))
                {
                    int amount = (int)Math.Min(incoming[index], pile == null ? Settings.pileCapacity : Settings.pileCapacity - pile.Amount);
                    Place(item, amount, pile, x, y);
                    incoming[index] -= amount;
                }
            }
        }

        private bool FindGroundSpace(ItemId item, int centerX, int centerY, out GroundPile target,
            out int x, out int y, double maximumDistance = double.MaxValue)
        {
            target = null; x = -1; y = -1;
            double best = maximumDistance;
            for (int px = 0; px < Settings.mapWidth; px++)
            {
                for (int py = 0; py < Settings.mapHeight; py++)
                {
                    if (IsReservedCell(px, py)) continue;
                    double distance = Distance(centerX, centerY, px, py);
                    if (distance > best) continue;
                    GroundPile existing = ground.Find(p => p.X == px && p.Y == py);
                    if (existing != null && (existing.Item != item || existing.Amount >= Settings.pileCapacity)) continue;
                    // Prefer filling the nearest existing compatible pile on a distance tie.
                    if (distance == best && x >= 0 && existing == null) continue;
                    x = px; y = py; target = existing; best = distance;
                }
            }
            return x >= 0;
        }

        public static bool IsReservedCell(int x, int y) =>
            (x >= 14 && x <= 16 && y >= 4 && y <= 6) || (x >= 10 && x <= 11 && y >= 5 && y <= 6);

        private void Place(ItemId item, int amount, GroundPile target, int x, int y)
        {
            if (target == null) ground.Add(new GroundPile(x, y, item, amount));
            else target.Amount += amount;
            GroundRevision++;
        }

        private static ActionResult Fail(string message) => new ActionResult(false, message);
        private static ActionResult Ok(string message, int amount = 0) => new ActionResult(true, message, amount);
    }
}
