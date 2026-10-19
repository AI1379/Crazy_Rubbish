using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private readonly bool[] navigationBlocked=new bool[PlanTerrain.Width*PlanTerrain.Height];
        private readonly int[] routeSeen=new int[PlanTerrain.Width*PlanTerrain.Height],routeClosed=new int[PlanTerrain.Width*PlanTerrain.Height],routeCost=new int[PlanTerrain.Width*PlanTerrain.Height],routeParent=new int[PlanTerrain.Width*PlanTerrain.Height];
        private readonly Dictionary<(int,int),int[]> routeCache=new Dictionary<(int,int),int[]>();
        private readonly Dictionary<PlanPerson,Journey> journeys=new Dictionary<PlanPerson,Journey>();
        private readonly List<OpenNode> routeHeap=new List<OpenNode>();
        private long navigationSignature=long.MinValue;private int searchStamp;
        public int NavigationSearches {get;private set;}
        public int LastSearchExpanded {get;private set;}
        private struct OpenNode {public int cell,cost,estimate;}
        private sealed class Journey {public int x,y,index;public int[] cells;public double lastX,lastY;}
        private void RefreshNavigation()
        {
            long signature=State.region;unchecked{foreach(var s in State.sites)if(!s.pending&&!Overlay(s.type)){signature=signature*397^s.id;signature=signature*397^s.x;signature=signature*397^s.y;signature=signature*397^s.rotation;signature=signature*397^s.type.GetHashCode();}}
            if(signature==navigationSignature)return;navigationSignature=signature;Array.Clear(navigationBlocked,0,navigationBlocked.Length);routeCache.Clear();journeys.Clear();
            foreach(var s in State.sites)if(!s.pending&&!Overlay(s.type)){var size=Size(s);for(int y=Math.Max(0,s.y);y<Math.Min(PlanTerrain.Height,s.y+size.height);y++)for(int x=Math.Max(0,s.x);x<Math.Min(PlanTerrain.Width,s.x+size.width);x++)navigationBlocked[y*PlanTerrain.Width+x]=true;}
        }
        private bool NavigationOpen(int x,int y)=>Owned(x,y)&&!navigationBlocked[y*PlanTerrain.Width+x];
        public bool Walkable(int x,int y){RefreshNavigation();return NavigationOpen(x,y);}
        private static bool Earlier(OpenNode a,OpenNode b)=>a.estimate<b.estimate||a.estimate==b.estimate&&(a.cost>b.cost||a.cost==b.cost&&a.cell<b.cell);
        private void Push(OpenNode node)
        {int n=routeHeap.Count;routeHeap.Add(node);while(n>0){int p=(n-1)/2;if(!Earlier(node,routeHeap[p]))break;routeHeap[n]=routeHeap[p];n=p;}routeHeap[n]=node;}
        private OpenNode Pop()
        {var first=routeHeap[0];var last=routeHeap[routeHeap.Count-1];routeHeap.RemoveAt(routeHeap.Count-1);if(routeHeap.Count==0)return first;int n=0;while(n*2+1<routeHeap.Count){int c=n*2+1;if(c+1<routeHeap.Count&&Earlier(routeHeap[c+1],routeHeap[c]))c++;if(!Earlier(routeHeap[c],last))break;routeHeap[n]=routeHeap[c];n=c;}routeHeap[n]=last;return first;}
        public int[] Path(double ax,double ay,int bx,int by)
        {
            RefreshNavigation();int sx=(int)Math.Round(ax),sy=(int)Math.Round(ay);
            if(!NavigationOpen(sx,sy)||!NavigationOpen(bx,by))return null;
            int start=sy*PlanTerrain.Width+sx,target=by*PlanTerrain.Width+bx;if(start==target)return Array.Empty<int>();
            if(routeCache.TryGetValue((start,target),out var cached))return cached;
            NavigationSearches++;LastSearchExpanded=0;if(++searchStamp==int.MaxValue){Array.Clear(routeSeen,0,routeSeen.Length);Array.Clear(routeClosed,0,routeClosed.Length);searchStamp=1;}
            routeHeap.Clear();routeSeen[start]=searchStamp;routeCost[start]=0;routeParent[start]=-1;Push(new OpenNode{cell=start,cost=0,estimate=(Math.Abs(sx-bx)+Math.Abs(sy-by))*10});
            while(routeHeap.Count>0)
            {
                var node=Pop();int at=node.cell;if(routeClosed[at]==searchStamp||node.cost!=routeCost[at])continue;
                if(at==target){var path=new List<int>();while(at!=start){path.Add(at);at=routeParent[at];}path.Reverse();var found=path.ToArray();if(routeCache.Count>=128)routeCache.Clear();routeCache[(start,target)]=found;return found;}
                routeClosed[at]=searchStamp;LastSearchExpanded++;int x=at%PlanTerrain.Width,y=at/PlanTerrain.Width;
                Visit(x-1,y,at,target);Visit(x+1,y,at,target);Visit(x,y-1,at,target);Visit(x,y+1,at,target);
            }
            if(routeCache.Count>=128)routeCache.Clear();routeCache[(start,target)]=null;return null;
        }
        private void Visit(int x,int y,int from,int target)
        {
            if(!NavigationOpen(x,y))return;int cell=y*PlanTerrain.Width+x;if(routeClosed[cell]==searchStamp)return;int cost=routeCost[from]+(int)Math.Round(10*TerrainMoveCost(x,y));
            if(routeSeen[cell]==searchStamp&&routeCost[cell]<=cost)return;routeSeen[cell]=searchStamp;routeCost[cell]=cost;routeParent[cell]=from;
            Push(new OpenNode{cell=cell,cost=cost,estimate=cost+(Math.Abs(x-target%PlanTerrain.Width)+Math.Abs(y-target/PlanTerrain.Width))*10});
        }
        public bool Walk(PlanPerson p,int x,int y,double dt)
        {
            if(p.id==0&&HeroAboard)return false;
            RefreshNavigation();if(!journeys.TryGetValue(p,out var trip)||trip.x!=x||trip.y!=y||Math.Abs(trip.lastX-p.x)+Math.Abs(trip.lastY-p.y)>1e-7)
            {
                var path=Path(p.x,p.y,x,y);if(path==null)return false;int start=(int)Math.Round(p.y)*PlanTerrain.Width+(int)Math.Round(p.x);
                trip=new Journey{x=x,y=y,cells=new[]{start}.Concat(path).ToArray(),lastX=p.x,lastY=p.y};journeys[p]=trip;
            }
            double remaining=Config.Rule("workers.move_grid_per_second")*dt;
            while(trip.index<trip.cells.Length)
            {
                int cell=trip.cells[trip.index],tx=cell%PlanTerrain.Width,ty=cell/PlanTerrain.Width;double dx=tx-p.x,dy=ty-p.y,distance=Math.Sqrt(dx*dx+dy*dy);
                if(distance<1e-8){trip.index++;continue;}double cost=TerrainMoveCost(tx,ty),step=Math.Min(distance,remaining/cost);p.x+=dx*step/distance;p.y+=dy*step/distance;remaining-=step*cost;if(step>=distance-1e-8)trip.index++;if(remaining<=1e-9)break;
            }
            trip.lastX=p.x;trip.lastY=p.y;return true;
        }
    }
}
