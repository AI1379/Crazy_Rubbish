using System;
using System.Collections.Generic;
using System.Linq;

namespace CrazyRubbish.Core
{
    public sealed partial class PlanSimulation
    {
        private readonly HashSet<int> terrainClearCells=new HashSet<int>();
        public bool TerrainCleared(int x,int y)=>x>=0&&y>=0&&x<PlanTerrain.Width&&y<PlanTerrain.Height&&terrainClearCells.Contains(y*PlanTerrain.Width+x);
        public bool TerrainBuildable(int x,int y)=>PlanTerrain.Buildable(x,y)||TerrainCleared(x,y);
        public double TerrainMoveCost(int x,int y)=>TerrainCleared(x,y)?1:PlanTerrain.MoveCost(x,y);
        private void RestoreTerrainClearings()
        {
            if(State.terrainClearings==null)State.terrainClearings=new List<PlanTerrainClearance>();
            if(State.mapRevision<6)
            {
                foreach(var site in State.sites){var size=Size(site);State.terrainClearings.Add(new PlanTerrainClearance{x=site.x,y=site.y,width=size.width,height=size.height});var front=Front(site);State.terrainClearings.Add(new PlanTerrainClearance{x=front.x,y=front.y});}
                foreach(var g in State.ground)State.terrainClearings.Add(new PlanTerrainClearance{x=g.x,y=g.y,width=g.width});
            }
            foreach(var plot in State.terrainClearings)for(int y=plot.y;y<plot.y+plot.height;y++)for(int x=plot.x;x<plot.x+plot.width;x++)if(x>=0&&y>=0&&x<PlanTerrain.Width&&y<PlanTerrain.Height)terrainClearCells.Add(y*PlanTerrain.Width+x);
        }
    }
}
