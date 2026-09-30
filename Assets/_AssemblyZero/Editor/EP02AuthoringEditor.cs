using UnityEditor;
using UnityEngine;
namespace AssemblyZero.Unity.Editor
{
    public static class EP02AuthoringEditor
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLayout(EP02LayoutAuthoring layout, GizmoType type)
        {
            var topology = BeltTopologyBuilder.Bake(layout.Belts, 2, layout.LaneSeparation);
            foreach (var arm in layout.Inserters)
            {
                if (arm == null) continue; var pickup = BeltTopologyBuilder.SampleWorld(topology, arm.PreferredDistance, 0);
                Gizmos.color = Color.green; Gizmos.DrawWireSphere(pickup, .45f); Gizmos.DrawLine(BeltTopologyBuilder.SampleWorld(topology, arm.PreferredDistance - arm.PortWidth / 2, 0), BeltTopologyBuilder.SampleWorld(topology, arm.PreferredDistance + arm.PortWidth / 2, 0));
                Gizmos.color = Color.white; Gizmos.DrawWireSphere(arm.transform.position, arm.Reach);
                foreach (var socket in layout.Sockets) if (socket != null && socket.StableId == arm.SocketId) { Gizmos.color = Color.blue; Gizmos.DrawWireCube(socket.transform.position, Vector3.one * .3f); Gizmos.DrawLine(arm.transform.position, socket.transform.position); }
                Handles.Label(arm.transform.position + Vector3.up, "I" + arm.StableId + " / Part " + arm.PartTypeId);
            }
            if (Application.isPlaying)
            {
                var driver = layout.GetComponent<EP02SimulationDriver>(); if (driver != null && driver.Simulation != null)
                {
                    foreach (var socket in driver.Simulation.Snapshot.Sockets) { Gizmos.color = socket.Locked ? Color.red : socket.ReservedBy >= 0 ? Color.yellow : Color.blue; Gizmos.DrawWireSphere(layout.Socket(socket.Id).transform.position, .4f); }
                    foreach (var item in driver.Simulation.Snapshot.Items) if (item.Ownership == AssemblyZero.Domain.ItemOwnership.ReservedOnBelt) { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(BeltTopologyBuilder.SampleWorld(topology, item.Distance, item.Lane), Vector3.one * .55f); }
                }
            }
        }
    }
}
