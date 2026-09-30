using System;
using System.Linq;
using AssemblyZero.Domain;
using NUnit.Framework;

namespace AssemblyZero.Tests
{
    internal static class EP02TestFactory
    {
        public static AssemblyScenarioDefinition Config(bool full = false, bool source = false)
        {
            var n = full ? 6 : 1;
            var config = new AssemblyScenarioDefinition { BeltSpeed = 8, MinimumSpacing = 64, SourceEnabled = source, SourceInterval = 9, RejectInterval = 10, ScanTicks = 3, ShowcaseTicks = 5, FullRobotManifest = full };
            config.SourceParts = Enumerable.Range(2, n).Select(x => new PartTypeId(x)).ToArray();
            config.Manifest = new AssemblyManifest { RequiredParts = (PartTypeId[])config.SourceParts.Clone() };
            config.Sockets = Enumerable.Range(0, n).Select(i => new AssemblySocketData { Id = new AssemblySocketId(i), AcceptedPart = new PartTypeId(i + 2) }).ToArray();
            config.Inserters = Enumerable.Range(0, n).Select(i => new InserterData { Id = new InserterId(i), AllowedPart = new PartTypeId(i + 2), TargetSocket = new AssemblySocketId(i), PickingTicks = 2, CarryingTicks = 2, PlacingTicks = 2, ReturningTicks = 2, Port = new BeltAccessPort(new LineId(0), 3, 0, 4096, 512, 1UL << (i + 2)) }).ToArray();
            return config;
        }
        public static EP02Simulation World(AssemblyScenarioDefinition config, BackendKind backend = BackendKind.Gap) => new EP02Simulation(config, BeltTopology.CreateStraight(2, 8192), backend);
        public static void Run(EP02Simulation world, int ticks) { for (var i = 0; i < ticks; i++) { world.Tick(); Assert.IsTrue(world.Statistics.Conserved, "tick " + world.TickNumber); } }
    }
    public sealed class AssemblyOwnershipTests
    {
        [Test]
        public void CompetingInsertersReserveOneItemInStableIdOrder()
        {
            var c = EP02TestFactory.Config(); var first = c.Inserters[0]; var second = first.Clone(); second.Id = new InserterId(4); c.Inserters = new[] { second, first }; c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) };
            var w = EP02TestFactory.World(c); w.Tick(); Assert.AreEqual(1, w.Statistics.Reserved); Assert.AreEqual(1, w.Snapshot.Inserters[0].ReservedItem); Assert.AreEqual(0, w.Snapshot.Inserters[1].ReservedItem); Assert.AreEqual(1, w.Snapshot.Inserters[1].IntentItem);
        }
        [Test]
        public void LockedTargetNeverRemovesAnItem()
        {
            var c = EP02TestFactory.Config(); c.Sockets[0].Locked = true; c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) };
            var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 40); Assert.AreEqual(1, w.Statistics.OnBelt); Assert.AreEqual(0, w.Statistics.Held); Assert.AreEqual(InserterState.WaitingForTarget, w.Snapshot.Inserters[0].State);
        }
        [Test]
        public void OnBeltHeldSocketTransitionsConserveIdentity()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) }; c.ScanTicks = 100;
            var w = EP02TestFactory.World(c); var held = false; var installed = false;
            for (var i = 0; i < 20; i++) { w.Tick(); Assert.AreEqual(1, w.Statistics.Generated); Assert.IsTrue(w.Statistics.Conserved); held |= w.Statistics.Held == 1; installed |= w.Statistics.Installed == 1; Assert.AreEqual(1, w.Snapshot.Items.Single().ItemId); }
            Assert.IsTrue(held); Assert.IsTrue(installed);
        }
        [Test]
        public void OccupiedTargetDoesNotTakeSecondPart()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512), new InitialAssemblyPart(2, 0, 384) }; c.ShowcaseTicks = 1000;
            var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 30); Assert.AreEqual(1, w.Statistics.OnBelt); Assert.AreEqual(0, w.Statistics.Held); Assert.AreEqual(1, w.Snapshot.Inserters[0].Transfers);
        }
        [Test]
        public void DisableReleasesItemAndSocketReservation()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) }; var w = EP02TestFactory.World(c); w.Tick();
            w.EnqueueCommand(new AssemblyCommand(2, 0, AssemblyCommandKind.SetInserterEnabled, 0, false)); w.Tick(); Assert.AreEqual(0, w.Statistics.Reserved); Assert.AreEqual(1, w.Statistics.OnBelt); Assert.AreEqual(-1, w.Snapshot.Sockets[0].ReservedBy);
        }
        [Test]
        public void DisableHeldPartRejectsWithoutLoss()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) }; var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 4); Assert.AreEqual(1, w.Statistics.Held);
            w.EnqueueCommand(new AssemblyCommand(5, 0, AssemblyCommandKind.SetInserterEnabled, 0, false)); w.Tick(); Assert.AreEqual(1, w.Statistics.Rejected); Assert.AreEqual(0, w.Statistics.Held); Assert.IsTrue(w.Statistics.Conserved);
            Assert.IsTrue(w.TryGetItemRecord(1, out var record)); Assert.AreEqual(AssemblyRejectReason.InserterDisabledWhileHolding, record.RejectReason);
        }
        [Test]
        public void SixUniquePartsCompleteExactlyOneRobot()
        {
            var c = EP02TestFactory.Config(true); c.InitialParts = Enumerable.Range(0, 6).Select(i => new InitialAssemblyPart(i + 2, i % 2, 512 + 128 * i)).ToArray(); var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 100);
            Assert.AreEqual(1, w.Statistics.CompletedRobots); Assert.AreEqual(6, w.Statistics.CompletedConsumed); Assert.AreEqual(0, w.Statistics.Installed); Assert.AreEqual(1, w.Snapshot.Events.Count(e => e.Kind == AssemblyEventKind.Complete));
        }
        [Test]
        public void DuplicatePartsCannotReplaceMissingPart()
        {
            var c = EP02TestFactory.Config(true); c.InitialParts = Enumerable.Range(0, 6).Select(i => new InitialAssemblyPart(i == 5 ? 2 : i + 2, i % 2, 512 + 128 * i)).ToArray(); var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 200);
            Assert.AreEqual(0, w.Statistics.CompletedRobots); Assert.AreEqual(5, w.Statistics.Installed);
        }
        [Test]
        public void ShortPickupPortFailsBeforeStarting()
        {
            var c = EP02TestFactory.Config(); c.Inserters[0].Port = new BeltAccessPort(new LineId(0), 3, 0, 8, 0, 4);
            Assert.Throws<ArgumentException>(() => EP02TestFactory.World(c));
        }
    }
}
