using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Pure presentation: transforms never own goods, progress, or cash.
    public sealed partial class HardwareFactoryView : MonoBehaviour
    {
        private HardwareGame game; private Transform root,environment,truck,ghost;
        private readonly Dictionary<Color,Material> colors=new Dictionary<Color,Material>();
        private readonly Dictionary<int,TMP_Text> labels=new Dictionary<int,TMP_Text>();
        private readonly Dictionary<int,Transform> lamps=new Dictionary<int,Transform>();
        private readonly Dictionary<int,HardwareConveyorPath> paths=new Dictionary<int,HardwareConveyorPath>();
        private readonly Dictionary<int,Transform> cargo=new Dictionary<int,Transform>();
        private readonly Dictionary<int,(int Route,float Distance)> previousCargo=new Dictionary<int,(int Route,float Distance)>();
        private readonly Dictionary<int,Transform> workpieces=new Dictionary<int,Transform>();
        private float labelTimer;
        private readonly Color steel=new Color(.54f,.63f,.68f),dark=new Color(.075f,.11f,.15f),amber=new Color(.95f,.61f,.18f),green=new Color(.22f,.65f,.4f);
        private WorldSimulation builtWorld;
        private int builtFactory, geometryKey;
        private readonly HashSet<int> alive=new HashSet<int>();
        private readonly List<int> removed=new List<int>();
        private readonly Dictionary<int,Stack<Transform>> cargoPool=new Dictionary<int,Stack<Transform>>();
        private readonly Dictionary<int,int> cargoMaterials=new Dictionary<int,int>();
        private readonly Dictionary<int,Transform> inputPieces=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Transform> actions=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Vector3> actionOrigins=new Dictionary<int,Vector3>();
        private readonly Dictionary<int,Transform> utilizationBars=new Dictionary<int,Transform>();
        private readonly Dictionary<int,MeshRenderer> beds=new Dictionary<int,MeshRenderer>();
        private static readonly Color red=new Color(.9f,.24f,.2f),yellow=new Color(.96f,.78f,.24f),violet=new Color(.62f,.42f,.88f),grey=new Color(.5f,.55f,.6f);
        private static readonly Color jammed=new Color(.55f,.16f,.12f),busy=new Color(.42f,.27f,.08f);
        public const float ModelHeight=1.45f,ChipZoom=22;
        private readonly Dictionary<int,TMP_Text> chips=new Dictionary<int,TMP_Text>();
        private readonly Dictionary<int,Transform> stations=new Dictionary<int,Transform>();
        private readonly Dictionary<int,int> lastOutput=new Dictionary<int,int>();
        private readonly Dictionary<Color,Material> glows=new Dictionary<Color,Material>();
        private int pendingPop;
        public float GlowBoost=1.4f;
        public bool LabelVisible(int node)=>labels.TryGetValue(node,out var l)&&l.gameObject.activeSelf;
        public bool ChipVisible(int node)=>chips.TryGetValue(node,out var c)&&c.gameObject.activeSelf;
        public Vector3 StationPosition(int node)=>stations.TryGetValue(node,out var s)&&s!=null?s.position:Vector3.zero;
        public void MarkBuilt(int node)=>pendingPop=node;
        // Emissive variant used by lamps, LEDs and screens so they bloom at night.
        private Material Glow(Color c)
        {
            if(!glows.TryGetValue(c,out var m)){m=new Material(Paint(c));m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*GlowBoost);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;glows.Add(c,m);}
            return m;
        }
        public void SetGlow(float boost){GlowBoost=boost;foreach(var p in glows)p.Value.SetColor("_EmissionColor",p.Key*boost);}
        private void Dust(Vector3 at)
        {
            for(var i=0;i<12;i++)
            {
                var puff=Cube("Build dust",at+Vector3.down*.3f,Vector3.one*.35f,new Color(.78f,.82f,.84f),transform);var dir=Quaternion.Euler(0,i*30,0)*Vector3.forward;
                HardwareTween.Run(puff,.6f,p=>{if(puff==null)return;puff.position=at+Vector3.down*.3f+dir*(2.6f*p)+Vector3.up*Mathf.Sin(p*Mathf.PI)*.6f;puff.localScale=Vector3.one*.35f*(1-p);},HardwareTween.OutQuad,()=>{if(puff!=null)Destroy(puff.gameObject);});
            }
        }
        private static readonly Vector3 LampScale=new Vector3(.4f,.6f,.4f);
        public bool Night {get;private set;}
        private readonly List<Light> nightLights=new List<Light>();
        private static readonly string[] NightGlowParts={"Rear window band","Rear cyan guide","Supplier facade stripe","Headlight","Scan strip","Dock marking"};
        // Night key-art lighting: stronger emission, glowing architecture and pooled point lights over machines.
        public void SetNight(bool night)
        {
            Night=night;SetGlow(night?3.4f:1.4f);
            foreach(var r in transform.GetComponentsInChildren<MeshRenderer>(true))
                if(System.Array.IndexOf(NightGlowParts,r.name)>=0){var c=r.sharedMaterial.color;r.sharedMaterial=night?Glow(c):Paint(c);}
            foreach(var l in nightLights)if(l!=null)Destroy(l.gameObject);nightLights.Clear();
            if(!night)return;
            var index=0;foreach(var pair in stations)
            {
                if(pair.Value==null||index++>=16)continue;var n=game.World.Node(pair.Key);
                var light=new GameObject("Night machine light").AddComponent<Light>();light.transform.SetParent(pair.Value,false);light.transform.localPosition=Vector3.up*5.5f;
                light.type=LightType.Point;light.range=9;light.intensity=6;light.shadows=LightShadows.None;
                light.color=n!=null&&(n.Kind==BuildingKind.Packaging||n.Kind==BuildingKind.Sales||n.Kind==BuildingKind.Manufacturer)?new Color(1,.72f,.42f):new Color(.45f,.85f,1);nightLights.Add(light);
            }
        }
        // Expanding coloured ring on the floor around a station (rejects, deliveries).
        private void Flash(int node,Color color,float from=3.4f,float to=5.2f)
        {
            if(!stations.TryGetValue(node,out var s)||s==null)return;
            var ring=Cube("Station flash",s.position+Vector3.down*.4f,new Vector3(from,.05f,from),color,transform);ring.GetComponent<Renderer>().sharedMaterial=Glow(color);
            HardwareTween.Run(ring,.55f,p=>{if(ring!=null)ring.localScale=new Vector3(Mathf.Lerp(from,to,p),.05f*(1-p)+.005f,Mathf.Lerp(from,to,p));},HardwareTween.OutQuad,()=>{if(ring!=null)Destroy(ring.gameObject);});
        }
        // Sales dock celebration when a contract is paid.
        public void CelebrateDelivery(int factory)
        {
            if(factory!=game.Factory)return;var sales=game.World.State.Nodes.FirstOrDefault(n=>n.Factory==factory&&n.Kind==BuildingKind.Sales);if(sales==null||!stations.TryGetValue(sales.Id,out var s))return;
            HardwareTween.Punch(s,.12f,.35f);Flash(sales.Id,new Color(.95f,.7f,.2f),3.6f,7);
            for(var i=0;i<14;i++)
            {
                var c=Cube("Confetti",s.position+Vector3.up*2.5f,Vector3.one*.18f,i%3==0?amber:i%3==1?green:cyan,transform);var dir=Quaternion.Euler(0,i*(360f/14),0)*Vector3.forward*Random.Range(1.5f,3f);var spin=Random.Range(180f,720f);var start=c.position;
                HardwareTween.Run(c,1.1f,p=>{if(c==null)return;c.position=start+dir*p+Vector3.up*(3.5f*p-4.5f*p*p);c.rotation=Quaternion.Euler(spin*p,spin*.6f*p,0);},HardwareTween.Linear,()=>{if(c!=null)Destroy(c.gameObject);});
            }
        }
        private void Hop(Transform t,float height=.35f)
        {
            if(t==null)return;var home=t.localPosition;HardwareTween.Cancel(t);
            HardwareTween.Run(t,.3f,p=>t.localPosition=home+Vector3.up*height*HardwareTween.Pulse(p),HardwareTween.Linear,()=>t.localPosition=home);
        }
        public Transform TruckVisual=>truck;
        public void Initialize(HardwareGame g)
        {
            game=g;BuildEnvironment();BuildTruck();Rebuild();
        }
        private Material Paint(Color c){if(!colors.TryGetValue(c,out var m)){m=new Material(game.Config.BaseMaterial);m.color=c;m.SetFloat("_Metallic",c==steel?.72f:.12f);m.SetFloat("_Smoothness",.42f);m.enableInstancing=true;colors.Add(c,m);}return m;}
        private Transform Cube(string name,Vector3 position,Vector3 scale,Color color,Transform parent)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=Paint(color);Destroy(o.GetComponent<Collider>());return o.transform;
        }
        private TMP_Text Label(string text,Vector3 p,Transform parent,float size=.6f)
        {
            var o=new GameObject("Label",typeof(TextMeshPro));o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.rotation=Quaternion.LookRotation(-new Vector3(26,42,-29));
            o.AddComponent<HardwareBillboard>().View=game.ViewCamera;
            var t=o.GetComponent<TextMeshPro>();t.font=game.Font;t.text=text;t.fontSize=14;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(18,4);t.transform.localScale=Vector3.one*size;t.color=new Color(.13f,.22f,.28f);return t;
        }
        private Vector3 Local(FactorySite f,GridPoint p)=>new Vector3((f.X+p.X)*1.2f,.6f+p.H*1.2f,(f.Z+p.Z)*1.2f);
        public void Rebuild()
        {
            var worldChanged=builtWorld!=game.World;
            if(worldChanged){if(builtWorld!=null)BuildEnvironment();builtWorld=game.World;truck.rotation=Quaternion.identity;truck.position=SampleTruck(game.Truck,0);truckLoad.gameObject.SetActive(game.Truck.Cargo.Count>0);}
            var key=17;unchecked{foreach(var n in game.World.State.Nodes)if(n.Factory==game.Factory){key=key*31+n.Id;key=key*31+n.X;key=key*31+n.Z;key=key*31+n.Rotation;key=key*31+n.Level;key=key*31+(n.RecipeId?.GetHashCode()??0);}
                foreach(var r in game.World.State.Routes)if(r.Factory==game.Factory){key=key*31+r.Id;key=key*31+r.Material;key=key*31+r.Length;}}
            if(!worldChanged&&builtFactory==game.Factory&&geometryKey==key&&root!=null)return;
            builtFactory=game.Factory;geometryKey=key;ClearCargo();ClearRouteMeshes();
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}root=new GameObject("Factory machinery").transform;root.SetParent(transform);labels.Clear();lamps.Clear();paths.Clear();workpieces.Clear();inputPieces.Clear();actions.Clear();actionOrigins.Clear();utilizationBars.Clear();beds.Clear();chips.Clear();stations.Clear();lastOutput.Clear();
            var f=game.World.Factory(game.Factory);
            foreach(var n in game.World.State.Nodes.Where(n=>n.Factory==game.Factory))
            {
                var pos=Local(f,n.Center);var station=new GameObject("Station "+n.Id).transform;station.SetParent(root);station.position=pos;station.rotation=Quaternion.Euler(0,-n.Rotation*90,0);stations[n.Id]=station;
                Cube("Hazard footprint",new Vector3(0,-.45f,0),new Vector3(3.8f,.08f,3.8f),amber,station);Cube("Base",Vector3.zero,new Vector3(3.2f,.8f,3.2f),dark,station).GetComponent<MeshFilter>().sharedMesh=BevelMesh();
                // Footprint and ports are fixed by the grid; machines grow upward for a bolder silhouette.
                var model=new GameObject("Model").transform;model.SetParent(station,false);model.localScale=new Vector3(1,ModelHeight,1);
                BuildBlocks(n,model);DetailStation(n,model);
                if(n.Kind!=BuildingKind.Warehouse&&n.Kind!=BuildingKind.Sales)
                {
                    var recipe=game.Config.Economy.Recipe(n.RecipeId);
                    workpieces[n.Id]=Product(recipe?.Output??HardwareCatalog.Board,station);workpieces[n.Id].localPosition=new Vector3(0,1.18f+.975f*(ModelHeight-1),-.7f);workpieces[n.Id].localScale=Vector3.one*1.25f;
                    inputPieces[n.Id]=Product(recipe!=null&&recipe.Inputs.Length>0?recipe.Inputs[0].Material:HardwareCatalog.Board,station);inputPieces[n.Id].localPosition=workpieces[n.Id].localPosition;inputPieces[n.Id].localScale=Vector3.one*1.25f;
                    workpieces[n.Id].gameObject.SetActive(n.Output.Count>0);inputPieces[n.Id].gameObject.SetActive(n.Batch.Count>0&&n.Output.Count==0);
                }
                lamps[n.Id]=Cube("Status light",new Vector3(1.4f,2.5f*ModelHeight,1.3f),LampScale,StateColor(n.RunState),station);lamps[n.Id].GetComponent<Renderer>().sharedMaterial=Glow(StateColor(n.RunState));
                Cube("Input dock",new Vector3(-2.4f,0,0),new Vector3(.5f,.4f,.65f),green,station);Cube("Output dock",new Vector3(2.4f,0,0),new Vector3(.5f,.4f,.65f),amber,station);
                var top=pos+Vector3.up*(3.2f*ModelHeight);
                if(game.Config.Economy.Recipe(n.RecipeId)!=null)
                {
                    // Utilization gauge above the machine; a full red bar marks the bottleneck.
                    var gauge=new GameObject("Utilization gauge").transform;gauge.SetParent(root,false);gauge.position=top;
                    Cube("Utilization track",Vector3.zero,new Vector3(2.6f,.14f,.3f),dark,gauge);
                    utilizationBars[n.Id]=Cube("Utilization fill",new Vector3(0,.04f,0),new Vector3(.05f,.16f,.32f),green,gauge);
                    chips[n.Id]=Label("",top+Vector3.up*.6f,root,.5f);chips[n.Id].fontStyle=FontStyles.Bold;
                }
                labels[n.Id]=Label(game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name,top+Vector3.up*1.35f,root,.6f);labels[n.Id].gameObject.SetActive(false);
                if(n.Id==pendingPop){pendingPop=0;HardwareTween.PopIn(station,.45f);Dust(pos);}
                lastOutput[n.Id]=n.Output.Count;
            }
            if(Night)SetNight(true);
            var factory=game.World.Factory(game.Factory);
            if(game.Config.Economy.Hardware.Slots!=null&&!game.Showcasing)foreach(var slot in game.Config.Economy.Hardware.Slots)
            {
                if(game.World.State.Nodes.Any(n=>n.Factory==game.Factory&&n.Kind==slot.Kind)||game.World.PlacementError(game.Factory,slot.X,slot.Z)!=null)continue;
                var center=Local(factory,new GridPoint(slot.X,slot.Z))-Vector3.up*.52f;var frame=new GameObject("Suggested slot "+slot.Kind).transform;frame.SetParent(root,false);frame.position=center;
                foreach(var side in new[]{-1.8f,1.8f}){Cube("Slot edge",new Vector3(side,0,0),new Vector3(.12f,.05f,3.72f),cyan,frame);Cube("Slot edge",new Vector3(0,0,side),new Vector3(3.72f,.05f,.12f),cyan,frame);}
                Cube("Slot arrow",new Vector3(1.2f,0,0),new Vector3(.9f,.05f,.18f),cyan,frame);
                Label("推荐位 · "+game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==slot.Kind).Name,center+Vector3.up*1.4f,frame,.5f).color=new Color(.02f,.48f,.57f);
            }
            foreach(var r in game.World.State.Routes.Where(r=>r.Factory==game.Factory))
            {
                var raw=r.Points.Select(p=>Local(f,p)).ToArray();var path=new HardwareConveyorPath(raw);paths[r.Id]=path;
                BuildConveyorRoute(r,path.Points,raw);
            }
        }
        // Station silhouettes keep their original footprints and ports.
        private void BuildBlocks(BusinessNode n,Transform station)
        {
            var ivory=new Color(.79f,.85f,.88f);var blue=new Color(.12f,.4f,.56f);var teal=new Color(.12f,.52f,.57f);
            var coral=new Color(.5f,.26f,.19f);var violet=new Color(.3f,.39f,.56f);
            var color=n.Kind==BuildingKind.Warehouse?teal:n.Kind==BuildingKind.Processor?coral:n.Kind==BuildingKind.Manufacturer?amber:n.Kind==BuildingKind.BoardAssembly?green:n.Kind==BuildingKind.GPUAssembly?blue:n.Kind==BuildingKind.Tester?violet:n.Kind==BuildingKind.Packaging?new Color(.94f,.75f,.2f):n.Kind==BuildingKind.Rework?coral:green;
            Cube("Brick deck",new Vector3(0,.48f,0),new Vector3(3.1f,.24f,3.1f),color,station);
            for(var i=0;i<n.Level;i++)Cube("Level brick",new Vector3(-1+i*.45f,.7f,-1.4f),new Vector3(.3f,.18f,.2f),ivory,station);
            switch(n.Kind)
            {
                case BuildingKind.Warehouse:
                    foreach(var x in new[]{-1.05f,1.05f})
                    {
                        foreach(var z in new[]{-.9f,.9f})Cube("Rack upright",new Vector3(x,1.45f,z),new Vector3(.25f,1.8f,.25f),teal,station);
                        for(var y=0;y<2;y++){Cube("Rack shelf",new Vector3(x,.75f+y*.85f,0),new Vector3(.9f,.18f,2.5f),ivory,station);for(var z=0;z<3;z++)Cube("Stock brick",new Vector3(x,1+y*.85f,(z-1)*.7f),new Vector3(.65f,.4f,.55f),z%2==0?amber:blue,station);}
                    }
                    break;
                case BuildingKind.Processor:
                    Cube("Smelter body",new Vector3(0,1.3f,.35f),new Vector3(2.6f,1.4f,1.7f),coral,station);
                    Cube("Furnace mouth",new Vector3(0,1.15f,-.55f),new Vector3(1.3f,.7f,.12f),dark,station);
                    Cube("Hot billet",new Vector3(0,.95f,-.65f),new Vector3(.85f,.2f,.25f),amber,station);
                    Cube("Stack",new Vector3(.8f,2.4f,.7f),new Vector3(.55f,1,.6f),ivory,station);
                    Cube("Stack cap",new Vector3(.8f,2.95f,.7f),new Vector3(.8f,.2f,.85f),dark,station);
                    break;
                case BuildingKind.Manufacturer:
                    foreach(var x in new[]{-1.1f,1.1f})Cube("Press pillar",new Vector3(x,1.35f,.2f),new Vector3(.5f,1.5f,1.5f),amber,station);
                    Cube("Press crown",new Vector3(0,2.15f,.2f),new Vector3(2.9f,.4f,1.6f),amber,station);
                    Cube("Press die",new Vector3(0,1.65f,0),new Vector3(1.15f,.45f,.8f),ivory,station);
                    for(var i=0;i<5;i++)Cube("Cooling fin",new Vector3((i-2)*.3f,1,-.8f),new Vector3(.13f,.65f,.65f),steel,station);
                    break;
                case BuildingKind.BoardAssembly:
                    Cube("PCB workbench",new Vector3(0,.85f,0),new Vector3(2.9f,.3f,2.5f),ivory,station);
                    Cube("Large green board",new Vector3(0,1.05f,0),new Vector3(1.9f,.12f,1.6f),green,station);
                    for(var i=0;i<4;i++)Cube("Chip feeder",new Vector3((i-1.5f)*.6f,1.5f,1),new Vector3(.4f,.9f,.6f),i%2==0?blue:dark,station);
                    Cube("Placement rail",new Vector3(0,2.1f,.1f),new Vector3(2.8f,.25f,.35f),green,station);
                    Cube("Pick head",new Vector3(.2f,1.7f,.1f),new Vector3(.35f,.6f,.35f),amber,station);
                    break;
                case BuildingKind.GPUAssembly:
                    Cube("Assembly bench",new Vector3(0,.8f,0),new Vector3(2.8f,.25f,2.5f),ivory,station);
                    foreach(var x in new[]{-1.05f,1.05f})
                    {
                        Cube("Robot pedestal",new Vector3(x,1.25f,.55f),new Vector3(.6f,1.1f,.7f),blue,station);
                        Cube("Robot shoulder",new Vector3(x*.65f,1.9f,.55f),new Vector3(1.05f,.4f,.5f),blue,station);
                        Cube("Robot tool",new Vector3(x*.3f,1.6f,.55f),new Vector3(.3f,.55f,.3f),amber,station);
                    }
                    break;
                case BuildingKind.Tester:
                    foreach(var x in new[]{-1.15f,1.15f})Cube("Scan tower",new Vector3(x,1.55f,0),new Vector3(.5f,1.9f,1.8f),violet,station);
                    Cube("Scan bridge",new Vector3(0,2.55f,0),new Vector3(2.9f,.4f,1.8f),violet,station);
                    Cube("Scan strip",new Vector3(0,2.3f,-.7f),new Vector3(1.8f,.15f,.2f),new Color(.3f,.95f,.95f),station);
                    Cube("Test screen",new Vector3(1.25f,1.45f,-1),new Vector3(.5f,.6f,.18f),green,station);
                    break;
                case BuildingKind.Packaging:
                    Cube("Packing table",new Vector3(0,.85f,0),new Vector3(2.9f,.25f,2.5f),ivory,station);
                    foreach(var x in new[]{-1.1f,1.1f})Cube("Sealer support",new Vector3(x,1.45f,.5f),new Vector3(.4f,1.3f,.6f),color,station);
                    Cube("Sealing bar",new Vector3(0,2.05f,.5f),new Vector3(2.6f,.3f,.6f),color,station);
                    Cube("Tape cassette",new Vector3(0,2.4f,.5f),new Vector3(.7f,.5f,.6f),blue,station);
                    for(var i=0;i<2;i++)Cube("Empty carton",new Vector3(-.9f,1.1f+i*.45f,-.8f),new Vector3(.65f,.4f,.7f),amber,station);
                    break;
                case BuildingKind.Rework:
                    Cube("Rework bench",new Vector3(0,.85f,0),new Vector3(2.9f,.25f,2.5f),ivory,station);
                    Cube("Reject bin",new Vector3(-.9f,1.2f,-.7f),new Vector3(.8f,.5f,.7f),new Color(.86f,.22f,.18f),station);
                    Cube("Microscope column",new Vector3(.6f,1.6f,.6f),new Vector3(.25f,1.3f,.25f),steel,station);
                    Cube("Microscope head",new Vector3(.6f,2.2f,.25f),new Vector3(.45f,.3f,.7f),dark,station);
                    Cube("Solder iron",new Vector3(.2f,1.4f,-.2f),new Vector3(.12f,.6f,.12f),amber,station);
                    break;
                case BuildingKind.Sales:
                    foreach(var x in new[]{-1.2f,1.2f})Cube("Dispatch post",new Vector3(x,1.5f,.7f),new Vector3(.25f,1.8f,.25f),green,station);
                    Cube("Dispatch canopy",new Vector3(0,2.4f,.3f),new Vector3(3.1f,.3f,2.3f),green,station);
                    Cube("Dispatch counter",new Vector3(0,.95f,-.5f),new Vector3(2.7f,.65f,1.2f),ivory,station);
                    for(var i=0;i<3;i++)Cube("Delivery carton",new Vector3((i-1)*.8f,1.5f,-.5f),new Vector3(.6f,.45f,.6f),amber,station);
                    break;
            }
            for(var i=0;i<4;i++)Cube("Square stud",new Vector3((i%2-.5f)*.65f,n.Kind==BuildingKind.Tester?2.82f:n.Kind==BuildingKind.Sales?2.62f:.66f,.2f+i/2*.6f),new Vector3(.3f,.12f,.3f),color,station);
        }
        private Transform Product(int material,Transform parent)=>BuildProduct(material,parent);
        public void BeforeTick()
        {
            // Capture authoritative pre-tick positions, including multiple ticks in one frame.
            foreach(var r in game.World.State.Routes)
            {
                if(r.Factory!=game.Factory||!paths.TryGetValue(r.Id,out var path))continue;
                r.Backend.CreateSnapshot(r.Snapshot);
                foreach(var item in r.Snapshot.Items)previousCargo[item.ItemId.Value]=(r.Id,item.Distance);
            }
        }
        private void ClearCargo()
        {
            foreach(var pair in cargo)ReleaseCargo(pair.Key,pair.Value);
            cargo.Clear();cargoMaterials.Clear();previousCargo.Clear();
        }
        private void ReleaseCargo(int id,Transform view)
        {
            view.gameObject.SetActive(false);var material=cargoMaterials[id];
            if(!cargoPool.TryGetValue(material,out var pool)){pool=new Stack<Transform>();cargoPool.Add(material,pool);}pool.Push(view);
        }
        public static Vector3 SampleTruck(WorldVehicle v,float interpolation)
        {
            var p=new Vector3(v.Position.X*1.2f,.65f,v.Position.Z*1.2f);
            if(v.RescueTicks==0&&v.Phase==VehiclePhase.Travelling&&v.PathIndex+1<v.Path.Count)
            {
                var next=v.Path[v.PathIndex+1];var t=Mathf.Clamp01((v.SegmentTicks+interpolation)/(v.Boost?5f:10f));
                return Vector3.Lerp(p,new Vector3(next.X*1.2f,.65f,next.Z*1.2f),t);
            }
            return p;
        }
        public void Present(float interpolation)
        {
            var w=game.World;var v=game.Truck;truck.position=SampleTruck(v,interpolation);
            if(!game.Paused&&v.RescueTicks==0&&v.Phase==VehiclePhase.Travelling&&v.PathIndex+1<v.Path.Count)
            {
                var next=v.Path[v.PathIndex+1];var direction=new Vector3(next.X-v.Position.X,0,next.Z-v.Position.Z);
                if(direction.sqrMagnitude>0)truck.rotation=Quaternion.RotateTowards(truck.rotation,Quaternion.LookRotation(direction),720*Time.unscaledDeltaTime*game.Speed);
            }
            alive.Clear();
            foreach(var r in w.State.Routes)
            {
                if(r.Factory!=game.Factory||!paths.TryGetValue(r.Id,out var path))continue;r.Backend.CreateSnapshot(r.Snapshot);
                foreach(var item in r.Snapshot.Items)
                {
                    var id=item.ItemId.Value;alive.Add(id);
                    if(!cargo.TryGetValue(id,out var view))
                    {
                        view=cargoPool.TryGetValue(r.Material,out var pool)&&pool.Count>0?pool.Pop():Product(r.Material,transform);
                        // Keep the largest product inside the existing 512-unit cargo pitch,
                        // including the shorter visual length of a rounded centreline.
                        var scale=Mathf.Min(.85f,path.Length*512/Mathf.Max(1,r.Length-512)*.85f/.94f);view.localScale=Vector3.one*scale;
                        view.gameObject.SetActive(true);cargo.Add(id,view);cargoMaterials.Add(id,r.Material);if(!previousCargo.ContainsKey(id))previousCargo[id]=(r.Id,item.Distance);
                    }
                    var distance=previousCargo.TryGetValue(id,out var previous)&&previous.Route==r.Id?Mathf.Lerp(previous.Distance,item.Distance,Mathf.Clamp01(interpolation)):item.Distance;
                    view.position=path.Sample(distance/Mathf.Max(1,r.Length-512),out var heading)+Vector3.up*(CargoHeight(r.Material)*view.localScale.x+.01f);
                    view.rotation=Quaternion.LookRotation(heading)*Quaternion.Euler(0,-90,0);
                }
            }
            removed.Clear();foreach(var id in cargo.Keys)if(!alive.Contains(id))removed.Add(id);
            foreach(var id in removed){ReleaseCargo(id,cargo[id]);cargo.Remove(id);cargoMaterials.Remove(id);previousCargo.Remove(id);}
            removed.Clear();foreach(var id in previousCargo.Keys)if(!alive.Contains(id))removed.Add(id);foreach(var id in removed)previousCargo.Remove(id);
            var phase=(w.State.Tick+interpolation)/60f;
            foreach(var pair in actions)
            {
                var n=w.Node(pair.Key);if(n==null)continue;var origin=actionOrigins[pair.Key];
                var progress=n.WorkTotal>0?1-n.WorkRemaining/(float)n.WorkTotal:0;
                pair.Value.localPosition=origin+(n.Batch.Count>0?(n.Kind==BuildingKind.BoardAssembly?Vector3.right*Mathf.Sin(progress*24)*.7f:Vector3.up*Mathf.Sin(progress*30)*.13f):Vector3.zero);
                if(n.Kind==BuildingKind.Tester)pair.Value.localPosition=origin+(n.Batch.Count>0?Vector3.forward*Mathf.Sin(phase*2)*.55f:Vector3.zero);
            }
            labelTimer+=Time.unscaledDeltaTime;if(labelTimer<.2f)return;labelTimer=0;
            truckLoad.gameObject.SetActive(v.Cargo.Count>0);
            var resource=w.State.Resources[0];oreLabel.text="北岭铝矿 / 自动采集\n储量 "+resource.Stock+" / "+resource.Capacity;
            var visibleRocks=resource.Capacity>0?Mathf.CeilToInt(oreRocks.Count*resource.Stock/(float)resource.Capacity):0;
            for(var i=0;i<oreRocks.Count;i++)oreRocks[i].gameObject.SetActive(i<visibleRocks);
            var bottleneck=w.Bottleneck(game.Factory)?.Id??0;var zoom=game.ViewCamera.orthographicSize;
            foreach(var pair in labels)
            {
                var n=w.Node(pair.Key);if(n==null)continue;var selected=game.SelectedNode==n.Id;var hovered=game.HoverNode==n.Id;var production=utilizationBars.TryGetValue(n.Id,out var bar);
                // Names appear only on demand so they never sit on top of the models.
                pair.Value.gameObject.SetActive(hovered||selected||n.Id==bottleneck);
                var name=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name;var utilization=production?w.Utilization(n):0;
                pair.Value.text=(n.Id==bottleneck?"<color=#D93A2B>【瓶颈】</color>":"")+name;
                pair.Value.color=selected||hovered?new Color(.02f,.48f,.57f):new Color(.13f,.22f,.28f);
                if(chips.TryGetValue(n.Id,out var chip))
                {
                    chip.gameObject.SetActive(zoom<=ChipZoom);chip.text=utilization+"%";
                    chip.color=n.Id==bottleneck?red:n.RunState==NodeState.Working?new Color(.12f,.5f,.3f):n.RunState==NodeState.Blocked?red:n.RunState==NodeState.Starved?new Color(.7f,.5f,.05f):new Color(.35f,.4f,.45f);
                }
                lamps[n.Id].GetComponent<Renderer>().sharedMaterial=Glow(StateColor(n.RunState));
                if(lastOutput.TryGetValue(n.Id,out var before)&&n.Output.Count>before&&production)
                {
                    var reject=n.Output.Skip(before).Any(g=>g.Material==HardwareCatalog.Defect);
                    HardwareTween.Punch(lamps[n.Id],.6f,.3f,LampScale);if(workpieces.TryGetValue(n.Id,out var made))Hop(made);
                    if(reject){HardwareAudio.Play("defect",.7f);Flash(n.Id,red);}else HardwareAudio.Play("product",n.Kind==BuildingKind.Packaging?.7f:.35f,n.Kind==BuildingKind.Packaging?1.2f:1);
                }
                lastOutput[n.Id]=n.Output.Count;
                if(production)
                {
                    var u=Mathf.Clamp01(utilization/100f);bar.localScale=new Vector3(Mathf.Max(.05f,2.5f*u),.16f,.32f);bar.localPosition=new Vector3(-1.25f+1.25f*u,.04f,0);
                    bar.GetComponent<Renderer>().sharedMaterial=Paint(n.Id==bottleneck?red:u>=.5f?green:yellow);
                }
                if(workpieces.TryGetValue(n.Id,out var piece))piece.gameObject.SetActive(n.Output.Count>0);
                if(inputPieces.TryGetValue(n.Id,out var input))input.gameObject.SetActive(n.Batch.Count>0&&n.Output.Count==0);
            }
            // Backed-up belts turn amber then red so queues read at a glance.
            foreach(var r in w.State.Routes)
                if(beds.TryGetValue(r.Id,out var bed)){var load=w.RouteLoad(r);bed.sharedMaterial=Paint(load>=.85f?jammed:load>=.6f?busy:dark);}
        }
        private Color StateColor(NodeState s)=>s==NodeState.Working?green:s==NodeState.Starved?yellow:s==NodeState.Blocked?red:s==NodeState.NoFunds?violet:s==NodeState.Disconnected?grey:amber;
        private static float CargoHeight(int material)
        {
            var style=HardwareVisualCatalog.Get(material);
            return style.Form==HardwareProductForm.Ore?.2f:style.Form==HardwareProductForm.Shell?.1f:style.Form==HardwareProductForm.Billet||style.Form==HardwareProductForm.Carton||style.Form==HardwareProductForm.Cooler?.08f:.035f;
        }
        public void Ghost(int x,int z,int rotation,bool valid){if(ghost==null)ghost=Cube("Placement footprint",Vector3.zero,new Vector3(3.6f,.15f,3.6f),green,transform);ghost.gameObject.SetActive(true);ghost.position=Local(game.World.Factory(game.Factory),new GridPoint(x,z));ghost.rotation=Quaternion.Euler(0,-rotation*90,0);ghost.GetComponent<Renderer>().sharedMaterial=Paint(valid?green:Color.red);}
        public void HideGhost(){if(ghost!=null)ghost.gameObject.SetActive(false);}
        private void OnDestroy(){ClearRouteMeshes();foreach(var m in colors.Values)Destroy(m);foreach(var m in glows.Values)Destroy(m);foreach(var mesh in ownedMeshes)Destroy(mesh);}
    }
}
