using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class EP04Game
    {
        private Transform environmentRoot,factoryRoot,vehicleRoot;
        private readonly Dictionary<Color,Material> paints=new Dictionary<Color,Material>();
        private readonly Dictionary<int,TextMesh> nodeLabels=new Dictionary<int,TextMesh>();
        private readonly Dictionary<int,Transform> vehicleViews=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Vector3[]> routePaths=new Dictionary<int,Vector3[]>();
        private readonly Dictionary<int,Matrix4x4[]> cargoMatrices=new Dictionary<int,Matrix4x4[]>();
        private Mesh cargoMesh;
        private LineRenderer journeyView;
        private readonly Color orange=new Color(.98f,.63f,.2f),teal=new Color(.17f,.72f,.68f);
        private Material Paint(Color color)
        {
            if(paints.TryGetValue(color,out var material))return material;
            material=Config.BaseMaterial!=null?new Material(Config.BaseMaterial):new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;material.enableInstancing=true;paints.Add(color,material);return material;
        }
        private GameObject Box(string name,Vector3 position,Vector3 scale,Color color,Transform parent)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=position;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=Paint(color);Destroy(g.GetComponent<Collider>());return g;
        }
        private TextMesh Label(string text,Vector3 position,float size,Transform parent)
        {
            var g=new GameObject("World label");g.transform.SetParent(parent,false);g.transform.position=position;g.transform.rotation=ViewCamera.transform.rotation;
            var t=g.AddComponent<TextMesh>();t.text=text;t.font=Config.Font;t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.85f,.94f,.96f);
            g.GetComponent<MeshRenderer>().sharedMaterial=Config.Font.material;return t;
        }
        private LineRenderer DrawLine(Vector3[] points,Color color,float width,Transform parent,string name)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);var line=g.AddComponent<LineRenderer>();line.sharedMaterial=Paint(color);line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=line.endWidth=width;line.numCornerVertices=3;line.numCapVertices=2;return line;
        }
        private static Vector3 LocalPoint(FactorySite f,GridPoint p)=>new Vector3((f.X+p.X)*1.2f,.65f+p.H*1.2f,(f.Z+p.Z)*1.2f);
        private void CreateEnvironment()
        {
            var env=new GameObject("World map");env.transform.SetParent(transform,false);environmentRoot=env.transform;
            Box("Terrain",new Vector3(69,-.4f,52),new Vector3(145,.5f,112),new Color(.07f,.13f,.16f),environmentRoot);
            for(var x=0;x<GridRouting.WorldWidth;x+=4)for(var z=0;z<GridRouting.WorldDepth;z+=4)
                if(GridRouting.WorldBlocked(new GridPoint(x,z)))Box("Rock ridge",new Vector3(x*1.2f,1,z*1.2f),new Vector3(4,2.5f,4),new Color(.22f,.29f,.29f),environmentRoot);
            foreach(var f in World.State.Factories)
            {
                Box("Factory plot "+f.Id,new Vector3((f.X+20)*1.2f,0,(f.Z+16)*1.2f),new Vector3(48,.2f,38.4f),new Color(.12f,.23f,.27f),environmentRoot);
                Label("工厂 "+f.Id,new Vector3((f.X+20)*1.2f,1,(f.Z+16)*1.2f),.6f,environmentRoot);
            }
            foreach(var r in World.State.Resources)
            {
                Box("Resource "+r.Id,new Vector3(r.X*1.2f,.6f,r.Z*1.2f),new Vector3(4,1.8f,4),MaterialColor(r.Material),environmentRoot);
                Label("资源信号 ?",new Vector3(r.X*1.2f,3,r.Z*1.2f),.45f,environmentRoot);
            }
            var vehicles=new GameObject("Vehicles");vehicles.transform.SetParent(transform,false);vehicleRoot=vehicles.transform;
            var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);cargoMesh=sample.GetComponent<MeshFilter>().sharedMesh;Destroy(sample);
        }
        private void RebuildFactoryViews()
        {
            if(environmentRoot==null)return;
            environmentRoot.gameObject.SetActive(MapMode);
            if(factoryRoot!=null)Destroy(factoryRoot.gameObject);
            var g=new GameObject("Factory presentation");g.transform.SetParent(transform,false);factoryRoot=g.transform;nodeLabels.Clear();routePaths.Clear();cargoMatrices.Clear();
            if(MapMode)
            {
                foreach(var f in World.State.Factories)Label(f.Owned?"工厂 "+f.Id+" · 运营中":f.Discovered?"可购买厂址 · "+Config.Economy.SecondFactoryCost:"未探索厂址 ?",new Vector3((f.X+20)*1.2f,3,(f.Z+16)*1.2f),.65f,factoryRoot);
                foreach(var r in World.State.Resources)
                {
                    Label(r.Discovered?EconomySettings.MaterialName(r.Material)+(r.Station?" · 采集站":""):"未知资源 ?",new Vector3(r.X*1.2f,4,r.Z*1.2f),.5f,factoryRoot);
                    if(r.Station)Box("Mining station",new Vector3(r.X*1.2f+3,1,r.Z*1.2f),new Vector3(2,2,3),orange,factoryRoot);
                }
                // Unexplored sectors remain dark; signals guide intentional exploration.
                for(var x=0;x<15;x++)for(var z=0;z<11;z++)
                    if(!World.State.Explored.Contains(new GridPoint(x,z))&&!(x<6&&z<5))Box("Unexplored",new Vector3((x*8+4)*1.2f,.2f,(z*8+4)*1.2f),new Vector3(9.4f,.08f,9.4f),new Color(.045f,.065f,.085f),factoryRoot);
            }
            else
            {
                var f=World.Factory(SelectedFactory);
                Box("Factory floor",new Vector3((f.X+20)*1.2f,-.2f,(f.Z+16)*1.2f),new Vector3(48,.3f,38.4f),new Color(.10f,.17f,.21f),factoryRoot);
                var lineColor=new Color(.16f,.26f,.3f);
                for(var x=0;x<=40;x++)DrawLine(new[]{new Vector3((f.X+x)*1.2f,0,f.Z*1.2f),new Vector3((f.X+x)*1.2f,0,(f.Z+32)*1.2f)},lineColor,.018f,factoryRoot,"Grid");
                for(var z=0;z<=32;z++)DrawLine(new[]{new Vector3(f.X*1.2f,0,(f.Z+z)*1.2f),new Vector3((f.X+40)*1.2f,0,(f.Z+z)*1.2f)},lineColor,.018f,factoryRoot,"Grid");
                foreach(var n in World.State.Nodes.Where(n=>n.Factory==SelectedFactory))
                {
                    var center=LocalPoint(f,n.Center);var color=n.Kind==BuildingKind.Warehouse?teal:n.Kind==BuildingKind.Sales?new Color(.4f,.66f,.35f):n.Kind==BuildingKind.Processor?new Color(.4f,.5f,.65f):orange;
                    Box("Node "+n.Id,center+Vector3.up*.3f,new Vector3(3.2f,1.8f,3.2f),color,factoryRoot);
                    Box("L"+n.Level,center+Vector3.up*1.4f,new Vector3(2.8f,.3f,2.8f),new Color(.13f,.2f,.24f),factoryRoot);
                    Box("Input",LocalPoint(f,n.Port(false)),Vector3.one*.5f,teal,factoryRoot);Box("Output",LocalPoint(f,n.Port(true)),Vector3.one*.5f,orange,factoryRoot);
                    nodeLabels[n.Id]=Label("",center+Vector3.up*3,.16f,factoryRoot);
                }
                foreach(var r in World.State.Routes.Where(r=>r.Factory==SelectedFactory))
                {
                    var points=FactoryRouteGeometry.RoundedPath(r.Points.Select(p=>LocalPoint(f,p)).ToArray());routePaths[r.Id]=points;
                    DrawLine(points,new Color(.33f,.40f,.43f),.48f,factoryRoot,"Belt "+r.Id);
                    DrawLine(points.Select(p=>p+Vector3.up*.04f).ToArray(),new Color(.63f,.69f,.60f),.07f,factoryRoot,"Belt centre");
                    for(var i=1;i<r.Points.Count;i++)if(r.Points[i].H!=r.Points[i-1].H)
                        Box("Lift",(LocalPoint(f,r.Points[i])+LocalPoint(f,r.Points[i-1]))*.5f,new Vector3(.6f,1.2f,.6f),new Color(.23f,.32f,.36f),factoryRoot);
                }
                Label("工厂 "+SelectedFactory+"   /   自动物流试验区",new Vector3((f.X+20)*1.2f,.2f,(f.Z+30)*1.2f),.28f,factoryRoot);
            }
            PresentDynamic();
        }
        private static Color MaterialColor(int material)=>material==8?new Color(.58f,.67f,.72f):material==9?new Color(.4f,.7f,.83f):material==10?new Color(.7f,.49f,.85f):material>=17?new Color(1,.68f,.24f):new Color(.4f,.85f,.61f);
        private void PresentDynamic()
        {
            if(factoryRoot==null)return;
            foreach(var pair in nodeLabels)
            {
                var n=World.Node(pair.Key);if(n==null)continue;pair.Value.text="#"+n.Id+" "+(Config.Economy.Recipe(n.RecipeId)?.Name??KindName(n.Kind))+" L"+n.Level+"\n"+n.Status;
                pair.Value.color=n.Id==SelectedNode?Color.yellow:n.Status.Contains("缺")||n.Status.Contains("堵")?new Color(1,.7f,.4f):Color.white;
            }
            foreach(var v in World.State.Vehicles)
            {
                if(!vehicleViews.TryGetValue(v.Id,out var root))
                {
                    var g=new GameObject("Vehicle "+v.Id);g.transform.SetParent(vehicleRoot,false);root=g.transform;vehicleViews[v.Id]=root;
                    Box("Chassis",Vector3.zero,new Vector3(1.3f,.6f,2.4f),v.Role==VehicleRole.Pickup?orange:teal,root);
                    Box("Cabin",new Vector3(0,.5f,.6f),new Vector3(1.2f,.7f,.9f),new Color(.2f,.4f,.48f),root);
                    foreach(var x in new[]{-.7f,.7f})foreach(var z in new[]{-.7f,.7f})Box("Wheel",new Vector3(x,-.25f,z),new Vector3(.25f,.55f,.5f),new Color(.06f,.08f,.09f),root);
                }
                root.gameObject.SetActive(MapMode);root.position=new Vector3(v.Position.X*1.2f,.7f,v.Position.Z*1.2f);
                if(v.PathIndex+1<v.Path.Count){var next=v.Path[v.PathIndex+1];var direction=new Vector3(next.X-v.Position.X,0,next.Z-v.Position.Z);if(direction.sqrMagnitude>0)root.rotation=Quaternion.LookRotation(direction);}
            }
            foreach(var id in vehicleViews.Keys.Where(id=>!World.State.Vehicles.Any(v=>v.Id==id)).ToArray()){Destroy(vehicleViews[id].gameObject);vehicleViews.Remove(id);}
            if(journeyView!=null)Destroy(journeyView.gameObject);
            var selected=World.State.Vehicles.FirstOrDefault(v=>v.Id==SelectedVehicle);
            if(MapMode&&selected!=null&&selected.Path.Count>1)journeyView=DrawLine(selected.Path.Skip(selected.PathIndex).Select(p=>new Vector3(p.X*1.2f,.8f,p.Z*1.2f)).ToArray(),orange,.2f,vehicleRoot,"Vehicle route");
            cargoMatrices.Clear();if(MapMode)return;
            var groups=new Dictionary<int,List<Matrix4x4>>();
            foreach(var r in World.State.Routes.Where(r=>r.Factory==SelectedFactory))
            {
                if(!routePaths.TryGetValue(r.Id,out var path))continue;r.Backend.CreateSnapshot(r.Snapshot);
                foreach(var item in r.Snapshot.Items)
                {
                    var position=Sample(path,item.Distance/(float)Math.Max(1,r.Length-512));
                    if(!groups.TryGetValue(r.Material,out var matrices))groups[r.Material]=matrices=new List<Matrix4x4>();
                    matrices.Add(Matrix4x4.TRS(position+Vector3.up*.25f,Quaternion.identity,new Vector3(.35f,.3f,.35f)));
                }
            }
            foreach(var group in groups)cargoMatrices[group.Key]=group.Value.ToArray();
        }
        private static Vector3 Sample(Vector3[] points,float ratio)
        {
            var length=0f;for(var i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);var distance=Mathf.Clamp01(ratio)*length;
            for(var i=1;i<points.Length;i++){var segment=Vector3.Distance(points[i-1],points[i]);if(distance<=segment)return Vector3.Lerp(points[i-1],points[i],segment<.001f?0:distance/segment);distance-=segment;}return points[points.Length-1];
        }
        private void LateUpdate()
        {
            if(cargoMesh==null)return;
            foreach(var group in cargoMatrices)
            {
                var parameters=new RenderParams(Paint(MaterialColor(group.Key))){shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off,receiveShadows=false};
                for(var offset=0;offset<group.Value.Length;offset+=1023)Graphics.RenderMeshInstanced(parameters,cargoMesh,0,group.Value,Math.Min(1023,group.Value.Length-offset),offset);
            }
        }
        private void OnDestroy(){foreach(var material in paints.Values)if(material!=null)Destroy(material);}
    }
}
