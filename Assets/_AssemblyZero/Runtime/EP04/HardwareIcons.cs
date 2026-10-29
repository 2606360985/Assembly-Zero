using System;
using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Procedural white glyphs drawn from signed-distance primitives; tint them through Image.color.
    public static class HardwareIcons
    {
        public const int Size=64;
        private static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        private static float Circle(Vector2 p,Vector2 c,float r)=>(p-c).magnitude-r;
        private static float Box(Vector2 p,Vector2 c,Vector2 half,float round=0){var d=new Vector2(Mathf.Abs(p.x-c.x),Mathf.Abs(p.y-c.y))-half+Vector2.one*round;return new Vector2(Mathf.Max(d.x,0),Mathf.Max(d.y,0)).magnitude+Mathf.Min(Mathf.Max(d.x,d.y),0)-round;}
        private static float Segment(Vector2 p,Vector2 a,Vector2 b,float r){var pa=p-a;var ba=b-a;var h=Mathf.Clamp01(Vector2.Dot(pa,ba)/Vector2.Dot(ba,ba));return (pa-ba*h).magnitude-r;}
        private static float Ring(Vector2 p,Vector2 c,float r,float w)=>Mathf.Abs(Circle(p,c,r))-w;
        private static float Union(params float[] d){var m=float.MaxValue;foreach(var v in d)m=Mathf.Min(m,v);return m;}
        private static float Cut(float a,float b)=>Mathf.Max(a,-b);
        private static float Triangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            float Edge(Vector2 e0,Vector2 e1){var e=e1-e0;var n=new Vector2(e.y,-e.x).normalized;return Vector2.Dot(p-e0,n);}
            var s=Mathf.Sign((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x));return Mathf.Max(Edge(a,b)*-s,Mathf.Max(Edge(b,c)*-s,Edge(c,a)*-s));
        }
        private static readonly Dictionary<string,Func<Vector2,float>> shapes=new Dictionary<string,Func<Vector2,float>>
        {
            {"coin",p=>Union(Ring(p,new Vector2(32,32),24,4),Segment(p,new Vector2(24,40),new Vector2(32,30),3.5f),Segment(p,new Vector2(40,40),new Vector2(32,30),3.5f),Segment(p,new Vector2(32,30),new Vector2(32,18),3.5f),Segment(p,new Vector2(24,28),new Vector2(40,28),3),Segment(p,new Vector2(24,22),new Vector2(40,22),3))},
            {"star",p=>{var a=new Vector2[10];for(var i=0;i<10;i++){var ang=Mathf.PI/2+i*Mathf.PI/5;var r=i%2==0?27f:11f;a[i]=new Vector2(32+Mathf.Cos(ang)*r,32+Mathf.Sin(ang)*r);}var d=float.MaxValue;for(var i=0;i<10;i+=2)d=Mathf.Min(d,Triangle(p,new Vector2(32,32),a[i],a[(i+1)%10]));for(var i=0;i<10;i+=2)d=Mathf.Min(d,Triangle(p,new Vector2(32,32),a[i],a[(i+9)%10]));return d;}},
            {"clock",p=>Union(Ring(p,new Vector2(32,32),24,3.5f),Segment(p,new Vector2(32,32),new Vector2(32,48),3),Segment(p,new Vector2(32,32),new Vector2(43,26),3))},
            {"speed",p=>Union(Triangle(p,new Vector2(10,14),new Vector2(10,50),new Vector2(32,32)),Triangle(p,new Vector2(32,14),new Vector2(32,50),new Vector2(54,32)))},
            {"pause",p=>Union(Box(p,new Vector2(22,32),new Vector2(6,18),2),Box(p,new Vector2(42,32),new Vector2(6,18),2))},
            {"alert",p=>Cut(Triangle(p,new Vector2(32,58),new Vector2(6,10),new Vector2(58,10)),Union(Segment(p,new Vector2(32,42),new Vector2(32,26),3.5f),Circle(p,new Vector2(32,18),3.5f)))},
            {"bolt",p=>Union(Triangle(p,new Vector2(38,60),new Vector2(14,28),new Vector2(36,30)),Triangle(p,new Vector2(26,4),new Vector2(50,36),new Vector2(28,34)))},
            {"factory",p=>Union(Box(p,new Vector2(32,20),new Vector2(26,12)),Triangle(p,new Vector2(6,32),new Vector2(22,32),new Vector2(6,46)),Triangle(p,new Vector2(22,32),new Vector2(38,32),new Vector2(22,46)),Box(p,new Vector2(48,40),new Vector2(5,18)))},
            {"order",p=>Cut(Box(p,new Vector2(32,30),new Vector2(20,26),4),Union(Box(p,new Vector2(32,40),new Vector2(12,2.5f)),Box(p,new Vector2(32,30),new Vector2(12,2.5f)),Box(p,new Vector2(28,20),new Vector2(8,2.5f))))},
            {"gpu",p=>Cut(Box(p,new Vector2(32,32),new Vector2(28,16),4),Union(Circle(p,new Vector2(20,32),10),Circle(p,new Vector2(44,32),10)))},
            {"cart",p=>Union(Segment(p,new Vector2(6,50),new Vector2(14,50),3),Segment(p,new Vector2(14,50),new Vector2(20,22),3),Box(p,new Vector2(36,36),new Vector2(18,10),2),Circle(p,new Vector2(24,14),5),Circle(p,new Vector2(46,14),5))},
            {"market",p=>Union(Segment(p,new Vector2(8,16),new Vector2(24,32),3.5f),Segment(p,new Vector2(24,32),new Vector2(34,24),3.5f),Segment(p,new Vector2(34,24),new Vector2(54,46),3.5f),Triangle(p,new Vector2(56,52),new Vector2(42,48),new Vector2(54,36)))},
            {"truck",p=>Union(Box(p,new Vector2(24,34),new Vector2(18,12),2),Box(p,new Vector2(50,30),new Vector2(8,8),2),Circle(p,new Vector2(18,16),6),Circle(p,new Vector2(46,16),6))},
            {"report",p=>Union(Box(p,new Vector2(16,22),new Vector2(5,12)),Box(p,new Vector2(32,28),new Vector2(5,18)),Box(p,new Vector2(48,18),new Vector2(5,8)),Box(p,new Vector2(32,7),new Vector2(26,2)))},
            {"chip",p=>Union(Box(p,new Vector2(32,32),new Vector2(16,16),3),Segment(p,new Vector2(22,6),new Vector2(22,58),2),Segment(p,new Vector2(32,6),new Vector2(32,58),2),Segment(p,new Vector2(42,6),new Vector2(42,58),2),Segment(p,new Vector2(6,22),new Vector2(58,22),2),Segment(p,new Vector2(6,42),new Vector2(58,42),2))},
            {"memory",p=>Union(Box(p,new Vector2(32,30),new Vector2(26,12),2),Box(p,new Vector2(32,14),new Vector2(22,3)))},
            {"heat",p=>Union(Box(p,new Vector2(14,32),new Vector2(3,20)),Box(p,new Vector2(26,32),new Vector2(3,20)),Box(p,new Vector2(38,32),new Vector2(3,20)),Box(p,new Vector2(50,32),new Vector2(3,20)),Box(p,new Vector2(32,10),new Vector2(24,3)))},
            {"box",p=>Union(Box(p,new Vector2(32,26),new Vector2(24,18),2),Box(p,new Vector2(32,48),new Vector2(26,4),1))},
            {"defect",p=>Union(Ring(p,new Vector2(32,32),24,4),Segment(p,new Vector2(22,22),new Vector2(42,42),4),Segment(p,new Vector2(42,22),new Vector2(22,42),4))},
            {"ore",p=>Triangle(p,new Vector2(32,58),new Vector2(8,24),new Vector2(56,24))<0||Triangle(p,new Vector2(32,6),new Vector2(8,24),new Vector2(56,24))<0?-1:Mathf.Min(Triangle(p,new Vector2(32,58),new Vector2(8,24),new Vector2(56,24)),Triangle(p,new Vector2(32,6),new Vector2(8,24),new Vector2(56,24)))},
            {"wrench",p=>Union(Segment(p,new Vector2(18,18),new Vector2(42,42),5),Cut(Circle(p,new Vector2(46,46),12),Circle(p,new Vector2(52,52),7)))},
            {"ring",p=>Ring(p,new Vector2(32,32),27,4.5f)},
            {"dot",p=>Circle(p,new Vector2(32,32),26)},
            {"up",p=>Triangle(p,new Vector2(32,54),new Vector2(10,18),new Vector2(54,18))},
            {"down",p=>Triangle(p,new Vector2(32,10),new Vector2(54,46),new Vector2(10,46))},
            {"bill",p=>Cut(Box(p,new Vector2(32,32),new Vector2(30,16),3),Union(Ring(p,new Vector2(32,32),9,2),Box(p,new Vector2(32,32),new Vector2(26,12),1)<0&&Box(p,new Vector2(32,32),new Vector2(24,10),1)>0?-1:1))},
            {"sound",p=>Union(Box(p,new Vector2(16,32),new Vector2(6,9)),Triangle(p,new Vector2(18,32),new Vector2(36,52),new Vector2(36,12)),Ring(p,new Vector2(34,32),14,2.5f)<0&&p.x>40?-1:1,Ring(p,new Vector2(34,32),24,2.5f)<0&&p.x>44?-1:1)},
            {"lens",p=>Union(Ring(p,new Vector2(32,32),24,3.5f),Ring(p,new Vector2(32,32),12,3),Circle(p,new Vector2(32,32),4))},
            {"camera",p=>Cut(Union(Box(p,new Vector2(32,28),new Vector2(26,16),4),Box(p,new Vector2(32,46),new Vector2(9,4),1)),Ring(p,new Vector2(32,28),9,3))},
        };
        public static Sprite Get(string id)
        {
            if(cache.TryGetValue(id,out var sprite)&&sprite!=null)return sprite;
            if(!shapes.TryGetValue(id,out var shape))shape=shapes["dot"];
            var tex=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Hardware icon "+id,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[Size*Size];
            for(var y=0;y<Size;y++)for(var x=0;x<Size;x++){var d=shape(new Vector2(x+.5f,y+.5f));pixels[y*Size+x]=new Color(1,1,1,Mathf.Clamp01(.5f-d));}
            tex.SetPixels(pixels);tex.Apply();sprite=Sprite.Create(tex,new Rect(0,0,Size,Size),new Vector2(.5f,.5f),100);sprite.name=tex.name;cache[id]=sprite;return sprite;
        }
        public static string ForMaterial(int material)
        {
            switch(HardwareVisualCatalog.Get(material).Form)
            {
                case HardwareProductForm.Card:return material==HardwareCatalog.Defect?"defect":"gpu";
                case HardwareProductForm.Retail:return "box";
                case HardwareProductForm.Core:return "chip";
                case HardwareProductForm.Memory:return "memory";
                case HardwareProductForm.Cooler:return "heat";
                case HardwareProductForm.Ore:case HardwareProductForm.Billet:return "ore";
                default:return "box";
            }
        }
        public static string ForPage(string page)=>page=="工厂"?"factory":page=="订单"?"order":page=="产品"?"gpu":page=="采购"?"cart":page=="市场"?"market":page=="运输"?"truck":page=="报表"?"report":"dot";
    }
}
