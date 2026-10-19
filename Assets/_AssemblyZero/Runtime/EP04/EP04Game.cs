using System;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AssemblyZero.Unity.EP04
{
    public sealed partial class EP04Game : MonoBehaviour
    {
        public EP04Config Config;
        public Camera ViewCamera;
        public WorldSimulation World { get; private set; }
        public bool Paused;
        public int GameSpeed=1,SelectedFactory=1,SelectedNode,SelectedVehicle=1,SelectedResource=1;
        public bool MapMode;
        public string Message="欢迎来到零号工厂。先放置免费仓库和销售站。";
        private BuildingKind? placing;
        private bool moving;
        private int rotation;
        private double accumulator;
        private float refreshTimer,saveTimer;
        private int seenRevision=-1;
        private GameObject ghost;
        private GridPoint ghostPoint;
        private bool validGhost;
        private bool confirmNew;
        private string SavePath=>Path.Combine(Application.persistentDataPath,"AssemblyZero-EP04-v1.json");
        public double TickMilliseconds { get; private set; }
        private readonly System.Diagnostics.Stopwatch stopwatch=new System.Diagnostics.Stopwatch();
        private void Start()
        {
            World=new WorldSimulation(Config.Economy);CreateUI();CreateEnvironment();SetView(false);RefreshUI();
            if(Environment.GetCommandLineArgs().Contains("--ep04-smoke"))StartCoroutine(PlayerSmoke());
        }
        public void Submit(WorldCommand command)
        {
            World.Enqueue(command);World.FlushCommands();Message=World.LastResult.Message;
            if(command.Kind==CommandKind.Build&&World.LastResult.Success)SelectedNode=World.LastResult.Entity;
            RefreshUI();
        }
        private void Update()
        {
            if(World==null)return;
            var keyboard=Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame){placing=null;moving=false;DestroyGhost();}
                if(keyboard.spaceKey.wasPressedThisFrame)Paused=!Paused;
                if(keyboard.rKey.wasPressedThisFrame){rotation=(rotation+1)%4;UpdateGhost();}
                if(keyboard.tabKey.wasPressedThisFrame)SetView(!MapMode);
            }
            if(!Paused)
            {
                accumulator+=Time.unscaledDeltaTime*GameSpeed;var steps=0;stopwatch.Restart();
                while(accumulator>=1d/60&&steps++<32){World.Tick();accumulator-=1d/60;}
                TickMilliseconds=stopwatch.Elapsed.TotalMilliseconds;accumulator=Math.Min(accumulator,.5);
            }
            HandlePointer();
            if(World.Revision!=seenRevision){seenRevision=World.Revision;RebuildFactoryViews();}
            refreshTimer+=Time.unscaledDeltaTime;saveTimer+=Time.unscaledDeltaTime;
            if(refreshTimer>.15f){refreshTimer=0;PresentDynamic();RefreshUI();}
            if(saveTimer>60){saveTimer=0;Save(false);}
        }
        public void SetView(bool map)
        {
            MapMode=map;placing=null;moving=false;DestroyGhost();
            var site=World.Factory(SelectedFactory);var center=map?new Vector3(69,0,51):new Vector3((site.X+20)*1.2f,0,(site.Z+16)*1.2f);
            ViewCamera.orthographic=true;ViewCamera.orthographicSize=map?62:27;
            ViewCamera.transform.position=center+new Vector3(0,70,-38);ViewCamera.transform.rotation=Quaternion.LookRotation(center-ViewCamera.transform.position);
            RebuildFactoryViews();RefreshUI();
        }
        private void SelectFactory(int id)
        {
            if(World.Factory(id)?.Owned!=true){Message="请在世界地图发现并购买第二厂址";return;}
            SelectedFactory=id;SelectedNode=0;SetView(false);
        }
        private void BeginBuild(BuildingKind kind){if(MapMode)SetView(false);placing=kind;moving=false;rotation=0;Message="点击厂区放置 · R 旋转 · Esc 取消";}
        private void BeginMove(){var n=World.Node(SelectedNode);if(n==null)return;placing=n.Kind;moving=true;rotation=n.Rotation;Message="选择新位置；仓库需能容纳线路回收物料";}
        private void HandlePointer()
        {
            var mouse=Mouse.current;if(mouse==null)return;
            if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject()){if(ghost!=null)ghost.SetActive(false);return;}
            var ray=ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var enter))return;
            var point=ray.GetPoint(enter);
            if(placing.HasValue&&!MapMode)
            {
                var f=World.Factory(SelectedFactory);var grid=new GridPoint(Mathf.RoundToInt(point.x/1.2f)-f.X,Mathf.RoundToInt(point.z/1.2f)-f.Z);
                if(!grid.Equals(ghostPoint)||ghost==null){ghostPoint=grid;UpdateGhost();}else ghost.SetActive(true);
                if(mouse.leftButton.wasPressedThisFrame)
                {
                    if(!validGhost){Message=World.PlacementError(SelectedFactory,grid.X,grid.Z,moving?SelectedNode:0);return;}
                    Submit(new WorldCommand{Kind=moving?CommandKind.Move:CommandKind.Build,Factory=SelectedFactory,Node=SelectedNode,Building=placing.Value,X=grid.X,Z=grid.Z,Rotation=rotation});
                    if(World.LastResult.Success){placing=null;moving=false;DestroyGhost();}
                }
                return;
            }
            if(!mouse.leftButton.wasPressedThisFrame)return;
            if(MapMode)
            {
                var resource=World.State.Resources.Where(r=>r.Discovered).FirstOrDefault(r=>Vector2.Distance(new Vector2(point.x,point.z),new Vector2(r.X*1.2f,r.Z*1.2f))<4);
                if(resource!=null){SelectedResource=resource.Id;Message="已选择 "+EconomySettings.MaterialName(resource.Material)+"，点击采集按钮派遣车辆";RefreshUI();return;}
                var vehicle=World.State.Vehicles.FirstOrDefault(v=>Vector2.Distance(new Vector2(point.x,point.z),new Vector2(v.Position.X*1.2f,v.Position.Z*1.2f))<2);
                if(vehicle!=null){SelectedVehicle=vehicle.Id;return;}
                Submit(new WorldCommand{Kind=CommandKind.Explore,Vehicle=SelectedVehicle,X=Mathf.RoundToInt(point.x/1.2f),Z=Mathf.RoundToInt(point.z/1.2f)});
            }
            else
            {
                var f=World.Factory(SelectedFactory);SelectedNode=World.State.Nodes.Where(n=>n.Factory==SelectedFactory).FirstOrDefault(n=>Mathf.Abs(point.x/1.2f-f.X-n.X)<2&&Mathf.Abs(point.z/1.2f-f.Z-n.Z)<2)?.Id??0;
                RefreshUI();
            }
        }
        private void UpdateGhost()
        {
            DestroyGhost();if(!placing.HasValue||MapMode)return;
            validGhost=World.PlacementError(SelectedFactory,ghostPoint.X,ghostPoint.Z,moving?SelectedNode:0)==null;
            var f=World.Factory(SelectedFactory);ghost=new GameObject("Placement preview");
            var position=new Vector3((f.X+ghostPoint.X)*1.2f,.7f,(f.Z+ghostPoint.Z)*1.2f);
            Box("占地预览",position,new Vector3(3.4f,1.4f,3.4f),validGhost?new Color(.15f,.65f,.48f):new Color(.8f,.2f,.2f),ghost.transform);
            var output=new BusinessNode{X=ghostPoint.X,Z=ghostPoint.Z,Rotation=rotation}.Port(true);
            Box("输出端口",new Vector3((f.X+output.X)*1.2f,.8f,(f.Z+output.Z)*1.2f),Vector3.one*.45f,Color.yellow,ghost.transform);
            // Preview one relevant incoming route per frame-position update, never rebake the factory.
            var material=placing==BuildingKind.Manufacturer?8:placing==BuildingKind.Processor?9:17;
            var source=World.State.Nodes.Where(n=>n.Factory==SelectedFactory&&n.Id!=(moving?SelectedNode:0)&&(n.Kind==BuildingKind.Warehouse||Config.Economy.Recipe(n.RecipeId)?.Output==material)).OrderBy(n=>GridPoint.Distance(n.Center,ghostPoint)).FirstOrDefault();
            if(validGhost&&source!=null)
            {
                var target=new BusinessNode{Id=-1,X=ghostPoint.X,Z=ghostPoint.Z,Rotation=rotation};
                var path=GridRouting.FactoryPath(source,target,World.State.Nodes.Where(n=>n.Factory==SelectedFactory&&n.Id!=(moving?SelectedNode:0)).Concat(new[]{target}),World.State.Routes.Where(r=>r.Factory==SelectedFactory&&r.From!=(moving?SelectedNode:0)&&r.To!=(moving?SelectedNode:0)));
                if(path!=null)DrawLine(path.Select(p=>LocalPoint(f,p)).ToArray(),new Color(.35f,1,.65f),.16f,ghost.transform,"预计连接");
            }
        }
        private void DestroyGhost(){if(ghost!=null)Destroy(ghost);ghost=null;}
        public bool Save(bool feedback=true)
        {
            try
            {
                World.CaptureState();var json=JsonUtility.ToJson(World.State);var path=SavePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",json);
                if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
                if(feedback)Message="已保存（包括加工与运输进度）";return true;
            }
            catch(Exception e){Message="保存失败："+e.Message;Debug.LogError(Message);return false;}
        }
        public bool Load()
        {
            Exception problem=null;
            foreach(var path in new[]{SavePath,SavePath+".bak"})
            {
                if(!File.Exists(path))continue;
                try
                {
                    var candidate=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(File.ReadAllText(path)));
                    World=candidate;SelectedFactory=1;SelectedNode=0;SelectedVehicle=1;accumulator=0;seenRevision=-1;SetView(false);Message=path.EndsWith(".bak")?"已恢复备用存档":"已继续游戏";return true;
                }
                catch(Exception e){problem=e;}
            }
            Message=problem==null?"尚无存档":"存档读取失败，当前游戏保留："+problem.Message;return false;
        }
        private void NewGame()
        {
            if(!confirmNew){confirmNew=true;Message="再次点击新游戏将重新开始；旧存档会保留到下次保存";return;}
            confirmNew=false;World=new WorldSimulation(Config.Economy);SelectedFactory=1;SelectedNode=0;SelectedVehicle=1;accumulator=0;seenRevision=-1;SetView(false);Message="新游戏已开始";
        }
    }
}
