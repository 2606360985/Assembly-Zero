using System.Linq;
using AssemblyZero.Domain.EP04;
using NUnit.Framework;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareNarrativeTests
    {
        private static CommandResult Do(WorldSimulation w,WorldCommand c){c.Factory=c.Factory==0?1:c.Factory;w.Enqueue(c);w.FlushCommands();return w.LastResult;}
        private static void Chain(WorldSimulation w){foreach(var s in w.Settings.Hardware.Slots){var r=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=s.Kind,X=s.X,Z=s.Z});Assert.IsTrue(r.Success,r.Message);}}
        private static string Save(WorldSimulation w){w.CaptureState();return JsonUtility.ToJson(w.State);}
        private static void InstallFour(WorldSimulation w)
        {
            var rack=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.ComputeRack,X=12,Z=24});Assert.IsTrue(rack.Success);
            var store=w.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse);
            for(var i=0;i<4;i++){store.Output.Add(new Goods(w.State.NextItem++,1,HardwareCatalog.Standard){SpecId=HardwareCatalog.StandardSpec,SpecVersion=1,Gen=1,Grade=2});w.State.Created++;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.InstallCompute,Node=rack.Entity}).Success);}
        }
        [TestCase(0,"林序")][TestCase(1,"沈知微")]
        public void FounderIdentity_IsSanitizedFixedAndHasNoEconomicBonus(int founder,string expected)
        {
            var w=new WorldSimulation(HardwareCatalog.Create());var cash=w.State.Money;
            w.ChooseFounder(founder," <> \n");Assert.AreEqual(expected,w.Narrative.FounderName);Assert.AreEqual(cash,w.State.Money);
            w.ChooseFounder(1-founder,"changed");Assert.AreEqual(founder,w.Narrative.Founder);Assert.AreEqual(expected,w.Narrative.FounderName);
            CollectionAssert.AreEqual(new[]{"intro"},w.Narrative.Pending);
        }
        [Test] public void Narrative_QueuesOnceAndRestoresDialogueAndChoicesWithNativeJson()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());w.ChooseFounder(1,"知微");w.Narrative.Active="contract1";w.Narrative.Pending.Clear();w.Narrative.Line=3;w.Narrative.Order=1;w.Narrative.QualityMode=2;
            w.QueueNarrative("contract1");Assert.IsEmpty(w.Narrative.Pending);
            var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));
            Assert.AreEqual("contract1",restored.Narrative.Active);Assert.AreEqual(3,restored.Narrative.Line);Assert.AreEqual(1,restored.Narrative.Order);Assert.AreEqual(2,restored.Narrative.QualityMode);
            restored.CompleteNarrative("contract1");restored.QueueNarrative("contract1");Assert.IsEmpty(restored.Narrative.Pending);
        }
        [Test] public void LegacyCampaign_ResumesWithoutReplayingHistoricalMilestones()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());w.State.Hardware.ScriptedDone=2;w.State.Hardware.FirstProductTick=1;w.State.Hardware.Story=null;
            var restored=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Assert.IsTrue(restored.Narrative.Legacy);
            restored.ChooseFounder(0,"林工");CollectionAssert.IsSubsetOf(new[]{"contract1","delivery1","contract2","first"},restored.Narrative.Completed);
            CollectionAssert.AreEqual(new[]{"recap","ai_invite"},restored.Narrative.Pending);
        }
        [TestCase(1)][TestCase(2)] public void FirstBatch_UsesRealInspectionStreakAndChosenEquipmentRequirement(int mode)
        {
            var w=new WorldSimulation(HardwareCatalog.Create());w.ChooseFounder(0,"林序");Chain(w);
            w.Narrative.QualityMode=mode;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);
            for(var i=0;i<200*60&&w.Narrative.QualityStreak<3;i++)w.Tick();
            Assert.AreEqual(3,w.Narrative.QualityStreak);
            if(mode==2)
            {
                Assert.IsFalse(w.NarrativeQualityReady(1));Assert.AreEqual(0,w.State.Hardware.ScriptedDone);
                var result=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Tester,X=20,Z=24});Assert.IsTrue(result.Success,result.Message);
            }
            Assert.IsTrue(w.NarrativeQualityReady(1));
            for(var i=0;i<90*60&&w.State.Hardware.ScriptedDone==0;i++)w.Tick();
            Assert.AreEqual(1,w.State.Hardware.ScriptedDone);Assert.IsNotEmpty(w.Narrative.LastDelivery);w.AssertConserved();
        }
        [Test] public void Validation_CountsOnlySuccessfulFourCardSettlementsAndDoesNotDoublePay()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());w.ChooseFounder(1,"沈知微");
            InstallFour(w);
            for(var i=0;i<600;i++)w.Tick();Assert.AreEqual(0,w.Narrative.ValidationCycles,"Historical service must not count before acceptance");
            w.StartNarrativeValidation(1);var revenue=w.State.Revenue;var income=w.Automation(1).Income;var fees=w.Automation(1).Fees;for(var i=0;i<1800;i++)w.Tick();
            Assert.AreEqual(3,w.Narrative.ValidationCycles);Assert.AreEqual(3600,w.State.Revenue-revenue,"Only three normal payouts; no narrative duplicate payment");
            Assert.AreEqual(3600,w.Automation(1).Income-income);Assert.AreEqual(600,w.Automation(1).Fees-fees);
            Assert.AreEqual(1,w.Narrative.Pending.Count(s=>s=="compute"));
            var saved=new WorldSimulation(w.Settings,JsonUtility.FromJson<WorldState>(Save(w)));Assert.AreEqual(3,saved.Narrative.ValidationCycles);saved.AssertConserved();
        }
        [Test] public void Validation_NoFundsOrWrongFactoryCannotCompleteObjective()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());w.ChooseFounder(0,"");InstallFour(w);w.StartNarrativeValidation(1);w.State.Money=0;
            for(var i=0;i<1800;i++)w.Tick();Assert.AreEqual(0,w.Narrative.ValidationCycles);Assert.IsFalse(w.Narrative.Pending.Contains("compute"));
            w.State.Money=10000;w.Narrative.Factory=2;for(var i=0;i<600;i++)w.Tick();Assert.AreEqual(0,w.Narrative.ValidationCycles);
            w.Narrative.AiDeferred=true;w.State.Hardware.ScriptedDone=2;w.ObserveNarrative();Assert.IsFalse(w.Narrative.Pending.Contains("ai_invite"));
        }
        [Test] public void Scenes_AllHaveUniqueKeysAndShortReadableLines()
        {
            Assert.AreEqual(FounderStory.Scenes.Length,FounderStory.Scenes.Select(s=>s.Id).Distinct().Count());
            foreach(var scene in FounderStory.Scenes){Assert.IsNotEmpty(scene.Art);Assert.GreaterOrEqual(scene.Lines.Length,3);foreach(var line in scene.Lines)Assert.LessOrEqual(line.Text.Length,100,scene.Id);}
        }
        [TestCase(0,1)][TestCase(1,2)] public void FounderCampaign_CompletesTwoChaptersWithRealStartingCapitalAndGoods(int founder,int quality)
        {
            var w=new WorldSimulation(HardwareCatalog.Create(),null,19);w.ChooseFounder(founder,"");w.CompleteNarrative("intro");Chain(w);
            if(quality==2)Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Tester,X=20,Z=24}).Success);
            w.Narrative.QualityMode=quality;Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);w.CompleteNarrative("contract1");
            var policy=w.Automation(1);policy.Purchase=true;policy.CashReserve=100000;int rack=0;
            for(var tick=0;tick<1200*60&&w.Narrative.ValidationCycles<3;tick++)
            {
                var second=w.State.Hardware.Orders.FirstOrDefault(o=>o.Template==2&&o.Phase==HardwareOrderPhase.Available);
                if(second!=null){Assert.IsTrue(Do(w,new WorldCommand{Kind=CommandKind.AcceptOrder,Order=second.Id}).Success);w.CompleteNarrative("contract2");}
                if(w.State.Hardware.ScriptedDone>=2&&!w.Narrative.ValidationAccepted){w.StartNarrativeValidation(1);w.CompleteNarrative("ai_invite");}
                if(w.Narrative.ValidationAccepted&&rack==0){var result=Do(w,new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.ComputeRack,X=12,Z=24});if(result.Success)rack=result.Entity;}
                if(rack!=0&&w.InstalledCards(1)<4&&w.FinishedStock(1).Any(g=>g.Material==HardwareCatalog.Standard&&g.ReservedOrder==0))Do(w,new WorldCommand{Kind=CommandKind.InstallCompute,Node=rack});
                w.Tick();
                foreach(var id in w.Narrative.Pending.Where(id=>id=="first"||id=="delivery1").ToArray())w.CompleteNarrative(id);
            }
            Assert.AreEqual(2,w.State.Hardware.ScriptedDone);Assert.AreEqual(3,w.Narrative.ValidationCycles);Assert.AreEqual(4,w.InstalledCards(1));
            w.CompleteNarrative("compute");Assert.Contains("horizon",w.Narrative.Pending);Assert.GreaterOrEqual(w.State.Money,0);w.AssertConserved();
        }
    }
}
