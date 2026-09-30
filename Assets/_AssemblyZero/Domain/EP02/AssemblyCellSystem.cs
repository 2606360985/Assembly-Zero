namespace AssemblyZero.Domain
{
    public sealed class AssemblyCellSystem
    {
        public void Tick(EP02Simulation world)
        {
            var cell = world.Cell; var elapsed = world.TickNumber - cell.StateEnterTick;
            switch (cell.State)
            {
                case AssemblyCellState.Filling:
                    var full = true; foreach (var socket in world.Sockets) if (socket.ItemId == 0) full = false;
                    if (full) Enter(world, AssemblyCellState.AwaitingSafe);
                    break;
                case AssemblyCellState.AwaitingSafe:
                    var safe = true; foreach (var arm in world.Inserters) if (arm.HeldItem != 0 || arm.ReservedItem != 0 || arm.State == InserterState.Returning) safe = false;
                    if (safe) { Enter(world, AssemblyCellState.Scanning); world.Emit(AssemblyEventKind.Scan, -1); }
                    break;
                case AssemblyCellState.Scanning:
                    if (elapsed < world.Config.ScanTicks) break;
                    foreach (var socket in world.Sockets) { var item = world.Item(socket.ItemId); item.Ownership = ItemOwnership.CompletedProduct; item.OwnerId = cell.CompletedRobotCount; }
                    // Partial teaching manifests are transfers, never a completed robot.
                    if (world.Config.FullRobotManifest) { cell.CompletedRobotCount++; cell.TotalAssemblyTicks += world.TickNumber - cell.FirstInstallTick; world.Emit(AssemblyEventKind.Complete, -1); }
                    Enter(world, AssemblyCellState.Showcase);
                    break;
                case AssemblyCellState.Showcase:
                    if (elapsed >= world.Config.ShowcaseTicks) Enter(world, AssemblyCellState.Clearing);
                    break;
                case AssemblyCellState.Clearing:
                    foreach (var socket in world.Sockets) { socket.ItemId = 0; socket.ReservedBy = -1; }
                    cell.FirstInstallTick = -1; Enter(world, AssemblyCellState.Filling);
                    break;
            }
        }
        private static void Enter(EP02Simulation world, AssemblyCellState state) { world.Cell.State = state; world.Cell.StateEnterTick = world.TickNumber; }
    }
}
