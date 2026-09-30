using AssemblyZero.Domain;
using NUnit.Framework;

namespace AssemblyZero.Tests
{
    public sealed class AssemblyDeterminismTests
    {
        [Test]
        public void TwoBackendsAndIndependentRunsMatchForTenThousandTicks()
        {
            var c = EP02TestFactory.Config(true, true); var a = EP02TestFactory.World(c, BackendKind.Naive); var b = EP02TestFactory.World(c); var repeat = EP02TestFactory.World(c);
            foreach (var world in new[] { a, b, repeat }) { world.EnqueueCommand(new AssemblyCommand(1000, 0, AssemblyCommandKind.SetSocketLocked, 0, true)); world.EnqueueCommand(new AssemblyCommand(2500, 0, AssemblyCommandKind.SetSocketLocked, 0, false)); }
            for (var i = 0; i < 10000; i++) { a.Tick(); b.Tick(); repeat.Tick(); Assert.IsTrue(a.Statistics.Conserved); Assert.AreEqual(a.Statistics.StateHash, b.Statistics.StateHash, "backend tick " + a.TickNumber); Assert.AreEqual(b.Statistics.StateHash, repeat.Statistics.StateHash); }
            Assert.Greater(a.Statistics.CompletedRobots, 2); Assert.Greater(a.Statistics.Rejected, 0);
        }
        [Test]
        public void RenderCadenceAndConfigurationMutationCannotChangeSimulation()
        {
            var c = EP02TestFactory.Config(true, true); var a = EP02TestFactory.World(c); var b = EP02TestFactory.World(c); c.SourceInterval = 9999; c.Inserters[0].PickingTicks = 9999;
            EP02TestFactory.Run(a, 1000); for (var frame = 0; frame < 250; frame++) for (var i = 0; i < 4; i++) b.Tick(); Assert.AreEqual(a.Statistics.StateHash, b.Statistics.StateHash);
        }
        [Test]
        public void ReservationsAndPendingCommandsAreInHash()
        {
            var c = EP02TestFactory.Config(); var a = EP02TestFactory.World(c); var b = EP02TestFactory.World(c); b.EnqueueCommand(new AssemblyCommand(10, 0, AssemblyCommandKind.SetSocketLocked, 0, true)); Assert.AreNotEqual(a.CalculateStateHash(), b.CalculateStateHash());
        }
    }
}
