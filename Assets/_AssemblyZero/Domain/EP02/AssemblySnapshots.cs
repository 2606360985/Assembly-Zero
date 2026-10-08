using System;
using System.Collections.Generic;

namespace AssemblyZero.Domain
{
    public readonly struct AssemblyItemSnapshot
    {
        public readonly int ItemId, OwnerId, Lane, Distance, ProductId;
        public readonly PartTypeId Part;
        public readonly ItemOwnership Ownership;
        public readonly AssemblyRejectReason RejectReason;
        public AssemblyItemSnapshot(AssemblyItemData item, int lane, int distance)
        { ItemId = item.ItemId; OwnerId = item.OwnerId; ProductId = item.ProductId; Part = item.Part; Ownership = item.Ownership; Lane = lane; Distance = distance; RejectReason = item.RejectReason; }
    }
    public readonly struct InserterSnapshot
    {
        public readonly int Id, Part, TargetSocket, ReservedItem, HeldItem, IntentItem, Duration, PickupLane, PickupDistance, StationId, TargetProductId;
        public readonly InserterState State;
        public readonly long StateEnterTick, Transfers;
        public readonly int LastIntentItem;
        public readonly long LastIntentTick;
        public readonly double BusyPercent, StarvedPercent, BlockedPercent;
        public InserterSnapshot(InserterData arm)
        {
            Id = arm.Id.Value; Part = arm.AllowedPart.Value; TargetSocket = arm.TargetSocket.Value; State = arm.State; StateEnterTick = arm.StateEnterTick; Duration = arm.Duration;
            StationId = arm.StationId; TargetProductId = arm.TargetProductId;
            ReservedItem = arm.ReservedItem; HeldItem = arm.HeldItem; IntentItem = arm.IntentItem; Transfers = arm.Transfers; PickupLane = arm.PickupLane; PickupDistance = arm.PickupDistance;
            LastIntentItem = arm.LastIntentItem; LastIntentTick = arm.LastIntentTick;
            var denominator = Math.Max(1, arm.EnabledTicks); BusyPercent = 100.0 * arm.BusyTicks / denominator; StarvedPercent = 100.0 * arm.StarvedTicks / denominator; BlockedPercent = 100.0 * arm.BlockedTicks / denominator;
        }
        public double Progress(long tick, double fraction = 0) => Math.Max(0, Math.Min(1, (tick - StateEnterTick + fraction) / Math.Max(1, Duration)));
    }
    public readonly struct AssemblySocketSnapshot
    {
        public readonly int Id, Part, ItemId, ReservedBy;
        public readonly bool Locked;
        public AssemblySocketSnapshot(AssemblySocketData socket) { Id = socket.Id.Value; Part = socket.AcceptedPart.Value; ItemId = socket.ItemId; ReservedBy = socket.ReservedBy; Locked = socket.Locked; }
    }
    public readonly struct AssemblyStatistics
    {
        public readonly long Tick;
        public readonly int Generated, OnBelt, Reserved, Held, Installed, Rejected, CompletedConsumed, CompletedRobots, BeltValuesTouched, SourceBlockedTicks;
        public readonly double AverageAssemblySeconds;
        public readonly ulong StateHash;
        public bool Conserved => Generated == OnBelt + Reserved + Held + Installed + Rejected + CompletedConsumed;
        public AssemblyStatistics(long tick, int generated, int onBelt, int reserved, int held, int installed, int rejected, int consumed, int robots, int touched, int sourceBlocked, double average, ulong hash)
        { Tick = tick; Generated = generated; OnBelt = onBelt; Reserved = reserved; Held = held; Installed = installed; Rejected = rejected; CompletedConsumed = consumed; CompletedRobots = robots; BeltValuesTouched = touched; SourceBlockedTicks = sourceBlocked; AverageAssemblySeconds = average; StateHash = hash; }
    }
    public sealed class AssemblySnapshot
    {
        public long Tick { get; internal set; }
        public AssemblyCellState CellState { get; internal set; }
        public long CellStateEnterTick { get; internal set; }
        public AssemblyStatistics Statistics { get; internal set; }
        public IReadOnlyList<AssemblyItemSnapshot> Items { get; internal set; } = Array.Empty<AssemblyItemSnapshot>();
        public IReadOnlyList<InserterSnapshot> Inserters { get; internal set; } = Array.Empty<InserterSnapshot>();
        public IReadOnlyList<AssemblySocketSnapshot> Sockets { get; internal set; } = Array.Empty<AssemblySocketSnapshot>();
        public IReadOnlyList<AssemblyEvent> Events { get; internal set; } = Array.Empty<AssemblyEvent>();
        public IReadOnlyList<ProductSnapshot> Products { get; internal set; } = Array.Empty<ProductSnapshot>();
        public IReadOnlyList<long> SegmentPhases { get; internal set; } = Array.Empty<long>();
        public IReadOnlyList<int> SegmentSpeeds { get; internal set; } = Array.Empty<int>();
        public IReadOnlyList<long> SupplyPhases { get; internal set; } = Array.Empty<long>();
        public int GeneratedProducts { get; internal set; }
        public int ExitedProducts { get; internal set; }
        public int ProductSourceBlockedTicks { get; internal set; }
        public bool ProductsConserved => GeneratedProducts == Products.Count + ExitedProducts;
        public ProductSnapshot LatestCompleted { get; internal set; }
    }
    public sealed class ProductSnapshot
    {
        public int Id { get; internal set; }
        public int Distance { get; internal set; }
        public int Segment { get; internal set; }
        public int NextStation { get; internal set; }
        public ProductState State { get; internal set; }
        public bool Completed { get; internal set; }
        public long CompletedTick { get; internal set; }
        public IReadOnlyList<AssemblySocketSnapshot> Sockets { get; internal set; }
        internal ProductSnapshot(ProductData p)
        {
            Id = p.Id.Value; Distance = p.Distance; Segment = p.Segment; NextStation = p.NextStation; State = p.State; Completed = p.Completed; CompletedTick = p.CompletedTick;
            var sockets = new AssemblySocketSnapshot[p.Sockets.Length]; for (var i = 0; i < sockets.Length; i++) sockets[i] = new AssemblySocketSnapshot(p.Sockets[i]); Sockets = Array.AsReadOnly(sockets);
        }
    }
}
