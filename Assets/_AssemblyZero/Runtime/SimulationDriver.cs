using System;
using System.Collections.Generic;
using System.Diagnostics;
using AssemblyZero.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity
{
    public sealed class AssemblyZeroSimulationDriver : MonoBehaviour
    {
        [SerializeField] private ScenarioConfig scenarioConfig;
        [SerializeField] private BatchItemRenderer itemRenderer;
        [SerializeField] private MetricsHud metricsHud;
        [SerializeField] private R01OrderDisplay orderDisplay;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private StagePresentationController presentationController;
        private IBeltSimulationBackend backend;
        private readonly BeltSnapshotWriter snapshot = new BeltSnapshotWriter();
        private BakedBeltTopology topology;
        private double accumulator;
        private long tick;
        private bool? gateOverride;
        private int stage = 1;
        private double simulationMs;
        private double presentationMs;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private ScenarioDefinition activeScenario;
        private int physicalCapacity;

        public int Stage => stage;
        public IBeltSimulationBackend Backend => backend;
        public BakedBeltTopology Topology => topology;
        public ScenarioConfig Config => scenarioConfig;

        public void Configure(ScenarioConfig config, BatchItemRenderer renderer, MetricsHud hud, R01OrderDisplay order, Camera camera, StagePresentationController presentation)
        { scenarioConfig = config; itemRenderer = renderer; metricsHud = hud; orderDisplay = order; stageCamera = camera; presentationController = presentation; }

        private void Awake()
        {
            if (scenarioConfig == null) scenarioConfig = ScriptableObject.CreateInstance<ScenarioConfig>();
            var pieces = FindObjectsByType<BeltAuthoring>(FindObjectsSortMode.None); Array.Sort(pieces, (a, b) => a.StablePieceId.CompareTo(b.StablePieceId)); topology = BeltTopologyBuilder.Bake(pieces);
            physicalCapacity = CalculatePhysicalCapacity(topology, scenarioConfig.PhysicalMinimumSpacing);
            ResetStage(1);
        }

        private void Update()
        {
            ReadInput();
            accumulator += Time.unscaledDeltaTime;
            var step = 1.0 / BeltConstants.TickRate; var catchUp = 0;
            while (accumulator >= step && catchUp++ < Math.Max(1, scenarioConfig.MaxCatchUpTicks)) { RunTick(); accumulator -= step; }
            Present();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) ResetStage(1); else if (keyboard.digit2Key.wasPressedThisFrame) ResetStage(2); else if (keyboard.digit3Key.wasPressedThisFrame) ResetStage(3); else if (keyboard.digit4Key.wasPressedThisFrame) ResetStage(4); else if (keyboard.digit5Key.wasPressedThisFrame) ResetStage(5); else if (keyboard.digit6Key.wasPressedThisFrame) ResetStage(6);
            if (stage == 6 && keyboard.rKey.wasPressedThisFrame) ResetStage(6);
            if (stage == 6 && keyboard.gKey.wasPressedThisFrame) gateOverride = !(gateOverride ?? backend.Statistics.GateOpen);
        }

        private void RunTick()
        {
            tick++;
            bool? scheduledGate = null;
            if (stage == 3 || stage == 5) scheduledGate = false;
            if (stage == 6) scheduledGate = tick < scenarioConfig.GateCloseTick || tick >= scenarioConfig.GateOpenTick;
            stopwatch.Restart(); backend.Tick(new TickInput(tick, gateOverride ?? scheduledGate)); stopwatch.Stop(); simulationMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        private void Present()
        {
            stopwatch.Restart(); backend.CreateSnapshot(snapshot); itemRenderer?.SetSnapshot(snapshot.Items, topology, stage, activeScenario?.VisibleItems ?? 0); metricsHud?.SetDebugItems(snapshot.Items, stage == 2); metricsHud?.Refresh(backend.Name, snapshot.Statistics, simulationMs, 0, topology?.BeltPieceCount ?? 0, topology?.DomainTopology.Lines.Length ?? 0, itemRenderer?.VisibleCount ?? 0, itemRenderer?.BatchCount ?? 0, physicalCapacity, itemRenderer?.SamplingRatio ?? 1f); orderDisplay?.SetProgress(snapshot.Statistics.OrderProgress, scenarioConfig.OrderTarget); stopwatch.Stop(); presentationMs = stopwatch.Elapsed.TotalMilliseconds;
            metricsHud?.SetPresentationMs(presentationMs); metricsHud?.SetNarrative(stage, snapshot.Statistics.GateOpen, snapshot.Statistics.Tick); presentationController?.SetGate(snapshot.Statistics.GateOpen);
        }

        public void ResetStage(int value)
        {
            stage = Mathf.Clamp(value, 1, 6); tick = 0; accumulator = 0; gateOverride = null;
            var stress = stage == 3 || stage == 5;
            var count = stage switch { 1 => 0, 2 => scenarioConfig.SmallLoadItems, 3 => scenarioConfig.StressLogicalItems, 4 => scenarioConfig.SmallLoadItems, 5 => scenarioConfig.StressLogicalItems, _ => scenarioConfig.GateDemoItems };
            var initial = stage switch { 2 => Math.Max(1, count / 4), 4 => count / 2, 6 => count / 2, _ => 0 };
            var scenario = stress ? scenarioConfig.CreateStress() : scenarioConfig.CreatePhysical(count, initial);
            if (stage == 6) { scenario.GateCloseTick = scenarioConfig.GateCloseTick; scenario.GateOpenTick = scenarioConfig.GateOpenTick; }
            if (stage == 6 && topology.DomainTopology.Lanes.Length > 0) scenario.InitialFrontDistance = Mathf.Max(initial, topology.DomainTopology.Lanes[0].Length - 128);
            activeScenario = scenario;
            backend = stage >= 5 ? new GapTransportLineSimulation() : new NaiveItemBeltSimulation(); backend.Reset(scenario, topology.DomainTopology);
            ApplyCamera(stage); itemRenderer?.SetDebugMode(stage); metricsHud?.SetStage(stage); presentationController?.ApplyStage(stage); orderDisplay?.SetProgress(0, scenarioConfig.OrderTarget);
            // Refresh immediately so a stage switch never displays the previous stage's sample
            // count or sampling ratio for a frame (important when replaying Stage 6).
            Present();
        }

        private static int CalculatePhysicalCapacity(BakedBeltTopology baked, int minimumSpacing)
        {
            if (baked?.DomainTopology?.Lanes == null || minimumSpacing <= 0) return 0;
            var capacity = 0;
            foreach (var lane in baked.DomainTopology.Lanes) capacity += lane.Length / minimumSpacing;
            return capacity;
        }

        private void ApplyCamera(int value)
        {
            if (stageCamera == null) stageCamera = Camera.main; if (stageCamera == null) return;
            var positions = new[] { new Vector3(0, 13, -12), new Vector3(-3, 9, -9), new Vector3(0, 16, -15), new Vector3(2, 12, -12), new Vector3(-2, 10, -10), new Vector3(0, 13, -12) };
            stageCamera.transform.position = positions[value - 1]; stageCamera.transform.rotation = Quaternion.Euler(43, value == 2 ? 20 : 0, 0);
        }
    }
}
