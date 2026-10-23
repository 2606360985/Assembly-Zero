using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssemblyZero.Unity
{
    // Runtime-safe extraction of EP03's validated continuous-section sweep.
    public static class FactoryConveyorGeometry
    {
        public static Mesh CreateMesh(Vector3[] path,float left,float right,float bottom,float top,string id)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var start=0;
            for(var boundary=1;boundary<path.Length;boundary++)
            {
                if(FactoryRouteGeometry.IsVerticalTransfer(path[boundary-1],path[boundary])){start=boundary;continue;}
                if(boundary<path.Length-1&&!FactoryRouteGeometry.IsVerticalTransfer(path[boundary],path[boundary+1])&&FactoryRouteGeometry.CanShareConveyorSection(path,boundary))continue;
                // Unsafe joints get separate full-width end sections, like the original
                // straight modules. Never average opposing directions into a zero frame.
                var section=FactoryRouteGeometry.TrimDockedSection(path.Skip(start).Take(boundary-start+1).ToArray(),
                    start>0&&FactoryRouteGeometry.IsVerticalTransfer(path[start-1],path[start])?FactoryRouteGeometry.ElevatorDockClearance:0,
                    boundary<path.Length-1&&FactoryRouteGeometry.IsVerticalTransfer(path[boundary],path[boundary+1])?FactoryRouteGeometry.ElevatorDockClearance:0);
                if(section.Length<2){start=boundary;continue;}
                var offset=vertices.Count;
                for(var i=0;i<section.Length;i++)
                {
                    var before=(section[i]-section[Mathf.Max(0,i-1)]).normalized;
                    var after=(section[Mathf.Min(section.Length-1,i+1)]-section[i]).normalized;
                    if(i==0)before=after;if(i==section.Length-1)after=before;
                    var tangent=(before+after).normalized;
                    var side=Vector3.Cross(Vector3.up,tangent).normalized;
                    var up=Vector3.Cross(tangent,side).normalized;
                    var sideScale=1/Mathf.Max(.5f,Vector3.Dot(side,Vector3.Cross(Vector3.up,after).normalized));
                    var upScale=1/Mathf.Max(.5f,Vector3.Dot(up,Vector3.Cross(after,side).normalized));
                    vertices.Add(section[i]+side*(left*sideScale)+up*(bottom*upScale));
                    vertices.Add(section[i]+side*(left*sideScale)+up*(top*upScale));
                    vertices.Add(section[i]+side*(right*sideScale)+up*(top*upScale));
                    vertices.Add(section[i]+side*(right*sideScale)+up*(bottom*upScale));
                    if(i==0)continue;
                    var previous=vertices.Count-8;var current=vertices.Count-4;
                    for(var j=0;j<4;j++)
                    {
                        var a=previous+j;var b=previous+(j+1)%4;var c=current+(j+1)%4;var d=current+j;
                        triangles.AddRange(new[]{a,b,c,a,c,d});
                    }
                }
                triangles.AddRange(new[]{offset,offset+2,offset+1,offset,offset+3,offset+2});
                var end=vertices.Count-4;triangles.AddRange(new[]{end,end+1,end+2,end,end+2,end+3});
                start=boundary;
            }
            for(var i=0;i<triangles.Count;i+=3){var swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            var mesh=new Mesh{name=id};mesh.vertices=vertices.ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
