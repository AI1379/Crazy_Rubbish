using System;
using System.Collections.Generic;
namespace CrazyRubbish.Core
{
    public sealed partial class PrototypeSimulation
    {
        private static int CellCoordinate(double position)=>(int)Math.Floor(position+0.5);
        public bool IsWalkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Settings.mapWidth || y >= Settings.mapHeight) return false;
            if (InBuilding(x,y,11,2,3,3) || InBuilding(x,y,16,2,3,3) || InBuilding(x,y,2,20) || (x == 20 && y == 5)) return false;
            return !(BenchBuilt && InBuilding(x,y,BenchX,BenchY+1) ||
                TableBuilt && InBuilding(x,y,TableX,TableY+1) ||
                DismantleBuilt && InBuilding(x,y,DismantleX,DismantleY+1) ||
                GeneratorBuilt && InBuilding(x,y,GeneratorX,GeneratorY+1));
        }
        public int[] FindPath(double fromX, double fromY, int tx, int ty)
        {
            int sx=CellCoordinate(fromX), sy=CellCoordinate(fromY);
            if (!IsWalkable(sx,sy) || !IsWalkable(tx,ty)) return null;
            int start=sy*Settings.mapWidth+sx, target=ty*Settings.mapWidth+tx;
            var queue=new Queue<int>(); var previous=new Dictionary<int,int>();
            queue.Enqueue(start); previous[start]=-1;
            int[] dx={0,-1,1,0}, dy={-1,0,0,1};
            while(queue.Count>0)
            {
                int at=queue.Dequeue();
                if(at==target)
                {
                    var result=new List<int>();
                    while(at!=start) { result.Add(at); at=previous[at]; }
                    result.Reverse(); return result.ToArray();
                }
                int x=at%Settings.mapWidth,y=at/Settings.mapWidth;
                for(int i=0;i<4;i++)
                {
                    int nx=x+dx[i],ny=y+dy[i],next=ny*Settings.mapWidth+nx;
                    if(IsWalkable(nx,ny) && !previous.ContainsKey(next)) { previous[next]=at;queue.Enqueue(next); }
                }
            }
            return null;
        }
        private bool RecoverPosition(double x,double y,out double recoveredX,out double recoveredY)
        {
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y))
                throw new ArgumentException("存档中的角色位置无效。");
            recoveredX=x;recoveredY=y;
            if(IsWalkable(CellCoordinate(x),CellCoordinate(y)))return false;
            double closest=double.PositiveInfinity;
            for(int cy=0;cy<Settings.mapHeight;cy++)for(int cx=0;cx<Settings.mapWidth;cx++)
            {
                if(!IsWalkable(cx,cy))continue;
                double distance=(cx-x)*(cx-x)+(cy-y)*(cy-y);
                if(distance>=closest)continue;
                closest=distance;recoveredX=cx;recoveredY=cy;
            }
            if(double.IsInfinity(closest))throw new InvalidOperationException("地图没有可供角色站立的位置。");
            return true;
        }
        private void RecoverBlockedActors()
        {
            // Older saves may contain actors enclosed by construction. Keep their cargo and jobs.
            if(RecoverPosition(PlayerX,PlayerY,out double x,out double y))
            {PlayerX=x;PlayerY=y;manualStation=0;ReleaseUnusedTools();}
            foreach(var person in residents)
            {
                if(!RecoverPosition(person.X,person.Y,out x,out y))continue;
                person.X=x;person.Y=y;person.Path=null;person.PathIndex=0;person.PathRevision=-1;
                if(person.Delivery!=null)person.Delivery.RetryAt=0;
            }
        }
        private void WalkResident(Resident person,int tx,int ty,double seconds)
        {
            if(person.Path==null || person.PathTargetX!=tx || person.PathTargetY!=ty || person.PathRevision!=geometryRevision)
            {
                person.Path=FindPath(person.X,person.Y,tx,ty); person.PathIndex=0;
                person.PathTargetX=tx;person.PathTargetY=ty;person.PathRevision=geometryRevision;
            }
            if(person.Path==null) return;
            double distanceLeft=Settings.residentMoveSpeed*seconds;
            while(distanceLeft>0 && person.PathIndex<person.Path.Length)
            {
                int cell=person.Path[person.PathIndex]; double x=cell%Settings.mapWidth,y=cell/Settings.mapWidth;
                double distance=Distance(person.X,person.Y,x,y);
                double move=Math.Min(distance,distanceLeft);
                if(distance>0) { person.X+=(x-person.X)*move/distance;person.Y+=(y-person.Y)*move/distance; }
                distanceLeft-=move;
                if(distance<0.000001 || move>=distance-0.000001) { person.X=x;person.Y=y;person.PathIndex++; }
                else break;
            }
        }
    }
}
