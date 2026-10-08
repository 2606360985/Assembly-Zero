using System;
using System.Linq;
namespace AssemblyZero.Domain
{
    public readonly struct InitialAssemblyPart
    {
        public readonly PartTypeId Part; public readonly int Lane, Distance;
        public InitialAssemblyPart(int part, int lane, int distance) { Part = new PartTypeId(part); Lane = lane; Distance = distance; }
    }
    public sealed class AssemblyScenarioDefinition
    {
        public int Seed = 2006, BeltSpeed = 32, MinimumSpacing = 768, SourceInterval = 24, RejectInterval = 60;
        public int ScanTicks = 90, ShowcaseTicks = 180, MaxGenerated;
        public bool SourceEnabled = true, FullRobotManifest = true;
        public int[] SupplySpeeds = { 32, 24 };
        public int ProductSpeed = 16, ProductInterval = 240, ProductSpacing = 2048, ProductLength;
        public int[] SegmentEnds = Array.Empty<int>();
        public AssemblyStationDefinition[] Stations = Array.Empty<AssemblyStationDefinition>();
        public PartTypeId[] SourceParts = { new PartTypeId(2), new PartTypeId(3), new PartTypeId(4), new PartTypeId(5), new PartTypeId(6), new PartTypeId(7) };
        public InserterData[] Inserters = Array.Empty<InserterData>();
        public AssemblySocketData[] Sockets = Array.Empty<AssemblySocketData>();
        public InitialAssemblyPart[] InitialParts = Array.Empty<InitialAssemblyPart>();
        public AssemblyManifest Manifest = new AssemblyManifest();
        public AssemblyScenarioDefinition Clone()
        {
            var c = (AssemblyScenarioDefinition)MemberwiseClone(); c.SupplySpeeds = (int[])SupplySpeeds.Clone(); c.SegmentEnds = (int[])SegmentEnds.Clone();
            c.Stations = Stations.Select(s => s.Clone()).ToArray(); c.SourceParts = (PartTypeId[])SourceParts.Clone(); c.InitialParts = (InitialAssemblyPart[])InitialParts.Clone();
            c.Inserters = Inserters.Select(a => a.Clone()).ToArray(); c.Sockets = Sockets.Select(s => s.Clone()).ToArray(); c.Manifest = new AssemblyManifest { RequiredParts = (PartTypeId[])Manifest.RequiredParts.Clone() }; return c;
        }
        public void Validate(BeltTopology topology)
        {
            if (topology == null || topology.Lanes.Length == 0 || SupplySpeeds.Length != topology.Lanes.Length || SupplySpeeds.Any(s => s < 0)) throw new ArgumentException("Supply lines require a nonnegative speed for each lane.");
            if (MinimumSpacing < 1 || SourceInterval < 1 || RejectInterval < 1 || ScanTicks < 1 || ShowcaseTicks < 1 || ProductSpeed < 1 || ProductInterval < 1 || ProductSpacing < 1 || ProductLength < ProductSpacing) throw new ArgumentException("Invalid production timing or spacing.");
            if (SegmentEnds.Length == 0 || SegmentEnds.Last() != ProductLength || SegmentEnds[0] < ProductSpacing) throw new ArgumentException("Product segments must cover the route.");
            for (var i = 1; i < SegmentEnds.Length; i++) if (SegmentEnds[i] - SegmentEnds[i - 1] < ProductSpacing) throw new ArgumentException("Product segment is smaller than the pallet clearance.");
            if (Sockets.Any(s => s == null) || Sockets.Select(s => s.Id.Value).Distinct().Count() != Sockets.Length || Sockets.Select(s => s.AcceptedPart.Value).Distinct().Count() != Sockets.Length) throw new ArgumentException("Duplicate or missing socket.");
            if (Manifest == null || !Manifest.RequiredParts.Select(p => p.Value).OrderBy(x => x).SequenceEqual(Sockets.Select(s => s.AcceptedPart.Value).OrderBy(x => x))) throw new ArgumentException("Manifest and sockets disagree.");
            if (FullRobotManifest && !Manifest.RequiredParts.Select(p => p.Value).OrderBy(x => x).SequenceEqual(new[] { 2, 3, 4, 5, 6, 7 })) throw new ArgumentException("A robot requires all six unique parts.");
            if (Stations.Length != Sockets.Length || Stations.Select(s => s.Id).Distinct().Count() != Stations.Length) throw new ArgumentException("Every socket requires a unique station.");
            for (var i = 0; i < Stations.Length; i++)
            {
                var s = Stations[i]; if (s.Distance < 0 || s.Distance >= ProductLength || (i > 0 && s.Distance <= Stations[i - 1].Distance) || !Sockets.Any(t => t.Id.Value == s.SocketId && t.AcceptedPart.Value == s.PartType)) throw new ArgumentException("Invalid station order or socket mapping.");
                if (SegmentEnds.TakeWhile(e => e <= s.Distance).Count() != SegmentEnds.TakeWhile(e => e <= Math.Min(ProductLength - 1, s.Distance + ProductSpacing / 2)).Count() || (s.Distance - (SegmentEnds.LastOrDefault(e => e <= s.Distance))) < ProductSpacing / 2) throw new ArgumentException("Station must leave pallet clearance at segment boundaries.");
            }
            if (Inserters.Select(a => a.Id.Value).Distinct().Count() != Inserters.Length) throw new ArgumentException("Duplicate arm ID.");
            foreach (var a in Inserters)
            {
                if (!Stations.Any(s => s.Id == a.StationId && s.SocketId == a.TargetSocket.Value && s.PartType == a.AllowedPart.Value) || a.PickingTicks < 1 || a.CarryingTicks < 1 || a.PlacingTicks < 1 || a.ReturningTicks < 1) throw new ArgumentException("Invalid arm timing or station.");
                foreach (var p in a.Ports.Length == 0 ? new[] { a.Port } : a.Ports)
                {
                    var lanes = topology.Lanes.Where(l => l.LineId.Value == p.TransportLineId.Value && (p.LaneMask & (1UL << l.Id.Value)) != 0).ToArray();
                    if (lanes.Length == 0 || p.StartDistance < 0 || p.EndDistance <= p.StartDistance || p.PreferredDistance < p.StartDistance || p.PreferredDistance > p.EndDistance || (p.AcceptedPartTypeMask & a.AllowedPart.Mask) == 0) throw new ArgumentException("Invalid pickup port.");
                    foreach (var l in lanes) if (p.EndDistance > l.Length || (long)SupplySpeeds[Array.IndexOf(topology.Lanes, l)] * (a.PickingTicks + 1) > p.EndDistance - p.PreferredDistance) throw new ArgumentException("Pickup port cannot retain the part for the picking duration.");
                }
            }
            if (SourceEnabled && SourceParts.Length == 0 || SourceParts.Any(p => !Sockets.Any(s => s.AcceptedPart.Value == p.Value))) throw new ArgumentException("Source contains unknown parts.");
        }
    }
}
