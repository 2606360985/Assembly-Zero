using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity
{
    /// <summary>A read-only documentary: follow one material's conversions, then its robot.</summary>
    public sealed class EP03CinematicCamera : MonoBehaviour
    {
        public EP03SimulationDriver Driver;
        public Camera FilmCamera;
        public bool AutoPlay;
        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public string ShotTitle { get; private set; } = "生产纪录";
        public string ShotDetail { get; private set; } = "沿物料流向，见证一台机器人的诞生";
        public int TrackedMaterialId { get; private set; }
        public int TrackedProductId { get; private set; }
        public int VisitedStationsMask { get; private set; }
        public int Conversions { get; private set; }
        public readonly List<string> ShotLog = new List<string>();

        private readonly Dictionary<string, FactoryRoutePlacement> paths = new Dictionary<string, FactoryRoutePlacement>();
        private readonly Dictionary<string, FactoryModuleAuthoring> modules = new Dictionary<string, FactoryModuleAuthoring>();
        private readonly Dictionary<string, string> moduleNames = new Dictionary<string, string>();
        private long began, shotBegan, lastTick = -1, closingBegan = -1;
        private string shotKey;
        private Vector3 blendPosition;
        private Quaternion blendRotation;
        private float blendFov;
        private bool manualCameraEnabled, statusVisible, autoStarted;
        private GameObject statusPanel;
        private Vector3 lastProductPosition;

        private void LateUpdate()
        {
            if (!autoStarted && Driver != null && Driver.Simulation != null)
            {
                autoStarted = true;
                if (AutoPlay) Begin();
            }
            Present();
        }

        public void Begin()
        {
            if (Driver == null || Driver.Simulation == null || FilmCamera == null) return;
            if (!IsPlaying)
            {
                var source = Driver.FreeCamera != null && Driver.FreeCamera.IsActive ? Driver.FreeCamera.View : Driver.Cameras.Camera;
                if (Driver.FreeCamera != null) Driver.FreeCamera.Stop();
                manualCameraEnabled = Driver.Cameras.Camera.enabled;
                statusPanel = Driver.Hud.transform.parent.gameObject;
                statusVisible = statusPanel.activeSelf;
                FilmCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                FilmCamera.fieldOfView = source.fieldOfView;
            }
            Driver.Cameras.Camera.enabled = false; FilmCamera.enabled = true;
            statusPanel.SetActive(false); IsPlaying = true; ResetStory(); Present();
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false; if (FilmCamera != null) FilmCamera.enabled = false;
            if (Driver != null && Driver.Cameras != null) Driver.Cameras.Camera.enabled = manualCameraEnabled;
            if (statusPanel != null) statusPanel.SetActive(statusVisible);
        }

        public void ResetStory()
        {
            began = Driver.Simulation.TickNumber; lastTick = -1; closingBegan = -1;
            TrackedMaterialId = 0; TrackedProductId = 0; VisitedStationsMask = 0; Conversions = 0;
            IsComplete = false; shotKey = null; ShotLog.Clear(); paths.Clear(); modules.Clear(); moduleNames.Clear();
            foreach (var route in Driver.Layout.Routes) paths.Add(route.Connection.Id, route);
            foreach (var module in Driver.Modules) modules.Add(module.InstanceId, module);
            foreach (var module in Driver.Simulation.Modules)
                moduleNames.Add(module.Definition.Id, module.Recipe?.Name ?? EP03SimulationDriver.MaterialName(module.Definition.Material) + (module.Definition.Warehouse ? "原料仓" : module.Definition.AssemblyLane >= 0 ? "总装供料" : "集散"));
        }

        public void Present()
        {
            if (!IsPlaying || Driver.Simulation == null) return;
            var world = Driver.Simulation; var tick = world.TickNumber;
            if (tick < lastTick) ResetStory();
            if (tick == lastTick) return;
            lastTick = tick;
            if (IsComplete) return;
            var seconds = (tick - began) / 60f;
            if (seconds < 5)
            {
                var pose = Driver.Cameras.Presets[0];
                var p = Vector3.Lerp(pose.Position + new Vector3(-10, 6, -6), pose.Position + new Vector3(6, -3, 2), Mathf.SmoothStep(0, 1, seconds / 5));
                Frame("opening", "01  一台机器人的诞生", "全厂建立镜头 · 从原料到成品", p, new Vector3(55, 0, 38), 46);
                return;
            }
            if (TrackedProductId != 0) { FollowProduct(); return; }
            if (TrackedMaterialId == 0)
            {
                // Plate is a body ingredient. Prefer its first metal parcel so the
                // documentary joins the robot before the first assembly operation.
                var route = world.Routes.FirstOrDefault(r => r.Definition.Material == FactoryCatalog.Metal && r.Definition.To.StartsWith("plate-") && r.Snapshot.Items.Count > 0);
                if (route != null) TrackedMaterialId = route.Snapshot.Items[0].ItemId.Value;
                else
                {
                    var warehouse = Driver.Modules.First(m => m.InstanceId == "warehouse-8");
                    ModuleFrame("supply", "02  原料启程", "金属供料 · 等待出料", warehouse.transform.position, 9); return;
                }
            }
            for (var hop = 0; hop < 12; hop++)
            {
                if (!world.Ledger.TryGetValue(TrackedMaterialId, out var material)) return;
                if (material.Type < 8 && material.Type != 3)
                {
                    // A mid-run raw parcel can become a limb. Cut to an unassigned
                    // body before following a robot, so none of its six stations is skipped.
                    var body = world.Ledger.Values.FirstOrDefault(m => m.Type == 3 && (m.Owner == FactoryOwner.Transit || m.Owner == FactoryOwner.Buffer || m.Owner == FactoryOwner.Assembly && world.Assembly.TryGetItemRecord(m.Id, out var b) && b.ProductId == 0));
                    if (body != null) { TrackedMaterialId = body.Id; continue; }
                    var cell = world.Modules.First(m => m.Recipe?.Output == 3);
                    ModuleFrame("body-ready", "04  六路部件准备就绪", "跟随下一台托盘产品 · 等待躯干部件", modules[cell.Definition.Id].transform.position, 7); return;
                }
                if (material.Owner == FactoryOwner.Consumed)
                {
                    var batch = world.Batches.First(b => b.Inputs.Contains(material.Id));
                    if (batch.Outputs.Length == 0) return;
                    TrackedMaterialId = batch.Outputs[0]; Conversions++; continue;
                }
                if (material.Owner == FactoryOwner.Transit)
                {
                    var route = world.Routes.First(r => r.Definition.Id == material.Location);
                    var item = route.Snapshot.Items.FirstOrDefault(i => i.ItemId.Value == material.Id);
                    var point = paths[material.Location].Sample(item.Distance, out var tangent);
                    var angle = Mathf.Sin((tick - began) / 600f) * .5f;
                    var offset = new Vector3(-4.6f + angle, 5.6f, -5.6f);
                    Frame("route/" + material.Location, material.Type >= 8 && material.Type <= 10 ? "02  原料启程" : material.Type >= 11 ? "03  零件流转" : "04  部件送往总装",
                        EP03SimulationDriver.MaterialName(material.Type) + " · 物料 #" + material.Id + " · " + moduleNames[route.Definition.From] + " → " + moduleNames[route.Definition.To],
                        point + offset, point + Vector3.up * .15f, 46); return;
                }
                if (material.Owner == FactoryOwner.Assembly)
                {
                    if (world.Assembly.TryGetItemRecord(material.Id, out var item))
                    {
                        if (item.ProductId != 0) { TrackedProductId = item.ProductId; FollowProduct(); return; }
                        if (item.Lane >= 0)
                        {
                            var point = Driver.AssemblyTopology.Supply[item.Lane].Sample(item.Distance, out _);
                            Frame("assembly-feed", "04  六路供料汇入总装", "跟随躯干部件 · 等待托盘到位", point + new Vector3(4, 3, -5), point + Vector3.up * .4f, 46); return;
                        }
                    }
                    ModuleFrame("assembly-buffer", "04  六路供料汇入总装", "独立供料 · 等待工位接收", Driver.AssemblyLayout.Inserters[0].transform.position, 7); return;
                }
                var module = world.Modules.First(m => m.Definition.Id == material.Location);
                var processing = material.Owner == FactoryOwner.Processing;
                var name = module.Recipe?.Name ?? EP03SimulationDriver.MaterialName(material.Type);
                var title = module.Recipe != null && module.Recipe.Output < 8 ? "04  部件加工与预装" : "03  零件加工";
                var detail = name + " · " + (processing ? "加工中" : "缓存待料") + " · 物料 #" + material.Id;
                if (processing && module.Batch != null) detail += " · " + Mathf.Clamp01((tick - module.Batch.Started) / (float)module.Recipe.Duration).ToString("P0");
                ModuleFrame("module/" + module.Definition.Id + "/" + processing, title, detail, modules[module.Definition.Id].transform.position, 7);
                return;
            }
        }

        private void FollowProduct()
        {
            var world = Driver.Simulation; var tick = world.TickNumber;
            var product = world.Assembly.Snapshot.Products.FirstOrDefault(p => p.Id == TrackedProductId);
            if (product != null)
            {
                var point = Driver.AssemblyTopology.Product.Sample(product.Distance, out _); lastProductPosition = point;
                var count = product.Sockets.Count(s => s.ItemId != 0);
                if (product.Completed || product.State == ProductState.Scanning)
                {
                    Frame("inspection", "06  完整机器人 · 成品检验", "P-" + product.Id.ToString("000") + " · " + count + "/6 部件安装完成",
                        point + new Vector3(5.8f, 3.3f, -4.2f), point + Vector3.up * 1.4f, 39); return;
                }
                var station = Mathf.Clamp(product.NextStation, 0, 5); VisitedStationsMask |= 1 << station;
                var author = Driver.AssemblyLayout.Inserters.OrderBy(a => a.StableId).ElementAt(station);
                var arm = world.Assembly.Snapshot.Inserters.First(a => a.Id == author.StableId);
                var held = arm.TargetProductId == product.Id && arm.HeldItem != 0;
                var center = point + Vector3.up * 1.2f;
                if (held) center = Vector3.Lerp(center, author.Grip.position, .22f);
                var offset = held ? new Vector3(5.6f, 3.6f, -4.6f) : new Vector3(7.2f, 5.5f, -7.8f);
                var phase = product.State == ProductState.Moving ? "托盘转运" : arm.TargetProductId == product.Id ? PhaseName(arm.State) : "工位待料";
                Frame("station/" + station + "/" + (held ? "action" : "wide"),
                    "05  总装 " + (station + 1) + "/6 · " + EP03SimulationDriver.MaterialName(author.PartTypeId),
                    "跟随 P-" + product.Id.ToString("000") + " · " + phase + " · 已安装 " + count + "/6",
                    point + offset, center, held ? 40 : 45); return;
            }
            if (closingBegan < 0) closingBegan = tick;
            var elapsed = (tick - closingBegan) / 60f;
            var queued = world.Finished.FirstOrDefault(p => p.Id == TrackedProductId);
            var position = queued != null ? Driver.FinishedRoot.position + new Vector3(world.Finished.TakeWhile(p => p.Id != TrackedProductId).Count() % 2 * 3, 0, world.Finished.TakeWhile(p => p.Id != TrackedProductId).Count() / 2 * 3) : lastProductPosition;
            var dispatched = world.ProductReceipts.Any(p => p.ProductId == TrackedProductId && p.DispatchedTick >= 0);
            var centerEnd = Vector3.Lerp(position + Vector3.up * 1.2f, Driver.FinishedRoot.position + new Vector3(1.5f, 1, 3), Mathf.Clamp01(elapsed / 8));
            Frame("closing", "07  成品出库", "P-" + TrackedProductId.ToString("000") + (dispatched ? " · 已完成出库" : " · 成品泊位等待出库"),
                centerEnd + Vector3.Lerp(new Vector3(5, 4, -6), new Vector3(12, 12, -14), Mathf.SmoothStep(0, 1, elapsed / 8)), centerEnd, 44);
            if (dispatched && elapsed >= 8)
            {
                IsComplete = true; ShotTitle = "生产影片完成"; ShotDetail = "从物料到 P-" + TrackedProductId.ToString("000") + " · 六工位装配与成品出库已完整跟拍";
            }
        }

        private void ModuleFrame(string key, string title, string detail, Vector3 point, float distance)
        {
            var orbit = Mathf.Sin((Driver.Simulation.TickNumber - began) / 900f) * 1.2f;
            var center = point + Vector3.up * .9f;
            Frame(key, title, detail, center + new Vector3(distance + orbit, distance * .65f, -distance), center, 45);
        }

        private void Frame(string key, string title, string detail, Vector3 position, Vector3 target, float fov)
        {
            if (shotKey != key)
            {
                shotKey = key; shotBegan = Driver.Simulation.TickNumber;
                blendPosition = FilmCamera.transform.position; blendRotation = FilmCamera.transform.rotation; blendFov = FilmCamera.fieldOfView;
                ShotLog.Add(shotBegan + ": " + title + " | " + detail);
            }
            ShotTitle = title; ShotDetail = detail;
            var t = Mathf.SmoothStep(0, 1, (Driver.Simulation.TickNumber - shotBegan) / 72f);
            // Distant chapter changes cut on action; nearby tracking angles ease in.
            if (Vector3.Distance(blendPosition, position) > 24) t = 1;
            FilmCamera.transform.SetPositionAndRotation(Vector3.Lerp(blendPosition, position, t), Quaternion.Slerp(blendRotation, Quaternion.LookRotation(target - position), t));
            FilmCamera.fieldOfView = Mathf.Lerp(blendFov, fov, t); FilmCamera.orthographic = false;
        }

        private static string PhaseName(InserterState state) => state switch
        {
            InserterState.Picking => "抓取来料", InserterState.Carrying => "搬运对位", InserterState.Placing => "定位安装", InserterState.Returning => "机械臂回程", _ => "等待来料与托盘"
        };

        private void OnDisable() => Stop();
    }
}
