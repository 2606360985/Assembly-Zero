using System;
using System.Diagnostics;
using AssemblyZero.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
namespace AssemblyZero.Unity
{
    public sealed class EP02SimulationDriver : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public EP02ScenarioConfig Scenario;
        public InserterPresenter ArmPresenter;
        public PartVisualPresenter PartPresenter;
        public AssemblyCellPresenter CellPresenter;
        public EP02MetricsHud Hud;
        public EP02CameraDirector CameraDirector;
        public EP02AudioPresenter AudioPresenter;
        public bool Paused;
        public string StartupError { get; private set; }
        public EP02Simulation Simulation { get; private set; }
        public EP02BakedLayout Topology { get; private set; }
        public double PresentationMs { get; private set; }
        public float Fraction => Paused ? 0 : (float)(accumulator * BeltConstants.TickRate);
        private double accumulator;
        private int sequence;
        private readonly Stopwatch clock = new Stopwatch();
        private MaterialPropertyBlock block;
        private void Start() { Restart(); }
        public void Restart()
        {
            try
            {
                if (Layout == null || Scenario == null || PartPresenter == null || CellPresenter == null || ArmPresenter == null || AudioPresenter == null || CameraDirector == null || Hud == null)
                    throw new InvalidOperationException("EP02 production driver is missing required references.");
                PartPresenter.Clear(); CellPresenter.Clear(); AudioPresenter.ResetEvents();
                Topology = Layout.BakeAndValidate();
                Simulation = new EP02Simulation(Scenario.Create(Layout, Topology), Topology.SupplyTopology, Scenario.Backend);
                accumulator = 0; sequence = 0; StartupError = null; CameraDirector.Select(0); Present();
            }
            catch (Exception e) { Simulation = null; StartupError = e.Message; UnityEngine.Debug.LogError("EP02 startup validation: " + e.Message, this); }
        }
        private void Update()
        {
            var k = Keyboard.current;
            if (k != null) { if (k.rKey.wasPressedThisFrame) Restart(); if (k.spaceKey.wasPressedThisFrame) Paused = !Paused; if (k.nKey.wasPressedThisFrame && Paused) StepTicks(1); if (k.lKey.wasPressedThisFrame) ToggleHeadLock(); if (k.cKey.wasPressedThisFrame) CameraDirector.Next(); }
            if (Simulation == null) { Hud?.ShowError(StartupError); return; }
            if (!Paused) { accumulator += Time.unscaledDeltaTime; var count = 0; while (accumulator >= 1.0 / BeltConstants.TickRate && count++ < 8) { TickProfiled(); accumulator -= 1.0 / BeltConstants.TickRate; } accumulator = Math.Min(accumulator, 8.0 / BeltConstants.TickRate); }
            Present();
        }
        public void StepTicks(int count) { if (Simulation == null) return; for (var i = 0; i < count; i++) TickProfiled(); accumulator = 0; Present(); }
        private void TickProfiled() { UnityEngine.Profiling.Profiler.BeginSample("EP02.FixedTick"); try { Simulation.Tick(); } finally { UnityEngine.Profiling.Profiler.EndSample(); } }
        public void ToggleHeadLock()
        {
            if (Simulation == null) return; var id = Layout.Part(2).SocketId;
            foreach (var s in Simulation.Snapshot.Sockets) if (s.Id == id) Simulation.EnqueueCommand(new AssemblyCommand(Simulation.TickNumber + 1, sequence++, AssemblyCommandKind.SetSocketLocked, id, !s.Locked));
        }
        public void Present()
        {
            if (Simulation == null) return; clock.Restart(); var snapshot = Simulation.Snapshot;
            UnityEngine.Profiling.Profiler.BeginSample("EP02.Presentation");
            try
            {
                ArmPresenter.Present(snapshot, Topology, Mathf.Clamp01(Fraction)); PartPresenter.Present(snapshot, Topology); CellPresenter.Present(snapshot, Scenario); AudioPresenter.Present(snapshot); Hud.Present(this);
                block ??= new MaterialPropertyBlock();
                foreach (var s in Layout.Surfaces)
                {
                    var phase = s.Product ? snapshot.SegmentPhases[s.Segment] : snapshot.SupplyPhases[s.Line];
                    var speed = s.Product ? snapshot.SegmentSpeeds[s.Segment] : s.Line == 0 ? Scenario.SupplySpeedA : Scenario.SupplySpeedB;
                    block.SetFloat("_UseSimulationPhase",1); block.SetFloat("_FlowPhase", (float)(((phase + speed * Fraction) / EP02Route.UnitsPerMeter) % 1024) * 4); block.SetFloat("_FlowOffset",s.StartMeters * 4); block.SetFloat("_FlowCoordScale",s.LengthMeters * 4); s.Renderer.SetPropertyBlock(block);
                }
            }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
            clock.Stop(); PresentationMs = clock.Elapsed.TotalMilliseconds + Simulation.SnapshotMs;
        }
    }
}
