using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity
{
    public sealed class EP03FreeCamera : MonoBehaviour
    {
        public EP03SimulationDriver Driver;
        public Camera View;
        public float MoveSpeed = 12, LookSensitivity = .12f;
        public bool IsActive { get; private set; }
        public bool IsLooking { get; private set; }
        public float CurrentSpeed { get; private set; } = 12;
        private float yaw, pitch;
        private CursorLockMode previousLock;
        private bool previousCursorVisible, focused = true;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public void Begin()
        {
            if (IsActive || Driver == null || View == null) return;
            var source = Driver.Cinematic != null && Driver.Cinematic.IsPlaying ? Driver.Cinematic.FilmCamera : Driver.Cameras.Camera;
            var position = source.transform.position; var rotation = source.transform.rotation; var fov = source.fieldOfView;
            if (Driver.Cinematic != null) Driver.Cinematic.Stop();
            View.transform.SetPositionAndRotation(position, rotation); View.fieldOfView = fov; View.orthographic = false;
            yaw = rotation.eulerAngles.y; pitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
            CurrentSpeed = Mathf.Clamp(MoveSpeed, .5f, 80);
            Driver.Cameras.Camera.enabled = false; View.enabled = true; IsActive = true;
        }
        public void Stop()
        {
            ReleaseMouse();
            if (!IsActive) return;
            IsActive = false; if (View != null) View.enabled = false;
            if (Driver != null && Driver.Cameras != null) Driver.Cameras.Camera.enabled = true;
        }
        public void Toggle() { if (IsActive) Stop(); else Begin(); }

        private void Update()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard != null && keyboard.gKey.wasPressedThisFrame) Toggle();
            if (!IsActive) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Stop(); return; }
            if (!focused || mouse == null) { ReleaseMouse(); return; }
            if (mouse.rightButton.wasPressedThisFrame && !OverUi()) CaptureMouse();
            if (!mouse.rightButton.isPressed) ReleaseMouse();
            var scroll = mouse.scroll.ReadValue().y / 120f;
            if (!IsLooking)
            {
                if (Mathf.Abs(scroll) > .001f && !OverUi()) View.transform.position += View.transform.forward * (scroll * Mathf.Max(1, CurrentSpeed * .15f));
                return;
            }
            if (Mathf.Abs(scroll) > .001f) CurrentSpeed = Mathf.Clamp(CurrentSpeed * Mathf.Pow(1.2f, scroll), .5f, 80);
            var delta = mouse.delta.ReadValue(); yaw += delta.x * LookSensitivity; pitch = Mathf.Clamp(pitch - delta.y * LookSensitivity, -89, 89);
            View.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (keyboard == null) return;
            var right = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            var forward = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            var vertical = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
            var movement = View.transform.right * right + View.transform.forward * forward + Vector3.up * vertical;
            var boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 4 : 1;
            View.transform.position += Vector3.ClampMagnitude(movement, 1) * (CurrentSpeed * boost * Mathf.Min(Time.unscaledDeltaTime, .05f));
        }
        private bool OverUi()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() }, uiHits);
            return uiHits.Count > 0;
        }
        private void CaptureMouse()
        {
            if (IsLooking) return;
            previousLock = Cursor.lockState; previousCursorVisible = Cursor.visible; IsLooking = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        private void ReleaseMouse()
        {
            if (!IsLooking) return;
            IsLooking = false; Cursor.lockState = previousLock; Cursor.visible = previousCursorVisible;
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) ReleaseMouse(); }
        private void OnDisable() => Stop();
    }
}
