using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AssemblyZero.Unity.Editor
{
    // Replace consecutive turning ramps with a level approach, a clear vertical
    // shaft, and a level exit. Candidate selection is stable and checks full shaft height.
    internal static class EP03ElevatorPlanner
    {
        public static void Bake(FactoryLayoutAsset layout)
        {
            foreach(var route in layout.Routes)
            {
                var scan=1;
                while(scan<route.Points.Length)
                {
                    var points=route.Points;
                    if(Mathf.Abs(points[scan].y-points[scan-1].y)<.01f||FactoryRouteGeometry.IsVerticalTransfer(points[scan-1],points[scan])){scan++;continue;}
                    var start=scan-1;var end=scan;var sign=Mathf.Sign(points[scan].y-points[start].y);
                    while(end+1<points.Length&&(points[end+1].y-points[end].y)*sign>.01f&&!FactoryRouteGeometry.IsVerticalTransfer(points[end],points[end+1]))end++;
                    var direction=FactoryRouteGeometry.Flat(points[start+1]-points[start]).normalized;
                    var folded=Enumerable.Range(start+1,end-start).Any(i=>Vector2.Dot(direction,FactoryRouteGeometry.Flat(points[i]-points[i-1]).normalized)<.99f);
                    if(!folded){scan=end+1;continue;}
                    var replacement=FindReplacement(layout,route,start,end);
                    // Include neighbouring level approach/exit segments when a shaft
                    // cannot fit inside the original zigzag envelope.
                    for(var extra=1;replacement==null&&extra<=6;extra++)
                    for(var left=0;replacement==null&&left<=Mathf.Min(3,extra);left++)
                    {
                        var right=extra-left;if(right>3)continue;var s=start-left;var e=end+right;
                        if(s<0||e>=points.Length)continue;
                        if(Enumerable.Range(s,start-s+1).Any(i=>Mathf.Abs(points[i].y-points[start].y)>.01f)||Enumerable.Range(end,e-end+1).Any(i=>Mathf.Abs(points[i].y-points[end].y)>.01f))continue;
                        replacement=FindReplacement(layout,route,s,e);
                    }
                    if(replacement==null)throw new InvalidOperationException("No clear elevator shaft for "+route.Connection.Id+" points "+start+".."+end);
                    route.Points=replacement;
                    route.ElevatorSegments=Enumerable.Range(1,replacement.Length-1).Where(i=>FactoryRouteGeometry.IsVerticalTransfer(replacement[i-1],replacement[i])).ToArray();
                    route.PrepareVisualPath();
                    // Restart because the successful replacement may have fewer points.
                    scan=1;
                }
            }
        }
        private static Vector3[] FindReplacement(FactoryLayoutAsset layout,FactoryRoutePlacement route,int start,int end)
        {
            var original=route.Points;var a=original[start];var b=original[end];
            var roundedObstacles=layout.Routes.Where(r=>r!=route).ToDictionary(r=>r,r=>FactoryRouteGeometry.RoundedPath(r.Points));
            var shafts=new List<Vector2>();
            for(var i=start;i<=end;i++)shafts.Add(FactoryRouteGeometry.Flat(original[i]));
            var lo=shafts.Aggregate(Vector2.Min)-Vector2.one*1.8f;var hi=shafts.Aggregate(Vector2.Max)+Vector2.one*1.8f;
            for(var x=lo.x;x<=hi.x+.001f;x+=.6f)for(var z=lo.y;z<=hi.y+.001f;z+=.6f)shafts.Add(new Vector2(x,z));
            foreach(var shaft in shafts.Distinct().OrderBy(p=>Vector2.Distance(p,FactoryRouteGeometry.Flat(a))+Vector2.Distance(p,FactoryRouteGeometry.Flat(b))))
            for(var entry=0;entry<2;entry++)for(var exit=0;exit<2;exit++)
            {
                var lower=new Vector3(shaft.x,a.y,shaft.y);var upper=new Vector3(shaft.x,b.y,shaft.y);
                var path=new List<Vector3>();path.AddRange(original.Take(start+1));
                path.Add(entry==0?new Vector3(lower.x,a.y,a.z):new Vector3(a.x,a.y,lower.z));path.Add(lower);path.Add(upper);
                path.Add(exit==0?new Vector3(b.x,b.y,upper.z):new Vector3(upper.x,b.y,b.z));path.Add(b);path.AddRange(original.Skip(end+1));
                for(var i=path.Count-1;i>0;i--)if(Vector3.Distance(path[i],path[i-1])<.001f)path.RemoveAt(i);
                var result=path.ToArray();
                if(Clear(layout,route,result,start,end,roundedObstacles))return result;
            }
            return null;
        }
        private static bool Clear(FactoryLayoutAsset layout,FactoryRoutePlacement route,Vector3[] path,int start,int end,Dictionary<FactoryRoutePlacement,Vector3[]> roundedObstacles)
        {
            var prefix=start;var suffix=route.Points.Length-end-1;
            for(var i=prefix+1;i<path.Length-suffix;i++)
            {
                var a=path[i-1];var b=path[i];var vertical=FactoryRouteGeometry.IsVerticalTransfer(a,b);
                foreach(var p in new[]{a,b})
                {
                    if(p.x<.29f||p.z<.29f||p.x>layout.FactorySize.x*layout.GridMeters-.29f||p.z>layout.FactorySize.y*layout.GridMeters-.29f)return false;
                    if(layout.Regions.Any(r=>r.Reserved&&r.Bounds.Contains(new Vector2Int(Mathf.FloorToInt(p.x/layout.GridMeters),Mathf.FloorToInt(p.z/layout.GridMeters)))))return false;
                }
                foreach(var module in layout.Modules)
                {
                    if(!vertical&&(module.Module.Id==route.Connection.From||module.Module.Id==route.Connection.To))continue;
                    var center=new Vector3((module.Grid.x+module.OccupiedSize.x*.5f)*layout.GridMeters,0,(module.Grid.y+module.OccupiedSize.y*.5f)*layout.GridMeters)+module.PositionOffset;
                    var half=new Vector2(module.OccupiedSize.x,module.OccupiedSize.y)*layout.GridMeters*.5f+Vector2.one*.30f;
                    var steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.15f));
                    for(var k=0;k<=steps;k++){var p=Vector3.Lerp(a,b,(float)k/steps);if(Mathf.Abs(p.x-center.x)<half.x&&Mathf.Abs(p.z-center.z)<half.y)return false;}
                }
                foreach(var other in layout.Routes)
                {
                    if(other==route)continue;
                    if(route.Connection.From==other.Connection.From||route.Connection.To==other.Connection.To)continue;
                    var obstacle=vertical?roundedObstacles[other]:other.Points;
                    for(var j=1;j<obstacle.Length;j++)
                        if(FactoryRouteGeometry.TransferSegmentsConflict(a,b,obstacle[j-1],obstacle[j]))return false;
                }
            }
            // Non-adjacent own segments must also stay clear of the full shaft.
            for(var i=1;i<path.Length;i++)for(var j=i+2;j<path.Length;j++)
            {
                var a=path[i-1];var b=path[i];var c=path[j-1];var d=path[j];
                if(FactoryRouteGeometry.IsVerticalTransfer(a,b)||FactoryRouteGeometry.IsVerticalTransfer(c,d))
                {if(FactoryRouteGeometry.TransferSegmentsConflict(a,b,c,d))return false;}
                else if(FactoryRouteGeometry.PlanarSeparation(a,b,c,d,out var ta,out var tb)<.001f&&Mathf.Abs(Mathf.Lerp(a.y,b.y,ta)-Mathf.Lerp(c.y,d.y,tb))<.75f)return false;
            }
            return true;
        }
    }
}
