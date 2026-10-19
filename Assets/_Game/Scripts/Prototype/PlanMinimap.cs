using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RawImage minimapImage;private RectTransform minimapHero;private bool mapOverview;
        private void BuildMinimap()
        {
            var box=Box(planCanvas,"Map overview",24,0,190,212);box.anchorMin=box.anchorMax=Vector2.zero;box.anchoredPosition=new Vector2(24,372);
            Text(box,"Map caption","地图 · 点击定位",8,4,174,24,15);var image=Rect(box,"Map",8,30,174,174);minimapImage=image.gameObject.AddComponent<RawImage>();
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=minimapImage;button.onClick.AddListener(()=>
            {if(!MenuOpen&&RectTransformUtility.ScreenPointToLocalPointInRectangle(image,Input.mousePosition,null,out var point)){mapCamera.transform.position=new Vector3(Mathf.Clamp01(point.x/174)*249,Mathf.Clamp01(1+point.y/174)*249,-10);if(mapCamera.orthographicSize>60)mapCamera.orthographicSize=25;mapOverview=false;}});
            minimapHero=Rect(image,"Player location",0,0,5,5);minimapHero.anchorMin=minimapHero.anchorMax=Vector2.zero;minimapHero.pivot=new Vector2(.5f,.5f);var marker=minimapHero.gameObject.AddComponent<Image>();marker.color=new Color32(255,218,97,255);marker.raycastTarget=false;RefreshMinimap();
        }
        private void RefreshMinimap()
        {
            if(minimapImage==null)return;var texture=minimapImage.texture as Texture2D;if(texture==null){texture=new Texture2D(PlanTerrain.Width,PlanTerrain.Height,TextureFormat.RGBA32,false){name="Cave minimap",filterMode=FilterMode.Point,hideFlags=HideFlags.DontSave};terrainResources.Add(texture);minimapImage.texture=texture;}
            var colors=new Color32[PlanTerrain.Width*PlanTerrain.Height];for(int y=0;y<PlanTerrain.Height;y++)for(int x=0;x<PlanTerrain.Width;x++)
            {
                int r=PlanTerrain.ChamberAt(x,y);Color color=r==0?new Color32(25,28,26,255):(PlanTerrain.ObstacleAt(x,y)!=null||PlanTerrain.BarrierAt(x,y))&&!Model.TerrainCleared(x,y)?new Color32(105,102,87,255):PlanTerrain.BridgeAt(x,y)?new Color32(162,145,106,255):PlanTerrain.WaterAt(x,y)>0?new Color32(52,112,124,255):PlanTerrain.SurfaceAt(x,y)==PlanTerrain.Surface.Soil?new Color32(121,105,72,255):new Color32(140,133,111,255);
                if(r>Model.State.region)color*=.48f;color.a=1;colors[y*PlanTerrain.Width+x]=color;
            }
            foreach(var r in Model.Config.regions)if(r.id<=Model.State.region){var a=r.garbage_rectangle;for(int y=a[1];y<=a[3];y++)for(int x=a[0];x<=a[2];x++)colors[y*PlanTerrain.Width+x]=new Color32(194,135,62,255);}
            texture.SetPixels32(colors);texture.Apply(false);
        }
        private void FocusHero(){mapOverview=false;mapCamera.transform.position=new Vector3((float)Model.State.player.x,(float)Model.State.player.y,-10);mapCamera.orthographicSize=20;}
        private void ToggleOverview(){if(mapOverview){FocusHero();return;}mapOverview=true;mapCamera.transform.position=new Vector3(PlanTerrain.Width/2f,PlanTerrain.Height/2f,-10);mapCamera.orthographicSize=PlanTerrain.Height*.54f;}
    }
}
