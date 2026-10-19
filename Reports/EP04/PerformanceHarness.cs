// Run with Unity CLI run_script after compilation/tests finish. Synthetic scale fixture;
// it measures the real simulation, not a claim of a playable collision-free layout.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Domain.EP04;
using UnityEngine;

public static class EP04PerformanceHarness
{
    public static string Run()
    {
        var state=new WorldState{Money=800};state.Factories.Add(new FactorySite{Id=1,Owned=true});state.Factories.Add(new FactorySite{Id=2,Owned=true});
        for(var f=0;f<2;f++)for(var i=0;i<30;i++)state.Nodes.Add(new BusinessNode{Id=f*30+i+1,Factory=f+1,X=4+i%5*7,Z=3+i/5*5,Kind=i<15?BuildingKind.Warehouse:BuildingKind.Processor,RecipeId=i<15?"":"shell"});
        var remaining=2000;
        for(var i=0;i<120;i++)
        {
            var f=i/60;var source=state.Nodes[f*30+i%15];var target=state.Nodes[f*30+15+(i%15+i/15)%15];
            var route=new BusinessRoute{Id=i+1,Factory=f+1,From=source.Id,To=target.Id,Material=9,Length=60000,Points=new List<GridPoint>{source.Port(true),new GridPoint(source.X+2,target.Z),target.Port(false)},Saved=new BeltRuntimeState{NextId=2200,GateOpen=true}};
            var amount=Math.Min(i<80?17:16,remaining);remaining-=amount;
            for(var j=0;j<amount;j++){var item=new Goods(state.NextItem++,f+1,9);route.Cargo.Add(item);route.Saved.Items.Add(new PersistedBeltItem{Id=item.Id,Lane=0,Material=9,Distance=j*512});state.Created++;}
            state.Routes.Add(route);
        }
        if(remaining!=0)throw new Exception("Fixture count mismatch");
        var settings=new EconomySettings();var world=new WorldSimulation(settings,state);world.CaptureState();
        Directory.CreateDirectory("Reports/EP04");File.WriteAllText("Reports/EP04/PerformanceSave.json",JsonUtility.ToJson(state));
        for(var i=0;i<120;i++)world.Tick();
        var times=new List<double>();var snapshotTimes=new List<double>();var watch=new Stopwatch();
        for(var i=0;i<1200;i++)
        {
            watch.Restart();world.Tick();times.Add(watch.Elapsed.TotalMilliseconds);
            if(i%6==0){watch.Restart();foreach(var r in state.Routes)r.Backend.CreateSnapshot(r.Snapshot);snapshotTimes.Add(watch.Elapsed.TotalMilliseconds);}
        }
        world.AssertConserved();
        string Stats(List<double> values){values.Sort();return "mean="+values.Average().ToString("F4")+"ms p95="+values[(int)(values.Count*.95)].ToString("F4")+"ms max="+values.Last().ToString("F4")+"ms";}
        var result="Synthetic capacity fixture: 2 factories, 60 nodes, 120 routes, 2000 initial in-transit goods.\n"+
            "This fixture tests simulation/transport load; it does not certify spatial routing or frame rate.\nFixed tick (includes periodic audit): "+Stats(times)+"\nAll-route snapshot preparation: "+Stats(snapshotTimes)+"\nUnity "+Application.unityVersion+" / "+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName;
        File.WriteAllText("Reports/EP04/Performance.txt",result);return result;
    }
}
