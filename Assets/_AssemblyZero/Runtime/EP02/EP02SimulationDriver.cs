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
        [Range(1, 6)] public int InitialStage = 6;
        public bool Paused;
        public int Stage { get; private set; }
        public EP02Simulation Simulation { get; private set; }
        public BakedBeltTopology Topology { get; private set; }
        public double PresentationMs { get; private set; }
        public string StartupError { get; private set; }
        public float Fraction => (float)(accumulator * BeltConstants.TickRate);
        private double accumulator;
        private int sequence;
        private readonly Stopwatch clock = new Stopwatch();
        private void Start() { ResetStage(InitialStage); }
        public void ResetStage(int stage)
        {
            try
            {
                if (Layout == null || Scenario == null || ArmPresenter == null || PartPresenter == null || CellPresenter == null || Hud == null || CameraDirector == null || AudioPresenter == null) throw new InvalidOperationException("EP02 driver is missing required references.");
                Topology = Layout.BakeAndValidate(); var config = Scenario.Create(stage, Layout, Topology);
                Simulation = new EP02Simulation(config, Topology.DomainTopology, Scenario.Backend); Stage = stage; accumulator = 0; sequence = 0; StartupError = null;
                if (stage == 4) Simulation.EnqueueCommand(new AssemblyCommand(16, sequence++, AssemblyCommandKind.SetSocketLocked, Layout.Part(2).SocketId, true));
                PartPresenter.Clear(); AudioPresenter.ResetEvents(); CameraDirector.SelectStage(stage); Present();
            }
            catch (Exception e) { Simulation = null; StartupError = e.Message; UnityEngine.Debug.LogError("EP02 startup validation: " + e.Message, this); }
        }
        private void Update()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                for (var i = 0; i < 6; i++) if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) ResetStage(i + 1);
                if (k.rKey.wasPressedThisFrame) ResetStage(Stage);
                if (k.spaceKey.wasPressedThisFrame) Paused = !Paused;
                if (k.nKey.wasPressedThisFrame && Paused) StepTicks(1);
                if (k.lKey.wasPressedThisFrame) ToggleHeadLock();
                if (k.cKey.wasPressedThisFrame) CameraDirector.Next();
            }
            if (Simulation == null) { Hud.ShowError(StartupError); return; }
            if (!Paused)
            {
                accumulator += Time.unscaledDeltaTime; var count = 0;
                while (accumulator >= 1.0 / BeltConstants.TickRate && count++ < 8) { TickProfiled(); accumulator -= 1.0 / BeltConstants.TickRate; }
                accumulator = Math.Min(accumulator, 8.0 / BeltConstants.TickRate);
            }
            Present();
        }
        public void StepTicks(int count) { if (Simulation == null) return; for (var i = 0; i < count; i++) TickProfiled(); accumulator = 0; Present(); }
        private void TickProfiled() { UnityEngine.Profiling.Profiler.BeginSample("EP02.FixedTick"); try { Simulation.Tick(); } finally { UnityEngine.Profiling.Profiler.EndSample(); } }
        public void ToggleHeadLock()
        {
            if (Simulation == null) return; var id = Layout.Part(2).SocketId;
            foreach (var s in Simulation.Snapshot.Sockets) if (s.Id == id) Simulation.EnqueueCommand(new AssemblyCommand(Simulation.TickNumber + 1, sequence++, AssemblyCommandKind.SetSocketLocked, id, !s.Locked));
        }
        private void Present()
        {
            if (Simulation == null) return;
            clock.Restart(); var snapshot = Simulation.Snapshot;
            UnityEngine.Profiling.Profiler.BeginSample("EP02.Presentation");
            try { ArmPresenter.Present(snapshot, Topology, Mathf.Clamp01(Fraction)); PartPresenter.Present(snapshot, Topology); CellPresenter.Present(snapshot, Scenario); AudioPresenter.Present(snapshot); Hud.Present(this); }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
            clock.Stop(); PresentationMs = clock.Elapsed.TotalMilliseconds + Simulation.SnapshotMs;
        }
    }
}
