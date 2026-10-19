using System;
using System.Collections.Generic;

namespace CrazyRubbish.Core
{
    [Serializable] public sealed class PlanGround { public int id,x,y,width=1;public PlanStock stock=new PlanStock(); }
    [Serializable] public sealed class PlanTerrainClearance {public int x,y,width=1,height=1;}
    [Serializable] public sealed class PlanLoss { public string kind;public double since=-1,loss,recovery; }
    [Serializable] public sealed class PlanPerson
    {
        public bool directed;public int commandX,commandY;
        public int id,assigned,manualAssignment,maintaining;public double x,y,nextMeal,nextRetry,nextServices,nextSeek,clothes=4800,foodSince=-1,waterSince=-1,foodRecovery,waterRecovery,lowSince=-1,restUntil;
        public double foodDebt,waterDebt,rescueWater;public long wallet=500;public double bonus,happiness=70,foodRecoveryRate,waterRecoveryRate;public bool dead,soapDue,clothesReturned;
        public PlanStock cargo=new PlanStock{capacity=8};public PlanTrip trip;public List<PlanLoss> losses=new List<PlanLoss>();public string activity="空闲";
    }
    [Serializable] public sealed class PlanTrip { public string source,target,item;public long ticks;public int stage,instance=-1;public double elapsed,retryAt;public bool picked; }
    [Serializable] public sealed class PlanSite
    {
        public int id,x,y,rotation,priority=3,maintainer,springRegion,springBody,lampBed,linkSite,direction,portSite,mergeTurn;
        public string type,recipe,pendingRecipe,status="待安排",portMode="input",filter;
        public bool enabled=true,autoTools=true,captureEffluent,pending,community,active,stageCommitted,sourceDrawn,permanent,stopped,crossing;
        public double durability=100,progress,sourceReserved,powerRatio,fuelSeconds,activeFuelRate,ashBudget,landfilled,thermalStartup,coolantTime;
        public int stage,batchId;public PlanStock input=new PlanStock(),output=new PlanStock(),reserved=new PlanStock(),tools=new PlanStock(),addons=new PlanStock(),builtMaterials=new PlanStock();
        public int cabinId;public bool cabinTested;
        public List<int> operators=new List<int>();public double beltProgress;
    }
    [Serializable] public sealed class PlanSpringState {public int region,site,body;public string kind;public double remaining=12,reserved;}
    [Serializable] public sealed class PlanBill {public int[] ids;public long[] unpaid;public long original;public double at;}
    [Serializable] public sealed class PlanMarket {public string group;public double pressure;}
    [Serializable] public sealed class PlanCleaning {public int group;public double until,nextAttempt;}
    [Serializable] public sealed class PlanBudget {public int region;public string category,pendingItem;public double capacity,integrated;}
    [Serializable] public sealed class PlanRiot {public bool active,wages,promise;public string repair="power";public double deadline;public long target,paid;public List<int> organizers=new List<int>();}
    [Serializable] public sealed class PlanState
    {
        public int schema=1,region=1,mapRevision,nextId=1,totalHired,heroSite,speed=1,revision,tutorial;public string version,ending,hero="male";
        public bool paused=true,recognized,freeFood,freeWater,externalSupply=true,emergencyWater=true,autoSupply;
        public long cash,communityCash,wagesPaid,supplySpent,retailIncome,externalSaleIncome,serviceIncome,personalSubsidyBudget,personalSubsidyUsed;public double seconds,accumulated,nextPayroll=-1,nextInput,inputIntegral,lastInput,nextAutosave=300,saleRemainder,dumped,invasionAt=-1;
        public double foodRetail=-1,waterRetail=-1,clothesRetail=-1,soapRetail=-1,retailDiscount=1;
        public int foodReserveCycles=2,waterReserveCycles=2;public long autoSupplyBudget=1000,autoSupplyUsed,toolBudget=2000,toolBudgetUsed;public bool autoTools,autoMakeTools=true;
        public double rent=1,firstPayroll=-1;public uint rng=4271;
        public PlanPerson player=new PlanPerson{id=0,x=9,y=4,wallet=0,nextMeal=300,nextServices=600};
        public PlanStock backpack=new PlanStock{capacity=8},warehouse=new PlanStock{capacity=80};
        public List<PlanGround> ground=new List<PlanGround>();public List<PlanSite> sites=new List<PlanSite>();public List<PlanPerson> people=new List<PlanPerson>();
        public List<PlanTerrainClearance> terrainClearings=new List<PlanTerrainClearance>();
        public List<string> acquired=new List<string>(),technologies=new List<string>(),soldKnowledge=new List<string>();
        public List<PlanBill> bills=new List<PlanBill>();public List<PlanMarket> markets=new List<PlanMarket>();public List<PlanBudget> budgets=new List<PlanBudget>();
        public List<PlanSpringState> springs=new List<PlanSpringState>();public PlanRiot riot=new PlanRiot();
        public List<PlanCleaning> cleaning=new List<PlanCleaning>();public double autoBudgetDue=600;
        public PlanStock holeInput=new PlanStock{capacity=20},disposalHeld=new PlanStock();public string holeFilter;public int disposalSite=-1,disposalWorker;
        public double disposalProgress,disposalCapacity;public List<string> discarded=new List<string>();
        public List<string> surveyedVeins=new List<string>();public int aboardSite;public bool returnArrivalPending;public string endingCode;
    }
}
