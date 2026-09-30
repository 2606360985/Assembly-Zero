using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AssemblyZero.Domain
{
    /// <summary>Single authority for logistics. Presentation may only read Snapshot.</summary>
    public sealed class EP02Simulation
    {
        public static readonly string[] TickOrder = { "ApplyCommands", "ItemSource", "BeltTransport", "BeltAccessQuery", "ItemReservation", "Inserter", "AssemblyCell", "RejectSink", "Statistics", "StateHash", "PublishSnapshot" };
        internal AssemblyScenarioDefinition Config { get; }
        internal InserterData[] Inserters { get; }
        internal AssemblySocketData[] Sockets { get; }
        internal AssemblyCellData Cell { get; } = new AssemblyCellData();
        internal ITransactionalBeltAccess Access { get; }
        internal ItemReservationSystem Reservations { get; } = new ItemReservationSystem();
        public IBeltSimulationBackend Backend { get; }
        public long TickNumber { get; private set; }
        public AssemblySnapshot Snapshot { get; private set; }
        public AssemblyStatistics Statistics { get; private set; }
        public double SimulationMs { get; private set; }
        public double AccessMs { get; private set; }
        public double HashMs { get; private set; }
        public double SnapshotMs { get; private set; }
        private readonly BeltTopology topology;
        private readonly List<AssemblyItemData> items = new List<AssemblyItemData>();
        private readonly List<AssemblyCommand> commands = new List<AssemblyCommand>();
        private readonly List<AssemblyEvent> events = new List<AssemblyEvent>();
        private readonly List<BeltAccessCandidate>[] queries;
        private readonly InserterSystem inserterSystem = new InserterSystem();
        private readonly AssemblyCellSystem cellSystem = new AssemblyCellSystem();
        private readonly BeltSnapshotWriter beltSnapshot = new BeltSnapshotWriter();
        private readonly List<BeltAccessCandidate> exitQuery = new List<BeltAccessCandidate>();
        private uint randomState;
        private PartTypeId[] bag;
        private int bagIndex, nextLane, sourceBlockedTicks;
        private long nextSourceTick = 1, nextRejectTick = 1;
        private readonly Stopwatch clock = new Stopwatch();

        public EP02Simulation(AssemblyScenarioDefinition config, BeltTopology topology, BackendKind kind = BackendKind.Gap)
        {
            config.Validate(topology); this.topology = topology;
            // Copy configuration so later Inspector edits cannot mutate a running domain.
            Config = new AssemblyScenarioDefinition { Seed = config.Seed, BeltSpeed = config.BeltSpeed, MinimumSpacing = config.MinimumSpacing, SourceInterval = config.SourceInterval, RejectInterval = config.RejectInterval, ScanTicks = config.ScanTicks, ShowcaseTicks = config.ShowcaseTicks, MaxGenerated = config.MaxGenerated, SourceEnabled = config.SourceEnabled, FullRobotManifest = config.FullRobotManifest, SourceParts = (PartTypeId[])config.SourceParts.Clone(), InitialParts = (InitialAssemblyPart[])config.InitialParts.Clone(), Manifest = new AssemblyManifest { RequiredParts = (PartTypeId[])config.Manifest.RequiredParts.Clone() } };
            Inserters = new InserterData[config.Inserters.Length]; for (var i = 0; i < Inserters.Length; i++) Inserters[i] = config.Inserters[i].Clone();
            Sockets = new AssemblySocketData[config.Sockets.Length]; for (var i = 0; i < Sockets.Length; i++) Sockets[i] = config.Sockets[i].Clone();
            Array.Sort(Inserters, (a, b) => a.Id.Value.CompareTo(b.Id.Value)); Array.Sort(Sockets, (a, b) => a.Id.Value.CompareTo(b.Id.Value));
            Config.Inserters = Inserters; Config.Sockets = Sockets;
            foreach (var arm in Inserters) { arm.State = arm.Enabled ? InserterState.WaitingForItem : InserterState.Disabled; arm.ReservedItem = arm.HeldItem = arm.IntentItem = arm.LastIntentItem = 0; arm.LastIntentTick = arm.StateEnterTick = 0; arm.EnabledTicks = arm.BusyTicks = arm.StarvedTicks = arm.BlockedTicks = arm.Transfers = 0; }
            foreach (var socket in Sockets) { socket.ItemId = 0; socket.ReservedBy = -1; }
            Backend = kind == BackendKind.Naive ? (IBeltSimulationBackend)new NaiveItemBeltSimulation() : new GapTransportLineSimulation(); Access = (ITransactionalBeltAccess)Backend;
            Backend.Reset(new ScenarioDefinition { SpeedUnitsPerTick = Config.BeltSpeed, MinimumSpacing = Config.MinimumSpacing, SpawnIntervalTicks = 0, InitialItems = 0, ExternalLogistics = true }, topology);
            randomState = (uint)Config.Seed; if (randomState == 0) randomState = 1;
            bag = (PartTypeId[])Config.SourceParts.Clone(); bagIndex = bag.Length;
            queries = new List<BeltAccessCandidate>[Inserters.Length]; for (var i = 0; i < queries.Length; i++) queries[i] = new List<BeltAccessCandidate>();
            foreach (var initial in Config.InitialParts) if (!Insert(initial.Part, initial.Lane, initial.Distance)) throw new ArgumentException("Initial parts overlap or lie outside the belt.");
            RefreshStatistics(0); var hash = CalculateStateHash(); RefreshStatistics(hash); PublishSnapshot();
        }

        internal AssemblyItemData Item(int id) => id > 0 && id <= items.Count ? items[id - 1] : null;
        public bool TryGetItemRecord(int id, out AssemblyItemSnapshot record)
        {
            var item = Item(id); if (item == null) { record = default; return false; }
            foreach (var visible in Snapshot.Items) if (visible.ItemId == id) { record = visible; return true; }
            record = new AssemblyItemSnapshot(item, -1, 0); return true;
        }
        internal AssemblySocketData Socket(int id) { foreach (var s in Sockets) if (s.Id.Value == id) return s; throw new ArgumentException("Unknown socket ID."); }
        public void EnqueueCommand(in AssemblyCommand command)
        {
            if (command.Tick <= TickNumber) throw new ArgumentException("Commands must target a future Tick.");
            foreach (var queued in commands) if (queued.Tick == command.Tick && queued.Sequence == command.Sequence) throw new ArgumentException("Duplicate command sequence.");
            if (command.Kind == AssemblyCommandKind.SetSocketLocked) Socket(command.Target);
            else { var found = false; foreach (var arm in Inserters) if (arm.Id.Value == command.Target) found = true; if (!found) throw new ArgumentException("Unknown inserter ID."); }
            commands.Add(command); commands.Sort((a, b) => { var c = a.Tick.CompareTo(b.Tick); return c != 0 ? c : a.Sequence.CompareTo(b.Sequence); });
        }

        public void Tick()
        {
            clock.Restart(); TickNumber++;
            ApplyCommands(); Source(); Access.TransportTick(TickNumber);
            var begin = clock.Elapsed.TotalMilliseconds;
            Query(); Reserve(); AccessMs = clock.Elapsed.TotalMilliseconds - begin;
            foreach (var arm in Inserters) inserterSystem.Tick(this, arm);
            cellSystem.Tick(this); Reject(); AccumulateUtilization(); RefreshStatistics(0);
            begin = clock.Elapsed.TotalMilliseconds; var hash = CalculateStateHash(); HashMs = clock.Elapsed.TotalMilliseconds - begin;
            RefreshStatistics(hash); begin = clock.Elapsed.TotalMilliseconds; PublishSnapshot(); SnapshotMs = clock.Elapsed.TotalMilliseconds - begin;
            SimulationMs = clock.Elapsed.TotalMilliseconds - AccessMs - HashMs - SnapshotMs; clock.Stop();
            if (!Statistics.Conserved) throw new InvalidOperationException("EP02 material conservation violated.");
        }
        private void ApplyCommands()
        {
            while (commands.Count > 0 && commands[0].Tick == TickNumber)
            {
                var c = commands[0]; commands.RemoveAt(0);
                if (c.Kind == AssemblyCommandKind.SetSocketLocked) Socket(c.Target).Locked = c.Value;
                else foreach (var arm in Inserters) if (arm.Id.Value == c.Target)
                {
                    arm.Enabled = c.Value;
                    if (!c.Value)
                    {
                        if (arm.HeldItem != 0) { var held = Item(arm.HeldItem); held.Ownership = ItemOwnership.InRejectSink; held.OwnerId = -1; held.RejectReason = AssemblyRejectReason.InserterDisabledWhileHolding; Emit(AssemblyEventKind.Reject, arm.Id.Value, held.ItemId); arm.HeldItem = 0; }
                        Reservations.Release(this, arm); InserterSystem.Enter(arm, InserterState.Disabled, TickNumber); Emit(AssemblyEventKind.Alarm, arm.Id.Value);
                    }
                }
            }
        }
        private uint Random()
        { var x = randomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return randomState = x; }
        private void Source()
        {
            if (!Config.SourceEnabled || TickNumber < nextSourceTick || (Config.MaxGenerated > 0 && items.Count >= Config.MaxGenerated)) return;
            if (bagIndex >= bag.Length) { bag = (PartTypeId[])Config.SourceParts.Clone(); for (var i = bag.Length - 1; i > 0; i--) { var j = (int)(Random() % (uint)(i + 1)); var x = bag[i]; bag[i] = bag[j]; bag[j] = x; } bagIndex = 0; }
            for (var attempt = 0; attempt < topology.Lanes.Length; attempt++)
            {
                var li = (nextLane + attempt) % topology.Lanes.Length;
                if (!Insert(bag[bagIndex], topology.Lanes[li].Id.Value, 0)) continue;
                bagIndex++; nextLane = (li + 1) % topology.Lanes.Length; nextSourceTick = TickNumber + Config.SourceInterval; return;
            }
            sourceBlockedTicks++;
        }
        private bool Insert(PartTypeId part, int lane, int distance)
        {
            if (lane < 0 || lane >= 64) return false;
            LineId line = default; var found = false; foreach (var def in topology.Lanes) if (def.Id.Value == lane) { line = def.LineId; found = true; }
            if (!found) return false;
            var port = new BeltAccessPort(line, 1UL << lane, distance, distance, distance, part.Mask);
            if (!Access.TryInsertPart(port, part, out var handle)) return false;
            if (handle.ItemId.Value != items.Count + 1) throw new InvalidOperationException("Item ID allocation must be monotonic.");
            items.Add(new AssemblyItemData { ItemId = handle.ItemId.Value, Part = part, Ownership = ItemOwnership.OnBelt, GeneratedTick = TickNumber, Handle = handle }); return true;
        }
        private void Query()
        {
            for (var i = 0; i < Inserters.Length; i++)
            {
                var arm = Inserters[i]; arm.IntentItem = 0; queries[i].Clear();
                if (!arm.Enabled || TickNumber < arm.PhaseTicks || arm.HeldItem != 0 || arm.ReservedItem != 0 || (arm.State != InserterState.WaitingForItem && arm.State != InserterState.WaitingForTarget)) continue;
                Access.QueryCandidates(arm.Port, queries[i]);
                foreach (var candidate in queries[i]) if (candidate.Part.Value == arm.AllowedPart.Value && Item(candidate.Handle.ItemId.Value).Ownership == ItemOwnership.OnBelt && (long)candidate.Distance + (long)Config.BeltSpeed * (arm.PickingTicks + 1) <= arm.Port.EndDistance)
                { arm.IntentItem = arm.LastIntentItem = candidate.Handle.ItemId.Value; arm.LastIntentTick = TickNumber; break; }
            }
        }
        private void Reserve()
        {
            for (var i = 0; i < Inserters.Length; i++)
            {
                var arm = Inserters[i]; if (arm.IntentItem == 0) continue;
                foreach (var candidate in queries[i]) if (candidate.Part.Value == arm.AllowedPart.Value && (long)candidate.Distance + (long)Config.BeltSpeed * (arm.PickingTicks + 1) <= arm.Port.EndDistance && Reservations.TryReserve(this, arm, candidate)) break;
            }
        }
        private void Reject()
        {
            if (TickNumber < nextRejectTick) return;
            // One shared receiver; stable item ID wins when both lanes reach the endpoint.
            BeltAccessCandidate selected = default;
            foreach (var lane in topology.Lanes)
            {
                var end = lane.Length - Config.MinimumSpacing;
                Access.QueryCandidates(new BeltAccessPort(lane.LineId, 1UL << lane.Id.Value, end, lane.Length, end, ulong.MaxValue), exitQuery);
                foreach (var candidate in exitQuery) if (Item(candidate.Handle.ItemId.Value).Ownership == ItemOwnership.OnBelt && (!selected.Handle.IsValid || candidate.Handle.ItemId.Value < selected.Handle.ItemId.Value)) selected = candidate;
            }
            if (!selected.Handle.IsValid || !Access.TryTake(selected.Handle, out _)) return;
            var item = Item(selected.Handle.ItemId.Value); item.Ownership = ItemOwnership.InRejectSink; item.OwnerId = -1; item.RejectReason = AssemblyRejectReason.MissedPickupAtLineEnd; nextRejectTick = TickNumber + Config.RejectInterval; Emit(AssemblyEventKind.Reject, -1, item.ItemId);
        }
        private void AccumulateUtilization()
        {
            foreach (var arm in Inserters)
            {
                if (!arm.Enabled) continue; arm.EnabledTicks++;
                if (arm.State == InserterState.WaitingForItem) arm.StarvedTicks++;
                else if (arm.State == InserterState.WaitingForTarget) arm.BlockedTicks++;
                else arm.BusyTicks++;
            }
        }
        internal void Emit(AssemblyEventKind kind, int actor, int item = 0)
        { events.Add(new AssemblyEvent(TickNumber, kind, actor, item)); if (events.Count > 128) events.RemoveAt(0); }
        private void RefreshStatistics(ulong hash)
        {
            var counts = new int[6]; foreach (var item in items) counts[(int)item.Ownership]++;
            Statistics = new AssemblyStatistics(TickNumber, items.Count, counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], Cell.CompletedRobotCount, Backend.Statistics.ValuesTouched, sourceBlockedTicks, Cell.CompletedRobotCount > 0 ? Cell.TotalAssemblyTicks / (double)Cell.CompletedRobotCount / BeltConstants.TickRate : 0, hash);
        }
        public ulong CalculateStateHash()
        {
            var h = StateHasher.Add(StateHasher.Offset, TickNumber);
            h = StateHasher.Add(h, unchecked((long)Backend.CalculateStateHash())); h = StateHasher.Add(h, randomState); h = StateHasher.Add(h, bagIndex); foreach (var p in bag) h = StateHasher.Add(h, p.Value);
            h = StateHasher.Add(h, nextSourceTick); h = StateHasher.Add(h, nextRejectTick); h = StateHasher.Add(h, nextLane); h = StateHasher.Add(h, sourceBlockedTicks);
            foreach (var item in items) { h = StateHasher.Add(h, item.ItemId); h = StateHasher.Add(h, item.Part.Value); h = StateHasher.Add(h, (int)item.Ownership); h = StateHasher.Add(h, item.OwnerId); h = StateHasher.Add(h, item.GeneratedTick); h = StateHasher.Add(h, (int)item.RejectReason); }
            foreach (var arm in Inserters)
            {
                h = StateHasher.Add(h, arm.Id.Value); h = StateHasher.Add(h, arm.Enabled ? 1 : 0); h = StateHasher.Add(h, (int)arm.State); h = StateHasher.Add(h, arm.StateEnterTick); h = StateHasher.Add(h, arm.ReservedItem); h = StateHasher.Add(h, arm.HeldItem); h = StateHasher.Add(h, arm.IntentItem); h = StateHasher.Add(h, arm.PickupDistance); h = StateHasher.Add(h, arm.PickupLane);
                h = StateHasher.Add(h, arm.Transfers); h = StateHasher.Add(h, arm.EnabledTicks); h = StateHasher.Add(h, arm.BusyTicks); h = StateHasher.Add(h, arm.StarvedTicks); h = StateHasher.Add(h, arm.BlockedTicks);
                h = StateHasher.Add(h, arm.LastIntentItem); h = StateHasher.Add(h, arm.LastIntentTick);
            }
            foreach (var socket in Sockets) { h = StateHasher.Add(h, socket.Id.Value); h = StateHasher.Add(h, socket.ItemId); h = StateHasher.Add(h, socket.ReservedBy); h = StateHasher.Add(h, socket.Locked ? 1 : 0); }
            h = StateHasher.Add(h, (int)Cell.State); h = StateHasher.Add(h, Cell.StateEnterTick); h = StateHasher.Add(h, Cell.FirstInstallTick); h = StateHasher.Add(h, Cell.CompletedRobotCount); h = StateHasher.Add(h, Cell.TotalAssemblyTicks);
            foreach (var c in commands) { h = StateHasher.Add(h, c.Tick); h = StateHasher.Add(h, c.Sequence); h = StateHasher.Add(h, (int)c.Kind); h = StateHasher.Add(h, c.Target); h = StateHasher.Add(h, c.Value ? 1 : 0); }
            return h;
        }
        private void PublishSnapshot()
        {
            Backend.CreateSnapshot(beltSnapshot);
            var positions = new Dictionary<int, BeltItemSnapshot>(); foreach (var item in beltSnapshot.Items) positions.Add(item.ItemId.Value, item);
            var visible = new List<AssemblyItemSnapshot>();
            foreach (var item in items)
            {
                if (positions.TryGetValue(item.ItemId, out var position)) visible.Add(new AssemblyItemSnapshot(item, position.LaneId.Value, position.Distance));
                else if (item.Ownership == ItemOwnership.HeldByInserter || item.Ownership == ItemOwnership.InAssemblySocket || (item.Ownership == ItemOwnership.CompletedProduct && SocketContains(item.ItemId))) visible.Add(new AssemblyItemSnapshot(item, -1, 0));
            }
            var arms = new InserterSnapshot[Inserters.Length]; for (var i = 0; i < arms.Length; i++) arms[i] = new InserterSnapshot(Inserters[i]);
            var sockets = new AssemblySocketSnapshot[Sockets.Length]; for (var i = 0; i < sockets.Length; i++) sockets[i] = new AssemblySocketSnapshot(Sockets[i]);
            Snapshot = new AssemblySnapshot { Tick = TickNumber, CellState = Cell.State, CellStateEnterTick = Cell.StateEnterTick, Statistics = Statistics, Items = visible.AsReadOnly(), Inserters = Array.AsReadOnly(arms), Sockets = Array.AsReadOnly(sockets), Events = Array.AsReadOnly(events.ToArray()) };
        }
        private bool SocketContains(int itemId) { foreach (var s in Sockets) if (s.ItemId == itemId) return true; return false; }
    }
}
