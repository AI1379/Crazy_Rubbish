using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private void OpenPlayerHelp()
        {
            BeginSaveDialog("help","操作说明 · v"+Application.version);
            var viewport=Rect(saveWindow,"Help viewport",28,76,824,458);
            viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var body=Rect(viewport,"Contents",0,0,824,1000);
            string help="目标与开局\n回收垃圾、照顾居民，逐步购买三区土地，重建升降舱返回上层。先收好食物、水和手锯，手工锯出木板；发电并实际供应住房或路灯后，才能招募居民。右上教程可展开查看具体步骤。\n\n"+
                "基本操作\nWASD或左键短按空地移动主角；左键拖动地图，滚轮缩放。右键短按查看物品或设施，右键拖动框选。选中人物后右键地面下达移动命令，到达后待命；在人物菜单选择恢复工作，才会重新自动找岗位。E拾取、Q入库或投料、F参加所选设施工作。\n\n"+
                "建设与查阅\nB打开建设，I打开库存，Tab打开管理，R旋转，右键或Esc取消放置。设施需要选择配方、投入原料，并留下操作侧通路。物品和设施菜单可查制作方法；窗口标题栏可拖动。\n\n"+
                "时间与经营\n空格暂停或继续，1/2/3选择速度；M查看全图，Home回主角。优先保障饮水、食物和当期工资。发薪到期需要在居民页手动支付；缺工具、缺原料、供电或维护不足会让产线停工。电动设备仍需要维护和物流。\n\n"+
                "存档与退出\n底部齿轮可以保存、读取和退出。每5个游戏分钟轮换保存3份自动档；建议重要扩建和返程前手动保存。打开本说明和存档窗口时经营停止，关闭后保持原暂停与速度状态。结局不会覆盖失败前的手动存档，可读档或新开局。\n\n"+
                "首版说明\n当前提供三个区域与返程结局。生活产线宜分步投入，一块种植床只能补充食物，不能直接供应全部人口。详细已知问题见下载包内说明。";
            var text=Text(body,"Help text",help,8,8,800,1000,19);
            float height=Mathf.Max(458,text.preferredHeight+24);text.rectTransform.sizeDelta=new Vector2(800,height-16);body.sizeDelta=new Vector2(824,height);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=body;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            Button(saveWindow,"关闭",682,590,170,36,CloseSaveDialog);
        }
    }
}
