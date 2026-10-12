using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity.Editor
{
    // Explicit editor bake: cardinal routes around functional footprints; never runs in Play Mode.
    internal static class EP03GroundRouter
    {
        private const float Step=.6f,Deck=.7f;
        public static void Bake(FactoryLayoutAsset asset)
        {
            var baked=new List<FactoryRoutePlacement>();
            Vector3 Port(string id,bool output,int type)
            {
                var p=asset.Modules.Single(m=>m.Module.Id==id);var a=p.Prefab.GetComponent<FactoryModuleAuthoring>();var port=a.Ports.Single(v=>v.Output==output&&v.Material==type);
                return new Vector3((p.Grid.x+p.OccupiedSize.x*.5f)*asset.GridMeters,0,(p.Grid.y+p.OccupiedSize.y*.5f)*asset.GridMeters)+p.PositionOffset+Quaternion.Euler(0,p.QuarterTurns*90,0)*a.transform.InverseTransformPoint(port.Anchor.position);
            }
            foreach(var route in asset.Routes.OrderBy(r=>r.Connection.Material).ThenBy(r=>r.Connection.Id,StringComparer.Ordinal))
            {
                var c=route.Connection;var start=Port(c.From,true,c.Material);var end=Port(c.To,false,c.Material);
                Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x/Step),Mathf.RoundToInt(p.z/Step));
                Vector3 World(Vector2Int p)=>new Vector3(p.x*Step,Deck,p.y*Step);
                var first=Cell(start+Vector3.right*1.2f);var last=Cell(end+Vector3.left*1.2f);
                var occupied=new Dictionary<Vector2Int,int>();
                foreach(var prior in baked)for(var i=1;i<prior.Points.Length;i++)
                {
                    var a=prior.Points[i-1];var b=prior.Points[i];var n=Mathf.CeilToInt(Vector3.Distance(a,b)/Step);
                    for(var k=0;k<=n;k++){var key=Cell(Vector3.Lerp(a,b,k/(float)Mathf.Max(1,n)));occupied.TryGetValue(key,out var mask);occupied[key]=mask|(Mathf.Abs(b.x-a.x)>.01f?1:2);}
                }
                bool Blocked(Vector2Int p)
                {
                    if(p==first||p==last)return false;var w=World(p);
                    if(w.x<.6f||w.z<.6f||w.x>asset.FactorySize.x*asset.GridMeters-.6f||w.z>asset.FactorySize.y*asset.GridMeters-.6f)return true;
                    foreach(var m in asset.Modules)
                    {
                        var center=new Vector3((m.Grid.x+m.OccupiedSize.x*.5f)*asset.GridMeters,0,(m.Grid.y+m.OccupiedSize.y*.5f)*asset.GridMeters)+m.PositionOffset;
                        var half=new Vector2(m.OccupiedSize.x,m.OccupiedSize.y)*asset.GridMeters*.5f;
                        if(Mathf.Abs(w.x-center.x)<half.x+.35f&&Mathf.Abs(w.z-center.z)<half.y+.35f)return true;
                    }
                    // Reserve the assembly, scan and robot working envelope.
                    return w.x>85||(w.x>55&&(!c.To.StartsWith("assembly-in-",StringComparison.Ordinal)||w.z<44||w.z>50.5f));
                }
                var cost=new Dictionary<Vector2Int,float>{{first,0}};var previous=new Dictionary<Vector2Int,Vector2Int>();
                var open=new SortedSet<(float score,int serial,Vector2Int cell)>(Comparer<(float score,int serial,Vector2Int cell)>.Create((a,b)=>a.score!=b.score?a.score.CompareTo(b.score):a.serial.CompareTo(b.serial)));
                var serial=0;open.Add((0,serial++,first));var closed=new HashSet<Vector2Int>();var found=false;
                var directions=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
                while(open.Count>0)
                {
                    var entry=open.Min;open.Remove(entry);var p=entry.cell;if(!closed.Add(p))continue;if(p==last){found=true;break;}
                    foreach(var dir in directions)
                    {
                        var q=p+dir;if(Blocked(q)||closed.Contains(q))continue;
                        var bend=previous.TryGetValue(p,out var prev)&&p-prev!=dir?1.5f:0;
                        var traffic=occupied.TryGetValue(q,out var mask)?((mask&(dir.x!=0?1:2))!=0?100:3):0;
                        var next=cost[p]+1+bend+traffic;
                        if(cost.TryGetValue(q,out var old)&&old<=next)continue;cost[q]=next;previous[q]=p;
                        open.Add((next+Mathf.Abs(q.x-last.x)+Mathf.Abs(q.y-last.y),serial++,q));
                    }
                }
                if(!found)throw new InvalidOperationException("No ground corridor for "+c.Id);
                var cells=new List<Vector2Int>{last};while(cells[cells.Count-1]!=first)cells.Add(previous[cells[cells.Count-1]]);cells.Reverse();
                var points=new List<Vector3>{start,new Vector3(first.x*Step,Deck,start.z),World(first)};
                points.AddRange(cells.Skip(1).Select(World));points.Add(new Vector3(last.x*Step,Deck,end.z));points.Add(end);
                // Sample approach stubs too, so their intersections cannot fall between grid vertices.
                var sampled=new List<Vector3>{points[0]};
                for(var i=1;i<points.Count;i++){var n=Mathf.CeilToInt(Vector3.Distance(points[i-1],points[i])/(Step*.5f));for(var k=1;k<=n;k++)sampled.Add(Vector3.Lerp(points[i-1],points[i],k/(float)n));}points=sampled;
                // Elevate only the short crossing section. Separate connections do not become a false junction.
                for(var i=1;i<points.Count-1;i++)
                {
                    var point=points[i];var height=Deck;
                    foreach(var prior in baked)
                    {
                        if(prior.Connection.From==c.From||prior.Connection.To==c.To)continue;
                        for(var j=1;j<prior.Points.Length;j++)
                        {
                            var a=prior.Points[j-1];var b=prior.Points[j];var flat=new Vector2(b.x-a.x,b.z-a.z);if(flat.sqrMagnitude<.001f)continue;
                            var t=Mathf.Clamp01(Vector2.Dot(new Vector2(point.x-a.x,point.z-a.z),flat)/flat.sqrMagnitude);
                            if(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(a.x+flat.x*t,a.z+flat.y*t))<.45f)height=Mathf.Max(height,Mathf.Max(a.y,b.y)+.9f);
                        }
                    }
                    points[i]=new Vector3(point.x,height,point.z);
                }
                // Remove duplicates before simplification: a zero length neighbor must not erase a corner.
                for(var i=points.Count-1;i>0;i--)if((points[i]-points[i-1]).sqrMagnitude<.00001f)points.RemoveAt(i);
                // Retain turns, ramp ends and crossing decks; discard collinear samples.
                var compact=new List<Vector3>{points[0]};
                for(var i=1;i<points.Count-1;i++)if(Vector3.Cross(points[i]-points[i-1],points[i+1]-points[i]).sqrMagnitude>.00001f)compact.Add(points[i]);
                compact.Add(points[points.Count-1]);
                baked.Add(new FactoryRoutePlacement{Connection=c,Points=compact.ToArray(),BridgeLevel=0});
            }
            asset.Routes=baked.ToArray();
        }
    }
}
