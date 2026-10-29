using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareGame
    {
        public const float OrbitDuration=.25f;
        // Close macro framing: machines read ~1.9x larger than the old 25-unit overview.
        public const float DefaultZoom=13,MinZoom=8,MaxZoom=40,ZoomStep=2f;
        private static readonly Vector3 CameraOffset=new Vector3(26,42,-29);
        private float cameraYaw,orbitFrom,orbitTarget,orbitTime=OrbitDuration;
        private bool cameraFocused=true;
        private readonly System.Collections.Generic.List<RaycastResult> pointerHits=new System.Collections.Generic.List<RaycastResult>();
        public Vector3 CameraFocus=>cameraFocus;
        public float CameraYaw=>cameraYaw;
        public float TargetCameraYaw=>orbitTarget;
        public bool CanNavigate=>!Paused&&cameraFocused&&!TextInputFocused()&&Hud?.ProcurementOpen!=true&&Hud?.EquipmentLibraryOpen!=true&&Hud?.EquipmentDragging!=true;

        private bool TextInputFocused()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            return selected!=null&&(selected.GetComponentInParent<TMP_InputField>()!=null||selected.GetComponentInParent<UnityEngine.UI.InputField>()!=null);
        }
        private void OnApplicationFocus(bool value){cameraFocused=value;pan=false;}
        private bool PointerOverUi(Vector2 point)
        {
            if(EventSystem.current==null)return false;
            pointerHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},pointerHits);return pointerHits.Count>0;
        }
        public void FocusGroundPoint(Vector3 point)
        {
            if(!CanNavigate)return;
            cameraFocus=new Vector3(Mathf.Clamp(point.x,0,(AssemblyZero.Domain.EP04.GridRouting.WorldWidth-1)*1.2f),0,
                Mathf.Clamp(point.z,0,(AssemblyZero.Domain.EP04.GridRouting.WorldDepth-1)*1.2f));
        }
        private Vector3 GroundRight=>Vector3.ProjectOnPlane(ViewCamera.transform.right,Vector3.up).normalized;
        private Vector3 GroundForward=>Vector3.Cross(GroundRight,Vector3.up);
        public void MoveView(Vector2 input,float deltaTime)
        {
            if(!CanNavigate)return;
            input=Vector2.ClampMagnitude(input,1);
            FocusGroundPoint(cameraFocus+(GroundRight*input.x+GroundForward*input.y)*(24*ViewCamera.orthographicSize/25*Mathf.Min(deltaTime,.05f)));
        }
        public void PanView(Vector2 pixels)
        {
            if(!CanNavigate)return;
            FocusGroundPoint(cameraFocus-(GroundRight*pixels.x+GroundForward*pixels.y)*(ViewCamera.orthographicSize/500));
        }
        public void ZoomView(float scroll)
        {
            if(CanNavigate)ViewCamera.orthographicSize=Mathf.Clamp(ViewCamera.orthographicSize-scroll*ZoomStep*ViewCamera.orthographicSize/25,MinZoom,MaxZoom);
        }
        public void RotateViewStep(int direction)
        {
            if(!CanNavigate||direction==0)return;
            orbitFrom=cameraYaw;orbitTarget+=Mathf.Sign(direction)*45;orbitTime=0;
        }
        private void UpdateCamera(float deltaTime)
        {
            if(CanNavigate&&orbitTime<OrbitDuration)
            {
                orbitTime=Mathf.Min(OrbitDuration,orbitTime+deltaTime);
                cameraYaw=Mathf.Lerp(orbitFrom,orbitTarget,Mathf.SmoothStep(0,1,orbitTime/OrbitDuration));
            }
            var offset=Quaternion.AngleAxis(cameraYaw,Vector3.up)*CameraOffset;
            ViewCamera.transform.SetPositionAndRotation(cameraFocus+offset,Quaternion.LookRotation(-offset));
        }
    }
}
