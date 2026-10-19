using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class EP04Game
    {
        // Explicit standalone verification mode; never runs in a normal player session.
        private IEnumerator PlayerSmoke()
        {
            Paused=true;yield return null;
            var directory=Path.Combine(Directory.GetCurrentDirectory(),"Reports/EP04");Directory.CreateDirectory(directory);
            string error=null;
            try
            {
                foreach(var c in new[]{new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Warehouse,X=5,Z=16},new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=15,Z=16},new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Sales,X=32,Z=16}})
                {Submit(c);if(!World.LastResult.Success)throw new InvalidOperationException(World.LastResult.Message);}
                for(var i=0;i<7200;i++)World.Tick();World.AssertConserved();if(World.State.Sold==0)throw new InvalidOperationException("No sales in standalone");
                World.CaptureState();var copy=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(World.State)));for(var i=0;i<600;i++){World.Tick();copy.Tick();}
                World.CaptureState();copy.CaptureState();if(JsonUtility.ToJson(World.State)!=JsonUtility.ToJson(copy.State))throw new InvalidOperationException("Standalone save continuation mismatch");
                RebuildFactoryViews();RefreshUI();
            }
            catch(Exception e){error=e.ToString();}
            if(error!=null){File.WriteAllText(Path.Combine(directory,"PlayerSmoke.txt"),"FAIL\n"+error);Application.Quit(1);yield break;}
            Paused=false;var frames=new List<float>();for(var i=0;i<180;i++){yield return null;if(i>=30)frames.Add(Time.unscaledDeltaTime*1000);}
            Paused=true;frames.Sort();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"Player.png"));for(var i=0;i<6;i++)yield return null;
            File.WriteAllText(Path.Combine(directory,"PlayerSmoke.txt"),"PASS: standalone scene, UI, command building, production, sales, conservation, save continuation.\n"+
                "Unity: "+Application.unityVersion+"\nDevice: "+SystemInfo.graphicsDeviceName+"\nResolution: "+Screen.width+"x"+Screen.height+
                "\nNodes: "+World.State.Nodes.Count+" / Routes: "+World.State.Routes.Count+" / Sold: "+World.State.Sold+
                "\nRendered frame ms (150 samples, starter factory): mean="+frames.Average().ToString("F3")+" p95="+frames[(int)(frames.Count*.95)].ToString("F3")+" max="+frames.Last().ToString("F3")+
                "\nNot the synthetic 60-node performance fixture. No user save file modified.");
            Application.Quit(0);
        }
    }
}
