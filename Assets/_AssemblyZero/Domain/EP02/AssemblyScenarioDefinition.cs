using System;

namespace AssemblyZero.Domain
{
    public readonly struct InitialAssemblyPart
    {
        public readonly PartTypeId Part;
        public readonly int Lane, Distance;
        public InitialAssemblyPart(int part, int lane, int distance) { Part = new PartTypeId(part); Lane = lane; Distance = distance; }
    }
    public sealed class AssemblyScenarioDefinition
    {
        public int Seed = 2003, BeltSpeed = 32, MinimumSpacing = 768, SourceInterval = 24, RejectInterval = 60;
        public int ScanTicks = 90, ShowcaseTicks = 180, MaxGenerated;
        public bool SourceEnabled = true, FullRobotManifest = true;
        public PartTypeId[] SourceParts = { new PartTypeId(2), new PartTypeId(3), new PartTypeId(4), new PartTypeId(5), new PartTypeId(6), new PartTypeId(7) };
        public InserterData[] Inserters = Array.Empty<InserterData>();
        public AssemblySocketData[] Sockets = Array.Empty<AssemblySocketData>();
        public InitialAssemblyPart[] InitialParts = Array.Empty<InitialAssemblyPart>();
        public AssemblyManifest Manifest = new AssemblyManifest { RequiredParts = new[] { new PartTypeId(2), new PartTypeId(3), new PartTypeId(4), new PartTypeId(5), new PartTypeId(6), new PartTypeId(7) } };

        public void Validate(BeltTopology topology)
        {
            if (topology == null || topology.Lanes.Length == 0 || topology.Lanes.Length > 64) throw new ArgumentException("EP02 requires 1..64 belt lanes.");
            if (BeltSpeed < 0 || MinimumSpacing < 1 || SourceInterval < 1 || RejectInterval < 1 || ScanTicks < 1 || ShowcaseTicks < 1) throw new ArgumentException("EP02 timings and spacing must be positive (belt speed may be zero).");
            var lanes = new System.Collections.Generic.HashSet<int>();
            foreach (var lane in topology.Lanes) if (lane.Id.Value < 0 || lane.Id.Value >= 64 || !lanes.Add(lane.Id.Value) || lane.Length <= MinimumSpacing) throw new ArgumentException("Invalid/duplicate lane ID or insufficient belt length.");
            var socketIds = new System.Collections.Generic.HashSet<int>();
            var types = new System.Collections.Generic.HashSet<int>();
            foreach (var socket in Sockets) if (socket == null || socket.Id.Value < 0 || !socketIds.Add(socket.Id.Value) || socket.AcceptedPart.Mask == 0 || !types.Add(socket.AcceptedPart.Value)) throw new ArgumentException("Sockets require unique IDs and accepted part types.");
            if (Manifest == null || Manifest.RequiredParts.Length != Sockets.Length) throw new ArgumentException("Manifest must describe every socket.");
            var required = new System.Collections.Generic.HashSet<int>();
            foreach (var part in Manifest.RequiredParts) if (!required.Add(part.Value) || !types.Contains(part.Value)) throw new ArgumentException("Manifest types must be unique and match sockets.");
            if (FullRobotManifest && (required.Count != 6 || !required.SetEquals(new[] { 2, 3, 4, 5, 6, 7 }))) throw new ArgumentException("A complete robot requires the six unique body parts.");
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (var arm in Inserters)
            {
                if (arm == null || arm.Id.Value < 0 || !ids.Add(arm.Id.Value) || arm.PickingTicks < 1 || arm.CarryingTicks < 1 || arm.PlacingTicks < 1 || arm.ReturningTicks < 1 || arm.PhaseTicks < 0) throw new ArgumentException("Invalid inserter ID or timing.");
                AssemblySocketData target = null; foreach (var socket in Sockets) if (socket.Id.Value == arm.TargetSocket.Value) target = socket;
                if (target == null || target.AcceptedPart.Value != arm.AllowedPart.Value) throw new ArgumentException("Inserter part must match its target socket.");
                var port = arm.Port; ulong validMask = 0; var maxLength = 0;
                foreach (var lane in topology.Lanes) if (lane.LineId.Value == port.TransportLineId.Value) { validMask |= 1UL << lane.Id.Value; maxLength = Math.Max(maxLength, lane.Length); }
                if (port.LaneMask == 0 || (port.LaneMask & ~validMask) != 0 || port.StartDistance < 0 || port.EndDistance > maxLength || port.EndDistance <= port.StartDistance || port.PreferredDistance < port.StartDistance || port.PreferredDistance > port.EndDistance || (port.AcceptedPartTypeMask & arm.AllowedPart.Mask) == 0) throw new ArgumentException("Invalid pickup port.");
                if ((long)BeltSpeed * (arm.PickingTicks + 1) > port.EndDistance - port.StartDistance) throw new ArgumentException("Pickup port is too short for the configured picking duration.");
            }
            if (SourceEnabled && SourceParts.Length == 0) throw new ArgumentException("Source requires configured part types.");
            foreach (var part in SourceParts) if (part.Mask == 0 || !types.Contains(part.Value)) throw new ArgumentException("Source contains an unknown part type.");
        }
    }
}
