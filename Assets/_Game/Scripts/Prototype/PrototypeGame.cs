using System;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    [DefaultExecutionOrder(-100)]
    public sealed class PrototypeGame : MonoBehaviour
    {
        public PrototypeBalance balance;
        public PrototypeAppearance appearance;
        public PrototypeSimulation Model { get; private set; }
        public string Notice { get; private set; } = "先拾取手锯，再把木料和连接件送进仓库。";
        public bool LastActionSucceeded { get; private set; } = true;
        public bool SmokeMode { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool SessionStarted { get; private set; }
        public bool ResultVisible { get; private set; }
        private bool resultSeen;
        private int[] walkPath;
        private int walkIndex;
        public int WalkTargetX { get; private set; }
        public int WalkTargetY { get; private set; }
        public bool Walking => walkPath!=null;
        private string sessionBalance;
        private int interactionRevision;
        private string saveSlot;
        private int autoIndex;
        private string SaveRoot
        {
            get
            {
                string[] arguments=Environment.GetCommandLineArgs();int index=Array.IndexOf(arguments,"-prototypeSaveDirectory");
                return index>=0&&index+1<arguments.Length?Path.GetFullPath(arguments[index+1]):Path.Combine(Application.persistentDataPath,"Saves");
            }
        }

        private void Awake()
        {
            if (balance == null || appearance == null || appearance.font == null)
                throw new InvalidOperationException("原型缺少配置或字体，请先运行 Setup。");
            SmokeMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-prototypeSmoke") >= 0;
            sessionBalance = JsonUtility.ToJson(balance.values);
            ResetGame();
            MenuOpen=!SmokeMode;SessionStarted=SmokeMode;
        }

        public void ResetGame()
        {
            // A fresh copy prevents play sessions and tests from editing the configuration asset.
            var settings = JsonUtility.FromJson<PrototypeSettings>(sessionBalance);
            Model = new PrototypeSimulation(settings, SmokeMode ? 4271 : Environment.TickCount);
            Notice = "先拾取手锯，再把木料和连接件送进仓库。";
            LastActionSucceeded = true;
            interactionRevision = 0;
            saveSlot = Guid.NewGuid().ToString("N"); autoIndex = 0;
            walkPath=null;resultSeen=false;ResultVisible=false;
        }

        public void StartNewGame(){ResetGame();SessionStarted=true;MenuOpen=false;}
        public void OpenMenu(){MenuOpen=true;walkPath=null;Model.LeaveManualRole();}
        public void ResumeGame(){if(SessionStarted&&!Model.Failed)MenuOpen=false;}
        public void ContinueAfterResult(){if(!Model.Failed){ResultVisible=false;resultSeen=true;Model.SetPaused(false);}}
        public void ShowResult(){ResultVisible=true;Model.SetPaused(true);walkPath=null;}
        public void QuitGame(){Application.Quit();}
        public void BeginTutorial(){Model.Tutorial.Begin();Model.Tutorial.Refresh(Model);}
        public void SkipTutorial(){Model.Tutorial.Skip();Report(new ActionResult(true,"教程已收起；可随时从教程页继续或翻阅。"));}
        public void ExecuteTutorialAction()
        {
            if(MenuOpen||ResultVisible||Model.Failed||Model.Interacting)return;
            var action=PrototypeTutorial.NextAction(Model,Model.Tutorial.step);
            if(action.operation==TutorialOperation.Trade){GetComponent<PrototypeHud>().ShowTutorialControl();return;}
            if(action.operation==TutorialOperation.Wait)return;
            if(action.hasTarget && !action.AtTarget(Model))
            {
                GoTo(action.x,action.y);GetComponent<PrototypeWorld>().FocusPoint(action.x,action.y);return;
            }
            walkPath=null;Report(PrototypeTutorialActions.Execute(Model,action));Model.Tutorial.Refresh(Model);
        }

        public void GoTo(int x,int y)
        {
            if(MenuOpen||ResultVisible||Model.Failed||Model.Tutorial.IntroOpen)return;
            walkPath=Model.FindPath(Model.PlayerX,Model.PlayerY,x,y);walkIndex=0;WalkTargetX=x;WalkTargetY=y;
            Report(new ActionResult(walkPath!=null,walkPath==null?"该位置没有可达路径。":"正在前往；WASD或Esc取消。"));
        }
        public void GoToItem(ItemId item)
        {
            var pile=Model.Ground.Where(p=>p.Item==item&&p.Amount>p.Reserved).OrderBy(p=>PrototypeSimulation.Distance(Model.PlayerX,Model.PlayerY,p.X,p.Y)).FirstOrDefault();
            if(pile==null){Report(new ActionResult(false,"地面没有该物品；可查看仓库或采购。"));return;}
            GoTo(pile.X,pile.Y);
        }
        private void TickWalk(double delta)
        {
            if(walkPath==null||Model.Paused)return;
            if(walkIndex>=walkPath.Length){walkPath=null;return;}
            int cell=walkPath[walkIndex],x=cell%Model.Settings.mapWidth,y=cell/Model.Settings.mapWidth;
            if(!Model.IsWalkable(x,y)){GoTo(WalkTargetX,WalkTargetY);return;}
            double dx=x-Model.PlayerX,dy=y-Model.PlayerY;
            double distance=Math.Abs(dx)+Math.Abs(dy);
            if(distance<0.001){walkIndex++;return;}
            Model.MovePlayer(dx,dy,Math.Min(delta,Math.Max(Math.Abs(dx),Math.Abs(dy))/(Model.Settings.moveSpeed*Model.Speed)));
        }

        private void Update()
        {
            if (SmokeMode) return;
            if(MenuOpen||ResultVisible)return;
            if(Model.Failed){ResultVisible=true;return;}
            if(Model.Tutorial.IntroOpen){if(Input.GetKeyDown(KeyCode.Escape))OpenMenu();return;}
            double delta = Math.Min(Time.unscaledDeltaTime, 0.25f);
            if (Input.GetKeyDown(KeyCode.Space)) Model.SetPaused(!Model.Paused);
            if (Input.GetKeyDown(KeyCode.Alpha1)) Model.SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Model.SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Model.SetSpeed(3);
            if (Input.GetKeyDown(KeyCode.Escape)) { if(Walking||Model.Interacting){walkPath=null;Model.LeaveManualRole();}else OpenMenu(); }
            if(MenuOpen)return;
            double x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            double y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            if(x!=0||y!=0)walkPath=null;
            if(walkPath!=null)TickWalk(delta);else Model.MovePlayer(x, y, delta);
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetKeyDown(KeyCode.Q)) Report(Model.Deposit());
            if (Input.GetKeyDown(KeyCode.F)) Report(Model.ToggleWork());
            Model.Advance(delta);
            if(Model.Failed||Model.DemoCompleted&&!resultSeen){ResultVisible=true;Model.SetPaused(true);walkPath=null;}
            if (interactionRevision != Model.InteractionRevision) { interactionRevision = Model.InteractionRevision; Report(Model.LastInteraction); }
            if (Model.GameSeconds >= Model.NextAutosave && !Model.Interacting)
            {
                Model.NextAutosave += 300;
                autoIndex = (autoIndex + 1) % 3;WriteSave("auto-" + autoIndex + ".json");
            }
        }

        public void Report(ActionResult result)
        { Notice = result.Message; LastActionSucceeded = result.Success; }

        public void SaveGame()
        {
            if(Model.Failed){Report(new ActionResult(false,"经营已失败，请读取失败前的存档或开始新局。"));return;}
            if (Model.Interacting) { Report(new ActionResult(false,"请等待当前交互完成后保存。")); return; }
            WriteSave("manual.json");
        }
        private void WriteSave(string name)
        {
            if (SmokeMode && Array.IndexOf(Environment.GetCommandLineArgs(),"-prototypeSaveDirectory")<0) return;
            try
            {
                var snapshot=Model.Capture();snapshot.slot=saveSlot;snapshot.autoIndex=autoIndex;
                string folder=Path.Combine(SaveRoot,saveSlot);
                Directory.CreateDirectory(folder);string path=Path.Combine(folder,name),temporary=path+".tmp";
                File.WriteAllText(temporary,JsonUtility.ToJson(snapshot,true));
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                Report(new ActionResult(true,"存档已保存。"));
            }
            catch(Exception error){Report(new ActionResult(false,"保存失败："+error.Message));}
        }
        public void LoadLatest()
        {
            try
            {
                string rootPath=SaveRoot;
                if(!Directory.Exists(rootPath)){Report(new ActionResult(false,"尚无存档。"));return;}
                string[] paths=Directory.GetFiles(rootPath,"*.json",SearchOption.AllDirectories);
                if(paths.Length==0){Report(new ActionResult(false,"尚无存档。"));return;}
                Array.Sort(paths,(a,b)=>File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                string lastError=null;
                foreach(string path in paths)
                {
                    try
                    {
                        var snapshot=JsonUtility.FromJson<SimulationSnapshot>(File.ReadAllText(path));
                        if(snapshot.slot==null||!System.Text.RegularExpressions.Regex.IsMatch(snapshot.slot,@"^[a-f0-9]{32}$"))throw new InvalidOperationException("存档槽标识无效。");
                        var restored=PrototypeSimulation.Restore(snapshot);
                        if(restored.Failed){lastError="跳过失败后的存档。";continue;}
                        Model=restored;saveSlot=snapshot.slot;autoIndex=snapshot.autoIndex;
                        walkPath=null;SessionStarted=true;MenuOpen=false;ResultVisible=false;resultSeen=Model.DemoCompleted;
                        interactionRevision=Model.InteractionRevision;Report(new ActionResult(true,lastError==null?"已读取最近存档，生活、工资、预约和工具状态保留。":"最近存档不可读，已恢复较早的有效存档。"));
                        if(Model.Tutorial.active)GetComponent<PrototypeHud>().ShowTutorial();return;
                    }
                    catch(Exception error){lastError=error.Message;}
                }
                throw new InvalidOperationException(lastError??"无有效存档。");
            }
            catch(Exception error){Report(new ActionResult(false,"读取失败："+error.Message));}
        }

        public void SellAll()
        {
            int quantity = Model.Warehouse.Count(ItemId.Plank) + Model.Warehouse.Count(ItemId.WoodHandle);
            if (quantity == 0) { Report(new ActionResult(false, "仓库没有可出售的产品。")); return; }
            decimal previous = Model.Money;
            Model.Sell(ItemId.Plank, int.MaxValue);
            Model.Sell(ItemId.WoodHandle, int.MaxValue);
            Report(new ActionResult(true, $"出售 {quantity} 件产品，获得 {Model.Money - previous:0.00} 金钱。"));
        }

        public void Interact()
        {
            bool bulk = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (bulk) { Report(Model.PickUp(int.MaxValue)); return; }
            if (Model.NearWarehouse && Model.Carry.Used > Model.Carry.Count(ItemId.WoodHandSaw)) { Report(Model.Deposit()); return; }
            if (Model.NearBench && Model.BenchBuilt)
            {
                if (Model.Carry.Available(ItemId.WasteWood)>0 && Model.WoodInputStock.FreeTicks>0) Report(Model.DeliverInput());
                else if (Model.Output.UsedTicks>0) Report(Model.TakeOutput());
                else Report(Model.ToggleWork());
                return;
            }
            if (Model.NearGenerator && Model.GeneratorBuilt)
            { Report(Model.AddFuel(Model.Carry.Available(ItemId.WoodChips)>0?ItemId.WoodChips:ItemId.WasteWood,int.MaxValue)); return; }
            if (Model.DismantleBuilt && PrototypeSimulation.Distance(Model.PlayerX, Model.PlayerY, PrototypeSimulation.DismantleX, PrototypeSimulation.DismantleY) <= Model.Settings.interactionRadius)
            {
                if (Model.Carry.Available(Model.SelectedDevice)>0) Report(Model.DeliverInput(true));
                else if (Model.DismantleOutput.UsedTicks>0) Report(Model.TakeOutput(true));
                else Report(Model.ToggleDismantle());
                return;
            }
            if(Model.TableBuilt&&PrototypeSimulation.Distance(Model.PlayerX,Model.PlayerY,PrototypeSimulation.TableX,PrototypeSimulation.TableY)<=Model.Settings.interactionRadius)
            {
                if(Model.Carry.Available(ItemId.WoodHandle)>0||Model.Carry.Available(Model.AssemblyEnd)>0)Report(Model.FeedAssembly());
                else if(Model.TableOutput.UsedTicks>0)Report(Model.TakeAssembly());
                else Report(Model.ToggleAssembly());return;
            }
            Report(Model.PickUp());
        }
        public static string ItemName(ItemId item) => ItemCatalog.Name(item);

        public static string StateName(WorkState state)
        {
            switch (state)
            {
                case WorkState.NotBuilt: return "等待搭建";
                case WorkState.Idle: return "已停工 · 进度保留";
                case WorkState.Paused: return "游戏暂停";
                case WorkState.NeedsOperator: return "等待操作人员到岗";
                case WorkState.NeedsTool: return "缺少对应工具";
                case WorkState.NeedsMaterials: return "缺少现场原料 · 请实际投料";
                case WorkState.OutputBlocked: return "输出堵塞 · 仓库或内部容量已满";
                default: return "正在手工锯切";
            }
        }
    }
}
