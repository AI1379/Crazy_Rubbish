using UnityEngine;
using UnityEngine.UI;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PlanGameController
    {
        private RectTransform timeHud;
        private Text timeReadout;
        private Button[] timeButtons;
        private PlanTimeIcon[] timeIcons;
        private Text[] timeKeys;

        private void BuildTimeHud(RectTransform header)
        {
            // Attach to the header edge rather than placing a floating window over the map.
            timeHud=Box(header,"Time controls",0,0,256,70);
            timeHud.anchorMin=timeHud.anchorMax=timeHud.pivot=new Vector2(1,1);
            timeHud.anchoredPosition=Vector2.zero;
            var background=timeHud.GetComponent<Image>();background.color=new Color32(19,31,34,255);background.raycastTarget=true;
            var edge=Rect(timeHud,"Inset edge",0,0,2,70).gameObject.AddComponent<Image>();edge.color=new Color32(64,80,80,255);edge.raycastTarget=false;
            timeReadout=Text(timeHud,"Clock","",10,4,236,21,14);timeReadout.alignment=TextAnchor.MiddleCenter;
            timeButtons=new Button[4];timeIcons=new PlanTimeIcon[4];timeKeys=new Text[4];
            for(int i=0;i<4;i++)
            {
                int command=i;var b=Button(timeHud,i==0?"Pause":"Speed "+i,8+i*60,28,54,36,()=>ApplyTimeControl(command));
                b.transform.Find("Label").gameObject.SetActive(false);timeButtons[i]=b;
                var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.18f,1.18f,1.18f);colors.pressedColor=new Color(.8f,.8f,.8f);colors.selectedColor=Color.white;b.colors=colors;
                b.navigation=new Navigation{mode=Navigation.Mode.None};
                var icon=Rect(b.transform,"Icon",17,3,20,17).gameObject.AddComponent<PlanTimeIcon>();icon.speed=i;icon.raycastTarget=false;timeIcons[i]=icon;
                var key=Text(b.transform,"Shortcut",i==0?"Space":i.ToString(),0,21,54,13,10);key.alignment=TextAnchor.MiddleCenter;timeKeys[i]=key;
            }
            RefreshTimeHud();
        }

        private void ApplyTimeControl(int command)
        {
            if(MenuOpen||SaveDialogOpen||Model==null||!string.IsNullOrEmpty(Model.State.ending))return;
            if(command==0)Model.SetPaused(!Model.State.paused);
            else {Model.State.speed=Mathf.Clamp(command,1,3);Model.SetPaused(false);}
            RefreshInterface();
        }

        private void RefreshTimeHud()
        {
            if(timeHud==null||Model==null)return;
            timeHud.gameObject.SetActive(!MenuOpen&&!SaveDialogOpen);var st=Model.State;int clock=(int)st.seconds;
            timeReadout.text=$"{clock/60}:{clock%60:00}   "+(st.paused?"暂停":st.speed+"×");
            for(int i=0;i<4;i++)
            {
                bool active=st.paused?i==0:i==st.speed;
                timeButtons[i].targetGraphic.color=active?Accent:new Color32(35,49,52,255);
                timeIcons[i].color=timeKeys[i].color=active?new Color32(24,35,36,255):Ink;
            }
        }
    }
}
