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
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}root=new GameObject("Factory machinery").transform;root.SetParent(transform);labels.Clear();lamps.Clear();paths.Clear();workpieces.Clear();inputPieces.Clear();actions.Clear();actionOrigins.Clear();
            var f=game.World.Factory(game.Factory);
            foreach(var n in game.World.State.Nodes.Where(n=>n.Factory==game.Factory))
            {
                var pos=Local(f,n.Center);var station=new GameObject("Station "+n.Id).transform;station.SetParent(root);station.position=pos;station.rotation=Quaternion.Euler(0,-n.Rotation*90,0);
                Cube("Hazard footprint",new Vector3(0,-.45f,0),new Vector3(3.8f,.08f,3.8f),amber,station);Cube("Base",Vector3.zero,new Vector3(3.2f,.8f,3.2f),dark,station);
                BuildBlocks(n,station);DetailStation(n,station);
                if(n.Kind!=BuildingKind.Warehouse&&n.Kind!=BuildingKind.Sales)
                {
                    var recipe=game.Config.Economy.Recipe(n.RecipeId);
                    workpieces[n.Id]=Product(recipe?.Output??HardwareCatalog.Board,station);workpieces[n.Id].localPosition=new Vector3(0,1.18f,-.7f);
                    inputPieces[n.Id]=Product(recipe!=null&&recipe.Inputs.Length>0?recipe.Inputs[0].Material:HardwareCatalog.Board,station);inputPieces[n.Id].localPosition=workpieces[n.Id].localPosition;
                    workpieces[n.Id].gameObject.SetActive(n.Output.Count>0);inputPieces[n.Id].gameObject.SetActive(n.Batch.Count>0&&n.Output.Count==0);
                }
                lamps[n.Id]=Cube("Status light",new Vector3(1.4f,2.3f,1.3f),new Vector3(.2f,.35f,.2f),n.Batch.Count>0?green:amber,station);
                Cube("Input dock",new Vector3(-2.4f,0,0),new Vector3(.5f,.4f,.65f),green,station);Cube("Output dock",new Vector3(2.4f,0,0),new Vector3(.5f,.4f,.65f),amber,station);
                labels[n.Id]=Label(game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name,pos+Vector3.up*3.8f,root,.6f);
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
            var color=n.Kind==BuildingKind.Warehouse?teal:n.Kind==BuildingKind.Processor?coral:n.Kind==BuildingKind.Manufacturer?amber:n.Kind==BuildingKind.BoardAssembly?green:n.Kind==BuildingKind.GPUAssembly?blue:n.Kind==BuildingKind.Tester?violet:n.Kind==BuildingKind.Packaging?new Color(.94f,.75f,.2f):green;
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
                        var scale=Mathf.Min(.58f,path.Length*512/Mathf.Max(1,r.Length-512)*.85f/.94f);view.localScale=Vector3.one*scale;
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
            foreach(var pair in labels)
            {
                var n=w.Node(pair.Key);if(n==null)continue;var selected=game.SelectedNode==n.Id;
                pair.Value.gameObject.SetActive(selected||game.ViewCamera.orthographicSize<29);
                var name=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name;
                pair.Value.text=name+(selected||game.ViewCamera.orthographicSize<20?"\n"+n.Status:"");pair.Value.color=selected?new Color(.02f,.48f,.57f):new Color(.13f,.22f,.28f);
                lamps[n.Id].GetComponent<Renderer>().sharedMaterial=Paint(n.Batch.Count>0?green:amber);
                if(workpieces.TryGetValue(n.Id,out var piece))piece.gameObject.SetActive(n.Output.Count>0);
                if(inputPieces.TryGetValue(n.Id,out var input))input.gameObject.SetActive(n.Batch.Count>0&&n.Output.Count==0);
            }
        }
        private static float CargoHeight(int material)
        {
            var style=HardwareVisualCatalog.Get(material);
            return style.Form==HardwareProductForm.Ore?.2f:style.Form==HardwareProductForm.Shell?.1f:style.Form==HardwareProductForm.Billet||style.Form==HardwareProductForm.Carton||style.Form==HardwareProductForm.Cooler?.08f:.035f;
        }
        public void Ghost(int x,int z,int rotation,bool valid){if(ghost==null)ghost=Cube("Placement footprint",Vector3.zero,new Vector3(3.6f,.15f,3.6f),green,transform);ghost.gameObject.SetActive(true);ghost.position=Local(game.World.Factory(game.Factory),new GridPoint(x,z));ghost.rotation=Quaternion.Euler(0,-rotation*90,0);ghost.GetComponent<Renderer>().sharedMaterial=Paint(valid?green:Color.red);}
        public void HideGhost(){if(ghost!=null)ghost.gameObject.SetActive(false);}
        private void OnDestroy(){ClearRouteMeshes();foreach(var m in colors.Values)Destroy(m);foreach(var mesh in ownedMeshes)Destroy(mesh);}
    }
}
