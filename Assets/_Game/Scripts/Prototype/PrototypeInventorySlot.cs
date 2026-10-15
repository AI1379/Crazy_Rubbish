using UnityEngine;
using UnityEngine.EventSystems;

namespace CrazyRubbish.Prototype
{
    public sealed class PrototypeInventorySlot : MonoBehaviour,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IPointerEnterHandler
    {
        public PrototypeHud hud;
        // 0 backpack, 1 warehouse, 2 nearby ground.
        public int container,index;
        private int lastDragFrame=-1;
        public void OnPointerClick(PointerEventData e)
        {
            if(Time.frameCount==lastDragFrame||e.button!=PointerEventData.InputButton.Left&&e.button!=PointerEventData.InputButton.Right)return;
            hud.ClickInventorySlot(container,index,e.button==PointerEventData.InputButton.Right,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
        }
        public void OnBeginDrag(PointerEventData e)
        {lastDragFrame=Time.frameCount;if(e.button==PointerEventData.InputButton.Left)hud.BeginInventoryDrag(container,index);}
        public void OnDrag(PointerEventData e){}
        public void OnEndDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            lastDragFrame=Time.frameCount;
            var target=e.pointerCurrentRaycast.gameObject?.GetComponentInParent<PrototypeInventorySlot>();
            if(target!=null&&target.hud==hud)hud.EndInventoryDrag(target.container,target.index);
        }
        public void OnPointerEnter(PointerEventData e)=>hud.InspectInventorySlot(container,index);
    }
}
