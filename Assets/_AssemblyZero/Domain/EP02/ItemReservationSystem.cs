namespace AssemblyZero.Domain
{
    public sealed class ItemReservationSystem
    {
        public bool TryReserve(EP02Simulation world, InserterData arm, in BeltAccessCandidate candidate)
        {
            var target = world.Target(arm); var item = world.Item(candidate.Handle.ItemId.Value);
            if (target == null || item == null || item.Ownership != ItemOwnership.OnBelt || target.Locked || target.ItemId != 0 || target.ReservedBy >= 0 || target.AcceptedPart.Value != item.Part.Value) return false;
            item.Ownership = ItemOwnership.ReservedOnBelt; item.OwnerId = arm.Id.Value; item.ProductId = arm.TargetProductId; arm.ReservedItem = item.ItemId; target.ReservedBy = arm.Id.Value; return true;
        }
        public void Release(EP02Simulation world, InserterData arm)
        {
            if (arm.ReservedItem != 0) { var item = world.Item(arm.ReservedItem); if (item != null && item.Ownership == ItemOwnership.ReservedOnBelt) { item.Ownership = ItemOwnership.OnBelt; item.OwnerId = -1; item.ProductId = 0; } }
            arm.ReservedItem = 0; var target = world.Target(arm); if (target != null && target.ReservedBy == arm.Id.Value) target.ReservedBy = -1;
        }
    }
}
