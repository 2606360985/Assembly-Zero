using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareIterationTests
    {
        private static WorldSimulation New(){var w=new WorldSimulation(HardwareCatalog.Create());w.State.Money=10000000;return w;}
        private static CommandResult Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static void Run(WorldSimulation w,int seconds){for(var i=0;i<seconds*60;i++)w.Tick();w.AssertConserved();}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        private static void Chain(WorldSimulation w){foreach(var s in w.Settings.Hardware.Slots)Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z}).Success);}
        private static Goods Put(WorldSimulation w,int material,int gen=1,int bin=0,int grade=0,string spec="")
        {
            var g=new Goods(w.State.NextItem++,1,material){Gen=gen,CoreBin=bin,Grade=grade,SpecId=spec,SpecVersion=1};
            w.State.Created++;w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Add(g);return g;
        }
        [Test] public void IndustryClock_DepreciatesExistingCardsWithoutPlayerResearchAndKeepsContractPrice()
        {
            var w=New();w.State.Hardware.ScriptedDone=2;var old=Put(w,HardwareCatalog.Standard,1,0,2,HardwareCatalog.StandardSpec);
            var quote=w.State.Hardware.Orders[0].UnitPrice;var value=w.SpotPrice(old);w.Settings.Hardware.GenerationSeconds=2;
            Run(w,4);Assert.AreEqual(2,w.State.Hardware.IndustryGen);Assert.AreEqual(1,w.State.Hardware.CoreGen);
            Assert.Less(w.SpotPrice(old),value);Assert.AreEqual(quote,w.State.Hardware.Orders[0].UnitPrice);
            var b=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Run(w,5);Run(b,5);Assert.AreEqual(Save(w),Save(b));
        }
        [Test] public void CorePurchase_QuotesAndCarriesChosenGenerationAndBin()
        {
            var w=New();w.State.Hardware.CoreGen=2;
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=2,Generation=1,CoreBin=1}).Success);
            var d=w.State.Hardware.Deliveries.Last();Assert.IsTrue(d.Cargo.All(g=>g.Gen==1&&g.CoreBin==1&&g.MaterialCost==22500));
            Assert.Greater(w.PurchasePrice(HardwareCatalog.Core,2,1),w.PurchasePrice(HardwareCatalog.Core,2,3));
            var before=Save(w);Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=2,Generation=3}).Success);
            Assert.AreEqual(1,w.State.Hardware.Deliveries.Count);w.AssertConserved();
        }
        [Test] public void Retooling_WaitsForBatchStopsProductionAndResumesExactlyAcrossSave()
        {
            var w=New();Chain(w);Run(w,8);var board=w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly);
            Assert.IsNotEmpty(board.Batch);w.State.Hardware.CoreGen=2;
            var geometry=string.Join(";",w.State.Routes.Select(r=>r.Id+":"+string.Join(",",r.Points.Select(p=>p.X+"/"+p.Z))));
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Retool,Node=board.Id}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Retool,Node=board.Id}).Success);
            Run(w,20);Assert.AreEqual(1,board.ProcessGen);Assert.Greater(board.RetoolRemaining,0);
            var b=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Run(w,45);Run(b,45);
            Assert.AreEqual(2,board.ProcessGen);Assert.AreEqual(Save(w),Save(b));
            Assert.AreEqual(geometry,string.Join(";",w.State.Routes.Select(r=>r.Id+":"+string.Join(",",r.Points.Select(p=>p.X+"/"+p.Z)))));
        }
        [Test] public void NewSku_RequiresMatchingBoardsAndRetooledAssemblyThenDelivers()
        {
            var w=New();Chain(w);Run(w,90);w.State.Hardware.CoreGen=2;
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="G2 A 游戏",CoreBin=1,Market=TargetMarket.Gaming,Fans=3,Length=2,Backplate=true}).Success);
            var sku=w.Sku("c1");Assert.AreEqual(2,sku.Gen);Assert.Greater(sku.BasePrice,48000);
            var assembly=w.State.Nodes.Single(n=>n.Kind==BuildingKind.GPUAssembly);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Recipe,Node=assembly.Id,Recipe=HardwareSku.RecipeId(sku.Id)}).Success);
            Run(w,20);StringAssert.Contains("工艺改造",assembly.Status);
            var bad=new Goods{Material=HardwareCatalog.AssembledBoard,Gen=1,CoreBin=1};Assert.IsFalse(w.AcceptsGoods(assembly,bad));
            bad.Gen=2;bad.CoreBin=3;Assert.IsFalse(w.AcceptsGoods(assembly,bad));
            foreach(var n in w.State.Nodes.Where(w.NeedsRetooling).ToArray())Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Retool,Node=n.Id}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=3,CoreBin=1}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=3}).Success);
            Run(w,200);var card=w.FinishedStock(1).FirstOrDefault(g=>g.Material==HardwareCatalog.Standard&&g.SpecId==sku.Id);
            Assert.IsNotNull(card,string.Join(" | ",w.State.Nodes.Select(n=>n.Kind+":"+n.Status+" in "+string.Join(",",n.Input.Select(g=>g.Material+"G"+g.Gen+"B"+g.CoreBin)))));Assert.AreEqual(2,card.Gen);Assert.AreEqual(1,card.CoreBin);Assert.Greater(card.Grade,0);
            var order=new HardwareOrder{Id=90,Spec=sku.Id,Material=HardwareCatalog.Standard,Version=1,MinGen=2,Quantity=1,UnitPrice=sku.BasePrice,Phase=HardwareOrderPhase.Available,Seconds=100};w.State.Hardware.Orders.Add(order);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=90}).Success);Run(w,1);Assert.AreEqual(HardwareOrderPhase.Delivered,order.Phase);
        }
        [Test] public void GradeC_PackagingDivertsOnlyCAndPreservesCostsAndGeneration()
        {
            var w=New();Chain(w);
            var pack=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Packaging,X=20,Z=24,Recipe="c-bin-pack"});Assert.IsTrue(pack.Success,pack.Message);
            var tester=w.State.Nodes.Single(n=>n.Kind==BuildingKind.Tester);tester.RecipeId="";
            var c=Put(w,HardwareCatalog.Tested,2,3,3,HardwareCatalog.StandardSpec);var a=Put(w,HardwareCatalog.Tested,2,1,1,HardwareCatalog.StandardSpec);
            w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Remove(c);tester.Output.Add(c);
            w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Remove(a);tester.Output.Add(a);
            Assert.IsFalse(w.AcceptsGoods(w.Node(pack.Entity),a));Run(w,120);
            var budget=w.FinishedStock(1).FirstOrDefault(g=>g.Material==HardwareCatalog.Budget);Assert.IsNotNull(budget);Assert.AreEqual(2,budget.Gen);Assert.AreEqual(3,budget.Grade);
            Assert.IsTrue(w.FinishedStock(1).Any(g=>g.Material==HardwareCatalog.Standard&&g.Grade==1));
        }
        [Test] public void Demand_DoesNotInventImpossibleAiOffersOrUseRetiredSkus()
        {
            var w=New();w.State.Hardware.Settlements.Add(new Settlement{Order=99});w.State.Hardware.Reputation=70;
            for(var i=0;i<40;i++){foreach(var o in w.Offers.Where(o=>o.Template==0))o.OfferExpires=w.State.Tick;w.State.Hardware.NextOfferRefresh=w.State.Tick;Run(w,1);Assert.IsFalse(w.Offers.Any(o=>o.Client==5),"No AI-capable SKU exists");}
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="矿场专用",Market=TargetMarket.Mining}).Success);
            var seen=0;for(var i=0;i<60;i++){foreach(var o in w.Offers.Where(o=>o.Template==0))o.OfferExpires=w.State.Tick;w.State.Hardware.NextOfferRefresh=w.State.Tick;Run(w,1);foreach(var o in w.Offers.Where(o=>o.Spec=="c1")){seen++;Assert.IsTrue(o.Client==4||o.Client==2);}}
            Assert.Greater(seen,0);
        }
        [Test] public void Procurement_DoesNotOffsetNewPremiumCoreDemandWithOldOrWrongBinStock()
        {
            var w=New();w.State.Hardware.CoreGen=2;Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="新 A",CoreBin=1});
            Put(w,HardwareCatalog.Core,2,3);Put(w,HardwareCatalog.AssembledBoard,1,1);
            w.State.Hardware.Orders.Add(new HardwareOrder{Id=90,Factory=1,Spec="c1",Material=HardwareCatalog.Standard,Version=1,MinGen=2,Quantity=2,Phase=HardwareOrderPhase.Active,HardDeadline=100000});
            Assert.AreEqual(2,HardwareProcurement.Calculate(w,1).Materials.Single(m=>m.Material==HardwareCatalog.Core).Shortage);
        }
        [Test] public void LateGenerations_StayPositiveAndBoundedWithoutAddingMaterialIds()
        {
            var w=New();w.State.Hardware.CoreGen=1000;w.State.Hardware.IndustryGen=1000;
            Assert.Greater(w.Research("gen-1001").Points,0);Assert.Greater(w.Research("gen-1001").Cost,0);
            var draft=w.Draft(new WorldCommand{Name="远未来",CoreBin=1,Memory=4});Assert.Greater(draft.BasePrice,0);
            Assert.Greater(HardwareSku.Perf(draft,1000),0);Assert.Greater(w.PurchasePrice(HardwareCatalog.Core),0);
            Assert.Less(HardwareSku.Assembly(draft).Output,64);
            w.State.Hardware.Skus.Add(draft);Assert.Greater(w.SpotPrice(new Goods{Material=HardwareCatalog.Standard,SpecId=draft.Id,Gen=1000,Grade=1}),0);
        }
        [Test] public void AnotherGeneration_DoesNotResetOldCardDepreciationAndReboundItsPrice()
        {
            var w=New();var h=w.State.Hardware;h.IndustryGen=2;h.IndustryLaunchTick=1;h.GenerationHistory.Add(new GenerationLaunch{Gen=2,Tick=1});w.State.Tick=60*3600;
            var before=w.GenerationValuePercent(1);h.IndustryGen=3;h.IndustryLaunchTick=w.State.Tick;h.GenerationHistory.Add(new GenerationLaunch{Gen=3,Tick=w.State.Tick});
            Assert.LessOrEqual(w.GenerationValuePercent(1),before);
        }
        [Test] public void Save_UpgradesPreviousP1P2ContentAndLegacyQuietGoodsWithoutLosingOwnership()
        {
            var w=New();w.State.Hardware.ContentVersion=3;w.State.Hardware.GenerationHistory=null;
            var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Assert.AreEqual(HardwareCatalog.ContentVersion,restored.State.Hardware.ContentVersion);
            Assert.IsNotNull(restored.State.Hardware.GenerationHistory);Assert.Greater(restored.GenerationValuePercent(1),0);
            var g=Put(w,47,1,0,0,HardwareCatalog.QuietSpec);w.State.Version=3;w.State.Hardware.ContentVersion=2;
            restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));
            var migrated=restored.FinishedStock(1).Single(x=>x.Id==g.Id);Assert.AreEqual(HardwareCatalog.Standard,migrated.Material);Assert.AreEqual(HardwareCatalog.QuietSpec,migrated.SpecId);Assert.AreEqual(2,migrated.Grade);restored.AssertConserved();
        }
    }
}
