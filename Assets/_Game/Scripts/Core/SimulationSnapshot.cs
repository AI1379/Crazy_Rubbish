using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace CrazyRubbish.Core
{
    [Serializable] public sealed class InventorySave { public int capacity,nextToken;public int[] counts;public List<ReservationSave> reservations; }
    [Serializable] public sealed class ReservationSave { public int id;public int[] items,amounts; }
    [Serializable] public sealed class GroundSave { public int id,x,y,item,amount,reserved,width,instance;public bool usable; }
    [Serializable] public sealed class ToolSave { public int id,item,durability,reserved,usingAt,condition;public string location;public bool usable; }
    [Serializable] public sealed class BillSave { public int[] ids;public long[] unpaid;public long original;public double due; }
    [Serializable] public sealed class DeliverySave { public int item,amount,token,stage,id,sx,sy,tx,ty,groundId;public string from,to;public double progress,retryAt;public bool picked; }
    [Serializable] public sealed class ResidentSave { public int id,job,facilityId,pathIndex,pathX,pathY,pathRevision;public int[] path;public double x,y,happiness=70;public long cash;public InventorySave cargo;public DeliverySave delivery;public LivingNeeds needs;public HappinessState mood; }
    [Serializable] public sealed class CommunitySave
    {
        public LivingNeeds player;public int foodPolicy,waterPolicy,meals,water,turn;public bool external,emergency,delivery,failed,riot;
        public string failure;public long community,spent,retail,riotTarget,riotPaid;public double firstPayroll,nextPeriod,riotDeadline;
    }
    [Serializable] public sealed class WorkshopSave
    {
        public int recipe,token,assembled,boughtMask;public double progress,purchaseAt,budgetReset;public long budgetSpent;public bool autoPurchase;
    }
    // Full-game state added with the complete v1 catalog (schema 6).
    [Serializable] public sealed class ExpansionSave
    {
        public int ownedRegion=1,unrecognizedOther,liftStage,bonusLevel,processBatches;
        public double[] regionBudgets;public int[] regionPending;
        public double invasionAt=-1,happinessDeadline,bonusUntil,heroClothes=4800,soapUnits,nextCleaning=600,heroClothesRetry;
        public bool ended,heroAboard,happinessRiot,promise,rest,communityClean=true,autoToolMaking;
        public string endingKind="",endingText="";
        public long landfilled,bonusPaid,rent=100;
    }
    [Serializable] public sealed class BlueprintSave { public int flag;public string inventory;public int token; }
    [Serializable] public sealed class SimulationSnapshot
    {
        public const int CurrentSchema=6;
        public int schema=CurrentSchema,itemCount,sourceVersion=11;
        public bool autoMachineStaffing=true;public double nextMachineStaffing;
        public System.Collections.Generic.List<ConveyorLane> conveyors;public System.Collections.Generic.List<ConveyorPort> ports;public System.Collections.Generic.List<InfrastructurePlan> infrastructurePlans;
        public int nextConveyor=1,nextPort=1,nextInfrastructurePlan=-1000000000,beltCredit,portTurn;
        public string baselineVersion=DesignBaseline.Version;
        public string[] itemIds;
        public int totalRecruited;
        public long conveyorDelivered;
        public List<WirePlan> wirePlans;public int nextWirePlan=-1;
        public List<FacilityInstance> facilities;public List<WireCell> wires;public int[] selectedFacilities;public int nextFacility,manualFacility,wireCredit;public PrototypeSettings settings;public uint random;
        public CommunitySave community;public WorkshopSave workshop;public bool demoCompleted;public double demoCompletedAt;
        public TutorialProgress tutorial;
        public ExpansionSave expansion;
        public OpeningProgress opening;
        public bool gradualOpening;
        public long dumpedTicks;
        public InventoryGridLayout carryGrid,warehouseGrid;
        public double seconds,accumulated,inputTimer,nextInput,woodProgress,dismantleProgress,nextWage,fuelRemaining,equivalentHeat,heatRate,nextAutosave;
        public int speed,manualStation,pendingBuildings,nextGround,nextTool,geometry,woodToken,dismantleToken,woodTool,screwTool,wrenchTool,dismantleObject,nextDelivery,nextMetal;
        public int recipe,activeRecipe,device,pendingRecipe=-1,pendingDevice=-1,fuelWood,fuelChips,woodBatches,dismantleBatches,deliveredTrips;
        public bool paused,bench,table,dismantle,generator,powered,runWood,runDismantle,woodActive,dismantleActive,dismantleUsable;
        public double playerX,playerY;public long cash,totalSold,wagesPaid,spawned;
        public string saleRemainder,slot;public int autoIndex;
        public int[] discovered;public double[] prices,budgets,livingBudgets;public bool[] soldTechnologies,soldSales;public string[] surveyedVeins;
        public List<GroundSave> ground;public List<ToolSave> tools;public List<ResidentSave> residents;public List<BillSave> bills;
        public List<BlueprintSave> blueprints;public List<string> inventoryKeys;public List<InventorySave> inventories;
    }
    internal sealed class SerializableRandom
    {
        public uint State;public SerializableRandom(int seed){State=(uint)seed;if(State==0)State=1;}
        public double NextDouble(){uint value=State;value^=value<<13;value^=value>>17;value^=value<<5;State=value;return value/4294967296.0;}
        public int Next(int maximum)=>(int)(NextDouble()*maximum);
    }
    public sealed partial class PrototypeSimulation
    {
        public double NextAutosave {get;set;}=300;
        public SimulationSnapshot Capture()
        {
            if(Interacting)throw new InvalidOperationException("请在当前0.5秒交互完成后存档。");
            if(Hand!=null)throw new InvalidOperationException("请先放下手中物品，或关闭物品栏后保存。");
            SyncInventoryGrids();
            var map=InventoryMap();
            return new SimulationSnapshot{
                conveyorDelivered=ConveyorDeliveredItems,
                autoMachineStaffing=AutomaticMachineStaffing,nextMachineStaffing=nextMachineStaffing,conveyors=conveyors.Select(b=>b.Copy()).ToList(),ports=ports.Select(p=>p.Copy()).ToList(),infrastructurePlans=infrastructurePlans.Select(p=>p.Copy()).ToList(),nextConveyor=nextConveyorId,nextPort=nextPortId,nextInfrastructurePlan=nextInfrastructurePlan,beltCredit=beltCredit,portTurn=portTurn,
                facilities=facilities.Select(f=>f.Copy()).ToList(),wires=wires.Select(w=>w.Copy()).ToList(),selectedFacilities=(int[])selectedFacilities.Clone(),nextFacility=nextFacilityId,manualFacility=manualFacilityId,wireCredit=wireCredit,wirePlans=wirePlans.Select(p=>p.Copy()).ToList(),nextWirePlan=nextWirePlan,
                itemCount=DesignCatalog.ItemCount,itemIds=(string[])DesignCatalog.ItemKeys.Clone(),totalRecruited=TotalRecruited,settings=Settings,random=random.State,
                expansion=new ExpansionSave{ownedRegion=OwnedRegion,unrecognizedOther=UnrecognizedOtherPlastic,liftStage=LiftStage,bonusLevel=BonusLevel,processBatches=ProcessBatchesCompleted,
                    regionBudgets=(double[])regionBudgets.Clone(),regionPending=(int[])regionPending.Clone(),invasionAt=InvasionAt,happinessDeadline=HappinessRiotDeadline,bonusUntil=BonusUntil,
                    heroClothes=HeroClothesSeconds,soapUnits=SoapUnits,nextCleaning=nextCleaningPeriod,heroClothesRetry=heroClothesRetry,ended=Ended,heroAboard=HeroAboard,happinessRiot=HappinessRiot,
                    promise=PromiseMade,rest=RestService,autoToolMaking=AutomaticToolMaking,communityClean=CommunityClean,endingKind=EndingKind,endingText=EndingText,landfilled=LandfilledTicks,bonusPaid=BonusPaidCents,rent=PlayerRentCents},
                tutorial=Tutorial.Copy(),opening=Opening?.Copy(),gradualOpening=Opening!=null,dumpedTicks=DumpedTicks,carryGrid=CarryGrid.Copy(),warehouseGrid=WarehouseGrid.Copy(),
                community=new CommunitySave{player=PlayerNeeds.Copy(),foodPolicy=(int)FoodPolicy,waterPolicy=(int)WaterPolicy,meals=MealsConsumed,water=WaterConsumed,turn=livingTurn,external=ExternalSupply,emergency=EmergencyWater,delivery=AutomaticLivingSupply,failed=Failed,riot=WageRiot,failure=FailureReason,community=CommunityCashCents,spent=ExternalLivingSpentCents,retail=RetailIncomeCents,riotTarget=RiotTargetCents,riotPaid=RiotPaidCents,firstPayroll=firstPayrollAt,nextPeriod=nextLivingPeriod,riotDeadline=RiotDeadline},
                workshop=new WorkshopSave{recipe=(int)SelectedAssembly,token=assemblyToken,assembled=AssembledTools,boughtMask=toolsBoughtMask,progress=assemblyProgress,purchaseAt=toolPurchaseAt,budgetReset=toolBudgetReset,budgetSpent=toolBudgetSpent,autoPurchase=AutomaticToolPurchase},demoCompleted=DemoCompleted,demoCompletedAt=DemoCompletedAt,
                seconds=GameSeconds,accumulated=accumulatedTime,inputTimer=inputTimer,nextInput=nextInput,woodProgress=jobProgress,dismantleProgress=dismantleProgress,nextWage=nextWage,
                fuelRemaining=fuelRemaining,equivalentHeat=equivalentHeat,heatRate=activeFuelHeatRate,nextAutosave=NextAutosave,
                speed=Speed,manualStation=manualStation,pendingBuildings=0,nextGround=nextGroundId,nextTool=nextToolId,geometry=geometryRevision,
                woodToken=woodToken,dismantleToken=dismantleToken,woodTool=woodToolId,screwTool=screwToolId,wrenchTool=wrenchToolId,dismantleObject=dismantleObjectId,nextDelivery=nextDelivery,nextMetal=nextMetalItem,
                recipe=(int)SelectedRecipe,activeRecipe=(int)activeRecipe,device=(int)SelectedDevice,pendingRecipe=pendingRecipe==null?-1:(int)pendingRecipe.Value,pendingDevice=pendingDevice==null?-1:(int)pendingDevice.Value,fuelWood=fuelWood,fuelChips=fuelChips,woodBatches=CompletedBatches,dismantleBatches=DismantledBatches,deliveredTrips=DeliveredTrips,
                paused=Paused,bench=BenchBuilt,table=TableBuilt,dismantle=DismantleBuilt,generator=GeneratorBuilt,powered=HasBeenPowered,runWood=RunRequested,runDismantle=DismantleRequested,
                woodActive=jobActive,dismantleActive=dismantleActive,dismantleUsable=dismantleUsable,playerX=PlayerX,playerY=PlayerY,cash=cashCents,totalSold=TotalSold,wagesPaid=TotalWagesPaidCents,spawned=SpawnedUnits,
                saleRemainder=saleRemainderCents.ToString(CultureInfo.InvariantCulture),discovered=discovered.Select(i=>(int)i).ToArray(),prices=(double[])salesPressure.Clone(),budgets=(double[])categoryBudgets.Clone(),livingBudgets=(double[])livingBudgets.Clone(),soldTechnologies=LegacySoldTechnologies(),soldSales=(bool[])soldSales.Clone(),surveyedVeins=surveyedVeins.ToArray(),
                ground=ground.Select(p=>new GroundSave{id=p.Id,x=p.X,y=p.Y,item=(int)p.Item,amount=p.Amount,reserved=p.Reserved,width=p.Width,instance=p.ToolId,usable=p.Usable}).ToList(),
                tools=tools.Select(t=>new ToolSave{id=t.Id,item=(int)t.Item,durability=t.Durability,reserved=t.ReservedBy,usingAt=t.UsingAt,location=t.Location,usable=t.Usable,condition=t.Condition}).ToList(),
                residents=residents.Select(r=>new ResidentSave{mood=r.Mood.Copy(),happiness=r.Happiness,id=r.Id,job=(int)r.Job,facilityId=r.FacilityId,x=r.X,y=r.Y,cash=r.CashCents,needs=r.Needs.Copy(),cargo=r.Cargo.Capture(),path=r.Path==null?null:(int[])r.Path.Clone(),pathIndex=r.PathIndex,pathX=r.PathTargetX,pathY=r.PathTargetY,pathRevision=r.PathRevision,delivery=r.Delivery==null?null:new DeliverySave{item=(int)r.Delivery.Item,amount=r.Delivery.Amount,token=r.Delivery.Token,stage=r.Delivery.Stage,id=r.Delivery.Id,sx=r.Delivery.SourceX,sy=r.Delivery.SourceY,tx=r.Delivery.TargetX,ty=r.Delivery.TargetY,groundId=r.Delivery.Ground?.Id??0,from=r.Delivery.From,to=r.Delivery.To,progress=r.Delivery.Progress,retryAt=r.Delivery.RetryAt,picked=r.Delivery.Picked}}).ToList(),
                bills=bills.Select(b=>new BillSave{ids=(int[])b.ResidentIds.Clone(),unpaid=(long[])b.UnpaidCents.Clone(),original=b.OriginalCents,due=b.DueAt}).ToList(),
                blueprints=blueprints.SelectMany(b=>b.Value.Select(t=>new BlueprintSave{flag=b.Key,inventory=map.First(p=>ReferenceEquals(p.Value,t.inventory)).Key,token=t.token})).ToList(),
                inventoryKeys=map.Keys.ToList(),inventories=map.Values.Select(i=>i.Capture()).ToList()
            };
        }
        // Older saves stop at the 59-item runtime slice; appended catalog items start empty.
        private static void MigrateItemCatalog(SimulationSnapshot s)
        {
            if(s.itemCount==DesignCatalog.ItemCount)return;
            void Pad(InventorySave i){if(i?.counts!=null&&i.counts.Length<DesignCatalog.ItemCount){var counts=new int[DesignCatalog.ItemCount];Array.Copy(i.counts,counts,i.counts.Length);i.counts=counts;}}
            if(s.inventories!=null)foreach(var i in s.inventories)Pad(i);
            if(s.residents!=null)foreach(var r in s.residents)Pad(r.cargo);
            s.itemCount=DesignCatalog.ItemCount;s.itemIds=(string[])DesignCatalog.ItemKeys.Clone();
        }
        private void RestoreExpansion(SimulationSnapshot s)
        {
            var e=s.schema>=6&&s.expansion!=null?s.expansion:new ExpansionSave{nextCleaning=s.seconds+600};
            if(e.ownedRegion<1||e.ownedRegion>DesignCatalog.Regions.Length)throw new ArgumentException("存档地区无效。");
            OwnedRegion=e.ownedRegion;UnrecognizedOtherPlastic=e.unrecognizedOther;LiftStage=e.liftStage;BonusLevel=e.bonusLevel;ProcessBatchesCompleted=e.processBatches;
            if(e.regionBudgets!=null&&e.regionBudgets.Length==regionBudgets.Length)Array.Copy(e.regionBudgets,regionBudgets,regionBudgets.Length);
            if(e.regionPending!=null&&e.regionPending.Length==regionPending.Length)Array.Copy(e.regionPending,regionPending,regionPending.Length);
            InvasionAt=e.invasionAt;HappinessRiotDeadline=e.happinessDeadline;BonusUntil=e.bonusUntil;HeroClothesSeconds=e.heroClothes;SoapUnits=e.soapUnits;nextCleaningPeriod=e.nextCleaning;heroClothesRetry=e.heroClothesRetry;
            Ended=e.ended;HeroAboard=e.heroAboard;HappinessRiot=e.happinessRiot;PromiseMade=e.promise;RestService=e.rest;AutomaticToolMaking=e.autoToolMaking;CommunityClean=e.communityClean;
            EndingKind=e.endingKind??"";EndingText=e.endingText??"";LandfilledTicks=e.landfilled;BonusPaidCents=e.bonusPaid;PlayerRentCents=e.rent;
            if(Settings.mapWidth<(OwnedRegion>=3?86:OwnedRegion>=2?52:32)||OwnedRegion>=3&&Settings.mapHeight<32)
            {var grown=Settings.Copy();grown.mapWidth=Math.Max(grown.mapWidth,OwnedRegion>=3?86:52);if(OwnedRegion>=3)grown.mapHeight=Math.Max(grown.mapHeight,32);Settings=grown;}
            UpdateWarehouseCapacity();
        }
        public static PrototypeSimulation Restore(SimulationSnapshot s)
        {
            if(s==null||s.schema<2||s.schema>SimulationSnapshot.CurrentSchema||(s.sourceVersion!=5&&s.sourceVersion!=11)||s.itemCount<=0||s.itemCount>DesignCatalog.ItemCount)throw new ArgumentException("存档版本或物品目录不兼容。");
            if(s.schema>=4&&s.itemIds!=null&&s.itemIds.Length>0&&!s.itemIds.SequenceEqual(DesignCatalog.ItemKeys.Take(s.itemIds.Length)))throw new ArgumentException("存档物品稳定ID目录不兼容。");
            MigrateItemCatalog(s);
            var m=new PrototypeSimulation(s.settings,1);m.RestoreConstruction(s);m.random.State=s.random;
            m.Opening=s.gradualOpening?(s.opening?.Copy()??throw new ArgumentException("存档缺少来料进度。")):null;m.DumpedTicks=s.dumpedTicks;
            m.CarryGrid=s.carryGrid?.Copy()??new InventoryGridLayout();m.WarehouseGrid=s.warehouseGrid?.Copy()??new InventoryGridLayout();
            if(m.DumpedTicks<0 || m.Opening!=null&&(m.Opening.batch<0||m.Opening.batch>m.openingShipments.Count||double.IsNaN(m.Opening.nextAt)||double.IsInfinity(m.Opening.nextAt)||m.Opening.nextAt<0))throw new ArgumentException("来料进度无效。");
            m.Tutorial=s.tutorial?.Copy()??new TutorialProgress{active=false,started=true};
            if(m.Tutorial.step<0 || m.Tutorial.step>TutorialProgress.LastStep || m.Tutorial.orientation<0 || m.Tutorial.orientation>5)throw new ArgumentException("教程进度无效。");
            if(s.schema>=3)
            {
                var c=s.community??throw new ArgumentException("存档缺少生活状态。");var w=s.workshop??throw new ArgumentException("存档缺少工作台状态。");
                m.PlayerNeeds=c.player.Copy();m.FoodPolicy=(SupplyPolicy)c.foodPolicy;m.WaterPolicy=(SupplyPolicy)c.waterPolicy;m.MealsConsumed=c.meals;m.WaterConsumed=c.water;m.livingTurn=c.turn;m.ExternalSupply=c.external;m.EmergencyWater=c.emergency;m.AutomaticLivingSupply=c.delivery;
                m.Failed=c.failed;m.FailureReason=c.failure;m.WageRiot=c.riot;m.CommunityCashCents=c.community;m.ExternalLivingSpentCents=c.spent;m.RetailIncomeCents=c.retail;m.RiotTargetCents=c.riotTarget;m.RiotPaidCents=c.riotPaid;m.firstPayrollAt=c.firstPayroll;m.nextLivingPeriod=c.nextPeriod;m.RiotDeadline=c.riotDeadline;
                m.SelectedAssembly=(AssemblyRecipe)w.recipe;m.assemblyToken=w.token;m.AssembledTools=w.assembled;m.toolsBoughtMask=w.boughtMask;m.assemblyProgress=w.progress;m.toolPurchaseAt=w.purchaseAt;m.toolBudgetReset=w.budgetReset;m.toolBudgetSpent=w.budgetSpent;m.AutomaticToolPurchase=w.autoPurchase;m.DemoCompleted=s.demoCompleted;m.DemoCompletedAt=s.demoCompletedAt;
            }
            else
            {
                // Existing S5 saves gain needs from load time, without retroactive consumption or death.
                m.PlayerNeeds=new LivingNeeds{nextMeal=s.seconds+300};m.nextLivingPeriod=s.seconds+600;
                m.firstPayrollAt=s.residents.Count==0?double.MaxValue:s.nextWage;
            }
            m.GameSeconds=s.seconds;m.accumulatedTime=s.accumulated;m.inputTimer=s.inputTimer;m.nextInput=s.nextInput;m.jobProgress=s.woodProgress;m.dismantleProgress=s.dismantleProgress;m.nextWage=s.nextWage;
            m.fuelRemaining=s.fuelRemaining;m.equivalentHeat=s.equivalentHeat;m.activeFuelHeatRate=s.heatRate;m.NextAutosave=s.nextAutosave;
            m.Speed=s.speed;m.manualStation=s.manualStation;m.nextGroundId=s.nextGround;m.nextToolId=s.nextTool;m.geometryRevision=s.geometry;
            m.woodToken=s.woodToken;m.dismantleToken=s.dismantleToken;m.woodToolId=s.woodTool;m.screwToolId=s.screwTool;m.wrenchToolId=s.wrenchTool;m.dismantleObjectId=s.dismantleObject;m.nextDelivery=s.nextDelivery;m.nextMetalItem=s.nextMetal;
            m.SelectedRecipe=(WoodRecipe)s.recipe;m.activeRecipe=(WoodRecipe)s.activeRecipe;m.SelectedDevice=(ItemId)s.device;m.fuelWood=s.fuelWood;m.fuelChips=s.fuelChips;
            m.pendingRecipe=s.pendingRecipe<0?(WoodRecipe?)null:(WoodRecipe)s.pendingRecipe;m.pendingDevice=s.pendingDevice<0?(ItemId?)null:(ItemId)s.pendingDevice;
            m.CompletedBatches=s.woodBatches;m.DismantledBatches=s.dismantleBatches;m.DeliveredTrips=s.deliveredTrips;
            m.Paused=s.paused;m.BenchBuilt=s.bench;m.TableBuilt=s.table;m.DismantleBuilt=s.dismantle;m.GeneratorBuilt=s.generator;m.HasBeenPowered=s.powered;m.RunRequested=s.runWood;m.DismantleRequested=s.runDismantle;
            m.jobActive=s.woodActive;m.dismantleActive=s.dismantleActive;m.dismantleUsable=s.dismantleUsable;m.PlayerX=s.playerX;m.PlayerY=s.playerY;m.cashCents=s.cash;m.TotalSold=s.totalSold;m.TotalWagesPaidCents=s.wagesPaid;m.SpawnedUnits=s.spawned;
            m.saleRemainderCents=decimal.Parse(s.saleRemainder,CultureInfo.InvariantCulture);m.discovered.Clear();foreach(int item in s.discovered)m.discovered.Add((ItemId)item);
            Array.Copy(s.prices,m.salesPressure,s.prices.Length);Array.Copy(s.budgets,m.categoryBudgets,5);Array.Copy(s.livingBudgets,m.livingBudgets,3);
            m.RestoreSales(s);
            var map=m.InventoryMap();for(int i=0;i<s.inventoryKeys.Count;i++)map[s.inventoryKeys[i]].Restore(s.inventories[i]);
            m.ground.Clear();m.groundCapacity.Clear();foreach(var p in s.ground){var pile=new GroundPile(p.x,p.y,(ItemId)p.item,p.amount){Id=p.id,Reserved=p.reserved,Width=p.width,ToolId=p.instance,Usable=p.usable};m.ground.Add(pile);for(int x=p.x;x<p.x+p.width;x++)m.groundCapacity[(x,p.y)]=m.CellTicks(x,p.y)+p.amount*ItemCatalog.CapacityTicks((ItemId)p.item)/p.width;}
            m.tools.Clear();foreach(var t in s.tools)m.tools.Add(new ToolInstance{Id=t.id,Item=(ItemId)t.item,Durability=t.durability,ReservedBy=t.reserved,UsingAt=t.usingAt,Location=t.location,Usable=t.usable,Condition=t.condition});
            m.residents.Clear();foreach(var r in s.residents)
            {
                var mood=s.schema>=6&&r.mood?.since!=null&&r.mood.since.Length==5&&r.mood.loss?.Length==5&&r.mood.recovery?.Length==5?r.mood.Copy():new HappinessState();
                var person=new Resident(r.id){Mood=mood,Happiness=s.schema>=6?r.happiness:70,Job=(WorkerJob)r.job,FacilityId=r.facilityId==0&&r.job==(int)WorkerJob.Woodwork?1:r.facilityId,X=r.x,Y=r.y,CashCents=r.cash,Needs=s.schema>=3?r.needs.Copy():new LivingNeeds{nextMeal=s.seconds+300},Path=r.path,PathIndex=r.pathIndex,PathTargetX=r.pathX,PathTargetY=r.pathY,PathRevision=r.pathRevision};person.Cargo.Restore(r.cargo);
                // Unity may deserialize a null nested record as an empty object. Real deliveries
                // always have a positive identity; an idle resident must remain idle after loading.
                if(r.delivery!=null&&r.delivery.id>0){var d=r.delivery;person.Delivery=new DeliveryTask{Id=d.id,Item=(ItemId)d.item,Amount=d.amount,Token=d.token,Stage=d.stage,SourceX=d.sx,SourceY=d.sy,TargetX=d.tx,TargetY=d.ty,From=d.from,To=d.to,Progress=d.progress,RetryAt=d.retryAt,Picked=d.picked,Ground=m.ground.Find(p=>p.Id==d.groundId),Source=map.ContainsKey(d.from)?map[d.from]:null,Target=map.ContainsKey(d.to)?map[d.to]:null};}
                m.residents.Add(person);
            }
            m.bills.Clear();foreach(var b in s.bills){var bill=new WageBill(b.ids,b.original/b.ids.Length,b.due);Array.Copy(b.unpaid,bill.UnpaidCents,b.unpaid.Length);m.bills.Add(bill);}
            m.TotalRecruited=Math.Max(s.totalRecruited,Math.Max(m.residents.Select(r=>r.Id).DefaultIfEmpty(0).Max(),m.bills.SelectMany(b=>b.ResidentIds).DefaultIfEmpty(0).Max()));
            m.blueprints.Clear();foreach(var b in s.blueprints){if(!m.blueprints.ContainsKey(b.flag))m.blueprints[b.flag]=new List<(UnitInventory,int)>();m.blueprints[b.flag].Add((map[b.inventory],b.token));}
            m.RestoreExpansion(s);
            m.manualFacilityId=s.schema>=4?s.manualFacility:(m.manualStation==0?0:m.Station(m.manualStation==3?4:m.manualStation).Id);m.RestoreBlueprintFlags(s);m.ValidateAutomationCargo();m.RecomputePower();m.RecoverBlockedActors();m.GroundRevision++;m.RefreshState();return m;
        }
    }
}
