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
        private static void Build(WorldSimulation w,BuildingKind kind,int x,int z,string recipe=""){var r=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=kind,X=x,Z=z,Recipe=recipe});Assert.IsTrue(r.Success,kind+": "+r.Message);}
        private static void BuildChain(WorldSimulation w){foreach(var s in w.Settings.Hardware.Slots)Build(w,s.Kind,s.X,s.Z);}
        private static long RunUntil(WorldSimulation w,Func<bool> done,int maxSeconds){var start=w.State.Tick;while(!done()&&w.State.Tick-start<maxSeconds*60L)w.Tick();Assert.IsTrue(done(),"Condition not reached within "+maxSeconds+"s; "+string.Join(" | ",w.State.Nodes.Select(n=>n.Kind+":"+n.Status+" in"+n.Input.Count+" out"+n.Output.Count))+" ¥"+w.State.Money);w.AssertConserved();return w.State.Tick-start;}
        private static HardwareOrder Order(WorldSimulation w,int template)=>w.State.Hardware.Orders.Last(o=>o.Template==template);
        // Accept and complete the opening contract quickly, unlocking the board, market and rejects.
        private static WorldSimulation Opened()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            RunUntil(w,()=>w.State.Hardware.Settlements.Count==1,240);return w;
        }
        private static Goods[] All(WorldSimulation w)=>w.State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(w.State.Routes.SelectMany(r=>r.Cargo)).ToArray();
        private static Goods Inject(WorldSimulation w,int material,string spec)
        {
            var g=new Goods(w.State.NextItem++,1,material){SpecId=spec,SpecVersion=1};w.State.Created++;w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Add(g);return g;
        }

        [Test] public void Hardware_DuplicatedMachinesConnectAllInputsFromTheHubWarehouse()
        {
            foreach(var p in new[]{(12,24),(20,24),(4,26),(36,26)})
            {
                var w=New();BuildChain(w);var r=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.BoardAssembly,X=p.Item1,Z=p.Item2});
                Assert.IsTrue(r.Success,r.Message);Assert.AreEqual(3,w.State.Routes.Count(x=>x.To==r.Entity),"Board assembly at "+p);Assert.IsFalse(w.Unlinked(w.Node(r.Entity)));
            }
        }
        [Test] public void Hardware_PartiallyConnectedMachineDoesNotHoardInputs()
        {
            var w=New();var board=w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly);
            var memory=w.State.Routes.Single(r=>r.To==board.Id&&r.Material==HardwareCatalog.Memory);
            w.State.Routes.Remove(memory);var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));
            var node=restored.Node(board.Id);Assert.IsTrue(restored.Unlinked(node));Run(restored,30);
            Assert.AreEqual(NodeState.Disconnected,node.RunState);Assert.IsEmpty(node.Input,"Core and board must stay in the warehouse");
        }
        [Test] public void Hardware_ExteriorDestinationsAreReachableWithoutCrossingFactoryFootprints()
        {
            var w=New();Assert.AreEqual(new GridPoint(12,72),w.State.Resources[0].Point);
            foreach(var f in w.State.Factories)foreach(var end in new[]{w.State.Resources[0].Point,new GridPoint(w.Settings.Hardware.SupplierX,w.Settings.Hardware.SupplierZ)})
            {
                var route=GridRouting.HardwarePath(f.Dock,end,w.State.Factories);Assert.IsNotNull(route);Assert.AreEqual(f.Dock,route.First());Assert.AreEqual(end,route.Last());
                Assert.IsTrue(route.All(p=>!GridRouting.HardwareBlocked(p,w.State.Factories)));Assert.Greater(route.Count,30);
            }
        }
        [Test] public void Hardware_OpeningIsAnIncompleteLineWithOneContract()
        {
            var w=New();
            CollectionAssert.AreEqual(new[]{BuildingKind.Warehouse,BuildingKind.BoardAssembly,BuildingKind.Sales},w.State.Nodes.Select(n=>n.Kind).ToArray());
            Assert.AreEqual(300000,w.State.Money);Assert.AreEqual(10,w.State.Hardware.Reputation);
            Assert.AreEqual(1,w.State.Hardware.Orders.Count);Assert.AreEqual(3,w.State.Hardware.Orders[0].Quantity);
            Assert.AreEqual(12,w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Core));Assert.AreEqual(4,w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Heat));
            Run(w,120);var board=w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly);
            Assert.AreEqual(NodeState.Blocked,board.RunState,"Without downstream machines the board line must visibly back up");
        }
        [Test] public void Hardware_FirstCardWithinOneMinuteAndFirstIncomeWithinThree()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);
            var first=RunUntil(w,()=>w.State.Hardware.FirstProductTick>0,120);Assert.LessOrEqual(first,60*60,"First card "+first/60d+"s");
            var cash=w.State.Money;var paid=RunUntil(w,()=>w.State.Hardware.Settlements.Count==1,240)+first;Assert.LessOrEqual(paid,180*60,"First income "+paid/60d+"s");
            var s=w.State.Hardware.Settlements.Single();Assert.AreEqual(DeliveryGrade.Early,s.Grade);Assert.AreEqual(3*48000*115/100,s.Income);
            Assert.AreEqual(15,w.State.Hardware.Reputation);Assert.Greater(w.State.Money,cash);
            Assert.AreEqual(HardwareOrderPhase.Delivered,Order(w,1).Phase);Assert.AreEqual(HardwareOrderPhase.Available,Order(w,2).Phase);
        }
        [Test] public void Hardware_LateDeliveryPaysLessAndCostsReputation()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Run(w,301);BuildChain(w);
            RunUntil(w,()=>w.State.Hardware.Settlements.Count==1,150);var s=w.State.Hardware.Settlements.Single();
            Assert.AreEqual(DeliveryGrade.Late,s.Grade);Assert.AreEqual(3*48000*75/100,s.Income);Assert.AreEqual(-3*48000/4,s.Adjustment);Assert.AreEqual(4,w.State.Hardware.Reputation);
        }
        [Test] public void Hardware_OnTimeDeliveryPaysBasePrice()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});Run(w,181);BuildChain(w);
            RunUntil(w,()=>w.State.Hardware.Settlements.Count==1,119);var s=w.State.Hardware.Settlements.Single();
            Assert.AreEqual(DeliveryGrade.OnTime,s.Grade);Assert.AreEqual(3*48000,s.Income);Assert.AreEqual(13,w.State.Hardware.Reputation);
        }
        [Test] public void Hardware_ExpiryChargesPenaltyAndRequotesScriptedContract()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});var cash=w.State.Money;Run(w,451);
            Assert.AreEqual(HardwareOrderPhase.Expired,w.State.Hardware.Orders[0].Phase);
            Assert.AreEqual(3*48000/10,w.State.Hardware.Penalties);Assert.AreEqual(0,w.State.Hardware.Reputation);Assert.Less(w.State.Money,cash);
            var retry=w.State.Hardware.Orders.Single(o=>o.Template==1&&o.Phase==HardwareOrderPhase.Available);Assert.AreNotEqual(1,retry.Id);
        }
        [Test] public void Hardware_CancelChargesPenaltyButNeverOverdrawsCash()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.CancelOrder,Order=1}).Success);
            Assert.AreEqual(300000-3*48000*5/100,w.State.Money);Assert.AreEqual(6,w.State.Hardware.Reputation);
            var retry=w.State.Hardware.Orders.Single(o=>o.Phase==HardwareOrderPhase.Available);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=retry.Id});
            w.State.Money=1000;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.CancelOrder,Order=retry.Id}).Success);Assert.AreEqual(0,w.State.Money);w.AssertConserved();
        }
        [Test] public void Hardware_OfferBoardOpensWithRepairFallbackAndRotates()
        {
            var w=New();Run(w,10);Assert.IsFalse(w.Offers.Any(o=>o.Template==0),"Board stays closed during the opening contract");
            w=Opened();Run(w,1);var board=w.Offers.Where(o=>o.Template==0).ToArray();
            Assert.AreEqual(w.Settings.Hardware.BoardSize,board.Length);Assert.IsTrue(board.Any(o=>o.Client==WorldSimulation.RepairClient));
            Assert.IsTrue(board.All(o=>o.Quantity>0&&o.UnitPrice>0&&o.Seconds>0&&o.OfferExpires>w.State.Tick));
            Run(w,w.Settings.Hardware.OfferLifetimeSeconds+w.Settings.Hardware.OfferRefreshSeconds);
            var next=w.Offers.Where(o=>o.Template==0).ToArray();Assert.IsTrue(next.Any(o=>o.Client==WorldSimulation.RepairClient));Assert.IsFalse(next.Any(o=>board.Any(b=>b.Id==o.Id)),"Expired offers are replaced");
        }
        [Test] public void Hardware_ReputationAndActiveLimitGateAcceptance()
        {
            var w=Opened();Run(w,1);var offers=w.Offers.Where(o=>o.Template==0).ToArray();
            offers[0].MinReputation=99;Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=offers[0].Id}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=Order(w,2).Id}).Success);
            foreach(var o in offers.Skip(1))o.MinReputation=0;
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=offers[1].Id}).Success);Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=offers[2].Id}).Success);
            var r=Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=offers[3].Id});Assert.IsFalse(r.Success);StringAssert.Contains("上限",r.Message);
        }
        [Test] public void Hardware_AllocationServesEarliestDeadlineFirst()
        {
            var w=Opened();Run(w,1);var big=Order(w,2);var repair=w.Offers.First(o=>o.Client==WorldSimulation.RepairClient);
            Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=big.Id});Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=repair.Id});Assert.Less(repair.Deadline,big.Deadline);
            Buy(w,HardwareCatalog.Heat,10);RunUntil(w,()=>repair.Phase==HardwareOrderPhase.Delivered,240);
            Assert.AreNotEqual(HardwareOrderPhase.Delivered,big.Phase,"The urgent repair job must be shipped before the larger order");
        }
        [Test] public void Hardware_RejectsAppearOnlyAfterFirstContractAndAreDeterministic()
        {
            var w=New();Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});BuildChain(w);Run(w,90);
            var gpu0=w.State.Nodes.First(n=>n.Kind==BuildingKind.GPUAssembly);Assert.AreEqual(0,w.FlawPermille(gpu0,w.Settings.Recipe("gpu")));
            Assert.IsFalse(All(w).Any(g=>g.Material==HardwareCatalog.Defect||g.Flawed),"The opening contract never produces rejects");
            WorldSimulation Play(){var x=Opened();Buy(x,HardwareCatalog.Heat,9);Run(x,240);return x;}
            var a=Play();var b=Play();Assert.AreEqual(Save(a),Save(b));
            var gpu=a.State.Nodes.First(n=>n.Kind==BuildingKind.GPUAssembly);Assert.AreEqual(100,a.FlawPermille(gpu,a.Settings.Recipe("gpu")));
            Assert.IsTrue(All(a).Where(g=>g.Material==HardwareCatalog.Defect).All(g=>g.SpecId==HardwareCatalog.StandardSpec));
            gpu.Level=3;Assert.AreEqual(30,a.FlawPermille(gpu,a.Settings.Recipe("gpu")));
        }
        [Test] public void Hardware_ReworkKeepsSpecificationAndBudgetPackagingUsesRejects()
        {
            var w=Opened();var injected=new[]{Inject(w,HardwareCatalog.Defect,HardwareCatalog.StandardSpec).Id,Inject(w,HardwareCatalog.Defect,HardwareCatalog.QuietSpec).Id};
            Build(w,BuildingKind.Rework,20,24);Buy(w,HardwareCatalog.Memory,4,1);Run(w,60);
            Assert.IsFalse(All(w).Any(g=>injected.Contains(g.Id)),"Injected rejects must be consumed by rework");Assert.IsTrue(All(w).Any(g=>g.Material==HardwareCatalog.QuietRaw&&g.SpecId==HardwareCatalog.QuietSpec));
            var v=Opened();Inject(v,HardwareCatalog.Defect,HardwareCatalog.StandardSpec);Inject(v,HardwareCatalog.Defect,HardwareCatalog.StandardSpec);
            Build(v,BuildingKind.Packaging,20,24,"budget-pack");Buy(v,HardwareCatalog.Carton,4,1);Run(v,60);
            Assert.AreEqual(2,v.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Budget&&g.SpecId==HardwareCatalog.BudgetSpec),string.Join(" | ",v.State.Nodes.Select(n=>n.Kind+":"+n.RecipeId+":"+n.Status)));
            Assert.IsTrue(Do(v,new WorldCommand{Kind=CommandKind.SpotSell,Material=HardwareCatalog.Budget,Quantity=2}).Success);v.AssertConserved();
        }
        [Test] public void Hardware_MarketForecastsThenChangesPricesWithoutRepricingHistory()
        {
            var w=Opened();Run(w,1);var m=w.State.Hardware.Market;Assert.AreEqual(-1,m.Current);Assert.Greater(m.PhaseEnds,w.State.Tick);m.Next=0;
            Assert.IsNull(w.ForecastEvent);var history=JsonUtility.ToJson(w.State.Hardware.Settlements[0]);
            while(w.State.Tick<m.PhaseEnds-w.Settings.Hardware.ForecastSeconds*60)w.Tick();Assert.AreEqual("mining",w.ForecastEvent.Id);
            Run(w,w.Settings.Hardware.ForecastSeconds+1);Assert.AreEqual("mining",w.CurrentEvent.Id);
            Assert.AreEqual(27000,w.PurchasePrice(HardwareCatalog.Core));Assert.AreEqual(6000,w.PurchasePrice(HardwareCatalog.Memory));
            var cash=w.State.Money;Buy(w,HardwareCatalog.Core,1);Assert.AreEqual(cash-27000-w.Settings.Hardware.DeliveryFee,w.State.Money);
            Assert.AreEqual(history,JsonUtility.ToJson(w.State.Hardware.Settlements[0]));
            Assert.AreEqual(130,w.OrderPricePercent(HardwareCatalog.Standard));Assert.AreEqual(100,w.OrderPricePercent(HardwareCatalog.Quiet));
            Run(w,w.Settings.Hardware.EventSeconds);Assert.IsNull(w.CurrentEvent);Assert.AreNotEqual(0,m.Next);
        }
        [Test] public void Hardware_SecondBoardAssemblyRaisesThroughput()
        {
            long Time(bool extra)
            {
                var w=New();BuildChain(w);if(extra)Build(w,BuildingKind.BoardAssembly,12,24);Buy(w,HardwareCatalog.Heat,8);
                return RunUntil(w,()=>w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Standard)>=12,600);
            }
            var single=Time(false);var dual=Time(true);Assert.Less(dual,single*85/100,"single "+single/60d+"s, dual "+dual/60d+"s");
        }
        [Test] public void Hardware_BottleneckIsTheSaturatedMachineFeedingStarvedOnes()
        {
            var w=New();BuildChain(w);Run(w,40);
            var board=w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly);Assert.GreaterOrEqual(w.Utilization(board),85);
            Assert.AreEqual(board.Id,w.Bottleneck(1)?.Id);Assert.IsTrue(w.State.Routes.All(r=>w.RouteLoad(r)>=0&&w.RouteLoad(r)<=1));
        }
        [Test] public void Hardware_GuideWalksOpeningThenSecondContractShortage()
        {
            var w=New();Assert.AreEqual("order",HardwareGuide.Evaluate(w).Key);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1});
            var hint=HardwareGuide.Evaluate(w);Assert.AreEqual("build",hint.Key);Assert.AreEqual(BuildingKind.GPUAssembly,hint.Building);
            BuildChain(w);Assert.AreNotEqual("build",HardwareGuide.Evaluate(w).Key);Assert.AreNotEqual("supply",HardwareGuide.Evaluate(w).Key);
            RunUntil(w,()=>w.State.Hardware.Settlements.Count==1,240);
            hint=HardwareGuide.Evaluate(w);Assert.AreEqual("order",hint.Key);Assert.AreEqual(Order(w,2).Id,hint.Order);
            Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=hint.Order});hint=HardwareGuide.Evaluate(w);
            Assert.AreEqual("supply",hint.Key);Assert.AreEqual(HardwareCatalog.Heat,hint.Material);Assert.AreEqual(7,hint.Quantity);
            Build(w,BuildingKind.Manufacturer,20,24);hint=HardwareGuide.Evaluate(w);Assert.AreEqual(HardwareCatalog.Aluminum,hint.Material);Assert.AreEqual(14,hint.Quantity);
            Build(w,BuildingKind.Processor,12,24);Assert.AreEqual("mining",HardwareGuide.Evaluate(w).Key);
        }
        [Test] public void Hardware_SaveV3ContinuesExactlyAndOlderSavesAreRejected()
        {
            var a=Opened();Do(a,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=Order(a,2).Id});Buy(a,HardwareCatalog.Heat,7);Run(a,30);
            var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));Run(a,300);Run(b,300);Assert.AreEqual(Save(a),Save(b));
            var old=JsonUtility.FromJson<WorldState>(Save(New()));old.Version=2;Assert.Throws<ArgumentException>(()=>new WorldSimulation(HardwareCatalog.Create(),old));
            Assert.Throws<ArgumentException>(()=>new WorldSimulation(HardwareCatalog.Create(),new WorldSimulation(new EconomySettings()).State));
            Assert.Throws<ArgumentException>(()=>new WorldSimulation(new EconomySettings(),New().State));
        }
        [Test] public void Hardware_SeedChangesTheMarketButNotTheOpening()
        {
            var a=new WorldSimulation(HardwareCatalog.Create(),null,11);var b=new WorldSimulation(HardwareCatalog.Create(),null,99);
            Assert.AreEqual(a.State.Nodes.Count,b.State.Nodes.Count);Assert.AreNotEqual(a.State.Hardware.Rng,b.State.Hardware.Rng);
        }
        [Test] public void Hardware_PurchaseFailureIsAtomicAndFreightIsExact()
        {
            var w=New();var cash=w.State.Money;var stock=w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Core).Quantity;
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=80}).Success);Assert.AreEqual(cash,w.State.Money);Assert.AreEqual(stock,w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Core).Quantity);
            Buy(w,HardwareCatalog.Carton,7);var d=w.State.Hardware.Deliveries.Single();Assert.AreEqual(w.Settings.Hardware.DeliveryFee,d.Cargo.Sum(g=>g.FreightCost));w.AssertConserved();
        }
        [Test] public void Hardware_SettlementMatchesAuditableCost()
        {
            var w=Opened();var s=w.State.Hardware.Settlements.Single();
            Assert.AreEqual(0,s.MaterialCost,"Gifted inputs carry no book cost");Assert.AreEqual(3*(300+200+100+50),s.ProcessingCost);Assert.AreEqual(s.Income-s.ProcessingCost,s.Margin);
            var old=JsonUtility.ToJson(s);w.Settings.Hardware.Materials.First(m=>m.Id==HardwareCatalog.Heat).Price=99999;Assert.AreEqual(old,JsonUtility.ToJson(s));
        }
        [Test] public void Hardware_WarehouseCapacityLeavesArrivalInTransit()
        {
            var w=New();w.Settings.WarehouseCapacity=1;Buy(w,HardwareCatalog.Heat,6);Run(w,100);Assert.AreEqual(6,w.State.Hardware.Deliveries.Single().Cargo.Count);w.AssertConserved();
        }
        [Test] public void Hardware_ExhaustedSupplierRestocks()
        {
            var w=New();Buy(w,HardwareCatalog.Carton,80,2);var cash=w.State.Money;Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Carton,Quantity=1}).Success);Assert.AreEqual(cash,w.State.Money);
            Run(w,w.Settings.Hardware.RestockSeconds);Assert.AreEqual(80,w.State.Hardware.Stock.First(s=>s.Material==HardwareCatalog.Carton).Quantity);
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
            Assert.IsEmpty(v.Cargo);Assert.AreEqual(VehiclePhase.Idle,v.Phase);
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
        [Test] public void Hardware_ZeroCashRecoversThroughOreAndRepairJobs()
        {
            var w=Opened();Run(w,1);w.State.Money=0;
            Do(w,new WorldCommand{Kind=CommandKind.Collect,Resource=1});RunUntil(w,()=>w.FinishedStock(1).Count(g=>g.Material==HardwareCatalog.Ore)>=20,240);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.SpotSell,Material=HardwareCatalog.Ore,Quantity=20}).Success);Assert.AreEqual(2000,w.State.Money);
            var repair=w.Offers.First(o=>o.Client==WorldSimulation.RepairClient);Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=repair.Id}).Success);w.AssertConserved();
        }
    }
}
