using System;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Tests
{
    public sealed class EP03FactoryTests
    {
        private static EP02Simulation Assembly(BackendKind kind)
        {
            var c=EP02TestFactory.Config(true);c.ExternalSupply=true;c.RejectEnabled=false;c.ProductInterval=240;c.SupplySpeeds=Enumerable.Repeat(32,6).ToArray();c.MinimumSpacing=512;
            var lanes=Enumerable.Range(0,6).Select(i=>new LaneDefinition(new LaneId(i),new LineId(i),2048,ItemType.ServoCore)).ToArray();
            for(var i=0;i<6;i++)c.Inserters[i].Port=new BeltAccessPort(new LineId(i),1UL<<i,512,1536,1024,1UL<<c.Inserters[i].AllowedPart.Value);
            return new EP02Simulation(c,new BeltTopology(lanes),kind);
        }
        private static FactoryDefinition Definition(bool minimal=false)
        {
            var asset=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/Factory"+(minimal?"Minimal":"Full")+".asset");Assert.IsNotNull(asset,"Generate EP03 assets first.");
            return JsonUtility.FromJson<FactoryDefinition>(JsonUtility.ToJson(asset.CreateDefinition()));
        }
        private static FactorySimulation World(BackendKind kind=BackendKind.Gap,bool minimal=false)=>new FactorySimulation(Definition(minimal),Assembly(kind),kind);
        [Test] public void SupplyConversionAndProductReceiptsReconcileActualIdentities()
        {
            var world=World(minimal:true);while(world.TickNumber<60000&&world.Dispatched==0)world.Tick();world.AssertConserved();Assert.Greater(world.Dispatched,0);
            Assert.AreEqual(world.Ledger.Count,world.SupplyReceipts.Sum(r=>r.Items.Length)+world.Batches.Sum(b=>b.Outputs.Length));
            Assert.AreEqual(world.Dispatched+world.Finished.Count,world.ProductReceipts.Count);
            var raw=world.Ledger[world.SupplyReceipts.First().Items.First()];var original=raw.Type;raw.Type=16;Assert.Throws<InvalidOperationException>(()=>world.AssertConserved());raw.Type=original;world.AssertConserved();
            var product=world.ProductReceipts.First(p=>p.DispatchedTick>=0);product.DispatchedTick=-1;Assert.Throws<InvalidOperationException>(()=>world.AssertConserved());
        }
        [Test] public void LayoutRejectsCrossingBridgesWithoutClearance()
        {
            var source=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/FactoryFull.asset");var layout=UnityEngine.Object.Instantiate(source);
            try
            {
                layout.Validate();
                var pair=(from a in layout.Routes from b in layout.Routes
                    where a!=b&&b.ElevatorSegments.Length==0&&a.Connection.From!=b.Connection.From&&a.Connection.To!=b.Connection.To
                    from i in Enumerable.Range(1,a.Points.Length-1) from j in Enumerable.Range(1,b.Points.Length-1)
                    where Mathf.Abs(a.Points[i].y-.7f)<.01f&&Mathf.Abs(a.Points[i-1].y-.7f)<.01f&&b.Points[j].y>1.45f&&Mathf.Abs(b.Points[j].y-b.Points[j-1].y)<.01f
                    where FactoryRouteGeometry.PlanarSeparation(a.Points[i-1],a.Points[i],b.Points[j-1],b.Points[j],out _,out _)<.001f
                    select new[]{a,b}).First();
                for(var i=1;i<pair[1].Points.Length-1;i++){var point=pair[1].Points[i];point.y=.7f;pair[1].Points[i]=point;}
                StringAssert.Contains("Bridge clearance",Assert.Throws<InvalidOperationException>(()=>layout.Validate()).Message);
            }
            finally{UnityEngine.Object.DestroyImmediate(layout);}
        }
        [Test] public void ReferenceLayoutHasCardinalRoutesAndKeepsExpansionClear()
        {
            var layout=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/FactoryFull.asset");
            foreach(var route in layout.Routes)for(var i=1;i<route.Points.Length;i++)
            {
                var a=route.Points[i-1];var b=route.Points[i];Assert.IsTrue(Mathf.Abs(a.x-b.x)<.01f||Mathf.Abs(a.z-b.z)<.01f,route.Connection.Id+" diagonal segment");
                var middle=(a+b)*.5f;Assert.IsFalse(middle.x>55&&middle.z<44,route.Connection.Id+" crosses expansion/maintenance area");
            }
            var modules=layout.Modules.Where(m=>!string.IsNullOrEmpty(m.Module.RecipeId)).ToArray();Assert.AreEqual(30,modules.Length);
            Assert.Greater(modules.Single(m=>m.Module.RecipeId=="body").Grid.y,modules.Single(m=>m.Module.RecipeId=="head").Grid.y);
        }
        [TestCase(BackendKind.Naive)] [TestCase(BackendKind.Gap)]
        public void IdentityTransfersAreAtomicAndResetInvalidatesHandles(BackendKind kind)
        {
            IBeltSimulationBackend New()=>kind==BackendKind.Gap?(IBeltSimulationBackend)new GapTransportLineSimulation():new NaiveItemBeltSimulation();
            var x=New();var y=New();var config=new ScenarioDefinition{ExternalLogistics=true,MinimumSpacing=64};var topology=BeltTopology.CreateStraight(1,1024);x.Reset(config,topology);y.Reset(config,topology);
            var a=(IIdentityBeltAccess)x;var b=(IIdentityBeltAccess)y;var port=new BeltAccessPort(new LaneId(0),0,0);
            Assert.IsTrue(a.TryInsertExisting(port,new ItemId(750),new PartTypeId(11),out var source));
            Assert.IsTrue(b.TryInsertExisting(port,new ItemId(900),new PartTypeId(11),out var blocking));
            Assert.IsFalse(a.TryTransfer(source,b,port,out _));Assert.IsTrue(a.TryValidate(source,out _));Assert.IsTrue(b.TryValidate(blocking,out _));
            b.TryTake(blocking,out _);Assert.IsTrue(a.TryTransfer(source,b,port,out var arrived));Assert.AreEqual(750,arrived.ItemId.Value);Assert.IsFalse(a.TryValidate(source,out _));
            Assert.IsFalse(b.TryInsertExisting(new BeltAccessPort(new LaneId(0),256,256),new ItemId(750),new PartTypeId(11),out _));
            y.Reset(config,topology);Assert.IsFalse(b.TryValidate(arrived,out _));
            x.Reset(config,BeltTopology.CreateStraight(2,1024));Assert.IsTrue(a.TryInsertExisting(port,new ItemId(751),new PartTypeId(11),out source));
            Assert.IsTrue(a.TryTransfer(source,a,new BeltAccessPort(new LaneId(1),256,256),out var moved));Assert.AreEqual(751,moved.ItemId.Value);Assert.IsFalse(a.TryValidate(source,out _));Assert.IsTrue(a.TryValidate(moved,out _));
        }
        [TestCase(false)] [TestCase(true)]
        public void FullAndMinimalChainsDeliverAndEveryMachineWorks(bool minimal)
        {
            var w=World(minimal:minimal);for(var t=0;t<60000;t++){w.Tick();if(t%600==0)w.AssertConserved();if(w.Dispatched>=10&&w.Modules.Where(m=>m.Recipe!=null).All(m=>m.CompletedBatches>0))break;}
            w.AssertConserved();Assert.GreaterOrEqual(w.Dispatched,10);Assert.AreEqual(minimal?12:30,w.Modules.Count(m=>m.Recipe!=null));Assert.IsTrue(w.Modules.Where(m=>m.Recipe!=null).All(m=>m.CompletedBatches>0));
        }
        [Test] public void MultiInputIsCommittedOnceAndOutputBackpressureRecovers()
        {
            var d=Definition(true);var body=d.Modules.Single(m=>m.RecipeId=="body");d.Connections=d.Connections.Where(c=>c.To!=body.Id||c.Material!=16).ToArray();
            Assert.Throws<ArgumentException>(()=>d.Validate(),"A disconnected input must fail before play.");
            var w=World(minimal:true);w.SupplyEnabled=false;
            for(var t=0;t<3000;t++)w.Tick();
            w.AssertConserved();foreach(var batch in w.Batches)Assert.AreEqual(batch.Inputs.Length,batch.Inputs.Distinct().Count());
            var before=w.Batches.Count;w.SupplyEnabled=true;for(var t=0;t<6000;t++)w.Tick();w.AssertConserved();Assert.Greater(w.Batches.Count,before);
        }
        [Test] public void MissingSecondIngredientNeverPartiallyConsumesTheFirst()
        {
            var recipe=FactoryCatalog.Recipes().Single(r=>r.Id=="motor");
            var d=new FactoryDefinition { Recipes=new[]{recipe},Modules=new[]{
                new FactoryModuleDefinition{Id="rods",Material=12,Warehouse=true,InitialStock=3,Capacity=8},
                new FactoryModuleDefinition{Id="electronics",Material=10,Warehouse=true,Capacity=8,RefillQuantity=1},
                new FactoryModuleDefinition{Id="motor",RecipeId="motor"},new FactoryModuleDefinition{Id="output",Material=15,Capacity=1}},
                Connections=new[]{new FactoryConnection{Id="r",From="rods",To="motor",Material=12,Length=1024},new FactoryConnection{Id="e",From="electronics",To="motor",Material=10,Length=1024},new FactoryConnection{Id="o",From="motor",To="output",Material=15,Length=1024}}};
            var w=new FactorySimulation(d,Assembly(BackendKind.Gap));w.SupplyEnabled=false;
            for(var i=0;i<200;i++)w.Tick();var motor=w.Modules.Single(m=>m.Definition.Id=="motor");
            Assert.AreEqual(3,motor.Inputs[12].Count);Assert.IsNull(motor.Batch);Assert.AreEqual(0,w.Batches.Count);w.AssertConserved();
            w.SupplyEnabled=true;for(var i=0;i<700;i++)w.Tick();w.AssertConserved();Assert.AreEqual(1,motor.CompletedBatches);Assert.AreEqual(2,motor.Inputs[12].Count);
            Assert.AreEqual(2,w.Ledger.Values.Count(x=>x.Owner==FactoryOwner.Consumed));Assert.AreEqual(1,w.Batches.Count);
        }
        [Test] public void FinishedQueueBlocksAndResumesWithoutLosingProducts()
        {
            var d=Definition(true);d.FinishedCapacity=1;var w=new FactorySimulation(d,Assembly(BackendKind.Gap));w.DispatchEnabled=false;
            for(var t=0;t<18000;t++)w.Tick();w.AssertConserved();Assert.AreEqual(1,w.Finished.Count);Assert.AreEqual(0,w.Dispatched);Assert.IsTrue(w.Assembly.Snapshot.Products.Any(p=>p.State==ProductState.WaitingForDownstream));
            w.DispatchEnabled=true;for(var t=0;t<6000;t++)w.Tick();w.AssertConserved();Assert.Greater(w.Dispatched,1);
        }
        [Test] public void NaiveGapAndFreshResetAreDeterministic()
        {
            var a=World(BackendKind.Naive,true);var b=World(BackendKind.Gap,true);
            for(var i=0;i<10000;i++){if(i==3000){a.DispatchEnabled=b.DispatchEnabled=false;}if(i==5000){a.DispatchEnabled=b.DispatchEnabled=true;}a.Tick();b.Tick();if(i%100==0)Assert.AreEqual(a.CalculateStateHash(),b.CalculateStateHash(),"Tick "+i);}
            a.AssertConserved();b.AssertConserved();var fresh=World(BackendKind.Gap,true);var another=World(BackendKind.Gap,true);Assert.AreEqual(fresh.CalculateStateHash(),another.CalculateStateHash());
        }
        [Test] public void LayoutLoadsAndVisualReplacementPreservesInterfacesAndProduction()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_AssemblyZero/Scenes/EP03_Demo.unity",OpenSceneMode.Additive);
            try
            {
                var d=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EP03SimulationDriver>()).Single();d.Layout.Validate();var m=d.Modules.First(v=>v.InstanceId=="plate-0");m.Validate();var before=m.InterfaceSignature();
                var topology=d.AssemblyLayout.BakeAndValidate();var config=d.AssemblyScenario.Create(d.AssemblyLayout,topology);config.SourceEnabled=false;config.RejectEnabled=false;config.ExternalSupply=true;
                var a=new FactorySimulation(Definition(true),new EP02Simulation(config,topology.SupplyTopology));
                var original=m.Visual;var replacement=new GameObject("Replacement Art").transform;replacement.SetParent(m.transform,false);m.Visual=replacement;UnityEngine.Object.DestroyImmediate(original.gameObject);m.Validate();Assert.AreEqual(before,m.InterfaceSignature());
                topology=d.AssemblyLayout.BakeAndValidate();config=d.AssemblyScenario.Create(d.AssemblyLayout,topology);config.SourceEnabled=false;config.RejectEnabled=false;config.ExternalSupply=true;
                var b=new FactorySimulation(Definition(true),new EP02Simulation(config,topology.SupplyTopology));
                for(var i=0;i<20000&&a.Dispatched<1;i++){a.Tick();b.Tick();}Assert.GreaterOrEqual(a.Dispatched,1);Assert.AreEqual(a.CalculateStateHash(),b.CalculateStateHash());
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
