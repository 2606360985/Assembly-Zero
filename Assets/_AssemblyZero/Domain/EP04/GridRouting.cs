using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblyZero.Domain.EP04
{
    public static class GridRouting
    {
        public const int Width=40, Depth=32, WorldWidth=116, WorldDepth=88;
        private static readonly GridPoint[] Directions={new GridPoint(1,0),new GridPoint(0,1),new GridPoint(-1,0),new GridPoint(0,-1),new GridPoint(0,0,1),new GridPoint(0,0,-1)};
        private readonly struct SearchKey : IEquatable<SearchKey>
        {
            public readonly GridPoint P;public readonly int Direction;
            public SearchKey(GridPoint p,int d){P=p;Direction=d;}
            public bool Equals(SearchKey k)=>P.Equals(k.P)&&Direction==k.Direction;
            public override bool Equals(object o)=>o is SearchKey k&&Equals(k);
            public override int GetHashCode()=>P.GetHashCode()*7+Direction;
        }
        public static bool WorldBlocked(GridPoint p)
        {
            // Two ridges with deliberate road openings; all destinations remain reachable.
            return p.X<0||p.Z<0||p.X>=WorldWidth||p.Z>=WorldDepth||
                (p.X>=49&&p.X<=53&&p.Z>=5&&p.Z<=68&&!(p.Z>=32&&p.Z<=39))||
                (p.Z>=43&&p.Z<=46&&p.X>=4&&p.X<=43&&!(p.X>=22&&p.X<=28));
        }
        public static List<GridPoint> WorldPath(GridPoint start,GridPoint end)=>Search(start,end,WorldBlocked,false);
        public static bool HardwareBlocked(GridPoint p,IEnumerable<FactorySite> factories)
        {
            if(WorldBlocked(p))return true;
            foreach(var f in factories)
                if(p.X>=f.X-1&&p.X<=f.X+Width+1&&p.Z>=f.Z-1&&p.Z<=f.Z+Depth+1)return true;
            return false;
        }
        public static List<GridPoint> HardwarePath(GridPoint start,GridPoint end,IEnumerable<FactorySite> factories)
            =>Search(start,end,p=>HardwareBlocked(p,factories),false);
        public static List<GridPoint> FactoryPath(BusinessNode from,BusinessNode to,IEnumerable<BusinessNode> nodes,IEnumerable<BusinessRoute> routes)
        {
            var start=from.Port(true);var end=to.Port(false);
            var obstacles=new HashSet<GridPoint>();
            foreach(var n in nodes)for(var x=n.X-1;x<=n.X+1;x++)for(var z=n.Z-1;z<=n.Z+1;z++)for(var h=0;h<=2;h++)obstacles.Add(new GridPoint(x,z,h));
            foreach(var r in routes)
                foreach(var p in r.Points)
                {
                    // Explicit shared source/output and shared destination/input manifolds.
                    // The source fan-out is wider so a hub warehouse can feed duplicated machines.
                    if(r.From==from.Id&&GridPoint.Distance(p,start)<=2||r.To==to.Id&&GridPoint.Distance(p,end)<=1)continue;
                    obstacles.Add(p);
                }
            // Reserve other machines' docking tiles, including their vertical shafts.
            foreach(var n in nodes)foreach(var p in new[]{n.Port(false),n.Port(true)})
                if(!p.Equals(start)&&!p.Equals(end))for(var h=0;h<=2;h++)obstacles.Add(new GridPoint(p.X,p.Z,h));
            bool Blocked(GridPoint p)=>p.X<0||p.Z<0||p.X>=Width||p.Z>=Depth||p.H<0||p.H>2||obstacles.Contains(p);
            return Search(start,end,Blocked,true);
        }
        private static List<GridPoint> Search(GridPoint start,GridPoint end,Func<GridPoint,bool> blocked,bool levels)
        {
            if(blocked(start)||blocked(end))return null;
            var first=new SearchKey(start,6);var best=new Dictionary<SearchKey,int>{{first,0}};
            var prev=new Dictionary<SearchKey,SearchKey>();
            var open=new SortedSet<(int score,int order,SearchKey key)>(Comparer<(int score,int order,SearchKey key)>.Create((a,b)=>a.score!=b.score?a.score.CompareTo(b.score):a.order.CompareTo(b.order)));
            var serial=0;open.Add((0,serial++,first));var visited=new HashSet<SearchKey>();
            while(open.Count>0)
            {
                var entry=open.Min;open.Remove(entry);var current=entry.key;
                if(!visited.Add(current))continue;
                if(current.P.Equals(end))
                {
                    var result=new List<GridPoint>{end};while(prev.TryGetValue(current,out var p)){current=p;result.Add(p.P);}result.Reverse();return result;
                }
                for(var i=0;i<(levels?6:4);i++)
                {
                    var d=Directions[i];var p=new GridPoint(current.P.X+d.X,current.P.Z+d.Z,current.P.H+d.H);
                    if(blocked(p))continue;
                    var next=new SearchKey(p,i);var cost=best[current]+(i>=4?12:2)+(current.Direction==i||current.Direction==6?0:1)+p.H;
                    if(best.TryGetValue(next,out var old)&&old<=cost)continue;
                    best[next]=cost;prev[next]=current;open.Add((cost+GridPoint.Distance(p,end)*2,serial++,next));
                }
            }
            return null;
        }
    }
}
