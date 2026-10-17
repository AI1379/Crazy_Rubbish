using System;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeHud
    {
        [SerializeField] private RectTransform saveBrowser,newSessionConfirmation;
        [SerializeField] private Text saveBrowserInfo,newSessionInfo;
        [SerializeField] private Button[] saveRows;
        [SerializeField] private Button savePrevious,saveNext,saveBeforeNew;
        private PrototypeSaveEntry[] availableSaves=Array.Empty<PrototypeSaveEntry>();
        private int savePage;

        private void BuildSessionControls()
        {
            interfaceScale=ButtonAt(modal.Find("Card"),"InterfaceScale","界面：自适应",710,20,198,30);
            saveBrowser=StretchPanel(root,"Save Browser",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0.03f,0.04f,0.04f,0.85f));
            saveBrowser.GetComponent<Image>().raycastTarget=true;
            var card=Surface(saveBrowser,"Card",0,0,940,650,Panel);card.anchorMin=card.anchorMax=card.pivot=new Vector2(0.5f,0.5f);card.anchoredPosition=Vector2.zero;
            TextAt(card,"Title","选择存档继续",28,20,760,40,28,Ink);
            ButtonAt(card,"CloseSaveBrowser","×",872,20,36,32);
            saveBrowserInfo=TextAt(card,"Summary","",28,76,880,52,16,Muted);
            saveRows=new Button[5];
            for(int i=0;i<saveRows.Length;i++)saveRows[i]=ButtonAt(card,"LoadSaveRow"+i,"",28,144+i*76,880,66);
            savePrevious=ButtonAt(card,"SavePrevious","上一页",28,546,184,38);
            saveNext=ButtonAt(card,"SaveNext","下一页",224,546,184,38);
            ButtonAt(card,"SaveLoadLatest","继续最近有效存档",456,546,452,38,true);
            TextAt(card,"Help","加载会替换当前会话。需要保留当前进度时，可先返回菜单保存。",28,600,880,30,14,Muted);
            saveBrowser.gameObject.SetActive(false);

            newSessionConfirmation=StretchPanel(root,"New Session Confirmation",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0.03f,0.04f,0.04f,0.85f));
            newSessionConfirmation.GetComponent<Image>().raycastTarget=true;
            var confirm=Surface(newSessionConfirmation,"Card",0,0,760,300,Panel);confirm.anchorMin=confirm.anchorMax=confirm.pivot=new Vector2(0.5f,0.5f);confirm.anchoredPosition=Vector2.zero;
            TextAt(confirm,"Title","开始一局新工坊",28,22,704,40,28,Ink);
            newSessionInfo=TextAt(confirm,"Info","",28,82,704,92,18,Ink);
            saveBeforeNew=ButtonAt(confirm,"SaveAndNewSession","保存后开始",28,214,216,48,true);
            ButtonAt(confirm,"ConfirmNewSession","直接开始新局",272,214,216,48);
            ButtonAt(confirm,"CancelNewSession","返回",516,214,216,48);
            newSessionConfirmation.gameObject.SetActive(false);
        }

        private static bool IsSessionCommand(string command) => command=="InterfaceScale"||command.StartsWith("LoadSaveRow")||command.StartsWith("SavePrevious")||command.StartsWith("SaveNext")||command=="SaveLoadLatest"||command=="CloseSaveBrowser"||command=="SaveAndNewSession"||command=="ConfirmNewSession"||command=="CancelNewSession";

        public void OpenSaveBrowser()
        {
            try{availableSaves=game.ListSavedGames();savePage=0;saveBrowser.gameObject.SetActive(true);saveBrowser.SetAsLastSibling();RefreshSessionControls();}
            catch(Exception error){game.Report(new CrazyRubbish.Core.ActionResult(false,"无法列出存档："+error.Message));}
        }

        private void RequestNewSession()
        {
            if(!game.SessionStarted){game.StartNewGame();return;}
            var m=game.Model;
            newSessionInfo.text=$"当前游戏 {(int)m.GameSeconds/60:00}:{(int)m.GameSeconds%60:00} · 现金 {m.Money:0.00}\n开始新局会替换当前会话。已有存档会保留；可先保存当前进度。";
            saveBeforeNew.interactable=!m.Failed&&!m.Interacting;
            newSessionConfirmation.gameObject.SetActive(true);newSessionConfirmation.SetAsLastSibling();
        }

        public bool TryCloseSessionOverlay()
        {
            foreach(var panel in new[]{saveBrowser,newSessionConfirmation})
                if(panel!=null&&panel.gameObject.activeSelf){panel.gameObject.SetActive(false);return true;}
            return false;
        }

        private void RefreshSessionControls()
        {
            if(saveBrowser==null||!saveBrowser.gameObject.activeSelf)return;
            int pages=Mathf.Max(1,(availableSaves.Length+4)/5);savePage=Mathf.Clamp(savePage,0,pages-1);
            saveBrowserInfo.text=availableSaves.Length==0?"还没有存档。返回菜单开始游戏，或保存当前进度。":$"共 {availableSaves.Length} 个存档 · 第 {savePage+1}/{pages} 页\n"+game.Notice;
            for(int i=0;i<saveRows.Length;i++)
            {
                int index=savePage*5+i;bool show=index<availableSaves.Length;saveRows[i].gameObject.SetActive(show);if(!show)continue;
                saveRows[i].GetComponentInChildren<Text>().text=availableSaves[index].Description;saveRows[i].interactable=availableSaves[index].Valid;
            }
            savePrevious.interactable=savePage>0;saveNext.interactable=savePage+1<pages;
        }

        private bool ExecuteSessionCommand(string command)
        {
            if(command.StartsWith("LoadSaveRow")&&int.TryParse(command.Substring(11),out int row))
            {int index=savePage*5+row;if(index<availableSaves.Length&&availableSaves[index].Valid)game.LoadSavedGame(availableSaves[index].Path);return true;}
            switch(command)
            {
                case "CloseSaveBrowser":saveBrowser.gameObject.SetActive(false);return true;
                case "SavePrevious":savePage--;return true;
                case "SaveNext":savePage++;return true;
                case "SaveLoadLatest":game.LoadLatest();return true;
                case "CancelNewSession":newSessionConfirmation.gameObject.SetActive(false);return true;
                case "ConfirmNewSession":game.StartNewGame();return true;
                case "SaveAndNewSession":game.SaveGame();if(game.LastActionSucceeded)game.StartNewGame();return true;
                default:return false;
            }
        }
    }
}
