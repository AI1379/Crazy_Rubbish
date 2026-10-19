using System.Collections.Generic;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private sealed class TerrainBatch
        {
            public int region,order;public Texture texture;public string name;
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<Color> colors=new List<Color>();public readonly List<int> indices=new List<int>();
        }
        private readonly Dictionary<(Texture,int,int,int,int),TerrainBatch> terrainBatches=new Dictionary<(Texture,int,int,int,int),TerrainBatch>();
        private readonly List<KeyValuePair<int,MeshRenderer>> terrainRenderers=new List<KeyValuePair<int,MeshRenderer>>();
        private readonly List<Object> terrainResources=new List<Object>();
        private void ClearTerrainMeshes()
        {
            foreach(var resource in terrainResources)if(resource!=null&&resource.hideFlags==HideFlags.DontSave){if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);}
            terrainResources.Clear();terrainBatches.Clear();terrainRenderers.Clear();
        }
        private void TerrainQuad(Texture texture,Rect uv,float x,float y,float width,float height,int region,int order,Color color,bool flip=false)
        {
            int cx=Mathf.FloorToInt(x/32),cy=Mathf.FloorToInt(y/32);var key=(texture,region,order,cx,cy);
            if(!terrainBatches.TryGetValue(key,out var batch))terrainBatches[key]=batch=new TerrainBatch{region=region,order=order,texture=texture,name="Terrain "+region+" "+order+" "+cx+" "+cy+" "+texture.name};
            int start=batch.vertices.Count;float left=x-width/2,right=x+width/2,bottom=y-height/2,top=y+height/2;
            batch.vertices.Add(new Vector3(left,bottom));batch.vertices.Add(new Vector3(left,top));batch.vertices.Add(new Vector3(right,top));batch.vertices.Add(new Vector3(right,bottom));
            float u0=flip?uv.xMax:uv.xMin,u1=flip?uv.xMin:uv.xMax;batch.uv.Add(new Vector2(u0,uv.yMin));batch.uv.Add(new Vector2(u0,uv.yMax));batch.uv.Add(new Vector2(u1,uv.yMax));batch.uv.Add(new Vector2(u1,uv.yMin));
            for(int i=0;i<4;i++)batch.colors.Add(color);batch.indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        private void TerrainSprite(Sprite sprite,float x,float y,float size,int region,int order,Color color,bool square=false,bool flip=false)
        {
            var rect=sprite.textureRect;var uv=new Rect(rect.x/sprite.texture.width,rect.y/sprite.texture.height,rect.width/sprite.texture.width,rect.height/sprite.texture.height);
            float scale=size/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);TerrainQuad(sprite.texture,uv,x,y,square?size:sprite.bounds.size.x*scale,square?size:sprite.bounds.size.y*scale,region,order,color,flip);
        }
        private void FinishTerrainMeshes()
        {
            var materials=new Dictionary<(Texture,int),Material>();
            foreach(var batch in terrainBatches.Values)
            {
                var key=(batch.texture,batch.region);if(!materials.TryGetValue(key,out var material))
                {material=new Material(Shader.Find("Sprites/Default")){name="Terrain material "+batch.region+" "+batch.texture.name,mainTexture=batch.texture,hideFlags=HideFlags.DontSave};materials[key]=material;terrainResources.Add(material);}
                var mesh=new Mesh{name=batch.name,hideFlags=HideFlags.DontSave};mesh.SetVertices(batch.vertices);mesh.SetUVs(0,batch.uv);mesh.SetColors(batch.colors);mesh.SetTriangles(batch.indices,0);mesh.RecalculateBounds();terrainResources.Add(mesh);
                var root=Root(batch.name,floorRoot);root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=batch.order;
                terrainRenderers.Add(new KeyValuePair<int,MeshRenderer>(batch.region,renderer));
            }
            terrainBatches.Clear();
        }
    }
}
