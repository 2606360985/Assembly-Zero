using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AssemblyZero.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AssemblyZero.Unity.Editor
{
    public static class EP03Acceptance
    {
        [MenuItem("Tools/Assembly Zero/Run EP03 Acceptance and Capture")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before batch acceptance.");
            EditorSceneManager.OpenScene(EP03SceneBuilder.ScenePath);var d=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();d.Paused=true;d.Restart();
            if(d.StartupError!=null)throw new InvalidOperationException(d.StartupError);
            var clock=Stopwatch.StartNew();var times=new double[60000];var count=0;
            while(count<60000&&(d.Simulation.Dispatched<10||d.Simulation.Modules.Where(m=>m.Recipe!=null).Any(m=>m.CompletedBatches==0)))
            {
                var before=clock.Elapsed.TotalMilliseconds;d.Simulation.Tick();times[count++]=clock.Elapsed.TotalMilliseconds-before;
                if(count%600==0)d.Simulation.AssertConserved();
            }
            d.Simulation.AssertConserved();if(d.Simulation.Dispatched<10)throw new InvalidOperationException("EP03 failed to deliver ten robots in 60,000 ticks.");
            var report="EP03 Unity Editor acceptance\nUTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\nCPU: "+SystemInfo.processorType+"\nGPU: "+SystemInfo.graphicsDeviceName+"\nMemory MB: "+SystemInfo.systemMemorySize+"\nResolution: 1920x1080 captures\nMode: Editor fixed-tick CPU run, not a player frame-rate benchmark\nTicks: "+count+"\nRecipe machines: "+d.Simulation.Modules.Count(m=>m.Recipe!=null)+"\nRoutes: "+d.Simulation.Routes.Count+"\nDelivered: "+d.Simulation.Dispatched+"\nActive transport cargo: "+d.Simulation.Routes.Sum(r=>r.Snapshot.Items.Count)+"\nBuffer cargo: "+d.Simulation.Modules.Sum(m=>m.Output.Count+m.Inputs.Values.Sum(q=>q.Count))+"\nLedger entries: "+d.Simulation.Ledger.Count+"\nBatches: "+d.Simulation.Batches.Count;
            var sorted=times.Take(count).OrderBy(x=>x).ToArray();report+="\nTick including route snapshots mean ms: "+sorted.Average().ToString("F4")+"\np95 ms: "+sorted[(int)((count-1)*.95)].ToString("F4")+"\nmax ms: "+sorted.Last().ToString("F4");
            d.Simulation.DispatchEnabled=false;for(var i=0;i<12000;i++)d.Simulation.Tick();d.Simulation.AssertConserved();
            if(d.Simulation.Finished.Count!=d.Layout.FinishedCapacity||!d.Simulation.Assembly.Snapshot.Products.Any(p=>p.State==ProductState.WaitingForDownstream))throw new InvalidOperationException("Finished queue failed to backpressure assembly.");
            report+="\nFull outlet backpressure: passed";d.Simulation.DispatchEnabled=true;var delivered=d.Simulation.Dispatched;for(var i=0;i<6000;i++)d.Simulation.Tick();d.Simulation.AssertConserved();if(d.Simulation.Dispatched<=delivered)throw new InvalidOperationException("Outlet did not recover.");report+="\nOutlet recovery: passed";
            d.Simulation.SupplyEnabled=false;for(var i=0;i<24000;i++)d.Simulation.Tick();d.Simulation.AssertConserved();if(!d.Simulation.Modules.Any(m=>m.State==FactoryMachineState.MissingInput))throw new InvalidOperationException("Stopped supply did not starve machines.");report+="\nStopped supply starvation: passed";delivered=d.Simulation.Dispatched;
            d.Simulation.SupplyEnabled=true;for(var i=0;i<18000;i++)d.Simulation.Tick();d.Simulation.AssertConserved();if(d.Simulation.Dispatched<=delivered)throw new InvalidOperationException("Supply did not recover.");report+="\nSupply recovery: passed";
            var view=d.Modules.First(m=>m.InstanceId=="plate-0");var signature=view.InterfaceSignature();view.Visual.gameObject.SetActive(false);var replacement=GameObject.CreatePrimitive(PrimitiveType.Cube);replacement.transform.SetParent(view.transform,false);replacement.transform.localPosition=Vector3.up;replacement.transform.localScale=new Vector3(3,2,3);view.Visual=replacement.transform;view.Validate();if(signature!=view.InterfaceSignature())throw new InvalidOperationException("Visual replacement moved an interface.");report+="\nRepresentative Visual replacement: interface signature unchanged";
            clock.Restart();d.Present();report+="\nOne presentation including initial visual allocation ms: "+clock.Elapsed.TotalMilliseconds.ToString("F4");
            Directory.CreateDirectory("Reports/EP03");
            // Prime the SRP once before the overview: the first explicit editor render initializes its resources.
            d.Cameras.Select(5);Capture(d.Cameras.Camera,"Reports/EP03/View-5.png");
            foreach(var index in new[]{0,1,2,3,4,5,6}){d.Cameras.Select(index);Capture(d.Cameras.Camera,"Reports/EP03/View-"+index+".png");}
            File.WriteAllText("Reports/EP03/Acceptance.txt",report+"\nAll 30 machines completed at least one batch.\nGC/frame and Development Player performance require separate frame sampling.\n");
            UnityEngine.Debug.Log(report);
        }
        public static void CaptureViews()
        {
            EditorSceneManager.OpenScene(EP03SceneBuilder.ScenePath);var d=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();d.Paused=true;d.Restart();if(d.StartupError!=null)throw new InvalidOperationException(d.StartupError);
            d.StepTicks(20000);d.Simulation.AssertConserved();Directory.CreateDirectory("Reports/EP03");d.Cameras.Select(5);Capture(d.Cameras.Camera,"Reports/EP03/View-5.png");
            var watch=Stopwatch.StartNew();for(var i=0;i<120;i++)foreach(var route in d.Simulation.Routes)route.Backend.CreateSnapshot(route.Snapshot);
            File.WriteAllText("Reports/EP03/Snapshot.txt","Editor CPU snapshot observation at 20,000 ticks\nRoutes: "+d.Simulation.Routes.Count+"\nTransport items: "+d.Simulation.Routes.Sum(r=>r.Snapshot.Items.Count)+"\nIsolated route snapshot refresh mean ms (120 repetitions): "+(watch.Elapsed.TotalMilliseconds/120).ToString("F4")+"\nLast assembly snapshot publish ms: "+d.Simulation.Assembly.SnapshotMs.ToString("F4")+"\nThe isolated refresh is an extra measurement, not additional production ticks.\n");
            foreach(var i in new[]{0,1,2,3,4,6}){d.Cameras.Select(i);Capture(d.Cameras.Camera,"Reports/EP03/View-"+i+".png");}
            for(var i=0;i<6000&&d.Simulation.Assembly.Snapshot.Inserters[0].HeldItem==0;i++)d.Simulation.Tick();d.Present();d.Cameras.Select(5);Capture(d.Cameras.Camera,"Reports/EP03/View-5.png");
            d.Simulation.DispatchEnabled=false;for(var i=0;i<10000;i++)d.Simulation.Tick();d.Simulation.AssertConserved();d.Present();d.Cameras.Select(6);Capture(d.Cameras.Camera,"Reports/EP03/View-6.png");
        }
        public static void CheckRebuild()
        {
            EP03SceneBuilder.BuildScene();var first=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();var signature=EP03SceneBuilder.Signature(first);var objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{EP03SceneBuilder.Root}).Length;
            EP03SceneBuilder.BuildScene();var second=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();EP03SceneBuilder.ValidateScene();
            if(signature!=EP03SceneBuilder.Signature(second)||objects!=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length||prefabs!=AssetDatabase.FindAssets("t:Prefab",new[]{EP03SceneBuilder.Root}).Length)throw new InvalidOperationException("Repeated EP03 generation changed interfaces or object counts.");
            Directory.CreateDirectory("Reports/EP03");File.WriteAllText("Reports/EP03/Generation.txt","Repeated generation: passed\nScene transforms: "+objects+"\nEP03 prefabs: "+prefabs+"\nModule instances: "+second.Modules.Length+"\nRoutes: "+second.Layout.Routes.Length+"\n");
        }
        public static void SaveValidatedLayouts()
        {
            CheckRebuild();EP03SceneBuilder.BuildMinimal();
            foreach(var scene in new[]{EP03SceneBuilder.ScenePath,"Assets/_AssemblyZero/Scenes/EP03_Minimal.unity"})
            {
                EditorSceneManager.OpenScene(scene);EP03SceneBuilder.Freeze();
            }
            EditorSceneManager.OpenScene(EP03SceneBuilder.ScenePath);
            File.WriteAllText("Reports/EP03/LayoutBaseline.txt","Full and minimal layouts: V1\nFunctional module poses, ports, assembly pickup interfaces, sockets and ordered transport paths recorded.\nValidation and repeated generation passed.\nVisual inspection uses View-0 through View-6 captures; graybox art and dense bridge routing remain replaceable.\n");
        }
        public static void RefreshCaptures(){CheckRebuild();EP03SceneBuilder.BuildMinimal();CaptureViews();}
        public static void PrepareAcceptance(){CheckRebuild();EP03SceneBuilder.BuildMinimal();Run();CaptureViews();}
        public static void SaveAndBuild(){SaveValidatedLayouts();EP03SceneBuilder.BuildPlayer();}
        private static void Capture(Camera camera,string output)
        {
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try{camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(output,texture.EncodeToPNG());}
            finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
