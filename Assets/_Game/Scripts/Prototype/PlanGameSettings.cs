using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform settingsMenu;
        private Button settingsButton;
        private PlanGearIcon settingsIcon;
        private Text settingsCaption;
        private bool SettingsOpen=>settingsMenu!=null&&settingsMenu.gameObject.activeSelf;

        private void BuildSettings(RectTransform footer)
        {
            settingsButton=Button(footer,"Settings",0,10,52,44,ToggleSettings);
            var r=(RectTransform)settingsButton.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-12,-10);
            settingsButton.transform.Find("Label").gameObject.SetActive(false);
            settingsButton.navigation=new Navigation{mode=Navigation.Mode.None};
            settingsIcon=Rect(r,"Gear",14,3,24,24).gameObject.AddComponent<PlanGearIcon>();settingsIcon.color=Ink;settingsIcon.raycastTarget=false;
            settingsCaption=Text(r,"Caption","设置",0,28,52,13,11);settingsCaption.alignment=TextAnchor.MiddleCenter;
            settingsMenu=Box(planCanvas,"Settings Menu",0,0,286,348);
            settingsMenu.anchorMin=settingsMenu.anchorMax=new Vector2(1,0);settingsMenu.pivot=new Vector2(1,0);settingsMenu.anchoredPosition=new Vector2(-32,158);
            settingsMenu.GetComponent<Image>().raycastTarget=true;
            Text(settingsMenu,"Title","设置",16,12,200,32,21);
            Button(settingsMenu,"×",238,10,32,32,CloseSettings);
            Button(settingsMenu,"保存游戏",12,62,262,36,()=>{CloseSettings();Save();RefreshInterface();});
            Button(settingsMenu,"读取存档",12,104,262,36,OpenSaveBrowser);
            Button(settingsMenu,"主菜单",12,146,262,36,()=>{CloseSettings();MenuOpen=true;RebuildPanel();});
            Button(settingsMenu,"退出",12,188,262,36,RequestQuit);
            Button(settingsMenu,"操作说明",12,230,262,36,OpenPlayerHelp);
            planSizeButton=Button(settingsMenu,"界面大小："+PlanInterfaceSizeName,12,272,262,36,CyclePlanInterfaceSize);
            Text(settingsMenu,"Hint","空格暂停 · Esc关闭",16,318,254,22,13);
            settingsMenu.gameObject.SetActive(false);
        }

        private void ToggleSettings()
        {
            if(MenuOpen||SaveDialogOpen)return;
            if(SettingsOpen){CloseSettings();return;}
            CancelMapGesture();CloseContext();HideManagement();settingsMenu.gameObject.SetActive(true);settingsMenu.SetAsLastSibling();RefreshSettings();
        }

        private void CloseSettings()
        {if(settingsMenu!=null)settingsMenu.gameObject.SetActive(false);RefreshSettingsButton();}

        private void RefreshSettingsButton()
        {
            if(settingsButton==null)return;
            settingsButton.interactable=!MenuOpen&&!SaveDialogOpen;
            settingsButton.targetGraphic.color=SettingsOpen?Accent:new Color32(48,67,70,255);
            settingsIcon.color=settingsCaption.color=SettingsOpen?new Color32(24,35,36,255):Ink;
        }

        private void RefreshSettings()
        {if((MenuOpen||SaveDialogOpen)&&settingsMenu!=null)settingsMenu.gameObject.SetActive(false);RefreshSettingsButton();}

        // Consume the dismissing click so it does not also build, select or move on the map.
        private bool DismissSettingsAt(Vector2 point)
        {
            if(!SettingsOpen||RectTransformUtility.RectangleContainsScreenPoint(settingsMenu,point,null)||RectTransformUtility.RectangleContainsScreenPoint((RectTransform)settingsButton.transform,point,null))return false;
            CloseSettings();return true;
        }
    }
}
