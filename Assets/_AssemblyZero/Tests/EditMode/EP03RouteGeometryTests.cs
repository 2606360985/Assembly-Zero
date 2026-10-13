using System;
using System.Linq;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class EP03RouteGeometryTests
    {
        [Test] public void ElevatorDocksTrimAcrossShortStraightSegments()
        {
            var path=FactoryRouteGeometry.TrimDockedSection(new[]{Vector3.zero,Vector3.right*.2f,Vector3.right},.34f,0);
            Assert.That(path[0].x,Is.EqualTo(.34f).Within(.0001f));Assert.AreEqual(Vector3.right,path[path.Length-1]);
            Assert.IsEmpty(FactoryRouteGeometry.TrimDockedSection(new[]{Vector3.zero,Vector3.right*.6f},.34f,.34f));
        }
        [TestCase("Full")] [TestCase("Minimal")]
        public void BakedLayoutsNoLongerTurnDuringAContinuousClimb(string scale)
        {
            var layout=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/Factory"+scale+".asset");
            Assert.IsTrue(System.Linq.Enumerable.Any(layout.Routes,r=>r.ElevatorSegments.Length>0));
            foreach(var route in layout.Routes)for(var i=1;i<route.Points.Length-1;i++)
            {
                var a=route.Points[i]-route.Points[i-1];var b=route.Points[i+1]-route.Points[i];
                if(Mathf.Abs(a.y)<.01f||Mathf.Abs(b.y)<.01f||a.y*b.y<0||route.ElevatorSegments.Contains(i)||route.ElevatorSegments.Contains(i+1))continue;
                Assert.Greater(Vector2.Dot(FactoryRouteGeometry.Flat(a).normalized,FactoryRouteGeometry.Flat(b).normalized),.99f,route.Connection.Id+" point "+i);
            }
        }
        [TestCase("Full","EP03_Demo")] [TestCase("Minimal","EP03_Minimal")]
        public void ElevatorLandingBeltsLeaveTheCargoShaftOpen(string scale,string scene)
        {
            var layout=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/Factory"+scale+".asset");
            foreach(var route in layout.Routes)
            {
                if(route.ElevatorSegments.Length==0)continue;
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_AssemblyZero/EP03/Meshes/"+scene+"-"+route.Connection.Id+"-bed.asset");
                var vertices=mesh.vertices;var indices=mesh.triangles;
                foreach(var lift in route.ElevatorSegments)foreach(var landing in new[]{route.Points[lift-1],route.Points[lift]})
                for(var i=0;i<indices.Length;i+=3)
                {
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    if(Mathf.Abs(a.y-landing.y)>.001f||Mathf.Abs(b.y-landing.y)>.001f||Mathf.Abs(c.y-landing.y)>.001f)continue;
                    var x=FactoryRouteGeometry.Flat(a-landing);var y=FactoryRouteGeometry.Flat(b-landing);var z=FactoryRouteGeometry.Flat(c-landing);
                    float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
                    if(Mathf.Abs(Cross(y-x,z-x))<.000001f)continue;
                    var inside=(Cross(x,y)>=0&&Cross(y,z)>=0&&Cross(z,x)>=0)||(Cross(x,y)<=0&&Cross(y,z)<=0&&Cross(z,x)<=0);
                    var separation=inside?0:Mathf.Min(FactoryRouteGeometry.PlanarSeparation(landing,landing,a,b,out _,out _),FactoryRouteGeometry.PlanarSeparation(landing,landing,b,c,out _,out _),FactoryRouteGeometry.PlanarSeparation(landing,landing,c,a,out _,out _));
                    Assert.GreaterOrEqual(separation,.30f,route.Connection.Id+" elevator "+lift+" landing belt intrudes into shaft");
                }
            }
        }
        [Test] public void ElevatorSamplesMoveVerticallyWithLevelCargoAndContinuousPorts()
        {
            var route=new FactoryRoutePlacement{Points=new[]{new Vector3(-2,0,0),Vector3.zero,new Vector3(0,2,0),new Vector3(2,2,0)},ElevatorSegments=new[]{2}};
            var startUnits=Mathf.CeilToInt(2*EP02Route.UnitsPerMeter);var endUnits=Mathf.FloorToInt(4*EP02Route.UnitsPerMeter);
            var before=route.Sample(startUnits-1,out _,out _);var entry=route.Sample(startUnits,out _,out _);
            Assert.Less(Vector3.Distance(before,entry),.002f);
            for(var distance=startUnits+1;distance<endUnits;distance+=32)
            {
                var position=route.Sample(distance,out var heading,out var inElevator);
                Assert.IsTrue(inElevator);Assert.That(position.x,Is.EqualTo(0).Within(.0001f));Assert.That(position.z,Is.EqualTo(0).Within(.0001f));
                Assert.That(heading.y,Is.EqualTo(0).Within(.0001f));Assert.That(position.y,Is.EqualTo(distance/EP02Route.UnitsPerMeter-2).Within(.0001f));
            }
            Assert.Less(Vector3.Distance(route.Sample(endUnits,out _),route.Sample(endUnits+1,out _)),.002f);
        }
        [Test] public void ElevatorClearanceChecksTheEntireShaftInsteadOfOnlyItsBottom()
        {
            Assert.IsTrue(FactoryRouteGeometry.TransferSegmentsConflict(new Vector3(0,.7f,0),new Vector3(0,3.1f,0),new Vector3(-1,1.9f,0),new Vector3(1,1.9f,0)));
            Assert.IsFalse(FactoryRouteGeometry.TransferSegmentsConflict(new Vector3(0,.7f,0),new Vector3(0,3.1f,0),new Vector3(-1,4,0),new Vector3(1,4,0)));
        }
        [Test] public void OpposingRampsUseIndependentFullWidthSections()
        {
            var path=new[]{new Vector3(42.300003f,3.1f,40.5f),new Vector3(44.100002f,1.9f,40.5f),new Vector3(42.300003f,.7f,40.5f)};
            Assert.IsFalse(FactoryRouteGeometry.CanShareConveyorSection(path,1));
        }
        [Test] public void ShortPortCannotBeConsumedByNinetyDegreeMiter()
        {
            var path=new[]{new Vector3(41.7f,.7f,40.5f),new Vector3(41.7f,.7f,44.4f),new Vector3(42.000004f,.7f,44.4f)};
            Assert.IsFalse(FactoryRouteGeometry.CanShareConveyorSection(path,1));
        }
        [Test] public void RegularArcKeepsContinuousSections()
        {
            var path=FactoryRouteGeometry.RoundedPath(new[]{Vector3.left*2,Vector3.zero,Vector3.forward*2});
            for(var i=1;i<path.Length-1;i++)Assert.IsTrue(FactoryRouteGeometry.CanShareConveyorSection(path,i),"section "+i);
        }
        [TestCase("EP03_Demo")] [TestCase("EP03_Minimal")]
        public void EveryBakedConveyorSectionRetainsUsableWidth(string scene)
        {
            var assets=AssetDatabase.FindAssets("t:Mesh",new[]{"Assets/_AssemblyZero/EP03/Meshes"});var count=0;
            foreach(var guid in assets)
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(!System.IO.Path.GetFileName(path).StartsWith(scene+"-",StringComparison.Ordinal))continue;
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var vertices=mesh.vertices;
                var width=path.EndsWith("-bed.asset",StringComparison.Ordinal)?.46f:.04f;
                for(var i=0;i<vertices.Length;i+=4)
                    Assert.GreaterOrEqual(Vector3.Distance(vertices[i+1],vertices[i+2]),width-.0001f,path+" section "+i/4);
                count++;
            }
            Assert.AreEqual(scene=="EP03_Demo"?249:114,count);
        }
        [TestCase(1)] [TestCase(-1)]
        public void RoundedCornerHasConstantRadiusAndPreservesPorts(int turn)
        {
            var points=new[]{Vector3.left*2,Vector3.zero,Vector3.forward*turn*2};
            var path=FactoryRouteGeometry.RoundedPath(points);
            Assert.AreEqual(points[0],path[0]);Assert.AreEqual(points[2],path[path.Length-1]);
            var center=new Vector3(-.48f,0,turn*.48f);
            for(var i=1;i<path.Length-1;i++)Assert.That(Vector3.Distance(path[i],center),Is.EqualTo(.48f).Within(.00001f));
            var route=new FactoryRoutePlacement{Points=points};
            Assert.AreEqual(4,route.Length);
            Assert.That(Vector3.Distance(route.Sample(2048,out _),Vector3.zero),Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(route.Sample(4096,out _),points[2]),Is.LessThan(.0001f));
        }
        [Test] public void RampsPreserveHeightAndShortNeighbouringCornersStayOrdered()
        {
            var ramp=new[]{Vector3.zero,new Vector3(1,1,0),new Vector3(2,1,0)};
            CollectionAssert.AreEqual(ramp,FactoryRouteGeometry.RoundedPath(ramp));
            var path=FactoryRouteGeometry.RoundedPath(new[]{Vector3.left,Vector3.zero,Vector3.forward*.6f,new Vector3(1,0,.6f)});
            for(var i=1;i<path.Length;i++)Assert.Greater(Vector3.Distance(path[i-1],path[i]),.00001f);
        }
        [Test] public void TightPortCornerUsesTransferDeckWithoutInvertingInnerGuard()
        {
            var points=new[]{Vector3.left*.3f,Vector3.zero,Vector3.forward};
            CollectionAssert.AreEqual(points,FactoryRouteGeometry.RoundedPath(points));
        }
        [TestCase(2)] [TestCase(4)]
        public void NeighboringLowerCrossingsPreventDescentBetweenLanes(int count)
        {
            for(var lane=0;lane<count-1;lane++)
            {
                var position=new Vector3(lane*1.2f+.6f,1.9f,0);
                var next=(lane+1)*1.2f;
                Assert.IsTrue(FactoryRouteGeometry.LowerCrossingAhead(position,Vector3.right,new Vector3(next,.7f,-2),new Vector3(next,.7f,2)));
            }
            Assert.IsFalse(FactoryRouteGeometry.LowerCrossingAhead(new Vector3((count-1)*1.2f+.6f,1.9f,0),Vector3.right,new Vector3(0,.7f,-2),new Vector3(0,.7f,2)));
        }
        [Test] public void HigherBridgeDoesNotPreventDescendingBelowIt()
        {
            Assert.IsFalse(FactoryRouteGeometry.LowerCrossingAhead(new Vector3(0,1.9f,0),Vector3.right,new Vector3(1.2f,3.1f,-2),new Vector3(1.2f,3.1f,2)));
        }
        [Test] public void LayoutRejectsSteeperThanFortyDegreeRamp()
        {
            var source=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/FactoryFull.asset");
            var layout=UnityEngine.Object.Instantiate(source);
            try
            {
                layout.Routes[0].Points[1]+=Vector3.up*10;
                StringAssert.Contains("40 degrees",Assert.Throws<InvalidOperationException>(()=>layout.Validate()).Message);
            }
            finally{UnityEngine.Object.DestroyImmediate(layout);}
        }
        [Test] public void ClearanceIncludesSlopingSegments()
        {
            var separation=FactoryRouteGeometry.PlanarSeparation(new Vector3(0,.7f,0),new Vector3(4,2.7f,0),new Vector3(2,.7f,-2),new Vector3(2,.7f,2),out var a,out var b);
            Assert.That(separation,Is.EqualTo(0).Within(.0001f));Assert.That(a,Is.EqualTo(.5f).Within(.0001f));Assert.That(b,Is.EqualTo(.5f).Within(.0001f));
        }
        [TestCase("Full")] [TestCase("Minimal")]
        public void BakedLayoutsKeepEverySlopeBelowFortyDegrees(string scale)
        {
            var layout=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>("Assets/_AssemblyZero/EP03/Configs/Factory"+scale+".asset");
            layout.Validate();
            foreach(var route in layout.Routes)for(var i=1;i<route.Points.Length;i++)
                if(System.Array.IndexOf(route.ElevatorSegments,i)>=0)Assert.IsTrue(FactoryRouteGeometry.IsVerticalTransfer(route.Points[i-1],route.Points[i]));
                else Assert.LessOrEqual(FactoryRouteGeometry.SlopeDegrees(route.Points[i-1],route.Points[i]),40.001f,route.Connection.Id+" segment "+i);
        }
    }
}
