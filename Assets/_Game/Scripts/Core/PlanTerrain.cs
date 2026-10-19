using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    // Cell ownership, movement and rendering share this mask. The old rectangles are source metadata only.
    public static class PlanTerrain
    {
        public const int Width=250,Height=250,Revision=6;
        private static readonly int[][][] Contours={
            new[]{new[]{0,0},new[]{8,0},new[]{9,1},new[]{19,1},new[]{20,2},new[]{25,2},new[]{27,6},new[]{27,10},new[]{25,11},new[]{27,13},new[]{27,17},new[]{26,19},new[]{26,24},new[]{24,26},new[]{23,28},new[]{18,29},new[]{13,29},new[]{10,28},new[]{7,27},new[]{6,25},new[]{2,25},new[]{0,23}},
            new[]{new[]{28,4},new[]{34,3},new[]{39,1},new[]{42,0},new[]{50,0},new[]{53,3},new[]{54,7},new[]{54,10},new[]{53,13},new[]{54,17},new[]{53,21},new[]{53,24},new[]{50,27},new[]{46,29},new[]{41,30},new[]{36,29},new[]{32,27},new[]{28,24},new[]{28,18},new[]{26,17},new[]{26,12},new[]{28,11}},
            new[]{new[]{52,4},new[]{60,2},new[]{68,3},new[]{76,5},new[]{82,8},new[]{84,11},new[]{84,19},new[]{82,23},new[]{78,27},new[]{73,29},new[]{67,31},new[]{60,30},new[]{55,28},new[]{52,26}}
        };
        // Northern annexes extend the existing rooms; revision 2 ground is never removed.
        private static readonly int[][][] Annexes={
            new[]{new[]{8,27},new[]{23,27},new[]{25,30},new[]{29,34},new[]{28,42},new[]{25,44},new[]{26,51},new[]{23,56},new[]{17,59},new[]{9,57},new[]{4,52},new[]{2,45},new[]{3,39},new[]{7,35}},
            new[]{new[]{35,27},new[]{48,27},new[]{53,30},new[]{58,34},new[]{60,40},new[]{58,44},new[]{59,50},new[]{55,56},new[]{48,60},new[]{40,59},new[]{34,55},new[]{31,49},new[]{32,43},new[]{30,38},new[]{32,32}},
            new[]{new[]{58,27},new[]{76,26},new[]{82,24},new[]{89,27},new[]{96,34},new[]{98,42},new[]{94,49},new[]{88,53},new[]{85,58},new[]{76,61},new[]{67,58},new[]{63,52},new[]{64,45},new[]{61,40},new[]{57,35}}
        };
        public sealed class Feature
        {
            public readonly string kind;public readonly int region;public readonly int[][] outline;
            public Feature(string kind,int region,params int[][] outline){this.kind=kind;this.region=region;this.outline=outline;}
            public bool Covers(int x,int y)=>Contains(outline,x,y);
            public int MinX=>outline.Min(p=>p[0]);public int MaxX=>outline.Max(p=>p[0]);
            public int MinY=>outline.Min(p=>p[1]);public int MaxY=>outline.Max(p=>p[1]);
        }
        // These solid footprints live entirely in new annexes, preserving saved factories.
        private static readonly Feature[] LegacyFeatures={
            new Feature("pillar",1,new[]{9,36},new[]{11,35},new[]{13,37},new[]{12,40},new[]{9,40},new[]{8,38}),
            new Feature("ridge",1,new[]{18,33},new[]{21,34},new[]{22,39},new[]{20,42},new[]{18,40}),
            new Feature("pillar",1,new[]{16,47},new[]{19,46},new[]{21,48},new[]{20,51},new[]{17,52},new[]{15,50}),
            new Feature("rubble",1,new[]{7,47},new[]{9,46},new[]{10,48},new[]{8,50},new[]{6,49}),
            new Feature("pillar",2,new[]{38,34},new[]{41,33},new[]{43,36},new[]{41,39},new[]{38,38}),
            new Feature("ridge",2,new[]{49,37},new[]{52,37},new[]{54,41},new[]{53,46},new[]{51,48},new[]{49,45}),
            new Feature("pillar",2,new[]{39,48},new[]{42,46},new[]{45,48},new[]{44,52},new[]{40,53},new[]{38,51}),
            new Feature("rubble",2,new[]{50,53},new[]{52,52},new[]{54,53},new[]{53,55},new[]{50,55}),
            new Feature("ridge",3,new[]{67,33},new[]{70,34},new[]{72,39},new[]{71,44},new[]{69,46},new[]{67,42}),
            new Feature("pillar",3,new[]{82,33},new[]{85,32},new[]{88,35},new[]{86,39},new[]{82,38}),
            new Feature("pillar",3,new[]{77,48},new[]{80,46},new[]{83,49},new[]{82,53},new[]{78,54},new[]{76,51}),
            new Feature("rubble",3,new[]{89,43},new[]{92,42},new[]{94,44},new[]{92,46},new[]{89,46})
        };
        private static readonly int[][][] FrontierContours={
            new[]{new[]{7,55},new[]{24,55},new[]{30,65},new[]{28,88},new[]{38,103},new[]{65,107},new[]{77,119},new[]{78,147},new[]{71,157},new[]{79,175},new[]{77,215},new[]{69,237},new[]{50,244},new[]{22,239},new[]{7,224},new[]{2,198},new[]{6,175},new[]{2,150},new[]{7,126},new[]{4,107},new[]{11,89},new[]{7,73}},
            new[]{new[]{38,55},new[]{56,55},new[]{61,65},new[]{76,76},new[]{88,91},new[]{116,99},new[]{142,103},new[]{159,117},new[]{162,141},new[]{154,160},new[]{163,181},new[]{160,219},new[]{146,240},new[]{119,246},new[]{94,239},new[]{81,224},new[]{79,197},new[]{84,173},new[]{78,149},new[]{83,129},new[]{76,111},new[]{61,100},new[]{46,84},new[]{38,69}},
            new[]{new[]{78,55},new[]{94,53},new[]{109,60},new[]{132,68},new[]{161,77},new[]{178,89},new[]{207,96},new[]{232,109},new[]{245,133},new[]{247,163},new[]{240,185},new[]{246,209},new[]{236,233},new[]{216,246},new[]{188,242},new[]{171,230},new[]{164,210},new[]{168,186},new[]{163,166},new[]{167,141},new[]{161,117},new[]{148,102},new[]{127,89},new[]{103,80},new[]{88,70}}
        };
        public enum Surface { Rock, Soil, Gravel, Mud, ShallowWater, DeepWater }
        private static readonly byte[] Regions=BuildMask();
        private static readonly bool[] LegacyOpen=BuildLegacyOpen();
        private static readonly byte[] Water=BuildWater();
        private static readonly bool[] Bridges=BuildBridges();
        private static readonly Surface[] Surfaces=BuildSurfaces();
        private static readonly Feature[] Features=BuildFeatures();
        private static readonly Feature[] FeatureCells=BuildFeatureMask();
        private static readonly bool[] Barriers=BuildBarriers();
        private static readonly int[] WaterBodies=BuildWaterBodies();
        private static readonly (int x,int y)[][] RegionCells=BuildRegionCells();
        public static IEnumerable<Feature> Obstacles=>Features;
        private static Feature[] BuildFeatureMask()
        {var cells=new Feature[Width*Height];foreach(var f in Features)for(int y=f.MinY;y<=f.MaxY;y++)for(int x=f.MinX;x<=f.MaxX;x++)if(f.Covers(x,y))cells[y*Width+x]=f;return cells;}
        private static byte[] BuildMask()
        {
            var cells=new byte[Width*Height];var queue=new Queue<int>();for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int region=0;for(int i=0;i<Contours.Length;i++)if(Contains(Contours[i],x,y)||Contains(Annexes[i],x,y)){region=i+1;break;}if(region==0)for(int i=0;i<FrontierContours.Length;i++)if(Contains(FrontierContours[i],x,y)){region=i+1;break;}int cell=y*Width+x;cells[cell]=(byte)region;if(region>0)queue.Enqueue(cell);}
            // Complete the square by growing the existing districts through former voids.
            // Region assignment is independent of rivers and of the physical rock barriers.
            while(queue.Count>0){int at=queue.Dequeue(),x=at%Width,y=at/Width;foreach(int next in new[]{at-1,at+1,at-Width,at+Width})if(next>=0&&next<cells.Length&&Math.Abs(next%Width-x)+Math.Abs(next/Width-y)==1&&cells[next]==0){cells[next]=cells[at];queue.Enqueue(next);}}
            for(int y=20;y<=21;y++)for(int x=2;x<=3;x++)cells[y*Width+x]=0;return cells;
        }
        private static bool[] BuildLegacyOpen()
        {var cells=new bool[Width*Height];for(int y=0;y<64;y++)for(int x=0;x<100;x++)if(Regions[y*Width+x]>0&&!LegacyFeatures.Any(f=>f.Covers(x,y)))cells[y*Width+x]=Contours.Any(p=>Contains(p,x,y))||Annexes.Any(p=>Contains(p,x,y));return cells;}
        private static bool Contains(int[][] polygon,int x,int y)
        {
            bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
            {
                var a=polygon[i];var b=polygon[j];int cross=(x-a[0])*(b[1]-a[1])-(y-a[1])*(b[0]-a[0]);
                if(cross==0&&x>=Math.Min(a[0],b[0])&&x<=Math.Max(a[0],b[0])&&y>=Math.Min(a[1],b[1])&&y<=Math.Max(a[1],b[1]))return true;
                if((a[1]>y)!=(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(double)(b[1]-a[1])+a[0])inside=!inside;
            }return inside;
        }
        public static int ChamberAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?Regions[y*Width+x]:0;
        public static Feature ObstacleAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?FeatureCells[y*Width+x]:null;
        public static int RegionAt(int x,int y)=>ObstacleAt(x,y)==null&&!BarrierAt(x,y)&&(WaterAt(x,y)!=2||BridgeAt(x,y))?ChamberAt(x,y):0;
        public static int WaterAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?Water[y*Width+x]:0;
        public static Surface SurfaceAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?Surfaces[y*Width+x]:Surface.Rock;
        public static bool BridgeAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height&&Bridges[y*Width+x];
        public static bool BarrierAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height&&Barriers[y*Width+x];
        public static int BarrierCellCount=>Barriers.Count(v=>v);
        public static int SquareTerrainCellCount=>Regions.Count(r=>r>0);
        public static bool Buildable(int x,int y)=>RegionAt(x,y)>0&&(WaterAt(x,y)==0||BridgeAt(x,y));
        public static double MoveCost(int x,int y)=>BridgeAt(x,y)?1:WaterAt(x,y)==1?2.2:SurfaceAt(x,y)==Surface.Mud?1.3:1;
        public static bool Available(int x,int y,int unlocked)=>RegionAt(x,y)>0&&RegionAt(x,y)<=unlocked;
        public static IEnumerable<(int x,int y)> Cells(int region)
        {return region>=1&&region<=3?RegionCells[region-1]:new (int,int)[0];}
        private static (int x,int y)[][] BuildRegionCells()
        {var groups=new[]{new List<(int,int)>(),new List<(int,int)>(),new List<(int,int)>()};for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int r=RegionAt(x,y);if(r>0)groups[r-1].Add((x,y));}return groups.Select(g=>g.ToArray()).ToArray();}
        public static int CellCount(int region)=>Cells(region).Count(c=>Buildable(c.x,c.y));
        public static int WaterCellCount(int region)=>Enumerable.Range(0,Width*Height).Count(c=>Regions[c]==region&&Water[c]>0);
        public static (int x,int y) Center(int region)
        {var cells=Cells(region).Where(c=>Buildable(c.x,c.y)).ToArray();double x=cells.Average(c=>c.x),y=cells.Average(c=>c.y);return cells.OrderBy(c=>(c.x-x)*(c.x-x)+(c.y-y)*(c.y-y)).First();}
        public static (int x,int y) NearestOpen(double x,double y,int unlocked)
        {return Enumerable.Range(1,unlocked).SelectMany(Cells).OrderBy(c=>(c.x-x)*(c.x-x)+(c.y-y)*(c.y-y)).ThenBy(c=>c.y).ThenBy(c=>c.x).First();}
        private static uint Hash(int x,int y){unchecked{uint n=(uint)(x*374761393+y*668265263+9137);n=(n^(n>>13))*1274126177;return n^(n>>16);}}
        private static double SegmentDistance(int x,int y,int[] a,int[] b)
        {double dx=b[0]-a[0],dy=b[1]-a[1],t=Math.Max(0,Math.Min(1,((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)));dx=x-a[0]-dx*t;dy=y-a[1]-dy*t;return Math.Sqrt(dx*dx+dy*dy);}
        private static byte[] BuildWater()
        {
            var score=new double[Width*Height];
            PaintRiver(score,new[]{new[]{30,256},new[]{28,225},new[]{65,195},new[]{70,166},new[]{108,143},new[]{140,120},new[]{147,85},new[]{190,53},new[]{219,20},new[]{234,-9}},5.0);
            PaintRiver(score,new[]{new[]{256,210},new[]{223,211},new[]{206,188},new[]{181,180},new[]{165,151},new[]{140,136},new[]{108,143}},3.6);
            PaintRiver(score,new[]{new[]{-8,126},new[]{25,119},new[]{43,129},new[]{65,136},new[]{86,135},new[]{108,143}},3.4);
            PaintRiver(score,new[]{new[]{48,207},new[]{55,208},new[]{64,207}},2.4);
            PaintRiver(score,new[]{new[]{148,89},new[]{136,93},new[]{120,88}},2.4);
            int[][] ponds={new[]{64,207,8,5},new[]{198,184,9,6},new[]{120,88,7,5}};
            foreach(var p in ponds)for(int y=p[1]-p[3];y<=p[1]+p[3];y++)for(int x=p[0]-p[2];x<=p[0]+p[2];x++){double d=Math.Sqrt(Math.Pow((x-p[0])/(double)p[2],2)+Math.Pow((y-p[1])/(double)p[3],2));if(d<=1)score[y*Width+x]=Math.Max(score[y*Width+x],1-d);}
            int[][] fords={new[]{65,195},new[]{108,143},new[]{147,85},new[]{219,20},new[]{206,188},new[]{43,129},new[]{25,119},new[]{140,120}};
            var water=new byte[Width*Height];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int cell=y*Width+x;if(score[cell]<=0||LegacyOpen[cell]||Regions[cell]==0)continue;bool ford=fords.Any(p=>(x-p[0])*(x-p[0])+(y-p[1])*(y-p[1])<=81);water[cell]=(byte)(score[cell]<.26||ford?1:2);}
            for(int y=21;y<=27;y++)for(int x=16;x<=24;x++)if(StarterPondAt(x,y))water[y*Width+x]=(byte)(Math.Pow((x-20)/3.2,2)+Math.Pow((y-24)/2.2,2)<.27?2:1);
            return water;
        }
        public static bool StarterPondAt(int x,int y)=>!(x>=22&&y<=23)&&Math.Pow((x-20)/3.2,2)+Math.Pow((y-24)/2.2,2)<=1;
        public static int StarterPondBody=>WaterBodyAt(20,24);
        public static int WaterBodyAt(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?WaterBodies[y*Width+x]:0;
        private static int[] BuildWaterBodies()
        {
            var bodies=new int[Width*Height];var queue=new Queue<int>();
            for(int first=0;first<bodies.Length;first++)
            {
                if(Water[first]==0||bodies[first]!=0)continue;int id=first+1;bodies[first]=id;queue.Enqueue(first);
                while(queue.Count>0){int at=queue.Dequeue();foreach(int next in new[]{at-1,at+1,at-Width,at+Width})if(next>=0&&next<bodies.Length&&bodies[next]==0&&Water[next]>0&&Math.Abs(next%Width-at%Width)+Math.Abs(next/Width-at/Width)==1){bodies[next]=id;queue.Enqueue(next);}}
            }
            return bodies;
        }
        private static void PaintRiver(double[] score,int[][] points,double width)
        {
            for(int i=0;i<points.Length-1;i++)for(int s=0;s<=40;s++)
            {
                double t=s/40.0,t2=t*t,t3=t2*t;var p0=points[Math.Max(0,i-1)];var p1=points[i];var p2=points[i+1];var p3=points[Math.Min(points.Length-1,i+2)];
                double cx=.5*(2*p1[0]+(-p0[0]+p2[0])*t+(2*p0[0]-5*p1[0]+4*p2[0]-p3[0])*t2+(-p0[0]+3*p1[0]-3*p2[0]+p3[0])*t3),cy=.5*(2*p1[1]+(-p0[1]+p2[1])*t+(2*p0[1]-5*p1[1]+4*p2[1]-p3[1])*t2+(-p0[1]+3*p1[1]-3*p2[1]+p3[1])*t3);
                double radius=width*(.90+.13*Math.Sin((i+t)*1.7)+.08*Math.Cos((i+t)*3.3));for(int y=Math.Max(0,(int)Math.Floor(cy-radius));y<=Math.Min(Height-1,cy+radius);y++)for(int x=Math.Max(0,(int)Math.Floor(cx-radius));x<=Math.Min(Width-1,cx+radius);x++){double d=Math.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));if(d<radius)score[y*Width+x]=Math.Max(score[y*Width+x],1-d/radius);}
            }
        }
        private static bool[] BuildBridges()
        {var cells=new bool[Width*Height];int[][] decks={new[]{70,166,8,1},new[]{165,151,1,8},new[]{190,53,8,1}};foreach(var p in decks)for(int y=p[1]-p[3];y<=p[1]+p[3];y++)for(int x=p[0]-p[2];x<=p[0]+p[2];x++)cells[y*Width+x]=true;return cells;}
        public static double Noise(double x,double y)
        {int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double tx=x-ix,ty=y-iy;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);double a=(Hash(ix,iy)%65536)/65535.0,b=(Hash(ix+1,iy)%65536)/65535.0,c=(Hash(ix,iy+1)%65536)/65535.0,d=(Hash(ix+1,iy+1)%65536)/65535.0;return (a+(b-a)*tx)*(1-ty)+(c+(d-c)*tx)*ty;}
        private static Surface[] BuildSurfaces()
        {
            var values=new Surface[Width*Height];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                int cell=y*Width+x;if(Water[cell]>0){values[cell]=Water[cell]==1?Surface.ShallowWater:Surface.DeepWater;continue;}
                int bank=9;if(Regions[cell]>0)for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++)if(WaterAt(x+dx,y+dy)>0)bank=Math.Min(bank,Math.Max(Math.Abs(dx),Math.Abs(dy)));
                double soil=Noise(x*.023,y*.023)*.62+Noise(x*.061+17,y*.061-9)*.28+Noise(x*.14,y*.14)*.1;values[cell]=Bridges[cell]?Surface.Rock:bank<=1?Surface.Mud:bank<=3?Surface.Gravel:soil>.56?Surface.Soil:soil<.39?Surface.Rock:Surface.Gravel;
            }return values;
        }
        private static Feature[] BuildFeatures()
        {
            var features=new List<Feature>(LegacyFeatures);for(int y=84;y<241;y+=17)for(int x=12;x<242;x+=19)
            {
                uint h=Hash(x,y);if(h%4==0)continue;int cx=x+(int)(h%11)-5,cy=y+(int)((h>>4)%11)-5,r=ChamberAt(cx,cy);if(r==0)continue;string kind=new[]{"pillar","ridge","rubble"}[(h>>7)%3];int rx=kind=="ridge"?2:3,ry=kind=="ridge"?5:kind=="rubble"?2:3;
                bool clear=true;for(int py=cy-ry-3;py<=cy+ry+3&&clear;py++)for(int px=cx-rx-3;px<=cx+rx+3;px++)if(ChamberAt(px,py)!=r||WaterAt(px,py)>0){clear=false;break;}if(!clear)continue;
                features.Add(new Feature(kind,r,new[]{cx-rx,cy-ry+1},new[]{cx,cy-ry},new[]{cx+rx,cy-ry+1},new[]{cx+rx,cy+ry-1},new[]{cx,cy+ry},new[]{cx-rx,cy+ry-1}));
            }return features.ToArray();
        }
        private static bool[] BuildBarriers()
        {
            var edges=new List<(int x,int y)>();for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int r=ChamberAt(x,y);if(r>0&&(ChamberAt(x+1,y)>0&&ChamberAt(x+1,y)!=r||ChamberAt(x,y+1)>0&&ChamberAt(x,y+1)!=r))edges.Add((x,y));}
            var openings=new List<(int x,int y)>{(27,14)};for(int gy=14;gy<Height;gy+=46)for(int gx=18;gx<Width;gx+=46){var candidates=edges.Where(c=>(c.x-gx)*(c.x-gx)+(c.y-gy)*(c.y-gy)<900).OrderBy(c=>(c.x-gx)*(c.x-gx)+(c.y-gy)*(c.y-gy)).ToArray();if(candidates.Length>0&&!openings.Any(p=>(p.x-candidates[0].x)*(p.x-candidates[0].x)+(p.y-candidates[0].y)*(p.y-candidates[0].y)<484))openings.Add(candidates[0]);}
            var cells=new bool[Width*Height];foreach(var edge in edges){int radius=1+(int)(Noise(edge.x*.12,edge.y*.12)*2.9);for(int y=Math.Max(0,edge.y-radius);y<=Math.Min(Height-1,edge.y+radius);y++)for(int x=Math.Max(0,edge.x-radius);x<=Math.Min(Width-1,edge.x+radius);x++){int cell=y*Width+x;if(!LegacyOpen[cell]&&Water[cell]==0&&!Bridges[cell]&&!openings.Any(p=>(x-p.x)*(x-p.x)+(y-p.y)*(y-p.y)<=36))cells[cell]=true;}}
            RemoveTinyPockets(cells);return cells;
        }
        private static void RemoveTinyPockets(bool[] barriers)
        {
            // Rasterized banks and ridge corners can leave a handful of unreachable pixels.
            // Never remove a sizeable area or any protected legacy floor to mask a broken layout.
            for(int level=1;level<=3;level++)
            {
                var seen=new bool[Width*Height];for(int start=0;start<seen.Length;start++)
                {
                    if(seen[start]||!PocketOpen(start,level,barriers))continue;var component=new List<int>();var queue=new Queue<int>();queue.Enqueue(start);seen[start]=true;
                    while(queue.Count>0){int at=queue.Dequeue();component.Add(at);foreach(int next in new[]{at-1,at+1,at-Width,at+Width})if(next>=0&&next<seen.Length&&!seen[next]&&Math.Abs(next%Width-at%Width)+Math.Abs(next/Width-at/Width)==1&&PocketOpen(next,level,barriers)){seen[next]=true;queue.Enqueue(next);}}
                    if(component.Count<=32&&component.All(c=>Water[c]>0&&!Bridges[c])){foreach(int cell in component){Water[cell]=2;Surfaces[cell]=Surface.DeepWater;}continue;}
                    if(component.Count>8||component.Any(c=>LegacyOpen[c])||component.Any(c=>Bridges[c]&&Water[c]>0))continue;
                    foreach(int cell in component)if(Bridges[cell]&&Water[cell]==0)Bridges[cell]=false;
                    foreach(int cell in component)if(Water[cell]>0){Water[cell]=2;Surfaces[cell]=Surface.DeepWater;}else barriers[cell]=true;
                }
            }
        }
        private static bool PocketOpen(int cell,int level,bool[] barriers)=>Regions[cell]>0&&Regions[cell]<=level&&FeatureCells[cell]==null&&!barriers[cell]&&(Water[cell]!=2||Bridges[cell]);
    }
}
