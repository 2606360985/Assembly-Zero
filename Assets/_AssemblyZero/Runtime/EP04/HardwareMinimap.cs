using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AssemblyZero.Unity.EP04
{
    // Cached schematic geometry; no extra camera, render texture, or simulation commands.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HardwareMinimap : MaskableGraphic, IPointerClickHandler
    {
        private HardwareGame game;
        private WorldSimulation cachedWorld;
        private int cachedState;
        private Rect cachedRect;
        private readonly List<UIVertex> background=new List<UIVertex>();
        private readonly Vector2[] corners=new Vector2[4];
        private readonly Color road=new Color(.25f,.34f,.39f),cyan=new Color(.09f,.8f,.9f);
        public void Initialize(HardwareGame value){game=value;raycastTarget=true;SetVerticesDirty();}
        public static Vector3 MapToWorld(Vector2 uv)=>new Vector3(Mathf.Clamp01(uv.x)*(GridRouting.WorldWidth-1)*1.2f,0,Mathf.Clamp01(uv.y)*(GridRouting.WorldDepth-1)*1.2f);
        public void Navigate(Vector2 uv){if(game!=null&&game.CanNavigate)game.FocusGroundPoint(MapToWorld(uv));}
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left||game==null||!game.CanNavigate)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,data.position,data.pressEventCamera,out var p))
            {
                var r=rectTransform.rect;
                if(p.x>=r.xMin-.01f&&p.x<=r.xMax+.01f&&p.y>=r.yMin-.01f&&p.y<=r.yMax+.01f)
                    Navigate(new Vector2((p.x-r.xMin)/r.width,(p.y-r.yMin)/r.height));
            }
        }
        private void LateUpdate(){if(game!=null)SetVerticesDirty();}
        private Vector2 Point(float x,float z)
        {
            var r=rectTransform.rect;return new Vector2(r.xMin+x/(GridRouting.WorldWidth-1)*r.width,r.yMin+z/(GridRouting.WorldDepth-1)*r.height);
        }
        private void RectShape(VertexHelper vh,Vector2 center,Vector2 size,Color tint)
        {
            var n=vh.currentVertCount;var a=center-size*.5f;var b=center+size*.5f;
            vh.AddVert(new Vector3(a.x,a.y),tint,Vector2.zero);vh.AddVert(new Vector3(a.x,b.y),tint,Vector2.zero);
            vh.AddVert(new Vector3(b.x,b.y),tint,Vector2.zero);vh.AddVert(new Vector3(b.x,a.y),tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        private static bool Clip(float p,float q,ref float from,ref float to)
        {
            if(Mathf.Abs(p)<.00001f)return q>=0;var t=q/p;
            if(p<0){if(t>to)return false;from=Mathf.Max(from,t);}else{if(t<from)return false;to=Mathf.Min(to,t);}return true;
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b,Color tint,float thickness=1.5f)
        {
            var r=rectTransform.rect;var d=b-a;float from=0,to=1;
            if(!Clip(-d.x,a.x-r.xMin,ref from,ref to)||!Clip(d.x,r.xMax-a.x,ref from,ref to)||!Clip(-d.y,a.y-r.yMin,ref from,ref to)||!Clip(d.y,r.yMax-a.y,ref from,ref to))return;
            b=a+d*to;a+=d*from;var normal=new Vector2(-(b-a).y,(b-a).x).normalized*(thickness*.5f);var n=vh.currentVertCount;
            vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(game==null||game.World==null)return;
            var w=game.World;var state=17;
            foreach(var f in w.State.Factories)state=state*31+(f.Owned?1:0)+(f.Discovered?2:0);
            foreach(var resource in w.State.Resources)state=state*31+(resource.Discovered?1:0);
            if(cachedWorld!=w||cachedState!=state||cachedRect!=rectTransform.rect)
            {
                cachedWorld=w;cachedState=state;cachedRect=rectTransform.rect;
                RectShape(vh,cachedRect.center,cachedRect.size,new Color(.035f,.07f,.1f));
                var h=game.Config.Economy.Hardware;var supplier=new GridPoint(h.SupplierX,h.SupplierZ);
                var cells=new HashSet<GridPoint>();
                foreach(var f in w.State.Factories)foreach(var dest in new[]{supplier,new GridPoint(h.OreX,h.OreZ)})
                {
                    var route=GridRouting.HardwarePath(f.Dock,dest,w.State.Factories);if(route==null)continue;
                    foreach(var cell in route)cells.Add(cell);
                }
                foreach(var p in cells)RectShape(vh,Point(p.X,p.Z),new Vector2(cachedRect.width/(GridRouting.WorldWidth-1)*1.2f,cachedRect.height/(GridRouting.WorldDepth-1)*1.2f),road);
                foreach(var f in w.State.Factories)
                {
                    var a=Point(f.X,f.Z);var b=Point(f.X+GridRouting.Width,f.Z+GridRouting.Depth);
                    RectShape(vh,(a+b)*.5f,b-a,f.Owned?new Color(.18f,.4f,.46f):new Color(.15f,.21f,.25f));
                }
                foreach(var resource in w.State.Resources)if(resource.Discovered)RectShape(vh,Point(resource.X,resource.Z),new Vector2(7,7),new Color(.85f,.65f,.29f));
                RectShape(vh,Point(h.SupplierX,h.SupplierZ),new Vector2(7,7),new Color(.61f,.69f,.95f));
                background.Clear();vh.GetUIVertexStream(background);
            }
            else vh.AddUIVertexTriangleStream(background);
            var truck=game.Truck;RectShape(vh,Point(truck.Position.X,truck.Position.Z),new Vector2(5,5),Color.white);
            var plane=new Plane(Vector3.up,Vector3.zero);var valid=true;
            for(var i=0;i<4;i++)
            {
                var uv=new Vector3(i==1||i==2?1:0,i>=2?1:0,0);var ray=game.ViewCamera.ViewportPointToRay(uv);
                if(!plane.Raycast(ray,out var distance)){valid=false;break;}var p=ray.GetPoint(distance);corners[i]=Point(p.x/1.2f,p.z/1.2f);
            }
            if(valid)for(var i=0;i<4;i++)Line(vh,corners[i],corners[(i+1)%4],cyan);
            var focus=Point(game.CameraFocus.x/1.2f,game.CameraFocus.z/1.2f);
            Line(vh,focus-Vector2.right*4,focus+Vector2.right*4,cyan);Line(vh,focus-Vector2.up*4,focus+Vector2.up*4,cyan);
        }
    }
}
