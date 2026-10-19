using System;
using System.IO;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class PlanGameController : MonoBehaviour
    {
        public PrototypeAppearance appearance;public Camera mapCamera;public GameObject legacyRoot;
        public PlanSimulation Model {get;private set;}
        public bool MenuOpen=true;public string Notice="先收集生活物资和工具；左键拖图；右键拖框选择，选中人物后右键地面移动。Tab 管理，B 建设，I 库存。";
        public int selectedSite;public string buildType;public int buildRotation;public bool beltCrossing;
        private bool session;private double autoAt=300;private int autoSlot;private string slot;
        private int moveX=-1,moveY;
        public PlanConfig LoadConfig(){var c=JsonUtility.FromJson<PlanConfig>(Resources.Load<TextAsset>("Plan/RuntimeConfig").text);c.Validate();return c;}
        private string SaveRoot
        {get{var args=Environment.GetCommandLineArgs();int n=Array.IndexOf(args,"-planSaveRoot");return Array.IndexOf(args,"-planSmoke")>=0&&n>=0&&n+1<args.Length?args[n+1]:Path.Combine(Application.persistentDataPath,"S6PlanSaves");}}
        private void Awake()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-prototypeSmoke")>=0){legacyRoot.SetActive(true);gameObject.SetActive(false);return;}
            if(legacyRoot!=null)legacyRoot.SetActive(false);MenuOpen=true;session=false;NewModel("male");Application.wantsToQuit+=WantsToQuit;
        }
        private void Start(){if(!gameObject.activeSelf)return;BuildPresentation();if(Array.IndexOf(Environment.GetCommandLineArgs(),"-planSmoke")>=0)StartCoroutine(Smoke());}
        private void NewModel(string hero){lastSavedSnapshot=null;Model=new PlanSimulation(LoadConfig(),Environment.TickCount);Model.State.hero=hero;Model.State.paused=false;slot=Guid.NewGuid().ToString("N");autoAt=300;autoSlot=0;selectedSite=0;buildType=null;movingSite=0;moveX=-1;}
        public void StartGame(string hero){NewModel(hero);MenuOpen=false;session=true;BuildPresentation();}
        public void EditPreview(){Model=new PlanSimulation(LoadConfig());MenuOpen=true;session=false;BuildPresentation();}
        public void Act(Action action)
        {try{action();Model.State.revision++;Notice="操作完成。";}catch(Exception e){Notice=e.Message;}RebuildPanel();RefreshPresentation();}
        private void Update()
        {
            if(Model==null)return;
            contextDismissed=false;cancelledPlacement=false;
            if(Input.GetKeyDown(KeyCode.Escape)){if(EditingText&&!MenuOpen&&!SaveDialogOpen)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);else EscapeInterface();}
            if(MenuOpen||SaveDialogOpen){CancelMapGesture();return;}
            if((Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1))&&DismissSettingsAt(Input.mousePosition))contextDismissed=true;
            if(!string.IsNullOrEmpty(Model.State.ending)){MenuOpen=true;RebuildPanel();return;}
            bool keyboard=!EditingText;
            if(keyboard)
            {
            if(Input.GetKeyDown(KeyCode.Space))ApplyTimeControl(0);
            if(Input.GetKeyDown(KeyCode.Alpha1))ApplyTimeControl(1);if(Input.GetKeyDown(KeyCode.Alpha2))ApplyTimeControl(2);if(Input.GetKeyDown(KeyCode.Alpha3))ApplyTimeControl(3);
            if(Input.GetKeyDown(KeyCode.M))ToggleOverview();if(Input.GetKeyDown(KeyCode.Home))FocusHero();
            if(Input.GetKeyDown(KeyCode.R))buildRotation=(buildRotation+1)%4;
            if(Input.GetKeyDown(KeyCode.Tab))ToggleManagement();
            if(Input.GetKeyDown(KeyCode.B))ShowManagement(1);if(Input.GetKeyDown(KeyCode.I))ShowManagement(3);
            }
            if(Input.GetMouseButtonDown(1))cancelledPlacement=CancelPlacement();
            if(Input.GetMouseButtonDown(0)&&ContextOpen&&!RectTransformUtility.RectangleContainsScreenPoint(contextMenu,Input.mousePosition,null)){CloseContext();contextDismissed=true;}
            double dt=Math.Min(Time.unscaledDeltaTime,.25);
            if(!Model.State.paused&&!Model.HeroAboard)
            {
                int x=keyboard?((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0)):0,y=keyboard?((Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)):0;
                if(x!=0||y!=0){if(Model.State.player.directed)Model.ResumePeople(new[]{0});moveX=-1;var p=Model.State.player;Model.Walk(p,(int)Math.Round(p.x)+x,(int)Math.Round(p.y)+y,dt*Model.State.speed);}
                else if(moveX>=0&&!Model.State.player.directed){Model.Walk(Model.State.player,moveX,moveY,dt*Model.State.speed);if(Vector2.Distance(new Vector2((float)Model.State.player.x,(float)Model.State.player.y),new Vector2(moveX,moveY))<.1)moveX=-1;}
            }
            if(keyboard&&Input.GetKeyDown(KeyCode.E))PickupNearest();
            if(keyboard&&!Model.HeroAboard&&Input.GetKeyDown(KeyCode.Q)){var site=Model.Site(selectedSite);if(site!=null)Act(()=>Model.Deposit(site));}
            if(keyboard&&!Model.HeroAboard&&Input.GetKeyDown(KeyCode.F)&&selectedSite>0)Act(()=>Model.AssignHero(selectedSite));
            HandleMap();Model.Advance(dt);
            if(Model.State.seconds>=autoAt){Save(true);autoAt=Model.State.seconds+300;}
            RefreshPresentation();
        }
        private void OnDestroy(){Application.wantsToQuit-=WantsToQuit;}
        private void OnApplicationFocus(bool focus){if(!focus)CancelMapGesture();}
        private void PickupNearest()
        {
            if(Model.HeroAboard){Notice="主角正在返程，无法拾取物品。";return;}var p=Model.State.player;var g=Model.State.ground.Find(a=>a.stock.stacks.Count>0&&Vector2.Distance(new Vector2(a.x,a.y),new Vector2((float)p.x,(float)p.y))<=1.5f);
            if(g==null){Notice="附近没有可拾取物品。";return;}var stack=g.stock.stacks[0];long n=Math.Min(stack.ticks,PlanStock.Scale);
            Act(()=>Model.Pick(g.id,stack.item,n));
        }
        private void HandleMap()
        {
            Vector2 pointer=Input.mousePosition;
            if(Input.GetMouseButtonDown(0))BeginMapPointer(0,pointer,contextDismissed);
            if(Input.GetMouseButtonDown(1)&&!cancelledPlacement&&!contextDismissed)BeginMapPointer(1,pointer);
            if(pointerButton>=0)
            {
                if(Input.GetMouseButton(pointerButton))DragMapPointer(pointer);
                if(Input.GetMouseButtonUp(pointerButton))ReleaseMapPointer(pointerButton,pointer);
            }
            if(!InMap(pointer)||PointerOverInterface(pointer))return;
            if(pointerButton<0&&Input.mouseScrollDelta.y!=0)mapCamera.orthographicSize=Mathf.Clamp(mapCamera.orthographicSize*Mathf.Pow(.85f,Input.mouseScrollDelta.y),4,160);
            if(pointerButton<0&&Input.GetMouseButton(2))mapCamera.transform.position-=new Vector3(Input.GetAxis("Mouse X"),Input.GetAxis("Mouse Y"))*mapCamera.orthographicSize*.025f;
            var point=mapCamera.ScreenToWorldPoint(pointer);UpdatePreview(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.y));
        }
        private void MapLeftClick(int x,int y)
        {
            ClearMapSelection();
            if(TryPlaceAt(x,y))return;
            if(x>=2&&x<=3&&y>=20&&y<=21){selectedSite=-1;Notice="已选中洞口；右键查看操作。";return;}
            var selected=Model.State.sites.FindLast(s=>Model.Covers(s,x,y));
            if(selected!=null){selectedSite=selected.id;Notice="已选中设施；右键查看操作，F 参加劳动，Q 投料。";if(managementOpen&&panelTab==2)RebuildPanel();return;}
            if(!Model.Walkable(x,y)){Notice=PlanTerrain.BarrierAt(x,y)?"这里是岩脊隔断，请沿狭口或桥梁通行；未开放地块需先付费。":PlanTerrain.WaterAt(x,y)==2?"这里是深水，请寻找浅滩或天然石脊通路。":PlanTerrain.ObstacleAt(x,y)!=null?"这里有固定岩体，不能通行或建设；请绕开石柱与岩脊。":"这里是岩壁、洞口或未开放洞室；先选择可达地面，或在区域页付费开放。";moveX=-1;return;}
            SetHeroDestination(x,y);
        }
    }
}
