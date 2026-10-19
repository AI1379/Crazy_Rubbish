using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    // Landfill, the dumping threshold with its delayed invasion, and the three main endings.
    public sealed partial class PrototypeSimulation
    {
        public const long DumpThresholdTicks=2400*20;
        public const double InvasionDelay=600;
        public double InvasionAt {get;private set;}=-1;
        public bool Ended {get;private set;}
        public string EndingKind {get;private set;}=""; // good / normal / invasion
        public string EndingText {get;private set;}="";
        public int LiftStage {get;private set;} // 0 none, 1 assembled, 2 tested, 3 arrived
        public bool HeroAboard {get;private set;}
        public long LandfilledTicks {get;private set;}
        public double InvasionSeconds=>InvasionAt<0?-1:Math.Max(0,InvasionAt-GameSeconds);

        public static bool DisposalAccepted(ItemId item)
        {
            string category=DesignCatalog.ItemCategories[(int)item],key=DesignCatalog.ItemKeys[(int)item];
            if(category!="非普通交易物"&&category!="损坏工具"&&category!="压缩包")return false;
            return !key.EndsWith("_ore")&&!key.EndsWith("_concentrate")&&key!="clean_clay"&&key!="reclaimed_structure"&&!key.StartsWith("lift_")&&key!="industrial_effluent";
        }
        // Packages are judged by their uncompressed contents.
        public static int DisposalTicks(ItemId item)=>ItemCatalog.IsPackage(item)?DesignCatalog.PackDisposalTicks[(int)item]:ItemCatalog.CapacityTicks(item);
        private void RecordDump(long ticks)
        {
            DumpedTicks+=ticks;
            if(InvasionAt<0&&DumpedTicks>=DumpThresholdTicks)InvasionAt=GameSeconds+InvasionDelay;
        }
        private void TickLandfill(FacilityInstance f,double seconds)
        {
            if(f.Sealed||!f.ProcessRun||RiotActive)return;
            var item=f.ProcessInput.Stocked().FirstOrDefault(i=>f.ProcessInput.Available(i)>0);
            if(f.ProcessInput.UsedTicks==0)return;
            double rate=CrewEfficiency(f,false,1,out int present);if(present==0)return;
            int ticks=DisposalTicks(item);
            double capacity=Definition(f.Kind).Capacity*20;
            if(f.LandfillTicks+ticks>capacity+1e-6){f.Sealed=true;f.ProcessRun=false;return;}
            // 10 human-seconds per 10 capacity units, i.e. one second per capacity unit buried.
            f.ProcessProgress+=seconds*rate;
            if(f.ProcessProgress+1e-6<ticks/20.0)return;
            f.ProcessProgress=0;f.ProcessInput.TryRemove(item,1);
            var instance=tools.FirstOrDefault(t=>t.Item==item&&t.Location==ProcessInputKey(f.Id)&&t.ReservedBy==0);if(instance!=null)tools.Remove(instance);
            f.LandfillTicks+=ticks;LandfilledTicks+=ticks;
            if(f.LandfillTicks>=capacity-1e-6){f.Sealed=true;f.ProcessRun=false;}
        }
        private void TickEndings()
        {
            if(Failed||Ended)return;
            if(InvasionAt>=0&&GameSeconds+1e-6>=InvasionAt)
            {EndingKind="invasion";EndGame("下层利用难处理的垃圾发展起来并发动入侵，中层工厂熄灭，主角失败。");}
        }
        private void ReachUpperLayer()
        {
            if(Failed)return;
            Ended=true;Paused=true;CancelInteraction();manualStation=0;
            if(DumpedTicks==0)
            {EndingKind="good";EndingText="主角回到上层。中层的回收产线仍在运转，没有把垃圾向下层转嫁——这是零倾倒的好结局。";}
            else
            {EndingKind="normal";EndingText="主角回到上层，镜头停在中层向下的洞口。下层是否安全，没有人知道。";}
        }
    }
}
