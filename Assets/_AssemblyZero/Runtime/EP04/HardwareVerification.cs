using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareGame
    {
        private IEnumerator VerifyPlayer()
        {
            Paused=true;var folder=Path.Combine(Directory.GetCurrentDirectory(),"Reports","Hardware",Screen.width+"x"+Screen.height);Directory.CreateDirectory(folder);
            for(int i=0;i<10;i++)yield return null;
            yield return Capture("Default",folder);
            Drawer="产品";Hud.RebuildDrawer();yield return Capture("Products",folder);
            Drawer="订单";Hud.RebuildDrawer();yield return Capture("Orders",folder);
            Drawer="设备";SelectedNode=3;Hud.RebuildDrawer();yield return Capture("Shortage",folder);
            Drawer="";Hud.RebuildDrawer();ToggleDriving();yield return Capture("Driving",folder);ToggleDriving();
            string error=null;string walkthrough=null;
            try
            {
                walkthrough=HardwareWalkthrough.Run(false)+"\n"+HardwareWalkthrough.Run(true)+"\n"+HardwareWalkthrough.RunMake()+"\n"+HardwareWalkthrough.RunRecovery();
                Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Submit(new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=6});
                for(int i=0;i<850*60;i++)World.Tick();Submit(new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});
                if(!Submit(new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1}).Success)throw new InvalidOperationException("Standalone first order failed");
                World.CaptureState();var copy=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(World.State)));for(int i=0;i<3600;i++){World.Tick();copy.Tick();}World.CaptureState();copy.CaptureState();if(JsonUtility.ToJson(World.State)!=JsonUtility.ToJson(copy.State))throw new InvalidOperationException("Save continuation mismatch");
            }
            catch(Exception e){error=e.ToString();}
            File.WriteAllText(Path.Combine(folder,"Walkthrough.txt"),error??walkthrough);
            Drawer="报表";Hud.RebuildDrawer();View.Rebuild();yield return Capture("Settlement",folder);
            if(error!=null){File.WriteAllText(Path.Combine(folder,"Verification.txt"),"FAIL\n"+error);Application.Quit(1);yield break;}
            Drawer="";Hud.RebuildDrawer();NewGame();Paused=false;
            if(!Environment.GetCommandLineArgs().Contains("--hardware-short"))
            {
                yield return SampleFrames(folder,"Starter");
                Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24});Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});Submit(new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Aluminum,Quantity=12});
                yield return SampleFrames(folder,"Expanded");
            }
            File.WriteAllText(Path.Combine(folder,"Verification.txt"),"PASS: command-driven standalone production, delivery, driving domain walkthrough and save continuation.\nScreenshots require visual inspection. Does not certify physical mouse/keyboard playtesting.\n"+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+" / Unity "+Application.unityVersion+"\nNo user saves read or written.");Application.Quit(0);
        }
        private IEnumerator Capture(string name,string folder){Hud.Refresh();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));for(int i=0;i<4;i++)yield return null;}
        private IEnumerator SampleFrames(string folder,string label)
        {
            var until=Time.realtimeSinceStartup+30;while(Time.realtimeSinceStartup<until)yield return null;
            var frames=new List<float>();until=Time.realtimeSinceStartup+120;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();File.WriteAllText(Path.Combine(folder,label+"Performance.txt"),"Visible player; VSync=0; unlimited; warmup 30s; sample 120s\n"+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+"\nResolution "+Screen.width+"x"+Screen.height+" Quality "+QualitySettings.names[QualitySettings.GetQualityLevel()]+"\nNodes "+World.State.Nodes.Count+" Routes "+World.State.Routes.Count+"\nFrames "+frames.Count+" Mean ms "+frames.Average().ToString("F3")+" P95 ms "+frames[(int)(frames.Count*.95)].ToString("F3")+"\nBudget mean<=16.7, P95<=25: "+(frames.Average()<=16.7&&frames[(int)(frames.Count*.95)]<=25));
        }
    }
    public static class HardwareWalkthrough
    {
        private static void Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();if(!w.LastResult.Success)throw new InvalidOperationException(c.Kind+": "+w.LastResult.Message);}
        private static void RunTicks(WorldSimulation w,int count){for(int i=0;i<count;i++)w.Tick();}
        public static string Run(bool pickup)
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=6,Delivery=pickup?2:0});
            if(pickup)
            {
                Do(w,new WorldCommand{Kind=CommandKind.TakeControl});Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=1});
                TravelTo(w,35,true);Do(w,new WorldCommand{Kind=CommandKind.Drive});RunTicks(w,100);Do(w,new WorldCommand{Kind=CommandKind.Interact});
                Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=-1});TravelTo(w,17.5,false);Do(w,new WorldCommand{Kind=CommandKind.Drive});RunTicks(w,100);Do(w,new WorldCommand{Kind=CommandKind.Interact});
                if(w.State.Vehicles[0].Cargo.Count!=0)throw new InvalidOperationException("Manual unload incomplete");Do(w,new WorldCommand{Kind=CommandKind.ReleaseControl});
            }
            long first=0;while(w.State.Tick<850*60){w.Tick();if(first==0&&w.FinishedStock(1).Any(g=>g.Material==HardwareCatalog.Standard))first=w.State.Tick;}
            Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=2});
            var firstSettlement=w.State.Hardware.Settlements.Single();
            foreach(var m in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board,HardwareCatalog.Shell,HardwareCatalog.Carton,HardwareCatalog.QuietHeat})Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=m,Quantity=6});
            foreach(var n in w.State.Nodes.Where(n=>n.Kind==BuildingKind.GPUAssembly||n.Kind==BuildingKind.Tester||n.Kind==BuildingKind.Packaging))Do(w,new WorldCommand{Kind=CommandKind.Recipe,Node=n.Id,Recipe="quiet-"+n.RecipeId});
            RunTicks(w,800*60);Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=2});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=2});w.AssertConserved();
            return (pickup?"Self pickup":"Delivery")+": first GPU "+first/60d+"s; two orders completed at "+w.State.Tick/60d+"s; first settlement freight "+firstSettlement.FreightCost+" cents; cash "+w.State.Money+" cents. No injected cash/unlocks/items.";
        }
        public static string RunMake()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Aluminum,Quantity=12});RunTicks(w,850*60);Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});return "Make heatsinks: completed at 850s; cash "+w.State.Money+" cents; equipment investment 18000 cents (separate from margin).";
        }
        private static void TravelTo(WorldSimulation w,double z,bool north)
        {
            var deadline=w.State.Tick+3600;while((north?w.State.Vehicles[0].DriveZ<z:w.State.Vehicles[0].DriveZ>z)&&w.State.Tick<deadline)w.Tick();
            if(w.State.Tick>=deadline)throw new InvalidOperationException("Driving route blocked; no teleport fallback");
        }
        public static string RunRecovery()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());
            foreach(var c in new[]{new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=16},new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=1},new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Board,Quantity=1,Delivery=2}})Do(w,c);
            Do(w,new WorldCommand{Kind=CommandKind.TakeControl});Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=1});TravelTo(w,25.5,true);Do(w,new WorldCommand{Kind=CommandKind.Drive});RunTicks(w,100);
            if(w.State.Money!=0)throw new InvalidOperationException("Recovery fixture must actually exhaust cash using purchases and production");
            Do(w,new WorldCommand{Kind=CommandKind.Interact});RunTicks(w,21*60);Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=-1});TravelTo(w,17.5,false);Do(w,new WorldCommand{Kind=CommandKind.Drive});RunTicks(w,100);Do(w,new WorldCommand{Kind=CommandKind.Interact});Do(w,new WorldCommand{Kind=CommandKind.ReleaseControl});
            Do(w,new WorldCommand{Kind=CommandKind.SpotSell,Material=HardwareCatalog.Ore,Quantity=20});Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=3});RunTicks(w,500*60);Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=3});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=3});w.AssertConserved();return "Recovery: cash exhausted via over-purchasing; mined/recycled 20 ore and delivered repair order; final cash "+w.State.Money+" cents. No cash injection.";
        }
    }
}
