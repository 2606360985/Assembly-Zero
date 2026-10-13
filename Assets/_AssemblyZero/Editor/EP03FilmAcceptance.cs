using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity.Editor
{
    public static class EP03FilmAcceptance
    {
        public static void Capture()
        {
            EditorApplication.update -= Capture;
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var original = SceneManager.GetActiveScene().path;
            var report = "EP03 film camera and controls acceptance\n";
            try
            {
                foreach (var scale in new[] { "Demo", "Minimal" })
                {
                    EditorSceneManager.OpenScene("Assets/_AssemblyZero/Scenes/EP03_" + scale + ".unity");
                    var d = UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>(); d.Restart();
                    if (d.StartupError != null) throw new InvalidOperationException(d.StartupError);
                    var controls = d.Hud.GetComponentInParent<Canvas>().GetComponentInChildren<EP03ViewControls>();
                    controls.Refresh(); CaptureFrame(d.Cameras.Camera, controls, "Reports/EP03/Film-Controls-" + scale + ".png");
                    var film = d.Cinematic; film.Begin();
                    var captured = new HashSet<string>(); var lastLog = -1; var shotTick = 0L;
                    while (d.Simulation.TickNumber < 60000 && !film.IsComplete)
                    {
                        d.Simulation.Tick(); if (d.Simulation.TickNumber % 8 != 0) continue;
                        d.Present(); film.Present(); controls.Refresh();
                        if (lastLog != film.ShotLog.Count) { lastLog = film.ShotLog.Count; shotTick = d.Simulation.TickNumber; }
                        if (d.Simulation.TickNumber - shotTick < (film.ShotTitle.StartsWith("05") ? 40 : 80)) continue;
                        string key = null;
                        if (film.ShotTitle.StartsWith("02")) key = "Raw";
                        if (film.ShotTitle.StartsWith("03") && d.Simulation.Ledger.TryGetValue(film.TrackedMaterialId, out var material) && material.Owner == AssemblyZero.Domain.FactoryOwner.Processing) key = "Processing";
                        if (film.ShotTitle.StartsWith("04")) key = "Components";
                        if (film.ShotTitle.StartsWith("05"))
                        {
                            var product = d.Simulation.Assembly.Snapshot.Products.FirstOrDefault(p => p.Id == film.TrackedProductId);
                            if (product != null && product.NextStation < 6 && d.Simulation.Assembly.Snapshot.Inserters.Any(a => a.TargetProductId == product.Id && a.State == AssemblyZero.Domain.InserterState.Placing)) key = "Station-" + (product.NextStation + 1);
                        }
                        if (film.ShotTitle.StartsWith("06")) key = "Robot";
                        if (film.ShotTitle.StartsWith("07")) key = "Dispatch";
                        if (key == null || captured.Contains(key) || scale == "Minimal" && key != "Robot") continue;
                        CaptureFrame(film.FilmCamera, controls, "Reports/EP03/Film-" + scale + "-" + key + ".png"); captured.Add(key);
                        if (scale == "Demo" && key == "Robot")
                        {
                            var visibility = d.GetComponent<EP03UiVisibility>(); visibility.SetHidden(true);
                            CaptureFrame(film.FilmCamera, controls, "Reports/EP03/Film-No-UI.png"); visibility.SetHidden(false);
                        }
                    }
                    d.Simulation.AssertConserved();
                    if (!film.IsComplete || film.VisitedStationsMask != 63) throw new InvalidOperationException(scale + " film did not cover all six stations.");
                    var hash = d.Simulation.CalculateStateHash(); var position = film.FilmCamera.transform.position;
                    film.Present(); film.Present();
                    if (hash != d.Simulation.CalculateStateHash() || position != film.FilmCamera.transform.position) throw new InvalidOperationException("Paused film changed camera/production.");
                    report += scale + ": ticks=" + d.Simulation.TickNumber + ", product=" + film.TrackedProductId + ", conversions=" + film.Conversions + ", stationMask=" + film.VisitedStationsMask + ", dispatched=" + d.Simulation.Dispatched + ", completed=" + film.IsComplete + "\n";
                    File.WriteAllLines("Reports/EP03/Film-Shots-" + scale + ".txt", film.ShotLog);
                    film.Stop();
                }
                File.WriteAllText("Reports/EP03/FilmAcceptance.txt", report + "Production conserved. Camera motion freezes without simulation ticks; cinematic and manual camera handoff passed.\n");
            }
            catch (Exception e) { File.WriteAllText("Reports/EP03/FilmAcceptance.txt", report + e); throw; }
            finally { EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? EP03SceneBuilder.ScenePath : original); }
        }

        private static void CaptureFrame(Camera camera, EP03ViewControls controls, string path)
        {
            var canvas = controls.GetComponentInParent<Canvas>(); var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; var plane = canvas.planeDistance;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var asyncShaders = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .02f;
                Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = null; ShaderUtil.allowAsyncCompilation = asyncShaders;
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane;
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
