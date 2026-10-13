using System.Collections.Generic;
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
    public sealed class EP03AssemblyPresentationTests
    {
        private Scene scene;
        private EP03SimulationDriver driver;

        private void Open(string scale)
        {
            scene = EditorSceneManager.OpenScene("Assets/_AssemblyZero/Scenes/EP03_" + scale + ".unity", OpenSceneMode.Additive);
            driver = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EP03SimulationDriver>()).Single();
        }

        [TearDown] public void Close()
        {
            if (driver != null) { driver.Parts.Clear(); driver.Cell.Clear(); }
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        [Test] public void AllSixTransferredPartsMatchTheOriginalRobotInPalletCoordinates()
        {
            Open("Minimal"); driver.Restart(); Assert.IsNull(driver.StartupError);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Robots.fbx");
            var originals = source.GetComponentsInChildren<MeshFilter>().ToDictionary(m => m.sharedMesh);
            var bounds = new Bounds(); var first = true;
            foreach (var mesh in originals.Values) foreach (var v in mesh.sharedMesh.vertices)
            {
                var p = source.transform.InverseTransformPoint(mesh.transform.TransformPoint(v));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var held = new HashSet<int>(); var installed = new HashSet<int>();
            while (driver.Simulation.TickNumber < 40000 && installed.Count < 6)
            {
                driver.Simulation.Tick();
                if (driver.Simulation.TickNumber % 8 != 0) continue;
                var snapshot = driver.Simulation.Assembly.Snapshot;
                driver.Arms.Present(snapshot, driver.AssemblyTopology, 0);
                driver.Parts.Present(snapshot, driver.AssemblyTopology);
                foreach (var item in snapshot.Items)
                {
                    if (item.Ownership == ItemOwnership.HeldByInserter) held.Add(item.ItemId);
                    if (!held.Contains(item.ItemId) || item.Ownership != ItemOwnership.InAssemblySocket || installed.Contains(item.Part.Value)) continue;
                    var anchor = driver.Parts.GetComponentsInChildren<PartVisualAnchor>().Single(a => a.name.StartsWith("Item " + item.ItemId + " -"));
                    var product = driver.Parts.ProductSocket(item.ProductId, item.OwnerId).parent;
                    Assert.That(Quaternion.Angle(anchor.PayloadRoot.localRotation, Quaternion.identity), Is.LessThan(.001f), anchor.name);
                    foreach (var mesh in anchor.SourceRoot.GetComponentsInChildren<MeshFilter>())
                    {
                        var original = originals[mesh.sharedMesh];
                        foreach (var v in mesh.sharedMesh.vertices)
                        {
                            var common = source.transform.InverseTransformPoint(original.transform.TransformPoint(v));
                            var expected = (common - origin) * anchor.Definition.AssemblyPose.Scale.x + Vector3.up * .14f;
                            var actual = product.InverseTransformPoint(mesh.transform.TransformPoint(v));
                            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.003f), anchor.name + " / " + mesh.name);
                        }
                    }
                    driver.Parts.Present(snapshot, driver.AssemblyTopology);
                    Assert.That(Quaternion.Angle(anchor.PayloadRoot.localRotation, Quaternion.identity), Is.LessThan(.001f));
                    installed.Add(item.Part.Value);
                }
            }
            driver.Simulation.AssertConserved(); Assert.AreEqual(6, installed.Count, "Every part must be observed from grip through installation.");
        }

        [TestCase("Demo")] [TestCase("Minimal")]
        public void StationsHaveDistinctRealProcessDurations(string scale)
        {
            Open(scale);
            var topology = driver.AssemblyLayout.BakeAndValidate();
            var config = driver.AssemblyScenario.Create(driver.AssemblyLayout, topology);
            Assert.AreEqual(6, config.Inserters.Select(a => (a.PickingTicks, a.CarryingTicks, a.PlacingTicks, a.ReturningTicks)).Distinct().Count());
            Assert.Greater(config.Inserters.Max(a => a.PlacingTicks), config.Inserters.Min(a => a.PlacingTicks));
            driver.Restart(); Assert.IsNull(driver.StartupError);
            // EP03 uses dedicated, externally supplied lanes. Random EP02 source
            // traffic can starve those lanes and does not represent these scenes.
            var world = driver.Simulation;
            var independent = new HashSet<int>();
            for (var tick = 0; tick < 40000 && (independent.Count < 2 || world.Dispatched < 2); tick++)
            {
                world.Tick();
                var active = world.Assembly.Snapshot.Inserters.Where(a => a.State == InserterState.Picking || a.State == InserterState.Carrying || a.State == InserterState.Placing).ToArray();
                if (active.Length > 1 && active.Select(a => (a.State, a.StateEnterTick, a.Duration)).Distinct().Count() > 1)
                    foreach (var arm in active) independent.Add(arm.Id);
            }
            // The minimal chain can supply only a few simultaneous stations. It
            // must not animate the others just to make the line look busy.
            Assert.GreaterOrEqual(independent.Count, 2, "Available stations must overlap with distinct phases.");
            Assert.IsTrue(world.Assembly.Snapshot.Inserters.All(a => a.Transfers > 0), "Every station must perform its own transfer.");
            Assert.Greater(world.Dispatched, 1); world.AssertConserved();
        }
    }
}
