using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    // Pure presentation: transforms never own goods, progress, or cash.
    public sealed class HardwareFactoryView : MonoBehaviour
    {
        private HardwareGame game; private Transform root,environment,truck,ghost;
        private readonly Dictionary<Color,Material> colors=new Dictionary<Color,Material>();
        private readonly Dictionary<int,TMP_Text> labels=new Dictionary<int,TMP_Text>();
        private readonly Dictionary<int,Transform> lamps=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Vector3[]> paths=new Dictionary<int,Vector3[]>();
        private readonly Dictionary<int,Transform> cargo=new Dictionary<int,Transform>();
        private readonly Dictionary<int,Vector3> previousCargo=new Dictionary<int,Vector3>();
        private readonly Dictionary<int,Transform> workpieces=new Dictionary<int,Transform>();
        private long lastTick=-1;private Vector3 priorTruck;private float labelTimer;
        private readonly Color steel=new Color(.42f,.47f,.45f),dark=new Color(.12f,.15f,.14f),amber=new Color(.95f,.61f,.18f),green=new Color(.22f,.65f,.4f);
        public void Initialize(HardwareGame g)
        {
            game=g;environment=new GameObject("Hardware environment").transform;environment.SetParent(transform);
            Cube("Terrain",new Vector3(58,-.6f,44),new Vector3(150,.5f,120),new Color(.28f,.32f,.27f),environment);
            foreach(var f in game.World.State.Factories)
            {
                Cube("Factory foundation",new Vector3((f.X+20)*1.2f,-.2f,(f.Z+16)*1.2f),new Vector3(49,.5f,39),new Color(.53f,.53f,.47f),environment);
                for(int x=0;x<=40;x+=4)Cube("Floor seam",new Vector3((f.X+x)*1.2f,.06f,(f.Z+16)*1.2f),new Vector3(.025f,.015f,38),new Color(.38f,.39f,.35f),environment);
                for(int z=0;z<=32;z+=4)Cube("Floor seam",new Vector3((f.X+20)*1.2f,.06f,(f.Z+z)*1.2f),new Vector3(48,.015f,.025f),new Color(.38f,.39f,.35f),environment);
                for(int x=2;x<40;x+=4){Cube("Rear fence",new Vector3((f.X+x)*1.2f,1.1f,(f.Z+32)*1.2f),new Vector3(.15f,2.2f,.15f),steel,environment);}
                Cube("Rear rail",new Vector3((f.X+20)*1.2f,1.8f,(f.Z+32)*1.2f),new Vector3(48,.12f,.12f),steel,environment);
            }
            Cube("Service road",new Vector3(1.2f,-.07f,32),new Vector3(4,.2f,52),new Color(.16f,.18f,.17f),environment);
            for(var z=8;z<56;z+=4)Cube("Road marking",new Vector3(1.2f,.04f,z),new Vector3(.12f,.03f,1.8f),amber,environment);
            var h=game.Config.Economy.Hardware;
            Cube("Supplier warehouse",new Vector3(7,.9f,h.SupplierZ*1.2f),new Vector3(6,2,4),new Color(.18f,.62f,.72f),environment);Cube("Supplier canopy",new Vector3(7,2.1f,h.SupplierZ*1.2f),new Vector3(6.4f,.3f,4.4f),amber,environment);Label("青港采购站 · 自动装运",new Vector3(3,3,h.SupplierZ*1.2f),environment,.9f);
            var r=game.World.State.Resources[0];for(int i=0;i<8;i++)Cube("Aluminum ore",new Vector3(4+i%3,.3f+i%2*.3f,r.Z*1.2f+i/3),new Vector3(.8f,.8f,.8f),new Color(.6f,.55f,.43f),environment);
            Label("铝原料点 · 点击规划采集",new Vector3(4,2.5f,r.Z*1.2f),environment,.9f);Label("车库 · 自动卸货",new Vector3(2,2,19.2f),environment,.8f);
            truck=new GameObject("Player truck").transform;truck.SetParent(transform);Cube("Chassis",Vector3.zero,new Vector3(1.4f,.5f,2.6f),amber,truck);Cube("Cabin",new Vector3(0,.5f,.8f),new Vector3(1.35f,.9f,.9f),amber,truck);Cube("Windscreen",new Vector3(0,.7f,1.27f),new Vector3(1.1f,.4f,.05f),dark,truck);Cube("Cargo bed",new Vector3(0,.3f,-.5f),new Vector3(1.25f,.2f,1.35f),steel,truck);
            foreach(var x in new[]{-.75f,.75f})foreach(var z in new[]{-.8f,.8f})Cube("Tire",new Vector3(x,-.15f,z),new Vector3(.25f,.65f,.6f),dark,truck);
            priorTruck=new Vector3(1.2f,.65f,19.2f);Rebuild();
        }
        private Material Paint(Color c){if(!colors.TryGetValue(c,out var m)){m=new Material(game.Config.BaseMaterial);m.color=c;m.enableInstancing=true;colors.Add(c,m);}return m;}
        private Transform Cube(string name,Vector3 position,Vector3 scale,Color color,Transform parent)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=Paint(color);Destroy(o.GetComponent<Collider>());return o.transform;
        }
        private TMP_Text Label(string text,Vector3 p,Transform parent,float size=.6f)
        {
            var o=new GameObject("Label",typeof(TextMeshPro));o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.rotation=Quaternion.LookRotation(-new Vector3(26,42,-29));
            var t=o.GetComponent<TextMeshPro>();t.font=game.Font;t.text=text;t.fontSize=14;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(18,4);t.transform.localScale=Vector3.one*size;t.color=new Color(.08f,.11f,.1f);return t;
        }
        private Vector3 Local(FactorySite f,GridPoint p)=>new Vector3((f.X+p.X)*1.2f,.6f+p.H*1.2f,(f.Z+p.Z)*1.2f);
        public void Rebuild()
        {
            if(root!=null)Destroy(root.gameObject);root=new GameObject("Factory machinery").transform;root.SetParent(transform);labels.Clear();lamps.Clear();paths.Clear();workpieces.Clear();
            var f=game.World.Factory(game.Factory);
            foreach(var n in game.World.State.Nodes.Where(n=>n.Factory==game.Factory))
            {
                var pos=Local(f,n.Center);var station=new GameObject("Station "+n.Id).transform;station.SetParent(root);station.position=pos;station.rotation=Quaternion.Euler(0,-n.Rotation*90,0);
                Cube("Hazard footprint",new Vector3(0,-.45f,0),new Vector3(3.8f,.08f,3.8f),amber,station);Cube("Base",Vector3.zero,new Vector3(3.2f,.8f,3.2f),dark,station);
                BuildBlocks(n,station);
                if(n.Kind!=BuildingKind.Warehouse&&n.Kind!=BuildingKind.Sales)
                {
                    workpieces[n.Id]=Product(game.Config.Economy.Recipe(n.RecipeId)?.Output??HardwareCatalog.Board,station);workpieces[n.Id].localPosition=new Vector3(0,1,-.8f);
                }
                lamps[n.Id]=Cube("Status light",new Vector3(1.4f,2.3f,1.3f),new Vector3(.2f,.35f,.2f),green,station);
                Cube("Input dock",new Vector3(-2.4f,0,0),new Vector3(.5f,.4f,.65f),green,station);Cube("Output dock",new Vector3(2.4f,0,0),new Vector3(.5f,.4f,.65f),amber,station);
                labels[n.Id]=Label(game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name,pos+Vector3.up*3.8f,root,.6f);
            }
            foreach(var r in game.World.State.Routes.Where(r=>r.Factory==game.Factory))
            {
                var pts=r.Points.Select(p=>Local(f,p)).ToArray();paths[r.Id]=pts;
                for(int i=1;i<pts.Length;i++)
                {
                    var delta=pts[i]-pts[i-1];var seg=Cube("Conveyor "+r.Id,(pts[i]+pts[i-1])/2,new Vector3(.62f,.16f,delta.magnitude+.05f),dark,root);seg.rotation=Quaternion.LookRotation(delta,Mathf.Abs(delta.y)>.1f?Vector3.forward:Vector3.up);
                    if(i%3==0)Cube("Support",(pts[i]+pts[i-1])/2-Vector3.up*.3f,new Vector3(.1f,.6f,.1f),steel,root);
                }
            }
        }
        // Only colored box primitives; silhouettes describe the work performed at each station.
        private void BuildBlocks(BusinessNode n,Transform station)
        {
            var ivory=new Color(.88f,.9f,.82f);var blue=new Color(.16f,.5f,.85f);var teal=new Color(.12f,.65f,.65f);
            var coral=new Color(.87f,.3f,.2f);var violet=new Color(.57f,.38f,.85f);
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
        private Transform Product(int material,Transform parent)
        {
            var t=new GameObject("Cargo "+HardwareCatalog.Name(material)).transform;t.SetParent(parent,false);
            if(material>=HardwareCatalog.Standard)Cube("Retail box",Vector3.zero,new Vector3(.8f,.35f,.5f),amber,t);
            else if(material>=HardwareCatalog.AssembledBoard)
            {
                Cube("PCB",Vector3.zero,new Vector3(.85f,.07f,.45f),green,t);
                if(material>HardwareCatalog.AssembledBoard){Cube("Heatsink",new Vector3(0,.1f,0),new Vector3(.8f,.16f,.42f),steel,t);if(material>=HardwareCatalog.Tested)for(int i=0;i<2;i++){var p=new Vector3((i-.5f)*.4f,.21f,0);Cube("Fan brick",p,new Vector3(.3f,.05f,.3f),dark,t);Cube("Fan blade",p+Vector3.up*.03f,new Vector3(.24f,.025f,.06f),steel,t);Cube("Fan blade",p+Vector3.up*.03f,new Vector3(.06f,.025f,.24f),steel,t);}}
            }
            else if(material==HardwareCatalog.Heat||material==HardwareCatalog.QuietHeat){for(int i=0;i<6;i++)Cube("Fin",new Vector3(i*.1f-.25f,0,0),new Vector3(.04f,.35f,.4f),steel,t);}
            else Cube("Material tray",Vector3.zero,new Vector3(.45f,.25f,.4f),material==HardwareCatalog.Aluminum?steel:material==HardwareCatalog.Core?dark:green,t);
            return t;
        }
        public void Present(float interpolation)
        {
            var w=game.World;var changed=w.State.Tick!=lastTick;
            if(changed){lastTick=w.State.Tick;foreach(var p in cargo)previousCargo[p.Key]=p.Value.position;priorTruck=truck.position;}
            var v=game.Truck;var target=new Vector3(v.Position.X*1.2f,.65f,v.Position.Z*1.2f);truck.position=game.Paused?target:Vector3.Lerp(priorTruck,target,Mathf.Clamp01(interpolation));
            if(v.PathIndex+1<v.Path.Count){var p=v.Path[v.PathIndex+1];var direction=new Vector3(p.X-v.Position.X,0,p.Z-v.Position.Z);if(direction.sqrMagnitude>0)truck.rotation=Quaternion.LookRotation(direction);}
            var alive=new HashSet<int>();
            foreach(var r in w.State.Routes.Where(r=>r.Factory==game.Factory))
            {
                if(!paths.TryGetValue(r.Id,out var path))continue;r.Backend.CreateSnapshot(r.Snapshot);
                foreach(var item in r.Snapshot.Items)
                {
                    var id=item.ItemId.Value;
                    alive.Add(id);var t=Position(path,item.Distance/(float)Mathf.Max(1,r.Length-512))+Vector3.up*.25f;
                    if(!cargo.TryGetValue(id,out var view)){view=Product(r.Material,transform);view.position=t;cargo.Add(id,view);previousCargo[id]=t;}
                    view.position=game.Paused?t:Vector3.Lerp(previousCargo.TryGetValue(id,out var old)?old:t,t,Mathf.Clamp01(interpolation));
                }
            }
            foreach(var id in cargo.Keys.Where(id=>!alive.Contains(id)).ToArray()){Destroy(cargo[id].gameObject);cargo.Remove(id);previousCargo.Remove(id);}
            labelTimer+=Time.unscaledDeltaTime;if(labelTimer<.2f)return;labelTimer=0;
            foreach(var pair in labels){var n=w.Node(pair.Key);if(n==null)continue;pair.Value.text=game.Config.Economy.Hardware.Buildings.First(b=>b.Kind==n.Kind).Name+"\n"+n.Status;pair.Value.color=game.SelectedNode==n.Id?new Color(.65f,.28f,.02f):new Color(.08f,.11f,.1f);lamps[n.Id].GetComponent<Renderer>().sharedMaterial=Paint(n.Batch.Count>0?green:amber);if(workpieces.TryGetValue(n.Id,out var piece))piece.gameObject.SetActive(n.Batch.Count>0||n.Output.Count>0);}
        }
        private static Vector3 Position(Vector3[] points,float ratio){var lengths=0f;for(int i=1;i<points.Length;i++)lengths+=Vector3.Distance(points[i-1],points[i]);var d=Mathf.Clamp01(ratio)*lengths;for(int i=1;i<points.Length;i++){var l=Vector3.Distance(points[i-1],points[i]);if(d<=l)return Vector3.Lerp(points[i-1],points[i],l==0?0:d/l);d-=l;}return points[points.Length-1];}
        public void Ghost(int x,int z,int rotation,bool valid){if(ghost==null)ghost=Cube("Placement footprint",Vector3.zero,new Vector3(3.6f,.15f,3.6f),green,transform);ghost.gameObject.SetActive(true);ghost.position=Local(game.World.Factory(game.Factory),new GridPoint(x,z));ghost.rotation=Quaternion.Euler(0,-rotation*90,0);ghost.GetComponent<Renderer>().sharedMaterial=Paint(valid?green:Color.red);}
        public void HideGhost(){if(ghost!=null)ghost.gameObject.SetActive(false);}
        private void OnDestroy(){foreach(var m in colors.Values)Destroy(m);}
    }
}
