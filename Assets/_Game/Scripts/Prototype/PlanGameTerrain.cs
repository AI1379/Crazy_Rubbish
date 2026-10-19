using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private readonly List<KeyValuePair<int,SpriteRenderer>> caveGates=new List<KeyValuePair<int,SpriteRenderer>>();
        private readonly HashSet<int> renderedCaveSprings=new HashSet<int>();
        private static uint TerrainHash(int x,int y)
        {unchecked{uint n=(uint)(x*374761393+y*668265263+9137);n=(n^(n>>13))*1274126177;return n^(n>>16);}}
        private void BuildCaveTerrain()
        {
            ClearTerrainMeshes();caveGates.Clear();renderedCaveSprings.Clear();var dark=Draw("Deep cave backdrop",appearance.marker,floorRoot,PlanTerrain.Width/2f,PlanTerrain.Height/2f,1,-50);dark.transform.localScale=new Vector3((PlanTerrain.Width+160)/dark.sprite.bounds.size.x,(PlanTerrain.Height+120)/dark.sprite.bounds.size.y,1);dark.color=new Color32(26,28,26,255);mapCamera.backgroundColor=new Color32(26,28,26,255);
            var distance=new Dictionary<Vector2Int,int>();var queue=new Queue<Vector2Int>();
            for(int y=0;y<PlanTerrain.Height;y++)for(int x=0;x<PlanTerrain.Width;x++)if(PlanTerrain.ChamberAt(x,y)>0){var cell=new Vector2Int(x,y);distance[cell]=0;queue.Enqueue(cell);}
            var directions=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(queue.Count>0){var at=queue.Dequeue();if(distance[at]>=8)continue;foreach(var direction in directions){var next=at+direction;if(!distance.ContainsKey(next)){distance[next]=distance[at]+1;queue.Enqueue(next);}}}
            var wallRoot=Root("Natural cave walls",planWorld);
            foreach(var pair in distance.OrderBy(p=>p.Key.y).ThenBy(p=>p.Key.x))
            {
                int x=pair.Key.x,y=pair.Key.y,region=PlanTerrain.ChamberAt(x,y);if(x>=2&&x<=3&&y>=20&&y<=21)region=1;uint hash=TerrainHash(x,y);var tiles=appearance.bedrockTiles;
                Color tint=Color.white;
                if(region>0)
                {
                    switch(PlanTerrain.SurfaceAt(x,y))
                    {
                        case PlanTerrain.Surface.Soil:tiles=appearance.compactedTiles;tint=new Color(.83f,.75f,.61f);break;
                        case PlanTerrain.Surface.Mud:tiles=appearance.compactedTiles;tint=new Color(.67f,.62f,.50f);break;
                        case PlanTerrain.Surface.Gravel:tiles=appearance.gravelTiles;tint=new Color(.94f,.92f,.85f);break;
                    }
                    float natural=(float)PlanTerrain.Noise(x*.08,y*.08);tint*=.94f+natural*.10f;tint.a=1;
                    if(x<100&&y<32&&Model.Config.regions.Any(r=>PlanSimulation.InRect(x,y,r.garbage_rectangle)))tiles=appearance.gravelTiles;
                }
                else {float fade=1-pair.Value/8f;tint=new Color(.42f,.42f,.40f,fade*fade);}
                TerrainSprite(tiles[((y&3)*4)+(x&3)],x,y,1,region,region>0?-20:-30,tint,true);
                int water=PlanTerrain.WaterAt(x,y);
                if(region>0&&water>0)
                {
                    float variation=(hash%7)*.006f;var color=water==1?new Color(.24f+variation,.43f+variation,.43f+variation):new Color(.10f+variation,.26f+variation,.32f+variation);
                    TerrainQuad(Texture2D.whiteTexture,new Rect(0,0,1,1),x,y,1,1,region,-18,color);
                    if(hash%4==0)TerrainQuad(Texture2D.whiteTexture,new Rect(0,0,1,1),x-.12f,y+(hash%3)*.12f,.38f,.055f,region,-17,new Color(.61f,.79f,.75f,water==1?.36f:.19f));
                }
                bool deck=PlanTerrain.BridgeAt(x,y)||Model.TerrainCleared(x,y)&&water>0;
                if(region>0&&deck)
                {
                    TerrainSprite(appearance.compactedTiles[((y&3)*4)+(x&3)],x,y,1,region,-14,new Color(.86f,.76f,.57f),true);
                    TerrainQuad(Texture2D.whiteTexture,new Rect(0,0,1,1),x,y+.35f,1,.055f,region,-13,new Color(.47f,.40f,.28f));
                }
                if(region>0&&PlanTerrain.BarrierAt(x,y)&&!Model.TerrainCleared(x,y))
                {
                    TerrainSprite(appearance.caveRocks[2],x,y,2.5f,region,-11,new Color(.88f,.86f,.78f),false,(hash&1)!=0);
                    if(hash%3==0)TerrainSprite(appearance.caveRocks[0],x,y+.15f,3.2f,region,-10,new Color(.88f,.86f,.78f),false,(hash&2)!=0);
                }
                if(region==0&&!(x>=2&&x<=3&&y>=20&&y<=21)&&appearance.caveRocks!=null&&appearance.caveRocks.Length==4&&(pair.Value==1||(pair.Value==2&&hash%7==0)))
                {int variant=pair.Value==2?3:(int)(hash%3);float size=pair.Value==2?1.7f:2.6f+(hash%11)*.055f;TerrainSprite(appearance.caveRocks[variant],x+(hash%5-2f)*.09f,y+((hash>>4)%5-2f)*.08f,size,0,-12,new Color(.83f,.81f,.76f),false,(hash&8)!=0);}
                if(region>1&&!PlanTerrain.BarrierAt(x,y)&&(water==0||deck)&&directions.Any(d=>PlanTerrain.RegionAt(x+d.x,y+d.y)>0&&PlanTerrain.RegionAt(x+d.x,y+d.y)<region)&&hash%2==0)
                {var gate=Draw("Unopened cave entrance "+region,appearance.caveRocks[(int)(hash%3)],wallRoot,x,y,2.8f,-9);gate.color=new Color(.65f,.65f,.62f);caveGates.Add(new KeyValuePair<int,SpriteRenderer>(region,gate));}
            }
            foreach(var feature in PlanTerrain.Obstacles)
            {
                float cx=(feature.MinX+feature.MaxX)/2f,cy=(feature.MinY+feature.MaxY)/2f;
                // Cover every solid cell with low rock before placing a taller silhouette above it.
                for(int y=feature.MinY;y<=feature.MaxY;y++)for(int x=feature.MinX;x<=feature.MaxX;x++)if(feature.Covers(x,y)&&!Model.TerrainCleared(x,y))
                {
                    uint h=TerrainHash(x,y);TerrainSprite(appearance.caveRocks[feature.kind=="rubble"?3:2],x,y,2.1f,feature.region,-11,Color.white,false,(h&1)!=0);
                }
                if(Enumerable.Range(feature.MinY,feature.MaxY-feature.MinY+1).Any(y=>Enumerable.Range(feature.MinX,feature.MaxX-feature.MinX+1).Any(x=>feature.Covers(x,y)&&Model.TerrainCleared(x,y))))continue;
                if(feature.kind=="ridge")
                {
                    for(int y=feature.MinY;y<=feature.MaxY;y+=2)
                    {var segment=Draw("Ridge segment "+cx+","+y,appearance.caveRocks[0],wallRoot,cx,y,(feature.MaxX-feature.MinX+1)*1.15f,-8);segment.flipX=(TerrainHash(feature.MinX,y)&1)!=0;caveGates.Add(new KeyValuePair<int,SpriteRenderer>(-feature.region,segment));}
                    continue;
                }
                var rockSprite=feature.kind=="pillar"?appearance.cavePillar:appearance.caveRocks[3];
                float size=feature.kind=="pillar"?(feature.MaxX-feature.MinX+1)*1.75f:Mathf.Max(feature.MaxX-feature.MinX+1,feature.MaxY-feature.MinY+1)*1.5f;
                var silhouette=Draw("Cave "+feature.kind+" "+cx+","+cy,rockSprite,wallRoot,cx,feature.kind=="pillar"?feature.MinY+size*.45f:cy,size,-8);
                silhouette.flipX=(TerrainHash(feature.MinX,feature.MinY)&1)!=0;caveGates.Add(new KeyValuePair<int,SpriteRenderer>(-feature.region,silhouette));
            }
            FinishTerrainMeshes();
            foreach(int region in new[]{1,2,3}){var center=PlanTerrain.Center(region);int north=PlanTerrain.Cells(region).Max(c=>c.y);WorldLabel(Model.Config.Region(region).name,planWorld,center.x,north-2,.10f);}
        }
        private void RefreshCaveRegions()
        {
            foreach(var pair in terrainRenderers)if(pair.Value.sortingOrder==-17)pair.Value.transform.localPosition=new Vector3(Mathf.Sin((float)Model.State.seconds*.6f)*.10f,0,0);
            if(floorRegion==Model.State.region)return;
            foreach(var pair in terrainRenderers)pair.Value.sharedMaterial.color=pair.Key<=Model.State.region?Color.white:new Color(.45f,.48f,.48f);
            if(minimapImage!=null)RefreshMinimap();
            foreach(var gate in caveGates){gate.Value.gameObject.SetActive(gate.Key<0||gate.Key>Model.State.region);if(gate.Key<0)gate.Value.color=-gate.Key<=Model.State.region?Color.white:new Color(.45f,.48f,.48f);}
            floorRegion=Model.State.region;
            foreach(var region in Model.Config.regions.Where(r=>r.id<=Model.State.region&&!renderedCaveSprings.Contains(r.id)))
            {
                if(region.id==1)WorldLabel("小水塘 · 岸边可取水",planWorld,20,27,.07f);
                else{var p=region.freshwater_source.source;var marker=Draw("Spring "+region.id,appearance.marker,planWorld,p[0],p[1],.7f,2);marker.color=new Color32(95,154,173,220);WorldLabel("渗水源",planWorld,p[0],p[1]+.7f,.07f);}
                renderedCaveSprings.Add(region.id);
            }
        }
        private void ClampCaveCamera()
        {
            var p=mapCamera.transform.position;float halfY=mapCamera.orthographicSize;
            float halfX=halfY*Screen.width*mapCamera.rect.width/Mathf.Max(1,Screen.height*mapCamera.rect.height);
            p.x=halfX>(PlanTerrain.Width-1)/2f?(PlanTerrain.Width-1)/2f:Mathf.Clamp(p.x,halfX-5,PlanTerrain.Width-halfX+4);
            p.y=halfY>(PlanTerrain.Height-1)/2f?(PlanTerrain.Height-1)/2f:Mathf.Clamp(p.y,halfY-1,PlanTerrain.Height-halfY);
            mapCamera.transform.position=p;
        }
    }
}
