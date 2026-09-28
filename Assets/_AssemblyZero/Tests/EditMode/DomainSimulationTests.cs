using AssemblyZero.Domain;
using NUnit.Framework;
using System.Diagnostics;

namespace AssemblyZero.Tests
{
    public sealed class DomainSimulationTests
    {
        private static ScenarioDefinition Scenario(int max = 200) => new ScenarioDefinition { SpeedUnitsPerTick = 8, MinimumSpacing = 16, SpawnIntervalTicks = 2, MaxItems = max, OrderTarget = max, GateCloseTick = 30, GateOpenTick = 90 };

        [TestCase(false)] [TestCase(true)]
        public void ItemsKeepOrderAndMinimumSpacing(bool gap)
        {
            IBeltSimulationBackend sim = gap ? new GapTransportLineSimulation() : new NaiveItemBeltSimulation(); sim.Reset(Scenario(), BeltTopology.CreateStraight(1, 1024));
            var writer = new BeltSnapshotWriter(); for (var t = 1; t <= 300; t++) sim.Tick(new TickInput(t)); sim.CreateSnapshot(writer);
            for (var i = 1; i < writer.Items.Count; i++) { Assert.Greater(writer.Items[i - 1].ItemId.Value, 0); Assert.Less(writer.Items[i - 1].Distance - writer.Items[i].Distance, int.MaxValue); Assert.GreaterOrEqual(writer.Items[i - 1].Distance - writer.Items[i].Distance, 16); Assert.Less(writer.Items[i - 1].ItemId.Value, writer.Items[i].ItemId.Value); }
        }

        [Test]
        public void GateCompressionAndRecoveryMatch()
        {
            var a = new NaiveItemBeltSimulation(); var b = new GapTransportLineSimulation(); var scenario = Scenario(); var topology = BeltTopology.CreateStraight(2, 1024); a.Reset(scenario, topology); b.Reset(scenario, topology);
            for (var t = 1; t <= 500; t++) { a.Tick(new TickInput(t)); b.Tick(new TickInput(t)); Assert.AreEqual(a.CalculateStateHash(), b.CalculateStateHash(), $"tick {t}"); }
            Assert.Greater(a.Statistics.SinkCount, 0); Assert.IsTrue(a.Statistics.GateOpen);
        }

        [Test]
        public void DeterministicForTenThousandTicks()
        {
            var topology = BeltTopology.CreateStraight(2, 4096); var scenario = Scenario(500); var a = new GapTransportLineSimulation(); var b = new GapTransportLineSimulation(); a.Reset(scenario, topology); b.Reset(scenario, topology);
            for (var t = 1; t <= 10000; t++) { a.Tick(new TickInput(t)); b.Tick(new TickInput(t)); }
            Assert.AreEqual(a.CalculateStateHash(), b.CalculateStateHash()); Assert.AreEqual(a.Statistics.SourceCount, a.Statistics.LogicalItems + a.Statistics.SinkCount);
        }

        [Test]
        public void OpenGapTouchesLessThanNaive()
        {
            var scenario = Scenario(20000); scenario.InitialItems = 20000; scenario.InitialFrontDistance = 10000; scenario.SpawnIntervalTicks = int.MaxValue; scenario.GateCloseTick = -1; scenario.GateOpenTick = -1; var topology = BeltTopology.CreateStraight(2, 20000);
            var a = new NaiveItemBeltSimulation(); var b = new GapTransportLineSimulation(); a.Reset(scenario, topology); b.Reset(scenario, topology); a.Tick(new TickInput(1)); b.Tick(new TickInput(1)); Assert.Less(b.Statistics.ValuesTouched, a.Statistics.ValuesTouched);
        }

