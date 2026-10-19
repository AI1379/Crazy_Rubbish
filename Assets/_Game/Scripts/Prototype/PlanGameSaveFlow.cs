using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform saveOverlay,saveWindow;
        private Text saveError;
        private string lastSavedSnapshot,saveDialogKind;
        private Action pendingTransition,pendingCancel;
        private Func<bool> pendingSave;
        private bool quitApproved;
        private bool SaveDialogOpen=>saveOverlay!=null&&saveOverlay.gameObject.activeSelf;
        private bool HasUnsavedProgress=>session&&string.IsNullOrEmpty(Model.State.ending)&&(lastSavedSnapshot==null||lastSavedSnapshot!=JsonUtility.ToJson(Model.Capture()));
        private string ManualPath=>Path.Combine(SaveRoot,slot+"-manual.json");
        private void BuildSaveInterface()
        {
            saveOverlay=Box(planCanvas,"Save dialogs",0,0,0,0);saveOverlay.anchorMin=Vector2.zero;saveOverlay.anchorMax=Vector2.one;saveOverlay.offsetMin=saveOverlay.offsetMax=Vector2.zero;
            var shade=saveOverlay.GetComponent<Image>();shade.color=new Color(0,0,0,.7f);shade.raycastTarget=true;
            saveWindow=Box(saveOverlay,"Window",0,0,880,640);saveWindow.anchorMin=saveWindow.anchorMax=saveWindow.pivot=new Vector2(.5f,.5f);saveWindow.anchoredPosition=Vector2.zero;
            saveDialogKind=null;pendingTransition=pendingCancel=null;pendingSave=null;saveOverlay.gameObject.SetActive(false);
        }
        public void Save(bool automatic=false){TrySave(automatic);}
        private bool TrySave(bool automatic=false)
        {
            if(!session){Notice="请先开始游戏。";return false;}
            if(!string.IsNullOrEmpty(Model.State.ending)){Notice="结局不会覆盖失败前存档。";return false;}
            try
            {
                Directory.CreateDirectory(SaveRoot);string path=automatic?Path.Combine(SaveRoot,slot+"-auto"+(autoSlot%3)+".json"):ManualPath,temp=path+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(Model.Capture(),true));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                if(automatic)autoSlot++;lastSavedSnapshot=JsonUtility.ToJson(Model.Capture());Notice="已保存新版进度。";return true;
            }
            catch(Exception e){Debug.LogWarning("Save failed: "+e.Message);Notice="保存失败，请检查剩余空间和访问权限；当前进度仍保留。";return false;}
        }
        private bool SaveUnderNewSession()
        {string old=slot;slot=Guid.NewGuid().ToString("N");if(TrySave())return true;slot=old;return false;}
        private PlanState ReadSave(string path)
        {
            var state=JsonUtility.FromJson<PlanState>(File.ReadAllText(path));
            if(state==null||state.schema!=1||state.version!=Model.Config.version||state.player==null||state.sites==null||!state.sites.Any(s=>s.community&&s.type=="warehouse")||state.warehouse==null||state.people==null||state.ground==null||state.springs==null)
                throw new InvalidOperationException("存档内容不完整或版本不兼容，原文件已保留。");
            if(state.acquired==null||state.technologies==null||state.bills==null||state.markets==null||state.budgets==null||state.cleaning==null||state.terrainClearings==null||state.discarded==null||state.soldKnowledge==null)
                throw new InvalidOperationException("存档台账不完整，原文件已保留。");
            var stocks=new List<PlanStock>{state.warehouse,state.backpack,state.holeInput,state.disposalHeld};
            foreach(var person in state.people.Concat(new[]{state.player}))
            {if(person==null||person.losses==null)throw new InvalidOperationException("居民记录不完整。");stocks.Add(person.cargo);}
            foreach(var site in state.sites)
            {
                var definition=Model.Config.facilities.FirstOrDefault(f=>f.id==site.type);var recipe=Model.Config.recipes.FirstOrDefault(r=>r.id==site.recipe);
                if(definition==null||!string.IsNullOrEmpty(site.recipe)&&(recipe==null||recipe.facility!=site.type)||site.active&&(recipe==null||site.stage<0||site.stage>=recipe.stages.Length))throw new InvalidOperationException("设施或配方记录不兼容。");
                stocks.AddRange(new[]{site.input,site.output,site.reserved,site.tools,site.addons,site.builtMaterials});
            }
            stocks.AddRange(state.ground.Select(g=>g.stock));var items=new HashSet<string>(Model.Config.items.Select(i=>i.id));
            if(stocks.Any(stock=>stock==null||stock.stacks==null||stock.stacks.Any(v=>v==null||!items.Contains(v.item)||v.ticks<0||v.reserved<0||v.reserved>v.ticks)))throw new InvalidOperationException("物品记录不完整或不兼容。");
            return state;
        }
        public void Load()
        {
            var files=SaveFiles();if(files.Length==0){Notice="没有新版存档；旧 Demo 存档保留在原目录。";return;}
            TryLoadFile(files[0]);
        }
        private string[] SaveFiles()
        {return Directory.Exists(SaveRoot)?Directory.GetFiles(SaveRoot,"*.json").OrderByDescending(File.GetLastWriteTimeUtc).ToArray():new string[0];}
        private bool TryLoadFile(string path)
        {
            try
            {
                var restored=new PlanSimulation(LoadConfig(),1,ReadSave(path));
                Model=restored;slot=Path.GetFileName(path).Split('-')[0];MenuOpen=false;session=true;autoAt=Model.State.seconds+300;autoSlot=0;selectedSite=0;buildType=null;movingSite=0;moveX=-1;
                lastSavedSnapshot=JsonUtility.ToJson(Model.Capture());BuildPresentation();Notice="已恢复新版进度。";return true;
            }
            catch(Exception e){Debug.LogWarning("Load failed: "+e.Message);Notice="读取失败：存档不可用，当前进度仍保留。";if(saveError!=null)saveError.text=Notice;return false;}
        }
        private void BeginSaveDialog(string kind,string title)
        {
            CancelMapGesture();CloseContext();CloseSettings();saveDialogKind=kind;
            for(int i=saveWindow.childCount-1;i>=0;i--)Remove(saveWindow.GetChild(i).gameObject);
            Text(saveWindow,"Title",title,28,18,760,40,26);saveError=Text(saveWindow,"Error","",28,552,824,44,16);saveError.color=Accent;
            saveOverlay.gameObject.SetActive(true);saveOverlay.SetAsLastSibling();RefreshInterface();
        }
        private void CloseSaveDialog()
        {saveOverlay.gameObject.SetActive(false);saveDialogKind=null;pendingTransition=pendingCancel=null;pendingSave=null;RefreshInterface();}
        private void CancelSaveDialog()
        {var cancel=pendingCancel;CloseSaveDialog();cancel?.Invoke();}
        private void OpenSaveBrowser()
        {
            BeginSaveDialog("browser","读取存档");
            var viewport=Rect(saveWindow,"Viewport",28,76,824,458);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var body=Rect(viewport,"Files",0,0,824,458);float y=0;
            try
            {
                var files=SaveFiles();if(files.Length==0)Text(body,"Empty","还没有新版存档，请先开始游戏并保存。",12,12,780,70,19);
                foreach(string file in files)
                {
                    string path=file;PlanState state=null;string error=null;try{state=ReadSave(path);}catch(Exception e){error=e.Message;}
                    string kind=Path.GetFileName(path).Contains("-auto")?"自动":"手动";
                    string info=kind+" · "+File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss")+"\n"+(state==null?"无法读取："+error:$"第{state.region}区 · {(int)state.seconds/60}:{(int)state.seconds%60:00} · {(state.hero=="female"?"女主":"男主")} · 现金 {state.cash/100m:0.00}");
                    Text(body,"Save information",info,10,y+6,680,76,17);
                    var button=Button(body,"载入 "+Path.GetFileName(path),698,y+14,112,46,()=>RequestLoadFile(path));button.transform.Find("Label").GetComponent<Text>().text="读取";button.interactable=state!=null;
                    y+=96;
                }
            }
            catch(Exception e){saveError.text="无法列出存档："+e.Message;}
            body.sizeDelta=new Vector2(824,Mathf.Max(458,y));var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=body;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            Button(saveWindow,"关闭",682,590,170,36,CloseSaveDialog);
        }
        private void RequestLoadFile(string path)
        {
            bool same=string.Equals(Path.GetFullPath(path),Path.GetFullPath(ManualPath),StringComparison.OrdinalIgnoreCase);
            RequestTransition(()=>TryLoadFile(path),"读取选中的存档",OpenSaveBrowser,same?(Func<bool>)SaveUnderNewSession:null);
        }
        private void RequestNewGame(string hero){RequestTransition(()=>StartGame(hero),"开始新的游戏");}
        private void RequestQuit(){RequestTransition(()=>{quitApproved=true;Application.Quit();},"退出游戏");}
        private bool WantsToQuit()
        {
            if(quitApproved||!Application.isPlaying||Array.IndexOf(Environment.GetCommandLineArgs(),"-planSmoke")>=0||!HasUnsavedProgress)return true;
            RequestQuit();return false;
        }
        private void RequestTransition(Action action,string description,Action cancel=null,Func<bool> save=null)
        {
            if(!HasUnsavedProgress){if(SaveDialogOpen)CloseSaveDialog();action();return;}
            BeginSaveDialog("confirm","保存当前进度？");pendingTransition=action;pendingCancel=cancel;pendingSave=save??(()=>TrySave());
            Text(saveWindow,"Description","当前经营进度尚未保存。\n即将"+description+"，请选择保存后继续、不保存继续，或取消。"+(cancel!=null?"\n\n选择保存后读取时，会保留被选中的存档，必要时另存当前进度。":""),28,108,824,190,21);
            Button(saveWindow,"保存并继续",28,350,250,50,()=>{if(pendingSave()){ProceedTransition();}else saveError.text=Notice;});
            Button(saveWindow,"不保存继续",310,350,250,50,ProceedTransition);
            Button(saveWindow,"取消",592,350,250,50,CancelSaveDialog);
        }
        private void ProceedTransition()
        {var action=pendingTransition;CloseSaveDialog();action?.Invoke();}
    }
}
