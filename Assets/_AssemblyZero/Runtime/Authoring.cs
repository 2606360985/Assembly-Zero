using System;
using System.Collections.Generic;
using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity
{
    public enum GridDirection : byte { North, East, South, West }

    public readonly struct TopologyIssue
    {
        public readonly BeltAuthoring Piece;
        public readonly string Message;
        public TopologyIssue(BeltAuthoring piece, string message) { Piece = piece; Message = message; }
    }

    public sealed class BakedBeltTopology
    {
        public BeltTopology DomainTopology;
        public BeltAuthoring[] OrderedPieces = Array.Empty<BeltAuthoring>();
        public Vector3[] WorldCenters = Array.Empty<Vector3>();
        public List<TopologyIssue> Issues = new List<TopologyIssue>();
        public int BeltPieceCount => OrderedPieces.Length;
    }

    public static class BeltTopologyBuilder
    {
        public static BakedBeltTopology Bake(IReadOnlyList<BeltAuthoring> pieces)
        {
            var result = new BakedBeltTopology();
            if (pieces == null || pieces.Count == 0) { result.Issues.Add(new TopologyIssue(null, "No belt pieces found.")); result.DomainTopology = new BeltTopology(Array.Empty<LaneDefinition>()); return result; }
            var byGrid = new Dictionary<Vector2Int, BeltAuthoring>();
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                if (byGrid.ContainsKey(pieces[i].Grid)) result.Issues.Add(new TopologyIssue(pieces[i], $"Duplicate belt grid {pieces[i].Grid}.")); else byGrid.Add(pieces[i].Grid, pieces[i]);
            }
            BeltAuthoring start = null;
            foreach (var p in pieces) if (p != null && (p.StartsLine || !HasInput(p, pieces))) { if (start == null || p.StablePieceId < start.StablePieceId) start = p; }
            start ??= pieces[0];
            var ordered = new List<BeltAuthoring>(); var visited = new HashSet<BeltAuthoring>(); var cursor = start;
            while (cursor != null && visited.Add(cursor)) { ordered.Add(cursor); if (cursor.EndsLine) break; if (!byGrid.TryGetValue(cursor.OutputGrid, out var next)) { result.Issues.Add(new TopologyIssue(cursor, $"Output {cursor.OutputGrid} is not connected.")); break; } cursor = next; }
            if (cursor != null && visited.Contains(cursor) && ordered.Count > 1 && cursor == ordered[0]) result.Issues.Add(new TopologyIssue(cursor, "Closed loops are outside EP01 scope."));
            foreach (var p in pieces) if (p != null && !visited.Contains(p)) result.Issues.Add(new TopologyIssue(p, "Piece is disconnected from the primary transport line."));
            result.OrderedPieces = ordered.ToArray(); result.WorldCenters = new Vector3[ordered.Count];
            for (var i = 0; i < ordered.Count; i++) result.WorldCenters[i] = ordered[i].transform.position;
            var length = Math.Max(BeltConstants.UnitsPerGrid, ordered.Count * BeltConstants.UnitsPerGrid);
            var line = new TransportLineDefinition(new LineId(0), length, 0, ordered.Count);
            var lanes = new[] { new LaneDefinition(new LaneId(0), line.Id, length, ItemType.ServoCore) };
            var samples = new PathSample[ordered.Count + 1]; for (var i = 0; i < samples.Length; i++) samples[i] = new PathSample(i * BeltConstants.UnitsPerGrid, Math.Min(i, Math.Max(0, ordered.Count - 1)), i == ordered.Count ? BeltConstants.UnitsPerGrid : 0);
            result.DomainTopology = new BeltTopology(lanes, new[] { line }, new[] { new LineConnection(lanes[0].Id, default, EndpointKind.Sink) }, samples);
            return result;
        }

        private static bool HasInput(BeltAuthoring piece, IReadOnlyList<BeltAuthoring> pieces) { for (var i = 0; i < pieces.Count; i++) if (pieces[i] != null && pieces[i] != piece && pieces[i].OutputGrid == piece.Grid) return true; return false; }

        public static Vector3 SampleWorld(BakedBeltTopology topology, int logicalDistance, int laneId) => SampleWorldPose(topology, logicalDistance, laneId, out _);

        public static Vector3 SampleWorldPose(BakedBeltTopology topology, int logicalDistance, int laneId, out Vector3 tangent)
        {
            tangent = Vector3.right;
            if (topology == null || topology.WorldCenters.Length == 0) return Vector3.zero;
            var centers = topology.WorldCenters; var count = centers.Length;
            var maxLogical = Math.Max(1, topology.DomainTopology.Lanes[laneId].Length);
            var scaled = Mathf.Clamp01(logicalDistance / (float)maxLogical) * Math.Max(0, count - 1);
            var piece = Mathf.Clamp(Mathf.FloorToInt(scaled + 0.5f), 0, count - 1);
            var t = Mathf.Clamp01(scaled + 0.5f - piece);
            var entryVector = PieceEntry(centers, piece); var exitVector = PieceExit(centers, piece, entryVector);
            var entryHalf = entryVector.magnitude * 0.5f; var exitHalf = exitVector.magnitude * 0.5f;
            var entry = entryHalf > 0.0001f ? entryVector.normalized : Vector3.right; var exit = exitHalf > 0.0001f ? exitVector.normalized : entry;
            Vector3 center;
            if (Mathf.Abs(Vector3.Dot(entry, exit)) < 0.5f)
            {
                var pivot = centers[piece] - entry * entryHalf + exit * entryHalf;
                var angle = t * Mathf.PI * 0.5f; var cos = Mathf.Cos(angle); var sin = Mathf.Sin(angle);
                center = pivot + (entry * sin - exit * cos) * entryHalf;
                tangent = (entry * cos + exit * sin).normalized;
            }
            else
            {
                center = centers[piece] + entry * Mathf.Lerp(-entryHalf, exitHalf, t);
                tangent = entry;
            }
            return center + Vector3.up * 0.24f;
        }

        private static Vector3 PieceEntry(Vector3[] centers, int piece)
        {
            Vector3 v;
            if (piece > 0) v = centers[piece] - centers[piece - 1]; else if (centers.Length > 1) v = centers[1] - centers[0]; else v = Vector3.right;
            v.y = 0f; return v;
        }

        private static Vector3 PieceExit(Vector3[] centers, int piece, Vector3 entry)
        {
            if (piece >= centers.Length - 1) return entry;
            var v = centers[piece + 1] - centers[piece]; v.y = 0f; return v;
        }
    }

}
