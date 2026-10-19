using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.Editor
{
    public static class EP04Acceptance
    {
        public static string Walkthrough()
        {
            Directory.CreateDirectory("Reports/EP04");var log=new StringBuilder();var clock=Stopwatch.StartNew();
            var w=new WorldSimulation(new EconomySettings());
            void Record(string message){log.AppendLine((w.State.Tick/60.0).ToString("F1")+"s / cash "+w.State.Money+" / "+message);File.WriteAllText("Reports/EP04/Walkthrough.txt",log.ToString());}
            void Wait(Func<bool> condition,string reason,int limit=120000)
            {
                var start=w.State.Tick;while(!condition()&&w.State.Tick-start<limit)w.Tick();
                if(!condition())throw new InvalidOperationException(reason+" timed out. "+string.Join("; ",w.State.Nodes.Select(n=>n.Id+":"+n.RecipeId+":"+n.Status))+" | cash "+w.State.Money);
            }
            CommandResult Do(WorldCommand c){w.Enqueue(c);w.FlushCommands();var r=w.LastResult;if(!r.Success)throw new InvalidOperationException(c.Kind+": "+r.Message);return r;}
            void Money(int amount)=>Wait(()=>w.State.Money>=amount,"Earn "+amount);
            int Build(BuildingKind kind,int x,int z,int factory=1)
            {
                Money(kind==BuildingKind.Manufacturer?w.Settings.MachineCost:kind==BuildingKind.Processor?w.Settings.ProcessorCost:0);
                if(w.PlacementError(factory,x,z)!=null)
                {
                    var found=false;for(var iz=5;iz<29&&!found;iz+=5)for(var ix=5;ix<36&&!found;ix+=5)if(w.PlacementError(factory,ix,iz)==null){x=ix;z=iz;found=true;}
                    if(!found)throw new InvalidOperationException("No placement available");
                }
                return Do(new WorldCommand{Kind=CommandKind.Build,Building=kind,Factory=factory,X=x,Z=z}).Entity;
            }
            void Upgrade(int id,int level)
            {
                while(w.Node(id).Level<level){Money(w.Node(id).Level==1?w.Settings.Level2Cost:w.Settings.Level3Cost);Do(new WorldCommand{Kind=CommandKind.Upgrade,Node=id});}
            }
            void Recipe(int id,string recipe){Do(new WorldCommand{Kind=CommandKind.Recipe,Node=id,Recipe=recipe});Wait(()=>w.Node(id).RecipeId==recipe&&!w.Node(id).PendingRemove&&w.Node(id).PendingRecipe=="","Recipe switch");}
            try
            {
                Build(BuildingKind.Warehouse,5,16);var kit=Build(BuildingKind.Manufacturer,15,16);Build(BuildingKind.Sales,32,16);
                Do(new WorldCommand{Kind=CommandKind.Collect,Resource=1});Wait(()=>w.State.Unlocked>=2,"First sale");Record("First T1 sale");
                Money(w.Settings.CollectorCost+w.Settings.TripFee);Do(new WorldCommand{Kind=CommandKind.HireCollector,Resource=1});Record("Metal collection team");
                Wait(()=>w.State.Vehicles[0].Phase==VehiclePhase.Idle,"Pickup returns");
                foreach(var resource in new[]{3,2})
                {
                    var r=w.State.Resources.Single(s=>s.Id==resource);Do(new WorldCommand{Kind=CommandKind.Explore,X=r.X,Z=r.Z});Wait(()=>w.State.Vehicles[0].Phase==VehiclePhase.Idle,"Discovery");
                    Record("Discovered "+resource);
                }
                var drive=Build(BuildingKind.Manufacturer,15,6);Upgrade(drive,2);Recipe(drive,"drive");
                Wait(()=>w.State.Unlocked>=3,"First T2 sale");Record("First T2 sale");
                foreach(var resource in new[]{3,2}){Money(w.Settings.CollectorCost+w.Settings.TripFee);Do(new WorldCommand{Kind=CommandKind.HireCollector,Resource=resource});}
                var shell=Build(BuildingKind.Processor,5,26);var circuit=Build(BuildingKind.Processor,15,26);Recipe(circuit,"circuit");
                var robot=Build(BuildingKind.Manufacturer,26,6);Upgrade(robot,3);Recipe(robot,"robot");
                Wait(()=>w.State.Unlocked>=4,"First T3 sale");Record("First T3 sale");
                var second=w.Factory(2);Do(new WorldCommand{Kind=CommandKind.Explore,X=second.Dock.X,Z=second.Dock.Z});Wait(()=>second.Discovered,"Second plot discovery");
                Money(w.Settings.SecondFactoryCost+w.Settings.MachineCost);Do(new WorldCommand{Kind=CommandKind.BuyFactory,Factory=2});Record("Second plot purchased");
                Build(BuildingKind.Warehouse,5,16,2);Build(BuildingKind.Manufacturer,15,16,2);Build(BuildingKind.Sales,32,16,2);
                Wait(()=>w.State.Vehicles[0].Phase==VehiclePhase.Idle,"Pickup reaches plot");Do(new WorldCommand{Kind=CommandKind.Transfer,Factory=1,TargetFactory=2,Material=8});
                Wait(()=>w.State.Complete,"Second factory sale");Record("Second factory produced and sold T1 after physical transfer");
                Money(w.Settings.HaulerCost+w.Settings.TripFee);Do(new WorldCommand{Kind=CommandKind.HireHauler,Factory=1,TargetFactory=2,Material=8});
                var sales=second.SalesCount;Wait(()=>second.SalesCount>sales+3,"Automated cross-factory trade");
                w.CaptureState();var json=JsonUtility.ToJson(w.State);var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(json));
                for(var i=0;i<3600;i++){w.Tick();restored.Tick();}w.CaptureState();restored.CaptureState();
                if(JsonUtility.ToJson(w.State)!=JsonUtility.ToJson(restored.State))throw new InvalidOperationException("Full-world save continuation mismatch");
                Record("Save/load continuation matched for 3600 ticks");
                File.WriteAllText("Reports/EP04/WalkthroughSave.json",JsonUtility.ToJson(w.State));
                log.AppendLine("PASS. Real wall time "+clock.Elapsed.TotalSeconds.ToString("F2")+"s; no money or unlock injection; all gameplay actions used commands.");
                File.WriteAllText("Reports/EP04/Walkthrough.txt",log.ToString());return log.ToString();
            }
            catch(Exception e){log.AppendLine("FAIL "+e);File.WriteAllText("Reports/EP04/Walkthrough.txt",log.ToString());throw;}
        }
    }
}
