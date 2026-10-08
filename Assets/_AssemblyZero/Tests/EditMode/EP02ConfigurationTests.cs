using System;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace AssemblyZero.Tests
{
    public sealed class EP02ConfigurationTests
    {
        private Scene scene;private EP02SimulationDriver driver;
        [SetUp]public void SetUp(){scene=EditorSceneManager.OpenScene("Assets/_AssemblyZero/Scenes/EP02_Demo.unity",OpenSceneMode.Additive);driver=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EP02SimulationDriver>()).Single();}
        [TearDown]public void TearDown(){if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);}
        [Test]public void OriginalSixPartsAndAssemblyRegistrationArePreserved(){var p=driver.Layout.Parts;var meshes=p.SelectMany(x=>x.SourcePrefab.GetComponentsInChildren<MeshFilter>()).Select(m=>m.sharedMesh).ToArray();Assert.AreEqual(67,meshes.Length);Assert.AreEqual(67,meshes.Distinct().Count());foreach(var x in p)Assert.AreEqual(p[0].AssemblyPose.Scale,x.AssemblyPose.Scale);}
        [Test]public void ThreeRoutesAreContinuousAndEveryCornerUsesArcShader()
        {
            var t=driver.Layout.BakeAndValidate();Assert.AreEqual(2,t.SupplyTopology.Lines.Length);
            foreach(var r in t.Supply.Concat(new[]{t.Product}))
            {
                var before=r.Sample(0,out var prev);for(var d=1;d<=r.Length;d+=16){var p=r.Sample(d,out var dir);Assert.Less(Vector3.Distance(p,before),.03f);Assert.Greater(Vector3.Dot(prev,dir),.98f);before=p;prev=dir;}
            }
            foreach(var s in driver.Layout.Surfaces){var piece=s.Product?driver.Layout.ProductBelts[s.Segment]:(s.Line==0?driver.Layout.SupplyA:driver.Layout.SupplyB)[s.Segment];Assert.AreEqual(piece.Shape==BeltPieceShape.Corner90?1:0,s.Renderer.sharedMaterial.GetFloat("_Corner"));}
        }
        [Test]public void SixAxesHaveFixedLinksAndReachBothSuppliesAndProduct(){var t=driver.Layout.BakeAndValidate();foreach(var a in driver.Layout.Inserters){Assert.AreEqual(6,a.Joints.Length);Assert.Less(a.Reach,4);var offsets=a.Joints.Select(j=>j.localPosition).ToArray();a.Solve(a.RestPosition,InserterPresenter.ToolRotation);CollectionAssert.AreEqual(offsets,a.Joints.Select(j=>j.localPosition).ToArray());}CollectionAssert.AreEqual(new[]{3,6,7,4,5,2},driver.Scenario.Create(driver.Layout,t).Stations.Select(s=>s.PartType));}
        [Test]public void MissingJointFailsBeforeSimulation(){driver.Layout.Inserters[0].Joints[2]=null;Assert.Throws<InvalidOperationException>(()=>driver.Layout.BakeAndValidate());}
        [Test] public void MotionKeepsJointRotationsContinuousAndWristSolvesOrientation()
        {
            var b = driver.Layout.BakeAndValidate();
            foreach (var a in driver.Layout.Inserters)
            {
                var p = b.Product.Sample(b.Product.CenterDistance(a.StationPiece), out var tangent);
                var socket = p + Quaternion.LookRotation(tangent) * driver.Layout.SocketOffset(a.SocketId);
                var pickup = b.Supply[1].Sample(a.PickupDistances[1], out _) + Vector3.up * .26f;
                a.ResetPose(); var previous = a.Joints.Select(j => j.localRotation).ToArray();
                foreach (var state in new[] { InserterState.Picking, InserterState.Carrying, InserterState.Placing, InserterState.Returning })
                    for (var i = 0; i <= 80; i++)
                    {
                        a.Solve(InserterPresenter.Motion(state, i / 80f, a.RestPosition, pickup, socket), InserterPresenter.ToolRotation);
                        Assert.Less(a.PositionError, .04f); Assert.Less(a.RotationError, 5);
                        for (var j = 0; j < 6; j++) { Assert.Less(Quaternion.Angle(previous[j], a.Joints[j].localRotation), 20, "I" + a.StableId + " J" + j); previous[j] = a.Joints[j].localRotation; }
                    }
                a.Solve(a.RestPosition, Quaternion.Euler(145, 30, 20), 120);
                Assert.Less(a.PositionError, .04f); Assert.Less(a.RotationError, 5);
            }
        }
        [Test]public void ContinuousProductionCompletesAndExitsWithDefaultSettings(){var t=driver.Layout.BakeAndValidate();var w=new EP02Simulation(driver.Scenario.Create(driver.Layout,t),t.SupplyTopology);EP02TestFactory.Run(w,10000);Assert.Greater(w.Statistics.CompletedRobots,1);Assert.Greater(w.Snapshot.ExitedProducts,0);}
    }
}
