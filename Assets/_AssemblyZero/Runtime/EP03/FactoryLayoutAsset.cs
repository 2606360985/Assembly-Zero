using System;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity
{
    [Serializable] public sealed class FactoryPlacement
    {
        public FactoryModuleDefinition Module;
        public Vector2Int Grid, Footprint = new Vector2Int(4,4);
        public int QuarterTurns;
        public Vector3 PositionOffset;
        public GameObject Prefab;
        public Vector2Int OccupiedSize => QuarterTurns % 2 == 0 ? Footprint : new Vector2Int(Footprint.y,Footprint.x);
    }
    [Serializable] public sealed class FactoryRoutePlacement
    {
        public FactoryConnection Connection;
        public Vector3[] Points = Array.Empty<Vector3>();
        public int BridgeLevel;
        public float Length { get { var length=0f;for(var i=1;i<Points.Length;i++)length+=Vector3.Distance(Points[i-1],Points[i]);return length; } }
        public Vector3 Sample(int distance,out Vector3 tangent)
        {
            var remaining=Mathf.Clamp(distance / EP02Route.UnitsPerMeter,0,Length);tangent=Vector3.forward;
            for(var i=1;i<Points.Length;i++){var delta=Points[i]-Points[i-1];var length=delta.magnitude;if(length<.001f)continue;tangent=delta/length;if(remaining<=length)return Points[i-1]+tangent*remaining;remaining-=length;}
            return Points.Last();
        }
    }
    [Serializable] public sealed class FactoryRegion { public string Name; public RectInt Bounds; public bool Reserved; }
    [CreateAssetMenu(menuName="Assembly Zero/EP03 Factory Layout")]
    public sealed class FactoryLayoutAsset : ScriptableObject
    {
        public float GridMeters=1.2f;
        public Vector2Int FactorySize=new Vector2Int(92,64);
        public Vector3 AssemblyOrigin=new Vector3(73.2f,0,56.4f);
        public FactoryRecipe[] Recipes=Array.Empty<FactoryRecipe>();
        public FactoryPlacement[] Modules=Array.Empty<FactoryPlacement>();
        public FactoryRoutePlacement[] Routes=Array.Empty<FactoryRoutePlacement>();
        public FactoryRegion[] Regions=Array.Empty<FactoryRegion>();
        public int RefillInterval=600,FinishedCapacity=8,DispatchInterval=240;
        public string Version="Candidate";
        [TextArea] public string FrozenInterfaceSignature;
        public FactoryDefinition CreateDefinition()
        {
            foreach(var r in Routes)r.Connection.Length=Mathf.CeilToInt(r.Length*EP02Route.UnitsPerMeter)+r.Connection.Spacing;
            var d=new FactoryDefinition { Recipes=Recipes,Modules=Modules.Select(m=>m.Module).ToArray(),Connections=Routes.Select(r=>r.Connection).ToArray(),RefillInterval=RefillInterval,FinishedCapacity=FinishedCapacity,DispatchInterval=DispatchInterval };d.Validate();return d;
        }
        public void Validate()
        {
            if(GridMeters<=0||FactorySize.x<1||FactorySize.y<1)throw new InvalidOperationException("Invalid factory grid.");
            CreateDefinition();
            foreach(var m in Modules)
            {
                if(m.Prefab==null||m.Footprint.x<1||m.Footprint.y<1||m.Grid.x<0||m.Grid.y<0||m.Grid.x+m.OccupiedSize.x>FactorySize.x||m.Grid.y+m.OccupiedSize.y>FactorySize.y||m.QuarterTurns<0||m.QuarterTurns>3)throw new InvalidOperationException("Invalid placement: "+m.Module.Id);
                if(m.Prefab.GetComponent<FactoryModuleAuthoring>()==null)throw new InvalidOperationException("Missing functional root: "+m.Module.Id);
                var view=m.Prefab.GetComponent<FactoryModuleAuthoring>();var recipe=Recipes.FirstOrDefault(r=>r.Id==m.Module.RecipeId);
                if(view.Footprint!=m.Footprint||view.Ports.Count(p=>p.Output)!=1||view.Ports.Single(p=>p.Output).Material!=(recipe?.Output??m.Module.Material)||!view.Ports.Where(p=>!p.Output).Select(p=>p.Material).OrderBy(x=>x).SequenceEqual((recipe?.Inputs.Select(i=>i.Material)??new[]{m.Module.Material}).OrderBy(x=>x)))throw new InvalidOperationException("Prefab ports/footprint disagree with configuration: "+m.Module.Id);
            }
            for(var i=0;i<Modules.Length;i++)for(var j=i+1;j<Modules.Length;j++)
            {
                var a=Modules[i];var b=Modules[j];if(new RectInt(a.Grid,a.OccupiedSize).Overlaps(new RectInt(b.Grid,b.OccupiedSize)))throw new InvalidOperationException("Overlapping modules: "+a.Module.Id+" / "+b.Module.Id);
            }
            foreach(var r in Routes)if(r.Points.Length<2||r.Points.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))||r.Length<1)throw new InvalidOperationException("Invalid route: "+r.Connection.Id);
            Vector3 Endpoint(string id,bool output,int material)
            {
                var placement=Modules.Single(m=>m.Module.Id==id);var view=placement.Prefab.GetComponent<FactoryModuleAuthoring>();var port=view.Ports.Single(p=>p.Output==output&&p.Material==material);
                var center=new Vector3((placement.Grid.x+placement.OccupiedSize.x*.5f)*GridMeters,0,(placement.Grid.y+placement.OccupiedSize.y*.5f)*GridMeters)+placement.PositionOffset;
                return center+Quaternion.Euler(0,placement.QuarterTurns*90,0)*view.transform.InverseTransformPoint(port.Anchor.position);
            }
            foreach(var r in Routes)
            {
                if(Vector3.Distance(r.Points[0],Endpoint(r.Connection.From,true,r.Connection.Material))>.01f||Vector3.Distance(r.Points.Last(),Endpoint(r.Connection.To,false,r.Connection.Material))>.01f)throw new InvalidOperationException("Route endpoints no longer match module ports: "+r.Connection.Id);
            }
            foreach(var route in Routes)for(var i=1;i<route.Points.Length;i++)
                if(FactoryRouteGeometry.SlopeDegrees(route.Points[i-1],route.Points[i])>FactoryRouteGeometry.MaxSlopeDegrees+.01f)
                    throw new InvalidOperationException("Conveyor slope exceeds 40 degrees: "+route.Connection.Id+" segment "+i);
            for(var i=0;i<Routes.Length;i++)for(var j=i+1;j<Routes.Length;j++)
            {
                var a=Routes[i];var b=Routes[j];
                // Shared source/receiver lifts are explicit split/merge nodes, with logical arbitration.
                if(a.Connection.From==b.Connection.From||a.Connection.To==b.Connection.To)continue;
                for(var p=1;p<a.Points.Length;p++)for(var q=1;q<b.Points.Length;q++)
                {
                    var x=a.Points[p-1];var y=a.Points[p];var u=b.Points[q-1];var v=b.Points[q];
                    if(FactoryRouteGeometry.PlanarSeparation(x,y,u,v,out var ta,out var tb)<.53f&&Mathf.Abs(Mathf.Lerp(x.y,y.y,ta)-Mathf.Lerp(u.y,v.y,tb))<.75f)
                        throw new InvalidOperationException("Bridge clearance below 0.75m: "+a.Connection.Id+" / "+b.Connection.Id);
                }
            }
            foreach(var route in Routes)for(var i=1;i<route.Points.Length;i++)for(var j=i+2;j<route.Points.Length;j++)
                if(FactoryRouteGeometry.PlanarSeparation(route.Points[i-1],route.Points[i],route.Points[j-1],route.Points[j],out var a,out var b)<.001f&&Mathf.Abs(Mathf.Lerp(route.Points[i-1].y,route.Points[i].y,a)-Mathf.Lerp(route.Points[j-1].y,route.Points[j].y,b))<.75f)
                    throw new InvalidOperationException("Conveyor doubles back through itself: "+route.Connection.Id);
        }
        public static bool PlanarCrosses(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            float Cross(Vector3 p,Vector3 q,Vector3 r)=>(q.x-p.x)*(r.z-p.z)-(q.z-p.z)*(r.x-p.x);
            return Cross(a,b,c)*Cross(a,b,d)<=0&&Cross(c,d,a)*Cross(c,d,b)<=0&&Mathf.Max(Mathf.Min(a.x,b.x),Mathf.Min(c.x,d.x))<=Mathf.Min(Mathf.Max(a.x,b.x),Mathf.Max(c.x,d.x))+.4f&&Mathf.Max(Mathf.Min(a.z,b.z),Mathf.Min(c.z,d.z))<=Mathf.Min(Mathf.Max(a.z,b.z),Mathf.Max(c.z,d.z))+.4f;
        }
    }
}
