using System.Collections;
using System.Linq;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssemblyZero.Tests
{
    public sealed class EP03FreeCameraTests
    {
        [UnityTest] public IEnumerator ButtonsAndHiddenUiShortcutSelectOneCameraInBothScenes()
        {
            foreach (var scene in new[] { EP03ViewControls.FullScene, EP03ViewControls.MinimalScene })
            {
                yield return SceneManager.LoadSceneAsync(scene); yield return null;
                var controls = Object.FindFirstObjectByType<EP03ViewControls>(); var driver = controls.Cinematic.Driver; driver.Paused = true;
                var free = driver.FreeCamera; Assert.IsNotNull(free); Assert.IsNotNull(controls.FreeButton);
                var hash = driver.Simulation.CalculateStateHash();
                controls.FilmButton.onClick.Invoke(); yield return null;
                var filmPosition = driver.Cinematic.FilmCamera.transform.position;
                controls.FreeButton.onClick.Invoke(); yield return null;
                Assert.IsTrue(free.IsActive); Assert.IsFalse(driver.Cinematic.IsPlaying);
                Assert.That(Vector3.Distance(filmPosition, free.View.transform.position), Is.LessThan(.001f));
                Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled));
                controls.FilmButton.onClick.Invoke(); yield return null;
                Assert.IsFalse(free.IsActive); Assert.IsTrue(driver.Cinematic.IsPlaying);
                Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled));
                var visibility = driver.GetComponent<EP03UiVisibility>(); visibility.SetHidden(true);
                var keyboard = InputSystem.AddDevice<Keyboard>("EP03 Free Shortcut Test");
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); yield return null;
                    Assert.IsTrue(free.IsActive); Assert.IsTrue(visibility.Hidden); Assert.IsFalse(driver.Cinematic.IsPlaying);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C)); yield return null;
                    Assert.IsFalse(free.IsActive); Assert.IsTrue(driver.Cameras.Camera.enabled);
                    Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled));
                    Assert.AreEqual(hash, driver.Simulation.CalculateStateHash());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                }
                finally { InputSystem.RemoveDevice(keyboard); visibility.SetHidden(false); }
            }
        }

        [UnityTest] public IEnumerator RightMouseFlightLookSpeedAndEscapeWorkWhileTimeIsPaused()
        {
            yield return SceneManager.LoadSceneAsync(EP03ViewControls.MinimalScene); yield return null;
            var driver = Object.FindFirstObjectByType<EP03SimulationDriver>(); driver.Paused = true;
            var free = driver.FreeCamera; free.Begin(); free.SendMessage("OnApplicationFocus", true);
            var keyboard = InputSystem.AddDevice<Keyboard>("EP03 Free Flight Test");
            var mouse = InputSystem.AddDevice<Mouse>("EP03 Free Flight Mouse");
            var previousTimeScale = Time.timeScale; var cursorLock = Cursor.lockState; var cursorVisible = Cursor.visible;
            var hash = driver.Simulation.CalculateStateHash(); var position = free.View.transform.position; var rotation = free.View.transform.rotation;
            try
            {
                Time.timeScale = 0;
                var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, delta = new Vector2(25, -12), scroll = new Vector2(0, 120) }.WithButton(MouseButton.Right));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.E, Key.LeftShift));
                yield return null; yield return null; yield return null;
                Assert.IsTrue(free.IsLooking); Assert.Greater(free.CurrentSpeed, free.MoveSpeed);
                Assert.Greater(Vector3.Distance(position, free.View.transform.position), .05f);
                Assert.Greater(free.View.transform.position.y, position.y);
                Assert.Greater(Quaternion.Angle(rotation, free.View.transform.rotation), .1f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }); yield return null;
                var stopped = free.View.transform.position; yield return null; yield return null;
                Assert.AreEqual(stopped, free.View.transform.position, "Movement requires the right mouse button.");
                Assert.IsFalse(free.IsLooking); Assert.AreEqual(cursorLock, Cursor.lockState); Assert.AreEqual(cursorVisible, Cursor.visible);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                Assert.IsFalse(free.IsActive); Assert.IsTrue(driver.Cameras.Camera.enabled);
                Assert.AreEqual(hash, driver.Simulation.CalculateStateHash());
            }
            finally
            {
                free.Stop(); Time.timeScale = previousTimeScale;
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest] public IEnumerator RightClickOnUiDoesNotCaptureMouseAndFocusLossReleasesIt()
        {
            yield return SceneManager.LoadSceneAsync(EP03ViewControls.MinimalScene); yield return null;
            var controls = Object.FindFirstObjectByType<EP03ViewControls>(); var free = controls.FreeCamera;
            free.Driver.Paused = true; free.Begin(); free.SendMessage("OnApplicationFocus", true); Canvas.ForceUpdateCanvases();
            var mouse = InputSystem.AddDevice<Mouse>("EP03 Free Camera UI Mouse");
            var cursorLock = Cursor.lockState; var cursorVisible = Cursor.visible;
            try
            {
                var rect = controls.SceneButton.GetComponent<RectTransform>();
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Right)); yield return null;
                Assert.IsFalse(free.IsLooking, "An interactive button must not start mouse-look.");
                var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center }.WithButton(MouseButton.Right)); yield return null;
                Assert.IsTrue(free.IsLooking);
                free.SendMessage("OnApplicationFocus", false);
                Assert.IsFalse(free.IsLooking); Assert.AreEqual(cursorLock, Cursor.lockState); Assert.AreEqual(cursorVisible, Cursor.visible);
                free.SendMessage("OnApplicationFocus", true); yield return null;
                Assert.IsFalse(free.IsLooking, "Refocusing requires a new right click before flight resumes.");
            }
            finally { free.Stop(); InputSystem.RemoveDevice(mouse); }
        }
    }
}
