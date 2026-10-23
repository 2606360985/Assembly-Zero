using System;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Meshes and cargo use the same EP03 centreline, without changing logical belt lengths.
    public sealed class HardwareConveyorPath
    {
        public readonly Vector3[] Points;
        public readonly float Length;
        private readonly float[] distances;
        private readonly Vector3[] headings;
        public HardwareConveyorPath(Vector3[] points)
        {
            Points=FactoryRouteGeometry.RoundedPath(points);distances=new float[Points.Length];headings=new Vector3[Points.Length];
            var previous=Vector3.forward;
            for(var i=0;i<Points.Length;i++)
            {
                if(i>0)distances[i]=distances[i-1]+Vector3.Distance(Points[i-1],Points[i]);
                var before=i>0?Points[i]-Points[i-1]:Vector3.zero;var after=i+1<Points.Length?Points[i+1]-Points[i]:Vector3.zero;
                before.y=after.y=0;var direction=before.normalized+after.normalized;
                if(direction.sqrMagnitude>.00001f)previous=direction.normalized;headings[i]=previous;
            }
            Length=distances.Length==0?0:distances[distances.Length-1];
        }
        public Vector3 Sample(float ratio,out Vector3 heading)
        {
            heading=Vector3.forward;if(Points.Length==0)return Vector3.zero;if(Points.Length==1)return Points[0];
            var distance=Mathf.Clamp01(ratio)*Length;var index=Array.BinarySearch(distances,distance);
            if(index<0)index=~index-1;index=Mathf.Clamp(index,0,Points.Length-2);
            var length=distances[index+1]-distances[index];var fraction=length>.000001f?(distance-distances[index])/length:0;
            heading=Vector3.Slerp(headings[index],headings[index+1],fraction).normalized;
            return Vector3.Lerp(Points[index],Points[index+1],fraction);
        }
    }
}
