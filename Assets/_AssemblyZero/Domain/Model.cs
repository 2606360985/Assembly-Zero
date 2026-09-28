using System;
using System.Collections.Generic;

namespace AssemblyZero.Domain
{
    public static class BeltConstants { public const int UnitsPerGrid = 1024; public const int TickRate = 60; }

    public enum ItemType : byte { ServoCore, SensorPack }
    public enum BeltPieceShape : byte { Straight, Corner90 }
    public enum EndpointKind : byte { Source, Gate, Sink, Line }
    public enum BackendKind : byte { Naive, Gap }

    public readonly struct ItemId : IEquatable<ItemId> { public readonly int Value; public ItemId(int value) => Value = value; public bool Equals(ItemId other) => Value == other.Value; public override bool Equals(object obj) => obj is ItemId other && Equals(other); public override int GetHashCode() => Value; public override string ToString() => Value.ToString(); }
    public readonly struct LineId : IEquatable<LineId> { public readonly int Value; public LineId(int value) => Value = value; public bool Equals(LineId other) => Value == other.Value; public override bool Equals(object obj) => obj is LineId other && Equals(other); public override int GetHashCode() => Value; }
    public readonly struct LaneId : IEquatable<LaneId> { public readonly int Value; public LaneId(int value) => Value = value; public bool Equals(LaneId other) => Value == other.Value; public override bool Equals(object obj) => obj is LaneId other && Equals(other); public override int GetHashCode() => Value; public override string ToString() => Value.ToString(); }
    public readonly struct NodeId { public readonly int Value; public NodeId(int value) => Value = value; }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public int TickRate = BeltConstants.TickRate;
        public int SpeedUnitsPerTick = 8;
        public int MinimumSpacing = 16;
        public int SpawnIntervalTicks = 3;
        public int MaxItems = 200;
        public int InitialItems;
        public bool AlternateItemTypes;
        public int InitialFrontDistance = -1;
        public int OrderTarget = 100;
        public int GateCloseTick = -1;
        public int GateOpenTick = -1;
        public int Seed = 101;
        public int VisibleItems = 20000;

