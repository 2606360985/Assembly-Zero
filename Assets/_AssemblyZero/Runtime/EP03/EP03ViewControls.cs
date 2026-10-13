using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity
{
    public sealed class EP03ViewControls : MonoBehaviour
    {
        public const string FullScene = "EP03_Demo", MinimalScene = "EP03_Minimal";
        public EP03CinematicCamera Cinematic;
        public EP03FreeCamera FreeCamera;
        public UnityEngine.UI.Button FreeButton;
        public UnityEngine.UI.Text FreeLabel;
        public UnityEngine.UI.Button SceneButton, FilmButton;
        public UnityEngine.UI.Text VersionLabel, SceneLabel, FilmLabel, ChapterTitle, ChapterDetail;
        public GameObject Captions;
        public bool Loading { get; private set; }
        public string Destination => gameObject.scene.name == MinimalScene ? FullScene : MinimalScene;

        private void OnEnable()
        {
            if (SceneButton == null || FilmButton == null) return;
            SceneButton.onClick.AddListener(SwitchScene); FilmButton.onClick.AddListener(ToggleFilm); Refresh();
            if (FreeButton != null) FreeButton.onClick.AddListener(ToggleFree);
        }
        private void OnDisable()
        {
            if (SceneButton != null) SceneButton.onClick.RemoveListener(SwitchScene);
            if (FilmButton != null) FilmButton.onClick.RemoveListener(ToggleFilm);
            if (FreeButton != null) FreeButton.onClick.RemoveListener(ToggleFree);
        }
        private void Update() => Refresh();
        public void ToggleFree() { if (!Loading && FreeCamera != null) FreeCamera.Toggle(); Refresh(); }
        public void ToggleFilm()
        {
            if (Loading) return;
            if (Cinematic.IsPlaying && !Cinematic.IsComplete) Cinematic.Stop(); else Cinematic.Begin();
            Refresh();
        }
        public void SwitchScene()
        {
            if (Loading) return;
            if (!Application.CanStreamedLevelBeLoaded(Destination)) { SceneLabel.text = "场景未加入构建列表"; return; }
            Loading = true; SceneButton.interactable = false; FilmButton.interactable = false;
            if (FreeButton != null) FreeButton.interactable = false;
            SceneLabel.text = "正在切换…";
            SceneManager.LoadSceneAsync(Destination, LoadSceneMode.Single);
        }
        public void Refresh()
        {
            if (Cinematic == null || VersionLabel == null) return;
            var minimal = gameObject.scene.name == MinimalScene;
            VersionLabel.text = minimal ? "EP03  /  最小版" : "EP03  /  完整版";
            if (!Loading) SceneLabel.text = minimal ? "切换至完整版" : "切换至最小版";
            FilmLabel.text = Cinematic.IsComplete ? "重新播放生产影片" : Cinematic.IsPlaying ? "退出导演镜头" : "播放生产影片";
            var flying = FreeCamera != null && FreeCamera.IsActive;
            if (FreeLabel != null) FreeLabel.text = flying ? "退出自由相机 [G]" : "自由相机 [G]";
            Captions.SetActive(Cinematic.IsPlaying || flying);
            ChapterTitle.text = flying ? "自由飞行 · " + FreeCamera.CurrentSpeed.ToString("F1") + " m/s" : Cinematic.ShotTitle;
            ChapterDetail.text = flying ? "按住右键：环视 / WASD 飞行 / Q/E 升降 · Shift 加速 · 右键+滚轮调速 · Esc 退出" : (Cinematic.Driver != null && Cinematic.Driver.Paused ? "已暂停 · " : "") + Cinematic.ShotDetail;
        }
    }
}
