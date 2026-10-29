using UnityEngine;
using UnityEngine.EventSystems;

namespace AssemblyZero.Unity.EP04
{
    // Hover/press scale feedback for HUD buttons; layout keeps the original size so neighbours do not shift.
    public sealed class HardwareButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private bool hover,down;private RectTransform target;private float scale=1;
        private void Awake(){target=(RectTransform)transform;}
        public void OnPointerEnter(PointerEventData e){hover=true;HardwareAudio.Play("hover");}
        public void OnPointerExit(PointerEventData e){hover=false;down=false;}
        public void OnPointerDown(PointerEventData e){down=true;}
        public void OnPointerUp(PointerEventData e){down=false;}
        private void OnDisable(){hover=down=false;scale=1;if(target!=null)target.localScale=Vector3.one;}
        private void Update()
        {
            var goal=down?.95f:hover?1.04f:1;if(Mathf.Approximately(scale,goal))return;
            scale=Mathf.MoveTowards(scale,goal,Time.unscaledDeltaTime*1.2f);target.localScale=Vector3.one*scale;
        }
    }
}
