using System;
using System.Linq;
namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        private readonly double[] categoryBudgets=new double[5];
        private int nextMetalItem=-1;
        private readonly double[] livingBudgets=new double[3];
        private double RandomInputInterval()=>DesignRules.InputMeanInterval(GameSeconds)*(2.0/3+random.NextDouble()*2.0/3);
        private void GenerateInput(double interval)
        {
            // Early arrivals remain useful and recognizable; advanced salvage joins after first power.
            if(Opening!=null && !HasBeenPowered)
            {
                AddNatural(ItemId.WasteWood,2,ReceivingX,ReceivingY-2,true);
                AddNatural(GameSeconds%120<60?ItemId.Food:ItemId.DrinkingWater,2,ReceivingX,ReceivingY-2,true);
                SpawnedUnits+=4;return;
            }
            double budget=DesignRules.InputRate(GameSeconds)*interval/60*(0.85+random.NextDouble()*0.3);
            double[] shares={0.35,0.25,0.20,0.15,0.05};
            for(int i=0;i<5;i++)categoryBudgets[i]+=budget*shares[i];
            for(int i=0;i<3;i++)livingBudgets[i]+=budget*0.05/3;
            var livingItems=new[]{ItemId.Food,ItemId.DrinkingWater,ItemId.Soap};
            for(int i=0;i<3;i++)while(livingBudgets[i]+1e-8>=ItemCatalog.CapacityTicks(livingItems[i])/20.0)
            {livingBudgets[i]-=ItemCatalog.CapacityTicks(livingItems[i])/20.0;AddNatural(livingItems[i],1,DropX,DropY,false);SpawnedUnits++;}
            for(int category=0;category<5;category++)
            {
                if(category==4)continue;
                while(categoryBudgets[category]>=0.1)
                {
                    ItemId item;
                    if(category==0)item=ItemId.WasteWood;
                    else if(category==2)item=ItemId.MixedPlastic;
                    else if(category==3)item=random.NextDouble()<0.5?ItemId.WasteTextile:ItemId.OldClothes;
                    else if(category==4){int draw=random.Next(3);item=draw==0?ItemId.Food:draw==1?ItemId.DrinkingWater:ItemId.Soap;}
                    else
                    {
                        if(nextMetalItem>=0)item=(ItemId)nextMetalItem;
                        else {double draw=random.NextDouble();item=draw<0.35?ItemId.SteelMaterial:draw<0.45?ItemId.CopperMaterial:draw<0.55?ItemId.AluminumMaterial:draw<0.60?ItemId.ToolSteelWaste:
                            new[]{ItemId.OldCutter,ItemId.OldWasher,ItemId.OldFormer,ItemId.MouldBox,ItemId.WireBundle,ItemId.OldGrowingLamp,ItemId.OldFeeder,ItemId.OldFurnace,ItemId.OldMetalTable,ItemId.OldControlBox,ItemId.OldLamp}[random.Next(11)];}
                        nextMetalItem=(int)item;
                    }
                    double cost=ItemCatalog.CapacityTicks(item)/20.0;
                    if(categoryBudgets[category]+1e-8<cost)break;
                    categoryBudgets[category]-=cost;if(category==1)nextMetalItem=-1;
                    AddNatural(item,1,DropX,DropY,false);SpawnedUnits++;
                }
            }
        }
        private void AddInitial(int x,int y,ItemId item,int count)=>AddNatural(item,count,x,y,true);
        private void AddNatural(ItemId item,int count,int cx=DropX,int cy=DropY,bool guaranteed=false)
        {
            if(Opening!=null && cx==DropX && cy==DropY){cx=ReceivingX;cy=ReceivingY-2;}
            while(count>0)
            {
                if(FindGroundSpace(item,cx,cy,out var pile,out int x,out int y,double.MaxValue,true))
                {
                    int width=ItemCatalog.CapacityTicks(item)>100?2:1;
                    int available=(100-CellTicks(x,y))*width/ItemCatalog.CapacityTicks(item);
                    if(width==2)available=Math.Min(available,(100-CellTicks(x+1,y))*2/ItemCatalog.CapacityTicks(item));
                    int placed=ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item)?1:Math.Min(count,Math.Max(1,available));
                    var created=Place(item,placed,pile,x,y);
                    created.Usable=guaranteed||random.NextDouble()>=0.25;count-=placed;
                    var instance=tools.FirstOrDefault(t=>t.Id==created.ToolId);if(instance!=null)instance.Usable=created.Usable;
                }
                else
                {
                    int width=ItemCatalog.CapacityTicks(item)>100?2:1;
                    var cell=ground.Where(p=>p.X+width<=Settings.mapWidth&&Enumerable.Range(p.X,width).All(x=>OwnedGround(x,p.Y)&&!Occupied(x,p.Y)))
                        .OrderBy(p=>Enumerable.Range(p.X,width).Sum(x=>CellTicks(x,p.Y))).ThenBy(p=>Distance(p.X,p.Y,cx,cy)).ThenBy(p=>p.Y).ThenBy(p=>p.X).First();
                    int placed=ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item)?1:count;
                    var compatible=ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item)?null:ground.FirstOrDefault(p=>p.X==cell.X&&p.Y==cell.Y&&p.Item==item);
                    Place(item,placed,compatible,cell.X,cell.Y);count-=placed;
                }
            }
        }
        private int CellTicks(int x,int y)=>groundCapacity.TryGetValue((x,y),out int ticks)?ticks:0;
        public decimal GroundCapacityAt(int x,int y)=>CellTicks(x,y)/20m;
        private bool FindGroundSpace(ItemId item,int cx,int cy,out GroundPile target,out int x,out int y,double limit=double.MaxValue,bool heapFirst=false)
        {
            target=null;x=y=-1;double best=double.MaxValue;int bestArea=2,bestMixed=2,width=ItemCatalog.CapacityTicks(item)>100?2:1;
            var otherCells=new System.Collections.Generic.HashSet<(int,int)>();
            foreach(var existing in ground)if(existing.Item!=item)
                for(int cell=existing.X;cell<existing.X+existing.Width;cell++)otherCells.Add((cell,existing.Y));
            var zone=GarbageZone(cx,cy);
            for(int py=0;py<Settings.mapHeight;py++)for(int px=0;px<Settings.mapWidth;px++)
            {
                if(!Enumerable.Range(px,width).All(q=>OwnedGround(q,py)&&!Occupied(q,py)&&CellTicks(q,py)+ItemCatalog.CapacityTicks(item)/width<=100))continue;
                double distance=Distance(cx,cy,px,py);if(distance>limit)continue;
                int area=heapFirst&&px>=zone.x0&&px<=zone.x1&&py>=zone.y0&&py<=zone.y1?0:1;
                int mixed=otherCells.Contains((px,py))||width==2&&otherCells.Contains((px+1,py))?1:0;
                if(area>bestArea||area==bestArea&&(mixed>bestMixed||mixed==bestMixed&&distance>=best))continue;
                bestArea=area;bestMixed=mixed;best=distance;x=px;y=py;
                target=ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item)?null:ground.Find(p=>p.X==px&&p.Y==py&&p.Item==item);
            }
            return x>=0;
        }
        private GroundPile Place(ItemId item,int count,GroundPile pile,int x,int y,bool createTool=true)
        {
            int width=ItemCatalog.CapacityTicks(item)>100?2:1;
            for(int px=x;px<x+width;px++)groundCapacity[(px,y)]=CellTicks(px,y)+count*ItemCatalog.CapacityTicks(item)/width;
            if(pile==null){pile=new GroundPile(x,y,item,count){Id=nextGroundId++,Width=width};ground.Add(pile);}else pile.Amount+=count;
            if(createTool&&(ItemCatalog.IsTool(item)||ItemCatalog.IsDevice(item)||ItemCatalog.IsOpenable(item))){var tool=new ToolInstance{Id=nextToolId++,Item=item,Location="ground:"+pile.Id,Durability=ItemCatalog.IsTool(item)?ItemCatalog.ToolDurability(item):0};tools.Add(tool);pile.ToolId=tool.Id;}
            GroundRevision++;return pile;
        }
        private void RemoveGround(GroundPile pile,int count)
        {
            pile.Amount-=count;for(int x=pile.X;x<pile.X+pile.Width;x++)groundCapacity[(x,pile.Y)]=CellTicks(x,pile.Y)-count*ItemCatalog.CapacityTicks(pile.Item)/pile.Width;
            if(pile.Amount==0)ground.Remove(pile);GroundRevision++;
        }
    }
}
