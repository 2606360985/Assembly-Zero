using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;

namespace AssemblyZero.Tests
{
    public sealed class EP03SmokeTests
    {
        [UnityTest] public IEnumerator ElevatorCarriersStayLevelPauseAndClearOnRestart()
        {
            yield return SceneManager.LoadSceneAsync("EP03_Demo",LoadSceneMode.Single);yield return null;
            var d=Object.FindFirstObjectByType<EP03SimulationDriver>();d.Paused=true;Assert.IsNull(d.StartupError);
            Assert.Greater(Object.FindObjectsByType<FactoryElevatorAuthoring>(FindObjectsSortMode.None).Length,0);
            Transform[] trays=null;
            for(var i=0;i<600;i++)
            {
                d.StepTicks(50);trays=d.CargoRoot.GetComponentsInChildren<Transform>().Where(t=>t.name=="Elevator Carrier").ToArray();
                if(trays.Length>0)break;
            }
            Assert.IsNotEmpty(trays);d.Simulation.AssertConserved();var hash=d.Simulation.CalculateStateHash();var positions=trays.Select(t=>t.position).ToArray();
            for(var i=0;i<trays.Length;i++)Assert.Greater(Vector3.Dot(trays[i].up,Vector3.up),.999f);
            d.Present();yield return null;d.Present();Assert.AreEqual(hash,d.Simulation.CalculateStateHash());
            for(var i=0;i<trays.Length;i++)Assert.AreEqual(positions[i],trays[i].position);
            d.Restart();yield return null;Assert.AreEqual(0,d.CargoRoot.childCount);Assert.IsNull(d.StartupError);d.Simulation.AssertConserved();
        }
        [UnityTest] public IEnumerator ResetClearsProcessingReservedHeldScanningAndBlockedStates()
        {
            yield return SceneManager.LoadSceneAsync("EP03_Demo",LoadSceneMode.Single);yield return null;
            var d=Object.FindFirstObjectByType<EP03SimulationDriver>();d.Paused=true;
            for(var phase=0;phase<5;phase++)
            {
                if(phase==4)d.Simulation.DispatchEnabled=false;
                bool Reached()=>phase==0?d.Simulation.Modules.Any(m=>m.Batch!=null):phase==1?d.Simulation.Assembly.Statistics.Reserved>0:phase==2?d.Simulation.Assembly.Statistics.Held>0:phase==3?d.Simulation.Assembly.Snapshot.Products.Any(p=>p.State==ProductState.Scanning):d.Simulation.Finished.Count==8&&d.Simulation.Assembly.Snapshot.Products.Any(p=>p.State==ProductState.WaitingForDownstream);
                while(d.Simulation.TickNumber<60000&&!Reached())d.Simulation.Tick();Assert.IsTrue(Reached(),"Reset phase "+phase+" was not reached");d.Simulation.AssertConserved();d.Present();yield return null;
                d.Restart();yield return null;yield return null;Assert.IsNull(d.StartupError);Assert.AreEqual(0,d.Simulation.TickNumber);Assert.AreEqual(0,d.FinishedRoot.childCount);Assert.AreEqual(0,d.CargoRoot.childCount);Assert.AreEqual(0,d.Parts.VisiblePayloads);d.Simulation.AssertConserved();
            }
        }
        [UnityTest] public IEnumerator FactoryProducesPausesAndRestartsWithoutResidualOwnership()
        {
            yield return SceneManager.LoadSceneAsync("EP03_Demo",LoadSceneMode.Single);yield return null;
            var d=Object.FindFirstObjectByType<EP03SimulationDriver>();d.Paused=true;Assert.IsNull(d.StartupError);Assert.IsNotNull(d.Simulation);
            for(var i=0;i<120&&d.Simulation.Dispatched<10;i++){d.StepTicks(500);yield return null;}
            d.Simulation.AssertConserved();Assert.GreaterOrEqual(d.Simulation.Dispatched,10);Assert.IsTrue(d.Simulation.Modules.Where(m=>m.Recipe!=null).All(m=>m.CompletedBatches>0));
            var hash=d.Simulation.CalculateStateHash();for(var i=0;i<3;i++){d.Present();yield return null;}Assert.AreEqual(hash,d.Simulation.CalculateStateHash());
            var samples=new List<string>{"frame_ms,simulation_ms,presentation_ms,audit_ms,gc_allocated_bytes"};
            var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",128);d.Paused=false;var sampleStart=Time.realtimeSinceStartupAsDouble;
            while(Time.realtimeSinceStartupAsDouble-sampleStart<10){yield return null;samples.Add(string.Join(",",new[]{(Time.unscaledDeltaTime*1000).ToString("F4",CultureInfo.InvariantCulture),d.SimulationMs.ToString("F4",CultureInfo.InvariantCulture),d.PresentationMs.ToString("F4",CultureInfo.InvariantCulture),d.AuditMs.ToString("F4",CultureInfo.InvariantCulture),gc.Valid?gc.LastValue.ToString(CultureInfo.InvariantCulture):"unavailable"}));}
            gc.Dispose();d.Paused=true;var report=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/EP03"));Directory.CreateDirectory(report);File.WriteAllLines(Path.Combine(report,"EditorFrameSamples.csv"),samples);
            File.WriteAllText(Path.Combine(report,"FrameContext.txt"),(Application.isBatchMode?"Batch Editor":"Connected interactive Editor")+" PlayMode, not a standalone player benchmark\nUnity: "+Application.unityVersion+"\nCPU: "+SystemInfo.processorType+"\nGPU: "+SystemInfo.graphicsDeviceName+"\nRender size: "+Screen.width+"x"+Screen.height+"\nSampling seconds: "+(Time.realtimeSinceStartupAsDouble-sampleStart).ToString("F3",CultureInfo.InvariantCulture)+"\nSamples: "+(samples.Count-1)+"\nSimulation field: accumulated fixed-tick CPU time per frame; zero when no tick.\nAudit field: conservation audit time in that frame; zero when no audit.\nGC recorder includes the test runner and CSV sampling overhead.\n");
            d.Simulation.DispatchEnabled=false;for(var i=0;i<20;i++){d.StepTicks(500);yield return null;}Assert.AreEqual(8,d.Simulation.Finished.Count);d.Simulation.AssertConserved();
            d.Restart();yield return null;yield return null;Assert.IsNull(d.StartupError);Assert.AreEqual(0,d.Simulation.TickNumber);Assert.AreEqual(0,d.Simulation.Dispatched);Assert.AreEqual(0,d.FinishedRoot.childCount);d.Simulation.AssertConserved();
            d.StepTicks(1);Assert.AreEqual(1,d.Simulation.TickNumber);
        }
    }
}
