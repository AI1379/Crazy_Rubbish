using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        public long ConveyorDeliveredItems {get;private set;}
        public string ConveyorStatus(int id)
        {
            var b=GetConveyor(id);if(b==null)return "传送带不存在";
            if(b.Blueprint)return "等待施工";if(!b.Enabled)return "已停止，实物保留";if(b.PowerRatio<=0)return "等待实际供电";
            if(b.Item<0)return "空带，等待上游物品";if(b.Progress<1-1e-6)return "正在输送";
            var next=ConveyorNext(b);var p=ports.FirstOrDefault(a=>a.X==next.x&&a.Y==next.y);
            if(p!=null)return !LivePort(p)?"接口停用或未绑定":!PortAccepts(p,(ItemId)b.Item)?"接口方向或过滤不匹配":SinkFreeTicks(p)<ItemCatalog.CapacityTicks((ItemId)b.Item)?"目标库存满，等待疏通":"等待接口交接";
            var target=LaneReceiving(next.x,next.y,b.Direction);return target==null?"未连接下游":target.PowerRatio<=0?"下游缺电":target.Item>=0?"下游拥堵，货物保留":"等待合流交接";
        }
        private sealed class BeltMove
        {public ConveyorLane Source,Target;public ConveyorPort Port;public int Item,Instance;}
        private static bool ConveyorNeedsInstance(ItemId item)=>ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item)||item==ItemId.BrokenWoodTool;
        private bool LivePort(ConveyorPort p)=>!p.Blueprint&&p.Enabled&&PortOwner(p.OwnerId)?.Built==true;
        private (UnitInventory stock,string key) PortStock(ConveyorPort p)
        {
            if(p.OwnerId==-1)return(Warehouse,"warehouse");
            var f=GetFacility(p.OwnerId);if(f==null)return(null,"");
            if(IsWoodMachine(f.Kind))return(p.Output?f.Output:f.WoodInputStock,p.Output?MachineOutputKey(f.Id):MachineInputKey(f.Id));
            if(IsGenericKind(f.Kind))return(p.Output?f.ProcessOutput:f.ProcessInput,p.Output?ProcessOutputKey(f.Id):ProcessInputKey(f.Id));
            return AtFacility(f,()=>f.Kind==1?(p.Output?f.Output:f.WoodInputStock,StockKey(p.Output?"woodOutput":"woodInput")):
                f.Kind==2?(p.Output?f.DismantleOutput:f.DismantleInput,StockKey(p.Output?"dismantleOutput":"dismantleInput")):
                f.Kind==4?(p.Output?f.TableOutput:f.TableInput,StockKey(p.Output?"tableOutput":"tableInput")):
                f.Kind==8?(p.Output?f.GeneratorResidue:null,StockKey(p.Output?"ash":"fuel")):(null,""));
        }
        private bool PortAccepts(ConveyorPort p,ItemId item)
        {
            if(!LivePort(p)||p.Output||p.Filter>=0&&p.Filter!=(int)item)return false;
            if(p.OwnerId==-1)return true;
            var f=GetFacility(p.OwnerId);
            if(IsWoodMachine(f.Kind))return item==MachineFeed(f);
            if(IsGenericKind(f.Kind))return FacilityKey(f.Kind)=="boiler"?item==ItemId.WasteWood||item==ItemId.WoodChips||item==ItemId.DrinkingWater||item==ItemId.IndustrialWater:AcceptedInputs(f).Contains(item);
            if(f.Kind==1)return item==ItemId.WasteWood;
            if(f.Kind==2)return item==f.SelectedDevice;
            if(f.Kind==4)return item==ItemId.WoodHandle||item==(f.SelectedAssembly==AssemblyRecipe.Saw?ItemId.SawBlade:f.SelectedAssembly==AssemblyRecipe.Hammer?ItemId.HammerHead:ItemId.ScrewdriverRod);
            return f.Kind==8&&(item==ItemId.WasteWood||item==ItemId.WoodChips);
        }
        private int SinkFreeTicks(ConveyorPort p)
        {
            var target=PortStock(p);
            if(target.stock!=null)return Math.Max(0,target.stock.FreeTicks-IncomingTicks(target.stock));
            var f=GetFacility(p.OwnerId);if(f==null||f.Kind!=8)return 0;
            return Math.Max(0,10-f.fuelWood-f.fuelChips-residents.Where(r=>r.Delivery?.To==target.key).Sum(r=>r.Delivery.Amount))*20;
        }
        private void DeliverBeltToPort(BeltMove move)
        {
            var target=PortStock(move.Port);var item=(ItemId)move.Item;
            if(target.stock!=null){if(!target.stock.TryAdd(item,1))throw new InvalidOperationException("接口目标容量在同一步中失去预约。");}
            else {var f=GetFacility(move.Port.OwnerId);if(item==ItemId.WasteWood)f.fuelWood++;else f.fuelChips++;}
            if(move.Instance!=0)
            {
                var instance=tools.FirstOrDefault(t=>t.Id==move.Instance&&t.Item==item&&t.Location=="belt:"+move.Source.Id);
                if(instance==null)throw new InvalidOperationException("输送实体实例位置不一致。");instance.Location=target.key;
            }
            discovered.Add(item);
            ConveyorDeliveredItems++;
        }
        private ConveyorLane LaneReceiving(int x,int y,int incomingDirection)
        {
            var at=conveyors.Where(b=>!b.Blueprint&&b.Enabled&&b.X==x&&b.Y==y).ToArray();
            if(at.Length>1)at=at.Where(b=>b.Direction%2==incomingDirection%2).ToArray();
            return at.FirstOrDefault(b=>b.Direction!=(incomingDirection+2)%4);
        }
        private ConveyorPort PortReceiving(ConveyorLane source,(int x,int y) target)
        {return ports.FirstOrDefault(p=>LivePort(p)&&!p.Output&&p.X==target.x&&p.Y==target.y&&p.X+ConveyorDx[p.Facing]==source.X&&p.Y+ConveyorDy[p.Facing]==source.Y);}
        private void TickConveyors(double seconds)
        {
            if(conveyors.Count==0)return;
            foreach(var b in conveyors.Where(b=>!b.Blueprint&&b.Enabled&&b.Item>=0&&b.StepPowerRatio>0))b.Progress=Math.Min(1,b.Progress+seconds*DesignBaseline.BeltSpeed*b.StepPowerRatio);
            var ready=conveyors.Where(b=>!b.Blueprint&&b.Enabled&&b.Item>=0&&b.Progress>=1-1e-6&&b.StepPowerRatio>0).OrderBy(b=>b.Id).ToArray();
            var proposed=new List<BeltMove>();var sinkRoom=new Dictionary<string,int>();
            for(int i=0;i<ready.Length;i++)
            {
                var b=ready[(i+(ready.Length==0?0:portTurn%ready.Length))%ready.Length];var next=ConveyorNext(b);
                var port=PortReceiving(b,next);
                if(port!=null)
                {
                    if(!PortAccepts(port,(ItemId)b.Item))continue;
                    string key=PortStock(port).key;if(!sinkRoom.TryGetValue(key,out int room))room=SinkFreeTicks(port);
                    int ticks=ItemCatalog.CapacityTicks((ItemId)b.Item);if(room<ticks)continue;sinkRoom[key]=room-ticks;
                    proposed.Add(new BeltMove{Source=b,Port=port,Item=b.Item,Instance=b.InstanceId});continue;
                }
                var target=LaneReceiving(next.x,next.y,b.Direction);if(target==null||target.StepPowerRatio<=0)continue;
                proposed.Add(new BeltMove{Source=b,Target=target,Item=b.Item,Instance=b.InstanceId});
            }
            var selected=new Dictionary<int,BeltMove>();
            foreach(var move in proposed.Where(p=>p.Port!=null))selected[move.Source.Id]=move;
            foreach(var group in proposed.Where(p=>p.Target!=null).GroupBy(p=>p.Target.Id))
            {
                var target=group.First().Target;
                var choice=group.OrderBy(p=>(p.Source.Direction-target.MergeCursor+4)%4).ThenBy(p=>p.Source.Id).First();
                selected[choice.Source.Id]=choice;
            }
            // Resolve simultaneous movement from sinks backwards. A complete directed loop
            // can rotate; a blocked chain preserves every entity and its accumulated progress.
            var state=new Dictionary<int,int>();Func<int,bool> canMove=null;
            canMove=id=>{
                if(state.TryGetValue(id,out int mark))return mark==1||mark==2;
                if(!selected.TryGetValue(id,out var move)){state[id]=3;return false;}
                state[id]=1;bool accepted=move.Port!=null||move.Target.Item<0||canMove(move.Target.Id);state[id]=accepted?2:3;return accepted;
            };
            var moves=selected.Values.Where(m=>canMove(m.Source.Id)).ToArray();
            foreach(var move in moves){move.Source.Item=-1;move.Source.InstanceId=0;move.Source.Progress=0;}
            foreach(var move in moves)
            {
                if(move.Port!=null)DeliverBeltToPort(move);
                else
                {
                    if(move.Target.Item>=0)throw new InvalidOperationException("同一步将两个实体写入同一带格。");
                    move.Target.Item=move.Item;move.Target.InstanceId=move.Instance;move.Target.Progress=0;move.Target.MergeCursor=(move.Source.Direction+1)%4;
                    if(move.Instance!=0){var instance=tools.First(t=>t.Id==move.Instance);instance.Location="belt:"+move.Target.Id;}
                }
            }
            var outputs=ports.Where(p=>LivePort(p)&&p.Output).OrderBy(p=>p.Id).ToArray();
            for(int i=0;i<outputs.Length;i++)
            {
                var p=outputs[(i+(outputs.Length==0?0:portTurn%outputs.Length))%outputs.Length];
                var target=LaneReceiving(p.X+ConveyorDx[p.Facing],p.Y+ConveyorDy[p.Facing],p.Facing);
                if(target==null||target.Item>=0||target.StepPowerRatio<=0)continue;
                var source=PortStock(p);if(source.stock==null)continue;
                int count=ItemCatalog.All.Length;
                for(int n=0;n<count;n++)
                {
                    int index=p.Filter>=0?p.Filter:(p.ItemCursor+n)%count;var item=(ItemId)index;
                    if(source.stock.Available(item)<=0){if(p.Filter>=0)break;continue;}
                    if(p.OwnerId==-1&&item==ItemId.DrinkingWater&&source.stock.Available(item)<=DrinkingReserve){if(p.Filter>=0)break;continue;}
                    ToolInstance instance=null;
                    if(ConveyorNeedsInstance(item))
                    {instance=tools.Where(t=>t.Location==source.key&&t.Item==item&&t.ReservedBy==0&&t.UsingAt==0).OrderBy(t=>t.Id).FirstOrDefault();if(instance==null){if(p.Filter>=0)break;continue;}}
                    if(!source.stock.TryRemove(item,1))throw new InvalidOperationException("输出接口扣料失败。");
                    target.Item=index;target.InstanceId=instance?.Id??0;target.Progress=0;if(instance!=null)instance.Location="belt:"+target.Id;
                    p.ItemCursor=(index+1)%count;break;
                }
            }
            portTurn=(portTurn+1)%1000000;
        }
    }
}
