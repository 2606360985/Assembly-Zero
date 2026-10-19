using System;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class EP04BusinessTests
    {
        private static CommandResult Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static int Build(WorldSimulation w,BuildingKind kind,int x,int z,int factory=1)
        {
            var result=Do(w,new WorldCommand{Kind=CommandKind.Build,Factory=factory,Building=kind,X=x,Z=z});Assert.IsTrue(result.Success,result.Message);return result.Entity;
        }
        private static WorldSimulation Chain()
        {
            var w=new WorldSimulation(new EconomySettings());Build(w,BuildingKind.Warehouse,5,16);Build(w,BuildingKind.Manufacturer,15,16);Build(w,BuildingKind.Sales,32,16);return w;
        }
        private static void Run(WorldSimulation w,int ticks){for(var i=0;i<ticks;i++)w.Tick();w.AssertConserved();}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        [Test] public void EP04_EmptyStartAndDownstreamFirstAreLegal()
        {
            var w=new WorldSimulation(new EconomySettings());Assert.AreEqual(0,w.State.Nodes.Count);
            Build(w,BuildingKind.Manufacturer,15,16);Run(w,600);Assert.AreEqual(0,w.State.Consumed);Assert.AreEqual(0,w.State.Sold);
            Build(w,BuildingKind.Sales,32,16);Build(w,BuildingKind.Warehouse,5,16);Run(w,9000);
            Assert.Greater(w.State.Sold,0);Assert.GreaterOrEqual(w.State.Unlocked,2);
        }
        [Test] public void EP04_ProductionSalesAndFreeBuildingsDoNotMintRefunds()
        {
            var w=Chain();var before=w.State.Money;Run(w,7200);Assert.Greater(w.State.Money,before);Assert.AreEqual(w.State.Sold*25,w.State.Revenue);
            var station=w.State.Nodes.Single(n=>n.Kind==BuildingKind.Sales);Assert.AreEqual(0,station.Investment);
            var money=w.State.Money;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Remove,Node=station.Id}).Success);Assert.AreEqual(money,w.State.Money);
        }
        [Test] public void EP04_SaveRestoresProcessingTransportAndVehicleContinuation()
        {
            var a=Chain();Do(a,new WorldCommand{Kind=CommandKind.Collect,Resource=1});Run(a,1100);
            var json=Save(a);var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(json));Assert.AreEqual(json,Save(b));
            for(var i=0;i<5000;i++){a.Tick();b.Tick();}Assert.AreEqual(Save(a),Save(b));
        }
        [Test] public void EP04_FailedPlacementAndMoveAreAtomic()
        {
            var w=Chain();Run(w,200);var before=Save(w);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=15,Z=16}).Success);
            w.State.Events.RemoveAt(w.State.Events.Count-1);Assert.AreEqual(before,Save(w));
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Move,Node=2,X=5,Z=16}).Success);Assert.AreEqual(15,w.Node(2).X);
        }
        [Test] public void EP04_MovePreservesBatchAndConservesInFlightCargo()
        {
            var w=Chain();Run(w,1100);var node=w.Node(2);var work=node.WorkRemaining;var batch=node.Batch.Select(g=>g.Id).ToArray();
            var result=Do(w,new WorldCommand{Kind=CommandKind.Move,Node=2,X=17,Z=24,Rotation=1});Assert.IsTrue(result.Success,result.Message);
            Assert.AreEqual(work,node.WorkRemaining);CollectionAssert.AreEqual(batch,node.Batch.Select(g=>g.Id));w.AssertConserved();Run(w,3000);Assert.Greater(w.State.Sold,0);
        }
        [Test] public void EP04_ExplorationHarvestAndPaidTeamsHaveRealCargo()
        {
            var w=Chain();Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1});Run(w,5000);Assert.IsTrue(w.State.Gathered);Assert.AreEqual(VehiclePhase.Idle,w.State.Vehicles[0].Phase);
            var before=w.State.Created;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.HireCollector,Resource=1}).Success);Run(w,9000);
            Assert.Greater(w.State.Created,before);Assert.IsTrue(w.State.Hired);Assert.Greater(w.State.Expenses,w.Settings.MachineCost+w.Settings.CollectorCost);
        }
        [Test] public void EP04_RoutesAreOrthogonalAndAvoidFootprints()
        {
            var w=Chain();Build(w,BuildingKind.Processor,24,24);
            foreach(var r in w.State.Routes)
            {
                for(var i=1;i<r.Points.Count;i++)Assert.AreEqual(1,GridPoint.Distance(r.Points[i-1],r.Points[i]));
                foreach(var p in r.Points)Assert.IsFalse(w.State.Nodes.Any(n=>Math.Abs(p.X-n.X)<=1&&Math.Abs(p.Z-n.Z)<=1));
            }
            Assert.IsNull(GridRouting.WorldPath(new GridPoint(0,0),new GridPoint(50,20)));
        }
        [Test] public void EP04_BackpressureAndResumeDoNotConsumePartially()
        {
            var w=new WorldSimulation(new EconomySettings());Build(w,BuildingKind.Warehouse,5,16);Build(w,BuildingKind.Manufacturer,15,16);
            Run(w,8000);Assert.AreEqual(4,w.Node(2).Output.Count);var consumed=w.State.Consumed;Run(w,1000);Assert.AreEqual(consumed,w.State.Consumed);
            Build(w,BuildingKind.Sales,32,16);Run(w,6000);Assert.Greater(w.State.Sold,0);Assert.Greater(w.State.Consumed,consumed);
        }
        [Test] public void EP04_TransportBackendRestoresCompressedQueues()
        {
            var scenario=new ScenarioDefinition{ExternalLogistics=true,MinimumSpacing=512,SpeedUnitsPerTick=50};var topology=BeltTopology.CreateStraight(1,4096);
            var a=new GapTransportLineSimulation();a.Reset(scenario,topology);
            for(var i=1;i<=6;i++)Assert.IsTrue(a.TryInsertExisting(new BeltAccessPort(new LaneId(0),i*512,i*512),new ItemId(i),new PartTypeId(8),out _));
            for(var i=1;i<=200;i++)a.TransportTick(i);var b=new GapTransportLineSimulation();b.ImportRuntimeState(scenario,topology,a.ExportRuntimeState());
            Assert.AreEqual(a.CalculateStateHash(),b.CalculateStateHash());for(var i=201;i<400;i++){a.TransportTick(i);b.TransportTick(i);Assert.AreEqual(a.CalculateStateHash(),b.CalculateStateHash());}
        }
        [Test] public void EP04_FullWarehouseRejectsRecoveryWithoutChangingMoneyOrCargo()
        {
            var w=Chain();Run(w,1000);var store=w.Node(1);var settings=w.Settings;
            // Capacity reduction is a failure fixture, not a gameplay progression shortcut.
            settings.WarehouseCapacity=store.Output.Count;
            var node=w.Node(2);var x=node.X;var money=w.State.Money;var cargo=w.State.Routes.Sum(r=>r.Cargo.Count);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Move,Node=2,X=17,Z=24}).Success);
            Assert.AreEqual(x,node.X);Assert.AreEqual(money,w.State.Money);Assert.AreEqual(cargo,w.State.Routes.Sum(r=>r.Cargo.Count));w.AssertConserved();
        }
        [Test] public void EP04_NoFundsDoesNotCreatePaidBuildingsOrTeams()
        {
            var w=new WorldSimulation(new EconomySettings{StartingMoney=0});Build(w,BuildingKind.Warehouse,5,16);Build(w,BuildingKind.Sales,32,16);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=15,Z=16}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.HireCollector,Resource=1}).Success);
            Assert.AreEqual(2,w.State.Nodes.Count);Assert.AreEqual(1,w.State.Vehicles.Count);Assert.AreEqual(0,w.State.Money);
        }
        [Test] public void EP04_PendingRemovalCanBeCancelledWithoutLosingTheBatch()
        {
            var w=Chain();for(var i=0;i<3000&&w.Node(2).Batch.Count==0;i++)w.Tick();Assert.IsNotEmpty(w.Node(2).Batch);
            var ids=w.Node(2).Batch.Select(g=>g.Id).ToArray();Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Remove,Node=2}).Success);
            Assert.IsTrue(w.Node(2).PendingRemove);Do(w,new WorldCommand{Kind=CommandKind.CancelChange,Node=2});Assert.IsFalse(w.Node(2).PendingRemove);
            CollectionAssert.AreEqual(ids,w.Node(2).Batch.Select(g=>g.Id));Run(w,1000);Assert.IsNotNull(w.Node(2));w.AssertConserved();
        }
    }
}
