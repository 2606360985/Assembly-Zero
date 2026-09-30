using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyZero.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace AssemblyZero.Unity.Editor
{
    public static class EP02SceneBuilder
    {
        public const string ScenePath = "Assets/_AssemblyZero/Scenes/EP02_Demo.unity";
        private const string Root = RobotPartConfigurationBuilder.Root;
        [MenuItem("Tools/Assembly Zero/Rebuild EP02 Demo Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scene changes before building EP02.");
            EnsureFolder(Root); foreach (var sub in new[] { "Configs", "Prefabs", "Materials" }) EnsureFolder(Root + "/" + sub);
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); scene.name = "EP02_Demo";
            var steel = Mat("Steel", new Color(.16f, .22f, .27f)); var dark = Mat("Dark", new Color(.035f, .06f, .085f)); var orange = Mat("SafetyOrange", new Color(1, .48f, .06f)); var cyan = Mat("SignalCyan", Color.cyan, true); var ghost = Mat("Ghost", new Color(.12f, .36f, .4f));
            var parts = RobotPartConfigurationBuilder.Build(steel);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.5f, .57f, .65f);
            var root = new GameObject("EP02 Assembly Cell"); var layout = root.AddComponent<EP02LayoutAuthoring>(); layout.Parts = parts;
            Primitive("Factory Floor", PrimitiveType.Cube, null, new Vector3(0, -.45f, 0), new Vector3(19, .5f, 15), dark);
            for (var i = -7; i <= 7; i++) Primitive("Floor Grid " + i, PrimitiveType.Cube, null, new Vector3(i * 1.2f, -.19f, 0), new Vector3(.025f, .015f, 13), steel);
            var light = new GameObject("Key Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(48, -32, 0); light.shadows = LightShadows.Soft;
            var fill = new GameObject("Cell Light").AddComponent<Light>(); fill.type = LightType.Point; fill.transform.position = new Vector3(0, 5, 0); fill.range = 18; fill.intensity = 2;
            var beltRoot = new GameObject("U-Line - 27 Modules"); var grid = new List<Vector2Int>();
            for (var y = 4; y >= -4; y--) grid.Add(new Vector2Int(-5, y)); for (var x = -4; x <= 5; x++) grid.Add(new Vector2Int(x, -4)); for (var y = -3; y <= 4; y++) grid.Add(new Vector2Int(5, y));
            var belts = new List<BeltAuthoring>();
            var beltMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Conveyor.mat");
            if (beltMat == null) { beltMat = new Material(Shader.Find("AssemblyZero/ConveyorSurface")); AssetDatabase.CreateAsset(beltMat, Root + "/Materials/Conveyor.mat"); }
            for (var i = 0; i < grid.Count; i++)
            {
                var go = new GameObject("EP02 Belt " + i.ToString("00")); go.transform.SetParent(beltRoot.transform); go.transform.position = new Vector3(grid[i].x * 1.2f, 0, grid[i].y * 1.2f);
                var b = go.AddComponent<BeltAuthoring>(); b.Grid = grid[i]; b.StablePieceId = i; b.StartsLine = i == 0; b.EndsLine = i == grid.Count - 1;
                var delta = i == grid.Count - 1 ? grid[i] - grid[i - 1] : grid[i + 1] - grid[i]; b.Direction = delta.x > 0 ? GridDirection.East : delta.x < 0 ? GridDirection.West : delta.y > 0 ? GridDirection.North : GridDirection.South;
                b.Shape = i > 0 && i < grid.Count - 1 && delta != grid[i] - grid[i - 1] ? BeltPieceShape.Corner90 : BeltPieceShape.Straight; b.SpeedUnitsPerTick = 32; belts.Add(b);
                Primitive("Housing", PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(1.18f, .28f, 1.18f), steel);
                var surface = Primitive("Moving Surface", PrimitiveType.Cube, go.transform, new Vector3(0, .17f, 0), new Vector3(1.08f, .035f, 1.08f), beltMat); surface.transform.localRotation = Quaternion.Euler(0, b.Direction == GridDirection.East ? 90 : b.Direction == GridDirection.South ? 180 : b.Direction == GridDirection.West ? 270 : 0, 0);
                for (var side = -1; side <= 1; side += 2) { var alongX = delta.x != 0; Primitive("Lane Guide", PrimitiveType.Cube, go.transform, alongX ? new Vector3(0, .2f, side * .54f) : new Vector3(side * .54f, .2f, 0), alongX ? new Vector3(1.16f, .07f, .04f) : new Vector3(.04f, .07f, 1.16f), orange); }
            }
            layout.Belts = belts.ToArray();
            var table = new GameObject("Final Assembly Table"); Primitive("Table Base", PrimitiveType.Cylinder, table.transform, new Vector3(0, .35f, 0), new Vector3(3.8f, .35f, 3.8f), steel);
            table.transform.rotation = Quaternion.Euler(0, 180, 0);
            Primitive("Table Surface", PrimitiveType.Cylinder, table.transform, new Vector3(0, .71f, 0), new Vector3(3.9f, .035f, 3.9f), dark);
            var sockets = new List<AssemblySocketAuthoring>();
            foreach (var part in parts)
            {
                var go = new GameObject(part.PartName + "Socket"); go.transform.SetParent(table.transform, false); go.transform.localPosition = part.AssemblySocketPosition;
                var socket = go.AddComponent<AssemblySocketAuthoring>(); socket.StableId = part.SocketId; socket.PartTypeId = part.PartTypeId;
                socket.Indicator = Primitive("Socket Indicator", PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * .15f, cyan).GetComponent<Renderer>();
                var preview = (GameObject)PrefabUtility.InstantiatePrefab(part.SourcePrefab); preview.name = part.PartName + " Ghost"; preview.transform.SetParent(go.transform, false); part.AssemblyPose.Apply(preview.transform);
                foreach (var r in preview.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(ghost, r.sharedMaterials.Length).ToArray(); socket.Ghost = preview; sockets.Add(socket);
            }
            layout.Sockets = sockets.ToArray();
            var cell = root.AddComponent<AssemblyCellPresenter>(); cell.Layout = layout;
            var ring = new List<Renderer>(); for (var i = 0; i < 36; i++) { var angle = i * Mathf.PI * 2 / 36; var r = Primitive("Progress " + i, PrimitiveType.Cube, table.transform, new Vector3(Mathf.Sin(angle) * 2, .78f, Mathf.Cos(angle) * 2), new Vector3(.19f, .045f, .09f), cyan); r.transform.localRotation = Quaternion.Euler(0, i * 10, 0); ring.Add(r.GetComponent<Renderer>()); } cell.ProgressRing = ring.ToArray();
            var scan = Mat("ScanningGlass", new Color(.05f, .95f, 1, .22f), true); scan.SetFloat("_Surface", 1); scan.SetFloat("_SrcBlend", 5); scan.SetFloat("_DstBlend", 10); scan.SetFloat("_ZWrite", 0); scan.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); scan.renderQueue = 3000; EditorUtility.SetDirty(scan);
            cell.Scanner = Primitive("Scanning Plane", PrimitiveType.Cylinder, table.transform, new Vector3(0, 1, 0), new Vector3(3, .012f, 3), scan).transform; cell.Scanner.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(table, Root + "/Prefabs/AssemblyTable.prefab");
            var armPrefab = CreateArmPrefab(steel, orange, cyan);
            var basePositions = new[] { new Vector3(-1.8f, 0, -3.3f), new Vector3(1.8f, 0, -3.3f), new Vector3(-4.6f, 0, 1.4f), new Vector3(4.6f, 0, 1.4f), new Vector3(-4.6f, 0, -1.8f), new Vector3(4.6f, 0, -1.8f) };
            var pickupIndices = new[] { 12, 15, 3, 23, 6, 20 }; var arms = new List<InserterAuthoring>();
            for (var i = 0; i < 6; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(armPrefab); go.name = "I-" + i.ToString("00") + " " + parts[i].PartName; go.transform.position = basePositions[i]; var a = go.GetComponent<InserterAuthoring>();
                a.StableId = i; a.PartTypeId = parts[i].PartTypeId; a.SocketId = parts[i].SocketId; a.PreferredDistance = Mathf.RoundToInt(pickupIndices[i] / 26f * 27 * 1024); a.PhaseTicks = i * 3; a.CarryingTicks += i % 3 * 2; a.ReturningTicks += i % 2 * 2; arms.Add(a);
                WorldLabel("I-" + i.ToString("00") + " / " + parts[i].PartName, go.transform.position + new Vector3(0, .01f, -.6f), Quaternion.Euler(90, 0, 0));
            }
            layout.Inserters = arms.ToArray();
            var armPresenter = root.AddComponent<InserterPresenter>(); armPresenter.Layout = layout;
            var animationOnly = Primitive("Animation Only - No Logistics Authority", PrimitiveType.Sphere, null, new Vector3(3, 2, -2.8f), Vector3.one * .3f, ghost); armPresenter.AnimationOnlyGrip = animationOnly.transform;
            Primitive("Animation Compare Stand", PrimitiveType.Cube, animationOnly.transform, new Vector3(0, -.8f, 0), new Vector3(.2f, 2, .2f), ghost);
            var sourceCabinet = Primitive("Six Part Source", PrimitiveType.Cube, null, new Vector3(-6, 1, 6.2f), new Vector3(2, 2.4f, 1.6f), steel);
            for (var i = 0; i < 6; i++) Primitive(parts[i].PartName + " Bin", PrimitiveType.Cube, sourceCabinet.transform, new Vector3((i % 3 - 1) * .28f, i / 3 * .25f, -.55f), new Vector3(.23f, .2f, .1f), Mat(parts[i].PartName, parts[i].Color));
            var reject = Primitive("Rate Limited Recovery - 1 part per 60 ticks", PrimitiveType.Cube, null, new Vector3(6, .55f, 6), new Vector3(2, 1.3f, 1.5f), orange); layout.RejectPoint = reject.transform;
            WorldLabel("六类部件 / SOURCE", new Vector3(-6, 2.6f, 5.35f), Quaternion.identity);
            WorldLabel("回收 / 60 Tick 每件", new Vector3(6, 1.7f, 5.2f), Quaternion.identity);
            WorldLabel("最终装配单元", new Vector3(0, .04f, -2.55f), Quaternion.Euler(90, 0, 0));
            for (var side = -1; side <= 1; side += 2) { Primitive("Safety Rail", PrimitiveType.Cube, null, new Vector3(side * 8, .8f, 0), new Vector3(.06f, .08f, 12), orange); for (var z = -5; z <= 5; z += 2) Primitive("Safety Post", PrimitiveType.Cylinder, null, new Vector3(side * 8, .3f, z), new Vector3(.07f, .7f, .07f), steel); }
            var camera = new GameObject("EP02 Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.backgroundColor = new Color(.025f, .045f, .065f); camera.clearFlags = CameraClearFlags.SolidColor; camera.nearClipPlane = .1f; camera.farClipPlane = 80; camera.gameObject.AddComponent<AudioListener>();
            var director = root.AddComponent<EP02CameraDirector>(); director.Camera = camera;
            director.Presets = new[] { CameraPose("Overview", new Vector3(0, 15, -18), new Vector3(0, 0, 0), 53), CameraPose("Pickup & Arbitration", new Vector3(-5.5f, 6, -9), new Vector3(-1.5f, 1, -3), 58), CameraPose("Final Assembly", new Vector3(0, 7.5f, -12), new Vector3(0, 1.3f, 0), 50), CameraPose("Recovery Backpressure", new Vector3(13, 8, 9), new Vector3(5, .3f, 1), 60) };
            var scenario = AssetDatabase.LoadAssetAtPath<EP02ScenarioConfig>(Root + "/Configs/EP02Scenario.asset"); if (scenario == null) { scenario = ScriptableObject.CreateInstance<EP02ScenarioConfig>(); AssetDatabase.CreateAsset(scenario, Root + "/Configs/EP02Scenario.asset"); }
            var driver = root.AddComponent<EP02SimulationDriver>(); driver.Layout = layout; driver.Scenario = scenario; driver.InitialStage = 6; driver.ArmPresenter = armPresenter; driver.CellPresenter = cell; driver.CameraDirector = director;
            driver.PartPresenter = root.AddComponent<PartVisualPresenter>(); driver.PartPresenter.Layout = layout;
            driver.AudioPresenter = root.AddComponent<EP02AudioPresenter>(); driver.AudioPresenter.Source = root.AddComponent<AudioSource>(); driver.AudioPresenter.Source.playOnAwake = false;
            driver.Hud = BuildHud(driver); var stages = root.AddComponent<EP02DemoStageController>(); stages.Driver = driver;
            layout.BakeAndValidate(); for (var stage = 1; stage <= 6; stage++) scenario.Create(stage, layout, BeltTopologyBuilder.Bake(layout.Belts, 2, layout.LaneSeparation));
            director.SelectStage(6); EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            var settings = EditorBuildSettings.scenes.ToList(); if (!settings.Any(x => x.path == ScenePath)) settings.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = settings.ToArray();
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
            Debug.Log("EP02 built: 27 modules, two lanes, six parts/arms/sockets; original prefabs unchanged.");
        }
        private static GameObject CreateArmPrefab(Material steel, Material orange, Material cyan)
        {
            var path = Root + "/Prefabs/ThreeJointInserter.prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("Three Joint Inserter"); var a = root.AddComponent<InserterAuthoring>();
            Primitive("Base", PrimitiveType.Cylinder, root.transform, new Vector3(0, .25f, 0), new Vector3(.8f, .25f, .8f), steel);
            a.Shoulder = Primitive("Shoulder Joint", PrimitiveType.Sphere, root.transform, Vector3.up * .8f, Vector3.one * .3f, orange).transform;
            a.Elbow = Primitive("Elbow Joint", PrimitiveType.Sphere, root.transform, new Vector3(0, 2.5f, 0), Vector3.one * .25f, orange).transform;
            var grip = new GameObject("Wrist & Grip"); grip.transform.SetParent(root.transform); grip.transform.localPosition = Vector3.up * 2; a.Grip = grip.transform;
            Primitive("Wrist", PrimitiveType.Sphere, grip.transform, Vector3.zero, Vector3.one * .22f, steel);
            for (var side = -1; side <= 1; side += 2) Primitive("Gripper Finger", PrimitiveType.Cube, grip.transform, new Vector3(side * .17f, -.1f, 0), new Vector3(.06f, .28f, .14f), orange);
            a.Links = new[] { Primitive("Upper Link", PrimitiveType.Cylinder, root.transform, Vector3.up, new Vector3(.12f, 1, .12f), steel).transform, Primitive("Fore Link", PrimitiveType.Cylinder, root.transform, Vector3.up * 2, new Vector3(.12f, 1, .12f), steel).transform };
            a.StatusLight = Primitive("Status", PrimitiveType.Sphere, root.transform, new Vector3(0, .6f, -.3f), Vector3.one * .13f, cyan).GetComponent<Renderer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }
        private static EP02CameraPose CameraPose(string name, Vector3 p, Vector3 target, float fov) => new EP02CameraPose { Name = name, Position = p, Euler = Quaternion.LookRotation(target - p).eulerAngles, FieldOfView = fov };
        private static void WorldLabel(string label, Vector3 position, Quaternion rotation)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");
            var go = new GameObject(label); go.transform.SetPositionAndRotation(position, rotation); var text = go.AddComponent<TextMesh>(); text.font = font; text.fontSize = 40; text.characterSize = .08f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.8f, .94f, 1); text.text = label; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        private static EP02MetricsHud BuildHud(EP02SimulationDriver driver)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf"); if (font == null) throw new InvalidOperationException("Existing EP01 Chinese font is required; no OS-specific font copy is performed.");
            var canvasGo = new GameObject("EP02 HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            var hud = canvasGo.AddComponent<EP02MetricsHud>();
            hud.Title = Text("Title", canvasGo.transform, font, new Vector2(28, -24), new Vector2(1300, 90), 28);
            var left = Panel("Metrics Panel", canvasGo.transform, new Vector2(24, -138), new Vector2(505, 342)); hud.Metrics = Text("Metrics", left, font, new Vector2(16, -15), new Vector2(475, 312), 20);
            var right = Panel("Inserter Debug Panel", canvasGo.transform, new Vector2(1425, -140), new Vector2(471, 808)); hud.Arms = Text("Per Arm", right, font, new Vector2(16, -16), new Vector2(438, 780), 18);
            var lesson = Panel("Lesson", canvasGo.transform, new Vector2(24, -840), new Vector2(860, 137)); hud.Lesson = Text("Explanation", lesson, font, new Vector2(16, -15), new Vector2(825, 115), 23);
            Text("Controls", canvasGo.transform, font, new Vector2(28, -990), new Vector2(1850, 65), 23).text = "1—6 阶段   R 重播   Space 暂停   N 单步   L Head 锁定   C 镜头    /    RepinSKY · 游戏工程侦探";
            for (var i = 1; i <= 6; i++) { var stage = i; var rect = Panel("Stage " + i, canvasGo.transform, new Vector2(28 + (i - 1) * 80, -110), new Vector2(68, 34)); var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true; var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(() => driver.ResetStage(stage)); Text("Number", rect, font, new Vector2(24, -4), new Vector2(42, 30), 22).text = i.ToString(); }
            // Persistent runtime callbacks are connected in EP02HudStageButton below; delegate listeners aren't serialized by Unity.
            for (var i = 1; i <= 6; i++) { var t = canvasGo.transform.Find("Stage " + i); var binding = t.gameObject.AddComponent<EP02HudStageButton>(); binding.Driver = driver; binding.Stage = i; }
            return hud;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = position; r.sizeDelta = size; var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.025f, .055f, .08f, .9f); image.raycastTarget = false; return r;
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = position; r.sizeDelta = size; var t = go.GetComponent<UnityEngine.UI.Text>(); t.font = font; t.fontSize = fontSize; t.color = new Color(.85f, .95f, 1); t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        public static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); var r = go.GetComponent<Renderer>(); r.sharedMaterial = material; return go;
        }
        private static Material Mat(string name, Color color, bool emission = false)
        {
            var path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m; m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .32f); if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color); } AssetDatabase.CreateAsset(m, path); return m;
        }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        public static string LastBuildResult { get; private set; }
        [MenuItem("Tools/Assembly Zero/Build EP02 Windows Development Player")]
        public static void BuildDevelopment()
        {
            var directory = "Build/EP02"; Directory.CreateDirectory(directory);
            var scenes = new[] { ScenePath }.Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != ScenePath).Select(s => s.path)).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, directory + "/AssemblyZero.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            LastBuildResult = report.summary.result + " / " + report.summary.totalErrors + " errors / " + report.summary.totalSize + " bytes";
            Debug.Log("EP02 development build: " + LastBuildResult);
        }
    }
}
