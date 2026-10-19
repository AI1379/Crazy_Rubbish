using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    // Bought regions extend the authored first-region floor; v1 facilities without dedicated art reuse the
    // closest delivered sprite, tinted by state and named on a small plate.
    public sealed partial class PrototypeWorld
    {
        private Transform expansionRoot;
        private int expansionWidth,expansionHeight,expansionRegion;
        private readonly Dictionary<int,TextMesh> facilityLabels=new Dictionary<int,TextMesh>();

        private void ResetExpansion()
        {
            if(expansionRoot!=null)RemoveView(expansionRoot.gameObject);
            expansionRoot=null;expansionWidth=expansionHeight=expansionRegion=0;facilityLabels.Clear();
        }
        private static Color RegionGround(PrototypeSimulation m,int x,int y)
        {
            for(int r=2;r<=DesignCatalog.Regions.Length;r++)
            {
                var def=PrototypeSimulation.Region(r);
                if(def.InGarbage(x,y))return r<=m.OwnedRegion?new Color32(238,221,191,255):new Color32(160,150,135,255);
                if(def.Veins.Any(v=>v.Contains(x,y))&&r<=m.OwnedRegion)return new Color32(196,170,140,255);
                if(def.InLand(x,y))return r<=m.OwnedRegion?new Color32(238,237,218,255):new Color32(150,154,150,255);
            }
            return new Color32(205,209,205,255);
        }
        private void RefreshExpansion(PrototypeSimulation m)
        {
            if(expansionRoot!=null&&expansionWidth==m.Settings.mapWidth&&expansionHeight==m.Settings.mapHeight&&expansionRegion==m.OwnedRegion)return;
            ResetExpansion();
            if(m.Settings.mapWidth<=32&&m.Settings.mapHeight<=28)return;
            expansionRoot=new GameObject("Expansion").transform;expansionRoot.SetParent(visualRoot,false);
            expansionWidth=m.Settings.mapWidth;expansionHeight=m.Settings.mapHeight;expansionRegion=m.OwnedRegion;
            for(int x=0;x<m.Settings.mapWidth;x++)for(int y=0;y<m.Settings.mapHeight;y++)
            {
                if(x<32&&y<28)continue;
                var tile=Sprite($"Expansion {x},{y}",game.appearance.tile,x,y,1,-2000);tile.color=RegionGround(m,x,y);tile.transform.SetParent(expansionRoot,true);
            }
            for(int r=2;r<=m.OwnedRegion;r++)
            {
                var def=PrototypeSimulation.Region(r);
                Outline($"Region {r} Land",def.LandX0-0.5f,def.LandY0-0.5f,def.LandX1-def.LandX0+1,def.LandY1-def.LandY0+1,new Color32(130,170,141,255));
                Outline($"Region {r} Heap",def.GarbageX0-0.5f,def.GarbageY0-0.5f,def.GarbageX1-def.GarbageX0+1,def.GarbageY1-def.GarbageY0+1,new Color32(223,164,94,255));
                ExpansionLabel($"Region {r} Label",$"第{r}区 · {def.Name}",(def.LandX0+def.LandX1)/2f,def.LandY1+0.8f,0.16f,new Color32(166,199,169,255));
                foreach(var v in def.Veins)
                {
                    Outline($"Vein {v.Key}",v.X0-0.5f,v.Y0-0.5f,v.X1-v.X0+1,v.Y1-v.Y0+1,new Color32(205,140,90,255));
                    ExpansionLabel($"Vein {v.Key} Label",(v.Key=="iron"?"铁矿":v.Key=="copper"?"铜矿":"陶土")+(m.SurveyedVeins.Contains(v.Key)?" · 已勘探":" · 待勘探"),(v.X0+v.X1)/2f,v.Y1+0.8f,0.11f,new Color32(232,185,121,255));
                }
                if(r==3)
                {
                    Outline("Launch Shaft",PrototypeSimulation.LaunchX-0.5f,PrototypeSimulation.LaunchY-0.5f,4,4,new Color32(120,190,220,255));
                    ExpansionLabel("Launch Label","旧检修竖井 · 升降舱平台",PrototypeSimulation.LaunchX+1.5f,PrototypeSimulation.LaunchY+4.3f,0.11f,new Color32(150,210,235,255));
                }
            }
        }
        private void Outline(string name,float x,float y,float width,float height,Color color)
        {
            var marker=game.appearance.marker!=null?game.appearance.marker:game.appearance.tile;
            foreach(var (n,cx,cy,w,h) in new[]{(" S",x+width/2,y,width,0.12f),(" N",x+width/2,y+height,width,0.12f),(" W",x,y+height/2,0.12f,height),(" E",x+width,y+height/2,0.12f,height)})
            {var line=Sprite(name+n,marker,cx,cy,1,-1400);line.transform.localScale=new Vector3(w/marker.bounds.size.x,h/marker.bounds.size.y,1);line.color=color;line.transform.SetParent(expansionRoot,true);}
        }
        private void ExpansionLabel(string name,string text,float x,float y,float size,Color color)
        {var label=Label(name,text,x,y,size,color);label.transform.SetParent(expansionRoot,true);}

        private Sprite GenericSprite(int kind)
        {
            var def=DesignCatalog.GenericFacility(kind);if(def==null)return game.appearance.workbench;
            switch(def.Key)
            {
                case "warehouse":return game.appearance.warehouse;
                case "grow_lamp":return game.appearance.streetlamp;
                case "boiler":case "turbine":case "condenser":return game.appearance.generator;
                case "landfill":case "composting":case "growing":return game.appearance.hole;
                case "lift_assembly":case "lift_launch":return game.appearance.housing;
            }
            if(def.Key.StartsWith("auto_"))return game.appearance.automaticWoodwork;
            if(def.Power>0||def.Maintainers>0)return game.appearance.electricWoodwork;
            return def.Width*def.Height<=4?game.appearance.dismantle:game.appearance.workbench;
        }
        private Color GenericTint(PrototypeSimulation m,FacilityInstance f)
        {
            if(f.Blueprint)return new Color(0.8f,0.85f,0.9f,0.4f);
            if(f.Sealed)return new Color(0.55f,0.5f,0.45f);
            if(f.MachineTripped)return new Color(1,0.42f,0.3f);
            if(f.ProcessActive||f.ThermalRunning)return new Color(1,0.94f,0.78f);
            var def=DesignCatalog.GenericFacility(f.Kind);
            // Each line family gets its own hue so reused sprites stay distinguishable.
            float hue=(def.Index*0.137f)%1f;var tint=Color.HSVToRGB(hue,0.22f,1);
            return f.PowerRatio<=0&&def.Power>0?tint*0.7f:tint;
        }
        private void RefreshGenericView(PrototypeSimulation m,FacilityInstance f,SpriteRenderer view)
        {
            if(!PrototypeSimulation.IsGenericKind(f.Kind))return;
            var size=PrototypeSimulation.Footprint(f);
            var image=view.sprite;if(image!=null)view.transform.localScale=new Vector3(0.92f*size.width/Mathf.Max(0.01f,image.bounds.size.x),0.92f*size.height/Mathf.Max(0.01f,image.bounds.size.y),1);
            if(!facilityLabels.TryGetValue(f.Id,out var label)||label==null)
            {label=Label("Facility Label "+f.Id,DesignCatalog.GenericFacility(f.Kind).Name,0,0,0.075f,new Color32(235,230,210,255));label.transform.SetParent(constructionVisuals,true);facilityLabels[f.Id]=label;}
            label.gameObject.SetActive(f.Built||f.Blueprint);
            label.transform.position=new Vector3(f.X+(size.width-1)/2f,f.Y+size.height-0.3f,-0.1f);
        }
    }
}
