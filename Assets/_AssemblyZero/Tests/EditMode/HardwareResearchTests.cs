using System;
using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareResearchTests
    {
        private static WorldSimulation New(){var w=new WorldSimulation(HardwareCatalog.Create());w.State.Money=5000000;return w;}
        private static CommandResult Do(WorldSimulation w,WorldCommand c){w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static void Run(WorldSimulation w,int seconds){for(var i=0;i<seconds*60;i++)w.Tick();w.AssertConserved();}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        private static int Lab(WorldSimulation w,int x=20,int z=24){var r=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Lab,X=x,Z=z});Assert.IsTrue(r.Success,r.Message);return r.Entity;}
        private static void Start(WorldSimulation w,string id){var r=Do(w,new WorldCommand{Kind=CommandKind.StartResearch,Recipe=id});Assert.IsTrue(r.Success,r.Message);}
        private static void Finish(WorldSimulation w,int maxSeconds=3600){var start=w.State.Tick;while(w.State.Hardware.ResearchActive!=""&&w.State.Tick-start<maxSeconds*60L)w.Tick();Assert.AreEqual("",w.State.Hardware.ResearchActive);w.AssertConserved();}

        [Test] public void Lab_ChargesRunningCostsAndAccumulatesPointsWithoutBelts()
        {
            var w=New();w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly).RecipeId="";var lab=w.Node(Lab(w));Assert.IsFalse(w.Unlinked(lab));Assert.IsFalse(w.State.Routes.Any(r=>r.To==lab.Id||r.From==lab.Id));
            Run(w,20);Assert.AreEqual(NodeState.Idle,lab.RunState);
            var cash=w.State.Money;Start(w,"mem-2x");Assert.AreEqual(cash-200000,w.State.Money);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.StartResearch,Recipe="test-bin"}).Success,"One project at a time");
            cash=w.State.Money;Run(w,30);Assert.AreEqual(15,w.State.Hardware.ResearchProgress);Assert.AreEqual(cash-3*300,w.State.Money);
            Finish(w);Assert.IsTrue(w.ResearchCompleted("mem-2x"));Assert.IsTrue(w.MemoryUnlocked(2));Assert.IsFalse(w.MemoryUnlocked(4));
            Assert.AreEqual(30,w.LabPointsPerMinute());
        }
        [Test] public void Research_PrerequisitesAndUnlocksGateRecipesAndDesigns()
        {
            var w=New();Lab(w);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.StartResearch,Recipe="mem-4x"}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.StartResearch,Recipe="gen-3"}).Success,"Generations follow in order");
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=12,Z=24,Recipe="tower-heat"}).Success);
            Assert.IsFalse(w.RecipesFor(BuildingKind.Manufacturer).Any(r=>r.Id=="tower-heat"));
            Start(w,"tower-cooler");Finish(w);Assert.IsTrue(w.RecipesFor(BuildingKind.Manufacturer).Any(r=>r.Id=="tower-heat"));
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=12,Z=24,Recipe="tower-heat"}).Success);
            w.State.Money=0;Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.StartResearch,Recipe="mem-2x"}).Success);Assert.AreEqual("",w.State.Hardware.ResearchActive);
        }
        [Test] public void Generations_AreEndlessAndScaleCostsPricesAndCoreSupply()
        {
            var w=New();var g2=w.Research(WorldSimulation.GenProject(2));var g3=w.Research(WorldSimulation.GenProject(3));var g12=w.Research(WorldSimulation.GenProject(12));
            Assert.AreEqual(600,g2.Points);Assert.AreEqual(960,g3.Points);Assert.Greater(g12.Points,g3.Points);Assert.AreEqual("gen-11",g12.Requires);
            Assert.AreEqual(WorldSimulation.GenProject(2),w.ResearchOptions().Last().Id);
            Lab(w);Lab(w,12,24);Start(w,"gen-2");Finish(w);
            Assert.AreEqual(2,w.State.Hardware.CoreGen);Assert.AreEqual(WorldSimulation.GenProject(3),w.ResearchOptions().Last().Id);
            Assert.AreEqual(18000*125/100,w.PurchasePrice(HardwareCatalog.Core));
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=HardwareCatalog.Core,Quantity=2,Delivery=1}).Success);
            Assert.IsTrue(w.State.Hardware.Deliveries.Last().Cargo.All(g=>g.Gen==2));
            Assert.AreEqual(135,w.GenerationValuePercent(2));Assert.AreEqual(82,w.GenerationValuePercent(1));
            Run(w,600);Assert.Less(w.GenerationValuePercent(1),82);Assert.GreaterOrEqual(w.GenerationValuePercent(1),55);
            Run(w,6000);Assert.AreEqual(55,w.GenerationValuePercent(1),"Old cards bottom out at the floor");
        }
        [Test] public void Generations_CardsInheritTheCoreGenerationAndOrdersDemandIt()
        {
            var w=New();foreach(var s in w.Settings.Hardware.Slots)Do(w,new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z});
            w.State.Hardware.CoreGen=2;w.State.Hardware.GenLaunchTick=w.State.Tick;
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="G2"}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.GPUAssembly,X=20,Z=24,Recipe=HardwareSku.RecipeId("c1")}).Success);
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Retool,Node=w.State.Nodes.Single(n=>n.Kind==BuildingKind.BoardAssembly).Id}).Success);
            foreach(var g in w.FinishedStock(1).Where(g=>g.Material==HardwareCatalog.Core).Take(2))g.Gen=2;
            Run(w,240);var cards=w.FinishedStock(1).Where(g=>g.Material==HardwareCatalog.Standard).ToArray();
            Assert.IsTrue(cards.Any(g=>g.Gen==2)&&cards.Any(g=>g.Gen==1),"Mixed cores yield mixed-generation cards");
            var latest=new HardwareOrder{Material=HardwareCatalog.Standard,Spec="c1",MinGen=2};
            Assert.IsTrue(cards.Where(g=>g.Gen==2).All(g=>WorldSimulation.Matches(g,latest)));Assert.IsFalse(cards.Where(g=>g.Gen==1).Any(g=>WorldSimulation.Matches(g,latest)));
            var old=cards.First(g=>g.Gen==1);var fresh=cards.First(g=>g.Gen==2);old.Grade=fresh.Grade;Assert.Greater(w.SpotPrice(fresh),w.SpotPrice(old));
        }
        // Formal opening, no injected cash: two contracts, one L1 lab, then the second generation.
        [Test] public void Campaign_ReachesSecondGenerationFromTheRealOpening()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());
            bool Until(Func<bool> done,int seconds){var start=w.State.Tick;while(!done()&&w.State.Tick-start<seconds*60L)w.Tick();return done();}
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);
            foreach(var s in w.Settings.Hardware.Slots)Do(w,new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z});
            Assert.IsTrue(Until(()=>w.State.Hardware.Settlements.Count==1,240));
            var second=w.State.Hardware.Orders.Single(o=>o.Template==2);Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=second.Id});
            var hint=HardwareGuide.Evaluate(w);Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=hint.Material,Quantity=hint.Quantity}).Success);
            // Follow live shortages after rejects as a player would; a fixed one-off purchase may be insufficient.
            for(var i=0;i<80&&second.Phase!=HardwareOrderPhase.Delivered&&second.Phase!=HardwareOrderPhase.Expired;i++)
            {
                Run(w,5);hint=HardwareGuide.Evaluate(w);
                if(hint.Key=="supply")Do(w,new WorldCommand{Kind=CommandKind.Purchase,Material=hint.Material,Quantity=Math.Min(80,hint.Quantity)});
            }
            Assert.AreEqual(HardwareOrderPhase.Delivered,second.Phase);
            hint=HardwareGuide.Evaluate(w);Assert.AreEqual("research",hint.Key);Assert.AreEqual(BuildingKind.Lab,hint.Building);
            Lab(w);Assert.AreEqual("research",HardwareGuide.Evaluate(w).Key);
            var gen2=w.Research(WorldSimulation.GenProject(2));Assert.IsTrue(Until(()=>w.State.Money>=gen2.Cost+20000,1800),"Cash for generation 2: ¥"+w.State.Money/100m);
            Start(w,gen2.Id);Assert.IsTrue(Until(()=>w.State.Hardware.CoreGen==2,3600),"Generation 2 not reached");
            var minutes=w.State.Tick/3600;Assert.LessOrEqual(minutes,60,"Generation 2 reached at minute "+minutes);
            Console.WriteLine("Generation 2 at minute "+minutes+", cash ¥"+w.State.Money/100m);w.AssertConserved();
        }
        [Test] public void Research_StateSurvivesSaveAndContinuesExactly()
        {
            var a=New();Lab(a);Start(a,"test-bin");Run(a,25);
            var b=new WorldSimulation(a.Settings,JsonUtility.FromJson<WorldState>(Save(a)));Assert.AreEqual("test-bin",b.State.Hardware.ResearchActive);
            Run(a,120);Run(b,120);Assert.AreEqual(Save(a),Save(b));
        }
        [Test] public void Design_SlotsDerivePerformanceNoiseCostAndReview()
        {
            var w=New();var draft=w.Draft(new WorldCommand{Name="S1 Max",Memory=1,Cooler=HardwareCatalog.Heat});
            Assert.AreEqual(48000,draft.BasePrice);Assert.AreEqual(50,draft.Score);Assert.AreEqual(100,HardwareSku.Perf(draft,1));Assert.AreEqual(135,HardwareSku.Perf(draft,2));
            var big=new SkuDefinition{Memory=4,Cooler=HardwareCatalog.TowerHeat};Assert.AreEqual(136*106/100,HardwareSku.PerfFactor(big));Assert.AreEqual(34,HardwareSku.Noise(big.Cooler));
            var cheap=w.Draft(new WorldCommand{Name="x",Memory=1,Cooler=HardwareCatalog.Heat,Price=40000});Assert.Greater(cheap.Score,50);
            var pricey=w.Draft(new WorldCommand{Name="x",Memory=1,Cooler=HardwareCatalog.Heat,Price=60000});Assert.Less(pricey.Score,50);
            Assert.AreEqual(43,w.Sku(HardwareCatalog.QuietSpec).Score);
        }
        [Test] public void Design_CommandsValidateLaunchAndRetireSkus()
        {
            var w=New();
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="",Memory=1,Cooler=HardwareCatalog.Heat}).Success);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="大显存",Memory=2,Cooler=HardwareCatalog.Heat}).Success,"Locked memory");
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="塔",Memory=1,Cooler=HardwareCatalog.TowerHeat}).Success,"Locked cooler");
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="贵",Memory=1,Cooler=HardwareCatalog.Heat,Price=200000}).Success,"Price outside band");
            w.State.Hardware.ResearchDone.Add("mem-2x");
            var r=Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="星铸 S1 Pro",Memory=2,Cooler=HardwareCatalog.Heat,Shell=3});Assert.IsTrue(r.Success,r.Message);
            var sku=w.State.Hardware.Skus.Last();Assert.AreEqual("c1",sku.Id);Assert.AreEqual(3,w.RecipesFor(BuildingKind.GPUAssembly).Count());
            var recipe=w.Recipe(HardwareSku.RecipeId(sku.Id));Assert.AreEqual(1,recipe.Inputs.Single(i=>i.Material==HardwareCatalog.Memory).Quantity);
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="星铸 S1 Pro",Memory=1,Cooler=HardwareCatalog.Heat}).Success,"Duplicate name");
            Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.RetireSku,Spec=sku.Id}).Success);Assert.AreEqual(2,w.RecipesFor(BuildingKind.GPUAssembly).Count());
            Assert.IsNotNull(w.Recipe(HardwareSku.RecipeId(sku.Id)),"Retired SKUs stay resolvable for running batches");
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.GPUAssembly,X=20,Z=24,Recipe=HardwareSku.RecipeId(sku.Id)}).Success);
            Do(w,new WorldCommand{Kind=CommandKind.RetireSku,Spec=HardwareCatalog.QuietSpec});
            Assert.IsFalse(Do(w,new WorldCommand{Kind=CommandKind.RetireSku,Spec=HardwareCatalog.StandardSpec}).Success,"Keep at least one SKU");
            var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Assert.IsNotNull(restored.Recipe(HardwareSku.RecipeId(sku.Id)));
        }
        [Test] public void Design_ClientsChooseSkusMatchingTheirNeedsAndReviews()
        {
            var w=New();w.State.Hardware.ResearchDone.Add("mem-2x");w.State.Hardware.Reputation=60;
            Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="AI 静音",Memory=2,Cooler=HardwareCatalog.QuietHeat});
            Do(w,new WorldCommand{Kind=CommandKind.DesignSku,Name="超值",Memory=1,Cooler=HardwareCatalog.Heat,Price=36000});
            w.State.Hardware.Settlements.Add(new Settlement{Order=99});
            var seen=new System.Collections.Generic.List<HardwareOrder>();
            for(var i=0;i<40;i++){w.State.Hardware.NextOfferRefresh=w.State.Tick;foreach(var o in w.Offers.Where(o=>o.Template==0))o.OfferExpires=w.State.Tick;Run(w,1);seen.AddRange(w.Offers.Where(o=>o.Template==0&&seen.All(s=>s.Id!=o.Id)));}
            var ai=seen.Where(o=>o.Client==5).ToArray();Assert.IsNotEmpty(ai);Assert.IsTrue(ai.All(o=>o.Spec=="c1"),"Only the quiet dual-memory SKU meets the AI lab");
            var quiet=seen.Where(o=>o.Client==3).ToArray();Assert.IsTrue(quiet.All(o=>w.IsQuiet(o.Spec)));
            var gaming=seen.Where(o=>o.Client==0).ToArray();Assert.IsTrue(gaming.All(o=>!w.IsQuiet(o.Spec)));Assert.IsTrue(gaming.Any(o=>o.Spec=="c2"),"The well-reviewed value SKU wins gaming orders");
            Assert.IsTrue(seen.All(o=>o.UnitPrice>0));
        }
    }
}
