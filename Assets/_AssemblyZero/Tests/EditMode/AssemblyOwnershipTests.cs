using System;
using System.Linq;
using AssemblyZero.Domain;
using NUnit.Framework;
namespace AssemblyZero.Tests
{
    internal static class EP02TestFactory
    {
        public static AssemblyScenarioDefinition Config(bool full=false,bool source=false)
        {
            var types=full?new[]{3,6,7,4,5,2}:new[]{3}; var n=types.Length;
            var c=new AssemblyScenarioDefinition{MinimumSpacing=64,SourceEnabled=source,SourceInterval=9,RejectInterval=10,ScanTicks=3,ShowcaseTicks=5,FullRobotManifest=full,SupplySpeeds=new[]{8,6},ProductSpeed=64,ProductInterval=1000,ProductSpacing=256,ProductLength=n*1024};
            c.SegmentEnds=Enumerable.Range(1,n).Select(i=>i*1024).ToArray(); c.SourceParts=types.Select(t=>new PartTypeId(t)).ToArray();c.Manifest=new AssemblyManifest{RequiredParts=(PartTypeId[])c.SourceParts.Clone()};
            c.Sockets=types.Select((t,i)=>new AssemblySocketData{Id=new AssemblySocketId(i),AcceptedPart=new PartTypeId(t)}).ToArray();
            c.Stations=types.Select((t,i)=>new AssemblyStationDefinition{Id=i,PartType=t,SocketId=i,Distance=512+i*1024}).ToArray();
            c.Inserters=types.Select((t,i)=>new InserterData{Id=new InserterId(i),AllowedPart=new PartTypeId(t),TargetSocket=new AssemblySocketId(i),StationId=i,PickingTicks=2,CarryingTicks=2,PlacingTicks=2,ReturningTicks=2,Port=new BeltAccessPort(new LineId(0),3,0,8192,512,1UL<<t)}).ToArray();return c;
        }
        public static EP02Simulation World(AssemblyScenarioDefinition c,BackendKind b=BackendKind.Gap)=>new EP02Simulation(c,BeltTopology.CreateStraight(2,8192),b);
        public static void Run(EP02Simulation w,int ticks){for(var i=0;i<ticks;i++){w.Tick();Assert.IsTrue(w.Statistics.Conserved);Assert.IsTrue(w.Snapshot.ProductsConserved);}}
        public static void Until(EP02Simulation w,Func<bool> done,int limit=300){for(var i=0;i<limit&&!done();i++)w.Tick();Assert.IsTrue(done(),"condition not reached");}
        public static InitialAssemblyPart[] Stock(AssemblyScenarioDefinition c)=>c.SourceParts.Select((p,i)=>new InitialAssemblyPart(p.Value,i%2,512+i*128)).ToArray();
    }
    public sealed class AssemblyOwnershipTests
    {
        [Test] public void CompetingArmsBindOneProductAndReserveOnePart()
        {
            var c=EP02TestFactory.Config();var other=c.Inserters[0].Clone();other.Id=new InserterId(4);c.Inserters=new[]{other,c.Inserters[0]};c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);
            EP02TestFactory.Until(w,()=>w.Statistics.Reserved==1);Assert.AreEqual(1,w.Snapshot.Inserters[0].ReservedItem);Assert.AreEqual(0,w.Snapshot.Inserters[1].ReservedItem);Assert.AreEqual(w.Snapshot.Inserters[0].TargetProductId,w.Snapshot.Inserters[1].TargetProductId);
        }
        [Test] public void LockedProductStopsItsSegmentWithoutTakingMaterial()
        {
            var c=EP02TestFactory.Config();c.Sockets[0].Locked=true;c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Run(w,40);
            Assert.AreEqual(1,w.Statistics.OnBelt);Assert.AreEqual(0,w.Statistics.Held);Assert.AreEqual(ProductState.Locked,w.Snapshot.Products[0].State);Assert.AreEqual(0,w.Snapshot.SegmentSpeeds[0]);
        }
        [Test] public void BeltGripAndProductSocketPreserveItemAndProductIdentity()
        {
            var c=EP02TestFactory.Config();c.InitialParts=EP02TestFactory.Stock(c);c.ScanTicks=100;var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Statistics.Held==1);
            var item=w.Snapshot.Items.Single();Assert.AreEqual(1,item.ItemId);Assert.AreEqual(1,item.ProductId);EP02TestFactory.Until(w,()=>w.Statistics.Installed==1);Assert.AreEqual(1,w.Snapshot.Products.Single().Sockets.Single().ItemId);Assert.IsTrue(w.Statistics.Conserved);
        }
        [Test] public void OccupiedProductCannotConsumeAnotherPart()
        {
            var c=EP02TestFactory.Config();c.InitialParts=new[]{new InitialAssemblyPart(3,0,512),new InitialAssemblyPart(3,0,384)};c.ScanTicks=1000;var w=EP02TestFactory.World(c);EP02TestFactory.Run(w,60);
            Assert.AreEqual(1,w.Statistics.OnBelt);Assert.AreEqual(1,w.Snapshot.Inserters[0].Transfers);
        }
        [Test] public void DisableReleasesBothReservations()
        {
            var c=EP02TestFactory.Config();c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Statistics.Reserved==1);
            w.EnqueueCommand(new AssemblyCommand(w.TickNumber+1,0,AssemblyCommandKind.SetInserterEnabled,0,false));w.Tick();Assert.AreEqual(0,w.Statistics.Reserved);Assert.AreEqual(1,w.Statistics.OnBelt);Assert.AreEqual(-1,w.Snapshot.Products[0].Sockets[0].ReservedBy);
        }
        [Test] public void DisableHeldPartRejectsAndCanResumeTheSameProduct()
        {
            var c=EP02TestFactory.Config(source:true);c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Statistics.Held==1);
            w.EnqueueCommand(new AssemblyCommand(w.TickNumber+1,0,AssemblyCommandKind.SetInserterEnabled,0,false));w.Tick();Assert.AreEqual(0,w.Statistics.Held);Assert.AreEqual(ProductState.Disabled,w.Snapshot.Products[0].State);Assert.IsTrue(w.TryGetItemRecord(1,out var item));Assert.AreEqual(AssemblyRejectReason.InserterDisabledWhileHolding,item.RejectReason);
            w.EnqueueCommand(new AssemblyCommand(w.TickNumber+1,0,AssemblyCommandKind.SetInserterEnabled,0,true));EP02TestFactory.Until(w,()=>w.Statistics.CompletedConsumed>0);
        }
        [Test] public void SixStationsCompleteOnceAndExitWithoutShowcaseDelay()
        {
            var c=EP02TestFactory.Config(true);c.InitialParts=EP02TestFactory.Stock(c);c.ShowcaseTicks=9999;var w=EP02TestFactory.World(c);EP02TestFactory.Run(w,180);
            Assert.AreEqual(1,w.Statistics.CompletedRobots);Assert.AreEqual(6,w.Statistics.CompletedConsumed);Assert.AreEqual(1,w.Snapshot.ExitedProducts);Assert.AreEqual(1,w.Snapshot.Events.Count(e=>e.Kind==AssemblyEventKind.Complete));
        }
        [Test] public void MissingPartStopsProductAndPropagatesQueue()
        {
            var c=EP02TestFactory.Config(true);c.ProductInterval=8;c.InitialParts=EP02TestFactory.Stock(c).Where(p=>p.Part.Value!=2).ToArray();var w=EP02TestFactory.World(c);EP02TestFactory.Run(w,200);
            Assert.AreEqual(0,w.Statistics.CompletedRobots);Assert.AreEqual(5,w.Statistics.Installed);Assert.Greater(w.Snapshot.ProductSourceBlockedTicks,0);
            Assert.AreEqual(w.Snapshot.Products.Count,w.Snapshot.Products.Select(p=>p.Segment).Distinct().Count());
        }
        [Test] public void LockWhileHoldingKeepsTargetProductUntilUnlocked()
        {
            var c=EP02TestFactory.Config();c.InitialParts=EP02TestFactory.Stock(c);var w=EP02TestFactory.World(c);EP02TestFactory.Until(w,()=>w.Statistics.Held==1);
            w.EnqueueCommand(new AssemblyCommand(w.TickNumber+1,0,AssemblyCommandKind.SetSocketLocked,0,true));EP02TestFactory.Run(w,30);Assert.AreEqual(1,w.Statistics.Held);Assert.AreEqual(1,w.Snapshot.Inserters[0].TargetProductId);
            w.EnqueueCommand(new AssemblyCommand(w.TickNumber+1,0,AssemblyCommandKind.SetSocketLocked,0,false));EP02TestFactory.Until(w,()=>w.Statistics.CompletedConsumed==1);
        }
        [Test] public void ShortPortFailsBeforeStarting(){var c=EP02TestFactory.Config();c.Inserters[0].Port=new BeltAccessPort(new LineId(0),3,0,8,0,8);Assert.Throws<ArgumentException>(()=>EP02TestFactory.World(c));}
        [TestCase(BackendKind.Naive)] [TestCase(BackendKind.Gap)]
        public void SuppliesHaveIndependentDrives(BackendKind backend)
        {
            var c = EP02TestFactory.Config(); c.SupplySpeeds = new[] { 32, 24 };
            c.InitialParts = new[] { new InitialAssemblyPart(3, 0, 512), new InitialAssemblyPart(3, 1, 512) };
            var w = EP02TestFactory.World(c, backend); w.Tick();
            Assert.AreEqual(544, w.Snapshot.Items.Single(i => i.Lane == 0).Distance);
            Assert.AreEqual(536, w.Snapshot.Items.Single(i => i.Lane == 1).Distance);
            Assert.AreEqual(2, w.Snapshot.Items.Select(i => i.ItemId).Distinct().Count());
        }
        [Test] public void AStoppedStationDoesNotStopOtherSegments()
        {
            var c = EP02TestFactory.Config(true); c.Sockets[0].Locked = true;
            var w = EP02TestFactory.World(c); EP02TestFactory.Run(w, 40);
            var stopped = w.Snapshot.SegmentPhases[0]; var free = w.Snapshot.SegmentPhases[1];
            w.Tick(); Assert.AreEqual(stopped, w.Snapshot.SegmentPhases[0]);
            Assert.AreEqual(free + c.ProductSpeed, w.Snapshot.SegmentPhases[1]);
        }
    }
}
