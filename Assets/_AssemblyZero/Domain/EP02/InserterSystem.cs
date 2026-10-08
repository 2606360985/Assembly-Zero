using System;
namespace AssemblyZero.Domain
{
    public sealed class InserterSystem
    {
        internal static void Enter(InserterData a, InserterState s, long t) { a.State = s; a.StateEnterTick = t; }
        public void Tick(EP02Simulation w, InserterData a)
        {
            if (!a.Enabled) { Change(a, InserterState.Disabled, w.TickNumber); return; }
            var target = w.Target(a); var elapsed = w.TickNumber - a.StateEnterTick;
            if (a.State == InserterState.Returning) { if (elapsed >= a.ReturningTicks) Enter(a, InserterState.WaitingForItem, w.TickNumber); return; }
            if (target == null) { Change(a, InserterState.WaitingForTarget, w.TickNumber); return; }
            if (a.ReservedItem != 0 && (!w.Access.TryValidate(w.Item(a.ReservedItem).Handle, out var check) || !w.InPort(a, check) || target.Locked || target.ItemId != 0))
            { w.Reservations.Release(w, a); Enter(a, InserterState.WaitingForItem, w.TickNumber); return; }
            if (a.HeldItem != 0 && target.Locked) { Change(a, InserterState.WaitingForTarget, w.TickNumber); return; }
            switch (a.State)
            {
                case InserterState.WaitingForItem: case InserterState.WaitingForTarget: case InserterState.Disabled:
                    if (a.HeldItem != 0 && !target.Locked) Enter(a, InserterState.Placing, w.TickNumber);
                    else if (a.ReservedItem != 0) Enter(a, InserterState.Reserving, w.TickNumber);
                    else Change(a, target.Locked || target.ItemId != 0 ? InserterState.WaitingForTarget : InserterState.WaitingForItem, w.TickNumber);
                    break;
                case InserterState.Reserving: if (elapsed >= 1) Enter(a, InserterState.Picking, w.TickNumber); break;
                case InserterState.Picking:
                    if (elapsed < a.PickingTicks) break;
                    var item = w.Item(a.ReservedItem);
                    if (item != null && w.Access.TryValidate(item.Handle, out var candidate) && w.InPort(a, candidate) && w.Access.TryTake(item.Handle, out candidate))
                    {
                        a.PickupDistance = candidate.Distance; a.PickupLane = candidate.Handle.LaneId.Value;
                        item.Ownership = ItemOwnership.HeldByInserter; a.HeldItem = item.ItemId; a.ReservedItem = 0;
                        Enter(a, InserterState.Carrying, w.TickNumber); w.Emit(AssemblyEventKind.Pick, a.Id.Value, item.ItemId);
                    }
                    else { w.Reservations.Release(w, a); Enter(a, InserterState.WaitingForItem, w.TickNumber); }
                    break;
                case InserterState.Carrying: if (elapsed >= a.CarryingTicks) Enter(a, InserterState.Placing, w.TickNumber); break;
                case InserterState.Placing:
                    if (elapsed < a.PlacingTicks) break;
                    if (target.ItemId != 0) throw new InvalidOperationException("Target product already owns a part.");
                    var held = w.Item(a.HeldItem);
                    if (held == null || held.Part.Value != target.AcceptedPart.Value || held.ProductId != a.TargetProductId || target.ReservedBy != a.Id.Value) throw new InvalidOperationException("Invalid product placement.");
                    held.Ownership = ItemOwnership.InAssemblySocket; held.OwnerId = target.Id.Value; target.ItemId = held.ItemId; target.ReservedBy = -1; a.HeldItem = 0; a.Transfers++;
                    Enter(a, InserterState.Returning, w.TickNumber); w.Emit(AssemblyEventKind.Lock, a.Id.Value, held.ItemId); break;
            }
        }
        private static void Change(InserterData a, InserterState s, long tick) { if (a.State != s) Enter(a, s, tick); }
    }
}
