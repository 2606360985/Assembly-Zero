using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareFactoryView
    {
        private readonly List<Mesh> routeMeshes=new List<Mesh>();
        public bool TryGetConveyorCorner(out Vector3 point)
        {
            foreach(var active in new[]{true,false})foreach(var route in game.World.State.Routes)
            {
                if(route.Factory!=game.Factory||(route.Cargo.Count>0)!=active)continue;var f=game.World.Factory(route.Factory);
                for(var i=2;i<route.Points.Count-2;i++)
                {
                    var a=Local(f,route.Points[i-1]);var b=Local(f,route.Points[i]);var c=Local(f,route.Points[i+1]);
                    if(Mathf.Abs(a.y-b.y)<.001f&&Mathf.Abs(c.y-b.y)<.001f&&Mathf.Abs(Vector3.Dot((b-a).normalized,(c-b).normalized))<.001f){point=b;return true;}
                }
            }
            point=Vector3.zero;return false;
        }
        private void ClearRouteMeshes(){foreach(var mesh in routeMeshes)Destroy(mesh);routeMeshes.Clear();}
        private void BuildConveyorRoute(BusinessRoute route,Vector3[] path,Vector3[] raw)
        {
            var line=new GameObject("Conveyor "+route.Id).transform;line.SetParent(root,false);
            MeshRenderer Surface(string name,float left,float right,float bottom,float top,Color color)
            {
                var mesh=FactoryConveyorGeometry.CreateMesh(path,left,right,bottom,top,"Hardware "+route.Id+" "+name);routeMeshes.Add(mesh);
                var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(line,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=Paint(color);return renderer;
            }
            beds[route.Id]=Surface("Continuous belt bed",-.27f,.27f,-.16f,0,dark);
            Surface("Continuous left guard",-.31f,-.27f,-.03f,.08f,steel);
            Surface("Continuous right guard",.27f,.31f,-.03f,.08f,steel);
            for(var i=1;i<raw.Length;i++)
            {
                var a=raw[i-1];var b=raw[i];
                if(FactoryRouteGeometry.IsVerticalTransfer(a,b))
                {
                    var middle=(a+b)*.5f;var height=Mathf.Abs(b.y-a.y);
                    foreach(var x in new[]{-.34f,.34f})foreach(var z in new[]{-.34f,.34f})
                        Cube("Vertical transfer guide",middle+new Vector3(x,0,z),new Vector3(.05f,height+.35f,.05f),steel,line);
                    foreach(var end in new[]{a,b})Cube("Transfer safety bar",end+Vector3.forward*.34f,new Vector3(.72f,.06f,.05f),amber,line);
                    continue;
                }
                if(i%3!=0)continue;var point=(a+b)*.5f;var clear=true;
                foreach(var other in game.World.State.Routes)
                {
                    if(other.Factory!=route.Factory||other.Id==route.Id)continue;var f=game.World.Factory(other.Factory);
                    for(var j=1;j<other.Points.Count;j++)
                    {
                        var c=Local(f,other.Points[j-1]);var d=Local(f,other.Points[j]);
                        if(FactoryRouteGeometry.PlanarSeparation(point,point,c,d,out _,out var t)<.4f&&Mathf.Lerp(c.y,d.y,t)<point.y-.15f){clear=false;break;}
                    }
                    if(!clear)break;
                }
                if(clear)Cube("Conveyor support",new Vector3(point.x,(point.y-.16f)*.5f,point.z),new Vector3(.12f,Mathf.Max(.1f,point.y-.16f),.12f),steel,line);
            }
        }
    }
}
