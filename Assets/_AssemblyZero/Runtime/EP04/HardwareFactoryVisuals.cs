using System.Collections.Generic;
using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class HardwareFactoryView
    {
        private readonly Dictionary<int,Transform> prototypes=new Dictionary<int,Transform>();
        private readonly List<Mesh> ownedMeshes=new List<Mesh>();
        private Mesh bevel,oreMesh;
        private TMP_Text oreLabel;
        private Transform truckLoad;
        private readonly List<Transform> oreRocks=new List<Transform>();
        private readonly Color ivory=new Color(.81f,.87f,.9f),cyan=HardwareVisualCatalog.Cyan;
        private Transform Shape(string name,Vector3 p,Vector3 scale,Color c,Transform parent,PrimitiveType type)
        {
            var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.localScale=scale;
            o.GetComponent<Renderer>().sharedMaterial=Paint(c);
            if(type==PrimitiveType.Sphere)
            {
                if(oreMesh==null)
                {
                    oreMesh=new Mesh{name="Faceted aluminum ore"};var v=new[]{Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f};
                    var faces=new[]{0,4,3,0,2,4,0,5,2,0,3,5,1,3,4,1,4,2,1,2,5,1,5,3};
                    var vertices=new Vector3[faces.Length];var indices=new int[faces.Length];for(var i=0;i<faces.Length;i++){vertices[i]=v[faces[i]];indices[i]=i;}
                    oreMesh.vertices=vertices;oreMesh.triangles=indices;oreMesh.RecalculateNormals();ownedMeshes.Add(oreMesh);
                }
                o.GetComponent<MeshFilter>().sharedMesh=oreMesh;
            }
            Destroy(o.GetComponent<Collider>());return o.transform;
        }
        private Transform Case(string name,Vector3 p,Vector3 scale,Color c,Transform parent)
        {
            var t=Cube(name,p,scale,c,parent);t.GetComponent<MeshFilter>().sharedMesh=BevelMesh();return t;
        }
        private Mesh BevelMesh()
        {
            if(bevel!=null)return bevel;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var ring=new[]{new Vector2(-.4f,-.5f),new Vector2(.4f,-.5f),new Vector2(.5f,-.4f),new Vector2(.5f,.4f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.4f),new Vector2(-.5f,-.4f)};
            for(int r=0;r<4;r++)foreach(var p in ring){var k=r==0||r==3?.86f:1;vertices.Add(new Vector3(p.x*k,new[]{-.5f,-.4f,.4f,.5f}[r],p.y*k));}
            for(int r=0;r<3;r++)for(int i=0;i<8;i++){int a=r*8+i,b=r*8+(i+1)%8;triangles.AddRange(new[]{a,b,b+8,a,b+8,a+8});}
            for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i+1,i,24,24+i,25+i});}
            for(var i=0;i<triangles.Count;i+=3){var tmp=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=tmp;}
            bevel=new Mesh{name="Hardware beveled casing"};bevel.SetVertices(vertices);bevel.SetTriangles(triangles,0);bevel.RecalculateNormals();bevel.RecalculateBounds();ownedMeshes.Add(bevel);return bevel;
        }
        private void BuildEnvironment()
        {
            if(environment!=null){environment.gameObject.SetActive(false);Destroy(environment.gameObject);}
            environment=new GameObject("Hardware environment").transform;environment.SetParent(transform,false);
            oreRocks.Clear();
            Cube("Landscape",new Vector3(68,-.65f,52),new Vector3(170,.5f,130),new Color(.32f,.41f,.4f),environment);
            for(var z=8;z<68;z+=6)if(z<32||z>39)Shape("Eastern rock ridge",new Vector3(51*1.2f,.5f,z*1.2f),new Vector3(4,2.5f,6),new Color(.34f,.43f,.45f),environment,PrimitiveType.Sphere);
            for(var x=6;x<43;x+=5)if(x<22||x>28)Shape("Northern rock ridge",new Vector3(x*1.2f,.4f,44.5f*1.2f),new Vector3(5,2.2f,3),new Color(.34f,.43f,.45f),environment,PrimitiveType.Sphere);
            foreach(var f in game.World.State.Factories)
            {
                var center=new Vector3((f.X+20)*1.2f,0,(f.Z+16)*1.2f);
                Case("Factory foundation",center-Vector3.up*.18f,new Vector3(49.8f,.5f,40.2f),new Color(.45f,.53f,.58f),environment);
                for(int x=0;x<=40;x+=4)Cube("Floor seam",new Vector3((f.X+x)*1.2f,.08f,center.z),new Vector3(.025f,.015f,38.4f),steel,environment);
                for(int z=0;z<=32;z+=4)Cube("Floor seam",new Vector3(center.x,.08f,(f.Z+z)*1.2f),new Vector3(48,.015f,.025f),steel,environment);
                // All walls lie beyond the buildable grid. Front and left remain low for the fixed camera.
                Cube("Rear wall",new Vector3(center.x,1.5f,(f.Z+33)*1.2f),new Vector3(49,3,.35f),ivory,environment);
                Cube("Rear window band",new Vector3(center.x,1.9f,(f.Z+32.8f)*1.2f),new Vector3(46,.8f,.08f),new Color(.22f,.43f,.52f),environment);
                Cube("Rear cyan guide",new Vector3(center.x,2.65f,(f.Z+32.75f)*1.2f),new Vector3(46,.065f,.08f),cyan,environment);
                Cube("East wall",new Vector3((f.X+41)*1.2f,1.15f,center.z),new Vector3(.35f,2.3f,39.6f),ivory,environment);
                Cube("Front low wall",new Vector3(center.x,.35f,(f.Z-1)*1.2f),new Vector3(49,.7f,.3f),ivory,environment);
                foreach(var span in new[]{new Vector2(4,8),new Vector2(24,16)})
                    Cube("West low wall",new Vector3((f.X-1)*1.2f,.35f,(f.Z+span.x)*1.2f),new Vector3(.3f,.7f,span.y*1.2f),ivory,environment);
                for(var i=0;i<=40;i+=8)Case("Structural column",new Vector3((f.X+i)*1.2f,1.7f,(f.Z+33)*1.2f),new Vector3(.45f,3.4f,.6f),steel,environment);
                Label("星铸科技  /  GPU FACTORY 0"+f.Id,new Vector3(center.x,4,(f.Z+33)*1.2f),environment,.55f);
                var dock=new Vector3(f.Dock.X*1.2f,.07f,f.Dock.Z*1.2f);
                Cube("Loading bay",dock,new Vector3(3.2f,.08f,5),dark,environment);
                Cube("Dock marking",dock+Vector3.up*.08f,new Vector3(.1f,.03f,4.6f),amber,environment);
                Label("0"+f.Id+" 装卸入口",dock+new Vector3(0,2.4f,0),environment,.35f);
            }
            var h=game.Config.Economy.Hardware;var supplier=new GridPoint(h.SupplierX,h.SupplierZ);var ore=new GridPoint(h.OreX,h.OreZ);
            // Draw the same deterministic paths used by the vehicles, including ridge openings.
            var roadCells=new HashSet<GridPoint>();
            foreach(var f in game.World.State.Factories)foreach(var destination in new[]{supplier,ore})
            {
                var path=GridRouting.HardwarePath(f.Dock,destination,game.World.State.Factories);if(path==null)continue;
                foreach(var p in path)roadCells.Add(p);
            }
            foreach(var p in roadCells)
            {
                var point=new Vector3(p.X*1.2f,-.02f,p.Z*1.2f);
                Cube("Exterior road",point,new Vector3(2.7f,.14f,2.7f),new Color(.17f,.23f,.27f),environment);
                if((p.X+p.Z)%4==0)Cube("Road stud",point+Vector3.up*.09f,new Vector3(.18f,.025f,.18f),amber,environment);
            }
            var sp=new Vector3(supplier.X*1.2f,0,supplier.Z*1.2f);
            Cube("Supplier yard",sp+Vector3.right*4,new Vector3(12,.12f,11),steel,environment);
            Case("Supplier warehouse",sp+new Vector3(5,1.6f,2),new Vector3(6,3.2f,5),ivory,environment);
            Cube("Supplier loading shutter",sp+new Vector3(1.9f,1.2f,2),new Vector3(.08f,2.4f,2.6f),dark,environment);
            Cube("Front dispatch door",sp+new Vector3(5,1.25f,-.55f),new Vector3(3.2f,2.5f,.08f),dark,environment);
            for(var i=0;i<6;i++)Cube("Shutter rib",sp+new Vector3(5,.3f+i*.37f,-.61f),new Vector3(3,.045f,.04f),steel,environment);
            Cube("Supplier facade stripe",sp+new Vector3(5,2.8f,-.62f),new Vector3(5.1f,.22f,.05f),cyan,environment);
            foreach(var x in new[]{3.5f,6.5f})Case("Roof ventilation",sp+new Vector3(x,3.4f,2.4f),new Vector3(1.3f,.55f,1.2f),steel,environment);
            Cube("Supplier canopy",sp+new Vector3(0,2.7f,0),new Vector3(4,.16f,4),cyan,environment);
            foreach(var x in new[]{-1.8f,1.8f})foreach(var z in new[]{-1.8f,1.8f})Cube("Canopy column",sp+new Vector3(x,1.35f,z),new Vector3(.14f,2.7f,.14f),steel,environment);
            for(var i=0;i<3;i++)Case("Supplier pallet",sp+new Vector3(6+i%2, .6f,-2-i/2),new Vector3(.8f,1.1f,.8f),amber,environment);
            Label("青港元件  /  采购物流区",sp+new Vector3(3,4.3f,0),environment,.55f);
            var op=new Vector3(ore.X*1.2f,0,ore.Z*1.2f);
            Cube("Ore yard",op+Vector3.right*3,new Vector3(10,.13f,9),new Color(.36f,.4f,.4f),environment);
            for(var i=0;i<25;i++)
            {
                var rock=Shape("Aluminum ore",op+new Vector3(1.5f+i%5*.65f,.4f+(i%3)*.24f,-1.5f+i/5*.65f),new Vector3(1.2f,1.1f,1.15f),new Color(.48f+(i%3)*.06f,.55f+(i%3)*.04f,.6f),environment,PrimitiveType.Sphere);
                rock.rotation=Quaternion.Euler(i*31,i*47,15);
                oreRocks.Add(rock);
            }
            for(var i=0;i<5;i++)Cube("Mine barrier",op+new Vector3(i*1.4f, .5f,3.8f),new Vector3(1.1f,.9f,.25f),i%2==0?amber:dark,environment);
            foreach(var x in new[]{1f,5.5f})Cube("Ore loading gantry",op+new Vector3(x,1.4f,-2.8f),new Vector3(.25f,2.8f,.35f),amber,environment);
            Cube("Ore loading beam",op+new Vector3(3.25f,2.9f,-2.8f),new Vector3(4.9f,.35f,.55f),amber,environment);
            Case("Ore loading hopper",op+new Vector3(5.1f,.8f,-2.1f),new Vector3(1.5f,1.5f,1.3f),steel,environment);
            oreLabel=Label("北岭铝矿  /  自动采集",op+new Vector3(2,3.7f,0),environment,.45f);
        }
        private void BuildTruck()
        {
            truck=new GameObject("Player truck").transform;truck.SetParent(transform,false);
            Case("Chassis",Vector3.zero,new Vector3(1.35f,.45f,2.5f),amber,truck);
            Case("Cabin",new Vector3(0,.5f,.72f),new Vector3(1.3f,.85f,.95f),ivory,truck);
            Cube("Windscreen",new Vector3(0,.65f,1.21f),new Vector3(1.06f,.4f,.035f),new Color(.1f,.3f,.4f),truck);
            Cube("Cargo bed",new Vector3(0,.24f,-.65f),new Vector3(1.1f,.14f,1.2f),dark,truck);
            truckLoad=Case("Secured cargo",new Vector3(0,.53f,-.65f),new Vector3(.85f,.5f,.85f),cyan,truck);truckLoad.gameObject.SetActive(false);
            foreach(var x in new[]{-.63f,.63f}){Cube("Bed rail",new Vector3(x,.47f,-.65f),new Vector3(.1f,.38f,1.2f),amber,truck);Cube("Headlight",new Vector3(x*.7f,.05f,1.26f),new Vector3(.3f,.13f,.04f),cyan,truck);}
            foreach(var x in new[]{-.72f,.72f})foreach(var z in new[]{-.8f,.8f})
            {
                var tire=Shape("Wheel",new Vector3(x,-.15f,z),new Vector3(.6f,.12f,.6f),dark,truck,PrimitiveType.Cylinder);tire.localRotation=Quaternion.Euler(0,0,90);
            }
        }
        private void DetailStation(BusinessNode n,Transform station)
        {
            foreach(var f in station.GetComponentsInChildren<MeshFilter>())if(f.name=="Base"||f.name=="Brick deck"||f.name=="Smelter body")f.sharedMesh=BevelMesh();
            Case("Control console",new Vector3(1.25f,.85f,-1.25f),new Vector3(.45f,.7f,.35f),ivory,station);
            Cube("OLED status screen",new Vector3(1.25f,1.02f,-1.44f),new Vector3(.31f,.27f,.025f),cyan,station);
            Cube("Service cable tray",new Vector3(-1.48f,.55f,0),new Vector3(.09f,.08f,2.5f),steel,station);
            for(var i=0;i<4;i++)Cube("Vent slot",new Vector3(-.8f+i*.3f,.25f,-1.61f),new Vector3(.17f,.22f,.025f),steel,station);
            foreach(var x in new[]{-1.3f,1.3f})foreach(var z in new[]{-1.3f,1.3f})Shape("Deck bolt",new Vector3(x,.69f,z),new Vector3(.1f,.035f,.1f),steel,station,PrimitiveType.Cylinder);
            var actionName=n.Kind==BuildingKind.BoardAssembly?"Pick head":n.Kind==BuildingKind.GPUAssembly?"Robot tool":n.Kind==BuildingKind.Tester?"Scan strip":n.Kind==BuildingKind.Packaging?"Sealing bar":n.Kind==BuildingKind.Manufacturer?"Press die":"";
            foreach(Transform child in station)if(child.name==actionName){actions[n.Id]=child;actionOrigins[n.Id]=child.localPosition;break;}
        }
        private Transform BuildProduct(int material,Transform parent)
        {
            if(!prototypes.TryGetValue(material,out var template))
            {
                template=new GameObject("Product template "+material).transform;template.SetParent(transform,false);
                template.gameObject.AddComponent<HardwareProductView>().Material=material;
                CreateProductGeometry(material,template);CombineProduct(template);template.gameObject.SetActive(false);prototypes.Add(material,template);
            }
            var copy=Instantiate(template,parent,false);copy.name="Cargo "+HardwareCatalog.Name(material);copy.gameObject.SetActive(true);return copy;
        }
        public Transform CreateProductGallery()
        {
            var gallery=new GameObject("Product visual acceptance gallery").transform;gallery.SetParent(transform,false);
            gallery.position=new Vector3(62,.25f,101);
            var right=game.ViewCamera.transform.right;right.y=0;right.Normalize();var back=Vector3.Cross(right,Vector3.up);
            var rows=new[]{new[]{HardwareCatalog.Board,HardwareCatalog.AssembledBoard,HardwareCatalog.RawGPU,HardwareCatalog.Tested,HardwareCatalog.Standard},new[]{HardwareCatalog.Heat,HardwareCatalog.QuietHeat,HardwareCatalog.QuietRaw,HardwareCatalog.QuietTested,HardwareCatalog.Quiet}};
            for(var row=0;row<2;row++)for(var i=0;i<5;i++)
            {
                var p=right*((i-2)*4.3f)+back*(row==0?3.4f:-3.4f);
                Case("Product display plinth",p,new Vector3(3.5f,.18f,2.5f),ivory,gallery);
                var sample=BuildProduct(rows[row][i],gallery);sample.localPosition=p+Vector3.up*.3f;sample.localScale=Vector3.one*3;
                Label(HardwareCatalog.Name(rows[row][i]),p-back*1.8f+Vector3.up*.5f,gallery,.22f);
            }
            return gallery;
        }
        private void CreateProductGeometry(int material,Transform t)
        {
            var s=HardwareVisualCatalog.Get(material);var pcb=new Color(.035f,.32f,.2f);var gold=new Color(.82f,.62f,.24f);
            if(s.Form==HardwareProductForm.Retail)
            {
                Case("Branded retail box",new Vector3(0,.12f,0),new Vector3(.94f,.3f,.5f),s.Quiet?ivory:dark,t);
                Cube("SKU band",new Vector3(-.3f,.275f,0),new Vector3(.13f,.02f,.48f),s.Quiet?cyan:amber,t);
                Cube("GPU printed silhouette",new Vector3(.1f,.279f,0),new Vector3(.48f,.02f,.28f),s.Quiet?dark:steel,t);
                Cube("QC seal",new Vector3(.35f,.286f,.13f),new Vector3(.09f,.02f,.09f),green,t);return;
            }
            if(s.Form==HardwareProductForm.BareBoard||s.Form==HardwareProductForm.PopulatedBoard||s.Form==HardwareProductForm.Card)
            {
                Cube("PCB",Vector3.zero,new Vector3(.92f,.055f,.46f),pcb,t);
                for(int i=0;i<9;i++)Cube("Gold contact",new Vector3(-.32f+i*.065f,.033f,-.21f),new Vector3(.035f,.015f,.09f),gold,t);
                for(int i=0;i<3;i++)Cube("Copper trace",new Vector3(0,.033f,(i-1)*.09f),new Vector3(.7f,.008f,.012f),gold,t);
                if(s.Form!=HardwareProductForm.BareBoard)
                {
                    Case("GPU package",new Vector3(0,.065f,0),new Vector3(.25f,.06f,.25f),dark,t);
                    Cube("GPU die",new Vector3(0,.1f,0),new Vector3(.13f,.015f,.13f),steel,t);
                    foreach(var x in new[]{-.3f,.3f})foreach(var z in new[]{-.13f,.13f})Cube("VRAM chip",new Vector3(x,.07f,z),new Vector3(.13f,.06f,.09f),dark,t);
                }
                if(s.Form==HardwareProductForm.Card)
                {
                    Case("Cooling shroud",new Vector3(0,s.Quiet?.18f:.15f,0),new Vector3(.91f,s.Quiet?.23f:.18f,.43f),s.Quiet?ivory:dark,t);
                    for(var i=0;i<8;i++)Cube("Cooling fin",new Vector3(-.35f+i*.1f,.13f,.224f),new Vector3(.025f,.13f,.035f),steel,t);
                    var count=s.Quiet?3:2;for(var i=0;i<count;i++)Fan(new Vector3((i-(count-1)*.5f)*(s.Quiet?.28f:.43f),s.Quiet?.31f:.25f,0),s.Quiet?.24f:.34f,t);
                    Cube("SKU light strip",new Vector3(0,.16f,-.235f),new Vector3(.62f,.035f,.018f),s.Color,t);
                    Cube(s.Qualified?"QC passed badge":"Pending inspection badge",new Vector3(.41f,.29f,.18f),new Vector3(.07f,.02f,.07f),s.Qualified?green:amber,t);
                    Cube("IO bracket",new Vector3(-.48f,.1f,0),new Vector3(.035f,.25f,.5f),steel,t);
                }
                return;
            }
            if(s.Form==HardwareProductForm.Cooler)
            {
                for(var i=0;i<(s.Quiet?10:7);i++)Cube("Cooling fin",new Vector3(-.35f+i*(s.Quiet?.075f:.11f),.08f,0),new Vector3(.035f,s.Quiet?.28f:.2f,.4f),steel,t);
                Cube("Copper heatpipe",new Vector3(0,-.015f,0),new Vector3(.78f,.04f,.075f),gold,t);return;
            }
            if(s.Form==HardwareProductForm.Ore){var rock=Shape("Raw ore",Vector3.zero,new Vector3(.48f,.36f,.42f),s.Color,t,PrimitiveType.Sphere);rock.localRotation=Quaternion.Euler(18,35,24);return;}
            if(s.Form==HardwareProductForm.Billet){for(var i=0;i<3;i++)Case("Aluminum ingot",new Vector3(0,.05f*i,i*.1f-.1f),new Vector3(.6f,.12f,.16f),steel,t);return;}
            if(s.Form==HardwareProductForm.Core||s.Form==HardwareProductForm.Memory)
            {
                Cube("Antistatic tray",Vector3.zero,new Vector3(.6f,.04f,.4f),pcb,t);
                for(var i=0;i<(s.Form==HardwareProductForm.Core?1:4);i++)Case("Silicon package",new Vector3((i%2)*.26f-.13f,.065f,(i/2)*.17f-.08f),new Vector3(.2f,.08f,.14f),dark,t);
                Cube("Silicon glint",new Vector3(-.13f,.11f,-.08f),new Vector3(.1f,.015f,.08f),steel,t);return;
            }
            if(s.Form==HardwareProductForm.Shell){Case("Empty shroud",Vector3.zero,new Vector3(.85f,.17f,.43f),dark,t);for(var i=0;i<2;i++)Shape("Fan mounting recess",new Vector3((i-.5f)*.4f,.09f,0),new Vector3(.32f,.015f,.32f),steel,t,PrimitiveType.Cylinder);return;}
            Case("Flat carton",Vector3.zero,new Vector3(.65f,.12f,.45f),s.Color,t);Cube("Carton fold",new Vector3(0,.065f,0),new Vector3(.015f,.01f,.45f),ivory,t);
        }
        private void Fan(Vector3 p,float diameter,Transform t)
        {
            Shape("Fan well",p,new Vector3(diameter,.018f,diameter),dark,t,PrimitiveType.Cylinder);
            for(var k=0;k<5;k++){var blade=Cube("Fan blade",p+Vector3.up*.023f,new Vector3(diameter*.7f,.018f,diameter*.12f),steel,t);blade.localRotation=Quaternion.Euler(0,k*72,0);}
            Shape("Fan hub",p+Vector3.up*.04f,new Vector3(diameter*.23f,.024f,diameter*.23f),cyan,t,PrimitiveType.Cylinder);
        }
        private void CombineProduct(Transform product)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var source=product.GetComponentsInChildren<MeshFilter>();
            foreach(var filter in source)
            {
                var renderer=filter.GetComponent<MeshRenderer>();var mat=renderer.sharedMaterial;
                if(!groups.TryGetValue(mat,out var list)){list=new List<CombineInstance>();groups.Add(mat,list);}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=product.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                filter.gameObject.SetActive(false);
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh{name="Hardware cargo batch",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());ownedMeshes.Add(mesh);
                var obj=new GameObject("Product surface",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(product,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
                obj.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            // Detach now so an instance created this frame cannot clone deferred-destruction children.
            foreach(var filter in source){filter.transform.SetParent(null,false);Destroy(filter.gameObject);}
        }
    }
}
