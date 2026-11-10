using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareGame
    {
        private bool fogPointerHeld;
        private Vector3 fogPreviousGround;
        private Vector2 fogPreviousScreen;
        private void ResetFogPointer(){if(fogPointerHeld)Fog?.EndDragStroke();fogPointerHeld=false;}
        private void GuardFogPointer()
        {
            var mouse=Mouse.current;
            if(mouse==null||Keyboard.current==null||!cameraFocused||Paused||Showcasing||Placing.HasValue||TextInputFocused()||
                Hud.ProcurementOpen||Hud.EquipmentLibraryOpen||Hud.EquipmentDragging||
                !mouse.leftButton.isPressed||!ViewCamera.pixelRect.Contains(mouse.position.ReadValue()))ResetFogPointer();
        }
        private void HandleFogPointer(Mouse mouse,Vector3 ground)
        {
            var screen=mouse.position.ReadValue();
            if(!ViewCamera.pixelRect.Contains(screen)){ResetFogPointer();return;}
            if(mouse.leftButton.wasPressedThisFrame)
            {
                fogPointerHeld=true;fogPreviousGround=ground;fogPreviousScreen=screen;
                Fog?.AddImpulse(ground,GroundFog.ClickRadius,GroundFog.ClickStrength);
            }
            else if(fogPointerHeld&&mouse.leftButton.isPressed)
            {
                // Camera motion beneath a stationary pointer is not a brush stroke.
                if((screen-fogPreviousScreen).sqrMagnitude>=1)
                    Fog?.AddDragStroke(fogPreviousGround,ground,Time.unscaledDeltaTime);
                fogPreviousGround=ground;fogPreviousScreen=screen;
            }
        }
    }
}