        public ScenarioDefinition Clone() => (ScenarioDefinition)MemberwiseClone();
    }

    public readonly struct LaneDefinition
    {
        public readonly LaneId Id;
        public readonly LineId LineId;
        public readonly int Length;
        public readonly ItemType ItemType;
        public LaneDefinition(LaneId id, LineId lineId, int length, ItemType itemType) { Id = id; LineId = lineId; Length = length; ItemType = itemType; }
    }

    public readonly struct TransportLineDefinition
    {
        public readonly LineId Id;
        public readonly int Length;
        public readonly int FirstPieceIndex;
        public readonly int PieceCount;
        public TransportLineDefinition(LineId id, int length, int firstPieceIndex, int pieceCount) { Id = id; Length = length; FirstPieceIndex = firstPieceIndex; PieceCount = pieceCount; }
    }

    public readonly struct LineConnection
    {
        public readonly LaneId From;
        public readonly LaneId To;
        public readonly EndpointKind TargetKind;
        public LineConnection(LaneId from, LaneId to, EndpointKind targetKind) { From = from; To = to; TargetKind = targetKind; }
    }

    public readonly struct PathSample
    {
        public readonly int Distance;
        public readonly int PieceIndex;
        public readonly int SegmentUnits;
        public PathSample(int distance, int pieceIndex, int segmentUnits) { Distance = distance; PieceIndex = pieceIndex; SegmentUnits = segmentUnits; }
    }

    public sealed class BeltTopology
    {
        public readonly LaneDefinition[] Lanes;
        public readonly TransportLineDefinition[] Lines;
        public readonly LineConnection[] Connections;
        public readonly PathSample[] PathSamples;
        public BeltTopology(LaneDefinition[] lanes, TransportLineDefinition[] lines = null, LineConnection[] connections = null, PathSample[] pathSamples = null)
        {
            Lanes = lanes ?? Array.Empty<LaneDefinition>();
            Lines = lines ?? Array.Empty<TransportLineDefinition>();
            Connections = connections ?? Array.Empty<LineConnection>();
            PathSamples = pathSamples ?? Array.Empty<PathSample>();
        }

        public static BeltTopology CreateStraight(int laneCount, int length)
        {
            var lanes = new LaneDefinition[laneCount];
            for (var i = 0; i < laneCount; i++) lanes[i] = new LaneDefinition(new LaneId(i), new LineId(0), length, (i & 1) == 0 ? ItemType.ServoCore : ItemType.SensorPack);
            return new BeltTopology(lanes, new[] { new TransportLineDefinition(new LineId(0), length, 0, 1) });
        }
    }

    public readonly struct TickInput
    {
        public readonly long Tick;
        public readonly bool? GateOpenOverride;
        public TickInput(long tick, bool? gateOpenOverride = null) { Tick = tick; GateOpenOverride = gateOpenOverride; }
    }

    public readonly struct BeltItemSnapshot
    {
        public readonly ItemId ItemId;
        public readonly ItemType ItemType;
        public readonly LineId LineId;
        public readonly LaneId LaneId;
        public readonly int Distance;
        public readonly int GapAhead;
        public BeltItemSnapshot(ItemId itemId, ItemType itemType, LineId lineId, LaneId laneId, int distance, int gapAhead) { ItemId = itemId; ItemType = itemType; LineId = lineId; LaneId = laneId; Distance = distance; GapAhead = gapAhead; }
    }

    public readonly struct SimulationStatistics
    {
        public readonly long Tick;
        public readonly int LogicalItems;
        public readonly int ValuesTouched;
        public readonly int ActiveLines;
        public readonly int SourceCount;
        public readonly int SinkCount;
        public readonly bool GateOpen;
        public readonly int OrderProgress;
        public readonly ulong StateHash;
        public SimulationStatistics(long tick, int logicalItems, int valuesTouched, int activeLines, int sourceCount, int sinkCount, bool gateOpen, int orderProgress, ulong stateHash)
        { Tick = tick; LogicalItems = logicalItems; ValuesTouched = valuesTouched; ActiveLines = activeLines; SourceCount = sourceCount; SinkCount = sinkCount; GateOpen = gateOpen; OrderProgress = orderProgress; StateHash = stateHash; }
    }

    public sealed class BeltSnapshotWriter
    {
        private readonly List<BeltItemSnapshot> items = new List<BeltItemSnapshot>(2048);
        public IReadOnlyList<BeltItemSnapshot> Items => items;
        public SimulationStatistics Statistics { get; private set; }
        public void Clear() => items.Clear();
        public void Add(in BeltItemSnapshot item) => items.Add(item);
        public void SetStatistics(in SimulationStatistics statistics) => Statistics = statistics;
    }

    public interface IBeltSimulationBackend
    {
        string Name { get; }
        SimulationStatistics Statistics { get; }
        void Reset(ScenarioDefinition scenario, BeltTopology topology);
        void Tick(in TickInput input);
        void CreateSnapshot(BeltSnapshotWriter writer);
        ulong CalculateStateHash();
    }

    public readonly struct BeltAccessPort { public readonly LaneId LaneId; public readonly int StartDistance; public readonly int EndDistance; public BeltAccessPort(LaneId laneId, int startDistance, int endDistance) { LaneId = laneId; StartDistance = startDistance; EndDistance = endDistance; } }
    public readonly struct BeltItemHandle { public readonly ItemId ItemId; public readonly LaneId LaneId; public BeltItemHandle(ItemId itemId, LaneId laneId) { ItemId = itemId; LaneId = laneId; } public bool IsValid => ItemId.Value > 0; }
    public interface IBeltAccess { bool TryQuery(in BeltAccessPort port, out BeltItemHandle handle); bool TryRemove(in BeltItemHandle handle); bool TryInsert(in BeltAccessPort port, ItemType type, out BeltItemHandle handle); }

    public static class StateHasher
    {
        public const ulong Offset = 14695981039346656037UL;
        public static ulong Add(ulong hash, long value)
        {
            unchecked { for (var i = 0; i < 8; i++) { hash ^= (byte)(value >> (i * 8)); hash *= 1099511628211UL; } return hash; }
        }
    }
}
