using System.Linq;
namespace AssemblyZero.Domain
{
    public sealed class AssemblyCellSystem
    {
        public void Tick(EP02Simulation w)
        {
            foreach (var p in w.Products)
            {
                if (p.Exited || p.Completed || p.NextStation >= w.Config.Stations.Length) continue;
                var s = w.Config.Stations[p.NextStation]; if (p.Distance != s.Distance) continue;
                var socket = p.Sockets.Single(x => x.Id.Value == s.SocketId);
                var arms = w.Inserters.Where(a => a.StationId == s.Id).ToArray();
                if (socket.ItemId == 0) { p.State = socket.Locked ? ProductState.Locked : arms.All(a => !a.Enabled) ? ProductState.Disabled : arms.Any(a => a.HeldItem != 0 || a.ReservedItem != 0) ? ProductState.Assembling : ProductState.WaitingForPart; continue; }
                if (arms.Any(a => a.HeldItem != 0 || a.ReservedItem != 0 || a.State == InserterState.Returning)) { p.State = ProductState.Assembling; continue; }
                if (p.NextStation == w.Config.Stations.Length - 1)
                {
                    if (p.ScanEnterTick < 0) { p.ScanEnterTick = w.TickNumber; w.Emit(AssemblyEventKind.Scan, s.Id); }
                    p.State = ProductState.Scanning; if (w.TickNumber - p.ScanEnterTick < w.Config.ScanTicks) continue;
                    p.Completed = true; p.CompletedTick = w.TickNumber; p.State = ProductState.Completed;
                    foreach (var t in p.Sockets) { var item = w.Item(t.ItemId); item.Ownership = ItemOwnership.CompletedProduct; }
                    if (w.Config.FullRobotManifest) { w.Cell.CompletedRobotCount++; w.Cell.TotalAssemblyTicks += w.TickNumber - p.GeneratedTick; w.Emit(AssemblyEventKind.Complete, s.Id, p.Id.Value); }
                    w.LatestCompleted = p;
                }
                p.NextStation++; foreach (var a in arms) a.TargetProductId = 0;
            }
        }
    }
}
