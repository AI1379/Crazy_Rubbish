using UnityEngine;

namespace CrazyRubbish.Prototype
{
    public sealed partial class PrototypeWorld
    {
        private bool pointerCaptured, draggingMap;
        private Vector2 pointerStart, pointerLast;
        public bool DraggingMap => pointerCaptured && draggingMap;

        public void BeginMapDrag(Vector2 screenPoint)
        {
            pointerCaptured=true;draggingMap=false;pointerStart=pointerLast=screenPoint;
        }
        public void ContinueMapDrag(Vector2 screenPoint)
        {
            if(!pointerCaptured)return;
            if(!draggingMap && Vector2.Distance(pointerStart,screenPoint)<6)return;
            draggingMap=true;
            Vector2 delta=screenPoint-pointerLast;pointerLast=screenPoint;
            float units=worldCamera.orthographicSize*2/Mathf.Max(1,worldCamera.pixelHeight);
            worldCamera.transform.position-=new Vector3(delta.x,delta.y,0)*units;
            ClampCamera(game.Model);
            UpdateHover(null,Time.unscaledTimeAsDouble);
        }
        public void EndMapDrag(Vector2 screenPoint,bool allowClick)
        {
            if(!pointerCaptured)return;
            ContinueMapDrag(screenPoint);
            bool click=allowClick&&!draggingMap;
            CancelMapDrag();
            if(click)HandleMapClick(worldCamera.ScreenToWorldPoint(screenPoint),false);
        }
        public void CancelMapDrag(){pointerCaptured=false;draggingMap=false;}
    }
}
