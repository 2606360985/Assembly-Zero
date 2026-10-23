using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareBusinessTests
    {
        private static WorldSimulation New()=>new WorldSimulation(HardwareCatalog.Create());
        private static CommandResult Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static void Run(WorldSimulation w,int seconds){for(var i=0;i<seconds*60;i++)w.Tick();w.AssertConserved();}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        private static void Buy(WorldSimulation w,int material,int amount,int method=0){var r=Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=material,Quantity=amount,Delivery=method});Assert.IsTrue(r.Success,r.Message);}
        private static WorldSimulation Ready(){var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Buy(w,HardwareCatalog.Heat,6);Run(w,850);return w;}
        [Test] public void Hardware_ExteriorDestinationsAreReachableWithoutCrossingFactoryFootprints()
        {
            var w=New();Assert.AreEqual(new GridPoint(12,72),w.State.Resources[0].Point);
            foreach(var f in w.State.Factories)foreach(var end in new[]{w.State.Resources[0].Point,new GridPoint(w.Settings.Hardware.SupplierX,w.Settings.Hardware.SupplierZ)})
            {
                var route=GridRouting.HardwarePath(f.Dock,end,w.State.Factories);Assert.IsNotNull(route);Assert.AreEqual(f.Dock,route.First());Assert.AreEqual(end,route.Last());
                Assert.IsTrue(route.All(p=>!GridRouting.HardwareBlocked(p,w.State.Factories)));Assert.Greater(route.Count,30);
            }
        }
        [TestCase(VehiclePhase.Travelling)] [TestCase(VehiclePhase.Gathering)] [TestCase(VehiclePhase.Unloading)] [TestCase(VehiclePhase.WaitingStock)]
        public void Hardware_OldLayoutMigratesOncePreservingGoodsAndBusiness(VehiclePhase phase)
        {
            var a=New();Buy(a,HardwareCatalog.Carton,3,2);Do(a,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Run(a,6);
            var v=a.State.Vehicles[0];var goods=a.State.Nodes[0].Output.Last();a.State.Nodes[0].Output.Remove(goods);v.Cargo.Add(goods);
            a.State.Hardware.LayoutVersion=0;a.State.Resources[0].X=1;a.State.Resources[0].Z=27;
            v.Phase=phase;v.Position=phase==VehiclePhase.Unloading?a.Factory(1).Dock:new GridPoint(10,10);v.Resource=1;v.Loop=true;v.Journey=1;
            v.PickupTask=phase==VehiclePhase.WaitingStock;v.Path=GridRouting.WorldPath(v.Position,new GridPoint(1,27));
            var money=a.State.Money;var tick=a.State.Tick;var deadline=a.State.Hardware.Orders[0].Deadline;var work=a.State.Nodes.Select(n=>n.WorkRemaining).ToArray();
            var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));
            Assert.AreEqual(1,b.State.Hardware.LayoutVersion);Assert.AreEqual(new GridPoint(12,72),b.State.Resources[0].Point);
            Assert.AreEqual(money,b.State.Money);Assert.AreEqual(tick,b.State.Tick);Assert.AreEqual(deadline,b.State.Hardware.Orders[0].Deadline);
            CollectionAssert.AreEqual(work,b.State.Nodes.Select(n=>n.WorkRemaining).ToArray());Assert.AreEqual(goods.Id,b.State.Vehicles[0].Cargo.Single().Id);
            Assert.IsFalse(GridRouting.HardwareBlocked(b.State.Vehicles[0].Position,b.State.Factories));b.AssertConserved();
            var saved=Save(b);var copy=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(saved));Assert.AreEqual(saved,Save(copy));
        }
        [Test] public void Hardware_FormalStartProducesAndDeliversWithoutDriving()
        {
            var w=Ready();Assert.AreEqual(8,w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Standard));
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1}).Success);
            var cash=w.State.Money;Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1}).Success);Assert.AreEqual(cash,w.State.Money);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=2}).Success);w.AssertConserved();
        }
        [Test] public void Hardware_ReservationPreventsCompetingOrdersSpotSaleAndTransfer()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});
            Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=3});Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=3});Assert.AreEqual(0,w.ReservedCount(3));
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.SpotSell,Material=HardwareCatalog.Standard,Quantity=1}).Success);
            Do(w,new WorldCommand{Kind=CommandKind.CancelOrder,Order=1});Assert.AreEqual(0,w.ReservedCount(1));
            Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=3});Assert.AreEqual(1,w.ReservedCount(3));w.AssertConserved();
        }
        [Test] public void Hardware_PurchaseFailureIsAtomicAndFreightIsExact()
        {
            var w=New();var cash=w.State.Money;var stock=w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Core).Quantity;
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=80}).Success);Assert.AreEqual(cash,w.State.Money);Assert.AreEqual(stock,w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Core).Quantity);
            Buy(w,HardwareCatalog.Carton,7);var d=w.State.Hardware.Deliveries.Single();Assert.AreEqual(w.Settings.Hardware.DeliveryFee,d.Cargo.Sum(g=>g.FreightCost));w.AssertConserved();
        }
        [Test] public void Hardware_SaveRestoresOrdersCostsAndInTransitPurchases()
        {
            var a=New();Do(a,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Buy(a,HardwareCatalog.Heat,6);Run(a,30);
            var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));Run(a,300);Run(b,300);Assert.AreEqual(Save(a),Save(b));
        }
        [Test] public void Hardware_OldAndNewSavesCannotCrossLoad()
        {
            Assert.Throws<ArgumentException>(()=>new WorldSimulation(HardwareCatalog.Create(),new WorldSimulation(new EconomySettings()).State));
            Assert.Throws<ArgumentException>(()=>new WorldSimulation(new EconomySettings(),New().State));
        }
        [Test] public void Hardware_ExpiredOrderReleasesGoods()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Run(w,240);Assert.AreEqual(HardwareOrderPhase.Expired,w.State.Hardware.Orders[0].Phase);Assert.AreEqual(0,w.ReservedCount(1));
        }
        [TestCase(false)] [TestCase(true)] public void Hardware_FailedFirstOrderCanRetryAndUnlockQuiet(bool expire)
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});
            if(expire)Run(w,240);else Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.CancelOrder,Order=1}).Success);
            var retry=w.State.Hardware.Orders.Single(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Available);
            Assert.AreNotEqual(1,retry.Id);Assert.AreEqual(0,w.ReservedCount(1));
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=retry.Id}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=retry.Id}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=retry.Id}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=2}).Success);
            Assert.IsFalse(w.State.Hardware.Orders.Any(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Available));w.AssertConserved();
        }
        [Test] public void Hardware_QuietOrderRejectsStandardGoods()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=2});Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=2});Assert.AreEqual(0,w.ReservedCount(2));Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=2}).Success);
        }
        [Test] public void Hardware_ManualDrivingIsFrozenAndRescueUnloadsAutomatically()
        {
            var w=New();Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.TakeControl}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=1}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1}).Success);Run(w,20);var v=w.State.Vehicles[0];Assert.IsNotEmpty(v.Cargo);var ids=v.Cargo.Select(g=>g.Id).ToArray();
            Do(w,new WorldCommand{Kind=CommandKind.RecoverVehicle});Assert.Greater(v.RescueTicks,0);Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1}).Success);Run(w,61);Assert.AreEqual(w.Factory(1).Dock,v.Position);Assert.IsEmpty(v.Cargo);Assert.IsTrue(ids.All(id=>w.FinishedStock(1).Any(g=>g.Id==id)));w.AssertConserved();
        }
        [Test] public void Hardware_PickupRepeatsMiningWaitsAtFullWarehouseAndStopsSafely()
        {
            var w=New();var v=w.State.Vehicles[0];Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1});Run(w,210);
            Assert.IsTrue(v.Loop);Assert.GreaterOrEqual(w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Ore),72);
            Do(w,new WorldCommand{Kind=CommandKind.Return});Run(w,30);Assert.IsFalse(v.Loop);Assert.Greater(v.Cargo.Count,0);Assert.AreEqual(VehiclePhase.Unloading,v.Phase);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Upgrade,Node=1}).Success);Run(w,10);
            Assert.IsEmpty(v.Cargo);Assert.AreEqual(VehiclePhase.Idle,v.Phase);var created=w.State.Created;Run(w,10);Assert.AreEqual(created,w.State.Created);
        }
        [Test] public void Hardware_AutomaticPickupLoadsMultipleTripsAndFinishes()
        {
            var w=New();Buy(w,HardwareCatalog.Carton,55,2);Do(w,new WorldCommand{Kind=CommandKind.PickupPurchase});Run(w,90);
            Assert.IsEmpty(w.State.Hardware.Deliveries);var v=w.State.Vehicles[0];Assert.IsEmpty(v.Cargo);Assert.IsFalse(v.Loop);Assert.IsFalse(v.PickupTask);Assert.AreEqual(VehiclePhase.Idle,v.Phase);
            Assert.AreEqual(55,w.State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(w.State.Routes.SelectMany(r=>r.Cargo)).Count(g=>g.Material==HardwareCatalog.Carton&&!g.Gift));
        }
        [TestCase(false)] [TestCase(true)] public void Hardware_AutomaticTasksContinueExactlyAfterSave(bool pickup)
        {
            var a=New();if(pickup){Buy(a,HardwareCatalog.Carton,55,2);Do(a,new WorldCommand{Kind=CommandKind.PickupPurchase});}else Do(a,new WorldCommand{Kind=CommandKind.Collect,Resource=1});
            Run(a,5);var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));Run(a,90);Run(b,90);Assert.AreEqual(Save(a),Save(b));
        }
        [Test] public void Hardware_OldManualSaveResumesWithAutomaticCargoRecovery()
        {
            var a=New();Do(a,new WorldCommand{Kind=CommandKind.Collect,Resource=1});Run(a,20);var v=a.State.Vehicles[0];Assert.IsNotEmpty(v.Cargo);var ids=v.Cargo.Select(g=>g.Id).ToArray();v.Manual=true;
            var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));Assert.IsFalse(b.State.Vehicles[0].Manual);Run(b,30);Assert.IsTrue(ids.All(id=>b.FinishedStock(1).Any(g=>g.Id==id)));Assert.AreEqual(VehiclePhase.Idle,b.State.Vehicles[0].Phase);
        }
        [Test] public void Hardware_GuideFollowsRealProgressAndFirstProductSurvivesDeliveryAndSave()
        {
            var w=New();Assert.AreEqual("order",HardwareGuide.Evaluate(w).Key);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});var hint=HardwareGuide.Evaluate(w);Assert.AreEqual("supply",hint.Key);Assert.AreEqual(6,hint.Quantity);
            Buy(w,HardwareCatalog.Heat,6);Assert.AreEqual("arrival",HardwareGuide.Evaluate(w).Key);Run(w,850);Assert.Greater(w.State.Hardware.FirstProductTick,0);Assert.AreEqual("reserve",HardwareGuide.Evaluate(w).Key);
            Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Assert.AreEqual("deliver",HardwareGuide.Evaluate(w).Key);Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});
            var b=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Assert.AreEqual(w.State.Hardware.FirstProductTick,b.State.Hardware.FirstProductTick);Assert.AreEqual(2,b.State.Hardware.Orders.First(o=>o.Id==HardwareGuide.Evaluate(b).Order).Template);
        }
        [Test] public void Hardware_GuideExplainsSelfMadeHeatAndMiningInputs()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24});
            var hint=HardwareGuide.Evaluate(w);Assert.AreEqual(HardwareCatalog.Aluminum,hint.Material);Assert.AreEqual(12,hint.Quantity);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24}).Success);Assert.AreEqual("mining",HardwareGuide.Evaluate(w).Key);
        }
        [Test] public void Hardware_WarehouseCapacityLeavesArrivalInTransit()
        {
            var w=New();w.Settings.WarehouseCapacity=1;Buy(w,HardwareCatalog.Heat,6);Run(w,100);Assert.AreEqual(6,w.State.Hardware.Deliveries.Single().Cargo.Count);w.AssertConserved();
        }
        [Test] public void Hardware_MakeHeatIsViableWithoutBuyingHeat()
        {
            var w=New();Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24}).Success);
            Buy(w,HardwareCatalog.Aluminum,12);Run(w,850);Assert.AreEqual(8,w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Standard));
        }
        [Test] public void Hardware_SettlementMatchesAuditableCostAndNeverReprices()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});
            var s=w.State.Hardware.Settlements.Single();Assert.AreEqual(384000,s.Income);Assert.AreEqual(36000,s.MaterialCost);Assert.AreEqual(5200,s.ProcessingCost);Assert.AreEqual(1200,s.FreightCost);Assert.AreEqual(341600,s.Margin);
            var old=JsonUtility.ToJson(s);w.Settings.Hardware.Materials.First(m=>m.Id==HardwareCatalog.Heat).Price=99999;Assert.AreEqual(old,JsonUtility.ToJson(s));
        }
        [Test] public void Hardware_AutomaticPickupRejectsRetaskingLoadedCargoAndUnloadsOnce()
        {
            var w=New();Buy(w,HardwareCatalog.Heat,6,2);Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.PickupPurchase}).Success);
            while(w.State.Vehicles[0].Cargo.Count==0&&w.State.Tick<3600)w.Tick();Assert.AreEqual(6,w.State.Vehicles[0].Cargo.Count);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1}).Success);Assert.AreEqual(6,w.State.Vehicles[0].Cargo.Count);
            w=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Run(w,20);Assert.IsEmpty(w.State.Vehicles[0].Cargo);Assert.IsEmpty(w.State.Hardware.Deliveries);
            Assert.AreEqual(6,w.State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(w.State.Routes.SelectMany(r=>r.Cargo)).Count(g=>g.Material==HardwareCatalog.Heat&&!g.Gift));w.AssertConserved();
        }
        [Test] public void Hardware_ReservationSurvivesWarehouseMoveAndBlocksCrossFactoryTransfer()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});w.Factory(2).Owned=true;w.Factory(2).Discovered=true;
            Do(w,new WorldCommand{Kind=CommandKind.Transfer,Factory=1,TargetFactory=2,Material=HardwareCatalog.Standard});Run(w,30);Assert.AreEqual(0,w.State.Vehicles[0].Cargo.Count);Assert.AreEqual(8,w.ReservedCount(1));
            var result=Do(w,new WorldCommand{Kind=CommandKind.Move,Node=1,X=4,Z=20});Assert.IsTrue(result.Success,result.Message);Assert.AreEqual(8,w.ReservedCount(1));w.AssertConserved();
        }
        [Test] public void Hardware_ExhaustedSupplierAndCancelledRecoveryRemainUsable()
        {
            var w=New();Buy(w,HardwareCatalog.Carton,80,2);var cash=w.State.Money;Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Carton,Quantity=1}).Success);Assert.AreEqual(cash,w.State.Money);
            Run(w,120);Assert.AreEqual(80,w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Carton).Quantity);
            Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=3});Do(w,new WorldCommand{Kind=CommandKind.CancelOrder,Order=3});Assert.IsTrue(w.State.Hardware.Orders.Any(o=>o.Template==3&&o.Id!=3&&o.Phase==HardwareOrderPhase.Available));
        }
    }
}
