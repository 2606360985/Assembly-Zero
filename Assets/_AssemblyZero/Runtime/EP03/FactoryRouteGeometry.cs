using UnityEngine;

namespace AssemblyZero.Unity
{
    // Distances are measured along the horizontal route, never along an already inclined belt.
    public static class FactoryRouteGeometry
    {
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
