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
            Paused=true;var folder=Path.Combine(Directory.GetCurrentDirectory(),"Reports","Hardware","VisualIteration",Screen.width+"x"+Screen.height);Directory.CreateDirectory(folder);
            for(int i=0;i<10;i++)yield return null;
            yield return Capture("Default",folder);
            var gallery=View.CreateProductGallery();var canvas=Hud.GetComponentInChildren<Canvas>();canvas.enabled=false;cameraFocus=gallery.position;ViewCamera.orthographicSize=10;
            yield return Capture("ProductStages",folder);Destroy(gallery.gameObject);canvas.enabled=true;FocusFactory();
            Drawer="运输";Hud.RebuildDrawer();FocusMine();yield return Capture("Mine",folder);FocusSupplier();yield return Capture("Supplier",folder);FocusFactory();
            if(Environment.GetCommandLineArgs().Contains("--hardware-video"))yield return CaptureTransport(folder);
            Drawer="产品";Hud.RebuildDrawer();yield return Capture("Products",folder);
            Drawer="订单";Hud.RebuildDrawer();yield return Capture("Orders",folder);
            for(int i=0;i<120*60;i++)World.Tick();
            Drawer="设备";SelectedNode=World.State.Nodes.First(n=>n.Kind==BuildingKind.BoardAssembly).Id;Hud.RebuildDrawer();yield return Capture("Shortage",folder);NewGame();
            HardwareWalkthrough.BuildChain(World);for(int i=0;i<40*60;i++)World.Tick();View.Rebuild();Drawer="设备";SelectedNode=World.Bottleneck(1)?.Id??SelectedNode;Hud.RebuildDrawer();yield return Capture("Bottleneck",folder);yield return CaptureConveyorCorner(folder);NewGame();
            Drawer="运输";Hud.RebuildDrawer();FocusLogistics();Submit(new WorldCommand{Kind=CommandKind.Collect,Resource=1});for(var i=0;i<300;i++)World.Tick();yield return Capture("AutomaticMining",folder);NewGame();
            Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24});Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});Drawer="";Hud.RebuildDrawer();View.Rebuild();yield return Capture("TechnologyStations",folder);NewGame();
            Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});HardwareWalkthrough.BuildChain(World);
            while(World.State.Hardware.FirstProductTick==0&&World.State.Tick<600*60)World.Tick();View.Rebuild();Drawer="";Hud.RebuildDrawer();yield return Capture("FirstProduct",folder);
            string error=null;string walkthrough=null;
            try
            {
                walkthrough=HardwareWalkthrough.RunOpening()+"\n"+HardwareWalkthrough.RunExpansion()+"\n"+HardwareWalkthrough.RunMarket()+"\n"+HardwareWalkthrough.RunMake()+"\n"+HardwareWalkthrough.RunRecovery();
                while(World.State.Hardware.Settlements.Count==0&&World.State.Tick<600*60)World.Tick();
                if(World.State.Hardware.Settlements.Count==0)throw new InvalidOperationException("Standalone first order failed");
                World.CaptureState();var copy=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(World.State)));for(int i=0;i<3600;i++){World.Tick();copy.Tick();}World.CaptureState();copy.CaptureState();if(JsonUtility.ToJson(World.State)!=JsonUtility.ToJson(copy.State))throw new InvalidOperationException("Save continuation mismatch");
            }
            catch(Exception e){error=e.ToString();}
            File.WriteAllText(Path.Combine(folder,"Walkthrough.txt"),error??walkthrough);
            if(error==null){PollBusinessEvents();Drawer="订单";Hud.RebuildDrawer();yield return Capture("OrderBoard",folder);Drawer="市场";Hud.RebuildDrawer();yield return Capture("Market",folder);}
            Drawer="报表";Hud.RebuildDrawer();View.Rebuild();yield return Capture("Settlement",folder);
            var zoomButton=Hud.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name.StartsWith("界面缩放"));
            zoomButton.onClick.Invoke();zoomButton.onClick.Invoke();yield return Capture("LargeUI",folder);zoomButton.onClick.Invoke();
            if(error!=null){File.WriteAllText(Path.Combine(folder,"Verification.txt"),"FAIL\n"+error);Application.Quit(1);yield break;}
            Drawer="";Hud.RebuildDrawer();NewGame();Paused=false;
            if(!Environment.GetCommandLineArgs().Contains("--hardware-short"))
            {
                yield return SampleFrames(folder,"Starter");
                Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24});Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});Submit(new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Aluminum,Quantity=12});
                yield return SampleFrames(folder,"Expanded");
            }
            File.WriteAllText(Path.Combine(folder,"Verification.txt"),"PASS: command-driven standalone production, delivery, automatic collection/pickup and save continuation.\nScreenshots require visual inspection. Does not certify physical mouse/keyboard playtesting.\n"+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+" / Unity "+Application.unityVersion+"\nNo user saves read or written.");Application.Quit(0);
        }
        private IEnumerator CaptureTransport(string folder)
        {
            NewGame();Submit(new WorldCommand{Kind=CommandKind.Collect,Resource=1});Paused=false;
            var frames=Path.Combine(folder,"TransportFrames");Directory.CreateDirectory(frames);var csv=new List<string>{"frame,realtime,tick,x,z,paused"};
            ViewCamera.orthographicSize=13;Drawer="运输";Hud.RebuildDrawer();
            for(var frame=0;frame<180;frame++)
            {
                Paused=frame>=70&&frame<85;cameraFocus=View.TruckVisual.position;yield return new WaitForSecondsRealtime(1f/15);yield return new WaitForEndOfFrame();
                var p=View.TruckVisual.position;csv.Add(frame+","+Time.realtimeSinceStartup.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+World.State.Tick+","+p.x.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+p.z.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+Paused);
                ScreenCapture.CaptureScreenshot(Path.Combine(frames,frame.ToString("D4")+".png"));
            }
            File.WriteAllLines(Path.Combine(folder,"TransportMotion.csv"),csv);Paused=true;NewGame();
        }
        private IEnumerator CaptureConveyorCorner(string folder)
        {
            if(!View.TryGetConveyorCorner(out var point))yield break;
            cameraFocus=point;ViewCamera.orthographicSize=3.3f;var canvas=Hud.GetComponentInChildren<Canvas>();canvas.enabled=false;
            var labels=View.GetComponentsInChildren<TMPro.TextMeshPro>();foreach(var label in labels)label.enabled=false;
            yield return Capture("ConveyorCorner",folder);foreach(var label in labels)label.enabled=true;canvas.enabled=true;FocusFactory();
        }
        private IEnumerator Capture(string name,string folder)
        {
            Hud.Refresh();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();var pixels=texture.GetPixels32();var rendered=false;
            for(var i=0;i<pixels.Length;i+=Math.Max(1,pixels.Length/512))if(pixels[i].r+pixels[i].g+pixels[i].b>8){rendered=true;break;}
            if(!rendered){File.WriteAllText(Path.Combine(folder,"Verification.txt"),"FAIL: capture was black; the player must have a rendering window.");Destroy(texture);Application.Quit(2);yield break;}
            File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);for(int i=0;i<4;i++)yield return null;
        }
        private IEnumerator SampleFrames(string folder,string label)
        {
            var until=Time.realtimeSinceStartup+30;while(Time.realtimeSinceStartup<until)yield return null;
            var frames=new List<float>();until=Time.realtimeSinceStartup+120;while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();File.WriteAllText(Path.Combine(folder,label+"Performance.txt"),"Visible player; VSync=0; unlimited; warmup 30s; sample 120s\n"+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+"\nResolution "+Screen.width+"x"+Screen.height+" Quality "+QualitySettings.names[QualitySettings.GetQualityLevel()]+"\nNodes "+World.State.Nodes.Count+" Routes "+World.State.Routes.Count+"\nFrames "+frames.Count+" Mean ms "+frames.Average().ToString("F3")+" P95 ms "+frames[(int)(frames.Count*.95)].ToString("F3")+"\nBudget mean<=16.7, P95<=25: "+(frames.Average()<=16.7&&frames[(int)(frames.Count*.95)]<=25));
        }
    }
    // Command-driven business routes using the formal catalog: no injected cash, items or unlocks.
    public static class HardwareWalkthrough
    {
        private static void Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();if(!w.LastResult.Success)throw new InvalidOperationException(c.Kind+": "+w.LastResult.Message);}
        private static void RunTicks(WorldSimulation w,int count){for(int i=0;i<count;i++)w.Tick();}
        public static void BuildChain(WorldSimulation w){foreach(var s in w.Settings.Hardware.Slots)Do(w,new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z,Rotation=s.Rotation});}
        private static long Until(WorldSimulation w,Func<bool> done,int seconds,string what)
        {
            var start=w.State.Tick;while(!done()){if(w.State.Tick-start>seconds*60L)throw new InvalidOperationException(what+" not reached within "+seconds+"s");w.Tick();}
            w.AssertConserved();return w.State.Tick-start;
        }
        private static string Grade(Settlement s)=>s.Grade+" "+s.Income/100m+"¥ rep"+(s.ReputationDelta>0?"+":"")+s.ReputationDelta;
        public static string RunOpening()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            var first=Until(w,()=>w.State.Hardware.FirstProductTick>0,120,"First card");var paid=first+Until(w,()=>w.State.Hardware.Settlements.Count==1,240,"First income");
            if(first>60*60||paid>180*60)throw new InvalidOperationException("Opening pacing missed: first "+first/60d+"s, income "+paid/60d+"s");
            var second=w.State.Hardware.Orders.Single(o=>o.Template==2);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=second.Id});
            var hint=HardwareGuide.Evaluate(w);Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=hint.Material,Quantity=hint.Quantity});
            Until(w,()=>second.Phase==HardwareOrderPhase.Delivered||second.Phase==HardwareOrderPhase.Expired,900,"Second contract");
            // Rejects may consume a heatsink; top up exactly like a player following the guide.
            var s=w.State.Hardware.Settlements;
            return "Opening: first card "+first/60d+"s, first income "+paid/60d+"s after the line was built; contract 1 "+Grade(s[0])+(s.Count>1?"; contract 2 "+Grade(s[1]):"; contract 2 "+second.Phase)+"; reputation "+w.State.Hardware.Reputation+"; cash "+w.State.Money/100m+"¥.";
        }
        public static string RunExpansion()
        {
            long Time(bool extra)
            {
                var w=new WorldSimulation(HardwareCatalog.Create());BuildChain(w);if(extra)Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.BoardAssembly,X=12,Z=24});
                Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=8});
                return Until(w,()=>w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Standard)>=12,600,"12 cards");
            }
            var single=Time(false);var dual=Time(true);
            if(dual>=single)throw new InvalidOperationException("A second board assembly must raise throughput");
            return "Expansion: 12 cards with one board assembly "+single/60d+"s, with two "+dual/60d+"s ("+(100-dual*100/single)+"% faster).";
        }
        public static string RunMarket()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            Until(w,()=>w.State.Hardware.Settlements.Count==1,240,"First income");RunTicks(w,1);
            var board=w.Offers.Count(o=>o.Template==0);var forecast=0L;
            Until(w,()=>{if(forecast==0&&w.ForecastEvent!=null)forecast=w.State.Tick;return w.CurrentEvent!=null;},w.Settings.Hardware.CalmSeconds+5,"Market event");
            var e=w.CurrentEvent;
            return "Market: board opened with "+board+" offers; forecast "+(w.State.Tick-forecast)/60+"s before 「"+e.Name+"」; core "+w.PurchasePrice(HardwareCatalog.Core)/100m+"¥, memory "+w.PurchasePrice(HardwareCatalog.Memory)/100m+"¥, standard contract price "+w.OrderPricePercent(HardwareCatalog.Standard)+"%.";
        }
        public static string RunMake()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            Until(w,()=>w.State.Hardware.Settlements.Count==1,240,"First income");var second=w.State.Hardware.Orders.Single(o=>o.Template==2);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=second.Id});
            Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});var hint=HardwareGuide.Evaluate(w);
            Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=hint.Material,Quantity=hint.Quantity});
            Until(w,()=>second.Phase==HardwareOrderPhase.Delivered||second.Phase==HardwareOrderPhase.Expired,900,"Self-made contract");
            return "Make heatsinks: bought "+hint.Quantity+" aluminum; contract 2 "+second.Phase+"; cash "+w.State.Money/100m+"¥; equipment investment "+w.State.Nodes.Sum(n=>n.Investment)/100m+"¥ (separate from margin).";
        }
        public static string RunRecovery()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            Until(w,()=>w.State.Hardware.Settlements.Count==1,240,"First income");RunTicks(w,1);
            // Spend down to zero through real purchases.
            foreach(var m in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board})while(w.State.Money>=w.PurchasePrice(m)+w.Settings.Hardware.DeliveryFee){w.Enqueue(new WorldCommand{Kind=CommandKind.Purchase,Material=m,Quantity=1});w.FlushCommands();if(!w.LastResult.Success)break;}
            for(var i=0;i<300*60&&w.State.Money>0;i++)w.Tick();
            Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1});Until(w,()=>w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Ore)>=20,240,"Ore recovery");
            Do(w,new WorldCommand{Kind=CommandKind.SpotSell,Material=HardwareCatalog.Ore,Quantity=20});
            var repair=w.Offers.First(o=>o.Client==WorldSimulation.RepairClient);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=repair.Id});w.AssertConserved();
            return "Recovery: cash spent to "+w.State.Money/100m+"¥ after selling 20 ore; repair offer #"+repair.Id+" ("+repair.Quantity+" cards) accepted as fallback. No cash injection.";
        }
    }
}
