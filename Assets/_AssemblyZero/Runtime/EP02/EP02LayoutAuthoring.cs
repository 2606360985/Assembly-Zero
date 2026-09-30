using System;
using System.Collections.Generic;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02LayoutAuthoring : MonoBehaviour
    {
        public BeltAuthoring[] Belts = Array.Empty<BeltAuthoring>();
        public InserterAuthoring[] Inserters = Array.Empty<InserterAuthoring>();
        public AssemblySocketAuthoring[] Sockets = Array.Empty<AssemblySocketAuthoring>();
        public RobotPartDefinition[] Parts = Array.Empty<RobotPartDefinition>();
        public float LaneSeparation = 0.52f;
        public Transform RejectPoint;
        public BakedBeltTopology BakeAndValidate()
        {
            if (Belts.Length != 27 || Inserters.Length != 6 || Sockets.Length != 6 || Parts.Length != 6 || RejectPoint == null) throw new InvalidOperationException("EP02 requires 27 belts, six explicit arms/sockets/parts and a reject point.");
            var baked = BeltTopologyBuilder.Bake(Belts, 2, LaneSeparation);
            if (baked.Issues.Count != 0) throw new InvalidOperationException("EP02 belt topology: " + baked.Issues[0].Message);
            var ids = new HashSet<int>(); var sockets = new Dictionary<int, AssemblySocketAuthoring>();
            foreach (var socket in Sockets) { if (socket == null || !ids.Add(socket.StableId)) throw new InvalidOperationException("Missing or duplicate socket."); sockets.Add(socket.StableId, socket); }
            ids.Clear();
            foreach (var part in Parts) { if (part == null || part.SourcePrefab == null || part.WrapperPrefab == null || !ids.Add(part.PartTypeId) || !sockets.TryGetValue(part.SocketId, out var socket) || socket.PartTypeId != part.PartTypeId || part.OccupancyUnits < 1) throw new InvalidOperationException("Missing/duplicate part, prefab or socket mapping."); }
            if (!ids.SetEquals(new[] { 2, 3, 4, 5, 6, 7 })) throw new InvalidOperationException("Six part IDs must be 2..7.");
            ids.Clear();
            foreach (var arm in Inserters)
            {
                if (arm == null || arm.Grip == null || arm.Shoulder == null || arm.Elbow == null || arm.Links == null || arm.Links.Length != 2 || !ids.Add(arm.StableId) || !sockets.TryGetValue(arm.SocketId, out var target) || target.PartTypeId != arm.PartTypeId || arm.PortWidth < 1 || arm.PreferredDistance < arm.PortWidth / 2 || arm.PreferredDistance + arm.PortWidth / 2 > baked.DomainTopology.Lanes[0].Length) throw new InvalidOperationException("Invalid arm identity, joints, socket or pickup port.");
                var pickup = BeltTopologyBuilder.SampleWorld(baked, arm.PreferredDistance, 0);
                if (Vector3.Distance(arm.transform.position, pickup) > arm.Reach || Vector3.Distance(arm.transform.position, target.transform.position) > arm.Reach) throw new InvalidOperationException("Arm " + arm.StableId + " cannot reach its pickup/socket.");
            }
            return baked;
        }
        public InserterAuthoring Arm(int id) { foreach (var a in Inserters) if (a.StableId == id) return a; throw new ArgumentException("Unknown arm"); }
        public AssemblySocketAuthoring Socket(int id) { foreach (var s in Sockets) if (s.StableId == id) return s; throw new ArgumentException("Unknown socket"); }
        public RobotPartDefinition Part(int id) { foreach (var p in Parts) if (p.PartTypeId == id) return p; throw new ArgumentException("Unknown part"); }
    }
}
