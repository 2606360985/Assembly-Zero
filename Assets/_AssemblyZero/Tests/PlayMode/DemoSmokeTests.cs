using System.Collections;
using System.Collections.Generic;
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
            Assert.AreEqual(45, Object.FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None).Length);
            var pieces = Object.FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None);
            System.Array.Sort(pieces, (a, b) => a.StablePieceId.CompareTo(b.StablePieceId));
            var grids = new HashSet<Vector2Int>(); var corners = 0;
            for (var i = 0; i < pieces.Length; i++)
            {
                Assert.IsTrue(grids.Add(pieces[i].Grid), $"Duplicate belt grid at {pieces[i].Grid}.");
                Assert.AreEqual(i == 0, pieces[i].StartsLine); Assert.AreEqual(i == pieces.Length - 1, pieces[i].EndsLine);
                if (pieces[i].Shape == AssemblyZero.Domain.BeltPieceShape.Corner90) corners++;
                if (i > 0) Assert.AreEqual(1, Mathf.Abs(pieces[i].Grid.x - pieces[i - 1].Grid.x) + Mathf.Abs(pieces[i].Grid.y - pieces[i - 1].Grid.y));
            }
            Assert.AreEqual(8, corners);
            var firstPiece = GameObject.Find("Belt Piece 00");
            Assert.IsNotNull(firstPiece);
            var movingSurface = firstPiece.transform.Find("Moving Surface");
            Assert.IsNotNull(movingSurface);
            Assert.IsNotNull(firstPiece.transform.Find("Center Lane"));
            Assert.IsNull(firstPiece.transform.Find("Left Lane"));
            var pieceBounds = firstPiece.GetComponent<Renderer>().bounds;
            var surfaceBounds = movingSurface.GetComponent<Renderer>().bounds;
            Assert.Greater(surfaceBounds.min.y, pieceBounds.max.y, "The animated conveyor surface must sit above the Belt Piece box.");

            var driver = Object.FindFirstObjectByType<AssemblyZeroSimulationDriver>();
            Assert.AreEqual(1, driver.Topology.DomainTopology.Lanes.Length);
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
