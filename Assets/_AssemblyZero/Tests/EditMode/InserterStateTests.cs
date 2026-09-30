using AssemblyZero.Domain;
using NUnit.Framework;

namespace AssemblyZero.Tests
{
    public sealed class InserterStateTests
    {
        [Test]
        public void StateChangesOnlyAtConfiguredTicks()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) }; var w = EP02TestFactory.World(c);
            var expected = new[] { InserterState.Reserving, InserterState.Picking, InserterState.Picking, InserterState.Carrying, InserterState.Carrying, InserterState.Placing, InserterState.Placing, InserterState.Returning, InserterState.Returning, InserterState.WaitingForItem };
            foreach (var state in expected) { w.Tick(); Assert.AreEqual(state, w.Snapshot.Inserters[0].State, "tick " + w.TickNumber); }
        }
        [Test]
        public void HeldPartWaitsForLockedTargetAndResumes()
        {
            var c = EP02TestFactory.Config(); c.InitialParts = new[] { new InitialAssemblyPart(2, 0, 512) }; var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 4);
            w.EnqueueCommand(new AssemblyCommand(5, 0, AssemblyCommandKind.SetSocketLocked, 0, true)); EP02TestFactory.Run(w, 10); Assert.AreEqual(1, w.Statistics.Held); Assert.AreEqual(InserterState.WaitingForTarget, w.Snapshot.Inserters[0].State);
            w.EnqueueCommand(new AssemblyCommand(16, 0, AssemblyCommandKind.SetSocketLocked, 0, false)); EP02TestFactory.Run(w, 4); Assert.AreEqual(1, w.Statistics.Installed);
        }
    }
}
