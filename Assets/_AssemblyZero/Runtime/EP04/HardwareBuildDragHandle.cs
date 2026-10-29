using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AssemblyZero.Unity.EP04
{
    public sealed class HardwareBuildDragHandle : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler,IPointerClickHandler
    {
        public HardwareHud Hud;
        public BuildingKind Kind;
        public int Slot=-1;
        public bool HasDevice=true;

        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||!HasDevice||Hud==null)return;
            e.eligibleForClick=false;Hud.BeginEquipmentDrag(Kind,e.position);
        }
        public void OnDrag(PointerEventData e){if(Hud!=null)Hud.MoveEquipmentDrag(e.position);}
        public void OnEndDrag(PointerEventData e){if(Hud!=null)Hud.EndEquipmentDrag();}
        public void OnDrop(PointerEventData e){if(Slot>=0&&Hud!=null)Hud.DropEquipmentShortcut(Slot);}
        public void OnPointerClick(PointerEventData e)
        {
            if(e.button==PointerEventData.InputButton.Right&&Slot>=0&&Hud!=null)Hud.OpenEquipmentLibraryForSlot(Slot);
        }
    }
}
