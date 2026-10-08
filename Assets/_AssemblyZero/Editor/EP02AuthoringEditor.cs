using UnityEditor;
using UnityEngine;
namespace AssemblyZero.Unity.Editor
{
    public static class EP02AuthoringEditor
    {
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawLayout(EP02LayoutAuthoring layout, GizmoType type)
        {
            foreach (var arm in layout.Inserters)
            {
                if (arm == null || arm.Joints.Length != 6) continue;
                Gizmos.color = Color.white; Gizmos.DrawWireSphere(arm.Joints[1].position, arm.Reach);
                Gizmos.color = Color.blue; Gizmos.DrawSphere(arm.Grip.position,.08f); Handles.Label(arm.transform.position + Vector3.up, "I" + arm.StableId + " / " + layout.Part(arm.PartTypeId).PartName);
            }
        }
    }
}
