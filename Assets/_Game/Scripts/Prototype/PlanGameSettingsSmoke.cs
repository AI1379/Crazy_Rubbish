using System;
using System.Collections;
using System.IO;
using System.Linq;
using CrazyRubbish.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeSettings(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("底部设置："+message);}
            void Click(string name){settingsMenu.Find(name).GetComponent<Button>().onClick.Invoke();}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            Vector2 Centre(RectTransform r)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            void Fits(RectTransform r)
            {var corners=new Vector3[4];r.GetWorldCorners(corners);foreach(var v in corners)Check(v.x>=-.1f&&v.y>=-.1f&&v.x<=Screen.width+.1f&&v.y<=Screen.height+.1f,"设置控件超出屏幕");}
            var saved=Model;Model=new PlanSimulation(LoadConfig(),1,JsonUtility.FromJson<PlanState>(JsonUtility.ToJson(saved.Capture())));Model.SetPaused(true);MenuOpen=false;BuildPresentation();RefreshInterface();yield return null;
            Check(!SettingsOpen&&settingsButton.gameObject.activeInHierarchy,"默认状态不正确");
            Check(planCanvas.Find("Header/保存")==null&&planCanvas.Find("Header/读档")==null&&planCanvas.Find("Header/菜单")==null&&planCanvas.Find("Header/管理 Tab")==null,"顶部旧入口残留");
            Fits((RectTransform)settingsButton.transform);Check(Centre((RectTransform)settingsButton.transform).y<Screen.height*.2f,"齿轮未放在底栏");
            settingsButton.onClick.Invoke();yield return null;
            Check(SettingsOpen&&Model.State.paused,"打开设置改变了暂停状态");Fits(settingsMenu);
            Check(PointerOverInterface(Centre(settingsMenu)),"面板不阻挡地图操作");BeginMapPointer(0,Centre(settingsMenu));Check(pointerButton<0,"面板点击开始拖图");
            Check(!DismissSettingsAt(Centre(settingsMenu))&&SettingsOpen,"点击面板内部误关闭");
            Shot("-settings");EscapeInterface();Check(!SettingsOpen&&!MenuOpen,"Esc未只关闭设置");
            settingsButton.onClick.Invoke();Check(DismissSettingsAt(new Vector2(Screen.width*.4f,Screen.height*.5f))&&!SettingsOpen,"外部点击未关闭并消耗");
            settingsButton.onClick.Invoke();settingsButton.onClick.Invoke();Check(!SettingsOpen,"再次点击齿轮未关闭");
            settingsButton.onClick.Invoke();ShowManagement(3);Check(!SettingsOpen&&managementOpen,"管理面板未关闭设置");HideManagement();
            settingsButton.onClick.Invoke();long cash=Model.State.cash;int speed=Model.State.speed;
            Click("保存游戏");Check(!SettingsOpen&&Notice=="已保存新版进度。","保存按钮未执行保存");Model.State.cash+=999;
            settingsButton.onClick.Invoke();Click("读取存档");Check(SaveDialogOpen&&saveDialogKind=="browser","未显示存档列表");
            saveWindow.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("载入 ")).onClick.Invoke();
            if(saveDialogKind=="confirm")saveWindow.Find("不保存继续").GetComponent<Button>().onClick.Invoke();
            Check(Model.State.cash==cash&&Model.State.speed==speed&&Model.State.paused&&!SettingsOpen&&!MenuOpen,"读取按钮未恢复刚保存的状态");yield return null;
            settingsButton.onClick.Invoke();Click("主菜单");Check(MenuOpen&&!SettingsOpen&&!settingsButton.interactable,"主菜单入口不正确");EscapeInterface();Check(!MenuOpen&&settingsButton.interactable,"返回经营后齿轮未恢复");
            Model.SetPaused(false);settingsButton.onClick.Invoke();double before=Model.State.seconds;Model.Advance(.2);Check(Model.State.seconds>before,"设置意外暂停模拟");Model.SetPaused(true);CloseSettings();RefreshInterface();Shot("-bottom-ui");
            Model=saved;BuildPresentation();RefreshPresentation(true);
        }
    }
}
