using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    [Serializable]
    public sealed class TutorialProgress
    {
        public bool active = true, started;
        public int step;
        public const int LastStep = 13;
        public bool Completed => step == LastStep;
        public bool IntroOpen => active && !started;
        public TutorialProgress Copy() => (TutorialProgress)MemberwiseClone();
        public void Begin() { active = true; started = true; }
        public void Skip() { active = false; started = true; }
        public void Refresh(PrototypeSimulation m)
        {
            if (!active || !started) return;
            if (step == 0) step = 1;
            while (step < LastStep && PrototypeTutorial.Satisfied(m, step)) step++;
        }
    }

    public enum TutorialOperation
    {
        Wait, Pick, Deposit, Withdraw, BuildBench, FeedWood, WorkWood, TakeWood,
        BuildTable, BuildDismantle, FeedDevice, WorkDismantle, TakeDeviceOutput,
        BuildGenerator, Fuel, Recruit, HandSaw, AssignWorker, Sell, Pay,
        SelectHandle, SelectPlank, BuyBlade, FeedAssembly, WorkAssembly, Trade
    }

    public sealed class TutorialAction
    {
        public TutorialOperation operation;
        public string label, instruction, button;
        public ItemId item;
        public int amount = 1, x, y, page;
        public bool hasTarget;
        public Technology technology;
        public bool AtTarget(PrototypeSimulation m)
        {
            if (!hasTarget) return true;
            if (operation == TutorialOperation.WorkWood || operation == TutorialOperation.WorkDismantle || operation == TutorialOperation.WorkAssembly)
                return (int)Math.Floor(m.PlayerY+0.5)==y && ((int)Math.Floor(m.PlayerX+0.5)==x || (int)Math.Floor(m.PlayerX+0.5)==x+1);
            return PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,x,y)<=m.Settings.interactionRadius;
        }
    }

    public static class PrototypeTutorial
    {
        public static readonly string[] Titles = {
            "欢迎来到废料工坊", "拾取你的手锯", "先备好食物和饮水", "准备第一批建材",
            "搭建木工地点", "木料变成木板，再送进仓库", "建设工作台和拆解地点",
            "拆一台旧切割装置", "组装发电机并加燃料", "招募居民，交工具和分岗位",
            "出售产品，支付第一期工资", "制作一把备用手锯", "准备200钱扩区资金", "新手教程完成"
        };
        public static readonly string[] Lessons = {
            "跟着提示亲手操作，学会经营第一座工坊。\n\n左键查看设施；右键或WASD移动。E交互、Shift+E整堆拾取、Q入库、F木工。空格暂停，1/2/3倍速。\n\n按B/P/I/H打开建造、人员、仓库或教程，Esc先收起面板。首次准备页时间停止。",
            "手锯就在开局来料区。走近物品，按E拾取；物品多时可在仓库页指定种类。\n\n“前往”只负责走路，到达后还要点“拾取”。手锯占背包容量，入库时会保留随身手锯。",
            "食物12份、饮水24升先送进仓库。每300秒会实际吃喝，缺货要补。\n\n背包容量8，装满要分趟搬。到仓库取用点按Q入库；不是走到仓库图像中心。居民首期免费，之后可在人员页改供应政策。",
            "准备废木料16、紧固件4，放在仓库。木工和拆解各用木料4+紧固件2，另外木料8做两批木板。\n\nShift+E拾整堆，或按教程按钮指定物品。背包满了先入库，不能凭空跨过容量限制。",
            "点击地图上的木工台，或从建造页查看木工台，再点“搭建木工台”，扣木料4和紧固件2。建设可从背包和仓库取建材。\n\n“前往作业点”会走到设施正面；暂停时只预约蓝图，恢复后才建成。",
            "每批木料4→木板3+碎屑1，基础40秒。先去仓库取木料，再到木工正面投料、按F进入岗位，站着等。\n\n移动会退出岗位，进度保留。取成品后按Q入库；成品不会自动变成现金。目标是两批、仓库木板6。",
            "先取得钢铁零件8：其中2件和木板4建工作台，其余6件留给发电机。拆解地点再用木料4+紧固件2。\n\n这些初始物资就在地面。不要提前把建设要用的木板全部卖掉。",
            "把螺丝刀和旧切割装置带到拆解正面。投设备，再开拆解，35秒后取电机、机器锯片和钢铁零件，送仓库。\n\n机器锯片和手锯锯片用途不同。移动会退出人工岗位，已投入的原料仍留在现场。",
            "发电机需要钢铁零件6、热动力组件1、发电组件1、电线2。组件在地面，先拾取并入库。\n\n建好后到正面投入木料或碎屑；要实际发出电，才能得到居民认可。没有燃料会停机。",
            "在人员页逐个招募5人。首批免招募费，工资不免：每人每600秒10钱。\n\n把随身手锯交到木工工具槽，再指定居民木工。居民会实际走路搬运，不能使用主角远处的工具。",
            "居民生产后，等搬运工把木板送进仓库，再出售。第一人加入600秒后，人员页手动发薪，5人共50钱。\n\n可用3倍速等待，仍要看生活库存和仓库空间。现金不够可部分支付；免费食物不抵工资。",
            "木工切到木柄配方，等旧批次和输出搬完。购买手锯锯片6钱，把木柄1+手锯锯片1带到工作台，投料并作业20秒。\n\n做好的手锯会由居民运回仓库，再送往缺工具的岗位。完成后建议切回木板；自动采购可在人员页开启。",
            "目标：现金200、没有欠薪或生活缺供。继续生产、拆解回收或出售已解锁技术都可以融资。\n\n技术出售会永久压低对应商品售价，确认窗口会说明影响；这不是必须选择。不要为了凑钱出售保护饮水。",
            "你已经掌握拾取、真实搬运、生产、发电、招募、工资和工具续补。\n\n可以继续经营，留意工资、生活物资和工具耐久。随时从“教程”页翻阅任意一课；重看不会重开游戏。"
        };

        public static bool Satisfied(PrototypeSimulation m, int step)
        {
            switch (step)
            {
                case 1: return m.HasSaw;
                case 2: return m.Warehouse.Count(ItemId.Food)>=12 && m.Warehouse.Count(ItemId.DrinkingWater)>=24 || m.HasBeenPowered && m.MealsConsumed>0 && m.MissingSupplyPeople==0;
                case 3: return m.BenchBuilt || m.Warehouse.Count(ItemId.WasteWood)>=16 && m.Warehouse.Count(ItemId.RecoveredFastener)>=4;
                case 4: return m.BenchBuilt;
                case 5: return m.CompletedBatches>=2 && (m.Warehouse.Count(ItemId.Plank)>=6 || m.TableBuilt || m.TotalSold>=6);
                case 6: return m.TableBuilt && m.DismantleBuilt;
                case 7: return m.DismantledBatches>=1 && m.DismantleOutput.UsedTicks==0 && m.Carry.UsedTicks==m.Carry.Count(ItemId.WoodHandSaw)*ItemCatalog.CapacityTicks(ItemId.WoodHandSaw);
                case 8: return m.HasBeenPowered;
                case 9: return m.Residents.Count>=5 && m.WoodTools.Count(ItemId.WoodHandSaw)>0 && m.Woodworker!=null;
                case 10: return m.TotalWagesPaidCents>=5000 && m.UnpaidWageCents==0;
                case 11: return m.AssembledTools>=1;
                case 12: return m.DemoCompleted;
                default: return false;
            }
        }
        private static TutorialAction Plan(TutorialOperation op,string label,string tip,string button,int page=0,int? x=null,int? y=null,ItemId item=ItemId.WasteWood,int amount=1)
            => new TutorialAction{operation=op,label=label,instruction=tip,button=button,page=page,x=x??0,y=y??0,hasTarget=x.HasValue,item=item,amount=amount};
        private static TutorialAction Wait(string text) => Plan(TutorialOperation.Wait,"等待实际完成",text,null);
        private static TutorialAction Depot() => Plan(TutorialOperation.Deposit,"入库 Q","把背包物品送入仓库；随身手锯保留。","Deposit",2,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY);
        private static TutorialAction Pick(PrototypeSimulation m,ItemId item,int count)
        {
            var pile=m.Ground.Where(p=>p.Item==item && p.Amount>p.Reserved).OrderBy(p=>PrototypeSimulation.Distance(m.PlayerX,m.PlayerY,p.X,p.Y)).FirstOrDefault();
            if(pile==null)return Wait("地面没有未预约的"+ItemCatalog.Name(item)+"。查看仓库或在现场/人员页采购。");
            return Plan(TutorialOperation.Pick,"拾取"+ItemCatalog.Name(item),"走近后拾取"+count+"份；装不下会按容量分趟。","Pick"+(int)item,2,pile.X,pile.Y,item,count);
        }
        private static TutorialAction Collect(PrototypeSimulation m,ItemId item,int target)
        {
            if(m.Carry.Available(item)>0 || m.Carry.AvailableSlots(item)==0)return Depot();
            return Pick(m,item,Math.Max(1,target-m.Warehouse.Available(item)));
        }
        private static TutorialAction Carry(PrototypeSimulation m,ItemId item,int count)
        {
            if(m.Carry.Available(item)>=count)return null;
            if(m.Carry.AvailableSlots(item)==0)return Depot();
            if(m.Warehouse.Available(item)==0)return Pick(m,item,count-m.Carry.Available(item));
            return Plan(TutorialOperation.Withdraw,"取出"+ItemCatalog.Name(item),"从仓库取到背包，再亲自带到现场。","Take"+(int)item,2,PrototypeSimulation.WarehouseX,PrototypeSimulation.WarehouseY,item,count-m.Carry.Available(item));
        }
        public static TutorialAction NextAction(PrototypeSimulation m,int step)
        {
            if(m.Interacting)return Wait("正在进行0.5秒交互，请等完成；移动会取消。");
            if(m.AnyBuildPending)return Wait("施工蓝图已规划。按空格恢复后建成；可在现场页取消蓝图。");
            switch(step)
            {
                case 1: return Pick(m,ItemId.WoodHandSaw,1);
                case 2:
                    if(m.Warehouse.Available(ItemId.Food)<12)return Collect(m,ItemId.Food,12);
                    return Collect(m,ItemId.DrinkingWater,24);
                case 3:
                    if(m.Warehouse.Available(ItemId.WasteWood)<16)return Collect(m,ItemId.WasteWood,16);
                    return Collect(m,ItemId.RecoveredFastener,4);
                case 4: return Plan(TutorialOperation.BuildBench,"搭建木工台","木料4+紧固件2；底栏按钮也可建设。","Build",0,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                case 5:
                    if(m.Carry.Available(ItemId.Plank)>0 || m.Carry.Available(ItemId.WoodChips)>0)return Depot();
                    if(m.Output.UsedTicks>0)return Plan(TutorialOperation.TakeWood,"取木工产物","把成品取进背包，下一步入库。","Command11",0,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                    if(m.SelectedRecipe!=WoodRecipe.Plank && !m.RecipeChangePending)return Plan(TutorialOperation.SelectPlank,"切到木板配方","这一步要生产木板。","Plank");
                    if(m.State==WorkState.NeedsTool){var personalSaw=Carry(m,ItemId.WoodHandSaw,1);if(personalSaw!=null)return personalSaw;}
                    if(m.JobActive || m.WoodInputStock.Available(ItemId.WasteWood)>=4)
                        return m.State==WorkState.Working?Wait("站在木工正面等一批完成。可按3加速；基础40秒，现场效率会影响耗时。"):
                            Plan(TutorialOperation.WorkWood,"木工作业 F","进入木工岗位后不要走开；进度保留。","Work",0,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                    var wood=Carry(m,ItemId.WasteWood,4);if(wood!=null)return wood;
                    return Plan(TutorialOperation.FeedWood,"投木料","原料进入地点缓冲，仓库里的木料不能隔空加工。","Command10",0,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                case 6:
                    if(!m.TableBuilt && m.Warehouse.Available(ItemId.SteelPart)<8)return Collect(m,ItemId.SteelPart,8);
                    if(!m.TableBuilt)return Plan(TutorialOperation.BuildTable,"建工作台","预留木板4+钢铁零件2。","Command1",0,PrototypeSimulation.TableX,PrototypeSimulation.TableY);
                    if(m.Warehouse.Available(ItemId.WasteWood)<4)return Collect(m,ItemId.WasteWood,4);
                    if(m.Warehouse.Available(ItemId.RecoveredFastener)<2)return Collect(m,ItemId.RecoveredFastener,2);
                    return Plan(TutorialOperation.BuildDismantle,"建拆解地点","木料4+紧固件2。","Command0",0,PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);
                case 7:
                    if(m.Carry.Available(ItemId.Motor)>0 || m.Carry.Available(ItemId.MachineBlade)>0 || m.DismantledBatches>0 && m.Carry.UsedTicks>m.Carry.Count(ItemId.WoodHandSaw)*20)return Depot();
                    if(m.DismantleOutput.UsedTicks>0)return Plan(TutorialOperation.TakeDeviceOutput,"取拆解产物","取出后搬运到仓库。","Command15",0,PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);
                    if(m.DismantleProgress>0 || m.DismantleInput.Count(ItemId.OldCutter)>0)
                        return m.DismantleState==WorkState.Working?Wait("在拆解岗位等待35秒；不要走开。"):
                            Plan(TutorialOperation.WorkDismantle,"开拆解作业","带螺丝刀在正面操作。","Command8",0,PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);
                    var screw=Carry(m,ItemId.Screwdriver,1);if(screw!=null)return screw;
                    var device=Carry(m,ItemId.OldCutter,1);if(device!=null)return device;
                    return Plan(TutorialOperation.FeedDevice,"投入旧切割装置","旧设备进入现场缓冲后才能拆解。","Command14",0,PrototypeSimulation.DismantleX,PrototypeSimulation.DismantleY);
                case 8:
                    if(!m.GeneratorBuilt)
                    {
                        foreach(var cost in new[]{new ItemAmount(ItemId.SteelPart,6),new ItemAmount(ItemId.ThermalModule,1),new ItemAmount(ItemId.GeneratorModule,1),new ItemAmount(ItemId.RecoveredWire,2)})
                            if(m.Warehouse.Available(cost.Item)<cost.Amount)return Collect(m,cost.Item,cost.Amount);
                        return Plan(TutorialOperation.BuildGenerator,"组装发电机","完整材料齐备才建设。","Command2",0,PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY);
                    }
                    ItemId fuel=m.Warehouse.Available(ItemId.WoodChips)>0 || m.Carry.Available(ItemId.WoodChips)>0?ItemId.WoodChips:ItemId.WasteWood;
                    var fuelAction=Carry(m,fuel,1);if(fuelAction!=null)return fuelAction;
                    return Plan(TutorialOperation.Fuel,"投入燃料","要发电机实际运行，居民才愿意加入。",fuel==ItemId.WasteWood?"Command6":"Command7",0,PrototypeSimulation.GeneratorX,PrototypeSimulation.GeneratorY,fuel,int.MaxValue);
                case 9:
                    if(m.Residents.Count<5)return Plan(TutorialOperation.Recruit,"招募1人","还需招募"+(5-m.Residents.Count)+"人。迁入存款属于居民，工资到期要付。","Command3",1);
                    if(m.WoodTools.Count(ItemId.WoodHandSaw)==0)
                    {
                        var saw=Carry(m,ItemId.WoodHandSaw,1);if(saw!=null)return saw;
                        return Plan(TutorialOperation.HandSaw,"交接手锯","居民不能使用远处主角背包的手锯。","Command12",0,PrototypeSimulation.BenchX,PrototypeSimulation.BenchY);
                    }
                    return Plan(TutorialOperation.AssignWorker,"指定居民木工","其余居民负责实际配送。","Command4",1);
                case 10:
                    if(m.Warehouse.Available(ItemId.Plank)>0 || m.Warehouse.Available(ItemId.WoodHandle)>0)return Plan(TutorialOperation.Sell,"出售木制品","先卖已送入仓库的产品，为工资筹钱。","Command19");
                    if(m.UnpaidWageCents>0 && m.CashCents>0)return Plan(TutorialOperation.Pay,"支付工资","按原账单把经营现金转进居民钱包。","Command5",1);
                    return Wait($"现金 {m.Money:0.00} / 欠薪 {m.UnpaidWages:0.00}。距离下次发薪 {m.SecondsToWage:0}秒；用3倍速等生产和配送，收到木板后再出售。");
                case 11:
                    if(m.TableOutput.UsedTicks>0)return Wait("工具已产出，居民会运回仓库。");
                    if(m.AssemblyActive)
                        return m.AssemblyWorking?Wait("站在工作台正面等装配完成；人工20秒。"):
                            Plan(TutorialOperation.WorkAssembly,"继续工作台作业","退出岗位会停，进度保留；回到正面继续。","Command33",0,PrototypeSimulation.TableX,PrototypeSimulation.TableY);
                    if(m.TableInput.Count(ItemId.WoodHandle)>0 && m.TableInput.Count(ItemId.SawBlade)>0)
                        return Plan(TutorialOperation.WorkAssembly,"工作台作业","在正面操作20秒，制成一把备用手锯。","Command33",0,PrototypeSimulation.TableX,PrototypeSimulation.TableY);
                    if(m.Carry.Available(ItemId.WoodHandle)==0 && m.Warehouse.Available(ItemId.WoodHandle)==0 && m.TableInput.Count(ItemId.WoodHandle)==0)
                        return m.SelectedRecipe==WoodRecipe.WoodHandle || m.RecipeChangePending?Wait("等待当前批次和输出清空，再由居民生产木柄。"):
                            Plan(TutorialOperation.SelectHandle,"木工切到木柄","当前批次会先做完，旧输出需实际运走。","Handle");
                    if(m.Warehouse.Available(ItemId.SawBlade)==0 && m.Carry.Available(ItemId.SawBlade)==0 && m.TableInput.Count(ItemId.SawBlade)==0)
                        return Plan(TutorialOperation.BuyBlade,"购买手锯锯片 · 6钱","机器锯片不能替代手锯锯片。","Command28");
                    if(m.TableInput.Count(ItemId.WoodHandle)==0){var handle=Carry(m,ItemId.WoodHandle,1);if(handle!=null)return handle;}
                    if(m.TableInput.Count(ItemId.SawBlade)==0){var end=Carry(m,ItemId.SawBlade,1);if(end!=null)return end;}
                    return Plan(TutorialOperation.FeedAssembly,"投装配材料","带木柄1、手锯锯片1到工作台正面。","Command32",0,PrototypeSimulation.TableX,PrototypeSimulation.TableY);
                case 12:
                    if(m.SelectedRecipe!=WoodRecipe.Plank && !m.RecipeChangePending)return Plan(TutorialOperation.SelectPlank,"木工切回木板","继续保持生产利润，注意工资与生活。","Plank");
                    if(m.UnpaidWageCents>0 && m.CashCents>0)return Plan(TutorialOperation.Pay,"支付到期工资","先解除欠薪，再达成200钱目标。","Command5",1);
                    if(m.Warehouse.Available(ItemId.Plank)>0 || m.Warehouse.Available(ItemId.WoodHandle)>0)return Plan(TutorialOperation.Sell,"出售木制品","继续生产也能融资；技术交易是另一个选择。","Command19");
                    return Plan(TutorialOperation.Trade,"查看融资选项","现场页底部提供技术交易；每次出售需单独确认，也可以继续生产或拆解筹钱。","Command37");
                default: return Wait("从菜单开始，或翻阅其他课程。");
            }
        }
    }

    public sealed partial class PrototypeSimulation
    {
        public TutorialProgress Tutorial { get; private set; } = new TutorialProgress();
    }
}
