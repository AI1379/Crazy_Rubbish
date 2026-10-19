using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private IEnumerator SmokeTimeHud(string screenshot)
        {
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("时间控制条："+message);}
            var saved=Model;bool wasPaused=Model.State.paused;int oldSpeed=Model.State.speed;Model.SetPaused(true);MenuOpen=false;RefreshInterface();yield return null;
            Check(timeHud!=null&&timeButtons.Length==4&&timeHud.gameObject.activeInHierarchy,"控件缺失");
            Check(planCanvas.Find("Header/暂停")==null&&planCanvas.Find("Header/1×")==null,"旧按钮未移除");
            var corners=new Vector3[4];timeHud.GetWorldCorners(corners);
            Check(corners[0].x>=0&&corners[0].y>=0&&corners[2].x<=Screen.width+.1f&&corners[2].y<=Screen.height+.1f,"控件超出屏幕");
            var header=(RectTransform)planCanvas.Find("Header");var headerCorners=new Vector3[4];header.GetWorldCorners(headerCorners);
            Check(Vector3.Distance(corners[2],headerCorners[2])<.1f,"未贴合顶栏右上角");
            var manage=(RectTransform)planCanvas.Find("Operations/管理 Tab");manage.GetWorldCorners(headerCorners);Check(headerCorners[2].y<corners[0].y,"与管理按钮重叠");
            Check(PointerOverInterface((corners[0]+corners[2])/2),"点击控制条会穿透至地图");
            for(int speed=1;speed<=3;speed++)
            {
                Model.SetPaused(true);timeButtons[speed].onClick.Invoke();
                Check(!Model.State.paused&&Model.State.speed==speed,"点击速度未恢复运行");
                double before=Model.State.seconds;Model.Advance(.5);Check(Math.Abs(Model.State.seconds-before-.5*speed)<.00001,"实际时间倍率错误");
                Check(timeButtons[speed].targetGraphic.color==Accent&&timeButtons[0].targetGraphic.color!=Accent,"运行高亮错误");
            }
            timeButtons[0].onClick.Invoke();double pausedAt=Model.State.seconds;Model.Advance(.5);
            Check(Model.State.paused&&Math.Abs(Model.State.seconds-pausedAt)<.00001&&timeButtons[0].targetGraphic.color==Accent,"暂停或高亮错误");
            timeButtons[0].onClick.Invoke();Check(!Model.State.paused&&Model.State.speed==3,"继续时未保留原速度");
            ApplyTimeControl(0);Check(Model.State.paused,"空格共用入口未暂停");ApplyTimeControl(2);Check(!Model.State.paused&&Model.State.speed==2,"数字键共用入口未恢复");ApplyTimeControl(0);
            if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-time-paused.png"));
            ApplyTimeControl(3);
            if(screenshot!=null)PrototypeSmokeRunner.CapturePreview(Path.Combine(Path.GetDirectoryName(screenshot),Path.GetFileNameWithoutExtension(screenshot)+"-time-controls.png"));
            Model.SetPaused(true);MenuOpen=true;RefreshInterface();Check(!timeHud.gameObject.activeSelf,"主菜单未隐藏时间控制");ApplyTimeControl(1);Check(Model.State.paused,"主菜单中改变了时间");
            MenuOpen=false;Model=saved;Model.State.speed=oldSpeed;Model.SetPaused(wasPaused);RefreshInterface();RefreshPresentation(true);
        }
    }
}
