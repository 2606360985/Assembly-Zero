using System;

namespace AssemblyZero.Domain
{
    public sealed class InserterSystem
    {
        internal static void Enter(InserterData arm, InserterState state, long tick) { arm.State = state; arm.StateEnterTick = tick; }
        public void Tick(EP02Simulation world, InserterData arm)
        {
            if (!arm.Enabled) { EnterIfDifferent(arm, InserterState.Disabled, world.TickNumber); return; }
            var target = world.Socket(arm.TargetSocket.Value); var elapsed = world.TickNumber - arm.StateEnterTick;
            if (arm.HeldItem == 0 && arm.ReservedItem != 0 && (!world.Access.TryValidate(world.Item(arm.ReservedItem).Handle, out var check) || check.Distance > arm.Port.EndDistance || target.Locked || target.ItemId != 0))
            { world.Reservations.Release(world, arm); Enter(arm, target.Locked || target.ItemId != 0 ? InserterState.WaitingForTarget : InserterState.WaitingForItem, world.TickNumber); return; }
            if (arm.HeldItem != 0 && target.Locked && arm.State != InserterState.WaitingForTarget)
            { Enter(arm, InserterState.WaitingForTarget, world.TickNumber); return; }
            switch (arm.State)
            {
                case InserterState.WaitingForItem:
                case InserterState.WaitingForTarget:
                    if (arm.HeldItem != 0 && !target.Locked && target.ItemId == 0) Enter(arm, InserterState.Placing, world.TickNumber);
                    else if (arm.HeldItem == 0 && arm.ReservedItem != 0) Enter(arm, InserterState.Reserving, world.TickNumber);
                    else if (target.Locked || target.ItemId != 0 || world.Cell.State != AssemblyCellState.Filling || (target.ReservedBy >= 0 && target.ReservedBy != arm.Id.Value)) EnterIfDifferent(arm, InserterState.WaitingForTarget, world.TickNumber);
                    else EnterIfDifferent(arm, InserterState.WaitingForItem, world.TickNumber);
                    break;
                case InserterState.Reserving:
                    if (elapsed >= 1) Enter(arm, InserterState.Picking, world.TickNumber);
                    break;
                case InserterState.Picking:
                    if (elapsed < arm.PickingTicks) break;
                    var item = world.Item(arm.ReservedItem);
                    if (!target.Locked && target.ItemId == 0 && item != null && world.Access.TryValidate(item.Handle, out var candidate) && candidate.Distance >= arm.Port.StartDistance && candidate.Distance <= arm.Port.EndDistance && world.Access.TryTake(item.Handle, out candidate))
                    {
                        arm.PickupDistance = candidate.Distance; arm.PickupLane = candidate.Handle.LaneId.Value;
                        item.Ownership = ItemOwnership.HeldByInserter; arm.HeldItem = item.ItemId; arm.ReservedItem = 0;
                        Enter(arm, InserterState.Carrying, world.TickNumber); world.Emit(AssemblyEventKind.Pick, arm.Id.Value, item.ItemId);
                    }
                    else { world.Reservations.Release(world, arm); Enter(arm, InserterState.WaitingForItem, world.TickNumber); }
                    break;
                case InserterState.Carrying:
                    if (elapsed >= arm.CarryingTicks) Enter(arm, InserterState.Placing, world.TickNumber);
                    break;
                case InserterState.Placing:
                    if (elapsed < arm.PlacingTicks) break;
                    if (target.Locked || target.ItemId != 0) { Enter(arm, InserterState.WaitingForTarget, world.TickNumber); break; }
                    var held = world.Item(arm.HeldItem);
                    if (held == null || held.Part.Value != target.AcceptedPart.Value || target.ReservedBy != arm.Id.Value) throw new InvalidOperationException("Invalid atomic socket placement.");
                    held.Ownership = ItemOwnership.InAssemblySocket; held.OwnerId = target.Id.Value; target.ItemId = held.ItemId; target.ReservedBy = -1; arm.HeldItem = 0; arm.Transfers++;
                    if (world.Cell.FirstInstallTick < 0) world.Cell.FirstInstallTick = world.TickNumber;
                    Enter(arm, InserterState.Returning, world.TickNumber); world.Emit(AssemblyEventKind.Lock, arm.Id.Value, held.ItemId);
                    break;
                case InserterState.Returning:
                    if (elapsed >= arm.ReturningTicks) Enter(arm, InserterState.WaitingForItem, world.TickNumber);
                    break;
                case InserterState.Disabled:
                    Enter(arm, InserterState.WaitingForItem, world.TickNumber);
                    break;
            }
        }
        private static void EnterIfDifferent(InserterData arm, InserterState state, long tick) { if (arm.State != state) Enter(arm, state, tick); }
    }
}
