using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity
{
    // Lives on the factory authority, outside every Canvas, so H also restores hidden UI.
    public sealed class EP03UiVisibility : MonoBehaviour
    {
        public bool Hidden { get; private set; }
        private readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
        private readonly Dictionary<UnityEngine.UI.GraphicRaycaster, bool> raycasters = new Dictionary<UnityEngine.UI.GraphicRaycaster, bool>();
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) Toggle();
        }
        public void Toggle() => SetHidden(!Hidden);
        public void SetHidden(bool hidden)
        {
            if (hidden == Hidden) return;
            Hidden = hidden;
            if (hidden)
            {
                canvases.Clear(); raycasters.Clear();
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (canvas.gameObject.scene != gameObject.scene) continue;
                    canvases.Add(canvas, canvas.enabled); canvas.enabled = false;
                    foreach (var raycaster in canvas.GetComponents<UnityEngine.UI.GraphicRaycaster>())
                    {
                        raycasters.Add(raycaster, raycaster.enabled); raycaster.enabled = false;
                    }
                }
            }
            else
            {
                foreach (var entry in canvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
                foreach (var entry in raycasters) if (entry.Key != null) entry.Key.enabled = entry.Value;
                canvases.Clear(); raycasters.Clear();
            }
        }
        private void OnDisable() => SetHidden(false);
    }
}
