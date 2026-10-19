using System;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        [SerializeField] private Transform planWorld,planCanvas;
        private Transform floorRoot,groundRoot,sitesRoot,peopleRoot,preview;private SpriteRenderer operationMarker;
        private readonly Dictionary<int,SpriteRenderer> groundViews=new Dictionary<int,SpriteRenderer>(),siteViews=new Dictionary<int,SpriteRenderer>(),actorViews=new Dictionary<int,SpriteRenderer>();
        private readonly Dictionary<int,TextMesh> siteLabels=new Dictionary<int,TextMesh>();
        private readonly Dictionary<int,Vector3> previousPositions=new Dictionary<int,Vector3>();private readonly Dictionary<int,int> facings=new Dictionary<int,int>();
        private readonly Dictionary<int,double> actorMovingUntil=new Dictionary<int,double>();
        private int lastWorldRevision=-1;private float nextUi;
        private int floorRegion;
        public void BuildPresentation()
        {
            CloseInventoryWindow();
            ResetMapSelection();ResetAutomationViews();journeyRoot=null;journeyVisualKey=null;liftView=null;placementOutline=null;placementFront=null;mapOverview=false;if(planWorld!=null)Remove(planWorld.gameObject);if(planCanvas!=null)Remove(planCanvas.gameObject);
            groundViews.Clear();siteViews.Clear();actorViews.Clear();siteLabels.Clear();previousPositions.Clear();facings.Clear();actorMovingUntil.Clear();lastWorldRevision=-1;floorRegion=0;
            planWorld=new GameObject("Plan World").transform;planWorld.SetParent(transform,false);
            floorRoot=Root("Ground",planWorld);groundRoot=Root("Waste and dropped objects",planWorld);sitesRoot=Root("Placed facilities",planWorld);peopleRoot=Root("People",planWorld);
            BuildCaveTerrain();
            Draw("Hole",appearance.hole,planWorld,2.5f,20.5f,2,-5);WorldLabel("洞 · 去向不明",planWorld,2.5f,19,0.1f);
            preview=Root("Build preview",planWorld);preview.gameObject.SetActive(false);
            operationMarker=Draw("Selected operation side",appearance.marker,planWorld,0,0,.3f,55);operationMarker.color=new Color32(114,217,164,230);operationMarker.gameObject.SetActive(false);
            mapCamera.orthographic=true;mapCamera.transform.position=new Vector3(14,18,-10);mapCamera.orthographicSize=20;FitViewport();BuildInterface();Canvas.ForceUpdateCanvases();RefreshPresentation(true);
        }
        private void FitViewport()
        {float scale=planCanvas!=null?planCanvas.GetComponent<Canvas>().scaleFactor:Mathf.Min(Screen.width/1600f,Screen.height/1000f);float w=Screen.width/Mathf.Max(.01f,scale),h=Screen.height/Mathf.Max(.01f,scale);mapCamera.rect=new Rect(20/w,150/h,(w-(managementOpen?460:40))/w,Mathf.Max(1,h-300)/h);
            if(management!=null){float height=Mathf.Clamp(h-280,360,720);management.sizeDelta=new Vector2(400,height);var viewport=management.Find("Viewport") as RectTransform;if(viewport!=null)viewport.sizeDelta=new Vector2(384,height-106);}}
        private static Transform Root(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        private static void Remove(GameObject o){o.SetActive(false);if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        private SpriteRenderer Draw(string name,Sprite image,Transform parent,float x,float y,float size,int order)
        {
            var t=Root(name,parent);t.position=new Vector3(x,y,0);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=image;r.sortingOrder=order;Scale(r,size);return r;
        }
        private static void Scale(SpriteRenderer r,float size){if(r.sprite!=null){float n=size/Mathf.Max(r.sprite.bounds.size.x,r.sprite.bounds.size.y);r.transform.localScale=new Vector3(n,n,1);}}
        private TextMesh WorldLabel(string value,Transform parent,float x,float y,float size)
        {
            var t=Root(value,parent);t.position=new Vector3(x,y,-.1f);var label=t.gameObject.AddComponent<TextMesh>();label.font=appearance.font;label.fontSize=48;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color32(235,224,201,255);label.text=value;
            var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=appearance.font.material;r.sortingOrder=50;return label;
        }
        private Sprite Heap(PlanGround g)
        {
            string id=g.stock.stacks.OrderByDescending(s=>Model.Config.Item(s.item).durability>0).First().item;bool high=g.stock.Used(Model.Config)/g.width>5;
            if(id=="waste_wood")return high?appearance.woodHeapHigh:appearance.wasteWood;
            if(id=="plastic_mix")return high?appearance.mixedHeapHigh:appearance.mixedPlastic;
            if(id=="steel_scrap"||id=="copper_scrap"||id=="aluminum_scrap")return high?appearance.metalHeapHigh:appearance.metalHeap;
            return appearance.PlanSprite(id);
        }
        private void RefreshPresentation(bool force=false)
        {
            if(Model==null||planWorld==null)return;FitViewport();if(minimapHero!=null)minimapHero.anchoredPosition=new Vector2((float)Model.State.player.x/249*174,(float)Model.State.player.y/249*174);
            RefreshCaveRegions();ClampCaveCamera();
            if(force||lastWorldRevision!=Model.State.revision)
            {
                foreach(int id in groundViews.Keys.Where(id=>!Model.State.ground.Any(g=>g.id==id&&g.stock.stacks.Count>0)).ToArray()){Remove(groundViews[id].gameObject);groundViews.Remove(id);}
                foreach(var g in Model.State.ground.Where(g=>g.stock.stacks.Count>0))
                {if(!groundViews.TryGetValue(g.id,out var view))groundViews[g.id]=view=Draw("Ground "+g.id,Heap(g),groundRoot,g.x,g.y,.86f,5);view.sprite=Heap(g);Scale(view,.86f);view.color=g.stock.stacks[0].item=="ash"?new Color(.6f,.6f,.6f):Color.white;}
                foreach(int id in siteViews.Keys.Where(id=>Model.Site(id)==null).ToArray()){Remove(siteViews[id].gameObject);Remove(siteLabels[id].gameObject);siteViews.Remove(id);siteLabels.Remove(id);}
                foreach(var s in Model.State.sites)
                {
                    var z=Model.Size(s);if(!siteViews.TryGetValue(s.id,out var view)){siteViews[s.id]=view=Draw("Facility "+s.id,appearance.PlanSprite(s.type,true),sitesRoot,s.x+(z.width-1)/2f,s.y+(z.height-1)/2f,Mathf.Max(z.width,z.height),10);siteLabels[s.id]=WorldLabel(Model.Config.Facility(s.type).name,sitesRoot,s.x+(z.width-1)/2f,s.y+z.height-.3f,.075f);}
                    view.transform.position=new Vector3(s.x+(z.width-1)/2f,s.y+(z.height-1)/2f,0);view.color=s.pending?new Color(1,1,1,.35f):s.enabled?Color.white:new Color(.6f,.6f,.6f);
                    if(s.type=="belt")view.transform.localRotation=Quaternion.Euler(0,0,s.direction*90);
                    siteLabels[s.id].transform.position=view.transform.position+new Vector3(0,z.height/2f,0);siteLabels[s.id].gameObject.SetActive(!Model.Overlay(s.type));
                }
                lastWorldRevision=Model.State.revision;
            }
            var chosen=Model.Site(selectedSite);operationMarker.gameObject.SetActive(chosen!=null);if(chosen!=null){var front=Model.Front(chosen);operationMarker.transform.position=new Vector3(front.x,front.y,0);}
            foreach(var p in Model.Alive.Concat(new[]{Model.State.player}))
            {
                var bank=p.id==0?(Model.State.hero=="female"?appearance.female:appearance.male):appearance.ResidentSprites(p.id);
                if(!actorViews.TryGetValue(p.id,out var actor))actorViews[p.id]=actor=Draw("Person "+p.id,bank.Get(0),peopleRoot,(float)p.x,(float)p.y,1.3f,40);
                Vector3 pos=new Vector3((float)p.x,(float)p.y,0),delta=previousPositions.TryGetValue(p.id,out var previous)?pos-previous:Vector3.zero;int facing=facings.TryGetValue(p.id,out var old)?old:0;
                if(delta.sqrMagnitude>.000001)facing=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?delta.x<0?2:3:delta.y<0?0:1;
                string pose=p.cargo.stacks.Count>0||p.id==0&&Model.State.backpack.stacks.Count>0?"carry":null;
                var site=Model.Site(p.id==0?Model.State.heroSite:p.assigned);if(!p.directed&&site!=null&&site.active&&site.type.Contains("wood")){pose="saw";facing=1;}
                // Simulation movement arrives in ticks; selection/readout refreshes can occur between them.
                // Hold the moving pose through that gap instead of resetting it on every repeated draw.
                double clock=Model.State.seconds+Model.State.accumulated;
                if(delta.sqrMagnitude>.000001)actorMovingUntil[p.id]=clock+Math.Max(.15,Model.Config.Rule("clock.tick_seconds")*1.5);
                bool walking=!Model.State.paused&&actorMovingUntil.TryGetValue(p.id,out double until)&&clock<until;
                actor.gameObject.SetActive(!(p.id==0&&Model.HeroAboard));actor.sprite=bank.Get(facing,pose);Scale(actor,1.3f);actor.transform.position=pos;actor.transform.localRotation=Quaternion.Euler(0,0,walking?Mathf.Sin((float)clock*12)*4:0);previousPositions[p.id]=pos;facings[p.id]=facing;
            }
            foreach(int id in actorViews.Keys.Where(id=>id!=0&&!Model.Alive.Any(p=>p.id==id)).ToArray()){Remove(actorViews[id].gameObject);actorViews.Remove(id);previousPositions.Remove(id);facings.Remove(id);actorMovingUntil.Remove(id);}
            RefreshSelectionMarkers();
            if(force||Time.unscaledTime>=nextUi){RefreshInterface();RefreshLogisticsVisuals();RefreshJourneyVisuals();nextUi=Time.unscaledTime+.25f;}
        }
    }
}
