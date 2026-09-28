using System;
using System.Collections.Generic;
using System.IO;
using AssemblyZero.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AssemblyZero.Unity.Editor
{
    [InitializeOnLoad]
    public static class EP01SceneBuilder
    {
        private const string Root = "Assets/_AssemblyZero";
        private const string ScenePath = Root + "/Scenes/EP01_Demo.unity";
        private const string ConfigPath = Root + "/Configs/EP01Scenario.asset";
        private const string FontSourcePath = Root + "/UI/AssemblyZeroChinese.ttf";
        private const string CargoPrefabPath = "Assets/Prefabs/CarboardBox04.prefab";
        private const string CargoMaterialPath = "Assets/Material/CarboardBoxMat.mat";
        private const string SessionKey = "AssemblyZero.EP01.Generated.v4";

        static EP01SceneBuilder()
        {
            EditorApplication.delayCall += AutoBuild;
        }

        private static void AutoBuild()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            SessionState.SetBool(SessionKey, true);
            if (!File.Exists(ScenePath)) BuildScene(); else EnsureBuildSettings();
        }

        [MenuItem("Tools/Assembly Zero/Rebuild EP01 Demo Scene")]
        public static void BuildScene()
        {
            EnsureFolders();
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "EP01_Demo";
            var palette = CreateMaterials();
            var beltCornerLeft = ConveyorMat("BeltCornerLeft", 1f, 1f); var beltCornerRight = ConveyorMat("BeltCornerRight", 1f, -1f);
            var guideWhite = Mat("GuideWhite", new Color(0.82f, 0.95f, 1f), true);
            var config = AssetDatabase.LoadAssetAtPath<ScenarioConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<ScenarioConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            config.ApplyCargoDemoDefaultsIfNeeded(); EditorUtility.SetDirty(config);
            var font = GetOrCreateFont();
            var cargoMesh = GetCargoMesh();
            var cargoSourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(CargoMaterialPath);
            if (cargoSourceMaterial == null) throw new FileNotFoundException("CarboardBox04 material is required for the EP01 cargo renderer.", CargoMaterialPath);
            var cargoOrange = CargoMaterial("CargoServoOrange", cargoSourceMaterial, new Color(1f, 0.16f, 0.015f, 1f));
            var cargoBlue = CargoMaterial("CargoSensorBlue", cargoSourceMaterial, new Color(0.015f, 0.28f, 1f, 1f));

            var environment = new GameObject("Miniature Factory");
            var floor = Mat("FactoryFloor", new Color(0.075f, 0.105f, 0.14f), false);
            CreatePrimitive("Tabletop", PrimitiveType.Cube, environment.transform, new Vector3(0, -0.35f, 0), new Vector3(16, 0.5f, 11), floor);
            CreateFactoryDressing(environment.transform, palette);
            var warehouse = new GameObject("Smart Warehouse"); warehouse.transform.SetParent(environment.transform);
            CreatePrimitive("Warehouse Body", PrimitiveType.Cube, warehouse.transform, new Vector3(-6.25f, 1.2f, 2.5f), new Vector3(2.5f, 2.8f, 3.6f), palette.Steel);
            CreatePrimitive("Loading Bay", PrimitiveType.Cube, warehouse.transform, new Vector3(-5.0f, 0.65f, 2.0f), new Vector3(0.22f, 1.15f, 1.35f), palette.Dark);
            for (var r = 0; r < 3; r++) for (var c = 0; c < 2; c++) CreatePrimitive($"Warehouse Cell {r}-{c}", PrimitiveType.Cube, warehouse.transform, new Vector3(-6.55f + c * 0.62f, 0.5f + r * 0.72f, 0.67f), new Vector3(0.48f, 0.5f, 0.08f), (r + c) % 2 == 0 ? palette.Blue : palette.Orange);
            CreatePrimitive("Warehouse Roof Beacon", PrimitiveType.Cylinder, warehouse.transform, new Vector3(-6.25f, 2.85f, 2.5f), new Vector3(0.28f, 0.15f, 0.28f), palette.Blue);

            var buffer = new GameObject("Joint Module Buffer"); buffer.transform.SetParent(environment.transform);
            CreatePrimitive("Buffer Base", PrimitiveType.Cube, buffer.transform, new Vector3(6.25f, 0.12f, -2.15f), new Vector3(2.8f, 0.25f, 3.2f), palette.Steel);
            CreatePrimitive("Servo Bin", PrimitiveType.Cube, buffer.transform, new Vector3(5.7f, 0.58f, -2.15f), new Vector3(0.92f, 0.8f, 2.5f), palette.Orange);
            CreatePrimitive("Sensor Bin", PrimitiveType.Cube, buffer.transform, new Vector3(6.8f, 0.58f, -2.15f), new Vector3(0.92f, 0.8f, 2.5f), palette.Blue);
            for (var i = 0; i < 5; i++) CreatePrimitive($"Buffer Capacity {i}", PrimitiveType.Cube, buffer.transform, new Vector3(7.32f, 0.28f + i * 0.18f, -3.25f), new Vector3(0.08f, 0.08f, 0.24f), palette.Holo);

            var beltRoot = new GameObject("Transport Belts"); beltRoot.transform.SetParent(environment.transform);
            var topologyOverlay = new GameObject("Stage 1 - Piece Directions"); topologyOverlay.transform.SetParent(environment.transform);
            var lineOverlay = new GameObject("Stage 4 - Transport Line"); lineOverlay.transform.SetParent(environment.transform);
            var path = BuildSerpentinePath();
            BeltAuthoring first = null, last = null;
            for (var i = 0; i < path.Length; i++)
            {
                var go = CreatePrimitive($"Belt Piece {i:00}", PrimitiveType.Cube, beltRoot.transform, new Vector3(path[i].x, 0, path[i].y), new Vector3(0.98f, 0.18f, 0.98f), palette.Steel);
                var authoring = go.AddComponent<BeltAuthoring>(); authoring.Grid = path[i]; authoring.StablePieceId = i; authoring.StartsLine = i == 0; authoring.EndsLine = i == path.Length - 1;
                if (i < path.Length - 1) { var delta = path[i + 1] - path[i]; authoring.Direction = Direction(delta); if (i > 0 && path[i] - path[i - 1] != delta) authoring.Shape = BeltPieceShape.Corner90; }
                go.transform.rotation = Quaternion.Euler(0, DirectionYaw(authoring.Direction), 0);
                first ??= authoring; last = authoring;
                if (authoring.Shape == BeltPieceShape.Corner90)
                {
                    var entryDelta = path[i] - path[i - 1]; var exitDelta = path[i + 1] - path[i];
                    var turnSign = entryDelta.x * exitDelta.y - entryDelta.y * exitDelta.x > 0 ? 1f : -1f;
                    var surface = CreateChildPrimitiveWorldScale("Moving Surface", PrimitiveType.Cube, go.transform, new Vector3(0, 0.115f, 0), new Vector3(0.9f, 0.035f, 0.9f), turnSign > 0 ? beltCornerLeft : beltCornerRight);
                    surface.transform.rotation = Quaternion.Euler(0, DirectionYaw(Direction(entryDelta)), 0);
                    CreateCornerDressing(go.transform, entryDelta, exitDelta, turnSign, palette.Orange, palette.Dark);
                    CreateCornerArrow($"Piece Arrow {i:00}", topologyOverlay.transform, go.transform.position + Vector3.up * 0.48f, entryDelta, exitDelta, guideWhite);
                    var linePivot = CornerPivot(go.transform.position + Vector3.up * 0.36f, entryDelta, exitDelta, out var lineEntry, out var lineExit);
                    CreateArc($"Line Segment {i:00}", lineOverlay.transform, linePivot, lineEntry, lineExit, 0.5f, 0f, 0.025f, 0.12f, palette.Holo, 0.05f, 0.95f);
                }
                else
                {
                    CreateChildPrimitiveWorldScale("Moving Surface", PrimitiveType.Cube, go.transform, new Vector3(0, 0.115f, 0), new Vector3(0.86f, 0.035f, 0.78f), palette.Belt);
                    CreateCenterStripe(go.transform, palette.Orange);
                    CreateChildPrimitiveWorldScale("Left Rail", PrimitiveType.Cube, go.transform, new Vector3(0, 0.18f, -0.45f), new Vector3(0.96f, 0.09f, 0.045f), palette.Dark);
                    CreateChildPrimitiveWorldScale("Right Rail", PrimitiveType.Cube, go.transform, new Vector3(0, 0.18f, 0.45f), new Vector3(0.96f, 0.09f, 0.045f), palette.Dark);
                    CreateArrow($"Piece Arrow {i:00}", topologyOverlay.transform, go.transform.position + Vector3.up * 0.48f, authoring.Direction, guideWhite);
                    CreatePrimitive($"Line Segment {i:00}", PrimitiveType.Cube, lineOverlay.transform, go.transform.position + Vector3.up * 0.36f, new Vector3(0.82f, 0.025f, 0.12f), palette.Holo).transform.rotation = Quaternion.Euler(0, DirectionYaw(authoring.Direction), 0);
                }
            }
            var source = new GameObject("Smart Warehouse Source"); source.transform.SetParent(environment.transform); source.transform.position = first.transform.position + Vector3.left; source.AddComponent<SourceAuthoring>().Target = first;
            var gate = new GameObject("QC Gate"); gate.transform.SetParent(environment.transform); gate.transform.position = last.transform.position; gate.AddComponent<GateAuthoring>().Target = last;
            CreatePrimitive("Gate Left Post", PrimitiveType.Cube, gate.transform, gate.transform.position + new Vector3(0, 0.85f, -0.62f), new Vector3(0.18f, 1.65f, 0.18f), palette.Steel);
            CreatePrimitive("Gate Right Post", PrimitiveType.Cube, gate.transform, gate.transform.position + new Vector3(0, 0.85f, 0.62f), new Vector3(0.18f, 1.65f, 0.18f), palette.Steel);
            CreatePrimitive("Gate Header", PrimitiveType.Cube, gate.transform, gate.transform.position + new Vector3(0, 1.68f, 0), new Vector3(0.22f, 0.2f, 1.42f), palette.Steel);
            var gateBarrier = CreatePrimitive("Gate Barrier", PrimitiveType.Cube, gate.transform, gate.transform.position + new Vector3(0, 1.65f, 0), new Vector3(0.12f, 0.12f, 1.15f), palette.Red);
            var gateIndicator = CreatePrimitive("Gate Status", PrimitiveType.Sphere, gate.transform, gate.transform.position + new Vector3(0, 1.82f, 0), Vector3.one * 0.2f, palette.Warning).GetComponent<Renderer>();
            var sink = new GameObject("Joint Module Sink"); sink.transform.SetParent(environment.transform); sink.transform.position = last.transform.position + Vector3.right; sink.AddComponent<SinkAuthoring>().Target = last;

            var holoRoot = new GameObject("R-01 Hologram"); holoRoot.transform.position = new Vector3(6.15f, 1.1f, 0.3f);
            var holoOrigin = holoRoot.transform.position;
            var torso = CreatePrimitive("Torso", PrimitiveType.Capsule, holoRoot.transform, holoOrigin, new Vector3(0.9f, 1.3f, 0.55f), palette.Holo);
            var head = CreatePrimitive("Head", PrimitiveType.Sphere, holoRoot.transform, holoOrigin + new Vector3(0, 1.35f, 0), Vector3.one * 0.5f, palette.Holo);
            var leftArm = CreatePrimitive("Left Arm", PrimitiveType.Capsule, holoRoot.transform, holoOrigin + new Vector3(-0.9f, 0.25f, 0), new Vector3(0.28f, 0.9f, 0.28f), palette.Holo); leftArm.transform.rotation = Quaternion.Euler(0, 0, -22);
            var rightArm = CreatePrimitive("Right Arm", PrimitiveType.Capsule, holoRoot.transform, holoOrigin + new Vector3(0.9f, 0.25f, 0), new Vector3(0.28f, 0.9f, 0.28f), palette.Holo); rightArm.transform.rotation = Quaternion.Euler(0, 0, 22);
            CreatePrimitive("Hologram Base", PrimitiveType.Cylinder, holoRoot.transform, holoOrigin + new Vector3(0, -1.35f, 0), new Vector3(1.3f, 0.12f, 1.3f), palette.Holo);
            CreatePrimitive("Left Leg", PrimitiveType.Capsule, holoRoot.transform, holoOrigin + new Vector3(-0.36f, -1.0f, 0), new Vector3(0.3f, 0.7f, 0.3f), palette.Holo);
            CreatePrimitive("Right Leg", PrimitiveType.Capsule, holoRoot.transform, holoOrigin + new Vector3(0.36f, -1.0f, 0), new Vector3(0.3f, 0.7f, 0.3f), palette.Holo);
            RemoveColliders(environment); RemoveColliders(holoRoot);

            var cameraGo = new GameObject("Stage Camera", typeof(Camera), typeof(AudioListener), typeof(UniversalAdditionalCameraData)); cameraGo.tag = "MainCamera"; var camera = cameraGo.GetComponent<Camera>(); camera.transform.position = new Vector3(0, 13f, -12.2f); camera.transform.rotation = Quaternion.Euler(46, 0, 0); camera.fieldOfView = 52f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.018f, 0.035f, 0.06f); camera.farClipPlane = 100; camera.allowHDR = true; cameraGo.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.20f, 0.27f, 0.36f);
            var lightGo = new GameObject("Key Light", typeof(Light)); var light = lightGo.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2.15f; light.color = new Color(1f, 0.82f, 0.65f); light.shadows = LightShadows.Soft; lightGo.transform.rotation = Quaternion.Euler(50, -35, 0);
            CreatePointLight("Blue Fill Light", new Vector3(-3.5f, 5.5f, -1f), new Color(0.08f, 0.55f, 1f), 7.5f, 12f);
            CreatePointLight("Orange Fill Light", new Vector3(4.8f, 4.5f, -2.5f), new Color(1f, 0.26f, 0.035f), 8f, 10f);
            CreatePointLight("Warehouse Fill Light", new Vector3(-5.5f, 4f, 2.2f), new Color(0.18f, 0.65f, 1f), 5f, 8f);
            CreatePostProcessing();

            CreateWorldLabel("Warehouse Label", "智能仓库\n出库口", environment.transform, new Vector3(-6.2f, 3.35f, 1.2f), font, palette.Blue.color);
            CreateWorldLabel("Gate Label", "质检闸门", environment.transform, gate.transform.position + new Vector3(-0.8f, 2.35f, 0.4f), font, palette.Warning.color);
            CreateWorldLabel("Buffer Label", "关节模组\n缓存区", environment.transform, new Vector3(6.55f, 1.65f, -3.85f), font, palette.Orange.color);
            CreateWorldLabel("R01 Label", "R-01\n左臂关节订单", environment.transform, new Vector3(6.15f, 3.15f, 0.3f), font, palette.Holo.color);
            BuildUI(font, out var hud, out var orderLabel);
            var order = holoRoot.AddComponent<R01OrderDisplay>(); order.Configure(leftArm.GetComponent<Renderer>(), orderLabel);
            var runtime = new GameObject("Assembly Zero Runtime"); var renderer = runtime.AddComponent<BatchItemRenderer>(); renderer.Configure(cargoMesh, cargoMesh, cargoOrange, cargoBlue, palette.Blue, palette.Red, palette.Warning); var presentation = runtime.AddComponent<StagePresentationController>(); presentation.Configure(topologyOverlay, lineOverlay, gateIndicator, gateBarrier.transform); var driver = runtime.AddComponent<AssemblyZeroSimulationDriver>(); driver.Configure(config, renderer, hud, order, camera, presentation);

            EditorSceneManager.SaveScene(scene, ScenePath); EnsureBuildSettings(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Selection.activeGameObject = runtime;
            Debug.Log($"Assembly Zero EP01 scene generated at {ScenePath}. Previous active scene: {previous}");
        }

        [MenuItem("Tools/Assembly Zero/Build Windows Development Demo")]
        public static void BuildWindowsDevelopmentDemo()
        {
            EnsureBuildSettings();
            var output = Path.GetFullPath("Builds/AssemblyZeroEP01/AssemblyZeroEP01.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException($"EP01 development build failed: {report.summary.result}");
            Debug.Log($"Assembly Zero EP01 development build: {output}, {report.summary.totalSize} bytes, {report.summary.totalTime}.");
        }

        [MenuItem("Tools/Assembly Zero/Capture EP01 Preview")]
        public static void CapturePreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); var camera = UnityEngine.Object.FindFirstObjectByType<Camera>(); if (camera == null) throw new InvalidOperationException("EP01 stage camera not found."); var lineOverlay = GameObject.Find("Stage 4 - Transport Line"); if (lineOverlay != null) lineOverlay.SetActive(false);
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false); camera.targetTexture = target; camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply(); var output = Path.GetFullPath("AssemblyZeroPreview.png"); File.WriteAllBytes(output, texture.EncodeToPNG()); camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); Debug.Log($"Assembly Zero preview captured: {output} ({scene.name}).");
        }

        private static void CreateFactoryDressing(Transform parent, (Material Dark, Material Steel, Material Belt, Material Orange, Material Blue, Material Holo, Material Warning, Material Red) palette)
        {
            var panel = Mat("FloorPanel", new Color(0.11f, 0.15f, 0.19f), false);
            for (var z = -1; z <= 1; z++) for (var x = -1; x <= 1; x++)
                CreatePrimitive($"Floor Panel {x + 1}-{z + 1}", PrimitiveType.Cube, parent, new Vector3(x * 4.6f, -0.075f, z * 3.1f), new Vector3(4.42f, 0.035f, 2.92f), panel);

            CreatePrimitive("Neon Edge North", PrimitiveType.Cube, parent, new Vector3(0, -0.015f, 5.05f), new Vector3(15.2f, 0.025f, 0.055f), palette.Holo);
            CreatePrimitive("Neon Edge South", PrimitiveType.Cube, parent, new Vector3(0, -0.015f, -5.05f), new Vector3(15.2f, 0.025f, 0.055f), palette.Holo);
            CreatePrimitive("Neon Edge West", PrimitiveType.Cube, parent, new Vector3(-7.55f, -0.015f, 0), new Vector3(0.055f, 0.025f, 10.1f), palette.Holo);
            CreatePrimitive("Neon Edge East", PrimitiveType.Cube, parent, new Vector3(7.55f, -0.015f, 0), new Vector3(0.055f, 0.025f, 10.1f), palette.Holo);
            CreatePrimitive("Power Trunk Blue", PrimitiveType.Cube, parent, new Vector3(-5.35f, -0.005f, -0.25f), new Vector3(0.055f, 0.025f, 7.7f), palette.Blue);
            CreatePrimitive("Power Trunk Orange", PrimitiveType.Cube, parent, new Vector3(5.35f, -0.005f, 0.5f), new Vector3(0.055f, 0.025f, 6.8f), palette.Orange);

            CreateMachinePod("North East Fabricator", parent, new Vector3(6.15f, 0, 3.45f), palette.Steel, palette.Orange, palette.Holo);
            CreateMachinePod("West Compressor", parent, new Vector3(-6.05f, 0, -0.35f), palette.Steel, palette.Blue, palette.Orange);
            CreateMachinePod("South West Reactor", parent, new Vector3(-6.15f, 0, -3.65f), palette.Steel, palette.Orange, palette.Blue);
            CreateMachinePod("Center Analyzer", parent, new Vector3(0, 0, 0), palette.Steel, palette.Holo, palette.Orange, 0.72f);

            for (var i = 0; i < 9; i++)
            {
                var x = -4f + i;
                var support = CreatePrimitive($"Belt Support {i:00}", PrimitiveType.Cylinder, parent, new Vector3(x, -0.02f, -4f), new Vector3(0.11f, 0.18f, 0.11f), palette.Dark);
                support.transform.rotation = Quaternion.identity;
            }
        }

        private static void CreateMachinePod(string name, Transform parent, Vector3 position, Material steel, Material accent, Material glow, float scale = 1f)
        {
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position;
            CreatePrimitive("Base", PrimitiveType.Cylinder, root.transform, position + Vector3.up * 0.16f, new Vector3(0.85f, 0.16f, 0.85f) * scale, steel);
            CreatePrimitive("Core", PrimitiveType.Cylinder, root.transform, position + Vector3.up * 0.62f * scale, new Vector3(0.46f, 0.58f, 0.46f) * scale, accent);
            CreatePrimitive("Light Ring", PrimitiveType.Cylinder, root.transform, position + Vector3.up * 1.17f * scale, new Vector3(0.58f, 0.055f, 0.58f) * scale, glow);
            CreatePrimitive("Cap", PrimitiveType.Sphere, root.transform, position + Vector3.up * 1.31f * scale, Vector3.one * 0.28f * scale, glow);
        }

        private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name, typeof(Light)); go.transform.position = position; var light = go.GetComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        }

        private static void CreatePostProcessing()
        {
            const string profilePath = Root + "/Materials/FactoryPostProcessing.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true; bloom.threshold.Override(0.65f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f);
            if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
            color.active = true; color.postExposure.Override(0.65f); color.contrast.Override(12f); color.saturation.Override(18f); color.colorFilter.Override(new Color(1f, 0.98f, 0.94f));
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.active = true; tone.mode.Override(TonemappingMode.ACES);
            EditorUtility.SetDirty(profile);
            var volumeGo = new GameObject("Factory Post Processing", typeof(Volume)); var volume = volumeGo.GetComponent<Volume>(); volume.isGlobal = true; volume.priority = 10f; volume.sharedProfile = profile;
        }

        private static (Material Dark, Material Steel, Material Belt, Material Orange, Material Blue, Material Holo, Material Warning, Material Red) CreateMaterials()
        {
            return (UnlitMat("Dark", new Color(0.025f,0.045f,0.075f)), Mat("Steel", new Color(0.24f,0.31f,0.39f), false), ConveyorMat("Belt", 0f, 1f), Mat("ServoOrange", new Color(1f,0.22f,0.025f), true), Mat("SensorBlue", new Color(0.015f,0.52f,1f), true), Mat("Hologram", new Color(0.02f,0.9f,1f), true), Mat("Warning", new Color(1f,0.78f,0.05f), true), Mat("CompressedRed", new Color(1f,0.03f,0.05f), true));
        }
        private static Material UnlitMat(string name, Color color) { var path = Root + "/Materials/" + name + ".mat"; var shader = Shader.Find("Universal Render Pipeline/Unlit"); var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader; material.SetColor("_BaseColor", color); material.SetColor("_Color", color); return material; }
        private static Material ConveyorMat(string name, float corner, float turnSign)
        {
            var path = Root + "/Materials/" + name + ".mat"; var shader = Shader.Find("AssemblyZero/ConveyorSurface"); if (shader == null) throw new InvalidOperationException("AssemblyZero/ConveyorSurface shader was not imported."); var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.055f, 0.075f, 0.095f)); material.SetColor("_StripeColor", new Color(1f, 0.19f, 0.015f)); material.SetFloat("_FlowSpeed", 1.4f); material.SetFloat("_Tiling", 9f); material.SetVector("_Direction", new Vector4(1, 0, 0, 0)); material.SetFloat("_EmissionStrength", 1.8f);
            material.SetFloat("_Corner", corner); material.SetFloat("_TurnSign", turnSign); material.SetFloat("_SurfaceSize", 0.9f); material.SetFloat("_BeltHalfWidth", 0.39f); EditorUtility.SetDirty(material); return material;
        }
        private static Material Mat(string name, Color color, bool emission)
        {
            var path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); if (m == null) { m = new Material(shader) { name = name, enableInstancing = true }; AssetDatabase.CreateAsset(m, path); } else m.shader = shader;
            m.enableInstancing = true; m.SetColor("_BaseColor", color); m.SetColor("_Color", color); m.SetFloat("_Smoothness", emission ? 0.65f : 0.42f); if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2.2f); } else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); } EditorUtility.SetDirty(m); return m;
        }
        private static Material CargoMaterial(string name, Material source, Color tint)
        {
            var path = Root + "/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path); } else material.CopyPropertiesFromMaterial(source);
            material.shader = source.shader; material.enableInstancing = true; material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", tint * 0.35f); }
            if (material.HasProperty("_ReceiveShadows")) material.SetFloat("_ReceiveShadows", 0f);
            EditorUtility.SetDirty(material); return material;
        }
        private static Mesh GetCargoMesh()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoPrefabPath);
            var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
            if (filter == null || filter.sharedMesh == null) throw new FileNotFoundException("CarboardBox04 prefab or mesh is required for the EP01 cargo renderer.", CargoPrefabPath);
            return filter.sharedMesh;
        }
        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        { var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go; }
        private static GameObject CreateChildPrimitiveWorldScale(string name, PrimitiveType type, Transform parent, Vector3 worldOffset, Vector3 worldScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, true); go.transform.position = parent.position + parent.rotation * worldOffset; go.transform.rotation = parent.rotation;
            var parentScale = parent.lossyScale;
            go.transform.localScale = new Vector3(worldScale.x / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f), worldScale.y / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f), worldScale.z / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        private static void CreateArrow(string name, Transform parent, Vector3 position, GridDirection direction, Material material)
        {
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position; root.transform.rotation = Quaternion.Euler(0, DirectionYaw(direction), 0);
            CreatePrimitive("Shaft", PrimitiveType.Cube, root.transform, position, new Vector3(0.48f, 0.035f, 0.07f), material);
            CreateArrowHead(root.transform, position, root.transform.rotation, material);
        }
        private static void CreateCornerArrow(string name, Transform parent, Vector3 position, Vector2Int entryDelta, Vector2Int exitDelta, Material material)
        {
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position;
            var pivot = CornerPivot(position, entryDelta, exitDelta, out var entry, out var exit);
            CreateArc("Shaft", root.transform, pivot, entry, exit, 0.5f, 0f, 0.035f, 0.07f, material, 0.2f, 0.8f);
            var angle = 0.85f * Mathf.PI * 0.5f; var tangent = (entry * Mathf.Cos(angle) + exit * Mathf.Sin(angle)).normalized;
            var headRotation = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0, -90f, 0);
            CreateArrowHead(root.transform, ArcPoint(pivot, entry, exit, 0.5f, 0.85f) - tangent * 0.28f, headRotation, material);
        }
        private static void CreateArrowHead(Transform root, Vector3 position, Quaternion rotation, Material material)
        {
            var right = rotation * Vector3.right; var forward = rotation * Vector3.forward;
            var headL = CreatePrimitive("Head L", PrimitiveType.Cube, root, position + right * 0.28f + forward * 0.09f, new Vector3(0.22f, 0.035f, 0.055f), material); headL.transform.rotation = rotation * Quaternion.Euler(0, 35, 0);
            var headR = CreatePrimitive("Head R", PrimitiveType.Cube, root, position + right * 0.28f - forward * 0.09f, new Vector3(0.22f, 0.035f, 0.055f), material); headR.transform.rotation = rotation * Quaternion.Euler(0, -35, 0);
        }
        private static Vector3 CornerPivot(Vector3 center, Vector2Int entryDelta, Vector2Int exitDelta, out Vector3 entry, out Vector3 exit)
        {
            entry = new Vector3(entryDelta.x, 0, entryDelta.y); exit = new Vector3(exitDelta.x, 0, exitDelta.y);
            return center - entry * 0.5f + exit * 0.5f;
        }
        private static void CreateCenterStripe(Transform parent, Material material) { var go = CreateChildPrimitiveWorldScale("Center Lane", PrimitiveType.Cube, parent, new Vector3(0, 0.145f, 0), new Vector3(0.82f, 0.018f, 0.09f), material); var c = go.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
        private static void CreateCornerDressing(Transform parent, Vector2Int entryDelta, Vector2Int exitDelta, float turnSign, Material orange, Material dark)
        {
            var pivot = CornerPivot(parent.position, entryDelta, exitDelta, out var entry, out var exit);
            CreateArc("Center Lane", parent, pivot, entry, exit, 0.5f, 0.145f, 0.018f, 0.09f, orange);
            CreateArc("Left Rail", parent, pivot, entry, exit, 0.5f + 0.45f * turnSign, 0.18f, 0.09f, 0.045f, dark);
            CreateArc("Right Rail", parent, pivot, entry, exit, 0.5f - 0.45f * turnSign, 0.18f, 0.09f, 0.045f, dark);
        }

        private static Vector2Int[] BuildSerpentinePath()
        {
            var path = new List<Vector2Int>(45);
            AddRun(path, new Vector2Int(-4, 3), new Vector2Int(4, 3));
            AddRun(path, new Vector2Int(4, 2), new Vector2Int(4, 1));
            AddRun(path, new Vector2Int(3, 1), new Vector2Int(-3, 1));
            AddRun(path, new Vector2Int(-3, 0), new Vector2Int(-3, -1));
            AddRun(path, new Vector2Int(-2, -1), new Vector2Int(3, -1));
            AddRun(path, new Vector2Int(3, -2), new Vector2Int(3, -3));
            AddRun(path, new Vector2Int(2, -3), new Vector2Int(-4, -3));
            AddRun(path, new Vector2Int(-4, -4), new Vector2Int(5, -4));
            return path.ToArray();
        }

        private static void AddRun(List<Vector2Int> path, Vector2Int from, Vector2Int to)
        {
            var step = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
            for (var point = from;; point += step) { path.Add(point); if (point == to) break; }
        }
        private static void CreateArc(string name, Transform parent, Vector3 pivot, Vector3 entry, Vector3 exit, float radius, float height, float thickness, float width, Material material, float t0 = 0f, float t1 = 1f)
        {
            var root = new GameObject(name); root.transform.position = parent.position; root.transform.SetParent(parent, true);
            var segments = Mathf.Max(3, Mathf.CeilToInt(radius * Mathf.PI * 0.5f * (t1 - t0) / 0.12f));
            for (var k = 0; k < segments; k++)
            {
                var a = ArcPoint(pivot, entry, exit, radius, Mathf.Lerp(t0, t1, k / (float)segments)); var b = ArcPoint(pivot, entry, exit, radius, Mathf.Lerp(t0, t1, (k + 1) / (float)segments)); var chord = b - a;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = $"{name} {k}";
                go.transform.position = (a + b) * 0.5f + Vector3.up * height; go.transform.rotation = Quaternion.LookRotation(chord.normalized, Vector3.up); go.transform.localScale = new Vector3(width, thickness, chord.magnitude + width * 0.5f);
                go.GetComponent<Renderer>().sharedMaterial = material; go.transform.SetParent(root.transform, true);
            }
        }
        private static Vector3 ArcPoint(Vector3 pivot, Vector3 entry, Vector3 exit, float radius, float t) { var angle = t * Mathf.PI * 0.5f; return pivot + (entry * Mathf.Sin(angle) - exit * Mathf.Cos(angle)) * radius; }
        private static void RemoveColliders(GameObject root) { foreach (var c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c); }
        private static Mesh CreateMesh(PrimitiveType type) { var go = GameObject.CreatePrimitive(type); var mesh = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh); mesh.name = type + " Item Mesh"; UnityEngine.Object.DestroyImmediate(go); return mesh; }
        private static GridDirection Direction(Vector2Int delta) => delta == Vector2Int.up ? GridDirection.North : delta == Vector2Int.down ? GridDirection.South : delta == Vector2Int.left ? GridDirection.West : GridDirection.East;
        private static float DirectionYaw(GridDirection direction) => direction switch { GridDirection.North => -90f, GridDirection.South => 90f, GridDirection.West => 180f, _ => 0f };

        private static GameObject BuildUI(Font font, out MetricsHud hud, out UnityEngine.UI.Text orderLabel)
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var panel = UiRect("Metrics Panel", canvasGo.transform, new Vector2(18, -18), new Vector2(390, 625), new Vector2(0, 1)); var image = panel.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(0.015f, 0.025f, 0.045f, 0.88f); image.raycastTarget = false;
            var metrics = Text("Metrics", panel.transform, font, 21, TextAnchor.UpperLeft); Stretch(metrics.rectTransform, 18, 18, 18, 145);
            var graphGo = UiRect("Simulation Graph", panel.transform, new Vector2(18, 18), new Vector2(354, 110), new Vector2(0, 0)); hud = graphGo.AddComponent<MetricsHud>(); hud.color = new Color(0.2f, 1f, 0.78f, 0.9f); hud.raycastTarget = false;
            var narrativePanel = UiRect("Narrative Panel", canvasGo.transform, new Vector2(-18, -82), new Vector2(430, 325), new Vector2(1, 1)); var narrativeImage = narrativePanel.AddComponent<UnityEngine.UI.Image>(); narrativeImage.color = new Color(0.015f, 0.025f, 0.045f, 0.9f); narrativeImage.raycastTarget = false; var narrative = Text("Narrative", narrativePanel.transform, font, 22, TextAnchor.UpperLeft); Stretch(narrative.rectTransform, 24, 24, 24, 24);
            var stage = Text("Stage Header", canvasGo.transform, font, 25, TextAnchor.MiddleCenter); var sr = stage.rectTransform; sr.anchorMin = new Vector2(0.5f, 1); sr.anchorMax = new Vector2(0.5f, 1); sr.pivot = new Vector2(0.5f, 1); sr.anchoredPosition = new Vector2(0, -18); sr.sizeDelta = new Vector2(900, 42);
            orderLabel = Text("Order", canvasGo.transform, font, 26, TextAnchor.MiddleCenter); var or = orderLabel.rectTransform; or.anchorMin = new Vector2(0.5f, 0); or.anchorMax = new Vector2(0.5f, 0); or.pivot = new Vector2(0.5f, 0); or.anchoredPosition = new Vector2(0, 24); or.sizeDelta = new Vector2(800, 48);
            hud.Configure(metrics, stage, narrative); return canvasGo;
        }
        private static void CreateWorldLabel(string name, string value, Transform parent, Vector3 position, Font font, Color color)
        {
            var root = new GameObject(name, typeof(Canvas)); root.transform.SetParent(parent); root.transform.position = position; root.transform.rotation = Quaternion.Euler(55, 0, 0); root.transform.localScale = Vector3.one * 0.006f; var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 1;
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(520, 120); var background = root.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(0.01f, 0.02f, 0.035f, 0.82f); background.raycastTarget = false;
            var label = Text("Label", root.transform, font, 34, TextAnchor.MiddleCenter); Stretch(label.rectTransform, 16, 16, 8, 8); label.text = value; label.color = color;
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, Font font, int size, TextAnchor alignment) { var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false); var text = go.GetComponent<UnityEngine.UI.Text>(); text.font = font; text.fontSize = size; text.alignment = alignment; text.color = new Color(0.78f, 0.93f, 1f); text.raycastTarget = false; return text; }
        private static GameObject UiRect(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = position; r.sizeDelta = size; return go; }
        private static void Stretch(RectTransform r, float left, float right, float top, float bottom) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top); }
        private static Font GetOrCreateFont() { if (!File.Exists(FontSourcePath)) { var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts); var source = Path.Combine(fonts, "simhei.ttf"); if (!File.Exists(source)) throw new FileNotFoundException("Cannot build the Chinese UI because simhei.ttf was not found in the Windows Fonts folder.", source); File.Copy(source, FontSourcePath, true); AssetDatabase.ImportAsset(FontSourcePath, ImportAssetOptions.ForceSynchronousImport); } return AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath); }
        private static void EnsureFolders() { foreach (var folder in new[] { "Scenes", "Configs", "Materials", "Meshes", "Prefabs", "UI" }) { var path = Root + "/" + folder; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, folder); } }
        private static void EnsureBuildSettings() { var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); if (!list.Exists(x => x.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); } }
    }
}
