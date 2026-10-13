using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity.Editor
{
    public static class EP03PresentationBuilder
    {
        [MenuItem("Tools/Assembly Zero/Install EP03 Film Camera and Scene Controls")]
        public static void InstallIntoScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            var original = SceneManager.GetActiveScene().path;
            try
            {
                foreach (var path in new[] { EP03SceneBuilder.ScenePath, "Assets/_AssemblyZero/Scenes/EP03_Minimal.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    Install(UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>());
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    var buildScenes = EditorBuildSettings.scenes.ToList();
                    var entry = buildScenes.Find(s => s.path == path);
                    if (entry == null) buildScenes.Add(new EditorBuildSettingsScene(path, true)); else entry.enabled = true;
                    EditorBuildSettings.scenes = buildScenes.ToArray();
                }
            }
            finally { EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? EP03SceneBuilder.ScenePath : original); }
        }

        public static void Install(EP03SimulationDriver driver)
        {
            if (driver == null || driver.Hud == null) throw new InvalidOperationException("Missing EP03 driver/HUD.");
            var canvas = driver.Hud.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Missing existing HUD Canvas.");
            if (driver.GetComponent<EP03UiVisibility>() == null) driver.gameObject.AddComponent<EP03UiVisibility>();
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var events = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            if (events.Length == 0) new GameObject("EP03 EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            else if (events.Length != 1) throw new InvalidOperationException("Expected exactly one EventSystem.");
            else if (events[0].GetComponent<InputSystemUIInputModule>() == null) events[0].gameObject.AddComponent<InputSystemUIInputModule>();

            var film = driver.Cinematic;
            if (film == null)
            {
                var go = new GameObject("EP03 Animated Production Camera", typeof(Camera));
                go.transform.SetParent(driver.transform, false);
                var camera = go.GetComponent<Camera>(); camera.CopyFrom(driver.Cameras.Camera); camera.enabled = false;
                camera.transform.SetPositionAndRotation(driver.Cameras.Camera.transform.position, driver.Cameras.Camera.transform.rotation);
                if (driver.Cameras.Camera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data))
                    EditorUtility.CopySerialized(data, go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>());
                film = go.AddComponent<EP03CinematicCamera>(); film.Driver = driver; film.FilmCamera = camera; film.AutoPlay = false;
                driver.Cinematic = film;
            }
            var existing = canvas.GetComponentInChildren<EP03ViewControls>(true);
            if (existing != null) { existing.Cinematic = film; existing.transform.localScale = Vector3.one * .5f; InstallFreeCamera(driver, existing); return; }
            // Match the existing HUD's Chinese font and legacy Text typography.
            var font = driver.Hud.font;
            var panel = Rect("EP03 View Controls", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(320, 156));
            panel.localScale = Vector3.one * .5f;
            Background(panel, new Color(.025f, .045f, .065f, .96f));
            var controls = panel.gameObject.AddComponent<EP03ViewControls>(); controls.Cinematic = film;
            controls.VersionLabel = Text("Current Version", panel, font, new Vector2(16, -10), new Vector2(288, 26), 18, new Color(.38f, .87f, .95f));
            (controls.SceneButton, controls.SceneLabel) = Button("Switch EP03 Scene", panel, font, new Vector2(16, -46), "切换至最小版");
            (controls.FilmButton, controls.FilmLabel) = Button("Toggle Production Film", panel, font, new Vector2(16, -100), "播放生产影片");
            var captions = Rect("Production Film Captions", canvas.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(920, 100));
            Background(captions, new Color(.015f, .028f, .04f, .93f)); controls.Captions = captions.gameObject;
            controls.ChapterTitle = Text("Chapter Title", captions, font, new Vector2(22, -12), new Vector2(876, 34), 25, Color.white);
            controls.ChapterDetail = Text("Shot Detail", captions, font, new Vector2(22, -56), new Vector2(876, 28), 18, new Color(.64f, .84f, .88f));
            InstallFreeCamera(driver, controls);
            controls.Refresh(); captions.gameObject.SetActive(false);
            EditorUtility.SetDirty(driver); EditorUtility.SetDirty(film); EditorUtility.SetDirty(controls);
        }

        private static void InstallFreeCamera(EP03SimulationDriver driver, EP03ViewControls controls)
        {
            if (driver.FreeCamera == null)
            {
                var go = new GameObject("EP03 Free Flight Camera", typeof(Camera)); go.transform.SetParent(driver.transform, false);
                var camera = go.GetComponent<Camera>(); camera.CopyFrom(driver.Cameras.Camera); camera.enabled = false; camera.nearClipPlane = .08f;
                camera.transform.SetPositionAndRotation(driver.Cameras.Camera.transform.position, driver.Cameras.Camera.transform.rotation);
                if (driver.Cameras.Camera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data))
                    EditorUtility.CopySerialized(data, go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>());
                var free = go.AddComponent<EP03FreeCamera>(); free.Driver = driver; free.View = camera; driver.FreeCamera = free;
            }
            controls.FreeCamera = driver.FreeCamera;
            ((RectTransform)controls.transform).sizeDelta = new Vector2(320, 210);
            if (controls.FreeButton == null)
                (controls.FreeButton, controls.FreeLabel) = Button("Toggle Free Camera", controls.transform, driver.Hud.font, new Vector2(16, -154), "自由相机 [G]");
            controls.Refresh(); EditorUtility.SetDirty(driver); EditorUtility.SetDirty(controls);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        private static void Background(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>(); text.font = font; text.fontSize = fontSize; text.color = color; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        private static (UnityEngine.UI.Button, UnityEngine.UI.Text) Button(string name, Transform parent, Font font, Vector2 position, string label)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(288, 40));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = new Color(.11f, .25f, .32f); colors.highlightedColor = new Color(.15f, .39f, .47f); colors.pressedColor = new Color(.07f, .18f, .24f); colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(.08f, .11f, .13f); button.colors = colors;
            var text = Text("Label", rect, font, Vector2.zero, rect.sizeDelta, 21, Color.white); text.alignment = TextAnchor.MiddleCenter; text.text = label;
            return (button, text);
        }
    }
}
