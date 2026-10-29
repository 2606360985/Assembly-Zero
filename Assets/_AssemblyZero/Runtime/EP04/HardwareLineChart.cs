using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AssemblyZero.Unity.EP04
{
    // Lightweight uGUI line chart: horizontal grid, optional filled first series, polyline per series.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HardwareLineChart : MaskableGraphic
    {
        public readonly List<float[]> Series=new List<float[]>();
        public readonly List<Color> Colors=new List<Color>();
        public float Thickness=3;public int GridLines=3;public bool FillFirst=true;public bool IncludeZero=true;
        public Color GridColor=new Color(1,1,1,.08f);
        public int PointCount{get{var c=0;foreach(var s in Series)c=Mathf.Max(c,s.Length);return c;}}
        public void SetData(params (float[] Values,Color Color)[] data)
        {
            Series.Clear();Colors.Clear();foreach(var d in data){Series.Add(d.Values);Colors.Add(d.Color);}SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            for(var i=0;i<=GridLines;i++){var y=r.yMin+r.height*i/Mathf.Max(1,GridLines);Quad(vh,new Vector2(r.xMin,y-.5f),new Vector2(r.xMax,y-.5f),new Vector2(r.xMax,y+.5f),new Vector2(r.xMin,y+.5f),GridColor);}
            var min=IncludeZero?0f:float.MaxValue;var max=IncludeZero?0f:float.MinValue;var count=0;
            foreach(var s in Series){count=Mathf.Max(count,s.Length);foreach(var v in s){min=Mathf.Min(min,v);max=Mathf.Max(max,v);}}
            if(count<2)return;if(Mathf.Approximately(max,min))max=min+1;
            Vector2 P(int i,float v)=>new Vector2(r.xMin+r.width*i/(count-1),r.yMin+r.height*(v-min)/(max-min));
            for(var s=0;s<Series.Count;s++)
            {
                var values=Series[s];var color=Colors[s];if(values.Length<2)continue;
                if(s==0&&FillFirst)
                {
                    var baseY=P(0,Mathf.Clamp(0,min,max)).y;var fill=new Color(color.r,color.g,color.b,.18f);var clear=new Color(color.r,color.g,color.b,0);
                    for(var i=0;i<values.Length-1;i++){var a=P(i,values[i]);var b=P(i+1,values[i+1]);Quad(vh,new Vector2(a.x,baseY),new Vector2(b.x,baseY),b,a,clear,clear,fill,fill);}
                }
                for(var i=0;i<values.Length-1;i++)
                {
                    var a=P(i,values[i]);var b=P(i+1,values[i+1]);var n=(b-a).normalized;n=new Vector2(-n.y,n.x)*Thickness*.5f;
                    Quad(vh,a-n,b-n,b+n,a+n,color);
                }
                var last=P(values.Length-1,values[values.Length-1]);Quad(vh,last+new Vector2(-4,-4),last+new Vector2(4,-4),last+new Vector2(4,4),last+new Vector2(-4,4),color);
            }
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)=>Quad(vh,a,b,c,d,color,color,color,color);
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            var i=vh.currentVertCount;vh.AddVert(a,ca,Vector4.zero);vh.AddVert(b,cb,Vector4.zero);vh.AddVert(c,cc,Vector4.zero);vh.AddVert(d,cd,Vector4.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
