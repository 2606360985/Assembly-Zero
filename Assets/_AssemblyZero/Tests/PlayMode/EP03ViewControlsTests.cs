using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssemblyZero.Tests
{
    public sealed class EP03ViewControlsTests
    {
        [UnityTest] public IEnumerator TopRightButtonsRaycastAndSwitchBothWaysWithoutDuplicateCamerasOrEvents()
        {
            yield return SceneManager.LoadSceneAsync(EP03ViewControls.FullScene); yield return null;
            foreach (var destination in new[] { EP03ViewControls.MinimalScene, EP03ViewControls.FullScene })
            {
                var controls = Object.FindFirstObjectByType<EP03ViewControls>(); controls.Cinematic.Driver.Paused = true;
                Assert.AreEqual(Vector3.one * .5f, controls.transform.localScale);
                Assert.AreEqual(destination, controls.Destination);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
                Canvas.ForceUpdateCanvases();
                var rect = controls.SceneButton.GetComponent<RectTransform>();
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Assert.That(point.x, Is.GreaterThan(Screen.width * .6f)); Assert.That(point.y, Is.GreaterThan(Screen.height * .6f));
                var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.IsTrue(hits.Any(h => h.gameObject == controls.SceneButton.gameObject));
                ExecuteEvents.Execute(controls.SceneButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.IsTrue(controls.Loading); controls.SwitchScene(); // Repeated clicks must not start another load.
                for (var frame = 0; frame < 300 && SceneManager.GetActiveScene().name != destination; frame++) yield return null;
                yield return null;
                Assert.AreEqual(destination, SceneManager.GetActiveScene().name);
                var driver = Object.FindFirstObjectByType<EP03SimulationDriver>(); driver.Paused = true; Assert.IsNull(driver.StartupError);
                Assert.AreEqual(1, Object.FindObjectsByType<EP03SimulationDriver>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled));
                Assert.Less(driver.Simulation.TickNumber, 30, "A scene switch starts its own fresh production state.");
            }
        }

        [UnityTest] public IEnumerator FilmButtonPausesAndReturnsToManualCamera()
        {
            yield return SceneManager.LoadSceneAsync(EP03ViewControls.MinimalScene); yield return null;
            var controls = Object.FindFirstObjectByType<EP03ViewControls>(); var driver = controls.Cinematic.Driver; driver.Paused = true;
            controls.FilmButton.onClick.Invoke(); yield return null;
            Assert.IsTrue(controls.Cinematic.IsPlaying); Assert.IsFalse(driver.Cameras.Camera.enabled); Assert.IsTrue(controls.Cinematic.FilmCamera.enabled);
            var position = controls.Cinematic.FilmCamera.transform.position; var tick = driver.Simulation.TickNumber;
            yield return null; yield return null;
            Assert.AreEqual(position, controls.Cinematic.FilmCamera.transform.position); Assert.AreEqual(tick, driver.Simulation.TickNumber);
            Assert.IsTrue(controls.Captions.activeSelf);
            controls.FilmButton.onClick.Invoke(); yield return null;
            Assert.IsFalse(controls.Cinematic.IsPlaying); Assert.IsTrue(driver.Cameras.Camera.enabled); Assert.IsFalse(controls.Captions.activeSelf);
        }

        [UnityTest] public IEnumerator HKeyHidesAllUiAndRestoresItWhileFilmKeepsItsState()
        {
            yield return SceneManager.LoadSceneAsync(EP03ViewControls.MinimalScene); yield return null;
            var controls = Object.FindFirstObjectByType<EP03ViewControls>(); var driver = controls.Cinematic.Driver; driver.Paused = true;
            controls.FilmButton.onClick.Invoke(); yield return null;
            var visibility = driver.GetComponent<EP03UiVisibility>(); Assert.IsNotNull(visibility);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var before = canvases.Select(c => c.enabled).ToArray(); var hash = driver.Simulation.CalculateStateHash();
            var keyboard = InputSystem.AddDevice<Keyboard>("EP03 UI Test Keyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H)); yield return null;
                Assert.IsTrue(visibility.Hidden); Assert.IsTrue(canvases.All(c => !c.enabled));
                Assert.IsTrue(controls.Cinematic.IsPlaying); Assert.IsTrue(controls.Cinematic.FilmCamera.enabled);
                Assert.IsFalse(controls.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().enabled);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                controls.Refresh(); Assert.IsTrue(canvases.All(c => !c.enabled));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H)); yield return null;
                Assert.IsFalse(visibility.Hidden); CollectionAssert.AreEqual(before, canvases.Select(c => c.enabled).ToArray());
                Assert.IsTrue(controls.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().enabled);
                Assert.AreEqual(hash, driver.Simulation.CalculateStateHash());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                controls.FilmButton.onClick.Invoke();
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
