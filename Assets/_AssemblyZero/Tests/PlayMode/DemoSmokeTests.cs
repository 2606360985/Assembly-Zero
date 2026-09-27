using System.Collections;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssemblyZero.Tests
{
    public sealed class DemoSmokeTests
    {
        [UnityTest]
        public IEnumerator DemoSceneLoadsWithSingleSimulationDriver()
        {
            yield return SceneManager.LoadSceneAsync("EP01_Demo", LoadSceneMode.Single);
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<AssemblyZeroSimulationDriver>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length);
            Assert.Greater(Object.FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None).Length, 10);
            Assert.IsNotNull(Object.FindFirstObjectByType<StagePresentationController>());
            Assert.IsNotNull(Shader.Find("AssemblyZero/ConveyorSurface"));
            Assert.IsNotNull(GameObject.Find("Smart Warehouse"));
            Assert.IsNotNull(GameObject.Find("Joint Module Buffer"));
            var overlay = GameObject.Find("Stage 1 - Piece Directions");
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.activeInHierarchy, "Stage 1 must show the piece-direction arrows at runtime.");
            Assert.AreEqual(32, Object.FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None).Length);
            var firstPiece = GameObject.Find("Belt Piece 00");
            Assert.IsNotNull(firstPiece);
            var movingSurface = firstPiece.transform.Find("Moving Surface");
            Assert.IsNotNull(movingSurface);
            var pieceBounds = firstPiece.GetComponent<Renderer>().bounds;
            var surfaceBounds = movingSurface.GetComponent<Renderer>().bounds;
            Assert.Greater(surfaceBounds.min.y, pieceBounds.max.y, "The animated conveyor surface must sit above the Belt Piece box.");

            var driver = Object.FindFirstObjectByType<AssemblyZeroSimulationDriver>();
            var cargoRenderer = Object.FindFirstObjectByType<BatchItemRenderer>();
            Assert.IsNotNull(cargoRenderer);
            Assert.AreEqual("CarboardBox4", cargoRenderer.CargoMeshName);

            driver.ResetStage(3);
            yield return null;
            Assert.AreEqual(20000, driver.Backend.Statistics.LogicalItems);
            Assert.AreEqual(128, cargoRenderer.VisibleCount);
            Assert.IsTrue(cargoRenderer.IsRepresentativeSampling);
            Assert.That(cargoRenderer.SamplingRatio, Is.EqualTo(156.25f).Within(0.01f));

            driver.ResetStage(5);
            yield return null;
            Assert.AreEqual(20000, driver.Backend.Statistics.LogicalItems);
            Assert.AreEqual(128, cargoRenderer.VisibleCount);
            Assert.That(cargoRenderer.SamplingRatio, Is.EqualTo(156.25f).Within(0.01f));

            driver.ResetStage(6);
            yield return null;
            Assert.AreEqual(112, driver.Backend.Statistics.LogicalItems);
            Assert.AreEqual(112, cargoRenderer.VisibleCount);
            Assert.IsFalse(cargoRenderer.IsRepresentativeSampling);
        }
    }
}
