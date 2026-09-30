using System.Collections.Generic;
using AssemblyZero.Domain;
using NUnit.Framework;

namespace AssemblyZero.Tests
{
    public sealed class TransactionalBeltAccessTests
    {
        private static IBeltSimulationBackend Backend(bool gap) => gap ? (IBeltSimulationBackend)new GapTransportLineSimulation() : new NaiveItemBeltSimulation();
        [TestCase(false)] [TestCase(true)]
        public void LaneTypeFilteringNearestOrderingAndStaleHandles(bool gap)
        {
            var b = Backend(gap); var scenario = new ScenarioDefinition { ExternalLogistics = true, MinimumSpacing = 32 }; var topology = BeltTopology.CreateStraight(2, 4096); b.Reset(scenario, topology); var a = (ITransactionalBeltAccess)b;
            Assert.IsTrue(a.TryInsertPart(new BeltAccessPort(new LaneId(0), 500, 500), new PartTypeId(2), out var h1));
            Assert.IsTrue(a.TryInsertPart(new BeltAccessPort(new LaneId(1), 450, 450), new PartTypeId(3), out _));
            Assert.IsTrue(a.TryInsertPart(new BeltAccessPort(new LaneId(1), 524, 524), new PartTypeId(2), out var h2));
            var candidates = new List<BeltAccessCandidate>(); a.QueryCandidates(new BeltAccessPort(new LineId(0), 3, 400, 600, 512, 4), candidates);
            Assert.AreEqual(2, candidates.Count); Assert.AreEqual(h1.ItemId, candidates[0].Handle.ItemId); Assert.AreEqual(h2.ItemId, candidates[1].Handle.ItemId);
            a.QueryCandidates(new BeltAccessPort(new LineId(0), 1, 400, 600, 512, 4), candidates); Assert.AreEqual(1, candidates.Count);
            b.Reset(scenario, topology); a.TryInsertPart(new BeltAccessPort(new LaneId(0), 500, 500), new PartTypeId(2), out _); Assert.IsFalse(a.TryValidate(h1, out _)); Assert.IsFalse(a.TryTake(h1, out _));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void GapHeadMiddleTailRemovalPreservesOtherPositions(int removeIndex)
        {
            var scenario = new ScenarioDefinition { ExternalLogistics = true, MinimumSpacing = 64, SpeedUnitsPerTick = 8 }; var topology = BeltTopology.CreateStraight(1, 4096);
            var naive = Backend(false); var gap = Backend(true); naive.Reset(scenario, topology); gap.Reset(scenario, topology); var a = (ITransactionalBeltAccess)naive; var b = (ITransactionalBeltAccess)gap;
            var handlesA = new BeltItemHandle[3]; var handlesB = new BeltItemHandle[3];
            for (var i = 0; i < 3; i++) { var p = new BeltAccessPort(new LaneId(0), 512 - i * 128, 512 - i * 128); a.TryInsertPart(p, new PartTypeId(2), out handlesA[i]); b.TryInsertPart(p, new PartTypeId(2), out handlesB[i]); }
            a.TransportTick(1); b.TransportTick(1); Assert.IsTrue(a.TryTake(handlesA[removeIndex], out _)); Assert.IsTrue(b.TryTake(handlesB[removeIndex], out _)); Assert.IsFalse(b.TryTake(handlesB[removeIndex], out _));
            for (var tick = 2; tick < 300; tick++) { a.TransportTick(tick); b.TransportTick(tick); Assert.AreEqual(naive.CalculateStateHash(), gap.CalculateStateHash()); }
            var snapshot = new BeltSnapshotWriter(); gap.CreateSnapshot(snapshot); Assert.AreEqual(2, snapshot.Items.Count); if (removeIndex == 1) Assert.AreEqual(192, snapshot.Items[1].GapAhead);
        }
    }
}
