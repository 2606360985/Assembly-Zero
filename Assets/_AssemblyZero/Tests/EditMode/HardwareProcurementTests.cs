using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareProcurementTests
    {
        private static WorldSimulation Empty()
        {
            var world=new WorldSimulation(HardwareCatalog.Create());
            foreach(var node in world.State.Nodes){node.Input.Clear();node.Output.Clear();node.Batch.Clear();}
            foreach(var route in world.State.Routes)route.Cargo.Clear();
            world.State.Hardware.Orders.Clear();return world;
        }

        private static HardwareOrder Order(WorldSimulation world,int quantity,int material=HardwareCatalog.Standard,int factory=1)
        {
            var order=new HardwareOrder{Id=world.State.Hardware.NextOrder++,Factory=factory,Material=material,
                Quantity=quantity,Spec=HardwareCatalog.Spec(material),Phase=HardwareOrderPhase.Active,Deadline=1000};
            world.State.Hardware.Orders.Add(order);return order;
        }

        private static Goods Good(WorldSimulation world,int material,string spec="",int reservation=0)
            =>new Goods(world.State.NextItem++,1,material){SpecId=spec,SpecVersion=1,ReservedOrder=reservation};

        private static void Stock(WorldSimulation world,int material,int quantity,string spec="",int reservation=0)
        {
            for(var i=0;i<quantity;i++)world.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Add(Good(world,material,spec,reservation));
        }

        private static ProcurementMaterial Row(WorldSimulation world,int material,int factory=1)
            =>HardwareProcurement.Calculate(world,factory).Materials.Single(m=>m.Material==material);

        private static CommandResult Buy(WorldSimulation world,int quantity,int method=0)
        {
            world.Enqueue(new WorldCommand{Kind=CommandKind.Purchase,Factory=1,Material=HardwareCatalog.Core,Quantity=quantity,Delivery=method});
            world.FlushCommands();return world.LastResult;
        }

        [Test] public void Overview_IsReadOnlyAndListsConfiguredPurchasesWithoutOrders()
        {
            var world=new WorldSimulation(HardwareCatalog.Create());var before=JsonUtility.ToJson(world.State);
            var snapshot=HardwareProcurement.Calculate(world,1);
            Assert.AreEqual(8,snapshot.Materials.Count);Assert.AreEqual(0,snapshot.ActiveOrders);
            Assert.IsTrue(snapshot.Materials.All(m=>m.Demand==0&&m.Shortage==0));
            Assert.AreEqual(12,snapshot.Materials.Single(m=>m.Material==HardwareCatalog.Core).Stock);
            Assert.AreEqual(before,JsonUtility.ToJson(world.State));
            world.Settings.Hardware.Materials.First(m=>m.Id==HardwareCatalog.Aluminum).Price=0;
            Assert.AreEqual(7,HardwareProcurement.Calculate(world,1).Materials.Count);
        }

        [Test] public void Overview_SharedMaterialsAreConsumedOnceAcrossStandardAndQuietOrders()
        {
            var world=Empty();Order(world,2);Order(world,3,HardwareCatalog.Quiet);Stock(world,HardwareCatalog.Core,3);
            var core=Row(world,HardwareCatalog.Core);Assert.AreEqual(5,core.Demand);Assert.AreEqual(2,core.Shortage);
            Assert.AreEqual(2,Row(world,HardwareCatalog.Heat).Shortage);Assert.AreEqual(3,Row(world,HardwareCatalog.QuietHeat).Shortage);
        }

        [Test] public void Overview_UsesCompatibleFinishedAndIntermediateGoodsBeforeExpandingRecipes()
        {
            var world=Empty();var standard=Order(world,4);Order(world,2,HardwareCatalog.Quiet);
            Stock(world,HardwareCatalog.Standard,1,HardwareCatalog.StandardSpec,standard.Id);
            Stock(world,HardwareCatalog.Tested,1,HardwareCatalog.StandardSpec);
            Stock(world,HardwareCatalog.RawGPU,1,HardwareCatalog.StandardSpec);
            Stock(world,HardwareCatalog.AssembledBoard,1);
            Stock(world,HardwareCatalog.Quiet,1,HardwareCatalog.QuietSpec);
            Stock(world,HardwareCatalog.QuietTested,1,HardwareCatalog.QuietSpec);
            Assert.AreEqual(0,Row(world,HardwareCatalog.Core).Shortage);
            Assert.AreEqual(1,Row(world,HardwareCatalog.Heat).Shortage);
            Assert.AreEqual(0,Row(world,HardwareCatalog.QuietHeat).Shortage);
            Assert.AreEqual(4,Row(world,HardwareCatalog.Carton).Shortage);
        }

        [Test] public void Overview_DoesNotUseWrongSpecificationsVersionsOrAnotherOrdersReservations()
        {
            var world=Empty();var order=Order(world,2);
            Stock(world,HardwareCatalog.Standard,1,HardwareCatalog.QuietSpec);
            Stock(world,HardwareCatalog.Standard,1,HardwareCatalog.StandardSpec,999);
            Stock(world,HardwareCatalog.Tested,1,HardwareCatalog.QuietSpec);
            var wrongVersion=Good(world,HardwareCatalog.Standard,order.Spec);wrongVersion.SpecVersion=2;
            world.State.Nodes[0].Output.Add(wrongVersion);
            Assert.AreEqual(2,Row(world,HardwareCatalog.Core).Shortage);
            Assert.AreEqual(2,Row(world,HardwareCatalog.Carton).Shortage);
        }

        [Test] public void Overview_AllocatesSharedBoardToEarliestDeadlineThenOrderId()
        {
            var world=Empty();var standard=Order(world,1);var quiet=Order(world,1,HardwareCatalog.Quiet);
            standard.Deadline=200;quiet.Deadline=100;
            // A single board plus quiet downstream stock must serve the quiet order first.
            Stock(world,HardwareCatalog.AssembledBoard,1);Stock(world,HardwareCatalog.QuietHeat,1);Stock(world,HardwareCatalog.Shell,1);
            var before=JsonUtility.ToJson(world.State);var snapshot=HardwareProcurement.Calculate(world,1);
            Assert.AreEqual(1,snapshot.Materials.Single(m=>m.Material==HardwareCatalog.Core).Shortage);
            Assert.AreEqual(before,JsonUtility.ToJson(world.State));
            // Reverse list storage without changing business priority; the result must stay deterministic.
            world.State.Hardware.Orders.Reverse();
            Assert.AreEqual(snapshot.EstimatedGoodsCost,HardwareProcurement.Calculate(world,1).EstimatedGoodsCost);
        }

        [Test] public void Overview_ProjectsBatchesWithoutAlsoCountingTheirCommittedInputs()
        {
            var world=Empty();Order(world,1);var board=world.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly);
            foreach(var material in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board})board.Batch.Add(Good(world,material));
            var snapshot=HardwareProcurement.Calculate(world,1);
            foreach(var material in new[]{HardwareCatalog.Core,HardwareCatalog.Memory,HardwareCatalog.Board})
            {var row=snapshot.Materials.Single(m=>m.Material==material);Assert.AreEqual(0,row.Stock);Assert.AreEqual(0,row.Demand);}
            Assert.AreEqual(1,Row(world,HardwareCatalog.Heat).Shortage);
            board.Batch.Clear();board.RecipeId="pack";
            board.Batch.Add(Good(world,HardwareCatalog.Tested,HardwareCatalog.StandardSpec));board.Batch.Add(Good(world,HardwareCatalog.Carton));
            Assert.IsTrue(HardwareProcurement.Calculate(world,1).Materials.All(m=>m.Demand==0));
        }

        [Test] public void Overview_CountsLocalCachesBeltsDeliveriesAndVehiclesInSeparateColumns()
        {
            var world=Empty();Order(world,8);Stock(world,HardwareCatalog.Core,1);
            world.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly).Input.Add(Good(world,HardwareCatalog.Core));
            world.State.Routes[0].Cargo.Add(Good(world,HardwareCatalog.Core));
            world.State.Vehicles[0].Cargo.Add(Good(world,HardwareCatalog.Core));
            var delivery=new PurchaseDelivery{Factory=1,Method=2};delivery.Cargo.Add(Good(world,HardwareCatalog.Core));world.State.Hardware.Deliveries.Add(delivery);
            var core=Row(world,HardwareCatalog.Core);Assert.AreEqual(3,core.Stock);Assert.AreEqual(2,core.InTransit);Assert.AreEqual(3,core.Shortage);
            Assert.AreEqual(2,core.InTransitUsed);
        }

        [Test] public void Overview_ExistingCoolersBeforeSelfProductionAndExistingOreOnly()
        {
            var world=Empty();Order(world,4);Order(world,2,HardwareCatalog.Quiet);
            world.State.Nodes.Add(new BusinessNode{Id=100,Factory=1,Kind=BuildingKind.Manufacturer,RecipeId="heat"});
            world.State.Nodes.Add(new BusinessNode{Id=101,Factory=1,Kind=BuildingKind.Manufacturer,RecipeId="quiet-heat"});
            world.State.Nodes.Add(new BusinessNode{Id=102,Factory=1,Kind=BuildingKind.Processor,RecipeId="aluminum"});
            Stock(world,HardwareCatalog.Heat,1);Stock(world,HardwareCatalog.Aluminum,2);Stock(world,HardwareCatalog.Ore,9);
            Assert.AreEqual(0,Row(world,HardwareCatalog.Heat).Shortage);Assert.AreEqual(3,Row(world,HardwareCatalog.Heat).ProductionDemand);
            Assert.AreEqual(2,Row(world,HardwareCatalog.QuietHeat).ProductionDemand);
            var aluminum=Row(world,HardwareCatalog.Aluminum);Assert.AreEqual(12,aluminum.Demand);Assert.AreEqual(4,aluminum.ProductionDemand);Assert.AreEqual(6,aluminum.Shortage);
            world.State.Nodes.Single(n=>n.Id==100).PendingRemove=true;
            Assert.AreEqual(3,Row(world,HardwareCatalog.Heat).Shortage);
        }

        [Test] public void Overview_ReportsUnpurchasableDefectsAndDoesNotInventThemFromProduction()
        {
            var world=Empty();Order(world,3,HardwareCatalog.Budget);Stock(world,HardwareCatalog.Defect,1,HardwareCatalog.QuietSpec);
            var snapshot=HardwareProcurement.Calculate(world,1);
            Assert.AreEqual(2,snapshot.NonPurchasableShortages[HardwareCatalog.Defect]);
            Assert.AreEqual(3,Row(world,HardwareCatalog.Carton).Shortage);Assert.AreEqual(0,Row(world,HardwareCatalog.Core).Demand);
        }

        [Test] public void Overview_ExistingRejectedBatchOnlyCoversBudgetDemand()
        {
            var world=Empty();Order(world,1);Order(world,1,HardwareCatalog.Budget);
            var tester=new BusinessNode{Id=100,Factory=1,RecipeId="test",Kind=BuildingKind.Tester};
            var raw=Good(world,HardwareCatalog.RawGPU,HardwareCatalog.StandardSpec);raw.Flawed=true;tester.Batch.Add(raw);world.State.Nodes.Add(tester);
            var snapshot=HardwareProcurement.Calculate(world,1);
            Assert.IsEmpty(snapshot.NonPurchasableShortages);Assert.AreEqual(1,Row(world,HardwareCatalog.Core).Shortage);
        }

        [Test] public void Overview_ExcludesOtherFactoriesAndClosedOrUnacceptedOrders()
        {
            var world=Empty();Order(world,2);Order(world,10,factory:2);Order(world,10).Phase=HardwareOrderPhase.Cancelled;Order(world,10).Phase=HardwareOrderPhase.Available;
            world.State.Nodes.Add(new BusinessNode{Id=100,Factory=2,Kind=BuildingKind.Warehouse});
            for(var i=0;i<10;i++)world.State.Nodes.Last().Output.Add(Good(world,HardwareCatalog.Core));
            var delivery=new PurchaseDelivery{Factory=2};delivery.Cargo.Add(Good(world,HardwareCatalog.Core));world.State.Hardware.Deliveries.Add(delivery);
            Assert.AreEqual(2,Row(world,HardwareCatalog.Core).Shortage);Assert.AreEqual(0,Row(world,HardwareCatalog.Core).Stock);Assert.AreEqual(0,Row(world,HardwareCatalog.Core).InTransit);
            Assert.AreEqual(0,Row(world,HardwareCatalog.Core,2).Shortage);
            Assert.AreEqual(1,HardwareProcurement.Calculate(world,1).ActiveOrders);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Overview_PurchaseAndPartialUnloadingNeverDoubleCount(int method)
        {
            var world=Empty();Order(world,3);Assert.IsTrue(Buy(world,2,method).Success);
            Assert.AreEqual(1,Row(world,HardwareCatalog.Core).Shortage);Assert.AreEqual(2,Row(world,HardwareCatalog.Core).InTransit);
            var delivery=world.State.Hardware.Deliveries.Single();var first=delivery.Cargo[0];delivery.Cargo.Remove(first);world.State.Nodes[0].Output.Add(first);
            var core=Row(world,HardwareCatalog.Core);Assert.AreEqual(1,core.Stock);Assert.AreEqual(1,core.InTransit);Assert.AreEqual(1,core.Shortage);
            var last=delivery.Cargo[0];delivery.Cargo.Clear();world.State.Vehicles[0].Cargo.Add(last);
            Assert.AreEqual(1,Row(world,HardwareCatalog.Core).InTransit);Assert.AreEqual(1,Row(world,HardwareCatalog.Core).Shortage);
        }

        [Test] public void Overview_PurchaseLimitsAndInsufficientFundsLeaveDemandUnchanged()
        {
            var world=Empty();Order(world,3);world.State.Hardware.Stock.Single(s=>s.Material==HardwareCatalog.Core).Quantity=2;
            Assert.IsFalse(Buy(world,3).Success);Assert.IsFalse(Buy(world,81).Success);Assert.IsFalse(Buy(world,0).Success);
            world.State.Money=0;Assert.IsFalse(Buy(world,1).Success);
            Assert.AreEqual(3,Row(world,HardwareCatalog.Core).Shortage);Assert.IsEmpty(world.State.Hardware.Deliveries);
        }
    }
}
