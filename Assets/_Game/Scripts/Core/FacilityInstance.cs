using System;
using System.Collections.Generic;
using System.Linq;
namespace CrazyRubbish.Core
{
    [Serializable] public sealed class FacilityInstance
    {
        public int Id,Kind,X,Y,Rotation; public bool Built,Blueprint; public double PowerRatio,PowerRequested;
        public FacilityInstance Copy() => (FacilityInstance)MemberwiseClone();
        public double jobProgress;
        public bool jobActive;
        public WoodRecipe activeRecipe;
        public int woodToken;
        public int woodToolId;
        public int pendingRecipeValue=-1;
        public WoodRecipe? pendingRecipe {get=>pendingRecipeValue<0?(WoodRecipe?)null:(WoodRecipe)pendingRecipeValue;set=>pendingRecipeValue=value.HasValue?(int)value.Value:-1;}
        public bool RunRequested;
        public WoodRecipe SelectedRecipe;
        public WorkState State;
        public double dismantleProgress;
        public bool dismantleActive;
        public int dismantleToken;
        public int screwToolId;
        public int wrenchToolId;
        public int dismantleObjectId;
        public bool dismantleUsable=true;
        public int pendingDeviceValue=-1;
        public ItemId? pendingDevice {get=>pendingDeviceValue<0?(ItemId?)null:(ItemId)pendingDeviceValue;set=>pendingDeviceValue=value.HasValue?(int)value.Value:-1;}
        public bool DismantleRequested;
        public ItemId SelectedDevice=ItemId.OldCutter;
        public WorkState DismantleState;
        public AssemblyRecipe SelectedAssembly;
        public int assemblyToken;
        public double assemblyProgress;
        public int fuelWood;
        public int fuelChips;
        public double fuelRemaining;
        public double equivalentHeat;
        public double activeFuelHeatRate;
        public bool MachineActive,MachineTripped;
        public bool WasConstructed,SuppressAutoOperator,SuppressAutoMaintenance;
        public int MachineToken,MachinePhase,MachineBatches;
        public double HumanProgress,MachineProgress,Durability=100,RepairedDurability;
        [NonSerialized] public double StepPowerRatio,StepPoweredFraction;
        [NonSerialized] public UnitInventory WoodInputStock;
        [NonSerialized] public UnitInventory WoodTools;
        [NonSerialized] public UnitInventory Output;
        [NonSerialized] public UnitInventory DismantleInput;
        [NonSerialized] public UnitInventory DismantleTools;
        [NonSerialized] public UnitInventory DismantleOutput;
        [NonSerialized] public UnitInventory TableInput;
        [NonSerialized] public UnitInventory TableOutput;
        [NonSerialized] public UnitInventory GeneratorResidue;
        public void InitializeStocks(int outputCapacity=8)
        {
            WoodInputStock=new UnitInventory(8);
            WoodTools=new UnitInventory(2);
            Output=new UnitInventory(outputCapacity);
            DismantleInput=new UnitInventory(12);
            DismantleTools=new UnitInventory(4);
            DismantleOutput=new UnitInventory(11);
            TableInput=new UnitInventory(8);
            TableOutput=new UnitInventory(4);
            GeneratorResidue=new UnitInventory(10);
        }
    }
    public sealed partial class PrototypeSimulation
    {
        private double jobProgress { get=>Station(1).jobProgress; set=>Station(1).jobProgress=value; }
        private bool jobActive { get=>Station(1).jobActive; set=>Station(1).jobActive=value; }
        private WoodRecipe activeRecipe { get=>Station(1).activeRecipe; set=>Station(1).activeRecipe=value; }
        private int woodToken { get=>Station(1).woodToken; set=>Station(1).woodToken=value; }
        private int woodToolId { get=>Station(1).woodToolId; set=>Station(1).woodToolId=value; }
        private WoodRecipe? pendingRecipe { get=>Station(1).pendingRecipe; set=>Station(1).pendingRecipe=value; }
        public bool RunRequested { get=>Station(1).RunRequested; private set=>Station(1).RunRequested=value; }
        public WoodRecipe SelectedRecipe { get=>Station(1).SelectedRecipe; private set=>Station(1).SelectedRecipe=value; }
        public WorkState State { get=>Station(1).State; private set=>Station(1).State=value; }
        private double dismantleProgress { get=>Station(2).dismantleProgress; set=>Station(2).dismantleProgress=value; }
        private bool dismantleActive { get=>Station(2).dismantleActive; set=>Station(2).dismantleActive=value; }
        private int dismantleToken { get=>Station(2).dismantleToken; set=>Station(2).dismantleToken=value; }
        private int screwToolId { get=>Station(2).screwToolId; set=>Station(2).screwToolId=value; }
        private int wrenchToolId { get=>Station(2).wrenchToolId; set=>Station(2).wrenchToolId=value; }
        private int dismantleObjectId { get=>Station(2).dismantleObjectId; set=>Station(2).dismantleObjectId=value; }
        private bool dismantleUsable { get=>Station(2).dismantleUsable; set=>Station(2).dismantleUsable=value; }
        private ItemId? pendingDevice { get=>Station(2).pendingDevice; set=>Station(2).pendingDevice=value; }
        public bool DismantleRequested { get=>Station(2).DismantleRequested; private set=>Station(2).DismantleRequested=value; }
        public ItemId SelectedDevice { get=>Station(2).SelectedDevice; private set=>Station(2).SelectedDevice=value; }
        public WorkState DismantleState { get=>Station(2).DismantleState; private set=>Station(2).DismantleState=value; }
        public AssemblyRecipe SelectedAssembly { get=>Station(4).SelectedAssembly; private set=>Station(4).SelectedAssembly=value; }
        private int assemblyToken { get=>Station(4).assemblyToken; set=>Station(4).assemblyToken=value; }
        private double assemblyProgress { get=>Station(4).assemblyProgress; set=>Station(4).assemblyProgress=value; }
        private int fuelWood { get=>Station(8).fuelWood; set=>Station(8).fuelWood=value; }
        private int fuelChips { get=>Station(8).fuelChips; set=>Station(8).fuelChips=value; }
        private double fuelRemaining { get=>Station(8).fuelRemaining; set=>Station(8).fuelRemaining=value; }
        private double equivalentHeat { get=>Station(8).equivalentHeat; set=>Station(8).equivalentHeat=value; }
        private double activeFuelHeatRate { get=>Station(8).activeFuelHeatRate; set=>Station(8).activeFuelHeatRate=value; }
        public UnitInventory WoodInputStock => Station(1).WoodInputStock;
        public UnitInventory WoodTools => Station(1).WoodTools;
        public UnitInventory Output => Station(1).Output;
        public UnitInventory DismantleInput => Station(2).DismantleInput;
        public UnitInventory DismantleTools => Station(2).DismantleTools;
        public UnitInventory DismantleOutput => Station(2).DismantleOutput;
        public UnitInventory TableInput => Station(4).TableInput;
        public UnitInventory TableOutput => Station(4).TableOutput;
        public UnitInventory GeneratorResidue => Station(8).GeneratorResidue;
        public int BenchFrontX => ServiceCell(Station(1)).x;
        public int BenchFrontY => ServiceCell(Station(1)).y;
        public int DismantleFrontX => ServiceCell(Station(2)).x;
        public int DismantleFrontY => ServiceCell(Station(2)).y;
        public int TableFrontX => ServiceCell(Station(4)).x;
        public int TableFrontY => ServiceCell(Station(4)).y;
        public int GeneratorFrontX => ServiceCell(Station(8)).x;
        public int GeneratorFrontY => ServiceCell(Station(8)).y;
    }
}
