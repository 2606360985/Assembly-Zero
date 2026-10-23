using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity;
using AssemblyZero.Unity.EP04;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class HardwareConveyorTests
    {
        [TestCase(1)] [TestCase(-1)]
        public void RoundedCargoFollowsTheSameArcAsTheBeltWithContinuousHeading(int turn)
        {
            var raw=new[]{Vector3.left*2,Vector3.zero,Vector3.forward*turn*2};var path=new HardwareConveyorPath(raw);
            CollectionAssert.AreEqual(FactoryRouteGeometry.RoundedPath(raw),path.Points);
            Assert.AreEqual(raw[0],path.Sample(0,out _));Assert.AreEqual(raw[2],path.Sample(1,out _));
            Assert.Greater(path.Sample(.5f,out _).magnitude,.1f);
            var previous=path.Sample(0,out var heading);var prior=heading;
            for(var i=1;i<=1000;i++)
            {
                var current=path.Sample(i/1000f,out heading);Assert.LessOrEqual(Vector3.Distance(previous,current),path.Length/1000+.0001f);
                Assert.Less(Vector3.Angle(prior,heading),2);Assert.That(heading.y,Is.EqualTo(0).Within(.0001f));previous=current;prior=heading;
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ContinuousSectionsNeverCollapseOnArcsTightPortsRampsOrLifts(int fixture)
        {
            var raw=fixture==0?new[]{Vector3.left*2,Vector3.zero,Vector3.forward*2}:
                fixture==1?new[]{Vector3.left*.3f,Vector3.zero,Vector3.forward}:
                fixture==2?new[]{new Vector3(42.3f,3.1f,40.5f),new Vector3(44.1f,1.9f,40.5f),new Vector3(42.3f,.7f,40.5f)}:
                new[]{Vector3.left*2,Vector3.zero,Vector3.up*1.2f,new Vector3(2,1.2f,0)};
            var path=new HardwareConveyorPath(raw);
            foreach(var side in new[]{0,1,2})
            {
                var left=side==0?-.27f:side==1?-.31f:.27f;var right=side==0?.27f:side==1?-.27f:.31f;
                var mesh=FactoryConveyorGeometry.CreateMesh(path.Points,left,right,-.16f,0,"Hardware geometry regression");
                try
                {
                    var v=mesh.vertices;Assert.IsNotEmpty(v);for(var i=0;i<v.Length;i+=4)
                    {
                        Assert.GreaterOrEqual(Vector3.Distance(v[i+1],v[i+2]),right-left-.0001f);
                        Assert.IsFalse(float.IsNaN(v[i].x)||float.IsNaN(v[i].y)||float.IsNaN(v[i].z));
                    }
                }
                finally{Object.DestroyImmediate(mesh);}
            }
        }
        [TestCase("Full","EP03_Demo")] [TestCase("Minimal","EP03_Minimal")]
        public void ExtractedSweepMatchesEveryAcceptedEp03BeltMesh(string scale,string scene)
        {
            var layout=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/Factory"+scale+".asset");
            foreach(var route in layout.Routes)
            {
                var old=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_AssemblyZero/EP03/Meshes/"+scene+"-"+route.Connection.Id+"-bed.asset");
                var mesh=FactoryConveyorGeometry.CreateMesh(FactoryRouteGeometry.RoundedPath(route.Points),-.23f,.23f,-.16f,0,"Regression");
                try
                {
                    Assert.AreEqual(old.vertexCount,mesh.vertexCount,route.Connection.Id);CollectionAssert.AreEqual(old.triangles,mesh.triangles,route.Connection.Id);
                    var a=old.vertices;var b=mesh.vertices;for(var i=0;i<a.Length;i++)Assert.Less(Vector3.Distance(a[i],b[i]),.00001f,route.Connection.Id+" vertex "+i);
                }
                finally{Object.DestroyImmediate(mesh);}
            }
        }
        [Test] public void EveryExpandedHardwareRouteUsesRoundedGeometryWithoutChangingSimulation()
        {
            var w=new WorldSimulation(HardwareCatalog.Create());
            foreach(var command in new[]{new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24},new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24}})
            {w.Enqueue(command);w.FlushCommands();Assert.IsTrue(w.LastResult.Success,w.LastResult.Message);}
            w.CaptureState();var before=JsonUtility.ToJson(w.State);Assert.Greater(w.State.Routes.Count,10);
            foreach(var route in w.State.Routes)
            {
                var raw=route.Points.Select(p=>new Vector3(p.X*1.2f,.6f+p.H*1.2f,p.Z*1.2f)).ToArray();var path=new HardwareConveyorPath(raw);
                Assert.AreEqual(raw[0],path.Sample(0,out _));Assert.AreEqual(raw.Last(),path.Sample(1,out _));
                var mesh=FactoryConveyorGeometry.CreateMesh(path.Points,-.27f,.27f,-.16f,0,"Hardware route "+route.Id);Assert.Greater(mesh.vertexCount,0);Object.DestroyImmediate(mesh);
            }
            w.CaptureState();Assert.AreEqual(before,JsonUtility.ToJson(w.State));w.AssertConserved();
        }
    }
}
