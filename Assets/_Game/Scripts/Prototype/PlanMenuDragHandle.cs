using UnityEngine;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed class PlanMenuDragHandle : MonoBehaviour,IInitializePotentialDragHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        [SerializeField] private RectTransform window;
        private RectTransform parent;
        private Vector2 pointerStart,windowStart;
        private bool dragging;

        public void Bind(RectTransform target){window=target;}
        public void OnInitializePotentialDrag(PointerEventData data){data.useDragThreshold=true;}
        public void OnBeginDrag(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left||window==null)return;
            parent=window.parent as RectTransform;
            if(parent==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,data.pressEventCamera,out pointerStart))return;
            windowStart=window.anchoredPosition;window.SetAsLastSibling();dragging=true;
        }
        public void OnDrag(PointerEventData data)
        {
            if(!dragging||data.button!=PointerEventData.InputButton.Left||window==null||parent==null)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,data.pressEventCamera,out var pointer))return;
            window.anchoredPosition=windowStart+pointer-pointerStart;
            // The context window uses a top-left anchor and pivot in canvas coordinates.
            const float margin=8;
            float xMax=Mathf.Max(margin,parent.rect.width-window.rect.width-margin);
            float yMax=Mathf.Max(margin,parent.rect.height-window.rect.height-margin);
            var position=window.anchoredPosition;
            window.anchoredPosition=new Vector2(Mathf.Clamp(position.x,margin,xMax),Mathf.Clamp(position.y,-yMax,-margin));
        }
        public void OnEndDrag(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)dragging=false;}
        private void OnDisable(){dragging=false;}
    }
}
