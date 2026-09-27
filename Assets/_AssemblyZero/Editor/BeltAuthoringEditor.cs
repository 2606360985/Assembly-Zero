using UnityEditor;
using UnityEngine;

namespace AssemblyZero.Unity.Editor
{
    [CustomEditor(typeof(BeltAuthoring))]
    public sealed class BeltAuthoringEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var belt = (BeltAuthoring)target; var start = belt.transform.position + Vector3.up * 0.3f; var dir2 = BeltAuthoring.DirectionVector(belt.Direction); var direction = new Vector3(dir2.x, 0, dir2.y);
            Handles.color = Color.cyan; Handles.ArrowHandleCap(0, start, Quaternion.LookRotation(direction), 0.55f, EventType.Repaint);
            var all = FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None); var baked = BeltTopologyBuilder.Bake(all);
            foreach (var issue in baked.Issues) if (issue.Piece == belt) { Handles.color = Color.red; Handles.DrawWireCube(start, Vector3.one * 0.8f); Handles.Label(start + Vector3.up * 0.5f, issue.Message); }
        }
    }
}
