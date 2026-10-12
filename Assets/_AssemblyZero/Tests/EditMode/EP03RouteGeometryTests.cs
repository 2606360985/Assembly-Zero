using System;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssemblyZero.Tests
{
    public sealed class EP03RouteGeometryTests
    {
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
                Assert.LessOrEqual(FactoryRouteGeometry.SlopeDegrees(route.Points[i-1],route.Points[i]),40.001f,route.Connection.Id+" segment "+i);
        }
    }
}
