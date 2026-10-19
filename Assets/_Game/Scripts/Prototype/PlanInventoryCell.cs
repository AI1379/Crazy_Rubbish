using UnityEngine;
using UnityEngine.EventSystems;
namespace CrazyRubbish.Prototype
{
    public sealed class PlanInventoryCell : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler
    {
        public PlanGameController game;
        public int container, index;
        private int dragFrame=-1;
        public void OnPointerClick(PointerEventData e)
        {
            if(Time.frameCount==dragFrame||e.button!=PointerEventData.InputButton.Left&&e.button!=PointerEventData.InputButton.Right)return;
            game.ClickPlanInventory(container,index,e.button==PointerEventData.InputButton.Right,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
        }
        public void OnBeginDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;dragFrame=Time.frameCount;game.BeginPlanInventoryDrag(container,index);}
        public void OnDrag(PointerEventData e)=>game.MovePlanInventoryHand(e.position);
        public void OnEndDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;dragFrame=Time.frameCount;
            var target=e.pointerCurrentRaycast.gameObject?.GetComponentInParent<PlanInventoryCell>();
            if(target!=null&&target.game==game)game.EndPlanInventoryDrag(target.container,target.index);
        }
        public void OnPointerEnter(PointerEventData e)=>game.InspectPlanInventory(container,index);
    }
}
