using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity
{
    public sealed class EP03SimulationDriver : MonoBehaviour
    {
        public FactoryLayoutAsset Layout;
        public EP02LayoutAuthoring AssemblyLayout;
        public EP02ScenarioConfig AssemblyScenario;
        public InserterPresenter Arms;
        public PartVisualPresenter Parts;
        public AssemblyCellPresenter Cell;
        public EP02CameraDirector Cameras;
        public EP03CinematicCamera Cinematic;
        public EP03FreeCamera FreeCamera;
        public FactoryModuleAuthoring[] Modules;
        public UnityEngine.UI.Text Hud;
        public Transform CargoRoot,FinishedRoot;
        public Material[] MaterialPalette;
        public bool Paused;
        public FactorySimulation Simulation { get; private set; }
        public EP02BakedLayout AssemblyTopology { get; private set; }
        public string StartupError { get; private set; }
        public double SimulationMs { get; private set; }
        public double PresentationMs { get; private set; }
        public double AuditMs { get; private set; }
        private double accumulator;
        private int sequence;
        private readonly Stopwatch watch=new Stopwatch();
        private readonly Dictionary<int,GameObject> cargo=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,GameObject> elevatorCarriers=new Dictionary<int,GameObject>();
        private Material elevatorCarrierMaterial;
        private readonly Dictionary<int,GameObject> finished=new Dictionary<int,GameObject>();
        private Dictionary<string,FactoryRoutePlacement> routes;
        private Dictionary<string,FactoryModuleAuthoring> moduleViews;
        private MaterialPropertyBlock block;
        private static readonly Dictionary<int,string> names=FactoryCatalog.Recipes().ToDictionary(r=>r.Output,r=>r.Name);
        private static void Release(UnityEngine.Object value) { if(value==null)return; if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value); }
        private void Start()=>Restart();
        public void Restart()
        {
            try
            {
                block ??= new MaterialPropertyBlock();
                Simulation=null;Parts.Clear();Cell.Clear();foreach(var g in cargo.Values)Release(g);foreach(var g in finished.Values)Release(g);cargo.Clear();finished.Clear();
                elevatorCarriers.Clear();elevatorCarrierMaterial=UnityEngine.Object.FindFirstObjectByType<FactoryElevatorAuthoring>()?.CarrierMaterial;
                Layout.Validate();foreach(var m in Modules)m.Validate();AssemblyTopology=AssemblyLayout.BakeAndValidate();
                var config=AssemblyScenario.Create(AssemblyLayout,AssemblyTopology);config.SourceEnabled=false;config.RejectEnabled=false;config.ExternalSupply=true;
                Simulation=new FactorySimulation(Layout.CreateDefinition(),new EP02Simulation(config,AssemblyTopology.SupplyTopology,AssemblyScenario.Backend),AssemblyScenario.Backend);
                foreach(var route in Layout.Routes)route.PrepareVisualPath();
                routes=Layout.Routes.ToDictionary(r=>r.Connection.Id);moduleViews=Modules.ToDictionary(m=>m.InstanceId);accumulator=0;sequence=0;StartupError=null;Cameras.Select(0);Present();
            }
            catch(Exception e){Simulation=null;StartupError=e.ToString();UnityEngine.Debug.LogError("EP03 startup: "+StartupError,this);if(Hud!=null)Hud.text=StartupError;}
        }
        private void Update()
        {
            SimulationMs=0;AuditMs=0;
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.rKey.wasPressedThisFrame)Restart();if(k.spaceKey.wasPressedThisFrame)Paused=!Paused;
                if(k.nKey.wasPressedThisFrame&&Paused)StepTicks(1);if(k.cKey.wasPressedThisFrame){if(Cinematic!=null)Cinematic.Stop();if(FreeCamera!=null)FreeCamera.Stop();Cameras.Next();}
                if(Simulation!=null){if(k.fKey.wasPressedThisFrame)Simulation.SupplyEnabled=!Simulation.SupplyEnabled;if(k.oKey.wasPressedThisFrame)Simulation.DispatchEnabled=!Simulation.DispatchEnabled;if(k.lKey.wasPressedThisFrame)ToggleHead();}
            }
            if(Simulation==null)return;
            if(!Paused){accumulator+=Time.unscaledDeltaTime;var n=0;while(accumulator>=1.0/60&&n++<8){Tick();accumulator-=1.0/60;}accumulator=Math.Min(accumulator,8.0/60);}
            Present();
        }
        private void ToggleHead()
        {
            var id=AssemblyLayout.Part(2).SocketId;var locked=Simulation.Assembly.Snapshot.Sockets.Single(s=>s.Id==id).Locked;
            Simulation.Assembly.EnqueueCommand(new AssemblyCommand(Simulation.TickNumber+1,sequence++,AssemblyCommandKind.SetSocketLocked,id,!locked));
        }
        private void Tick()
        {
            UnityEngine.Profiling.Profiler.BeginSample("EP03.FixedTick");watch.Restart();
            try{Simulation.Tick();SimulationMs+=watch.Elapsed.TotalMilliseconds;if(Simulation.TickNumber%600==0){watch.Restart();Simulation.AssertConserved();AuditMs+=watch.Elapsed.TotalMilliseconds;}}
            finally{watch.Stop();UnityEngine.Profiling.Profiler.EndSample();}
        }
        public void StepTicks(int count){if(Simulation==null)return;SimulationMs=0;AuditMs=0;for(var i=0;i<count;i++)Tick();accumulator=0;Present();}
        public void Present()
        {
            if(Simulation==null)return;watch.Restart();var s=Simulation.Assembly.Snapshot;
            Arms.Present(s,AssemblyTopology,0);Parts.Present(s,AssemblyTopology);Cell.Present(s,AssemblyScenario);
            var live=new HashSet<int>();
            foreach(var route in Simulation.Routes)foreach(var item in route.Snapshot.Items)
            {
                live.Add(item.ItemId.Value);if(!cargo.TryGetValue(item.ItemId.Value,out var g)){g=GameObject.CreatePrimitive(PrimitiveType.Cube);Release(g.GetComponent<Collider>());g.transform.SetParent(CargoRoot,false);g.transform.localScale=new Vector3(.3f,.22f,.3f);g.GetComponent<Renderer>().sharedMaterial=MaterialPalette[item.PartType.Value];cargo.Add(item.ItemId.Value,g);}
                var position=routes[route.Definition.Id].Sample(item.Distance,out var tangent,out var inElevator);g.transform.rotation=Quaternion.LookRotation(Mathf.Abs(tangent.y)>.9f?Vector3.forward:tangent);g.transform.position=position+g.transform.up*.12f;
                if(inElevator&&!elevatorCarriers.ContainsKey(item.ItemId.Value))
                {
                    var tray=GameObject.CreatePrimitive(PrimitiveType.Cube);tray.name="Elevator Carrier";Release(tray.GetComponent<Collider>());tray.transform.SetParent(g.transform,false);
                    tray.transform.localScale=new Vector3(1.4f,.04f/.22f,1.4f);tray.transform.localPosition=new Vector3(0,-.135f/.22f,0);tray.GetComponent<Renderer>().sharedMaterial=elevatorCarrierMaterial;
                    elevatorCarriers.Add(item.ItemId.Value,tray);
                }
                if(elevatorCarriers.TryGetValue(item.ItemId.Value,out var carrier))carrier.SetActive(inElevator);
            }
            foreach(var id in cargo.Keys.Where(id=>!live.Contains(id)).ToArray()){Release(cargo[id]);cargo.Remove(id);elevatorCarriers.Remove(id);}
            foreach(var state in Simulation.Modules)
            {
                var view=moduleViews[state.Definition.Id];var color=state.Recipe==null?Color.cyan:state.State==FactoryMachineState.OutputBlocked?Color.red:state.State==FactoryMachineState.MissingInput?Color.yellow:Color.green;
                block.SetColor("_BaseColor",color);if(view.Status!=null)view.Status.SetPropertyBlock(block);
                if(view.Label!=null)
                {
                    if(state.Definition.AssemblyLane>=0){view.Label.characterSize=.065f;view.Label.text=MaterialName(state.Definition.Material)+"供料 / "+state.Output.Count;}
                    else view.Label.text=(state.Recipe?.Name??MaterialName(state.Definition.Material))+"\n"+(state.Recipe==null?"库存 "+state.Output.Count:StatusName(state.State)+"  批次 "+state.CompletedBatches+"  输出 "+state.Output.Count)+"\n"+string.Join("  ",state.Inputs.Select(q=>MaterialName(q.Key)+" "+q.Value.Count));
                }
            }
            var finishedIds=new HashSet<int>();var slot=0;
            foreach(var p in Simulation.Finished)
            {
                finishedIds.Add(p.Id);if(!finished.TryGetValue(p.Id,out var g))
                {
                    g=Instantiate(AssemblyLayout.ProductCarrierPrefab,FinishedRoot);g.name="Finished Product "+p.Id;
                    foreach(var part in AssemblyLayout.Parts){var socket=new GameObject("Socket "+part.SocketId).transform;socket.SetParent(g.transform,false);socket.localPosition=AssemblyLayout.SocketOffset(part.SocketId);var visual=Instantiate(part.SourcePrefab,socket);part.AssemblyPose.Apply(visual.transform);}finished.Add(p.Id,g);
                }
                g.transform.localPosition=new Vector3(slot%2*3,0,slot/2*3);slot++;
            }
            foreach(var id in finished.Keys.Where(id=>!finishedIds.Contains(id)).ToArray()){Release(finished[id]);finished.Remove(id);}
            foreach(var surface in AssemblyLayout.Surfaces)
            {
                var phase=surface.Product?s.SegmentPhases[surface.Segment]:s.SupplyPhases[surface.Line];block.Clear();block.SetFloat("_UseSimulationPhase",1);block.SetFloat("_FlowPhase",(float)(phase/EP02Route.UnitsPerMeter%1024)*4);block.SetFloat("_FlowOffset",surface.StartMeters*4);block.SetFloat("_FlowCoordScale",surface.LengthMeters*4);surface.Renderer.SetPropertyBlock(block);
            }
            Hud.text="零号装配厂 · EP03 / "+Layout.Version+"\nTick "+Simulation.TickNumber+"   "+(Paused?"暂停":"运行")+"   镜头 "+(FreeCamera != null && FreeCamera.IsActive ? "自由飞行" : Cinematic != null && Cinematic.IsPlaying ? "导演镜头" : Cameras.Presets[Cameras.Selected].Name)+"\n设备 "+Simulation.Modules.Count(m=>m.Recipe!=null)+"   加工中 "+Simulation.Modules.Count(m=>m.Batch!=null)+"   运输物料 "+live.Count+"\n完成 "+s.Statistics.CompletedRobots+"   待入库 "+Simulation.Finished.Count+"/"+Layout.FinishedCapacity+"   已入库 "+Simulation.Dispatched+"\n原料补货 "+(Simulation.SupplyEnabled?"开":"关")+"   成品入库 "+(Simulation.DispatchEnabled?"开":"关")+"\n账本 "+Simulation.Ledger.Count+"   配方批次 "+Simulation.Batches.Count+"\n模拟 "+SimulationMs.ToString("F2")+" ms   表现 "+PresentationMs.ToString("F2")+" ms   核账 "+AuditMs.ToString("F2")+" ms\nSpace 暂停 · N 单步 · R 重置 · C 镜头\nF 停供/恢复 · O 出库开关 · L 头部锁定\nG 自由相机 · H 隐藏/显示全部 UI\n检测沿用 EP02；返修与第二产品区域预留";
            watch.Stop();PresentationMs=watch.Elapsed.TotalMilliseconds;
        }
        public static string StatusName(FactoryMachineState s)=>s==FactoryMachineState.MissingInput?"缺料":s==FactoryMachineState.OutputBlocked?"堵塞":s==FactoryMachineState.Processing?"加工":"等待";
        public static string MaterialName(int id)=>id==8?"金属":id==9?"塑料":id==10?"电子原料":names.TryGetValue(id,out var name)?name:id.ToString();
    }
}
