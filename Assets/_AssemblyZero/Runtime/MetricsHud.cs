using System.Collections.Generic;
using AssemblyZero.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyZero.Unity
{
    public sealed class MetricsHud : MaskableGraphic
    {
        [SerializeField] private UnityEngine.UI.Text metricsText;
        [SerializeField] private UnityEngine.UI.Text stageText;
        [SerializeField] private UnityEngine.UI.Text narrativeText;
        private readonly Queue<float> samples = new Queue<float>(300);
        private string mode;
        private SimulationStatistics statistics;
        private double simulationMs;
        private double presentationMs;
        private int pieces, lines, visible, batches, physicalCapacity;
        private float samplingRatio = 1f;
        private string debugItems = string.Empty;

        public void Configure(UnityEngine.UI.Text metrics, UnityEngine.UI.Text stage, UnityEngine.UI.Text narrative) { metricsText = metrics; stageText = stage; narrativeText = narrative; }

        public void Refresh(string backendMode, SimulationStatistics stats, double simMs, double presentMs, int beltPieces, int transportLines, int visibleItems, int batchCount, int linePhysicalCapacity, float visibleSamplingRatio)
        { mode = backendMode == "Naive" ? "逐件算法" : backendMode == "Gap" ? "间隙算法" : backendMode; statistics = stats; simulationMs = simMs; presentationMs = presentMs; pieces = beltPieces; lines = transportLines; visible = visibleItems; batches = batchCount; physicalCapacity = linePhysicalCapacity; samplingRatio = Mathf.Max(1f, visibleSamplingRatio); if (samples.Count >= 300) samples.Dequeue(); samples.Enqueue((float)simMs); UpdateText(); SetVerticesDirty(); }
        public void SetPresentationMs(double value) { presentationMs = value; UpdateText(); }
        public void SetStage(int stage) { if (stageText != null) stageText.text = $"EP01 / 阶段 {stage}    [1-6] 切换    [R] 重播    [G] 闸门"; }
        public void SetNarrative(int stage, bool gateOpen, long tick)
        {
            if (narrativeText == null) return;
            narrativeText.text = stage switch
            {
                1 => "01  传送带拓扑\n\n白色箭头：每个传送带段的输出方向。\n青色轨迹：有效连接。\n\n32 个物理段会烘焙为纯逻辑运输数据。",
                2 => "02  逐件算法 / 真实货箱\n\n纸箱按真实尺寸与安全间距运输。\n每个 Tick 都访问物料并校验最小间距。\n\n橙色：舵机核心\n蓝色：传感器包",
                3 => "03  逐件算法 / 20,000 件\n\nDomain 真实运行两万件逻辑物料。\n画面均匀抽样 128 个代表箱，避免重叠。\n\n观察“模拟耗时”和“本 Tick 访问值”。",
                4 => "04  运输线烘焙\n\n32 个传送带段 → 1 条运输线\n2 条物理车道 → 2 条逻辑车道\n\n青色覆盖层表示合并后的运输线。",
                5 => "05  间隙算法 / 20,000 件\n\n与 Stage 3 使用相同逻辑数量和抽样渲染。\n蓝色：内部间隙　黄色：首尾间隙\n红色：压缩边界\n\n位置展开计入“表现耗时”。",
                _ => $"06  质检闸门 / 堵塞传播\n\n闸门：{(gateOpen ? "开启" : "关闭")}    Tick：{tick}\n\n关闭后，红色压缩前沿向入口传播。\n重新开启后，队列流向缓存区。\n\n[R] 重播    [G] 手动控制"
            };
        }
        public void SetDebugItems(IReadOnlyList<BeltItemSnapshot> items, bool enabled) { if (!enabled) { debugItems = string.Empty; return; } var count = Mathf.Min(4, items.Count); debugItems = "\n\n物料 ID / 逻辑距离"; for (var i = 0; i < count; i++) debugItems += $"\n#{items[i].ItemId.Value}  {items[i].Distance}"; }
        private void UpdateText()
        {
            if (metricsText == null) return;
            var presentationMode = samplingRatio > 1.001f ? $"均匀抽样  1:{Mathf.RoundToInt(samplingRatio)}" : "全部显示";
            metricsText.text = $"模式  {mode}\nTick  {statistics.Tick:n0}\n模拟耗时  {simulationMs:F3} ms\n表现耗时  {presentationMs:F3} ms\n逻辑物料  {statistics.LogicalItems:n0}\n可见货箱  {visible:n0}\n表现方式  {presentationMode}\n物理容量  {physicalCapacity:n0}\n传送带段  {pieces}\n运输线  {lines}\n活动线路  {statistics.ActiveLines}\n本 Tick 访问值  {statistics.ValuesTouched:n0}\n出库数量  {statistics.SourceCount:n0}\n入库数量  {statistics.SinkCount:n0}\n闸门  {(statistics.GateOpen ? "开启" : "关闭")}\n实例批次  {batches}\n状态哈希  {statistics.StateHash:X16}{debugItems}";
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (samples.Count < 2) return; var rect = rectTransform.rect; var max = 0.01f; foreach (var s in samples) if (s > max) max = s; var values = samples.ToArray(); var color32 = (Color32)color;
            for (var i = 1; i < values.Length; i++) { var a = new Vector2(rect.xMin + rect.width * (i - 1) / 299f, rect.yMin + rect.height * values[i - 1] / max); var b = new Vector2(rect.xMin + rect.width * i / 299f, rect.yMin + rect.height * values[i] / max); AddLine(vh, a, b, 1.5f, color32); }
        }
        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color) { var n = ((b - a).normalized); n = new Vector2(-n.y, n.x) * width; var start = vh.currentVertCount; vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero); vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero); vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3); }
    }

}
