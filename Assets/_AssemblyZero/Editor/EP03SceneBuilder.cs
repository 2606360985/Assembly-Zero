using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyZero.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity.Editor
{
    public static class EP03SceneBuilder
    {
        public const string Root="Assets/_AssemblyZero/EP03";
        public const string ScenePath="Assets/_AssemblyZero/Scenes/EP03_Demo.unity";
        private static readonly List<Material> palette=new List<Material>();
        [MenuItem("Tools/Assembly Zero/Generate EP03 Factory")]
        public static void BuildScene()=>BuildFactory(false);
        [MenuItem("Tools/Assembly Zero/Generate EP03 Minimal Chain")]
        public static void BuildMinimal()=>BuildFactory(true);
        private static void BuildFactory(bool minimal)
        {
            var scenePath=minimal?"Assets/_AssemblyZero/Scenes/EP03_Minimal.unity":ScenePath;
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before generating EP03.");
            for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scene changes first.");
            var preflight=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(Root+"/Configs/"+(minimal?"FactoryMinimal":"FactoryFull")+".asset");
            if(preflight!=null)
            {
                if(preflight.Routes.Length>0)preflight.Validate();
                if(!string.IsNullOrEmpty(preflight.FrozenInterfaceSignature)&&AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)!=null){EditorSceneManager.OpenScene(scenePath);ValidateScene();}
            }
            Directory.CreateDirectory(Root+"/Configs");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
            palette.Clear();for(var i=0;i<17;i++)palette.Add(Mat("Material-"+i,Color.HSVToRGB((i*.137f)%1,.65f,.85f)));
            // Copy serialized references together. Never invoke EP02's asset-generating builder.
            var original=EditorSceneManager.OpenScene(EP02SceneBuilder.ScenePath,OpenSceneMode.Single);
            EditorSceneManager.SaveScene(original,scenePath,true);
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            var old=UnityEngine.Object.FindFirstObjectByType<EP02SimulationDriver>();var assembly=old.Layout;var scenario=old.Scenario;
            var arms=old.ArmPresenter;var parts=old.PartPresenter;var cell=old.CellPresenter;var cameras=old.CameraDirector;
            var oldSupply=new[]{new EP02Route(assembly.SupplyA),new EP02Route(assembly.SupplyB)};
            var root=new GameObject("EP03 Functional Assembly Line");
            foreach(var go in scene.GetRootGameObjects().Where(g=>g!=root).ToArray())go.transform.SetParent(root.transform,true);
            foreach(var obsolete in root.GetComponentsInChildren<TextMesh>().Where(t=>t.text.StartsWith("工位 ")||t.text=="产品入口"||t.text=="成品出库"||t.text=="供料 A / B"||t.text=="零件回收").ToArray())UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
            foreach(var label in root.GetComponentsInChildren<TextMesh>())
            {
                if(label.text=="供料 A / B")label.text="六路独立供料";
                if(label.text=="零件回收")label.text="检测出口";
            }
            StraightenAssembly(assembly,root.transform);
            var supplyRoutes=new List<SupplyRouteAuthoring>();var supplyEntries=new Vector3[6];var newFlows=new List<EP02FlowSurface>();
            var straight=assembly.SupplyA.First(p=>p.Shape==BeltPieceShape.Straight);
            for(var i=0;i<6;i++)
            {
                var a=assembly.Inserters[i];var center=a.transform.position+Vector3.back*2.5f;center.y=0;var tangent=Vector3.right;
                var routeRoot=new GameObject("Supply "+a.PartTypeId);routeRoot.transform.SetParent(root.transform,false);var pieces=new BeltAuthoring[4];
                for(var j=0;j<4;j++)
                {
                    var go=UnityEngine.Object.Instantiate(straight.gameObject,routeRoot.transform);go.name="Dedicated Segment "+j;
                    go.transform.position=center+tangent*(-3f+j*1.2f);go.transform.rotation=Quaternion.identity;
                    var b=go.GetComponent<BeltAuthoring>();b.StablePieceId=30000+i*10+j;b.Shape=BeltPieceShape.Straight;b.StartsLine=j==0;b.EndsLine=j==3;pieces[j]=b;
                    // Imported straight segment may face a different direction. Visuals rotate independently of the root.
                    var oldDirection=straight.Direction==GridDirection.East?Vector3.right:straight.Direction==GridDirection.West?Vector3.left:straight.Direction==GridDirection.North?Vector3.forward:Vector3.back;
                    var rotation=Quaternion.FromToRotation(oldDirection,tangent);
                    foreach(Transform child in go.transform){child.localPosition=rotation*child.localPosition;child.localRotation=rotation*child.localRotation;}
                    var renderer=go.GetComponentsInChildren<Renderer>().First(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="AssemblyZero/ConveyorSurface");
                    newFlows.Add(new EP02FlowSurface{Renderer=renderer,Line=i,Segment=j,LengthMeters=1.2f,StartMeters=j*1.2f});
                }
                supplyRoutes.Add(new SupplyRouteAuthoring{Pieces=pieces});a.PickupDistances=new int[6];a.PickupDistances[i]=3072;a.PortWidth=1024;
                PrefabUtility.RecordPrefabInstancePropertyModifications(a);
                supplyEntries[i]=new EP02Route(pieces).Sample(0,out _);
            }
            foreach(var g in assembly.SupplyA.Concat(assembly.SupplyB).Select(b=>b.transform.parent.gameObject).Distinct().ToArray())UnityEngine.Object.DestroyImmediate(g);
            assembly.SupplyA=assembly.SupplyB=Array.Empty<BeltAuthoring>();assembly.DedicatedSupplies=supplyRoutes.ToArray();assembly.Surfaces=assembly.Surfaces.Where(s=>s.Product).Concat(newFlows).ToArray();
            var oldHud=old.Hud.gameObject;var audio=old.AudioPresenter;
            UnityEngine.Object.DestroyImmediate(old);UnityEngine.Object.DestroyImmediate(oldHud);if(audio!=null)UnityEngine.Object.DestroyImmediate(audio);
            foreach(var go in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Factory Floor"||t.name=="Floor Grid"||t.name=="Mixed Part Source"||t.name=="Part Recovery").Select(t=>t.gameObject).ToArray())if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            assembly.RejectPoint=new GameObject("Reserved Repair Interface").transform;assembly.RejectPoint.SetParent(root.transform,false);assembly.RejectPoint.localPosition=new Vector3(25,0,0);
            var asset=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(Root+"/Configs/FactoryFull.asset");
            if(asset==null){asset=ScriptableObject.CreateInstance<FactoryLayoutAsset>();AssetDatabase.CreateAsset(asset,Root+"/Configs/FactoryFull.asset");CreateDefaultLayout(asset,supplyEntries);}
            if(minimal){CreateMinimal(asset);asset=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(Root+"/Configs/FactoryMinimal.asset");}
            // Explicit generation may align candidate inlet interfaces; a frozen layout must already agree.
            foreach(var inlet in asset.Modules.Where(m=>m.Module.AssemblyLane>=0))
            {
                var world=asset.AssemblyOrigin+supplyEntries[inlet.Module.AssemblyLane];var port=inlet.Prefab.GetComponent<FactoryModuleAuthoring>().Ports.Single(p=>!p.Output).Anchor.localPosition;
                var desired=world-port;
                if(Vector3.Distance(Position(asset,inlet),desired)>.01f)
                {
                    if(asset.Version!="Candidate")throw new InvalidOperationException("Frozen assembly inlet differs from functional assembly: "+inlet.Module.Id);
                    inlet.Grid=new Vector2Int(Mathf.FloorToInt(desired.x/1.2f),Mathf.FloorToInt(desired.z/1.2f));inlet.PositionOffset=desired-new Vector3((inlet.Grid.x+.5f)*1.2f,0,(inlet.Grid.y+.5f)*1.2f);
                    foreach(var route in asset.Routes.Where(r=>r.Connection.To==inlet.Module.Id)){route.Points[route.Points.Length-1]=world;var previous=route.Points[route.Points.Length-2];route.Points[route.Points.Length-2]=new Vector3(world.x,previous.y,world.z);}
                }
            }
            root.transform.position=asset.AssemblyOrigin;
            var scenarioPath=Root+"/Configs/AssemblyScenario.asset";var localScenario=AssetDatabase.LoadAssetAtPath<EP02ScenarioConfig>(scenarioPath);
            if(localScenario==null){localScenario=UnityEngine.Object.Instantiate(scenario);AssetDatabase.CreateAsset(localScenario,scenarioPath);}
            var views=new List<FactoryModuleAuthoring>();
            foreach(var p in asset.Modules)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(p.Prefab);go.name=p.Module.Id;go.transform.position=Position(asset,p);go.transform.rotation=Quaternion.Euler(0,p.QuarterTurns*90,0);
                var view=go.GetComponent<FactoryModuleAuthoring>();view.InstanceId=p.Module.Id;view.Footprint=p.Footprint;views.Add(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            if(asset.Routes.Length==0)CreateConnections(asset,views);
            asset.Validate();
            var conveyorRoot=new GameObject("EP03 Transport / Explicit Routes");
            foreach(var route in asset.Routes)BuildRoute(route,conveyorRoot.transform);
            var floor=Box("Factory Ground",null,new Vector3(55,-.4f,38.4f),new Vector3(112,.6f,79),Mat("Floor",new Color(.045f,.065f,.08f)));
            var markings=new GameObject("Regions and Maintenance Grid");
            for(var x=0;x<=92;x+=4)Box("Grid",markings.transform,new Vector3(x*1.2f,-.08f,38.4f),new Vector3(.025f,.01f,76.8f),Mat("Grid",new Color(.15f,.2f,.23f)));
            for(var z=0;z<=64;z+=4)Box("Grid",markings.transform,new Vector3(55.2f,-.08f,z*1.2f),new Vector3(110.4f,.01f,.025f),Mat("Grid",new Color(.15f,.2f,.23f)));
            foreach(var r in asset.Regions)
            {
                var rect=r.Bounds;var center=new Vector3(rect.center.x*1.2f,0,rect.center.y*1.2f);
                var material=Mat(r.Reserved?"Reserved":"Boundary",r.Reserved?new Color(.6f,.38f,.06f):new Color(.08f,.42f,.5f));
                foreach(var side in new[]{-1,1}){Box("Boundary",markings.transform,center+new Vector3(side*rect.width*.6f,-.04f,0),new Vector3(.08f,.03f,rect.height*1.2f),material);Box("Boundary",markings.transform,center+new Vector3(0,-.04f,side*rect.height*.6f),new Vector3(rect.width*1.2f,.03f,.08f),material);}
                Label(r.Name,markings.transform,new Vector3(center.x,.1f,(rect.yMax-1)*1.2f),.3f);
            }
            Label("局部返修接口预留 / 无不合格判定",null,new Vector3(100,.1f,40),.2f);
            Label("第二产品 / 第二总装线供料接口预留",null,new Vector3(75,.1f,15),.3f);
            var entry=new GameObject("Expansion Feed Interface");entry.transform.position=new Vector3(57.6f,.7f,30);
            cameras.Camera.farClipPlane=400;
            cameras.Presets=new[]{Pose("全厂俯瞰",new Vector3(56,100,-42),new Vector3(55,0,36),58),Pose("基础加工",new Vector3(17,28,16),new Vector3(17,0,44),55),Pose("零件制造",new Vector3(16,25,-12),new Vector3(16,0,15),55),Pose("部件装配",new Vector3(25,28,28),new Vector3(46,0,37),55),Pose("六工位总装",asset.AssemblyOrigin+new Vector3(12,26,-29),asset.AssemblyOrigin+new Vector3(12,0,-2),57),Pose("六轴近景",asset.AssemblyOrigin+new Vector3(-15,7,2),asset.AssemblyOrigin+new Vector3(-8.8f,1,0),55),Pose("检测与出库",new Vector3(94,24,20),new Vector3(99,0,59),55)};
            var overview=Pose("全厂俯瞰",new Vector3(55.2f,90,-26.5f),new Vector3(55.2f,0,38.4f),45);overview.Orthographic=false;overview.OrthographicSize=40.5f;cameras.Presets[0]=overview;
            cameras.Presets[5]=Pose("六轴近景",asset.AssemblyOrigin+new Vector3(-4,4,-8),asset.AssemblyOrigin+new Vector3(0,1,-1),58);
            var driver=new GameObject("EP03 Factory Authority").AddComponent<EP03SimulationDriver>();driver.Layout=asset;driver.AssemblyLayout=assembly;driver.AssemblyScenario=localScenario;driver.Arms=arms;driver.Parts=parts;driver.Cell=cell;driver.Cameras=cameras;driver.Modules=views.ToArray();driver.MaterialPalette=palette.ToArray();
            driver.CargoRoot=new GameObject("Runtime Materials").transform;driver.FinishedRoot=new GameObject("Finite Finished Queue").transform;driver.FinishedRoot.position=new Vector3(97,.18f,59);
            var bays=new GameObject("Visual - Eight Finished Queue Bays").transform;
            for(var i=0;i<8;i++)
            {
                var center=driver.FinishedRoot.position+new Vector3(i%2*3,-.22f,i/2*3);
                foreach(var side in new[]{-1,1})Box("Bay Mark",bays,center+new Vector3(side*1.35f,0,0),new Vector3(.08f,.03f,2.7f),Mat("Reserved",new Color(.6f,.38f,.06f)));
                Label("P"+(i+1),bays,center+new Vector3(0,.06f,-1.2f),.1f);
            }
            driver.Hud=BuildHud();
            assembly.BakeAndValidate();localScenario.Create(assembly,assembly.BakeAndValidate());cameras.Select(0);
            EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,scenePath);
            if(!EditorBuildSettings.scenes.Any(s=>s.path==scenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(scenePath,true)}).ToArray();
            if(!minimal)CreateMinimal(asset);Debug.Log("EP03 generated: "+asset.Modules.Count(m=>!string.IsNullOrEmpty(m.Module.RecipeId))+" recipe modules, "+asset.Routes.Length+" functional routes. Layout: "+asset.Version+".");
        }
        private static Vector3 Position(FactoryLayoutAsset asset,FactoryPlacement p)=>new Vector3((p.Grid.x+p.OccupiedSize.x*.5f)*asset.GridMeters,0,(p.Grid.y+p.OccupiedSize.y*.5f)*asset.GridMeters)+p.PositionOffset;
        private static void CreateDefaultLayout(FactoryLayoutAsset asset,Vector3[] entries)
        {
            asset.Recipes=FactoryCatalog.Recipes();var placements=new List<FactoryPlacement>();
            for(var i=0;i<3;i++)
            {
                var type=8+i;var d=new FactoryModuleDefinition{Id="warehouse-"+type,Material=type,Warehouse=true,Capacity=i==0?256:128,InitialStock=i==0?64:32,RefillQuantity=i==0?32:16};
                placements.Add(Placement(d,new Vector2Int(2+i*9,52),new Vector2Int(6,8),null));
            }
            for(var i=0;i<6;i++)
            {
                var r=asset.Recipes[i];for(var n=0;n<4;n++)placements.Add(Placement(new FactoryModuleDefinition{Id=r.Id+"-"+n,RecipeId=r.Id},i<3?new Vector2Int(2+i*9,28+n*5):new Vector2Int(2+n*7,3+(i-3)*7),i<3?new Vector2Int(4,4):new Vector2Int(6,4),r));
                placements.Add(Placement(new FactoryModuleDefinition{Id="hub-"+r.Output,Material=r.Output,Capacity=32},new Vector2Int(30,4+i*9),new Vector2Int(1,2),null));
            }
            for(var i=0;i<6;i++)
            {
                var r=asset.Recipes[6+i];placements.Add(Placement(new FactoryModuleDefinition{Id=r.Id+"-0",RecipeId=r.Id},new Vector2Int(35,52-i*9),new Vector2Int(8,6),r));
                var world=asset.AssemblyOrigin+entries[i];var grid=new Vector2Int(Mathf.FloorToInt(world.x/1.2f),Mathf.FloorToInt(world.z/1.2f));
                var lanePart=new[]{3,6,7,4,5,2}[i];var inlet=Placement(new FactoryModuleDefinition{Id="assembly-in-"+lanePart,Material=lanePart,Capacity=4,AssemblyLane=i},grid,Vector2Int.one,null);
                // Match the inlet's input port exactly to the existing belt entrance.
                var local=inlet.Prefab.GetComponent<FactoryModuleAuthoring>().Ports.First(p=>!p.Output).Anchor.localPosition;
                inlet.PositionOffset=world-new Vector3((grid.x+.5f)*1.2f,0,(grid.y+.5f)*1.2f)-local;placements.Add(inlet);
            }
            asset.Modules=placements.ToArray();
            asset.Regions=new[]{Region("01 原料仓储",2,50,26,12),Region("02 基础加工",2,26,26,22),Region("03 零件制造",2,2,26,22),Region("04 六类部件装配",32,4,12,56),Region("05 六工位总装",48,32,33,26),Region("06 检测出库",82,32,9,30),Region("07 扩建预留",48,2,42,28,true)};
        }
        private static FactoryRegion Region(string name,int x,int z,int w,int d,bool reserved=false)=>new FactoryRegion{Name=name,Bounds=new RectInt(x,z,w,d),Reserved=reserved};
        private static FactoryPlacement Placement(FactoryModuleDefinition d,Vector2Int grid,Vector2Int size,FactoryRecipe recipe)
        {
            var key=recipe?.Id??(d.Warehouse?"warehouse-":"buffer-")+d.Material;var path=Root+"/Prefabs/"+key+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)
            {
                var go=new GameObject(key);var a=go.AddComponent<FactoryModuleAuthoring>();a.InstanceId=key;a.Footprint=size;
                a.FunctionalBounds=go.AddComponent<BoxCollider>();a.FunctionalBounds.isTrigger=true;a.FunctionalBounds.center=new Vector3(0,.8f,0);a.FunctionalBounds.size=new Vector3(size.x*1.2f,1.6f,size.y*1.2f);
                a.Visual=new GameObject("Visual").transform;a.Visual.SetParent(go.transform,false);
                var mat=palette[recipe?.Output??d.Material];var w=size.x*1.2f;var depth=size.y*1.2f;
                Box("Base",a.Visual,new Vector3(0,.25f,0),new Vector3(w*.9f,.5f,depth*.9f),Mat("MachineSteel",new Color(.22f,.29f,.34f)));
                if(d.Warehouse)
                {
                    for(var x=-1;x<=1;x++)for(var z=-2;z<=2;z++)Box("Raw Bin",a.Visual,new Vector3(x*w*.27f,.9f,z*depth*.16f),new Vector3(w*.22f,.8f,depth*.13f),mat);
                    foreach(var side in new[]{-1,1})Box("Rack",a.Visual,new Vector3(side*w*.43f,1.7f,0),new Vector3(.13f,3.4f,depth*.9f),mat);
                }
                else if(recipe!=null)
                {
                    if(recipe.Output==11){Box("Press Columns",a.Visual,new Vector3(-w*.24f,1.6f,0),new Vector3(.5f,2.7f,depth*.7f),mat);Box("Press Beam",a.Visual,new Vector3(0,2.9f,0),new Vector3(w*.7f,.5f,depth*.6f),mat);}
                    else if(recipe.Output==12){var c=GameObject.CreatePrimitive(PrimitiveType.Cylinder);UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());c.transform.SetParent(a.Visual,false);c.transform.localPosition=new Vector3(0,1.2f,0);c.transform.localRotation=Quaternion.Euler(90,0,0);c.transform.localScale=new Vector3(1.5f,depth*.35f,1.5f);c.GetComponent<Renderer>().sharedMaterial=mat;}
                    else {Box("Machine Enclosure",a.Visual,new Vector3(0,1.25f,0),new Vector3(w*.58f,1.7f,depth*.65f),mat);Box("Process Window",a.Visual,new Vector3(0,1.4f,-depth*.34f),new Vector3(w*.42f,.65f,.06f),Mat("Window",new Color(.06f,.13f,.18f)));}
                    if(recipe.Output<8)
                    {
                        var definition=AssetDatabase.FindAssets("t:RobotPartDefinition",new[]{"Assets/_AssemblyZero/EP02"}).Select(g=>AssetDatabase.LoadAssetAtPath<RobotPartDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(p=>p.PartTypeId==recipe.Output);
                        var part=UnityEngine.Object.Instantiate(definition.WrapperPrefab,a.Visual);part.transform.localPosition=new Vector3(0,2.3f,0);
                    }
                }
                else Box("Finite Buffer",a.Visual,new Vector3(0,.7f,0),new Vector3(w*.7f,.7f,depth*.7f),mat);
                var portList=new List<FactoryPortAuthoring>();var inputs=recipe?.Inputs.Select(i=>i.Material).ToArray()??new[]{d.Material};
                for(var i=0;i<inputs.Length;i++)portList.Add(Port(go.transform,"in-"+inputs[i],inputs[i],false,new Vector3(-w*.5f,.7f,(i+1)*depth/(inputs.Length+1)-depth*.5f)));
                portList.Add(Port(go.transform,"out",recipe?.Output??d.Material,true,new Vector3(w*.5f,.7f,0)));a.Ports=portList.ToArray();
                a.Status=Box("Functional Status",go.transform,new Vector3(0,3.3f,0),new Vector3(.3f,.3f,.3f),Mat("Status",Color.green)).GetComponent<Renderer>();a.Label=Label(recipe?.Name??EP03SimulationDriver.MaterialName(d.Material),go.transform,new Vector3(0,3.6f,0),.12f);
                prefab=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
            }
            return new FactoryPlacement{Module=d,Grid=grid,Footprint=size,Prefab=prefab};
        }
        private static FactoryPortAuthoring Port(Transform root,string id,int material,bool output,Vector3 p)
        {
            var anchor=new GameObject(id).transform;anchor.SetParent(root,false);anchor.localPosition=p;anchor.localRotation=Quaternion.LookRotation(output?Vector3.right:Vector3.left);
            Transform Reference(string name){var t=new GameObject(name).transform;t.SetParent(anchor,false);return t;}
            return new FactoryPortAuthoring{Id=id,Material=material,Output=output,Anchor=anchor,Pickup=Reference("Pickup"),Place=Reference("Place"),Grip=Reference("Grip")};
        }
        private static void CreateConnections(FactoryLayoutAsset asset,List<FactoryModuleAuthoring> views)
        {
            var connections=new List<FactoryRoutePlacement>();var byId=views.ToDictionary(v=>v.InstanceId);
            void Connect(string from,string to,int material)
            {
                var start=byId[from].Ports.Single(p=>p.Output).Anchor.position;var end=byId[to].Ports.Single(p=>!p.Output&&p.Material==material).Anchor.position;
                connections.Add(new FactoryRoutePlacement{Connection=new FactoryConnection{Id=from+"--"+to,From=from,To=to,Material=material},Points=new[]{start,end}});
            }
            foreach(var p in asset.Modules.Where(m=>!string.IsNullOrEmpty(m.Module.RecipeId)))
            {
                var recipe=asset.Recipes.Single(r=>r.Id==p.Module.RecipeId);
                foreach(var input in recipe.Inputs)Connect(input.Material<=10?"warehouse-"+input.Material:"hub-"+input.Material,p.Module.Id,input.Material);
                Connect(p.Module.Id,recipe.Output<8?"assembly-in-"+recipe.Output:"hub-"+recipe.Output,recipe.Output);
            }
            asset.Routes=connections.ToArray();EP03GroundRouter.Bake(asset);
        }
        [MenuItem("Tools/Assembly Zero/Bake EP03 Routes from Layout Interfaces")]
        public static void RebakeRoutes()
        {
            foreach(var name in new[]{"FactoryFull","FactoryMinimal"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(Root+"/Configs/"+name+".asset");
                if(asset==null)continue;
                EP03GroundRouter.Bake(asset);asset.Version="Candidate";asset.FrozenInterfaceSignature="";
                asset.Validate();EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Assembly Zero/Replan EP03 from Reference Layout")]
        public static void ReplanReference()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and stop Play first.");
            palette.Clear();for(var i=0;i<17;i++)palette.Add(Mat("Material-"+i,Color.HSVToRGB((i*.137f)%1,.65f,.85f)));
            foreach(var name in new[]{"FactoryFull","FactoryMinimal"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(Root+"/Configs/"+name+".asset");
                asset.Version="Candidate";asset.FrozenInterfaceSignature="";asset.AssemblyOrigin=new Vector3(60,0,54);
                // The six feed entrances are below the horizontal assembly stations.
                var entries=Enumerable.Range(0,6).Select(i=>new Vector3(i*4.8f-3.6f,.18f,-4.5f)).ToArray();
                CreateDefaultLayout(asset,entries);
                if(name=="FactoryMinimal")asset.Modules=asset.Modules.Where(m=>string.IsNullOrEmpty(m.Module.RecipeId)||m.Module.Id.EndsWith("-0",StringComparison.Ordinal)).ToArray();
                asset.Routes=Array.Empty<FactoryRoutePlacement>();EditorUtility.SetDirty(asset);
            }
            RefineGrayboxes();AssetDatabase.SaveAssets();BuildScene();BuildMinimal();EditorSceneManager.OpenScene(ScenePath);
        }
        private static void RefineGrayboxes()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var go=PrefabUtility.LoadPrefabContents(path);var module=go.GetComponent<FactoryModuleAuthoring>();if(module==null){PrefabUtility.UnloadPrefabContents(go);continue;}
                var recipe=FactoryCatalog.Recipes().FirstOrDefault(r=>r.Id==go.name);
                if(recipe!=null)
                {
                    foreach(var renderer in module.Visual.GetComponentsInChildren<Renderer>())if(renderer.name=="Machine Enclosure")renderer.sharedMaterial=Mat("MachineSteel",new Color(.22f,.29f,.34f));
                    var trim=module.Visual.Find("Material Identification");if(trim==null)Box("Material Identification",module.Visual,new Vector3(0,.55f,-module.Footprint.y*.48f),new Vector3(module.Footprint.x*.9f,.18f,.08f),palette[recipe.Output]);
                    if(recipe.Output<8)
                    {
                        foreach(var t in module.Visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="Machine Enclosure"||t.name=="Process Window").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                        foreach(Transform t in module.Visual)if(t.GetComponent<PartVisualAnchor>()!=null){t.localPosition=new Vector3(0,.85f,0);t.localScale=Vector3.one*2.2f;}
                        if(module.Visual.Find("Cell Worktable")==null)Box("Cell Worktable",module.Visual,new Vector3(0,.65f,0),new Vector3(4,.3f,3),Mat("MachineSteel",new Color(.22f,.29f,.34f)));
                    }
                }
                module.Label.transform.localPosition=new Vector3(0,.08f,-module.Footprint.y*.48f);module.Label.characterSize=.075f;
                PrefabUtility.SaveAsPrefabAsset(go,path);PrefabUtility.UnloadPrefabContents(go);
            }
        }
        private static void StraightenAssembly(EP02LayoutAuthoring assembly,Transform root)
        {
            var old=assembly.ProductBelts;var template=old.First(p=>p.Shape==BeltPieceShape.Straight&&p.Direction==GridDirection.East);
            var line=new GameObject("Horizontal Six Station Assembly").transform;line.SetParent(root,false);
            var pieces=new BeltAuthoring[9];var flows=new List<EP02FlowSurface>();
            for(var i=0;i<pieces.Length;i++)
            {
                var go=UnityEngine.Object.Instantiate(template.gameObject,line);go.name="Assembly Segment "+i;go.transform.position=new Vector3((i-1)*4.8f,0,0);go.transform.rotation=Quaternion.identity;
                var belt=go.GetComponent<BeltAuthoring>();belt.StablePieceId=40000+i;belt.Shape=BeltPieceShape.Straight;belt.Direction=GridDirection.East;belt.StartsLine=i==0;belt.EndsLine=i==8;pieces[i]=belt;
                var surface=go.GetComponentsInChildren<Renderer>().First(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="AssemblyZero/ConveyorSurface");
                flows.Add(new EP02FlowSurface{Renderer=surface,Product=true,Segment=i,LengthMeters=4.8f,StartMeters=i*4.8f});
            }
            foreach(var parent in old.Select(p=>p.transform.parent.gameObject).Distinct().ToArray())UnityEngine.Object.DestroyImmediate(parent);
            assembly.ProductBelts=pieces;assembly.Surfaces=flows.ToArray();
            for(var i=0;i<6;i++)
            {
                var arm=assembly.Inserters[i];arm.transform.position=new Vector3(i*4.8f,0,-2);arm.transform.rotation=Quaternion.identity;arm.StationPiece=i+1;arm.RestOffset=new Vector3(0,2.4f,.55f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(arm);PrefabUtility.RecordPrefabInstancePropertyModifications(arm.transform);
                Label((i+1)+"  "+EP03SimulationDriver.MaterialName(arm.PartTypeId),root,new Vector3(i*4.8f,.1f,3),.16f);
            }
            var gantry=new GameObject("Visual - Assembly Station Gantry").transform;gantry.SetParent(root,false);
            var frame=Mat("Gantry Steel",new Color(.19f,.23f,.26f));
            Box("Station Beam",gantry,new Vector3(12,4.7f,2.1f),new Vector3(29,.3f,.35f),frame);
            for(var i=0;i<7;i++)Box("Station Post",gantry,new Vector3(i*4.8f-2.4f,2.35f,2.1f),new Vector3(.2f,4.7f,.2f),frame);
            for(var i=0;i<6;i++)
            {
                Box("Number Board",gantry,new Vector3(i*4.8f,4.35f,1.85f),new Vector3(1.2f,.7f,.12f),Mat("Number Back",new Color(.025f,.03f,.035f)));
                var number=Label((i+1).ToString(),gantry,new Vector3(i*4.8f,4.35f,1.77f),.3f);number.transform.localRotation=Quaternion.identity;number.color=new Color(1,.68f,.15f);
            }
            assembly.DisplayRoot.position=new Vector3(33.6f,0,0);
        }
        public static void BuildAll(){RebakeRoutes();BuildScene();BuildMinimal();}
        internal static bool Crosses(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            return FactoryLayoutAsset.PlanarCrosses(a,b,c,d);
        }
        private static void BuildRoute(FactoryRoutePlacement route,Transform parent)
        {
            var root=new GameObject(route.Connection.Id);root.transform.SetParent(parent,false);
            var bed=Mat("Conveyor Bed",new Color(.12f,.15f,.17f));var rail=Mat("Conveyor Rail",new Color(.38f,.42f,.44f));
            for(var i=1;i<route.Points.Length;i++)
            {
                var a=route.Points[i-1];var b=route.Points[i];var delta=b-a;if(delta.sqrMagnitude<.001f)continue;
                var segment=new GameObject("Conveyor Segment").transform;segment.SetParent(root.transform,false);segment.position=(a+b)*.5f-Vector3.up*.15f;segment.rotation=Quaternion.LookRotation(delta);
                Box("Belt",segment,Vector3.zero,new Vector3(.46f,.16f,delta.magnitude),bed);
                foreach(var side in new[]{-1,1})Box("Rail",segment,new Vector3(side*.25f,.1f,0),new Vector3(.04f,.12f,delta.magnitude),rail);
                if(delta.magnitude>1.1f)for(var d=.5f;d<delta.magnitude;d+=2.4f)
                {
                    var point=a+delta.normalized*d;Box("Support",root.transform,new Vector3(point.x,(point.y-.25f)*.5f,point.z),new Vector3(.16f,Mathf.Max(.1f,point.y-.25f),.16f),rail);
                }
            }
        }
        private static void CreateMinimal(FactoryLayoutAsset full)
        {
            var path=Root+"/Configs/FactoryMinimal.asset";if(AssetDatabase.LoadAssetAtPath<FactoryLayoutAsset>(path)!=null)return;
            var minimal=UnityEngine.Object.Instantiate(full);minimal.name="FactoryMinimal";minimal.Modules=minimal.Modules.Where(m=>string.IsNullOrEmpty(m.Module.RecipeId)||m.Module.Id.EndsWith("-0",StringComparison.Ordinal)).ToArray();
            var ids=new HashSet<string>(minimal.Modules.Select(m=>m.Module.Id));minimal.Routes=minimal.Routes.Where(r=>ids.Contains(r.Connection.From)&&ids.Contains(r.Connection.To)).ToArray();AssetDatabase.CreateAsset(minimal,path);AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Assembly Zero/Validate EP03 Factory")]
        public static void ValidateScene()
        {
            var d=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();if(d==null)throw new InvalidOperationException("Open EP03 first.");d.Layout.Validate();foreach(var m in d.Modules)m.Validate();d.AssemblyLayout.BakeAndValidate();
            var signature=Signature(d);if(!string.IsNullOrEmpty(d.Layout.FrozenInterfaceSignature)&&signature!=d.Layout.FrozenInterfaceSignature)throw new InvalidOperationException("Frozen module interface changed.");Debug.Log("EP03 layout and functional interfaces valid.");
        }
        public static string Signature(EP03SimulationDriver d)
        {
            string V(Vector3 v)=>v.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);
            var layout=d.AssemblyLayout;var baked=layout.BakeAndValidate();
            return string.Join("\n",d.Modules.OrderBy(m=>m.InstanceId,StringComparer.Ordinal).Select(m=>m.InterfaceSignature()))+"\nAssembly:"+V(layout.transform.position)+":"+V(layout.transform.eulerAngles)+"\n"+
                string.Join("\n",baked.Supply.Append(baked.Product).Select(r=>string.Join(";",r.Pieces.Select(p=>V(p.transform.position)+":"+V(p.transform.eulerAngles)))))+"\n"+
                string.Join("\n",layout.Inserters.Select(a=>a.StableId+":"+a.SocketId+":"+V(a.transform.position)+":"+V(a.transform.eulerAngles)+":"+V(a.RestOffset)+":"+a.PortWidth+":"+string.Join(",",a.PickupDistances)))+"\n"+
                string.Join("\n",layout.Sockets.Select(s=>s.StableId+":"+V(layout.SocketOffset(s.StableId))))+"\nRoutes:"+JsonUtility.ToJson(new RouteSignatures{Routes=d.Layout.Routes});
        }
        [Serializable] private sealed class RouteSignatures { public FactoryRoutePlacement[] Routes; }
        [MenuItem("Tools/Assembly Zero/Save Accepted EP03 Layout V1")]
        public static void Freeze()
        {
            ValidateScene();var d=UnityEngine.Object.FindFirstObjectByType<EP03SimulationDriver>();d.Layout.FrozenInterfaceSignature=Signature(d);d.Layout.Version="V1";EditorUtility.SetDirty(d.Layout);AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Assembly Zero/Build EP03 Windows Development Player")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/EP03");var r=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/EP03/AssemblyZero.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            Directory.CreateDirectory("Reports/EP03");File.WriteAllText("Reports/EP03/Build.txt","Target: Windows x64 Development\nUnity: "+Application.unityVersion+"\nResult: "+r.summary.result+"\nBuild seconds: "+r.summary.totalTime.TotalSeconds+"\nBytes: "+r.summary.totalSize+"\nWarnings: "+r.summary.totalWarnings+"\nErrors: "+r.summary.totalErrors+"\nOutput: Builds/EP03/AssemblyZero.exe\nBuild result does not certify standalone player frame performance.\n");
            File.AppendAllText("Reports/EP03/Build.txt",string.Join("\n",r.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content).Distinct())+"\n");
            if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("EP03 build failed: "+r.summary.result);
        }
        private static UnityEngine.UI.Text BuildHud()
        {
            var canvas=new GameObject("EP03 HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var go=new GameObject("Factory Status",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(canvas.transform,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(20,-20);rect.sizeDelta=new Vector2(550,340);go.GetComponent<UnityEngine.UI.Image>().color=new Color(.02f,.035f,.05f,.88f);
            var text=new GameObject("Text",typeof(RectTransform),typeof(UnityEngine.UI.Text));text.transform.SetParent(go.transform,false);var tr=text.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(14,10);tr.offsetMax=new Vector2(-14,-10);var t=text.GetComponent<UnityEngine.UI.Text>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");t.fontSize=20;t.color=Color.white;t.raycastTarget=false;return t;
        }
        private static EP02CameraPose Pose(string name,Vector3 position,Vector3 target,float fov)=>new EP02CameraPose{Name=name,Position=position,Euler=Quaternion.LookRotation(target-position).eulerAngles,FieldOfView=fov};
        private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        private static TextMesh Label(string value,Transform parent,Vector3 position,float size){var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(90,0,0);var t=go.AddComponent<TextMesh>();t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");t.fontSize=40;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.text=value;t.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=t.font.material;return t;}
        private static Material Mat(string name,Color color){var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);return m;}
    }
}
