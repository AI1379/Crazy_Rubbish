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
        private IEnumerator SmokeGuideAndSelectionMotion(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("教程与选中动画："+message);}
            void Shot(string suffix){if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+suffix+".png"));}
            var saved=Model;Model=new PlanSimulation(LoadConfig());Model.State.recognized=true;Model.State.cash=100000;Model.Recruit();Model.SetPaused(true);MenuOpen=false;guideCollapsed=false;guideExpanded=false;BuildPresentation();HideManagement();RefreshPresentation(true);
            yield return null;
            Check(guideHud.gameObject.activeInHierarchy&&guideGoal.text.Contains("步骤 1"),"开局当前步骤未显示");
            var corners=new Vector3[4];guideHud.GetWorldCorners(corners);var timeCorners=new Vector3[4];timeHud.GetWorldCorners(timeCorners);
            Check(corners.All(p=>p.x>=0&&p.y>=0&&p.x<=Screen.width+.1f&&p.y<=Screen.height+.1f)&&Math.Abs(corners[2].x-timeCorners[2].x)<1&&corners[2].y<timeCorners[0].y,"未在倍速下方右对齐或超出屏幕");
            Vector2 point=(corners[0]+corners[2])/2;Check(PointerOverInterface(point),"教程点击穿透地图");BeginMapPointer(0,point);Check(pointerButton<0,"教程拖动误触地图");
            var player=Model.State.player;double x=player.x,y=player.y,seconds=Model.State.seconds;guideLocate.onClick.Invoke();Check(ContextOpen&&Model.State.seconds==seconds&&player.x==x&&player.y==y&&!player.directed,"目标定位移动了人物或推进时间");CloseContext();Shot("-tutorial-compact");
            guideFold.onClick.Invoke();Check(guideHud.sizeDelta.y==38&&!guideGoal.gameObject.activeSelf,"教程不能收起");guideFold.onClick.Invoke();Check(guideGoal.gameObject.activeSelf,"教程不能重新打开");
            guideDetails.onClick.Invoke();Canvas.ForceUpdateCanvases();Check(guideExpanded&&guideViewport.gameObject.activeSelf&&guideContent.GetComponentsInChildren<Button>().Any(b=>b.name=="前往旧仓库"),"完整教程或操作速查缺失");
            yield return null;Shot("-tutorial-expanded");
            var scroll=guideViewport.GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=.4f;RefreshGuideHud();Check(Math.Abs(scroll.verticalNormalizedPosition-.4f)<.02f,"刷新重置教程滚动");
            Model.State.tutorial=3;RefreshInterface();Check(guideGoal.text.Contains("发电机")&&guideTitle.text.Contains("4 / 7"),"当前教程未自动更新");
            EscapeInterface();Check(!guideExpanded&&!MenuOpen,"Esc没有收起完整说明");ShowManagement(3);Check(!guideHud.gameObject.activeSelf,"教程遮挡管理面板");HideManagement();Check(guideHud.gameObject.activeSelf,"关闭管理后教程没有恢复");
            ShowManagement(0);Check(!managementOpen&&guideExpanded&&guideHud.gameObject.activeSelf,"原教程入口没有打开右上教程");guideExpanded=false;RefreshGuideHud();
            MenuOpen=true;RefreshInterface();Check(!guideHud.gameObject.activeSelf,"主菜单仍显示教程");MenuOpen=false;RefreshInterface();

            Model.State.tutorial=0;player.x=10;player.y=14;var worker=Model.Alive.First();worker.x=12;worker.y=14;FocusJourneyPoint(14,14);RefreshPresentation(true);
            selectedPeople.AddRange(new[]{0,worker.id});RefreshSelectionMarkers();Model.CommandMove(new[]{0,worker.id},18,14);Model.SetPaused(false);Model.Advance(.1);RefreshPresentation(true);
            foreach(int id in new[]{0,worker.id})
            {
                var actor=actorViews[id];var rotation=actor.transform.localRotation;Check(actor.gameObject.activeInHierarchy&&Quaternion.Angle(rotation,Quaternion.identity)>.1f,"选中人物移动时没有摇晃或消失");
                OpenPeopleContext(new Vector2(Screen.width*.5f,Screen.height*.6f));RefreshPresentation(true);RefreshPresentation(true);
                Check(actor.gameObject.activeInHierarchy&&Quaternion.Angle(rotation,actor.transform.localRotation)<.001f,"选中菜单或重复刷新清除了行走摇晃");
            }
            CloseContext();Shot("-selected-walking");
            Model.SetPaused(true);RefreshPresentation(true);Check(actorViews.Values.All(a=>Quaternion.Angle(a.transform.localRotation,Quaternion.identity)<.001f),"暂停没有停止摇晃");
            Model.SetPaused(false);for(int i=0;i<300&&VisiblePeople.Any(p=>Math.Abs(p.x-p.commandX)+Math.Abs(p.y-p.commandY)>.05);i++){Model.Advance(.1);RefreshPresentation();}
            Model.Advance(.3);RefreshPresentation(true);Check(actorViews.Values.All(a=>Quaternion.Angle(a.transform.localRotation,Quaternion.identity)<.001f),"抵达后仍持续摇晃");
            Model=saved;guideCollapsed=false;guideExpanded=false;moveX=-1;BuildPresentation();RefreshPresentation(true);
        }
    }
}
