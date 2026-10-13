using System.Linq;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Tests
{
    public sealed class EP03CinematicTests
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
            if (driver != null) { driver.Cinematic.Stop(); driver.Parts.Clear(); driver.Cell.Clear(); }
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase("Demo")] [TestCase("Minimal")]
        public void DocumentaryFollowsConversionsAllSixStationsAndActualDispatch(string scale)
        {
            Open(scale); driver.Restart(); Assert.IsNull(driver.StartupError);
            var film = driver.Cinematic; var world = driver.Simulation; film.Begin();
            for (var i = 0; i < 60000 && !film.IsComplete; i++)
            {
                world.Tick(); if (i % 4 != 0) continue;
                film.Present();
                Assert.IsTrue(float.IsFinite(film.FilmCamera.transform.position.x));
                Assert.Greater(film.FilmCamera.transform.position.y, .5f);
            }
            Assert.IsTrue(film.IsComplete, string.Join("\n", film.ShotLog));
            Assert.GreaterOrEqual(film.Conversions, 2);
            Assert.AreEqual(63, film.VisitedStationsMask, string.Join("\n", film.ShotLog));
            foreach (var chapter in new[] { "01", "02", "03", "04", "05", "06", "07" })
                Assert.IsTrue(film.ShotLog.Any(s => s.Contains(": " + chapter)), "Missing chapter " + chapter);
            Assert.IsTrue(world.ProductReceipts.Any(p => p.ProductId == film.TrackedProductId && p.DispatchedTick >= 0));
            world.AssertConserved(); var hash = world.CalculateStateHash(); var position = film.FilmCamera.transform.position;
            for (var i = 0; i < 10; i++) film.Present();
            Assert.AreEqual(hash, world.CalculateStateHash()); Assert.AreEqual(position, film.FilmCamera.transform.position);
        }

        [Test] public void StartingMidProductionStillRecordsEveryAssemblyStation()
        {
            Open("Minimal"); driver.Restart(); driver.StepTicks(2400); driver.Cinematic.Begin();
            for (var i = 0; i < 60000 && !driver.Cinematic.IsComplete; i++)
            {
                driver.Simulation.Tick(); if (i % 4 == 0) driver.Cinematic.Present();
            }
            Assert.IsTrue(driver.Cinematic.IsComplete); Assert.AreEqual(63, driver.Cinematic.VisitedStationsMask);
            driver.Simulation.AssertConserved();
        }

        [Test] public void PauseResetAndManualCameraHandoffDoNotChangeProduction()
        {
            Open("Minimal"); driver.Restart(); var film = driver.Cinematic; var manual = driver.Cameras.Camera;
            var position = manual.transform.position; var hash = driver.Simulation.CalculateStateHash();
            film.Begin(); Assert.IsFalse(manual.enabled); Assert.IsTrue(film.FilmCamera.enabled);
            Assert.AreEqual(hash, driver.Simulation.CalculateStateHash());
            driver.Simulation.Tick(); film.Present(); var paused = film.FilmCamera.transform.position;
            for (var i = 0; i < 10; i++) film.Present(); Assert.AreEqual(paused, film.FilmCamera.transform.position);
            driver.StepTicks(600); film.Present(); driver.Restart(); film.Present();
            Assert.AreEqual(0, film.TrackedProductId); Assert.IsFalse(film.IsComplete);
            film.Stop(); Assert.IsFalse(film.FilmCamera.enabled); Assert.IsTrue(manual.enabled); Assert.AreEqual(position, manual.transform.position);
        }
    }
}
