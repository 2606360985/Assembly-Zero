using System;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AssemblyZero.Tests
{
    public sealed class EP02ConfigurationTests
    {
        private Scene scene;
        private EP02SimulationDriver driver;
        [SetUp] public void SetUp() { scene = EditorSceneManager.OpenScene("Assets/_AssemblyZero/Scenes/EP02_Demo.unity", OpenSceneMode.Additive); driver = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EP02SimulationDriver>()).Single(); }
        [TearDown] public void TearDown() { if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
        [Test] public void SixPartsHaveSixtySevenUniqueMeshesAndOneAssemblyScale()
        {
            var parts = driver.Layout.Parts; Assert.AreEqual(6, parts.Length); var meshes = parts.SelectMany(p => p.SourcePrefab.GetComponentsInChildren<MeshFilter>()).Select(m => m.sharedMesh).ToArray(); Assert.AreEqual(67, meshes.Length); Assert.AreEqual(67, meshes.Distinct().Count());
            foreach (var p in parts) { Assert.AreEqual(parts[0].AssemblyPose.Scale, p.AssemblyPose.Scale); Assert.AreEqual(p.SourcePrefab, p.WrapperPrefab.GetComponent<PartVisualAnchor>().Definition.SourcePrefab); }
            foreach (var socket in driver.Layout.Sockets) Assert.Less(Quaternion.Angle(socket.transform.localRotation, Quaternion.identity), .01f, "Sockets must inherit the common assembly frame.");
        }
        [Test] public void ULineHasTwoLanesAndContinuousIndependentOffsets()
        {
            var t = driver.Layout.BakeAndValidate(); Assert.AreEqual(27, t.OrderedPieces.Length); Assert.AreEqual(2, t.OrderedPieces.Count(b => b.Shape == BeltPieceShape.Corner90)); Assert.AreEqual(2, t.DomainTopology.Lanes.Length);
            for (var d = 0; d < t.DomainTopology.Lanes[0].Length; d += 64) Assert.That(Vector3.Distance(BeltTopologyBuilder.SampleWorld(t, d, 0), BeltTopologyBuilder.SampleWorld(t, d, 1)), Is.EqualTo(driver.Layout.LaneSeparation).Within(.001));
        }
        [Test] public void MissingReferenceAndUnreachableArmFailBeforeSimulation()
        {
            var a = driver.Layout.Inserters[0]; var grip = a.Grip; a.Grip = null; Assert.Throws<InvalidOperationException>(() => driver.Layout.BakeAndValidate()); a.Grip = grip;
            a.Reach = .01f; Assert.Throws<InvalidOperationException>(() => driver.Layout.BakeAndValidate());
        }
        [Test] public void SixTeachingStagesAreReproducibleAndShowTheirClaimedBehaviour()
        {
            var topology = driver.Layout.BakeAndValidate();
            for (var stage = 1; stage <= 6; stage++)
            {
                var config = driver.Scenario.Create(stage, driver.Layout, topology); var a = new EP02Simulation(config, topology.DomainTopology); var b = new EP02Simulation(config, topology.DomainTopology);
                if (stage == 4) foreach (var w in new[] { a, b }) w.EnqueueCommand(new AssemblyCommand(16, 0, AssemblyCommandKind.SetSocketLocked, 0, true));
                var ticks = stage == 5 ? 6000 : 1600; var wait = false; var conflict = false; var endQueue = false;
                for (var tick = 1; tick <= ticks; tick++)
                {
                    a.Tick(); b.Tick(); Assert.AreEqual(a.Statistics.StateHash, b.Statistics.StateHash, "stage " + stage + ", tick " + tick); Assert.IsTrue(a.Statistics.Conserved);
                    wait |= a.Snapshot.Inserters.Any(i => i.State == InserterState.WaitingForTarget && i.HeldItem != 0);
                    if (stage == 2 && tick == 1) { conflict = a.Snapshot.Inserters[0].IntentItem != 0 && a.Snapshot.Inserters[0].IntentItem == a.Snapshot.Inserters[1].IntentItem; Assert.AreEqual(1, a.Statistics.Reserved); }
                    if (stage == 5) endQueue |= a.Snapshot.Items.Count(i => i.Lane >= 0 && i.Distance > topology.DomainTopology.Lanes[0].Length - config.MinimumSpacing * 4) >= 4;
                }
                if (stage <= 2) { Assert.AreEqual(1, a.Statistics.CompletedConsumed); Assert.AreEqual(0, a.Statistics.CompletedRobots); }
                if (stage == 2) Assert.IsTrue(conflict);
                if (stage == 4) { Assert.IsTrue(wait); Assert.Greater(a.Statistics.Rejected, 0); }
                if (stage == 5) { Assert.IsTrue(endQueue, "Finite recovery must create a real exit queue"); Assert.Greater(a.Statistics.SourceBlockedTicks, 0); }
                if (stage == 3 || stage == 6) Assert.Greater(a.Statistics.CompletedRobots, 0);
            }
        }
    }
}
