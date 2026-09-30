using System;

namespace AssemblyZero.Domain
{
    public readonly struct PartTypeId : IEquatable<PartTypeId>
    {
        public readonly int Value;
        public PartTypeId(int value) { Value = value; }
        public ulong Mask => Value >= 0 && Value < 64 ? 1UL << Value : 0;
        public bool Equals(PartTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PartTypeId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
    public readonly struct InserterId { public readonly int Value; public InserterId(int value) { Value = value; } }
    public readonly struct AssemblySocketId { public readonly int Value; public AssemblySocketId(int value) { Value = value; } }
    public enum ItemOwnership : byte { OnBelt, ReservedOnBelt, HeldByInserter, InAssemblySocket, InRejectSink, CompletedProduct }
    public enum InserterState : byte { WaitingForItem, Reserving, Picking, Carrying, Placing, Returning, WaitingForTarget, Disabled }
    public enum AssemblyCellState : byte { Filling, AwaitingSafe, Scanning, Showcase, Clearing }
    public enum AssemblyEventKind : byte { Pick, Lock, Scan, Complete, Reject, Alarm }
    public enum AssemblyCommandKind : byte { SetSocketLocked, SetInserterEnabled }
    public enum AssemblyRejectReason : byte { None, MissedPickupAtLineEnd, InserterDisabledWhileHolding }

    public readonly struct BeltAccessCandidate
    {
        public readonly BeltItemHandle Handle;
        public readonly PartTypeId Part;
        public readonly int Distance;
        public BeltAccessCandidate(BeltItemHandle handle, PartTypeId part, int distance) { Handle = handle; Part = part; Distance = distance; }
    }
    public sealed class AssemblyItemData
    {
        public int ItemId;
        public PartTypeId Part;
        public ItemOwnership Ownership;
        public int OwnerId = -1;
        public long GeneratedTick;
        public BeltItemHandle Handle;
        public AssemblyRejectReason RejectReason;
    }
    public sealed class InserterData
    {
        public InserterId Id;
        public PartTypeId AllowedPart;
        public AssemblySocketId TargetSocket;
        public BeltAccessPort Port;
        public InserterState State;
        public long StateEnterTick;
        public int ReservedItem, HeldItem, IntentItem;
        public int LastIntentItem;
        public long LastIntentTick;
        public bool Enabled = true;
        public int PhaseTicks, PickingTicks = 12, CarryingTicks = 24, PlacingTicks = 12, ReturningTicks = 24;
        public int PickupDistance, PickupLane;
        public long EnabledTicks, BusyTicks, StarvedTicks, BlockedTicks, Transfers;
        public InserterData Clone() => (InserterData)MemberwiseClone();
        public int Duration => State == InserterState.Reserving ? 1 : State == InserterState.Picking ? PickingTicks : State == InserterState.Carrying ? CarryingTicks : State == InserterState.Placing ? PlacingTicks : State == InserterState.Returning ? ReturningTicks : 1;
    }
    public sealed class AssemblySocketData
    {
        public AssemblySocketId Id;
        public PartTypeId AcceptedPart;
        public int ItemId, ReservedBy = -1;
        public bool Locked;
        public AssemblySocketData Clone() => (AssemblySocketData)MemberwiseClone();
    }
    public sealed class AssemblyManifest
    {
        public PartTypeId[] RequiredParts = Array.Empty<PartTypeId>();
    }
    public sealed class AssemblyCellData
    {
        public AssemblyCellState State;
        public long StateEnterTick, FirstInstallTick = -1;
        public int CompletedRobotCount;
        public long TotalAssemblyTicks;
    }
    public readonly struct AssemblyCommand
    {
        public readonly long Tick;
        public readonly int Sequence, Target;
        public readonly AssemblyCommandKind Kind;
        public readonly bool Value;
        public AssemblyCommand(long tick, int sequence, AssemblyCommandKind kind, int target, bool value)
        { Tick = tick; Sequence = sequence; Kind = kind; Target = target; Value = value; }
    }
    public readonly struct AssemblyEvent
    {
        public readonly long Tick;
        public readonly AssemblyEventKind Kind;
        public readonly int ActorId, ItemId;
        public AssemblyEvent(long tick, AssemblyEventKind kind, int actorId, int itemId = 0) { Tick = tick; Kind = kind; ActorId = actorId; ItemId = itemId; }
    }
}
