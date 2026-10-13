using UnityEngine;

namespace AssemblyZero.Unity
{
    // Distances are measured along the horizontal route, never along an already inclined belt.
    public static class FactoryRouteGeometry
    {
        public const float ElevatorDockClearance=.34f;
        public static Vector3[] TrimDockedSection(Vector3[] points,float entry,float exit)
        {
            var result=new System.Collections.Generic.List<Vector3>(points);
            void Trim(float distance,bool front)
            {
                while(distance>0&&result.Count>1)
                {
                    var index=front?0:result.Count-1;var next=front?1:result.Count-2;
                    var delta=result[next]-result[index];var length=delta.magnitude;
                    if(length<=distance+.000001f){result.RemoveAt(index);distance-=length;}
                    else{result[index]+=delta*(distance/length);distance=0;}
                }
            }
            Trim(entry,true);Trim(exit,false);return result.Count<2?System.Array.Empty<Vector3>():result.ToArray();
        }
        public static bool IsVerticalTransfer(Vector3 a,Vector3 b)=>Flat(b-a).sqrMagnitude<.000001f&&Mathf.Abs(b.y-a.y)>.01f;
        public static bool TransferSegmentsConflict(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            var av=IsVerticalTransfer(a,b);var bv=IsVerticalTransfer(c,d);
            if(PlanarSeparation(a,b,c,d,out var ta,out var tb)>=(av||bv ? .58f : .53f))return false;
            var lowA=av?Mathf.Min(a.y,b.y):Mathf.Lerp(a.y,b.y,ta);var highA=av?Mathf.Max(a.y,b.y):lowA;
            var lowB=bv?Mathf.Min(c.y,d.y):Mathf.Lerp(c.y,d.y,tb);var highB=bv?Mathf.Max(c.y,d.y):lowB;
            return Mathf.Max(lowA,lowB)-Mathf.Min(highA,highB)<.75f;
        }
        // Reject a shared miter if its frame collapses (e.g. an ascending/descending
        // switchback) or its extension consumes a short neighbouring segment.
        public static bool CanShareConveyorSection(Vector3[] path,int i)
        {
            if(i<=0||i>=path.Length-1)return false;
            var incoming=path[i]-path[i-1];var outgoing=path[i+1]-path[i];
            var before=incoming.normalized;var after=outgoing.normalized;
            var tangent=(before+after).normalized;
            if(Flat(tangent).sqrMagnitude<.01f)return false;
            var side=Vector3.Cross(Vector3.up,tangent).normalized;
            var up=Vector3.Cross(tangent,side).normalized;
            var sideDot=Vector3.Dot(side,Vector3.Cross(Vector3.up,after).normalized);
            var upDot=Vector3.Dot(up,Vector3.Cross(after,side).normalized);
            if(sideDot<.5f||upDot<.5f)return false;
            var sideOffset=side*(.27f/sideDot);var upOffset=up*(.16f/upDot);
            var beforeExtension=Mathf.Abs(Vector3.Dot(sideOffset,before))+Mathf.Abs(Vector3.Dot(upOffset,before));
            var afterExtension=Mathf.Abs(Vector3.Dot(sideOffset,after))+Mathf.Abs(Vector3.Dot(upOffset,after));
            return beforeExtension<incoming.magnitude*.45f&&afterExtension<outgoing.magnitude*.45f;
        }
        // One shared centreline for conveyor meshes and cargo presentation.
        // Trim at most 45% of either neighbour so adjacent corners cannot overlap.
        public static Vector3[] RoundedPath(Vector3[] points)
        {
            if(points.Length<3)return (Vector3[])points.Clone();
            var result=new System.Collections.Generic.List<Vector3>{points[0]};
            for(var i=1;i<points.Length-1;i++)
            {
                var incoming=points[i]-points[i-1];var outgoing=points[i+1]-points[i];
                var u=incoming.normalized;var v=outgoing.normalized;
                if(Mathf.Abs(u.y)>.001f||Mathf.Abs(v.y)>.001f||Mathf.Abs(Vector3.Dot(u,v))>.001f||incoming.magnitude<.001f||outgoing.magnitude<.001f)
                {result.Add(points[i]);continue;}
                var radius=Mathf.Min(.48f,incoming.magnitude*.45f,outgoing.magnitude*.45f);
                // A bend tighter than the guard width would invert its inside edge.
                // These ports use a compact right-angle transfer deck instead.
                if(radius<.30f){result.Add(points[i]);continue;}
                var start=points[i]-u*radius;var center=start+v*radius;
                for(var step=0;step<=16;step++)
                {
                    var angle=step*Mathf.PI/32;
                    result.Add(center-v*(Mathf.Cos(angle)*radius)+u*(Mathf.Sin(angle)*radius));
                }
            }
            result.Add(points[points.Length-1]);return result.ToArray();
        }
        public const float MaxSlopeDegrees = 40f;
        public const float Clearance = .9f;
        public static bool LowerCrossingAhead(Vector3 position,Vector3 direction,Vector3 lowerStart,Vector3 lowerEnd,float distance=2.4f)
            =>TryCrossingHeightAhead(position,direction,lowerStart,lowerEnd,out var height,distance)&&position.y-height>=Clearance-.001f;

        public static bool TryCrossingHeightAhead(Vector3 position,Vector3 direction,Vector3 lowerStart,Vector3 lowerEnd,out float height,float distance=2.4f)
        {
            direction.y=0;direction.Normalize();
            var end=position+direction*distance;
            var separation=PlanarSeparation(position,end,lowerStart,lowerEnd,out _,out var t);
            height=Mathf.Lerp(lowerStart.y,lowerEnd.y,t);
            return separation<=.55f;
        }

        public static float SlopeDegrees(Vector3 a, Vector3 b) => Mathf.Atan2(Mathf.Abs(b.y - a.y), Vector2.Distance(Flat(a), Flat(b))) * Mathf.Rad2Deg;
        public static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        // Closest points also cover sloping, parallel and collinear route segments.
        public static float PlanarSeparation(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float ta, out float tb)
        {
            var u = Flat(b) - Flat(a); var v = Flat(d) - Flat(c); var w = Flat(c) - Flat(a);
            float Cross(Vector2 x, Vector2 y) => x.x * y.y - x.y * y.x;
            var cross = Cross(u, v);
            if (Mathf.Abs(cross) > .000001f)
            {
                var s = Cross(w, v) / cross; var t = Cross(w, u) / cross;
                if (s >= 0 && s <= 1 && t >= 0 && t <= 1) { ta = s; tb = t; return 0; }
            }
            float Project(Vector2 p, Vector2 origin, Vector2 delta) => delta.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - origin, delta) / delta.sqrMagnitude);
            ta=0;tb=Project(Flat(a),Flat(c),v);
            var best=Vector2.Distance(Flat(a),Flat(c)+v*tb);
            var t1=Project(Flat(b),Flat(c),v);var d1=Vector2.Distance(Flat(b),Flat(c)+v*t1);
            if(d1<best){best=d1;ta=1;tb=t1;}
            var s0=Project(Flat(c),Flat(a),u);var d0=Vector2.Distance(Flat(a)+u*s0,Flat(c));
            if(d0<best){best=d0;ta=s0;tb=0;}
            var s1=Project(Flat(d),Flat(a),u);var d2=Vector2.Distance(Flat(a)+u*s1,Flat(d));
            if(d2<best){best=d2;ta=s1;tb=1;}
            return best;
        }
    }
}
