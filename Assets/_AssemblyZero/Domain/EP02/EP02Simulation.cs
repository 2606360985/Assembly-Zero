using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace AssemblyZero.Domain
{
    /// <summary>Fixed-tick authority. Unity presentation never owns material or production state.</summary>
    public sealed class EP02Simulation
    {
        public static readonly string[] TickOrder = { "ApplyCommands", "ItemSource", "BeltTransport", "ProductTransport", "BeltAccessQuery", "ItemReservation", "Inserter", "ProductAssembly", "RejectSink", "Statistics", "StateHash", "PublishSnapshot" };
        internal AssemblyScenarioDefinition Config { get; }
        internal InserterData[] Inserters => Config.Inserters;
        internal AssemblySocketData[] Sockets => Config.Sockets;
        internal AssemblyCellData Cell { get; } = new AssemblyCellData();
        internal List<ProductData> Products { get; } = new List<ProductData>();
        internal ProductData LatestCompleted;
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
        private readonly List<BeltAccessCandidate> scratch = new List<BeltAccessCandidate>();
        private readonly InserterSystem inserterSystem = new InserterSystem();
        private readonly AssemblyCellSystem cellSystem = new AssemblyCellSystem();
        private readonly BeltSnapshotWriter beltSnapshot = new BeltSnapshotWriter();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly long[] segmentPhases, supplyPhases;
        private readonly int[] segmentSpeeds;
        private uint randomState;
        private PartTypeId[] bag;
        private int bagIndex, nextLane, sourceBlockedTicks, productSourceBlockedTicks, exitedProducts;
        private long nextSourceTick = 1, nextRejectTick = 1, nextProductTick = 1;
        public EP02Simulation(AssemblyScenarioDefinition config, BeltTopology topology, BackendKind kind = BackendKind.Gap)
        {
            config.Validate(topology); this.topology = topology; Config = config.Clone();
            Array.Sort(Inserters, (a, b) => a.Id.Value.CompareTo(b.Id.Value));
            foreach (var a in Inserters) { a.State = a.Enabled ? InserterState.WaitingForItem : InserterState.Disabled; a.ReservedItem = a.HeldItem = a.IntentItem = a.TargetProductId = 0; a.StateEnterTick = 0; }
            foreach (var s in Sockets) { s.ItemId = 0; s.ReservedBy = -1; }
            Backend = kind == BackendKind.Naive ? (IBeltSimulationBackend)new NaiveItemBeltSimulation() : new GapTransportLineSimulation(); Access = (ITransactionalBeltAccess)Backend;
            Backend.Reset(new ScenarioDefinition { LaneSpeeds = (int[])Config.SupplySpeeds.Clone(), MinimumSpacing = Config.MinimumSpacing, ExternalLogistics = true }, topology);
            randomState = (uint)Config.Seed; if (randomState == 0) randomState = 1;
            bag = (PartTypeId[])Config.SourceParts.Clone(); bagIndex = bag.Length;
            queries = Inserters.Select(_ => new List<BeltAccessCandidate>()).ToArray();
            segmentPhases = new long[Config.SegmentEnds.Length]; segmentSpeeds = new int[segmentPhases.Length]; supplyPhases = new long[Config.SupplySpeeds.Length];
            foreach (var x in Config.InitialParts) if (!Insert(x.Part, x.Lane, x.Distance)) throw new ArgumentException("Initial parts overlap or lie outside supply belts.");
            RefreshStatistics(0); RefreshStatistics(CalculateStateHash()); PublishSnapshot();
        }
        internal AssemblyItemData Item(int id) => id > 0 && id <= items.Count ? items[id - 1] : null;
        internal AssemblySocketData Socket(int id) => Sockets.Single(s => s.Id.Value == id);
        internal AssemblySocketData Target(InserterData a)
        {
            var p = Products.FirstOrDefault(x => x.Id.Value == a.TargetProductId && !x.Exited);
            return p?.Sockets.FirstOrDefault(s => s.Id.Value == a.TargetSocket.Value);
        }
        internal bool InPort(InserterData a, BeltAccessCandidate c) => Ports(a).Any(p => p.TransportLineId.Value == c.Handle.LineId.Value && (p.LaneMask & (1UL << c.Handle.LaneId.Value)) != 0 && c.Distance >= p.StartDistance && c.Distance <= p.EndDistance);
        private static IEnumerable<BeltAccessPort> Ports(InserterData a) => a.Ports.Length == 0 ? new[] { a.Port } : a.Ports;
        private int SupplySpeed(int lane) => Config.SupplySpeeds[Array.FindIndex(topology.Lanes, l => l.Id.Value == lane)];
        public bool TryGetItemRecord(int id, out AssemblyItemSnapshot record)
        {
            var item = Item(id); record = default; if (item == null) return false;
            foreach (var x in Snapshot.Items) if (x.ItemId == id) { record = x; return true; }
            record = new AssemblyItemSnapshot(item, -1, 0); return true;
        }
        public void EnqueueCommand(in AssemblyCommand command)
        {
            var tick = command.Tick; var sequence = command.Sequence;
            if (tick <= TickNumber || commands.Any(c => c.Tick == tick && c.Sequence == sequence)) throw new ArgumentException("Invalid command tick or sequence.");
            var target = command.Target;
            if (command.Kind == AssemblyCommandKind.SetSocketLocked) Socket(target); else if (!Inserters.Any(a => a.Id.Value == target)) throw new ArgumentException("Unknown arm.");
            commands.Add(command); commands.Sort((a, b) => a.Tick == b.Tick ? a.Sequence.CompareTo(b.Sequence) : a.Tick.CompareTo(b.Tick));
        }
        public void Tick()
        {
            clock.Restart(); TickNumber++; ApplyCommands(); Source(); Access.TransportTick(TickNumber);
            for (var i = 0; i < supplyPhases.Length; i++) supplyPhases[i] += Config.SupplySpeeds[i];
            TransportProducts(); BindTargets(); var begin = clock.Elapsed.TotalMilliseconds; Query(); Reserve(); AccessMs = clock.Elapsed.TotalMilliseconds - begin;
            foreach (var a in Inserters) inserterSystem.Tick(this, a);
            cellSystem.Tick(this); Reject(); AccumulateUtilization(); RefreshStatistics(0);
            begin = clock.Elapsed.TotalMilliseconds; var hash = CalculateStateHash(); HashMs = clock.Elapsed.TotalMilliseconds - begin;
            RefreshStatistics(hash); begin = clock.Elapsed.TotalMilliseconds; PublishSnapshot(); SnapshotMs = clock.Elapsed.TotalMilliseconds - begin;
            SimulationMs = clock.Elapsed.TotalMilliseconds - AccessMs - HashMs - SnapshotMs; clock.Stop();
            if (!Statistics.Conserved || !Snapshot.ProductsConserved) throw new InvalidOperationException("Production conservation violated.");
        }
        private void ApplyCommands()
        {
            while (commands.Count > 0 && commands[0].Tick == TickNumber)
            {
                var c = commands[0]; commands.RemoveAt(0);
                if (c.Kind == AssemblyCommandKind.SetSocketLocked)
                {
                    Socket(c.Target).Locked = c.Value;
                    foreach (var p in Products) foreach (var s in p.Sockets) if (s.Id.Value == c.Target) s.Locked = c.Value;
                }
                else foreach (var a in Inserters) if (a.Id.Value == c.Target)
                {
                    a.Enabled = c.Value;
                    if (!c.Value)
                    {
                        if (a.HeldItem != 0) { var held = Item(a.HeldItem); held.Ownership = ItemOwnership.InRejectSink; held.OwnerId = -1; held.ProductId = 0; held.RejectReason = AssemblyRejectReason.InserterDisabledWhileHolding; Emit(AssemblyEventKind.Reject, a.Id.Value, held.ItemId); a.HeldItem = 0; }
                        Reservations.Release(this, a); InserterSystem.Enter(a, InserterState.Disabled, TickNumber); Emit(AssemblyEventKind.Alarm, a.Id.Value);
                    }
                }
            }
        }
        private uint Random() { var x = randomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return randomState = x; }
        private void Source()
        {
            if (!Config.SourceEnabled || TickNumber < nextSourceTick || Config.MaxGenerated > 0 && items.Count >= Config.MaxGenerated) return;
            if (bagIndex >= bag.Length) { bag = (PartTypeId[])Config.SourceParts.Clone(); for (var i = bag.Length - 1; i > 0; i--) { var j = (int)(Random() % (uint)(i + 1)); var x = bag[i]; bag[i] = bag[j]; bag[j] = x; } bagIndex = 0; }
            for (var n = 0; n < topology.Lanes.Length; n++) { var lane = (nextLane + n) % topology.Lanes.Length; if (!Insert(bag[bagIndex], lane, 0)) continue; bagIndex++; nextLane = (lane + 1) % topology.Lanes.Length; nextSourceTick = TickNumber + Config.SourceInterval; return; }
            sourceBlockedTicks++;
        }
        private bool Insert(PartTypeId part, int lane, int distance)
        {
            if (lane < 0 || lane >= topology.Lanes.Length) return false;
            var l = topology.Lanes[lane]; var port = new BeltAccessPort(l.LineId, 1UL << l.Id.Value, distance, distance, distance, part.Mask);
            if (!Access.TryInsertPart(port, part, out var handle)) return false;
            if (handle.ItemId.Value != items.Count + 1) throw new InvalidOperationException("Item IDs must be monotonic.");
            items.Add(new AssemblyItemData { ItemId = handle.ItemId.Value, Part = part, Handle = handle, Ownership = ItemOwnership.OnBelt, GeneratedTick = TickNumber }); return true;
        }
        private void TransportProducts()
        {
            for (var i = 0; i < segmentSpeeds.Length; i++) segmentSpeeds[i] = Config.ProductSpeed;
            ProductData front = null;
            foreach (var p in Products)
            {
                if (p.Exited) continue;
                var before = p.Distance; var oldSegment = p.Segment;
                var limit = p.NextStation < Config.Stations.Length ? Config.Stations[p.NextStation].Distance : Config.ProductLength;
                if (front != null) limit = Math.Min(limit, front.Distance - Config.ProductSpacing);
                if (p.Segment < Config.SegmentEnds.Length - 1 && Products.Any(x => !x.Exited && x != p && x.Segment == p.Segment + 1)) limit = Math.Min(limit, Config.SegmentEnds[p.Segment] - 1);
                p.Distance = Math.Max(before, Math.Min(before + Config.ProductSpeed, limit));
                segmentSpeeds[oldSegment] = p.Distance - before;
                if (p.Distance == before && p.NextStation < Config.Stations.Length && p.Distance != Config.Stations[p.NextStation].Distance) p.State = ProductState.WaitingForDownstream;
                else if (p.Distance > before) p.State = p.Completed ? ProductState.Completed : ProductState.Moving;
                while (p.Segment < Config.SegmentEnds.Length - 1 && p.Distance >= Config.SegmentEnds[p.Segment]) p.Segment++;
                if (p.Distance == Config.ProductLength) { if (!p.Completed) throw new InvalidOperationException("Unfinished product reached the outlet."); p.Exited = true; exitedProducts++; } else front = p;
            }
            if (TickNumber >= nextProductTick)
            {
                if (Products.Any(p => !p.Exited && (p.Segment == 0 || p.Distance < Config.ProductSpacing))) productSourceBlockedTicks++;
                else { Products.Add(new ProductData { Id = new ProductId(Products.Count + 1), GeneratedTick = TickNumber, Sockets = Sockets.Select(s => s.Clone()).ToArray(), State = ProductState.Moving }); nextProductTick = TickNumber + Config.ProductInterval; }
            }
            for (var i = 0; i < segmentPhases.Length; i++) segmentPhases[i] += segmentSpeeds[i];
        }
        private void BindTargets()
        {
            foreach (var a in Inserters)
            {
                if (a.TargetProductId != 0) continue;
                var station = Config.Stations.Single(s => s.Id == a.StationId);
                var p = Products.FirstOrDefault(x => !x.Exited && x.NextStation < Config.Stations.Length && Config.Stations[x.NextStation].Id == station.Id && x.Distance == station.Distance);
                if (p != null) a.TargetProductId = p.Id.Value;
            }
        }
        private void Query()
        {
            for (var i = 0; i < Inserters.Length; i++)
            {
                var a = Inserters[i]; a.IntentItem = 0; queries[i].Clear(); var target = Target(a);
                if (!a.Enabled || target == null || target.Locked || target.ItemId != 0 || a.HeldItem != 0 || a.ReservedItem != 0 || a.State == InserterState.Returning) continue;
                foreach (var port in Ports(a))
                {
                    Access.QueryCandidates(port, scratch);
                    foreach (var c in scratch) if (Item(c.Handle.ItemId.Value).Ownership == ItemOwnership.OnBelt && c.Part.Value == a.AllowedPart.Value && (long)c.Distance + (long)SupplySpeed(c.Handle.LaneId.Value) * (a.PickingTicks + 1) <= port.EndDistance) queries[i].Add(c);
                }
                queries[i].Sort((x, y) => x.Handle.ItemId.Value.CompareTo(y.Handle.ItemId.Value));
                if (queries[i].Count != 0) { a.IntentItem = a.LastIntentItem = queries[i][0].Handle.ItemId.Value; a.LastIntentTick = TickNumber; }
            }
        }
        private void Reserve() { for (var i = 0; i < Inserters.Length; i++) foreach (var c in queries[i]) if (Reservations.TryReserve(this, Inserters[i], c)) break; }
        private void Reject()
        {
            if (TickNumber < nextRejectTick) return; BeltAccessCandidate selected = default;
            foreach (var l in topology.Lanes)
            {
                Access.QueryCandidates(new BeltAccessPort(l.LineId, 1UL << l.Id.Value, l.Length - Config.MinimumSpacing, l.Length, l.Length, ulong.MaxValue), scratch);
                foreach (var c in scratch) if (Item(c.Handle.ItemId.Value).Ownership == ItemOwnership.OnBelt && (!selected.Handle.IsValid || c.Handle.ItemId.Value < selected.Handle.ItemId.Value)) selected = c;
            }
            if (!selected.Handle.IsValid || !Access.TryTake(selected.Handle, out _)) return;
            var item = Item(selected.Handle.ItemId.Value); item.Ownership = ItemOwnership.InRejectSink; item.OwnerId = -1; item.RejectReason = AssemblyRejectReason.MissedPickupAtLineEnd;
            nextRejectTick = TickNumber + Config.RejectInterval; Emit(AssemblyEventKind.Reject, -1, item.ItemId);
        }
        private void AccumulateUtilization()
        {
            foreach (var a in Inserters) { if (!a.Enabled) continue; a.EnabledTicks++; if (a.State == InserterState.WaitingForItem) a.StarvedTicks++; else if (a.State == InserterState.WaitingForTarget) a.BlockedTicks++; else a.BusyTicks++; }
        }
        internal void Emit(AssemblyEventKind kind, int actor, int item = 0) { events.Add(new AssemblyEvent(TickNumber, kind, actor, item)); if (events.Count > 128) events.RemoveAt(0); }
        private void RefreshStatistics(ulong hash)
        {
            var n = new int[6]; foreach (var item in items) n[(int)item.Ownership]++;
            Statistics = new AssemblyStatistics(TickNumber, items.Count, n[0], n[1], n[2], n[3], n[4], n[5], Cell.CompletedRobotCount, Backend.Statistics.ValuesTouched, sourceBlockedTicks, Cell.CompletedRobotCount == 0 ? 0 : Cell.TotalAssemblyTicks / (double)Cell.CompletedRobotCount / BeltConstants.TickRate, hash);
        }
        public ulong CalculateStateHash()
        {
            var h = StateHasher.Add(StateHasher.Offset, TickNumber); h = StateHasher.Add(h, unchecked((long)Backend.CalculateStateHash()));
            h = StateHasher.Add(h, randomState); h = StateHasher.Add(h, bagIndex); foreach (var x in bag) h = StateHasher.Add(h, x.Value);
            foreach (var x in new long[] { nextSourceTick, nextRejectTick, nextProductTick, nextLane, sourceBlockedTicks, productSourceBlockedTicks, exitedProducts, Cell.CompletedRobotCount, Cell.TotalAssemblyTicks }) h = StateHasher.Add(h, x);
            foreach (var item in items) foreach (var x in new long[] { item.ItemId, item.Part.Value, (int)item.Ownership, item.OwnerId, item.ProductId, item.GeneratedTick, (int)item.RejectReason }) h = StateHasher.Add(h, x);
            foreach (var a in Inserters) foreach (var x in new long[] { a.Id.Value, a.Enabled ? 1 : 0, (int)a.State, a.StateEnterTick, a.TargetProductId, a.ReservedItem, a.HeldItem, a.IntentItem, a.PickupDistance, a.PickupLane, a.Transfers, a.EnabledTicks, a.BusyTicks, a.StarvedTicks, a.BlockedTicks, a.LastIntentItem, a.LastIntentTick }) h = StateHasher.Add(h, x);
            foreach (var s in Sockets) h = StateHasher.Add(h, s.Locked ? 1 : 0);
            foreach (var p in Products)
            {
                foreach (var x in new long[] { p.Id.Value, p.Distance, p.Segment, p.NextStation, (int)p.State, p.GeneratedTick, p.ScanEnterTick, p.CompletedTick, p.Completed ? 1 : 0, p.Exited ? 1 : 0 }) h = StateHasher.Add(h, x);
                foreach (var s in p.Sockets) foreach (var x in new[] { s.Id.Value, s.ItemId, s.ReservedBy, s.Locked ? 1 : 0 }) h = StateHasher.Add(h, x);
            }
            foreach (var x in segmentPhases) h = StateHasher.Add(h, x); foreach (var x in supplyPhases) h = StateHasher.Add(h, x);
            foreach (var c in commands) foreach (var x in new long[] { c.Tick, c.Sequence, (int)c.Kind, c.Target, c.Value ? 1 : 0 }) h = StateHasher.Add(h, x);
            return h;
        }
        private void PublishSnapshot()
        {
            Backend.CreateSnapshot(beltSnapshot); var positions = beltSnapshot.Items.ToDictionary(x => x.ItemId.Value); var active = Products.Where(p => !p.Exited).ToArray(); var activeIds = new HashSet<int>(active.Select(p => p.Id.Value));
            var visible = new List<AssemblyItemSnapshot>();
            foreach (var item in items)
            {
                if (positions.TryGetValue(item.ItemId, out var x)) visible.Add(new AssemblyItemSnapshot(item, x.LaneId.Value, x.Distance));
                else if (item.Ownership == ItemOwnership.HeldByInserter || (item.Ownership == ItemOwnership.InAssemblySocket || item.Ownership == ItemOwnership.CompletedProduct) && activeIds.Contains(item.ProductId)) visible.Add(new AssemblyItemSnapshot(item, -1, 0));
            }
            Snapshot = new AssemblySnapshot { Tick = TickNumber, Statistics = Statistics, Items = visible.AsReadOnly(), Inserters = Array.AsReadOnly(Inserters.Select(a => new InserterSnapshot(a)).ToArray()), Sockets = Array.AsReadOnly(Sockets.Select(s => new AssemblySocketSnapshot(s)).ToArray()), Events = Array.AsReadOnly(events.ToArray()), Products = Array.AsReadOnly(active.Select(p => new ProductSnapshot(p)).ToArray()), LatestCompleted = LatestCompleted == null ? null : new ProductSnapshot(LatestCompleted), GeneratedProducts = Products.Count, ExitedProducts = exitedProducts, ProductSourceBlockedTicks = productSourceBlockedTicks, SegmentPhases = Array.AsReadOnly((long[])segmentPhases.Clone()), SegmentSpeeds = Array.AsReadOnly((int[])segmentSpeeds.Clone()), SupplyPhases = Array.AsReadOnly((long[])supplyPhases.Clone()) };
        }
    }
}
