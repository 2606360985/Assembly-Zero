using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssemblyZero.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace AssemblyZero.Unity.Editor
{
    public static class EP02SceneBuilder
    {
        public const string ScenePath = "Assets/_AssemblyZero/Scenes/EP02_Demo.unity";
        private const string Root = RobotPartConfigurationBuilder.Root;
        [MenuItem("Tools/Assembly Zero/Rebuild EP02 Demo Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
            for (var i=0;i<SceneManager.sceneCount;i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes before rebuilding EP02.");
            foreach (var p in new[] {Root,Root+"/Configs",Root+"/Prefabs",Root+"/Materials"}) EnsureFolder(p);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); scene.name="EP02_Demo";
            var steel=Mat("Steel",new Color(.16f,.22f,.27f)); var dark=Mat("Dark",new Color(.035f,.06f,.085f)); var orange=Mat("SafetyOrange",new Color(1,.48f,.06f)); var cyan=Mat("SignalCyan",Color.cyan,true); var ghost=Mat("Ghost",new Color(.12f,.36f,.4f));
            var root=new GameObject("EP02 Continuous Production"); var layout=root.AddComponent<EP02LayoutAuthoring>(); layout.Parts=RobotPartConfigurationBuilder.Build(steel);
            var audit=RobotPartAudit.Inspect(); var bounds=audit[0].CommonBounds; foreach(var a in audit.Skip(1)) bounds.Encapsulate(a.CommonBounds);
            var size=bounds.size*layout.Parts[0].AssemblyPose.Scale.x; layout.ProductWidth=Mathf.Max(size.x,size.z)+.35f; layout.ProductSpacing=layout.ProductWidth+.4f;
            Primitive("Factory Floor",PrimitiveType.Cube,null,new Vector3(0,-.42f,-1f),new Vector3(29,.5f,31),dark);
            for(var x=-14;x<=14;x+=2) Primitive("Floor Grid",PrimitiveType.Cube,null,new Vector3(x,-.16f,-2),new Vector3(.025f,.01f,25),steel);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.5f,.57f,.65f);
            var light=new GameObject("Key Light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.transform.rotation=Quaternion.Euler(48,-32,0); light.shadows=LightShadows.Soft;
            var surfaces=new List<EP02FlowSurface>();
            layout.SupplyA=BuildRoute("Supply A / 32",UGrid(9,-11,6),1.2f,.8f,0,false,steel,orange,surfaces);
            layout.SupplyB=BuildRoute("Supply B / 24",UGrid(10,-12,6),1.2f,.8f,1,false,steel,cyan,surfaces);
            var productGrid=new List<Vector2Int>{new Vector2Int(-2,2),new Vector2Int(-2,1),new Vector2Int(-2,0),new Vector2Int(-2,-1),new Vector2Int(-2,-2),new Vector2Int(-1,-2),new Vector2Int(0,-2),new Vector2Int(1,-2),new Vector2Int(1,-1),new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(1,2)};
            // Center the 4.8 m modules around x=0; corner radius is 2.4 m.
            layout.ProductBelts=BuildRoute("Product Assembly / Zoned",productGrid,4.8f,layout.ProductWidth,2,true,steel,orange,surfaces,new Vector3(2.4f,0,0)); layout.Surfaces=surfaces.ToArray();
            var productRoute=new EP02Route(layout.ProductBelts); var supply=new[]{new EP02Route(layout.SupplyA),new EP02Route(layout.SupplyB)};
            var carrier=new GameObject("Product Carrier With Fixture"); Primitive("Product Pallet",PrimitiveType.Cube,carrier.transform,new Vector3(0,.07f,0),new Vector3(layout.ProductWidth,.12f,layout.ProductWidth),steel);
            Primitive("Fixture Post",PrimitiveType.Cube,carrier.transform,new Vector3(0,.8f,-layout.ProductWidth*.4f),new Vector3(.12f,1.5f,.12f),orange); Primitive("Body Support",PrimitiveType.Cube,carrier.transform,new Vector3(0,1.5f,-layout.ProductWidth*.25f),new Vector3(.65f,.1f,layout.ProductWidth*.3f),steel);
            layout.ProductCarrierPrefab=PrefabUtility.SaveAsPrefabAsset(carrier,Root+"/Prefabs/ProductCarrier.prefab"); UnityEngine.Object.DestroyImmediate(carrier);
            var armPrefab=CreateArmPrefab(steel,orange,cyan); var order=new[]{3,6,7,4,5,2}; var stationPieces=new[]{1,3,5,6,8,10}; var arms=new List<InserterAuthoring>();
            for(var i=0;i<6;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(armPrefab); go.name="I-"+i.ToString("00")+" / "+layout.Part(order[i]).PartName;
                var position=productRoute.Sample(productRoute.CenterDistance(stationPieces[i]),out var tangent); position.y=0;
                var outward=Vector3.Cross(Vector3.up,tangent); go.transform.position=position+outward*2f;
                var a=go.GetComponent<InserterAuthoring>(); a.StableId=i; a.PartTypeId=order[i]; a.SocketId=layout.Part(order[i]).SocketId; a.StationPiece=stationPieces[i];
                for(var lane=0;lane<2;lane++) a.PickupDistances[lane]=supply[lane].Nearest(position+outward*4.5f);
                a.RestOffset=go.transform.InverseTransformVector(-outward*.55f+Vector3.up*2.4f); arms.Add(a);
                WorldLabel("工位 "+(i+1)+" / "+layout.Part(order[i]).PartName,position+outward*.35f+Vector3.up*.02f,Quaternion.Euler(90,0,0));
            }
            layout.Inserters=arms.ToArray(); BuildDisplay(layout,root,steel,dark,ghost,cyan);
            var source=Primitive("Mixed Part Source",PrimitiveType.Cube,null,new Vector3(-11.4f,1.1f,8.65f),new Vector3(2.8f,2.2f,1.7f),steel);
            for(var i=0;i<6;i++) Primitive(layout.Parts[i].PartName+" Bin",PrimitiveType.Cube,source.transform,new Vector3((i%3-1)*.27f,i/3*.25f,-.55f),new Vector3(.23f,.2f,.12f),Mat(layout.Parts[i].PartName,layout.Parts[i].Color));
            layout.RejectPoint=Primitive("Part Recovery",PrimitiveType.Cube,null,new Vector3(11.4f,.6f,8.65f),new Vector3(2.8f,1.2f,1.5f),orange).transform;
            WorldLabel("供料 A / B",new Vector3(-11.4f,2.5f,7.8f),Quaternion.identity); WorldLabel("零件回收",new Vector3(11.4f,1.6f,8.7f),Quaternion.identity);
            WorldLabel("产品入口",new Vector3(-7.2f,.05f,12.6f),Quaternion.Euler(90,0,0)); WorldLabel("成品出库",new Vector3(7.2f,.05f,12.6f),Quaternion.Euler(90,0,0));
            var camera=new GameObject("EP02 Main Camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.045f,.065f); camera.nearClipPlane=.1f; camera.farClipPlane=120; camera.gameObject.AddComponent<AudioListener>();
            var director=root.AddComponent<EP02CameraDirector>(); director.Camera=camera; director.Presets=new[]{CameraPose("Overview",new Vector3(0,26,-29),new Vector3(0,0,-2),57),CameraPose("Six Axis Pickup",new Vector3(-15,7,2),new Vector3(-8.8f,1,0),55),CameraPose("Product Assembly",new Vector3(0,8,-19),new Vector3(0,1,-10),55),CameraPose("Visualization",new Vector3(0,6,-9),new Vector3(0,1,1),48),CameraPose("Head Station",new Vector3(14,7,4),new Vector3(8,1,5),55)};
            var scenario=AssetDatabase.LoadAssetAtPath<EP02ScenarioConfig>(Root+"/Configs/EP02Scenario.asset"); if(scenario==null){scenario=ScriptableObject.CreateInstance<EP02ScenarioConfig>();AssetDatabase.CreateAsset(scenario,Root+"/Configs/EP02Scenario.asset");}
            var driver=root.AddComponent<EP02SimulationDriver>();driver.Layout=layout;driver.Scenario=scenario;driver.CameraDirector=director;driver.CellPresenter=root.GetComponent<AssemblyCellPresenter>();driver.ArmPresenter=root.AddComponent<InserterPresenter>();driver.ArmPresenter.Layout=layout;driver.PartPresenter=root.AddComponent<PartVisualPresenter>();driver.PartPresenter.Layout=layout;driver.AudioPresenter=root.AddComponent<EP02AudioPresenter>();driver.AudioPresenter.Source=root.AddComponent<AudioSource>();driver.AudioPresenter.Source.playOnAwake=false;driver.Hud=BuildHud();
            var baked=layout.BakeAndValidate();scenario.Create(layout,baked);director.Select(0);EditorUtility.SetDirty(scenario);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
            Selection.activeGameObject=root;Debug.Log("EP02 continuous production built: two independent supplies, zoned product line, six industrial arms.");
        }
        private static List<Vector2Int> UGrid(int x,int bottom,int top)
        { var result=new List<Vector2Int>();for(var y=top;y>=bottom;y--)result.Add(new Vector2Int(-x,y));for(var xx=-x+1;xx<=x;xx++)result.Add(new Vector2Int(xx,bottom));for(var y=bottom+1;y<=top;y++)result.Add(new Vector2Int(x,y));return result; }
        private static BeltAuthoring[] BuildRoute(string name,List<Vector2Int> grid,float step,float width,int line,bool product,Material steel,Material rail,List<EP02FlowSurface> flows,Vector3 offset=default)
        {
            var root=new GameObject(name);var pieces=new List<BeltAuthoring>();
            for(var i=0;i<grid.Count;i++)
            {
                var go=new GameObject(name+" Segment "+i.ToString("00"));go.transform.SetParent(root.transform);go.transform.position=new Vector3(grid[i].x*step,0,grid[i].y*step)+offset;
                var entry=i==0?grid[1]-grid[0]:grid[i]-grid[i-1];var exit=i==grid.Count-1?entry:grid[i+1]-grid[i];var corner=entry!=exit;
                var a=go.AddComponent<BeltAuthoring>();a.StablePieceId=line*1000+i;a.Grid=grid[i];a.Direction=exit.x>0?GridDirection.East:exit.x<0?GridDirection.West:exit.y>0?GridDirection.North:GridDirection.South;a.Shape=corner?BeltPieceShape.Corner90:BeltPieceShape.Straight;a.StartsLine=i==0;a.EndsLine=i==grid.Count-1;pieces.Add(a);
                var sign=entry.x*exit.y-entry.y*exit.x>0?1f:-1f;var yaw=entry.x>0?0:entry.x<0?180:entry.y>0?270:90;
                var mat=ConveyorMat(name.Replace("/","_")+" "+(corner?"Corner "+sign:"Straight"),corner,sign,width / (2 * step),line);
                var surface=new GameObject("Moving Surface",typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(go.transform,false);surface.transform.localPosition=Vector3.up*.19f;surface.transform.localRotation=Quaternion.Euler(0,yaw,0);surface.transform.localScale=new Vector3(step,1,corner?step:width);surface.GetComponent<MeshFilter>().sharedMesh=SurfaceMesh();surface.GetComponent<Renderer>().sharedMaterial=mat;
                if(corner)
                {
                    for(var n=0;n<16;n++)
                    {
                        var aa=n/16f*Mathf.PI*.5f;var bb=(n+1)/16f*Mathf.PI*.5f;
                        Vector3 Arc(float angle,float radius)=>new Vector3(-step*.5f+Mathf.Sin(angle)*radius,0,sign*(step*.5f-Mathf.Cos(angle)*radius));
                        var rot=Quaternion.Euler(0,yaw,0);Segment("Corner Housing",go.transform,rot*Arc(aa,step*.5f),rot*Arc(bb,step*.5f),width+.18f,.28f,steel,0);
                        foreach(var side in new[]{-1,1})Segment("Arc Guide",go.transform,rot*Arc(aa,step*.5f+side*width*.5f),rot*Arc(bb,step*.5f+side*width*.5f),.04f,.07f,rail,.22f);
                    }
                }
                else
                {
                    var housing=Primitive("Housing",PrimitiveType.Cube,go.transform,Vector3.zero,new Vector3(step-.035f,.28f,width+.18f),steel);housing.transform.localRotation=Quaternion.Euler(0,yaw,0);
                    foreach(var side in new[]{-1,1}){var guide=Primitive("Guide",PrimitiveType.Cube,go.transform,Quaternion.Euler(0,yaw,0)*new Vector3(0,.22f,side*width*.5f),new Vector3(step-.035f,.07f,.04f),rail);guide.transform.localRotation=Quaternion.Euler(0,yaw,0);}
                }
                flows.Add(new EP02FlowSurface{Renderer=surface.GetComponent<Renderer>(),Line=line,Product=product,Segment=i});
            }
            var route=new EP02Route(pieces.ToArray());var start=0;foreach(var flow in flows.Where(f=>f.Line==line)){flow.StartMeters=start/EP02Route.UnitsPerMeter;flow.LengthMeters=(route.Ends[flow.Segment]-start)/EP02Route.UnitsPerMeter;start=route.Ends[flow.Segment];}return pieces.ToArray();
        }
        private static void Segment(string name,Transform parent,Vector3 a,Vector3 b,float width,float height,Material mat,float y)
        { var go=Primitive(name,PrimitiveType.Cube,parent,(a+b)*.5f+Vector3.up*y,new Vector3(Vector3.Distance(a,b)+.015f,height,width),mat);go.transform.localRotation=Quaternion.FromToRotation(Vector3.right,b-a); }
        private static Mesh SurfaceMesh()
        {
            var path=Root+"/Prefabs/ConveyorPlane.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh{name="Conveyor Plane"};mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Material ConveyorMat(string name,bool corner,float sign,float size,int line)
        {
            var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("AssemblyZero/ConveyorSurface"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_Corner",corner?1:0);m.SetFloat("_TurnSign",sign);m.SetFloat("_SurfaceSize",1);m.SetFloat("_BeltHalfWidth",size);
            m.SetVector("_Direction",new Vector4(1,0,0,0));m.SetColor("_StripeColor",line==1?new Color(.06f,.45f,.65f):line==2?new Color(.08f,.55f,.4f):new Color(.8f,.26f,.05f));m.SetFloat("_UseSimulationPhase",1);EditorUtility.SetDirty(m);return m;
        }
        private static GameObject CreateArmPrefab(Material steel,Material orange,Material cyan)
        {
            var root=new GameObject("Six Axis Industrial Arm");var a=root.AddComponent<InserterAuthoring>();
            Primitive("Fixed Pedestal",PrimitiveType.Cylinder,root.transform,new Vector3(0,.6f,0),new Vector3(.72f,.6f,.72f),steel);
            a.Joints=new Transform[6];Transform parent=root.transform;
            var offsets=new[]{new Vector3(0,1.25f,0),new Vector3(0,.2f,0),new Vector3(0,1.5f,0),new Vector3(0,1.5f,0),Vector3.zero,Vector3.zero};
            for(var i=0;i<6;i++)
            {
                var j=new GameObject("J"+(i+1)+" "+new[]{"Base","Shoulder","Elbow","Wrist Roll","Wrist Pitch","Flange"}[i]).transform;j.SetParent(parent,false);j.localPosition=offsets[i];a.Joints[i]=j;parent=j;
                Primitive("Joint Housing",PrimitiveType.Sphere,j,Vector3.zero,Vector3.one*(i<3?.42f:.25f),i<3?orange:steel);
                if(i==1||i==2) Primitive(i==1?"Upper Arm":"Forearm",PrimitiveType.Cube,j,new Vector3(0,.75f,0),new Vector3(i==1?.28f:.22f,1.5f,.24f),orange);
            }
            var grip=new GameObject("Tool Center Point").transform;grip.SetParent(parent,false);grip.localPosition=Vector3.up*.3f;a.Grip=grip;
            Primitive("Wrist Tool",PrimitiveType.Cylinder,parent,Vector3.up*.1f,new Vector3(.18f,.1f,.18f),steel);
            foreach(var side in new[]{-1,1})Primitive("Gripper Finger",PrimitiveType.Cube,parent,new Vector3(side*.12f,.24f,0),new Vector3(.05f,.22f,.1f),orange);
            a.StatusLight=Primitive("Status",PrimitiveType.Sphere,root.transform,new Vector3(0,1.1f,-.35f),Vector3.one*.12f,cyan).GetComponent<Renderer>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/SixAxisIndustrialArm.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        private static void BuildDisplay(EP02LayoutAuthoring layout,GameObject root,Material steel,Material dark,Material ghost,Material cyan)
        {
            var table=new GameObject("Assembly Status Visualization");table.transform.position=new Vector3(0,0,1.5f);layout.DisplayRoot=table.transform;
            Primitive("Display Base",PrimitiveType.Cylinder,table.transform,new Vector3(0,.35f,0),new Vector3(3.8f,.35f,3.8f),steel);Primitive("Display Surface",PrimitiveType.Cylinder,table.transform,new Vector3(0,.71f,0),new Vector3(3.9f,.035f,3.9f),dark);
            var sockets=new List<AssemblySocketAuthoring>();var cell=root.AddComponent<AssemblyCellPresenter>();cell.Layout=layout;cell.DisplayParts=new GameObject[6];
            foreach(var part in layout.Parts)
            {
                var go=new GameObject(part.PartName+" Display Socket");go.transform.SetParent(table.transform,false);go.transform.localPosition=part.AssemblySocketPosition;var socket=go.AddComponent<AssemblySocketAuthoring>();socket.StableId=part.SocketId;socket.PartTypeId=part.PartTypeId;
                socket.Indicator=Primitive("Indicator",PrimitiveType.Sphere,go.transform,Vector3.zero,Vector3.one*.1f,cyan).GetComponent<Renderer>();
                var preview=(GameObject)PrefabUtility.InstantiatePrefab(part.SourcePrefab);preview.transform.SetParent(go.transform,false);part.AssemblyPose.Apply(preview.transform);foreach(var r in preview.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(ghost,r.sharedMaterials.Length).ToArray();socket.Ghost=preview;
                var actual=(GameObject)PrefabUtility.InstantiatePrefab(part.SourcePrefab);actual.name=part.PartName+" Display Copy";actual.transform.SetParent(go.transform,false);part.AssemblyPose.Apply(actual.transform);actual.SetActive(false);cell.DisplayParts[part.SocketId]=actual;sockets.Add(socket);
            }
            layout.Sockets=sockets.ToArray();var ring=new List<Renderer>();for(var i=0;i<36;i++){var angle=i*Mathf.PI*2/36;var r=Primitive("Progress "+i,PrimitiveType.Cube,table.transform,new Vector3(Mathf.Sin(angle)*2,.78f,Mathf.Cos(angle)*2),new Vector3(.19f,.045f,.09f),cyan);r.transform.localRotation=Quaternion.Euler(0,i*10,0);ring.Add(r.GetComponent<Renderer>());}cell.ProgressRing=ring.ToArray();
            cell.Scanner=Primitive("Visualization Scanner",PrimitiveType.Cylinder,table.transform,Vector3.up,new Vector3(3,.006f,3),cyan).transform;cell.Scanner.gameObject.SetActive(false);
            cell.Label=WorldLabel("装配状态可视化 / 完成 0",new Vector3(0,.02f,-1.1f),Quaternion.Euler(90,0,0));
        }
        private static EP02MetricsHud BuildHud()
        {
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");var go=new GameObject("EP02 Production HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(InputSystemUIInputModule));
            var hud=go.AddComponent<EP02MetricsHud>();hud.Title=Text("Title",go.transform,font,new Vector2(28,-24),new Vector2(1300,90),28);
            var left=Panel("Metrics",go.transform,new Vector2(24,-140),new Vector2(445,350));hud.Metrics=Text("Metrics Text",left,font,new Vector2(14,-12),new Vector2(420,328),19);
            var right=Panel("Stations",go.transform,new Vector2(1520,-140),new Vector2(376,725));hud.Arms=Text("Station Text",right,font,new Vector2(14,-12),new Vector2(350,700),17);
            var bottom=Panel("Products",go.transform,new Vector2(24,-740),new Vector2(420,235));hud.Lesson=Text("Product Text",bottom,font,new Vector2(14,-12),new Vector2(396,210),18);
            Text("Controls",go.transform,font,new Vector2(28,-1000),new Vector2(1850,55),23).text="R 重播   Space 暂停   N 单步   L Head 锁定   C 镜头    /    RepinSKY · 零号装配厂";return hud;
        }
        private static RectTransform Panel(string name,Transform parent,Vector2 p,Vector2 size){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=p;r.sizeDelta=size;go.GetComponent<UnityEngine.UI.Image>().color=new Color(.025f,.055f,.08f,.85f);return r;}
        private static UnityEngine.UI.Text Text(string name,Transform parent,Font font,Vector2 p,Vector2 size,int fs){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=p;r.sizeDelta=size;var t=go.GetComponent<UnityEngine.UI.Text>();t.font=font;t.fontSize=fs;t.color=new Color(.85f,.95f,1);t.raycastTarget=false;return t;}
        private static TextMesh WorldLabel(string label,Vector3 p,Quaternion q){var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_AssemblyZero/UI/AssemblyZeroChinese.ttf");var go=new GameObject(label);go.transform.SetPositionAndRotation(p,q);var t=go.AddComponent<TextMesh>();t.font=font;t.fontSize=40;t.characterSize=.08f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.8f,.94f,1);t.text=label;go.GetComponent<MeshRenderer>().sharedMaterial=font.material;return t;}
        private static EP02CameraPose CameraPose(string name,Vector3 p,Vector3 target,float fov)=>new EP02CameraPose{Name=name,Position=p,Euler=Quaternion.LookRotation(target-p).eulerAngles,FieldOfView=fov};
        public static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 scale,Material m){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=m;return go;}
        private static Material Mat(string name,Color c,bool emission=false){var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",c);if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c);}AssetDatabase.CreateAsset(m,path);return m;}
        private static void EnsureFolder(string p){if(AssetDatabase.IsValidFolder(p))return;var parent=Path.GetDirectoryName(p).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(p));}
        public static string LastBuildResult{get;private set;}
        [MenuItem("Tools/Assembly Zero/Build EP02 Windows Development Player")]
        public static void BuildDevelopment(){Directory.CreateDirectory("Builds/EP02");var report=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/EP02/AssemblyZero.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);LastBuildResult=report.summary.result.ToString();Debug.Log("EP02 build: "+LastBuildResult);}
    }
}
