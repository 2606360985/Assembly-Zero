using System;
using UnityEngine;
namespace AssemblyZero.Unity
{
    [Serializable] public struct EP02CameraPose { public string Name; public Vector3 Position, Euler; public float FieldOfView; }
    public sealed class EP02CameraDirector : MonoBehaviour
    {
        public Camera Camera;
        public EP02CameraPose[] Presets = Array.Empty<EP02CameraPose>();
        public int Selected { get; private set; }
        public void SelectStage(int stage) { Select(stage <= 2 ? 1 : stage == 5 ? 3 : stage == 6 ? 2 : 0); }
        public void Next() { Select((Selected + 1) % Presets.Length); }
        private void Select(int index) { if (Camera == null || Presets.Length == 0) return; Selected = Mathf.Clamp(index, 0, Presets.Length - 1); var p = Presets[Selected]; Camera.transform.SetPositionAndRotation(p.Position, Quaternion.Euler(p.Euler)); Camera.fieldOfView = p.FieldOfView; }
    }
}
