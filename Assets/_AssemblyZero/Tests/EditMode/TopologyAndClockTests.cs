using System.Collections.Generic;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class TopologyAndClockTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        [TearDown] public void Cleanup() { foreach (var go in created) if (go != null) Object.DestroyImmediate(go); created.Clear(); }

        [Test]
        public void StraightAndCornerLogicalDistanceIsContinuous()
        {
            var pieces = new List<BeltAuthoring>();
            pieces.Add(Piece(0, new Vector2Int(0, 0), GridDirection.East, BeltPieceShape.Straight, true, false));
            pieces.Add(Piece(1, new Vector2Int(1, 0), GridDirection.North, BeltPieceShape.Corner90, false, false));
            pieces.Add(Piece(2, new Vector2Int(1, 1), GridDirection.North, BeltPieceShape.Straight, false, true));
            var baked = BeltTopologyBuilder.Bake(pieces); Assert.AreEqual(0, baked.Issues.Count); Assert.AreEqual(3 * BeltConstants.UnitsPerGrid, baked.DomainTopology.Lanes[0].Length);
            for (var i = 1; i < baked.DomainTopology.PathSamples.Length; i++) Assert.AreEqual(BeltConstants.UnitsPerGrid, baked.DomainTopology.PathSamples[i].Distance - baked.DomainTopology.PathSamples[i - 1].Distance);
        }

        [Test]
        public void CornerWorldSamplingIsContinuous()
        {
            var pieces = new List<BeltAuthoring>();
            pieces.Add(Piece(0, new Vector2Int(0, 0), GridDirection.East, BeltPieceShape.Straight, true, false));
            pieces.Add(Piece(1, new Vector2Int(1, 0), GridDirection.North, BeltPieceShape.Corner90, false, false));
            pieces.Add(Piece(2, new Vector2Int(1, 1), GridDirection.North, BeltPieceShape.Straight, false, true));
            var baked = BeltTopologyBuilder.Bake(pieces); var length = baked.DomainTopology.Lanes[0].Length;
            Assert.AreEqual(1, baked.DomainTopology.Lanes.Length);
            for (var lane = 0; lane < baked.DomainTopology.Lanes.Length; lane++)
            {
                var previous = BeltTopologyBuilder.SampleWorldPose(baked, 0, lane, out var previousTangent);
                for (var d = 8; d <= length; d += 8)
                {
                    var current = BeltTopologyBuilder.SampleWorldPose(baked, d, lane, out var tangent);
                    Assert.Less(Vector3.Distance(previous, current), 0.02f, $"Position jump at lane {lane}, distance {d}.");
                    Assert.Less(Vector3.Angle(previousTangent, tangent), 2f, $"Tangent jump at lane {lane}, distance {d}.");
                    previous = current; previousTangent = tangent;
                }
                BeltTopologyBuilder.SampleWorldPose(baked, length, lane, out var endTangent);
                Assert.Less(Vector3.Angle(Vector3.forward, endTangent), 0.01f);
            }
            var center = BeltTopologyBuilder.SampleWorld(baked, 0, 0);
            Assert.That(center.z, Is.EqualTo(0f).Within(0.0001f), "The single cargo lane must be centered on the belt.");
        }

        [Test]
        public void InvalidConnectionProducesDiagnostic()
        {
            var pieces = new List<BeltAuthoring> { Piece(0, Vector2Int.zero, GridDirection.East, BeltPieceShape.Straight, true, false) };
            Assert.Greater(BeltTopologyBuilder.Bake(pieces).Issues.Count, 0);
        }

        [Test]
        public void RenderFrameCadenceCannotChangeManualTickResult()
        {
            var scenario = new ScenarioDefinition { MaxItems = 1000, SpawnIntervalTicks = 2, MinimumSpacing = 8, SpeedUnitsPerTick = 4 }; var topology = BeltTopology.CreateStraight(2, 8192);
            var a = new NaiveItemBeltSimulation(); var b = new NaiveItemBeltSimulation(); a.Reset(scenario, topology); b.Reset(scenario, topology);
            for (var i = 1; i <= 1000; i++) a.Tick(new TickInput(i));
            for (var frame = 0; frame < 250; frame++) for (var sub = 1; sub <= 4; sub++) { var tick = frame * 4 + sub; b.Tick(new TickInput(tick)); }
            Assert.AreEqual(a.CalculateStateHash(), b.CalculateStateHash());
        }

        private BeltAuthoring Piece(int id, Vector2Int grid, GridDirection direction, BeltPieceShape shape, bool start, bool end) { var go = new GameObject("Piece " + id); created.Add(go); go.transform.position = new Vector3(grid.x, 0, grid.y); var p = go.AddComponent<BeltAuthoring>(); p.StablePieceId = id; p.Grid = grid; p.Direction = direction; p.Shape = shape; p.StartsLine = start; p.EndsLine = end; return p; }
    }
}
