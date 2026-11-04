using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareSkuTests
    {
        private static WorldSimulation New()=>new WorldSimulation(HardwareCatalog.Create());
        private static CommandResult Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static void Run(WorldSimulation w,int seconds){for(var i=0;i<seconds*60;i++)w.Tick();w.AssertConserved();}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        private static void Build(WorldSimulation w,BuildingKind kind,int x,int z,string recipe=""){var r=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=kind,X=x,Z=z,Recipe=recipe});Assert.IsTrue(r.Success,kind+": "+r.Message);}
        private static void BuildChain(WorldSimulation w){foreach(var s in w.Settings.Hardware.Slots)Build(w,s.Kind,s.X,s.Z);}
        private static Goods Inject(WorldSimulation w,int material,string spec,int grade)
        {
            var g=new Goods(w.State.NextItem++,1,material){SpecId=spec,SpecVersion=1,Grade=grade};w.State.Created++;w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse).Output.Add(g);return g;
        }

        [Test] public void Binning_EveryInspectedCardHasAGradeAndOddsImproveWithTesterLevel()
        {
            var w=New();BuildChain(w);w.Enqueue(new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Heat,Quantity=8});w.FlushCommands();Run(w,300);
            var cards=w.FinishedStock(1).Where(g=>g.Material==HardwareCatalog.Standard).ToArray();Assert.Greater(cards.Length,6);
            Assert.IsTrue(cards.All(g=>g.Grade>=WorldSimulation.GradeA&&g.Grade<=WorldSimulation.GradeC));
            var tester=w.State.Nodes.First(n=>n.Kind==BuildingKind.Tester);var l1=w.GradeOdds(tester,1);Assert.AreEqual((15,60,25),l1);
            tester.Level=3;Assert.AreEqual((25,60,15),w.GradeOdds(tester,1));
            w.State.Hardware.ResearchDone.Add("test-bin");Assert.AreEqual(30,w.GradeOdds(tester,1).A);
        }
        [Test] public void Binning_NewGenerationRampsUpBeforeYieldingAsMuchA()
        {
            var w=New();var tester=new BusinessNode{Kind=BuildingKind.Tester,Level=1};
            w.State.Hardware.CoreGen=2;w.State.Hardware.GenLaunchTick=w.State.Tick;
            Assert.AreEqual(5,w.GradeOdds(tester,2).A);Assert.AreEqual(15,w.GradeOdds(tester,1).A,"Older generations are already mature");
            Run(w,WorldSimulation.MaturitySeconds/2);Assert.AreEqual(10,w.GradeOdds(tester,2).A);
            Run(w,WorldSimulation.MaturitySeconds/2);Assert.AreEqual(15,w.GradeOdds(tester,2).A);
        }
        [Test] public void Binning_GradeRequirementsFilterAllocationAndKeepACardsForDemandingOrders()
        {
            var w=New();var a=Inject(w,HardwareCatalog.Standard,HardwareCatalog.StandardSpec,WorldSimulation.GradeA);
            var c=Inject(w,HardwareCatalog.Standard,HardwareCatalog.StandardSpec,WorldSimulation.GradeC);
            var any=new HardwareOrder{Material=HardwareCatalog.Standard,Spec=HardwareCatalog.StandardSpec};var strict=new HardwareOrder{Material=HardwareCatalog.Standard,Spec=HardwareCatalog.StandardSpec,MinGrade=WorldSimulation.GradeA};
            Assert.IsTrue(WorldSimulation.Matches(c,any));Assert.IsFalse(WorldSimulation.Matches(c,strict));Assert.IsTrue(WorldSimulation.Matches(a,strict));
            var b=new HardwareOrder{Material=HardwareCatalog.Standard,Spec=HardwareCatalog.StandardSpec,MinGrade=WorldSimulation.GradeB};Assert.IsFalse(WorldSimulation.Matches(c,b));
            // A one-card order without a grade requirement must ship the C card and keep the A card.
            w.State.Hardware.Orders.Add(new HardwareOrder{Id=50,Factory=1,Material=HardwareCatalog.Standard,Spec=HardwareCatalog.StandardSpec,Quantity=1,UnitPrice=48000,Phase=HardwareOrderPhase.Active,Deadline=99999,BonusDeadline=99999,HardDeadline=99999,Name="test"});
            Run(w,1);Assert.AreEqual(HardwareOrderPhase.Delivered,w.State.Hardware.Orders.Single(o=>o.Id==50).Phase);
            Assert.IsTrue(w.FinishedStock(1).Contains(a));Assert.IsFalse(w.FinishedStock(1).Contains(c));
            Assert.Greater(w.SpotPrice(a),w.SpotPrice(c));Assert.AreEqual(w.SpotPrice(HardwareCatalog.Standard)*125/100,w.SpotPrice(a));
        }
        [Test] public void Binning_GradedOffersCarryRequirementPremiumAndSurviveSave()
        {
            var w=New();w.State.Hardware.Settlements.Add(new Settlement{Order=99});w.State.Hardware.Reputation=40;
            Run(w,2);var offers=w.Offers.Where(o=>o.Template==0).ToList();
            for(var i=0;i<30&&!offers.Any(o=>o.MinGrade>0);i++){w.State.Hardware.NextOfferRefresh=w.State.Tick;foreach(var o in offers)o.OfferExpires=w.State.Tick;Run(w,1);offers=w.Offers.Where(o=>o.Template==0).ToList();}
            var graded=offers.First(o=>o.MinGrade>0);StringAssert.Contains("档",graded.Name);
            var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));
            Assert.AreEqual(graded.MinGrade,restored.State.Hardware.Orders.Single(o=>o.Id==graded.Id).MinGrade);
        }
        [Test] public void Binning_ReworkReturnsAnUngradedCardThatIsInspectedAgain()
        {
            var w=New();w.State.Hardware.Settlements.Add(new Settlement{Order=99});
            Inject(w,HardwareCatalog.Defect,HardwareCatalog.QuietSpec,0);Build(w,BuildingKind.Rework,20,24);
            w.Enqueue(new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Memory,Quantity=2,Delivery=1});w.FlushCommands();Run(w,60);
            var raw=w.State.Nodes.SelectMany(n=>n.Input.Concat(n.Output).Concat(n.Batch)).Concat(w.State.Routes.SelectMany(r=>r.Cargo)).Single(g=>g.Material==HardwareCatalog.RawGPU&&g.SpecId==HardwareCatalog.QuietSpec);
            Assert.AreEqual(0,raw.Grade);
        }
    }
}
