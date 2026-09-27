using System;
using System.Collections.Generic;

namespace AssemblyZero.Domain
{
    internal struct SimItem
    {
        public int Id;
        public ItemType Type;
        public int Distance;
        public SimItem(int id, ItemType type, int distance) { Id = id; Type = type; Distance = distance; }
    }

    internal sealed class RingBuffer<T>
    {
        private T[] data;
        private int head;
        public int Count { get; private set; }
        public RingBuffer(int capacity = 16) => data = new T[Math.Max(4, capacity)];
        public T this[int index] { get => data[(head + index) % data.Length]; set => data[(head + index) % data.Length] = value; }
        public void Clear() { Array.Clear(data, 0, data.Length); head = 0; Count = 0; }
        public void AddLast(T value) { Ensure(); data[(head + Count) % data.Length] = value; Count++; }
        public T RemoveFirst() { if (Count == 0) throw new InvalidOperationException(); var value = data[head]; data[head] = default; head = (head + 1) % data.Length; Count--; return value; }
        public T RemoveAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index)); var value = this[index];
            if (index < Count / 2) { for (var i = index; i > 0; i--) this[i] = this[i - 1]; data[head] = default; head = (head + 1) % data.Length; }
            else { for (var i = index; i < Count - 1; i++) this[i] = this[i + 1]; data[(head + Count - 1) % data.Length] = default; }
            Count--; return value;
        }
        public void InsertAt(int index, T value)
        {
            if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index)); Ensure();
            if (index == Count) { AddLast(value); return; }
            for (var i = Count; i > index; i--) this[i] = this[i - 1]; this[index] = value; Count++;
        }
        private void Ensure() { if (Count < data.Length) return; var next = new T[data.Length * 2]; for (var i = 0; i < Count; i++) next[i] = this[i]; data = next; head = 0; }
    }

    public abstract class BeltSimulationBase : IBeltSimulationBackend, IBeltAccess
    {
        protected ScenarioDefinition Scenario;
        protected BeltTopology Topology;
        protected long TickNumber;
        protected int NextItemId;
        protected int Spawned;
        protected int SinkCount;
        protected int ValuesTouched;
        protected bool GateOpen;
        protected ulong Hash;
        public abstract string Name { get; }
        public SimulationStatistics Statistics { get; protected set; }
        public abstract void Reset(ScenarioDefinition scenario, BeltTopology topology);
        public abstract void Tick(in TickInput input);
        public abstract void CreateSnapshot(BeltSnapshotWriter writer);
        public abstract ulong CalculateStateHash();
        public abstract bool TryQuery(in BeltAccessPort port, out BeltItemHandle handle);
        public abstract bool TryRemove(in BeltItemHandle handle);
        public abstract bool TryInsert(in BeltAccessPort port, ItemType type, out BeltItemHandle handle);

        protected void ApplyGate(in TickInput input)
        {
            if (input.GateOpenOverride.HasValue) GateOpen = input.GateOpenOverride.Value;
            else
            {
                if (Scenario.GateCloseTick >= 0 && input.Tick == Scenario.GateCloseTick) GateOpen = false;
                if (Scenario.GateOpenTick >= 0 && input.Tick == Scenario.GateOpenTick) GateOpen = true;
            }
        }

        protected void FinishTick(int logicalItems, int activeLines)
        {
            Statistics = new SimulationStatistics(TickNumber, logicalItems, ValuesTouched, activeLines, Spawned, SinkCount, GateOpen, Math.Min(SinkCount, Scenario.OrderTarget), Hash);
        }

        protected ulong CommitHash(ulong value) { Hash = value; Statistics = new SimulationStatistics(Statistics.Tick, Statistics.LogicalItems, Statistics.ValuesTouched, Statistics.ActiveLines, Statistics.SourceCount, Statistics.SinkCount, Statistics.GateOpen, Statistics.OrderProgress, Hash); return Hash; }

        protected ulong HashHeader()
        {
            var h = StateHasher.Offset;
            h = StateHasher.Add(h, TickNumber); h = StateHasher.Add(h, NextItemId); h = StateHasher.Add(h, Spawned); h = StateHasher.Add(h, SinkCount); h = StateHasher.Add(h, GateOpen ? 1 : 0);
            return h;
        }
    }

    public sealed class NaiveItemBeltSimulation : BeltSimulationBase
    {
        private readonly List<List<SimItem>> lanes = new List<List<SimItem>>();
        public override string Name => "Naive";

        public override void Reset(ScenarioDefinition scenario, BeltTopology topology)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario)); Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            lanes.Clear(); TickNumber = 0; NextItemId = 1; Spawned = 0; SinkCount = 0; ValuesTouched = 0; GateOpen = true;
            for (var laneIndex = 0; laneIndex < topology.Lanes.Length; laneIndex++)
            {
                var lane = new List<SimItem>(Math.Max(16, scenario.InitialItemsPerLane)); lanes.Add(lane);
                var count = Math.Min(scenario.InitialItemsPerLane, topology.Lanes[laneIndex].Length / Math.Max(1, scenario.MinimumSpacing)); var front = scenario.InitialFrontDistance >= 0 ? Math.Min(topology.Lanes[laneIndex].Length, scenario.InitialFrontDistance) : topology.Lanes[laneIndex].Length;
                for (var i = 0; i < count; i++) { lane.Add(new SimItem(NextItemId++, topology.Lanes[laneIndex].ItemType, front - i * scenario.MinimumSpacing)); Spawned++; }
            }
            FinishTick(TotalItems(), ActiveLines()); CalculateStateHash();
        }

        public override void Tick(in TickInput input)
        {
            TickNumber = input.Tick; ValuesTouched = 0; ApplyGate(input);
            for (var li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li]; var length = Topology.Lanes[li].Length;
                if (GateOpen && lane.Count > 0 && lane[0].Distance >= length) { lane.RemoveAt(0); SinkCount++; ValuesTouched++; }
                var limit = GateOpen ? length : length - Scenario.MinimumSpacing;
                for (var i = 0; i < lane.Count; i++)
                {
                    var item = lane[i]; var max = i == 0 ? limit : lane[i - 1].Distance - Scenario.MinimumSpacing;
                    item.Distance = Math.Min(item.Distance + Scenario.SpeedUnitsPerTick, max); lane[i] = item; ValuesTouched++;
                }
                if (Spawned < Scenario.MaxItems && Scenario.SpawnIntervalTicks > 0 && input.Tick % Scenario.SpawnIntervalTicks == 0 && (lane.Count == 0 || lane[lane.Count - 1].Distance >= Scenario.MinimumSpacing))
                { lane.Add(new SimItem(NextItemId++, Topology.Lanes[li].ItemType, 0)); Spawned++; ValuesTouched++; }
            }
            FinishTick(TotalItems(), ActiveLines());
        }

        public override void CreateSnapshot(BeltSnapshotWriter writer)
        {
            CalculateStateHash(); writer.Clear();
            for (var li = 0; li < lanes.Count; li++) for (var i = 0; i < lanes[li].Count; i++) { var item = lanes[li][i]; var gap = i == 0 ? Topology.Lanes[li].Length - item.Distance : lanes[li][i - 1].Distance - item.Distance - Scenario.MinimumSpacing; writer.Add(new BeltItemSnapshot(new ItemId(item.Id), item.Type, Topology.Lanes[li].LineId, Topology.Lanes[li].Id, item.Distance, gap)); }
            writer.SetStatistics(Statistics);
        }

        private int TotalItems() { var n = 0; for (var i = 0; i < lanes.Count; i++) n += lanes[i].Count; return n; }
        private int ActiveLines() { var n = 0; for (var i = 0; i < lanes.Count; i++) if (lanes[i].Count > 0) n++; return n; }
        private ulong BuildHash() { var h = HashHeader(); for (var li = 0; li < lanes.Count; li++) for (var i = 0; i < lanes[li].Count; i++) { var x = lanes[li][i]; h = StateHasher.Add(h, x.Id); h = StateHasher.Add(h, (int)x.Type); h = StateHasher.Add(h, Topology.Lanes[li].LineId.Value); h = StateHasher.Add(h, Topology.Lanes[li].Id.Value); h = StateHasher.Add(h, x.Distance); } return h; }
        public override ulong CalculateStateHash() => CommitHash(BuildHash());

        public override bool TryQuery(in BeltAccessPort port, out BeltItemHandle handle) { var li = port.LaneId.Value; if (li >= 0 && li < lanes.Count) for (var i = 0; i < lanes[li].Count; i++) if (lanes[li][i].Distance >= port.StartDistance && lanes[li][i].Distance <= port.EndDistance) { handle = new BeltItemHandle(new ItemId(lanes[li][i].Id), port.LaneId); return true; } handle = default; return false; }
        public override bool TryRemove(in BeltItemHandle handle) { var li = handle.LaneId.Value; if (li < 0 || li >= lanes.Count) return false; var itemId = handle.ItemId.Value; var index = lanes[li].FindIndex(x => x.Id == itemId); if (index < 0) return false; lanes[li].RemoveAt(index); return true; }
        public override bool TryInsert(in BeltAccessPort port, ItemType type, out BeltItemHandle handle) { var li = port.LaneId.Value; if (li < 0 || li >= lanes.Count) { handle = default; return false; } var d = port.StartDistance; for (var i = 0; i < lanes[li].Count; i++) if (Math.Abs(lanes[li][i].Distance - d) < Scenario.MinimumSpacing) { handle = default; return false; } var item = new SimItem(NextItemId++, type, d); var at = lanes[li].FindIndex(x => x.Distance < d); if (at < 0) lanes[li].Add(item); else lanes[li].Insert(at, item); Spawned++; handle = new BeltItemHandle(new ItemId(item.Id), port.LaneId); return true; }
    }

    public sealed class GapTransportLineSimulation : BeltSimulationBase
    {
        private sealed class GapLane { public readonly RingBuffer<SimItem> Items = new RingBuffer<SimItem>(); public int Offset; public int CompressionBoundary = -1; }
        private readonly List<GapLane> lanes = new List<GapLane>();
        public override string Name => "Gap";

        public override void Reset(ScenarioDefinition scenario, BeltTopology topology)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario)); Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            lanes.Clear(); TickNumber = 0; NextItemId = 1; Spawned = 0; SinkCount = 0; ValuesTouched = 0; GateOpen = true;
            for (var li = 0; li < topology.Lanes.Length; li++) { var lane = new GapLane(); lanes.Add(lane); var count = Math.Min(scenario.InitialItemsPerLane, topology.Lanes[li].Length / Math.Max(1, scenario.MinimumSpacing)); var front = scenario.InitialFrontDistance >= 0 ? Math.Min(topology.Lanes[li].Length, scenario.InitialFrontDistance) : topology.Lanes[li].Length; for (var i = 0; i < count; i++) { lane.Items.AddLast(new SimItem(NextItemId++, topology.Lanes[li].ItemType, front - i * scenario.MinimumSpacing)); Spawned++; } }
            FinishTick(TotalItems(), ActiveLines()); CalculateStateHash();
        }

        public override void Tick(in TickInput input)
        {
            TickNumber = input.Tick; ValuesTouched = 0; ApplyGate(input);
            for (var li = 0; li < lanes.Count; li++)
            {
                var lane = lanes[li]; var length = Topology.Lanes[li].Length;
                if (GateOpen && lane.Items.Count > 0 && Absolute(lane, 0) >= length) { lane.Items.RemoveFirst(); SinkCount++; ValuesTouched++; }
                var limit = GateOpen ? length : length - Scenario.MinimumSpacing;
                if (lane.Items.Count > 0 && Absolute(lane, 0) + Scenario.SpeedUnitsPerTick <= limit)
                { lane.Offset += Scenario.SpeedUnitsPerTick; lane.CompressionBoundary = -1; ValuesTouched++; }
                else if (lane.Items.Count > 0)
                {
                    lane.CompressionBoundary = 0;
                    for (var i = 0; i < lane.Items.Count; i++) { var item = lane.Items[i]; var max = i == 0 ? limit : Absolute(lane, i - 1) - Scenario.MinimumSpacing; var next = Math.Min(Absolute(lane, i) + Scenario.SpeedUnitsPerTick, max); item.Distance = next - lane.Offset; lane.Items[i] = item; ValuesTouched++; if (i > 0 && next < max) lane.CompressionBoundary = i; }
                }
                if (Spawned < Scenario.MaxItems && Scenario.SpawnIntervalTicks > 0 && input.Tick % Scenario.SpawnIntervalTicks == 0 && (lane.Items.Count == 0 || Absolute(lane, lane.Items.Count - 1) >= Scenario.MinimumSpacing))
                { lane.Items.AddLast(new SimItem(NextItemId++, Topology.Lanes[li].ItemType, -lane.Offset)); Spawned++; ValuesTouched++; }
            }
            FinishTick(TotalItems(), ActiveLines());
        }

        private static int Absolute(GapLane lane, int index) => lane.Items[index].Distance + lane.Offset;
        public override void CreateSnapshot(BeltSnapshotWriter writer)
        {
            CalculateStateHash(); writer.Clear(); for (var li = 0; li < lanes.Count; li++) for (var i = 0; i < lanes[li].Items.Count; i++) { var x = lanes[li].Items[i]; var d = Absolute(lanes[li], i); var gap = i == 0 ? Topology.Lanes[li].Length - d : Absolute(lanes[li], i - 1) - d - Scenario.MinimumSpacing; writer.Add(new BeltItemSnapshot(new ItemId(x.Id), x.Type, Topology.Lanes[li].LineId, Topology.Lanes[li].Id, d, gap)); } writer.SetStatistics(Statistics);
        }
        private int TotalItems() { var n = 0; for (var i = 0; i < lanes.Count; i++) n += lanes[i].Items.Count; return n; }
        private int ActiveLines() { var n = 0; for (var i = 0; i < lanes.Count; i++) if (lanes[i].Items.Count > 0) n++; return n; }
        private ulong BuildHash() { var h = HashHeader(); for (var li = 0; li < lanes.Count; li++) for (var i = 0; i < lanes[li].Items.Count; i++) { var x = lanes[li].Items[i]; h = StateHasher.Add(h, x.Id); h = StateHasher.Add(h, (int)x.Type); h = StateHasher.Add(h, Topology.Lanes[li].LineId.Value); h = StateHasher.Add(h, Topology.Lanes[li].Id.Value); h = StateHasher.Add(h, Absolute(lanes[li], i)); } return h; }
        public override ulong CalculateStateHash() => CommitHash(BuildHash());
        public override bool TryQuery(in BeltAccessPort port, out BeltItemHandle handle) { var li = port.LaneId.Value; if (li >= 0 && li < lanes.Count) for (var i = 0; i < lanes[li].Items.Count; i++) { var d = Absolute(lanes[li], i); if (d >= port.StartDistance && d <= port.EndDistance) { handle = new BeltItemHandle(new ItemId(lanes[li].Items[i].Id), port.LaneId); return true; } } handle = default; return false; }
        public override bool TryRemove(in BeltItemHandle handle) { var li = handle.LaneId.Value; if (li < 0 || li >= lanes.Count) return false; var lane = lanes[li]; for (var i = 0; i < lane.Items.Count; i++) if (lane.Items[i].Id == handle.ItemId.Value) { lane.Items.RemoveAt(i); return true; } return false; }
        public override bool TryInsert(in BeltAccessPort port, ItemType type, out BeltItemHandle handle)
        {
            var li = port.LaneId.Value; if (li < 0 || li >= lanes.Count) { handle = default; return false; } var lane = lanes[li]; var distance = port.StartDistance;
            var at = lane.Items.Count; for (var i = 0; i < lane.Items.Count; i++) { var d = Absolute(lane, i); if (Math.Abs(d - distance) < Scenario.MinimumSpacing) { handle = default; return false; } if (d < distance) { at = i; break; } }
            var item = new SimItem(NextItemId++, type, distance - lane.Offset); lane.Items.InsertAt(at, item); Spawned++; handle = new BeltItemHandle(new ItemId(item.Id), port.LaneId); return true;
        }
    }
}
