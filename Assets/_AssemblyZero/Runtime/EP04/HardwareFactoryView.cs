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
            Cube("Supplier warehouse",new Vector3(7,.9f,h.SupplierZ*1.2f),new Vector3(6,2,4),steel,environment);Label("青港采购站 · E 提货",new Vector3(3,3,h.SupplierZ*1.2f),environment,.9f);
            var r=game.World.State.Resources[0];for(int i=0;i<8;i++)Cube("Aluminum ore",new Vector3(4+i%3,.3f+i%2*.3f,r.Z*1.2f+i/3),new Vector3(.8f,.8f,.8f),new Color(.6f,.55f,.43f),environment);
            Label("铝原料点 · E 采集",new Vector3(4,2.5f,r.Z*1.2f),environment,.9f);Label("车库 · E 卸货",new Vector3(2,2,19.2f),environment,.8f);
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
                if(n.Kind==BuildingKind.Warehouse){for(int i=0;i<6;i++)Cube("Stock rack",new Vector3((i%2-.5f)*1.5f,.8f,(i/2-1)*.8f),new Vector3(1.2f,1.5f,.6f),steel,station);}
                else if(n.Kind==BuildingKind.Sales){Cube("Dispatch desk",new Vector3(0,.8f,0),new Vector3(2.8f,.2f,2),steel,station);for(int i=0;i<3;i++)Cube("Carton",new Vector3(i*.8f-.8f,1.15f,0),Vector3.one*.65f,amber,station);}
                else
                {
                    if(game.MachineVisual!=null){var visual=Instantiate(game.MachineVisual,station);visual.transform.localPosition=new Vector3(0,.4f,0);}
                    else Cube("Machine enclosure",new Vector3(0,1,0),new Vector3(2.6f,1.7f,2.5f),steel,station);
                    Cube("Work surface",new Vector3(0,.8f,-1.1f),new Vector3(2.8f,.15f,1),steel,station);
                    Cube("Control panel",new Vector3(1.2f,1.45f,-1.28f),new Vector3(.55f,.55f,.12f),dark,station);
                    if(n.Kind==BuildingKind.Tester){foreach(var side in new[]{-1.25f,1.25f})Cube("Inspection gantry",new Vector3(side,1.4f,-.5f),new Vector3(.18f,2.2f,.18f),steel,station);Cube("Scan light",new Vector3(0,2.4f,-.5f),new Vector3(2.7f,.2f,.3f),green,station);}
                    if(n.Kind==BuildingKind.Packaging){Cube("Packing roll",new Vector3(.9f,1.2f,0),new Vector3(.6f,.65f,1.3f),amber,station);Cube("Box stack",new Vector3(-1,1.1f,.8f),new Vector3(.7f,.9f,.7f),amber,station);}
                    if(game.ArmVisual!=null&&(n.Kind==BuildingKind.BoardAssembly||n.Kind==BuildingKind.GPUAssembly)){var arm=Instantiate(game.ArmVisual,station);arm.transform.localPosition=new Vector3(-.8f,.5f,.4f);arm.transform.localScale*=.6f;}
                    workpieces[n.Id]=Product(game.Config.Economy.Recipe(n.RecipeId)?.Output??HardwareCatalog.Board,station);workpieces[n.Id].localPosition=new Vector3(0,1,-1.2f);
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
        private Transform Product(int material,Transform parent)
        {
            var t=new GameObject("Cargo "+HardwareCatalog.Name(material)).transform;t.SetParent(parent,false);
            if(material>=HardwareCatalog.Standard)Cube("Retail box",Vector3.zero,new Vector3(.8f,.35f,.5f),amber,t);
            else if(material>=HardwareCatalog.AssembledBoard)
            {
                Cube("PCB",Vector3.zero,new Vector3(.85f,.07f,.45f),green,t);
                if(material>HardwareCatalog.AssembledBoard){Cube("Heatsink",new Vector3(0,.1f,0),new Vector3(.8f,.16f,.42f),steel,t);for(int i=0;i<2;i++){var fan=GameObject.CreatePrimitive(PrimitiveType.Cylinder);fan.name="Fan";fan.transform.SetParent(t,false);fan.transform.localPosition=new Vector3((i-.5f)*.4f,.21f,0);fan.transform.localScale=new Vector3(.3f,.025f,.3f);fan.GetComponent<Renderer>().sharedMaterial=Paint(dark);Destroy(fan.GetComponent<Collider>());}}
            }
            else if(material==HardwareCatalog.Heat||material==HardwareCatalog.QuietHeat){for(int i=0;i<6;i++)Cube("Fin",new Vector3(i*.1f-.25f,0,0),new Vector3(.04f,.35f,.4f),steel,t);}
            else Cube("Material tray",Vector3.zero,new Vector3(.45f,.25f,.4f),material==HardwareCatalog.Aluminum?steel:material==HardwareCatalog.Core?dark:green,t);
            return t;
        }
        public void Present(float interpolation)
        {
            var w=game.World;var changed=w.State.Tick!=lastTick;
            if(changed){lastTick=w.State.Tick;foreach(var p in cargo)previousCargo[p.Key]=p.Value.position;priorTruck=truck.position;}
            var v=game.Truck;var target=new Vector3((float)(v.Manual?v.DriveX:v.Position.X)*1.2f,.65f,(float)(v.Manual?v.DriveZ:v.Position.Z)*1.2f);truck.position=game.Paused?target:Vector3.Lerp(priorTruck,target,Mathf.Clamp01(interpolation));truck.rotation=Quaternion.Euler(0,(float)v.Heading*Mathf.Rad2Deg,0);
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
