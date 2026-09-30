using System.Collections;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace AssemblyZero.Tests
{
    public sealed class EP02SmokeTests
    {
        [UnityTest] public IEnumerator SceneLoadsAndPresentationDoesNotMutateLogistics()
        {
            yield return SceneManager.LoadSceneAsync("EP02_Demo", LoadSceneMode.Single); yield return null;
            var drivers = Object.FindObjectsByType<EP02SimulationDriver>(FindObjectsSortMode.None); Assert.AreEqual(1, drivers.Length); var driver = drivers[0]; driver.Paused = true;
            Assert.IsNull(driver.StartupError); Assert.AreEqual(0, Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length); Assert.AreEqual(27, driver.Layout.Belts.Length); Assert.AreEqual(6, driver.Layout.Inserters.Length); Assert.AreEqual(6, driver.Layout.Sockets.Length);
            Assert.AreEqual(1, Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length);
            driver.ResetStage(2); driver.StepTicks(1); Assert.AreEqual(1, driver.Simulation.Statistics.Reserved); Assert.AreEqual(driver.Simulation.Snapshot.Inserters[0].IntentItem, driver.Simulation.Snapshot.Inserters[1].IntentItem);
            driver.ResetStage(4); driver.StepTicks(80); Assert.IsTrue(driver.Simulation.Snapshot.Inserters.Any(a => a.HeldItem != 0 && a.State == InserterState.WaitingForTarget));
            driver.ResetStage(6); driver.StepTicks(190); Assert.Greater(driver.Simulation.Statistics.CompletedRobots, 0);
            var hash = driver.Simulation.CalculateStateHash(); var completed = driver.Simulation.Statistics.CompletedRobots; var payloadCount = driver.PartPresenter.VisiblePayloads;
            for (var i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(hash, driver.Simulation.CalculateStateHash()); Assert.AreEqual(completed, driver.Simulation.Statistics.CompletedRobots); Assert.AreEqual(payloadCount, driver.PartPresenter.VisiblePayloads);
            foreach (var item in driver.Simulation.Snapshot.Items)
            {
                var go = GameObject.Find("Item " + item.ItemId + " - " + driver.Layout.Part(item.Part.Value).PartName); Assert.IsNotNull(go); var anchor = go.GetComponent<PartVisualAnchor>();
                if (item.Ownership == ItemOwnership.HeldByInserter) Assert.AreEqual(driver.Layout.Arm(item.OwnerId).Grip, anchor.PayloadRoot.parent);
                if (item.Ownership == ItemOwnership.InAssemblySocket) Assert.AreEqual(driver.Layout.Socket(item.OwnerId).transform, anchor.PayloadRoot.parent);
                if (item.Ownership == ItemOwnership.CompletedProduct) Assert.AreEqual(driver.Layout.Socket(driver.Simulation.Snapshot.Sockets.Single(s => s.ItemId == item.ItemId).Id).transform, anchor.PayloadRoot.parent);
            }
            ScreenCapture.CaptureScreenshot("Temp/EP02_Stage6.png"); yield return null; yield return null;
            driver.ResetStage(1); yield return null; yield return null;
            Assert.AreEqual(driver.Simulation.Snapshot.Items.Count, Object.FindObjectsByType<PartVisualAnchor>(FindObjectsSortMode.None).Length, "Replay must not leak payloads parented to sockets/grips.");
        }
    }
}
