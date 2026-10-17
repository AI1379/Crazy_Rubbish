using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        public bool DemoCompleted { get; private set; }
        public double DemoCompletedAt { get; private set; }
        public bool[] DemoMilestones => new[]{
            discovered.Contains(ItemId.WoodHandSaw)&&discovered.Contains(ItemId.WasteWood)&&discovered.Contains(ItemId.RecoveredFastener),
            BenchBuilt, CompletedBatches>=2, DismantleBuilt&&TableBuilt,
            HasBeenPowered&&residents.Count>=5, TotalWagesPaidCents>=5000&&UnpaidWageCents==0,
            AssembledTools>=1, cashCents>=20000&&MissingSupplyPeople==0&&UnpaidWageCents==0
        };
        public static readonly string[] DemoGoalNames={"拾取手锯、木料和紧固件","搭建木工地点","完成两批木板并搬运","建拆解地点和工作台","发电取得认可，招募5人","出售成品，付清首期工资","制作一把工具替补","生活无缺供、无欠薪，攒够200钱"};
        private void CheckDemoComplete()
        {
            if(!DemoCompleted&&DemoMilestones.All(done=>done)){DemoCompleted=true;DemoCompletedAt=GameSeconds;}
        }
        public string CurrentDemoHint
        {
            get
            {
                if(Failed)return FailureReason;
                if(RiotActive)return $"欠薪暴动：还需补发 {Math.Max(0,RiotTargetCents-RiotPaidCents)/100m:0.00} 钱，剩余 {RiotSeconds:0} 秒。";
                if(MissingSupplyPeople>0)return "有人缺少食物或水：人员页可免费供给，仓库需有实物；也可采购。";
                if(UnpaidWageCents>0)return $"工资到期：出售仓库木板，再去人员页支付 {UnpaidWages:0.00} 钱。";
                if(!BenchBuilt)return "Shift+E 拾整堆；仓库页可按物品选拾取。木料4 + 紧固件2搭建木工。";
                if(CompletedBatches<2)return "到木工正面，投木料后按 F；两批需木料8，成品需取出并入库。";
                if(!TableBuilt||!DismantleBuilt)return "工作台需木板4 + 钢铁零件2；拆解地点需木料4 + 紧固件2。";
                if(!HasBeenPowered)return GeneratorBuilt?ProductionDiagnosis(SelectedFacility(8)):"发电机需钢铁零件6、热动力/发电组件各1、电线2；建好后接线到住房或路灯，再投燃料。";
                if(residents.Count<5)return "人员页招募5人；到木工现场交接手锯，再指定居民木工。";
                if(WoodTools.Count(ItemId.WoodHandSaw)==0)return "把随身手锯交到木工工具槽；居民不能远程使用主角的工具。";
                if(TotalWagesPaidCents<5000)return "维持木工及配送，出售仓库木板。首人入职600秒后，人员页手动发薪。";
                if(AssembledTools==0)return "工作台制作替补：木工切木柄配方，买工作端；携带木柄1 + 锯片1到工作台。";
                if(DemoCompleted)return "首区 demo 已达成；可继续经营，保存进度，或从菜单重开。";
                return "目标200钱：拆解交易或出售已解锁技术可融资；技术会永久压低售价。";
            }
        }
    }
}