        [Test]
        public void TwentyThousandItemPerformanceRecord()
        {
            var scenario = Scenario(20000); scenario.InitialItems = 20000; scenario.InitialFrontDistance = 10000; scenario.MinimumSpacing = 1; scenario.SpawnIntervalTicks = int.MaxValue; scenario.GateCloseTick = -1; scenario.GateOpenTick = -1; var topology = BeltTopology.CreateStraight(2, 30000);
            foreach (var sim in new IBeltSimulationBackend[] { new NaiveItemBeltSimulation(), new GapTransportLineSimulation() })
            {
                sim.Reset(scenario, topology); var timer = Stopwatch.StartNew(); for (var t = 1; t <= 300; t++) sim.Tick(new TickInput(t)); timer.Stop(); TestContext.Out.WriteLine($"{sim.Name}: {timer.Elapsed.TotalMilliseconds / 300.0:F4} ms/tick, touched={sim.Statistics.ValuesTouched}, items={sim.Statistics.LogicalItems}"); Assert.AreEqual(20000, sim.Statistics.LogicalItems + sim.Statistics.SinkCount);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void BeltAccessCanQueryRemoveAndInsert(bool gap)
        {
            var scenario = Scenario(20); scenario.InitialItems = 4; scenario.InitialFrontDistance = 64; scenario.GateCloseTick = -1; scenario.GateOpenTick = -1; IBeltSimulationBackend backend = gap ? new GapTransportLineSimulation() : new NaiveItemBeltSimulation(); backend.Reset(scenario, BeltTopology.CreateStraight(1, 1024)); var access = (IBeltAccess)backend; var port = new BeltAccessPort(new LaneId(0), 32, 64);
            Assert.IsTrue(access.TryQuery(port, out var original)); Assert.IsTrue(access.TryRemove(original)); Assert.IsFalse(access.TryQuery(new BeltAccessPort(new LaneId(0), 64, 64), out _)); Assert.IsTrue(access.TryInsert(new BeltAccessPort(new LaneId(0), 70, 70), ItemType.ServoCore, out var inserted)); Assert.IsTrue(inserted.IsValid); Assert.IsTrue(access.TryQuery(new BeltAccessPort(new LaneId(0), 70, 70), out var queried)); Assert.AreEqual(inserted.ItemId, queried.ItemId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SingleLaneCanAlternateCargoTypes(bool gap)
        {
            var scenario = Scenario(6); scenario.InitialItems = 6; scenario.InitialFrontDistance = 96; scenario.SpawnIntervalTicks = int.MaxValue; scenario.GateCloseTick = -1; scenario.GateOpenTick = -1; scenario.AlternateItemTypes = true;
            IBeltSimulationBackend backend = gap ? new GapTransportLineSimulation() : new NaiveItemBeltSimulation(); backend.Reset(scenario, BeltTopology.CreateStraight(1, 1024));
            var snapshot = new BeltSnapshotWriter(); backend.CreateSnapshot(snapshot);
            Assert.AreEqual(6, snapshot.Items.Count);
            for (var i = 0; i < snapshot.Items.Count; i++) Assert.AreEqual((i & 1) == 0 ? ItemType.ServoCore : ItemType.SensorPack, snapshot.Items[i].ItemType);
        }

        [Test]
        public void StageSixTimingCreatesAndReleasesVisibleBackpressure()
        {
            var scenario = new ScenarioDefinition { MaxItems = 4000, InitialItems = 4000, InitialFrontDistance = 31872, MinimumSpacing = 1, SpeedUnitsPerTick = 8, SpawnIntervalTicks = int.MaxValue, GateCloseTick = 180, GateOpenTick = 480 }; var sim = new GapTransportLineSimulation(); sim.Reset(scenario, BeltTopology.CreateStraight(2, 32000)); var snapshot = new BeltSnapshotWriter();
            for (var t = 1; t <= 260; t++) sim.Tick(new TickInput(t)); sim.CreateSnapshot(snapshot); var compressed = 0; for (var i = 0; i < snapshot.Items.Count; i++) if (snapshot.Items[i].GapAhead == 0) compressed++; Assert.IsFalse(snapshot.Statistics.GateOpen); Assert.Greater(compressed, 100);
            var sinkBeforeOpen = snapshot.Statistics.SinkCount; for (var t = 261; t <= 620; t++) sim.Tick(new TickInput(t)); sim.CreateSnapshot(snapshot); Assert.IsTrue(snapshot.Statistics.GateOpen); Assert.Greater(snapshot.Statistics.SinkCount, sinkBeforeOpen);
        }
    }
}
