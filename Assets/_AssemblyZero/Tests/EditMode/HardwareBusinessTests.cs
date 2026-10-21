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
        [Test] public void Hardware_QuietOrderRejectsStandardGoods()
        {
            var w=Ready();Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=1});Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=2});Do(w,new WorldCommand{Kind=CommandKind.ReserveOrder,Order=2});Assert.AreEqual(0,w.ReservedCount(2));Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DeliverOrder,Order=2}).Success);
        }
        [Test] public void Hardware_ManualControlExcludesAutomaticCommandsAndRescueKeepsCargo()
        {
            var w=New();Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.TakeControl}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1}).Success);
            Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=1});Run(w,2);var v=w.State.Vehicles[0];Assert.Greater(v.DriveZ,16);
            Do(w,new WorldCommand{Kind=CommandKind.RecoverVehicle});Assert.Greater(v.RescueTicks,0);Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Interact}).Success);Run(w,60);Assert.AreEqual(w.Factory(1).Dock,v.Position);w.AssertConserved();
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
        [Test] public void Hardware_ManualPickupAndHarvestSurviveSaveAndUnloadOnce()
        {
            var w=New();Buy(w,HardwareCatalog.Heat,6,2);Do(w,new WorldCommand{Kind=CommandKind.TakeControl});Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=1});
            while(w.State.Vehicles[0].DriveZ<35&&w.State.Tick<1200)w.Tick();Do(w,new WorldCommand{Kind=CommandKind.Drive});Run(w,2);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Interact}).Success);Assert.AreEqual(6,w.State.Vehicles[0].Cargo.Count);Do(w,new WorldCommand{Kind=CommandKind.Interact});Assert.AreEqual(6,w.State.Vehicles[0].Cargo.Count);
            w=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=-1});
            while(w.State.Vehicles[0].DriveZ>27.5&&w.State.Tick<2400)w.Tick();Do(w,new WorldCommand{Kind=CommandKind.Drive});Run(w,2);Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Interact}).Success);Run(w,4);Assert.Greater(w.State.Vehicles[0].Cargo.Count,6);
            Do(w,new WorldCommand{Kind=CommandKind.Drive,Throttle=-1});while(w.State.Vehicles[0].DriveZ>17.5&&w.State.Tick<3600)w.Tick();Do(w,new WorldCommand{Kind=CommandKind.Drive});Run(w,2);Do(w,new WorldCommand{Kind=CommandKind.Interact});Assert.AreEqual(0,w.State.Vehicles[0].Cargo.Count);w.AssertConserved();
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
