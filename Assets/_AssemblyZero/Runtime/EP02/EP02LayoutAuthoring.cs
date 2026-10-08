using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02Route
    {
        public const float UnitsPerMeter = 1024f / 1.2f;
        public readonly BeltAuthoring[] Pieces;
        public readonly int[] Ends;
        public readonly int Length;
        private readonly Vector3[] entries, exits;
        private readonly float[] radii;
        public EP02Route(BeltAuthoring[] pieces)
        {
            if (pieces == null || pieces.Length < 2) throw new InvalidOperationException("A route requires at least two pieces.");
            Pieces = pieces; Ends = new int[pieces.Length]; entries = new Vector3[pieces.Length]; exits = new Vector3[pieces.Length]; radii = new float[pieces.Length];
            for (var i = 0; i < pieces.Length; i++)
            {
                var c = pieces[i].transform.position;
                var incoming = i > 0 ? c - pieces[i - 1].transform.position : pieces[1].transform.position - c;
                var outgoing = i < pieces.Length - 1 ? pieces[i + 1].transform.position - c : incoming;
                entries[i] = incoming.normalized; exits[i] = outgoing.normalized;
                radii[i] = incoming.magnitude * .5f;
                var corner = Vector3.Dot(entries[i], exits[i]) < .5f;
                if (corner && Mathf.Abs(incoming.magnitude - outgoing.magnitude) > .01f) throw new InvalidOperationException("Corner needs equal tangent lengths.");
                var meters = corner ? radii[i] * Mathf.PI * .5f : (incoming.magnitude + outgoing.magnitude) * .5f;
                Ends[i] = (i == 0 ? 0 : Ends[i - 1]) + Mathf.RoundToInt(meters * UnitsPerMeter);
            }
            Length = Ends.Last();
        }
        public int PieceAt(int distance) { for (var i = 0; i < Ends.Length; i++) if (distance < Ends[i]) return i; return Ends.Length - 1; }
        public int CenterDistance(int piece) => ((piece == 0 ? 0 : Ends[piece - 1]) + Ends[piece]) / 2;
        public Vector3 Sample(int distance, out Vector3 tangent)
        {
            var i = PieceAt(distance); var start = i == 0 ? 0 : Ends[i - 1]; var t = Mathf.Clamp01((distance - start) / (float)(Ends[i] - start));
            var c = Pieces[i].transform.position; var entry = entries[i]; var exit = exits[i]; Vector3 p;
            if (Vector3.Dot(entry, exit) < .5f)
            {
                var pivot = c - entry * radii[i] + exit * radii[i]; var a = t * Mathf.PI * .5f;
                p = pivot + (entry * Mathf.Sin(a) - exit * Mathf.Cos(a)) * radii[i]; tangent = (entry * Mathf.Cos(a) + exit * Mathf.Sin(a)).normalized;
            }
            else { var exitHalf = i < Pieces.Length - 1 ? Vector3.Distance(c, Pieces[i + 1].transform.position) * .5f : radii[i]; p = c + entry * Mathf.Lerp(-radii[i], exitHalf, t); tangent = entry; }
            // Pallet underside (.01 m local) rests on the .19 m belt surface.
            return p + Vector3.up * .18f;
        }
        public int Nearest(Vector3 position)
        {
            var best = 0; var error = float.MaxValue;
            for (var d = 0; d <= Length; d += 16) { var e = (Sample(d, out _) - position).sqrMagnitude; if (e < error) { error = e; best = d; } }
            return best;
        }
    }
    public sealed class EP02BakedLayout
    {
        public EP02Route[] Supply; public EP02Route Product; public BeltTopology SupplyTopology;
    }
    [Serializable] public sealed class EP02FlowSurface
    {
        public Renderer Renderer; public int Line, Segment; public bool Product;
        public float StartMeters, LengthMeters;
    }
    public sealed class EP02LayoutAuthoring : MonoBehaviour
    {
        public BeltAuthoring[] SupplyA = Array.Empty<BeltAuthoring>(), SupplyB = Array.Empty<BeltAuthoring>(), ProductBelts = Array.Empty<BeltAuthoring>();
        public InserterAuthoring[] Inserters = Array.Empty<InserterAuthoring>();
        public AssemblySocketAuthoring[] Sockets = Array.Empty<AssemblySocketAuthoring>();
        public RobotPartDefinition[] Parts = Array.Empty<RobotPartDefinition>();
        public EP02FlowSurface[] Surfaces = Array.Empty<EP02FlowSurface>();
        public Transform RejectPoint, DisplayRoot;
        public GameObject ProductCarrierPrefab;
        public float ProductWidth, ProductSpacing;
        public EP02BakedLayout BakeAndValidate()
        {
            if (Inserters.Length != 6 || Sockets.Length != 6 || Parts.Length != 6 || RejectPoint == null || ProductCarrierPrefab == null || DisplayRoot == null) throw new InvalidOperationException("Missing EP02 production references.");
            if (Parts.Any(p => p == null || p.SourcePrefab == null || p.WrapperPrefab == null) || !Parts.Select(p => p.PartTypeId).OrderBy(x => x).SequenceEqual(new[] { 2, 3, 4, 5, 6, 7 }) || Sockets.Select(s => s.StableId).Distinct().Count() != 6) throw new InvalidOperationException("Invalid part/socket identities.");
            var b = new EP02BakedLayout { Supply = new[] { new EP02Route(SupplyA), new EP02Route(SupplyB) }, Product = new EP02Route(ProductBelts) };
            var lanes = new LaneDefinition[2]; var lines = new TransportLineDefinition[2];
            for (var i = 0; i < 2; i++) { lanes[i] = new LaneDefinition(new LaneId(i), new LineId(i), b.Supply[i].Length, ItemType.ServoCore); lines[i] = new TransportLineDefinition(new LineId(i), b.Supply[i].Length, 0, b.Supply[i].Pieces.Length); }
            b.SupplyTopology = new BeltTopology(lanes, lines);
            foreach (var a in Inserters)
            {
                if (a.Joints.Length != 6 || a.Joints.Any(j => j == null) || a.Grip == null || a.PartTypeId != PartBySocket(a.SocketId).PartTypeId || a.StationPiece < 0 || a.StationPiece >= ProductBelts.Length || ProductBelts[a.StationPiece].Shape == BeltPieceShape.Corner90) throw new InvalidOperationException("Invalid six-axis arm or station.");
                var product = b.Product.Sample(b.Product.CenterDistance(a.StationPiece), out var tangent);
                var target = product + Quaternion.LookRotation(tangent) * SocketOffset(a.SocketId);
                var poses = new List<Vector3> { target, target + Vector3.up * .32f, a.RestPosition };
                for (var l = 0; l < 2; l++) for (var d = -a.PortWidth / 2; d <= a.PortWidth / 2; d += 128)
                {
                    var pickup = b.Supply[l].Sample(a.PickupDistances[l] + d, out _) + Vector3.up * .26f;
                    for (var tick = 0; tick <= 10; tick++) foreach (var state in new[] { InserterState.Picking, InserterState.Carrying, InserterState.Placing, InserterState.Returning }) poses.Add(InserterPresenter.Motion(state, tick / 10f, a.RestPosition, pickup, target));
                }
                foreach (var p in poses) if (!a.CanReach(p, InserterPresenter.ToolRotation)) throw new InvalidOperationException("I-" + a.StableId + " pose is outside the fixed arm workspace: " + p);
                a.ResetPose();
            }
            return b;
        }
        public Vector3 SocketOffset(int id) => PartBySocket(id).AssemblySocketPosition - Vector3.up * .61f;
        private RobotPartDefinition PartBySocket(int id) => Parts.Single(p => p.SocketId == id);
        public InserterAuthoring Arm(int id) => Inserters.Single(a => a.StableId == id);
        public AssemblySocketAuthoring Socket(int id) => Sockets.Single(s => s.StableId == id);
        public RobotPartDefinition Part(int id) => Parts.Single(p => p.PartTypeId == id);
    }
}
