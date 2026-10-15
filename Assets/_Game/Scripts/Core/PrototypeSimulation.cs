using System;
using System.Collections.Generic;
using System.Linq;
namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        public const int BenchX=13,BenchY=7,WarehouseX=12,WarehouseY=5;
        public const int DropX=7,DropY=6,HousingX=17,HousingY=5;
        public const int TableX=13,TableY=15,DismantleX=13,DismantleY=11,GeneratorX=18,GeneratorY=7;
        private const double Step=0.05;
        private readonly List<GroundPile> ground=new List<GroundPile>();
        private readonly Dictionary<(int,int),int> groundCapacity=new Dictionary<(int,int),int>();
        private readonly List<Resident> residents=new List<Resident>();
        private readonly List<WageBill> bills=new List<WageBill>();
        private readonly List<ToolInstance> tools=new List<ToolInstance>();
        private readonly HashSet<ItemId> discovered=new HashSet<ItemId>();
        private readonly SerializableRandom random;
        private readonly double[] salesPressure=new double[Enum.GetValues(typeof(ItemId)).Length];
        private readonly Dictionary<int,List<(UnitInventory inventory,int token)>> blueprints=new Dictionary<int,List<(UnitInventory,int)>>();
        private double accumulatedTime,inputTimer,nextInput,jobProgress,dismantleProgress,nextWage;
        private decimal saleRemainderCents;
        private bool jobActive,dismantleActive;
        private WoodRecipe activeRecipe;
        private int pendingBuildings,nextGroundId=1,nextToolId=1,geometryRevision,woodToken,dismantleToken,woodToolId,screwToolId,wrenchToolId;
        private int dismantleObjectId;
        private bool dismantleUsable=true;
        private long cashCents;
        private int fuelWood,fuelChips,manualStation;
        private double fuelRemaining,equivalentHeat,activeFuelHeatRate;
        private readonly Dictionary<(int,int),(int revision,float value)> pressureCache=new Dictionary<(int,int),(int,float)>();
        private readonly Dictionary<(int,int),int> pressureCapacity=new Dictionary<(int,int),int>();
        private int pressureRevision=-1;
        public PrototypeSettings Settings {get;}
        public UnitInventory Carry {get;}
        public UnitInventory Warehouse {get;}
        public UnitInventory WoodInputStock {get;}=new UnitInventory(8);
        public UnitInventory DismantleInput {get;}=new UnitInventory(12);
        public UnitInventory WoodTools {get;}=new UnitInventory(2);
        public UnitInventory DismantleTools {get;}=new UnitInventory(4);
        public UnitInventory BrokenTools {get;}=new UnitInventory(8);
        public UnitInventory Output {get;}
        public UnitInventory DismantleOutput {get;}=new UnitInventory(11);
        public UnitInventory GeneratorResidue {get;}=new UnitInventory(10);
        public IReadOnlyList<GroundPile> Ground=>ground;
        public IReadOnlyList<Resident> Residents=>residents;
        public IReadOnlyList<WageBill> WageBills=>bills;
        public IReadOnlyList<ToolInstance> Tools=>tools;
        public double PlayerX {get;private set;}=8;
        public double PlayerY {get;private set;}=6;
        public double GameSeconds {get;private set;}
        public bool Paused {get;private set;}
        public int Speed {get;private set;}=1;
        public bool BenchBuilt {get;private set;}
        public bool TableBuilt {get;private set;}
        public bool DismantleBuilt {get;private set;}
        public bool GeneratorBuilt {get;private set;}
        public bool BuildPending=>(pendingBuildings&1)!=0;
        public bool AnyBuildPending=>pendingBuildings!=0;
        public bool HasBeenPowered {get;private set;}
        public bool RunRequested {get;private set;}
        public bool DismantleRequested {get;private set;}
        public WoodRecipe SelectedRecipe {get;private set;}
        public ItemId SelectedDevice {get;private set;}=ItemId.OldCutter;
        private WoodRecipe? pendingRecipe;
        private ItemId? pendingDevice;
        public bool RecipeChangePending=>pendingRecipe!=null;
        public WorkState State {get;private set;}
        public WorkState DismantleState {get;private set;}
        public long CashCents=>cashCents;
        public decimal Money=>cashCents/100m;
        public long TotalSold {get;private set;}
        public long TotalWagesPaidCents {get;private set;}
        public int CompletedBatches {get;private set;}
        public int DismantledBatches {get;private set;}
        public int DeliveredTrips {get;private set;}
        public int GroundRevision {get;private set;}
        public long IncomingUnits=>0;
        public long SpawnedUnits {get;private set;}
        public bool JobActive=>jobActive;
        public bool PlayerSawing=>manualStation==1&&State==WorkState.Working&&SelectedRecipe!=WoodRecipe.SortedWood;
        public int ReservedWood=>jobActive?WoodInput(activeRecipe):0;
        public float Progress=>jobActive?(float)Math.Min(1,jobProgress/Duration(activeRecipe)):0;
        public float DismantleProgress=>(float)(dismantleProgress/35);
        public bool NearWarehouse=>Near(WarehouseX,WarehouseY);
        public bool NearBench=>Near(BenchX,BenchY);
        public bool NearGenerator=>Near(GeneratorX,GeneratorY);
        public bool HasSaw=>tools.Any(t=>t.Item==ItemId.WoodHandSaw&&t.Durability>0&&!t.Location.StartsWith("ground:"));
        public int FuelCount=>fuelWood+fuelChips;
        public double FuelSeconds=>fuelRemaining+fuelWood*Settings.woodFuelSeconds+fuelChips*Settings.chipsFuelSeconds;
        public float AvailablePower=>GeneratorBuilt&&fuelRemaining>0?Settings.generatorOutput:0;
        public double SecondsToWage=>residents.Count==0?0:Math.Max(0,nextWage-GameSeconds);
        public long UnpaidWageCents=>bills.Sum(b=>b.RemainingCents);
        public decimal UnpaidWages=>UnpaidWageCents/100m;
        public double EquivalentUnpaidPeriods=>bills.Sum(b=>(double)b.RemainingCents/b.OriginalCents);
        public Resident Woodworker=>residents.Find(r=>r.Job==WorkerJob.Woodwork);
        public bool WorkerAtBench=>Woodworker!=null&&Woodworker.Delivery==null&&Distance(Woodworker.X,Woodworker.Y,BenchX,BenchY)<=0.1;
        public PrototypeSimulation(PrototypeSettings settings,int seed=4271,bool gradualOpening=false)
        {
            Settings=settings??throw new ArgumentNullException(nameof(settings));settings.Validate();random=new SerializableRandom(seed);
            Carry=new UnitInventory(settings.carryCapacity);Warehouse=new UnitInventory(settings.warehouseCapacity);Output=new UnitInventory(settings.outputCapacity);
            nextInput=RandomInputInterval();
            PrepareOpening();
            if(gradualOpening){Opening=new OpeningProgress();return;}
            AddInitial(7,6,ItemId.WoodHandSaw,1);AddInitial(7,7,ItemId.RecoveredFastener,settings.initialFasteners);
            AddInitial(DropX,DropY,ItemId.WasteWood,settings.initialWood);
            foreach(var pair in new[]{new ItemAmount(ItemId.SteelPart,30),new ItemAmount(ItemId.CopperPart,2),new ItemAmount(ItemId.RecoveredWire,8),
                new ItemAmount(ItemId.ThermalModule,1),new ItemAmount(ItemId.GeneratorModule,1),new ItemAmount(ItemId.Food,30),new ItemAmount(ItemId.DrinkingWater,40),
                new ItemAmount(ItemId.Soap,3),new ItemAmount(ItemId.WasteTextile,12),new ItemAmount(ItemId.OldClothes,6)})
                AddInitial(DropX,DropY,pair.Item,pair.Amount);
            foreach(var item in new[]{ItemId.Screwdriver,ItemId.Wrench,ItemId.Hammer,ItemId.Scissors,ItemId.SewingKit})AddInitial(DropX,DropY,item,1);
            foreach(var pair in new[]{new ItemAmount(ItemId.OldCutter,2),new ItemAmount(ItemId.OldWasher,1),new ItemAmount(ItemId.OldFormer,1),new ItemAmount(ItemId.MouldBox,1),
                new ItemAmount(ItemId.OldContainer,2),new ItemAmount(ItemId.WireBundle,1),new ItemAmount(ItemId.OldGrowingLamp,1),new ItemAmount(ItemId.OldFeeder,1),
                new ItemAmount(ItemId.OldFurnace,2),new ItemAmount(ItemId.OldMetalTable,1),new ItemAmount(ItemId.OldControlBox,1)})AddInitial(DropX,DropY,pair.Item,pair.Amount);
        }
        public static double Distance(double ax,double ay,double bx,double by){double dx=ax-bx,dy=ay-by;return Math.Sqrt(dx*dx+dy*dy);}
        private bool Near(int x,int y,double radius=-1)=>Distance(PlayerX,PlayerY,x,y)<=(radius<0?Settings.interactionRadius:radius);
        public static bool IsOwnedGround(int x,int y)=>(x>=10&&x<=27&&y>=6&&y<=23)||(x>=0&&x<=7&&y>=6&&y<=11);
        private static bool InBuilding(int x,int y,int bx,int by,int width=2,int height=2)=>x>=bx&&x<bx+width&&y>=by&&y<by+height;
        public static bool IsReservedCell(int x,int y)=>InBuilding(x,y,11,2,3,3)||InBuilding(x,y,16,2,3,3)||InBuilding(x,y,2,20)||(x==20&&y==5);
        private bool Occupied(int x,int y)=>IsReservedCell(x,y)||!IsWalkable(x,y);
        public void SetPaused(bool value){Paused=value;RefreshState();}
        public void SetSpeed(int speed){if(speed<1||speed>3)throw new ArgumentOutOfRangeException(nameof(speed));Speed=speed;}
        public void MovePlayer(double hx,double hy,double seconds)
        {
            if(Paused||Failed||seconds<=0||hx==0&&hy==0)return;
            CancelInteraction();manualStation=0;
            bool horizontalFirst=Math.Abs(hx)>=Math.Abs(hy);
            double amount=Settings.moveSpeed*seconds*Speed;
            while(amount>0)
            {
                double step=Math.Min(0.1,amount);
                bool moved=horizontalFirst
                    ? TryMovePlayerStep(Math.Sign(hx)*step,0)||(hy!=0&&TryMovePlayerStep(0,Math.Sign(hy)*step))
                    : TryMovePlayerStep(0,Math.Sign(hy)*step)||(hx!=0&&TryMovePlayerStep(Math.Sign(hx)*step,0));
                if(!moved)break;
                amount-=step;
            }
            ReleaseUnusedTools();RefreshState();
        }
        private bool TryMovePlayerStep(double dx,double dy)
        {
            double x=PlayerX+dx,y=PlayerY+dy;
            if(!IsWalkable(CellCoordinate(x),CellCoordinate(y)))return false;
            PlayerX=x;PlayerY=y;return true;
        }
        public GroundPile NearestPickup()=>ground.Where(p=>p.Amount>p.Reserved&&Distance(PlayerX,PlayerY,p.X,p.Y)<=Settings.interactionRadius)
            .OrderBy(p=>Distance(PlayerX,PlayerY,p.X,p.Y)).ThenByDescending(p=>ItemCatalog.IsTool(p.Item)).ThenBy(p=>p.Id).FirstOrDefault();
        public ActionResult BuildBench()=>Build(1,BenchBuilt,new ItemAmount(ItemId.WasteWood,4),new ItemAmount(ItemId.RecoveredFastener,2));
        public ActionResult BuildDismantle()=>Build(2,DismantleBuilt,new ItemAmount(ItemId.WasteWood,4),new ItemAmount(ItemId.RecoveredFastener,2));
        public ActionResult BuildTable()=>Build(4,TableBuilt,new ItemAmount(ItemId.Plank,4),new ItemAmount(ItemId.SteelPart,2));
        public ActionResult BuildGenerator()=>Build(8,GeneratorBuilt,new ItemAmount(ItemId.SteelPart,6),new ItemAmount(ItemId.ThermalModule,1),new ItemAmount(ItemId.GeneratorModule,1),new ItemAmount(ItemId.RecoveredWire,2));
        private ActionResult Build(int flag,bool built,params ItemAmount[] costs)
        {
            if(RiotActive||Failed)return Fail("暴动或失败期间无法安排建设。");
            if(built||(pendingBuildings&flag)!=0)return Fail("已经建成或规划。");
            if(manualStation!=0&&!residents.Any(r=>r.Job==WorkerJob.Idle&&r.Delivery==null))return Fail("缺空闲建设人员，请先退出岗位。");
            if(flag==4 && (!discovered.Contains(ItemId.Plank)||!discovered.Contains(ItemId.SteelPart)))return Fail("首次取得木板和钢铁零件后解锁工作台。");
            if(flag==8&&!discovered.Contains(ItemId.SteelPart))return Fail("先取得钢铁零件以解锁发电机。");
            string obstruction=ConstructionObstruction(flag);
            if(obstruction!=null)return Fail(obstruction);
            var tokens=new List<(UnitInventory,int)>();
            foreach(var cost in costs)
            {
                int fromCarry=Math.Min(cost.Amount,Carry.Available(cost.Item)),fromWarehouse=cost.Amount-fromCarry;
                if(Warehouse.Available(cost.Item)<fromWarehouse){foreach(var token in tokens)token.Item1.Release(token.Item2);return Fail("背包及仓库材料不足。");}
                if(fromCarry>0)tokens.Add((Carry,Carry.Reserve(new ItemAmount(cost.Item,fromCarry))));
                if(fromWarehouse>0)tokens.Add((Warehouse,Warehouse.Reserve(new ItemAmount(cost.Item,fromWarehouse))));
            }
            blueprints[flag]=tokens;pendingBuildings|=flag;
            if(!Paused)CompleteBuildings();
            RefreshState();return Ok(Paused?"已预约材料，恢复后建成；取消可全释放。":"建设完成。");
        }
        public ActionResult CancelBlueprints()
        {
            foreach(var list in blueprints.Values)foreach(var token in list)token.inventory.Release(token.token);
            blueprints.Clear();pendingBuildings=0;return Ok("蓝图已取消，预约全部释放。");
        }
        private void CompleteBuildings()
        {
            if(pendingBuildings==0)return;
            foreach(int flag in blueprints.Keys.ToArray())
            {
                // Recheck when resuming: people or dropped items can occupy an existing blueprint.
                if(ConstructionObstruction(flag)!=null)continue;
                foreach(var token in blueprints[flag])token.inventory.Commit(token.token);
                if(flag==1)BenchBuilt=true;if(flag==2)DismantleBuilt=true;if(flag==4)TableBuilt=true;if(flag==8)GeneratorBuilt=true;
                blueprints.Remove(flag);pendingBuildings&=~flag;geometryRevision++;GroundRevision++;
            }
        }
        private string ConstructionObstruction(int flag)
        {
            int x=flag==1?BenchX:flag==2?DismantleX:flag==4?TableX:GeneratorX,y=flag==1?BenchY:flag==2?DismantleY:flag==4?TableY:GeneratorY;
            if(InBuilding(CellCoordinate(PlayerX),CellCoordinate(PlayerY),x,y+1)||
                residents.Any(r=>InBuilding(CellCoordinate(r.X),CellCoordinate(r.Y),x,y+1)))
                return "建设位置有人，请先离开设施占地。";
            for(int px=x;px<x+2;px++)for(int py=y+1;py<y+3;py++)if(CellTicks(px,py)>0)return "建设位置有地面物品，先清理。";
            return null;
        }
        public ActionResult SelectRecipe(WoodRecipe recipe)
        {
            if(!Enum.IsDefined(typeof(WoodRecipe),recipe))throw new ArgumentOutOfRangeException(nameof(recipe));
            pendingRecipe=recipe;ApplyRecipeChanges();RefreshState();return Ok(pendingRecipe!=null?"配方已排队：先完成当前批次，并实际清出旧缓冲。":"已切换配方。");
        }
        public ActionResult SelectDevice(ItemId item)
        {
            if(!ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))return Fail("选择可拆设备。");
            pendingDevice=item;ApplyRecipeChanges();RefreshState();return Ok(pendingDevice!=null?"拆解模板排队，完成后先搬走旧缓冲。":"拆解配方已选择。");
        }
        private void ApplyRecipeChanges()
        {
            if(pendingRecipe!=null&&!jobActive&&Output.UsedTicks==0)
            {
                int capacity=pendingRecipe==WoodRecipe.WoodHandle?4:8;
                if(WoodInputStock.Used+IncomingTicks(WoodInputStock)/20m<=capacity){SelectedRecipe=pendingRecipe.Value;WoodInputStock.SetCapacity(capacity);Output.SetCapacity(SelectedRecipe==WoodRecipe.WoodHandle?4:8);pendingRecipe=null;}
            }
            if(pendingDevice!=null&&!dismantleActive&&DismantleInput.UsedTicks==0&&DismantleOutput.UsedTicks==0)
            {
                SelectedDevice=pendingDevice.Value;int ticks=ItemCatalog.DeviceYield(SelectedDevice).Sum(p=>p.Amount*ItemCatalog.CapacityTicks(p.Item));DismantleOutput.SetCapacity((int)Math.Ceiling(ticks*2/20.0));pendingDevice=null;
            }
        }
        public ActionResult ToggleWork()
        {
            if(RiotActive||Failed)return Fail("人工岗位因暴动或失败停工。");
            if(!BenchBuilt)return Fail("先建木工地点。");
            if(NearBench){manualStation=manualStation==1?0:1;RunRequested=true;ReleaseUnusedTools();RefreshState();return Ok(manualStation==1?"主角进入木工岗位。":"主角退出，投入及进度保留。");}
            RunRequested=!RunRequested;RefreshState();return Ok(RunRequested?"木工请求已开启。":"木工请求已停。");
        }
        public ActionResult ToggleDismantle()
        {
            if(RiotActive||Failed)return Fail("人工岗位因暴动或失败停工。");
            if(!DismantleBuilt)return Fail("先建拆解地点。");
            DismantleRequested=!DismantleRequested;manualStation=DismantleRequested&&Near(DismantleX,DismantleY)?2:0;ReleaseUnusedTools();RefreshState();return Ok("拆解请求已调整。");
        }
        public ActionResult CancelBatch(bool dismantle=false)
        {
            if(dismantle)
            {
                if(dismantleProgress>=35-0.000001&&dismantleActive)return Fail("已完成待出料的批次不能退同批原料。");
                if(dismantleActive)DismantleInput.Release(dismantleToken);dismantleActive=false;dismantleProgress=0;DismantleRequested=false;
            }
            else
            {
                if(jobActive&&jobProgress>=Duration(activeRecipe)-0.000001)return Fail("已完成待出料的批次不能退同批原料。");
                if(jobActive)WoodInputStock.Release(woodToken);jobActive=false;jobProgress=0;RunRequested=false;
            }
            manualStation=0;ReleaseUnusedTools();RefreshState();return Ok("在制预约释放，进度清零；已用人时不返还。");
        }
        private void Ignite()
        {
            if(fuelRemaining>0||!GeneratorBuilt)return;
            if(GeneratorResidue.FreeTicks<20&&equivalentHeat>=20-0.000001)return;
            if(fuelWood>0){fuelWood--;fuelRemaining=Settings.woodFuelSeconds;activeFuelHeatRate=1.0/Settings.woodFuelSeconds;}
            else if(fuelChips>0){fuelChips--;fuelRemaining=Settings.chipsFuelSeconds;activeFuelHeatRate=0.5/Settings.chipsFuelSeconds;}else return;
            HasBeenPowered=true;
        }
        public ActionResult Recruit()
        {
            if(!HasBeenPowered)return Fail("先实际发电取得认可。");
            if(residents.Count>=Settings.firstResidents)return Fail("旧住房满，请在后续住房阶段扩员。");
            if(residents.Count==0){nextWage=GameSeconds+Settings.wageSeconds;if(firstPayrollAt==double.MaxValue){firstPayrollAt=nextWage;nextLivingPeriod=nextWage;}}
            int id=Math.Max(residents.Select(r=>r.Id).DefaultIfEmpty(0).Max(),bills.SelectMany(b=>b.ResidentIds).DefaultIfEmpty(0).Max())+1;
            residents.Add(new Resident(id){Needs=new LivingNeeds{nextMeal=GameSeconds+300}});return Ok("居民加入：迁入存款5，周期工资10；饮食每300秒消耗。");
        }
        public ActionResult AssignWoodworker()
        {
            var worker=Woodworker;if(worker!=null){worker.Job=WorkerJob.Idle;RefreshState();return Ok("取消木工指定，已携货仍会送达。");}
            worker=residents.FirstOrDefault(r=>r.Job==WorkerJob.Idle);
            if(worker==null)return Fail("没有可分配居民。");
            worker.Job=WorkerJob.Woodwork;RefreshState();return Ok("居民完成当前搬运后到木工正面；请交接手锯。");
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
                        if (recipient == null) CommunityCashCents += transfer; else recipient.CashCents += transfer;
                        cashCents -= transfer; bill.UnpaidCents[i] -= transfer; paid += transfer;
                    }
                }
                if (cashCents == 0) break;
            }
            TotalWagesPaidCents += paid;if(RiotActive){RiotPaidCents+=paid;if(RiotPaidCents>=RiotTargetCents)RiotActive=false;} RefreshState();
            return Ok($"实付工资{paid / 100m:0.00}，剩余欠款{UnpaidWages:0.00}。");
        }

        public double SalePrice(ItemId item)
        {
            var price=ItemCatalog.Price(item);return MinimumPriceOnly(item)?(double)price.minimum:Math.Max((double)price.minimum,(double)price.sale*Math.Exp(-salesPressure[(int)item]/100));
        }
        public ActionResult Sell(ItemId item, int requested)
        {
            if (ItemCatalog.Price(item).sale<=0) return Fail("此物品没有前期出售渠道。");
            int amount = Math.Min(Math.Max(0, requested), Warehouse.Available(item));
            if(item==ItemId.DrinkingWater)amount=Math.Min(amount,Math.Max(0,Warehouse.Available(item)-DrinkingReserve));
            if (amount == 0) return Fail("仓库没有该成品。");
            decimal receipt = saleRemainderCents;
            for (int i = 0; i < amount; i++) { receipt += (decimal)SalePrice(item) * 100 * SaleDurabilityFactor(item, i); salesPressure[(int)item] += 1; }
            long earned = (long)decimal.Floor(receipt);
            saleRemainderCents = receipt - earned;
            Warehouse.TryRemove(item, amount); cashCents += earned; TotalSold += amount; RefreshState();
            if(ItemCatalog.IsTool(item))foreach(var tool in tools.Where(t=>t.Item==item&&t.Location=="warehouse"&&t.ReservedBy==0).OrderBy(t=>t.Id).Take(amount).ToArray())tools.Remove(tool);
            return Ok($"出售{amount}，获得{earned / 100m:0.00}钱。", amount);
        }


        public int NearbyPressureTiles=>ground.Where(p=>p.Item!=ItemId.PowerAsh&&Distance(p.X,p.Y,BenchX,BenchY)<=4).Select(p=>(p.X,p.Y)).Distinct().Count();
        public float PressureAt(double cx,double cy)
        {
            var key=((int)Math.Round(cx),(int)Math.Round(cy));
            if(pressureCache.TryGetValue(key,out var value)&&value.revision==GroundRevision)return value.value;
            if(pressureRevision!=GroundRevision)
            {
                pressureCapacity.Clear();
                foreach(var pile in ground)
                    if(pile.Item!=ItemId.PowerAsh)for(int x=pile.X;x<pile.X+pile.Width;x++)
                    {var cell=(x,pile.Y);pressureCapacity.TryGetValue(cell,out int ticks);pressureCapacity[cell]=ticks+pile.Amount*ItemCatalog.CapacityTicks(pile.Item)/pile.Width;}
                pressureRevision=GroundRevision;
            }
            int cells=0,exposed=0;double excess=0;
            for(int x=Math.Max(0,key.Item1-4);x<=Math.Min(Settings.mapWidth-1,key.Item1+4);x++)for(int y=Math.Max(0,key.Item2-4);y<=Math.Min(Settings.mapHeight-1,key.Item2+4);y++)
            {
                if(!IsOwnedGround(x,y)||Occupied(x,y)||Distance(x,y,key.Item1,key.Item2)>4)continue;cells++;
                pressureCapacity.TryGetValue((x,y),out int ticks);
                if(ticks>0){exposed++;excess+=Math.Max(0,ticks/20.0-5);}
            }
            double ratio=cells==0?0:(double)exposed/cells;
            double area=ratio<=0.3?1:ratio<=0.6?1-(ratio-0.3)*0.2/0.3:0.8-(ratio-0.6)*0.2/0.4;
            double overload=exposed==0?1:Math.Max(0.4,1/(1+excess/(exposed*5)));
            float result=(float)Math.Max(0.25,area*overload);pressureCache[key]=(GroundRevision,result);return result;
        }
        public float HumanEfficiency=>(float)DesignRules.HumanEfficiency(1,0,70,WorkerAtBench?EquivalentUnpaidPeriods:0,(WorkerAtBench?Woodworker.Needs:PlayerNeeds).FoodMultiplier(GameSeconds),(WorkerAtBench?Woodworker.Needs:PlayerNeeds).WaterMultiplier(GameSeconds),PressureAt(BenchX,BenchY),1);
        public double Duration(WoodRecipe recipe)=>recipe==WoodRecipe.Plank?40:recipe==WoodRecipe.WoodHandle?30:10;
        public int ProductAmount(WoodRecipe recipe)=>recipe==WoodRecipe.Plank?3:recipe==WoodRecipe.WoodHandle?2:4;
        public ItemId Product(WoodRecipe recipe)=>recipe==WoodRecipe.Plank?ItemId.Plank:recipe==WoodRecipe.WoodHandle?ItemId.WoodHandle:ItemId.SortedWood;
        public int WoodInput(WoodRecipe recipe)=>recipe==WoodRecipe.WoodHandle?2:4;
        private ItemAmount[] WoodProducts(WoodRecipe recipe)=>recipe==WoodRecipe.SortedWood?new[]{new ItemAmount(ItemId.SortedWood,4)}:new[]{new ItemAmount(Product(recipe),ProductAmount(recipe)),new ItemAmount(ItemId.WoodChips,1)};
        private int BatchTicks(WoodRecipe recipe)=>WoodProducts(recipe).Sum(p=>p.Amount*ItemCatalog.CapacityTicks(p.Item));
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(Paused||Failed){RefreshState();return;}accumulatedTime+=seconds*Speed;
            while(accumulatedTime+0.00000001>=Step)
            {
                accumulatedTime-=Step;GameSeconds+=Step;CompleteBuildings();TickInteraction(Step);TickOpening();
                for(int i=0;i<salesPressure.Length;i++)salesPressure[i]*=Math.Exp(-Math.Log(2)*Step/600);
                if(fuelRemaining>0&&(equivalentHeat<20-0.000001||GeneratorResidue.FreeTicks>=20))
                {double burned=Math.Min(Step,fuelRemaining);fuelRemaining=Math.Max(0,fuelRemaining-burned);if(fuelRemaining<1e-8)fuelRemaining=0;equivalentHeat+=burned*activeFuelHeatRate;}
                if(equivalentHeat>=20-0.000001&&GeneratorResidue.TryAdd(ItemId.PowerAsh,1))equivalentHeat=Math.Max(0,equivalentHeat-20);Ignite();
                if(residents.Count>0&&GameSeconds+1e-6>=nextWage){bills.Add(new WageBill(residents.Select(r=>r.Id).ToArray(),(long)(Settings.wagePerResident*100),nextWage));nextWage+=Settings.wageSeconds;CheckWageRiot();}
                TickCommunity(Step);
                if(Failed){accumulatedTime=0;break;}
                TickWorkshop(Step);TickToolPurchase();
                TickResidents(Step);
                if(Settings.automaticInput){inputTimer+=Step;while(inputTimer>=nextInput){inputTimer-=nextInput;GenerateInput(nextInput);nextInput=RandomInputInterval();}}
                RefreshState();
                if(State==WorkState.Working)
                {
                    if(!jobActive){woodToken=WoodInputStock.Reserve(new ItemAmount(ItemId.WasteWood,WoodInput(SelectedRecipe)));jobActive=true;activeRecipe=SelectedRecipe;jobProgress=0;woodToolId=SelectedRecipe==WoodRecipe.SortedWood?0:ToolFor(1,ItemId.WoodHandSaw)?.Id??0;var tool=tools.FirstOrDefault(t=>t.Id==woodToolId);if(tool!=null)tool.UsingAt=1;}
                    jobProgress=Math.Min(Duration(activeRecipe),jobProgress+Step*HumanEfficiency);
                }
                if(jobActive&&jobProgress+1e-6>=Duration(activeRecipe)&&Output.FreeTicks>=BatchTicks(activeRecipe)&&CanWear(woodToolId,WoodInput(activeRecipe)))
                {
                    WoodInputStock.Commit(woodToken);Output.TryAddBatch(WoodProducts(activeRecipe));DiscoverProducts(WoodProducts(activeRecipe));
                    WearTool(woodToolId,WoodInput(activeRecipe));jobActive=false;jobProgress=0;CompletedBatches++;woodToolId=0;
                }
                if(DismantleState==WorkState.Working)
                {
                    if(!dismantleActive){dismantleToken=DismantleInput.Reserve(new ItemAmount(SelectedDevice,1));dismantleActive=true;dismantleProgress=0;screwToolId=ToolFor(2,ItemId.Screwdriver)?.Id??0;wrenchToolId=ItemCatalog.NeedsWrench(SelectedDevice)?ToolFor(2,ItemId.Wrench)?.Id??0:0;var device=tools.FirstOrDefault(t=>t.Item==SelectedDevice&&t.Location=="dismantleInput");dismantleObjectId=device?.Id??0;dismantleUsable=device?.Usable??true;}
                    dismantleProgress=Math.Min(35,dismantleProgress+Step*PressureAt(DismantleX,DismantleY)*LivingEfficiency(null));
                }
                var products=ItemCatalog.DeviceYield(SelectedDevice,dismantleUsable);
                if(dismantleActive&&dismantleProgress>=35-1e-6&&DismantleOutput.FreeTicks>=products.Sum(p=>p.Amount*ItemCatalog.CapacityTicks(p.Item))&&CanWearDismantleTools())
                {
                    DismantleInput.Commit(dismantleToken);DismantleOutput.TryAddBatch(products);DiscoverProducts(products);
                    WearTool(screwToolId,1);WearTool(wrenchToolId,1);dismantleActive=false;dismantleProgress=0;DismantledBatches++;screwToolId=wrenchToolId=0;
                    tools.RemoveAll(t=>t.Id==dismantleObjectId);dismantleObjectId=0;
                }
                ReleaseUnusedTools();RefreshState();
                ApplyRecipeChanges();
                CheckDemoComplete();
            }
            Tutorial.Refresh(this);
        }
        private void DiscoverProducts(ItemAmount[] products){foreach(var product in products)discovered.Add(product.Item);}
        private void RefreshState()
        {
            bool mainWood=manualStation==1&&AtFront(BenchX,BenchY),mainDismantle=manualStation==2&&AtFront(DismantleX,DismantleY);
            if(!BenchBuilt)State=WorkState.NotBuilt;else if(Paused)State=WorkState.Paused;else if(RiotActive||!RunRequested)State=WorkState.Idle;
            else if(pendingRecipe!=null&&!jobActive)State=WorkState.OutputBlocked;
            else if(jobActive&&jobProgress>=Duration(activeRecipe)-1e-6||!jobActive&&Output.FreeTicks<BatchTicks(SelectedRecipe))State=WorkState.OutputBlocked;
            else if(!mainWood&&!WorkerAtBench)State=WorkState.NeedsOperator;
            else if((jobActive?activeRecipe:SelectedRecipe)!=WoodRecipe.SortedWood&&ToolFor(1,ItemId.WoodHandSaw)==null)State=WorkState.NeedsTool;
            else if(!jobActive&&WoodInputStock.Available(ItemId.WasteWood)<WoodInput(SelectedRecipe))State=WorkState.NeedsMaterials;else State=WorkState.Working;
            if(!DismantleBuilt)DismantleState=WorkState.NotBuilt;else if(Paused)DismantleState=WorkState.Paused;else if(RiotActive||!DismantleRequested)DismantleState=WorkState.Idle;
            else if(pendingDevice!=null&&!dismantleActive)DismantleState=WorkState.OutputBlocked;
            else if(dismantleActive&&dismantleProgress>=35-1e-6)DismantleState=WorkState.OutputBlocked;
            else if(!mainDismantle)DismantleState=WorkState.NeedsOperator;
            else if(ToolFor(2,ItemId.Screwdriver)==null||ItemCatalog.NeedsWrench(SelectedDevice)&&ToolFor(2,ItemId.Wrench)==null)DismantleState=WorkState.NeedsTool;
            else if(!dismantleActive&&DismantleInput.Available(SelectedDevice)==0)DismantleState=WorkState.NeedsMaterials;else DismantleState=WorkState.Working;
        }
        private static ActionResult Fail(string message)=>new ActionResult(false,message);
        private static ActionResult Ok(string message,int amount=0)=>new ActionResult(true,message,amount);
    }
}
