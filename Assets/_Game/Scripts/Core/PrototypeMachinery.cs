using System;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        public static bool IsWoodMachine(int kind)=>kind==64||kind==128;
        public bool ElectricWoodUnlocked=>HasDiscovered(ItemId.Motor)&&HasDiscovered(ItemId.MachineBlade);
        public bool AutomaticWoodUnlocked=>ElectricWoodUnlocked&&HasDiscovered(ItemId.FeedingModule);
        public bool ConveyorsUnlocked=>HasDiscovered(ItemId.Motor)&&HasDiscovered(ItemId.SteelPart);
        public bool AutomaticMachineStaffing {get;private set;}=true;
        private double nextMachineStaffing;
        public static string MachineInputKey(int id)=>"machineInput:"+id;
        public static string MachineOutputKey(int id)=>"machineOutput:"+id;
        public static ItemId MachineFeed(FacilityInstance f)=>f.Kind==128?ItemId.SortedWood:ItemId.WasteWood;
        public static (int x,int y) MaintenanceCell(FacilityInstance f)
        {
            var size=Footprint(f);
            switch(f.Rotation){case 1:return(f.X-1,f.Y+size.height-2);case 2:return(f.X+size.width-2,f.Y+size.height);case 3:return(f.X+size.width,f.Y+1);default:return(f.X+1,f.Y-1);}
        }
        public void SetAutomaticMachineStaffing(bool enabled){AutomaticMachineStaffing=enabled;nextMachineStaffing=0;if(enabled)foreach(var f in facilities){f.SuppressAutoOperator=f.SuppressAutoMaintenance=false;}}
        public ActionResult ToggleMachine(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!IsWoodMachine(f.Kind)||Failed)return Fail("请选择已建成的木工机器。");
            f.RunRequested=!f.RunRequested;nextMachineStaffing=0;RecomputePower();UpdateMachineState(f);
            return Ok(f.RunRequested?"已请求生产；需要现场原料、操作和实际供电。":"机器已停产，批次和预约保留；维护继续。");
        }
        public ActionResult AssignMachineWorker(int id,bool maintenance,int residentId=0,bool automatic=false)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!IsWoodMachine(f.Kind))return Fail("没有该木工机器。");
            if(!maintenance&&f.Kind==128)return Fail("自动木工不需要持续操作工。");
            WorkerJob job=maintenance?WorkerJob.Maintenance:WorkerJob.MachineOperator;
            var existing=residents.FirstOrDefault(r=>r.Job==job&&r.FacilityId==id);
            if(existing!=null){existing.Job=WorkerJob.Idle;existing.FacilityId=0;if(maintenance)f.SuppressAutoMaintenance=true;else f.SuppressAutoOperator=true;nextMachineStaffing=GameSeconds+2;return Ok("已手动释放该机岗位，自动填岗不会立即填回；已携货仍会完成。");}
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            var candidate=residents.Where(r=>r.Job==WorkerJob.Idle&&r.Delivery==null&&(residentId==0||r.Id==residentId)&&FindPath(r.X,r.Y,side.x,side.y)!=null)
                .OrderBy(r=>Distance(r.X,r.Y,side.x,side.y)).ThenBy(r=>r.Id).FirstOrDefault();
            if(candidate==null)return Fail("没有可到达岗位的空闲居民；正在搬运者先送完货。");
            candidate.Job=job;candidate.FacilityId=id;candidate.Path=null;
            if(maintenance)f.SuppressAutoMaintenance=false;else f.SuppressAutoOperator=false;
            return Ok(maintenance?"维护工正在前往维护侧；断电和停产时仍可维护。":"操作工正在前往作业面；机器阶段可以参与搬运。");
        }
        public ActionResult ToggleMachinePlayerRole(int id,bool maintenance)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!IsWoodMachine(f.Kind)||Failed||RiotActive)return Fail("当前不能进入机器岗位。");
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            if(Distance(PlayerX,PlayerY,side.x,side.y)>0.51)return Fail(maintenance?"先前往蓝色维护侧。":"先到机器作业面。");
            int role=maintenance?6:5;
            if(manualStationValue==role&&manualFacilityId==id){LeaveManualRole();return Ok("已退出岗位，批次进度保留。");}
            if(!maintenance&&f.Kind==128)return Fail("自动木工无需人工装料工序。");
            CancelInteraction();manualStationValue=role;manualFacilityId=id;
            if(!maintenance)f.RunRequested=true;
            return Ok(maintenance?"主角开始维护；移动或Esc退出。":"主角进入人工准备岗位；机器阶段自动运行。");
        }
        public ActionResult FeedMachine(int id)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!IsWoodMachine(f.Kind))return Fail("没有该机器。");
            var side=ServiceCell(f);if(!Near(side.x,side.y))return Fail("先到机器正面投料。");
            return QueueInteraction(()=>{
                if(!f.Built||!Near(side.x,side.y))return Fail("位置或机器状态已改变。");
                int count=MoveStock(Carry,f.WoodInputStock,"carry",MachineInputKey(id),MachineFeed(f),int.MaxValue);
                return count>0?Ok("原料已投入现场缓冲。",count):Fail("背包缺少适用原料，或输入已满。");
            });
        }
        public ActionResult TakeMachineStock(int id,bool input=false)
        {
            var f=GetFacility(id);if(f==null||!f.Built||!IsWoodMachine(f.Kind))return Fail("没有该机器。");
            var side=ServiceCell(f);if(!Near(side.x,side.y))return Fail("先到机器正面取物。");
            return QueueInteraction(()=>{
                if(!f.Built||!Near(side.x,side.y))return Fail("位置或机器状态已改变。");
                var stock=input?f.WoodInputStock:f.Output;int count=0;
                foreach(ItemId item in ItemCatalog.All)count+=MoveStock(stock,Carry,input?MachineInputKey(id):MachineOutputKey(id),"carry",item,stock.Available(item));
                return count>0?Ok("机器物品已进入背包。",count):Fail("没有可取物品或背包已满；在制原料不能取走。");
            });
        }
        public ActionResult CancelMachineBatch(int id)
        {
            var f=GetFacility(id);if(f==null||!IsWoodMachine(f.Kind))return Fail("没有该机器。");
            if(f.MachineActive&&f.MachineProgress>=DesignBaseline.WoodMachineSeconds-1e-6)return Fail("已完成待出料批次不能再退同批原料。");
            if(f.MachineActive)f.WoodInputStock.Release(f.MachineToken);
            f.MachineActive=false;f.MachineToken=0;f.HumanProgress=f.MachineProgress=0;f.MachinePhase=0;f.RunRequested=false;
            if(manualFacilityId==id&&manualStationValue==5)LeaveManualRole();
            RecomputePower();UpdateMachineState(f);return Ok("在制预约已释放，已用人时、燃料和机器磨损不返还。");
        }
        private Resident MachineWorker(FacilityInstance f,WorkerJob job)=>residents.FirstOrDefault(r=>r.Job==job&&r.FacilityId==f.Id&&r.Delivery==null);
        private bool AtMachinePost(FacilityInstance f,bool maintenance,out Resident person)
        {
            person=MachineWorker(f,maintenance?WorkerJob.Maintenance:WorkerJob.MachineOperator);
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            if(manualStationValue==(maintenance?6:5)&&manualFacilityId==f.Id&&Distance(PlayerX,PlayerY,side.x,side.y)<0.51){person=null;return true;}
            return person!=null&&Distance(person.X,person.Y,side.x,side.y)<0.1;
        }
        private double MachineryHumanRate(FacilityInstance f,Resident person,bool maintenance=false)
        {
            var p=maintenance?MaintenanceCell(f):ServiceCell(f);var needs=person?.Needs??PlayerNeeds;
            return DesignRules.HumanEfficiency(1,BonusFactor,person?.Happiness??70,person==null?0:EquivalentUnpaidPeriods,needs.FoodMultiplier(GameSeconds),needs.WaterMultiplier(GameSeconds),PressureAt(p.x,p.y),1);
        }
        private void StaffMachines()
        {
            if(!AutomaticMachineStaffing||RiotActive||GameSeconds<nextMachineStaffing)return;
            nextMachineStaffing=GameSeconds+2;
            // Maintenance is a persistent post, including idle/blocked equipment.
            foreach(var f in facilities.Where(f=>f.Built&&IsWoodMachine(f.Kind)).OrderBy(f=>f.Id))
                if(!f.SuppressAutoMaintenance&&!residents.Any(r=>r.FacilityId==f.Id&&r.Job==WorkerJob.Maintenance)&&!(manualStationValue==6&&manualFacilityId==f.Id))AssignMachineWorker(f.Id,true,0,true);
            foreach(var f in facilities.Where(f=>f.Built&&f.Kind==64&&f.RunRequested).OrderBy(f=>f.Id))
                if(!f.SuppressAutoOperator&&!residents.Any(r=>r.FacilityId==f.Id&&r.Job==WorkerJob.MachineOperator)&&!(manualStationValue==5&&manualFacilityId==f.Id))AssignMachineWorker(f.Id,false,0,true);
            StaffProcesses();
        }
        private bool TickMachineWorker(Resident person,double seconds)
        {
            if(person.Job!=WorkerJob.Maintenance&&person.Job!=WorkerJob.MachineOperator)return false;
            var f=GetFacility(person.FacilityId);
            if(f!=null&&f.Built&&!IsWoodMachine(f.Kind)&&(RunsProcesses(f.Kind)||HasMaintenanceSide(f.Kind)))return false;
            if(f==null||!f.Built||!IsWoodMachine(f.Kind)){person.Job=WorkerJob.Idle;person.FacilityId=0;return false;}
            if(person.Delivery!=null)return false;
            bool maintenance=person.Job==WorkerJob.Maintenance;
            if(manualFacilityId==f.Id&&manualStationValue==(maintenance?6:5))return false;
            bool preparing=f.Kind==64&&f.RunRequested&&!f.MachineTripped&&(!f.MachineActive&&f.WoodInputStock.Available(MachineFeed(f))>=4&&f.Output.FreeTicks>=80||f.MachineActive&&f.MachinePhase==1);
            if(!maintenance&&!preparing)return false;
            var side=maintenance?MaintenanceCell(f):ServiceCell(f);
            person.Activity=maintenance?$"维护 #{f.Id} · 耐久 {f.Durability:0.#}":$"机器 #{f.Id} 人工准备";
            WalkResident(person,side.x,side.y,seconds);
            if(person.Path==null)person.Activity="岗位无路径，等待恢复";
            return true;
        }
        private void PrepareMachineBatches()
        {
            foreach(var f in facilities.Where(f=>f.Built&&IsWoodMachine(f.Kind)))
            {
                if(f.MachineActive||!f.RunRequested||f.MachineTripped||f.Durability<=0||f.Output.FreeTicks<80)continue;
                if(f.Kind==64&&(RiotActive||!AtMachinePost(f,false,out _)))continue;
                int token=f.WoodInputStock.Reserve(new ItemAmount(MachineFeed(f),4));if(token==0)continue;
                f.MachineToken=token;f.MachineActive=true;f.MachinePhase=f.Kind==128?2:1;f.HumanProgress=f.MachineProgress=0;
            }
        }
        private double MachinePowerRequest(FacilityInstance f)=>IsWoodMachine(f.Kind)&&f.Built&&f.RunRequested&&f.MachineActive&&f.MachinePhase==2&&!f.MachineTripped&&f.Durability>0&&f.MachineProgress<DesignBaseline.WoodMachineSeconds-1e-6?(f.Kind==128?DesignBaseline.AutomaticWoodLoad:DesignBaseline.ElectricWoodLoad):0;
        private void TickMachinery(double seconds)
        {
            foreach(var f in facilities.Where(f=>f.Built&&IsWoodMachine(f.Kind)))
            {
                bool poweredPhase=MachinePowerRequest(f)>0;
                if(!RiotActive&&AtMachinePost(f,true,out var maintainer)){double repair=Math.Min(DesignBaseline.MachineMaximum-f.Durability,seconds*DesignBaseline.MachineRepair*MachineryHumanRate(f,maintainer,true));f.Durability+=repair;f.RepairedDurability+=repair;}
                if(f.MachineTripped&&f.Durability>=DesignBaseline.MachineRestart-1e-8){f.Durability=Math.Max(f.Durability,DesignBaseline.MachineRestart);f.MachineTripped=false;}
                if(f.RunRequested&&f.MachineActive&&!f.MachineTripped&&f.Durability>0)
                {
                    bool working=false;double activeSeconds=Math.Min(seconds,f.Durability/DesignBaseline.MachineDecay);
                    if(f.MachinePhase==1&&!RiotActive&&AtMachinePost(f,false,out var person))
                    {
                        f.HumanProgress=Math.Min(DesignBaseline.ElectricHumanSeconds,f.HumanProgress+activeSeconds*MachineryHumanRate(f,person));working=true;
                        if(f.HumanProgress>=DesignBaseline.ElectricHumanSeconds-1e-6)f.MachinePhase=2;
                    }
                    else if(poweredPhase&&f.MachinePhase==2&&f.StepPowerRatio>0&&f.MachineProgress<DesignBaseline.WoodMachineSeconds-1e-6)
                    {activeSeconds=Math.Min(seconds*f.StepPoweredFraction,f.Durability/DesignBaseline.MachineDecay);f.MachineProgress=Math.Min(DesignBaseline.WoodMachineSeconds,f.MachineProgress+activeSeconds*f.StepPowerRatio/f.StepPoweredFraction);working=true;}
                    if(working){f.Durability=Math.Max(0,f.Durability-activeSeconds*DesignBaseline.MachineDecay);if(f.Durability<=1e-8){f.Durability=0;f.MachineTripped=true;}}
                }
                if(f.MachineActive&&f.MachineProgress>=DesignBaseline.WoodMachineSeconds-1e-6&&f.Output.FreeTicks>=80)
                    CompleteMachineBatch(f,false);
                UpdateMachineState(f);
            }
            RecomputePower();
        }
        private void CompleteMachineBatch(FacilityInstance f,bool evacuate)
        {
            if(!f.WoodInputStock.Commit(f.MachineToken))throw new InvalidOperationException("机器在制预约丢失。");
            var products=new[]{new ItemAmount(ItemId.Plank,3),new ItemAmount(ItemId.WoodChips,1)};
            if(evacuate)foreach(var p in products)ReturnAtOldSite(p.Item,p.Amount,"",f.X,f.Y);
            else if(!f.Output.TryAddBatch(products))throw new InvalidOperationException("整批输出容量未受保护。");
            DiscoverProducts(products);f.MachineBatches++;CompletedBatches++;f.MachineToken=0;f.MachineActive=false;f.MachinePhase=0;f.HumanProgress=f.MachineProgress=0;
        }
        private void UpdateMachineState(FacilityInstance f)
        {
            f.State=!f.Built?WorkState.NotBuilt:Paused?WorkState.Paused:f.MachineTripped?WorkState.NeedsMaintenance:!f.RunRequested?WorkState.Idle:
                f.MachineActive&&f.MachineProgress>=DesignBaseline.WoodMachineSeconds-1e-6||!f.MachineActive&&f.Output.FreeTicks<80?WorkState.OutputBlocked:
                !f.MachineActive&&f.WoodInputStock.Available(MachineFeed(f))<4?WorkState.NeedsMaterials:
                f.Kind==64&&(!f.MachineActive||f.MachinePhase==1)&&(RiotActive||!AtMachinePost(f,false,out _))?WorkState.NeedsOperator:
                f.MachinePhase==2?(f.PowerRatio<=0?WorkState.NeedsPower:WorkState.MachineRunning):WorkState.Preparing;
        }
        private ActionResult EvacuateMachine(FacilityInstance f,bool sell,bool returnMaterials)
        {
            if(Interacting||Failed)return Fail("先结束当前交互；失败后不能拆迁。");
            if(f.MachineActive)
            {if(f.MachineProgress>=DesignBaseline.WoodMachineSeconds-1e-6)CompleteMachineBatch(f,true);else{f.WoodInputStock.Release(f.MachineToken);f.MachineActive=false;f.MachineToken=0;}}
            RedirectMachineDeliveries(f);DetachFacilityPorts(f.Id);
            foreach(var entry in new[]{(f.WoodInputStock,MachineInputKey(f.Id)),(f.Output,MachineOutputKey(f.Id))})
                foreach(ItemId item in ItemCatalog.All){int count=entry.Item1.Available(item);entry.Item1.TryRemove(item,count);ReturnAtOldSite(item,count,entry.Item2,f.X,f.Y);}
            foreach(var r in residents.Where(r=>r.FacilityId==f.Id)){r.Job=WorkerJob.Idle;r.FacilityId=0;}
            if(manualFacilityId==f.Id)LeaveManualRole();f.Built=false;f.RunRequested=false;f.MachinePhase=0;f.HumanProgress=f.MachineProgress=0;
            if(returnMaterials)
            {
                var costs=DesignBaseline.BuildCost(f.Kind);
                if(sell)cashCents+=(long)decimal.Round(costs.Sum(p=>ItemCatalog.Price(p.Item).buy*p.Amount)*0.45m*(decimal)(0.5+f.Durability/200)*100,0,MidpointRounding.AwayFromZero);
                else foreach(var p in costs)ReturnAtOldSite(p.Item,p.Amount,"",f.X,f.Y);
            }
            geometryRevision++;GroundRevision++;ConstructionRevision++;RecomputePower();return Ok(sell?"机器已出售，内部物品已返还。":"机器已清空，预约及岗位已释放。");
        }
        private void RedirectMachineDeliveries(FacilityInstance f)
        {
            foreach(var r in residents.Where(r=>r.Delivery!=null&&(r.Delivery.Source==f.WoodInputStock||r.Delivery.Source==f.Output||r.Delivery.Target==f.WoodInputStock)))
            {
                var d=r.Delivery;if(!d.Picked){if(d.Source!=null)d.Source.Release(d.Token);else d.Ground.Reserved-=d.Amount;ClearDelivery(r);}
                else {d.Target=Warehouse;d.To="warehouse";d.TargetX=WarehouseX;d.TargetY=WarehouseY;r.Path=null;}
            }
        }
        private DeliveryTask MachineDelivery(Resident actor,bool output)
        {
            foreach(var f in facilities.Where(f=>f.Built&&IsWoodMachine(f.Kind)).OrderBy(f=>f.Id))
            {
                var side=ServiceCell(f);DeliveryTask task;
                if(output)
                {foreach(var item in new[]{ItemId.Plank,ItemId.WoodChips}){if(PreferConveyorOutput(f.Output,item))continue;task=Plan(actor,f.Output,Warehouse,MachineOutputKey(f.Id),"warehouse",item,side.x,side.y,WarehouseX,WarehouseY,f.Output.Available(item));if(task!=null)return task;}}
                else if(f.RunRequested&&f.WoodInputStock.Count(MachineFeed(f))<4&&!PreferConveyorInput(f.Id,MachineFeed(f)))
                {task=Supply(actor,f.WoodInputStock,MachineInputKey(f.Id),MachineFeed(f),side.x,side.y,8-f.WoodInputStock.Count(MachineFeed(f)));if(task!=null)return task;}
            }
            return null;
        }
    }
}
