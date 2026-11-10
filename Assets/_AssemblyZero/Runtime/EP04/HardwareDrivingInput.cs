using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareGame
    {
        private int drivingPreviousSpeed=1;
        private void SetDrivingInput(float throttle,float steer)
        {
            if(Truck.Throttle==throttle&&Truck.Steer==steer)return;
            World.Enqueue(new WorldCommand{Kind=CommandKind.Drive,Throttle=throttle,Steer=steer});World.FlushCommands();
        }
        private void HandleDrivingInput()
        {
            ResetFogPointer();pan=false;View.HideGhost();HoverNode=0;
            var k=Keyboard.current;
            if(k==null||!cameraFocused||TextInputFocused()||Hud.ProcurementOpen||Hud.EquipmentLibraryOpen||Hud.EquipmentDragging)
            {SetDrivingInput(0,0);return;}
            if(k.escapeKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame){SetDrivingInput(0,0);TogglePause();return;}
            if(Paused){SetDrivingInput(0,0);return;}
            if(k.tabKey.wasPressedThisFrame){SetDrivingInput(0,0);ToggleDriving();return;}
            SetDrivingInput((k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0),(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0));
            if(k.eKey.wasPressedThisFrame)Submit(new WorldCommand{Kind=CommandKind.Interact});
            if(k.fKey.wasPressedThisFrame)Submit(new WorldCommand{Kind=CommandKind.RightVehicle});
            var mouse=Mouse.current;if(mouse!=null&&!PointerOverUi(mouse.position.ReadValue()))ZoomView(mouse.scroll.ReadValue().y);
        }
    }
}
