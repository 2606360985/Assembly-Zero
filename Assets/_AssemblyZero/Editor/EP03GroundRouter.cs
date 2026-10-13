using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity.Editor
{
    // Editor-only routing in horizontal cells and height levels. Every ramp step is 33.69 degrees.
    internal static class EP03GroundRouter
    {
        private const float Step=.6f, Rise=.4f, Deck=.7f, Offset=.3f;
        private const int MaxLevel=16;
        private sealed class Obstacle
        {
            public Vector3 A,B;
            public string From,To;
            public string Id;
            public bool Baked;
        }
        public static void Refine(FactoryLayoutAsset asset)=>Bake(asset);
        public static void Bake(FactoryLayoutAsset asset)
        {
            var order=asset.Routes.OrderBy(r=>r.Connection.Material<8?0:1).ThenBy(r=>r.Connection.Material<8?-r.Connection.Material:r.Connection.Material).ThenBy(r=>r.Connection.From.StartsWith("hub-",StringComparison.Ordinal)?1:0).ThenBy(r=>r.Connection.Id,StringComparer.Ordinal).ToList();
            var seen=new HashSet<string>();
            for(var attempt=0;attempt<100;attempt++)
            {
                if(!seen.Add(string.Join("|",order.Select(r=>r.Connection.Id))))throw new InvalidOperationException("Unresolved EP03 port access ordering.");
                try{BakeOrdered(asset,order);EP03ElevatorPlanner.Bake(asset);return;}
                catch(AccessException e)
                {
                    var route=order.Single(r=>r.Connection.Id==e.Route);var blocker=order.FindIndex(r=>r.Connection.Id==e.Blocker);
                    if(blocker<0||blocker>=order.IndexOf(route))throw;
                    order.Remove(route);order.Insert(blocker,route);Debug.Log("EP03 reserve approach: "+e.Route+" before "+e.Blocker);
                }
            }
            throw new InvalidOperationException("EP03 routing did not converge.");
        }
        private sealed class AccessException : InvalidOperationException
        {
            public readonly string Route,Blocker;
            public AccessException(string route,string blocker):base("No slope-safe conveyor corridor for "+route+"; blocked by "+blocker){Route=route;Blocker=blocker;}
        }
        private static void BakeOrdered(FactoryLayoutAsset asset,List<FactoryRoutePlacement> order)
        {
            Vector3 Port(string id,bool output,int type)
            {
                var p=asset.Modules.Single(m=>m.Module.Id==id);var a=p.Prefab.GetComponent<FactoryModuleAuthoring>();var port=a.Ports.Single(v=>v.Output==output&&v.Material==type);
                return new Vector3((p.Grid.x+p.OccupiedSize.x*.5f)*asset.GridMeters,0,(p.Grid.y+p.OccupiedSize.y*.5f)*asset.GridMeters)+p.PositionOffset+Quaternion.Euler(0,p.QuarterTurns*90,0)*a.transform.InverseTransformPoint(port.Anchor.position);
            }
            Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.RoundToInt((p.x-Offset)/Step),Mathf.RoundToInt((p.z-Offset)/Step));
            Vector3 World(Vector3Int p)=>new Vector3(p.x*Step+Offset,Deck+p.y*Rise,p.z*Step+Offset);
            var buckets=new Dictionary<Vector2Int,List<Obstacle>>();
            void Add(Obstacle obstacle)
            {
                var lo=Cell(Vector3.Min(obstacle.A,obstacle.B)-new Vector3(.9f,0,.9f));var hi=Cell(Vector3.Max(obstacle.A,obstacle.B)+new Vector3(.9f,0,.9f));
                for(var x=lo.x;x<=hi.x;x++)for(var z=lo.y;z<=hi.y;z++)
                {
                    var key=new Vector2Int(x,z);if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<Obstacle>();list.Add(obstacle);
                }
            }
            // Future ports stay accessible: a main trunk must bridge over their ground-level spurs.
            foreach(var route in asset.Routes)
            {
                var c=route.Connection;
                var a=Port(c.From,true,c.Material);var b=Port(c.To,false,c.Material);
                var ac=Cell(a+Vector3.right*.3f);var bc=Cell(b+Vector3.left*(b.y<Deck-.01f?1.2f:.3f));
                var aw=World(new Vector3Int(ac.x,0,ac.y));var bw=World(new Vector3Int(bc.x,0,bc.y));
                var stubA=new Vector3(aw.x,Deck,a.z);var stubB=new Vector3(bw.x,Deck,b.z);
                foreach(var pair in new[]{(a,stubA),(stubA,aw),(bw,stubB),(stubB,b)})
                    if((pair.Item1-pair.Item2).sqrMagnitude>.000001f)Add(new Obstacle{A=pair.Item1,B=pair.Item2,From=c.From,To=c.To});
            }
            var baked=new List<FactoryRoutePlacement>();
            foreach(var route in order)
            {
                var c=route.Connection;var start=Port(c.From,true,c.Material);var end=Port(c.To,false,c.Material);
                string blockerId=null;var blockerDistance=float.MaxValue;
                var firstCell=Cell(start+Vector3.right*.3f);var lastCell=Cell(end+Vector3.left*(end.y<Deck-.01f?1.2f:.3f));
                var first=new Vector3Int(firstCell.x,0,firstCell.y);var last=new Vector3Int(lastCell.x,0,lastCell.y);
                var blockedCache=new Dictionary<Vector2Int,bool>();
                bool Blocked(Vector3Int p)
                {
                    if(p.x==first.x&&p.z==first.z||p.x==last.x&&p.z==last.z)return false;
                    var key=new Vector2Int(p.x,p.z);if(blockedCache.TryGetValue(key,out var value))return value;
                    var w=World(p);var blocked=w.x<.29f||w.z<.29f||w.x>asset.FactorySize.x*asset.GridMeters-.29f||w.z>asset.FactorySize.y*asset.GridMeters-.29f;
                    foreach(var m in asset.Modules)
                    {
                        if(blocked)break;
                        var center=new Vector3((m.Grid.x+m.OccupiedSize.x*.5f)*asset.GridMeters,0,(m.Grid.y+m.OccupiedSize.y*.5f)*asset.GridMeters)+m.PositionOffset;
                        var half=new Vector2(m.OccupiedSize.x,m.OccupiedSize.y)*asset.GridMeters*.5f;
                        if(Mathf.Abs(w.x-center.x)<half.x+.27f&&Mathf.Abs(w.z-center.z)<half.y+.27f)blocked=true;
                    }
                    blocked|=w.x>85||(w.x>55&&(!c.To.StartsWith("assembly-in-",StringComparison.Ordinal)||w.z<44||w.z>50.5f));
                    blockedCache[key]=blocked;return blocked;
                }
                var probes=new Dictionary<(Vector2 a,Vector2 b),List<(float fraction,float height,string id,bool baked)>>();
                bool Clear(Vector3 a,Vector3 b)
                {
                    var key=(FactoryRouteGeometry.Flat(a),FactoryRouteGeometry.Flat(b));
                    if(!probes.TryGetValue(key,out var checks))
                    {
                        checks=new List<(float,float,string,bool)>();probes[key]=checks;
                        if(buckets.TryGetValue(Cell((a+b)*.5f),out var nearby))
                        foreach(var obstacle in nearby)
                        {
                            if(obstacle.From==c.From||obstacle.To==c.To)continue;
                            if(FactoryRouteGeometry.PlanarSeparation(a,b,obstacle.A,obstacle.B,out var ta,out var tb)>.55f)continue;
                            checks.Add((ta,Mathf.Lerp(obstacle.A.y,obstacle.B.y,tb),obstacle.Id,obstacle.Baked));
                            for(var k=0;k<=2;k++)
                            {
                                var point=Vector3.Lerp(a,b,k*.5f);var flat=FactoryRouteGeometry.Flat(obstacle.B-obstacle.A);
                                var t=flat.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(FactoryRouteGeometry.Flat(point-obstacle.A),flat)/flat.sqrMagnitude);
                                if(Vector2.Distance(FactoryRouteGeometry.Flat(point),FactoryRouteGeometry.Flat(obstacle.A)+flat*t)<.55f)
                                    checks.Add((k*.5f,Mathf.Lerp(obstacle.A.y,obstacle.B.y,t),obstacle.Id,obstacle.Baked));
                            }
                        }
                    }
                    foreach(var check in checks)
                    {
                        if(Mathf.Abs(Mathf.Lerp(a.y,b.y,check.fraction)-check.height)>=FactoryRouteGeometry.Clearance)continue;
                        var distance=Vector2.Distance(FactoryRouteGeometry.Flat((a+b)*.5f),FactoryRouteGeometry.Flat(end));
                        if(check.baked&&distance<blockerDistance){blockerDistance=distance;blockerId=check.id;}
                        return false;
                    }
                    return true;
                }
                var deckThresholds=new Dictionary<(Vector2Int,Vector3Int),float>();
                bool HoldDeck(Vector3Int p,Vector3Int direction)
                {
                    var position=World(p);var forward=new Vector3(direction.x,0,direction.z);
                    var cacheKey=(new Vector2Int(p.x,p.z),direction);
                    if(deckThresholds.TryGetValue(cacheKey,out var threshold))return position.y>=threshold;
                    threshold=float.PositiveInfinity;
                    for(var k=0;k<=4;k++)
                    {
                        var key=new Vector2Int(p.x+direction.x*k,p.z+direction.z*k);
                        if(!buckets.TryGetValue(key,out var nearby))continue;
                        foreach(var obstacle in nearby)
                            if(obstacle.From!=c.From&&obstacle.To!=c.To&&FactoryRouteGeometry.TryCrossingHeightAhead(position,forward,obstacle.A,obstacle.B,out var height))threshold=Mathf.Min(threshold,height+FactoryRouteGeometry.Clearance-.001f);
                    }
                    deckThresholds[cacheKey]=threshold;return position.y>=threshold;
                }
                var startStub=new Vector3(World(first).x,Deck,start.z);var endStub=new Vector3(World(last).x,Deck,end.z);
                if(!Clear(start,startStub)||!Clear(startStub,World(first))||!Clear(World(last),endStub)||!Clear(endStub,end))throw new InvalidOperationException("Port approach blocked: "+c.Id);
                var cost=new Dictionary<Vector3Int,float>{{first,0}};var previous=new Dictionary<Vector3Int,Vector3Int>();
                var open=new SortedSet<(float score,int serial,Vector3Int cell)>(Comparer<(float score,int serial,Vector3Int cell)>.Create((a,b)=>a.score!=b.score?a.score.CompareTo(b.score):a.serial.CompareTo(b.serial)));
                var serial=0;open.Add((0,serial++,first));var closed=new HashSet<Vector3Int>();var found=false;
                var directions=new[]{Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
                while(open.Count>0)
                {
                    var entry=open.Min;open.Remove(entry);var p=entry.cell;if(!closed.Add(p))continue;if(p==last){found=true;break;}
                    foreach(var dir in directions)for(var rise=-1;rise<=1;rise++)
                    {
                        var q=p+dir+new Vector3Int(0,rise,0);if(q.y<0||q.y>MaxLevel||Blocked(q)||closed.Contains(q)||!Clear(World(p),World(q)))continue;
                        if(rise<0&&HoldDeck(p,dir))continue;
                        var entryDirection=World(first)-startStub;var exit=endStub-World(last);
                        if(rise!=0&&p==first&&Vector3.Dot(new Vector3(dir.x,0,dir.z),entryDirection)<-.001f)continue;
                        if(rise!=0&&q==last&&Vector3.Dot(new Vector3(dir.x,0,dir.z),exit)<-.001f)continue;
                        var selfClear=true;var cursor=p;var history=0;
                        if(previous.TryGetValue(cursor,out var before))
                        {
                            cursor=before;
                            while(history++<24&&previous.TryGetValue(cursor,out before))
                            {
                                var x=World(before);var y=World(cursor);
                                var a=World(p);var b=World(q);
                                if(Mathf.Max(a.x,b.x)<Mathf.Min(x.x,y.x)-.001f||Mathf.Max(x.x,y.x)<Mathf.Min(a.x,b.x)-.001f||Mathf.Max(a.z,b.z)<Mathf.Min(x.z,y.z)-.001f||Mathf.Max(x.z,y.z)<Mathf.Min(a.z,b.z)-.001f){cursor=before;continue;}
                                if(FactoryRouteGeometry.PlanarSeparation(World(p),World(q),x,y,out var sa,out var sb)<.001f&&Mathf.Abs(Mathf.Lerp(World(p).y,World(q).y,sa)-Mathf.Lerp(x.y,y.y,sb))<.75f){selfClear=false;break;}
                                cursor=before;
                            }
                            if(cursor==first)
                            foreach(var pair in new[]{(start,startStub),(startStub,World(first))})
                                if(FactoryRouteGeometry.PlanarSeparation(World(p),World(q),pair.Item1,pair.Item2,out var sa,out var sb)<.001f&&Mathf.Abs(Mathf.Lerp(World(p).y,World(q).y,sa)-Mathf.Lerp(pair.Item1.y,pair.Item2.y,sb))<.75f){selfClear=false;break;}
                        }
                        if(q!=last)
                        foreach(var pair in new[]{(World(last),endStub),(endStub,end)})
                            if(FactoryRouteGeometry.PlanarSeparation(World(p),World(q),pair.Item1,pair.Item2,out var sa,out var sb)<.001f&&Mathf.Abs(Mathf.Lerp(World(p).y,World(q).y,sa)-Mathf.Lerp(pair.Item1.y,pair.Item2.y,sb))<.75f){selfClear=false;break;}
                        if(!selfClear)continue;
                        var bend=0f;
                        if(previous.TryGetValue(p,out var prev))
                        {
                            var old=p-prev;
                            if(old.x!=dir.x||old.z!=dir.z)bend+=1.5f;
                            if(old.y!=rise)bend+=2f;
                            if((old.x!=dir.x||old.z!=dir.z)&&(old.y!=0||rise!=0))bend+=8f;
                        }
                        var next=cost[p]+1+bend+q.y*.4f+(rise==0?0:4f);
                        if(cost.TryGetValue(q,out var oldCost)&&oldCost<=next)continue;cost[q]=next;previous[q]=p;
                        open.Add((next+Mathf.Abs(q.x-last.x)+Mathf.Abs(q.z-last.z)+Mathf.Abs(q.y-last.y),serial++,q));
                    }
                }
                if(!found)throw new AccessException(c.Id,blockerId);
                var cells=new List<Vector3Int>{last};while(cells[cells.Count-1]!=first)cells.Add(previous[cells[cells.Count-1]]);cells.Reverse();
                var points=new List<Vector3>{start,startStub};points.AddRange(cells.Select(World));points.Add(endStub);points.Add(end);
                for(var i=points.Count-1;i>0;i--)if((points[i]-points[i-1]).sqrMagnitude<.000001f)points.RemoveAt(i);
                var compact=new List<Vector3>{points[0]};
                for(var i=1;i<points.Count-1;i++)if(Vector3.Cross((points[i]-compact[compact.Count-1]).normalized,(points[i+1]-points[i]).normalized).sqrMagnitude>.000001f)compact.Add(points[i]);
                compact.Add(points[points.Count-1]);
                var placement=new FactoryRoutePlacement{Connection=c,Points=compact.ToArray(),BridgeLevel=0};baked.Add(placement);
                for(var i=1;i<placement.Points.Length;i++)Add(new Obstacle{A=placement.Points[i-1],B=placement.Points[i],From=c.From,To=c.To,Id=c.Id,Baked=true});
                Debug.Log("EP03 slope-safe route "+baked.Count+"/"+asset.Routes.Length+": "+c.Id);
            }
            asset.Routes=baked.ToArray();
        }
    }
}
