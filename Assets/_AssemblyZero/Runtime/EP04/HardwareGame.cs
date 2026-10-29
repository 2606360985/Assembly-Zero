using System;
using System.IO;
using System.Linq;
using AssemblyZero.Domain.EP04;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AssemblyZero.Unity.EP04
{
    public enum HardwareInteraction { Browsing, Placing, Selected, Driving, Modal }
    public sealed partial class HardwareGame : MonoBehaviour
    {
        public EP04Config Config; public TMP_FontAsset Font; public Camera ViewCamera;
        public WorldSimulation World {get;private set;} public HardwareHud Hud {get;private set;} public HardwareFactoryView View {get;private set;}
        public HardwareInteraction Interaction; public int SelectedNode,Factory=1,Speed=1; public bool Paused;
        public BuildingKind? Placing; public bool Moving; public int Rotation; public string Message="欢迎来到星铸显卡工厂：接下首单，再放置总装、检测、包装打通产线。";
        public string Drawer=""; public int SelectedOrder=1; public string[] Notices=Array.Empty<string>();
        private double accumulator; private float uiTime,saveTime; private Vector3 cameraFocus; private bool pan; private Vector2 lastMouse; private int savedRevision=-1;private long seenFirstProduct;
        private int seenSettlements,seenEvent=-1,seenPenalties;
        public HardwareAudio Audio {get;private set;}
        public static readonly int[] Speeds={1,2,4,8};
        public string SavePath=>Path.Combine(Application.persistentDataPath,"AssemblyZero-Hardware-v3.json");
        private bool verifying;
        public bool LastActionFailed {get;private set;}
        public WorldVehicle Truck=>World.State.Vehicles.First(v=>v.Role==VehicleRole.Pickup);
        public static string Money(long cents)=>"¥"+(cents/100m).ToString("N2");
        private void Start()
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            verifying=Environment.GetCommandLineArgs().Contains("--hardware-verify");
            // Older Economy.asset content would replay the previous rules; use the catalog until the asset is refreshed.
            if(Config.Economy?.Hardware==null||Config.Economy.Hardware.Version<HardwareCatalog.ContentVersion)
            {
                Debug.LogWarning("Hardware Economy.asset is outdated; using HardwareCatalog defaults. Run Tools > Assembly Zero > Refresh Hardware Economy.");
                Config=Instantiate(Config);Config.Economy=HardwareCatalog.Create();
            }
            World=new WorldSimulation(Config.Economy,null,verifying?0:NewSeed());SyncNotifications();
            ViewCamera.backgroundColor=new Color(.32f,.41f,.4f);RenderSettings.ambientLight=new Color(.55f,.62f,.68f);
            foreach(var light in FindObjectsByType<Light>())if(light.type==LightType.Directional){light.intensity=1.25f;light.color=new Color(1,.97f,.91f);light.shadows=LightShadows.Soft;light.shadowStrength=.65f;}
            Audio=gameObject.AddComponent<HardwareAudio>();Audio.Initialize();
            Hud=gameObject.AddComponent<HardwareHud>();Hud.Initialize(this);View=gameObject.AddComponent<HardwareFactoryView>();View.Initialize(this);FocusFactory();
            InitializeLens();
            if(verifying)StartCoroutine(VerifyPlayer());
            else if(Environment.GetCommandLineArgs().Contains("--hardware-capsule"))StartCoroutine(CapsuleCommandLine());
        }
        private static int NewSeed()=>Environment.TickCount|1;
        public void CycleSpeed(){var i=Array.IndexOf(Speeds,Speed);Speed=Speeds[(i+1)%Speeds.Length];Hud?.Refresh();}
        private void Notify(string message){Message=message;Notices=Notices.Concat(new[]{message}).TakeLast(3).ToArray();}
        private void SyncNotifications(){seenSettlements=World.State.Hardware.Settlements.Count;seenEvent=World.State.Hardware.Market.Current;seenFirstProduct=World.State.Hardware.FirstProductTick;seenPenalties=World.State.Hardware.Penalties;Hud?.SnapMoney();}
        // Surfaces automatic business outcomes that happen inside simulation ticks.
        private void PollBusinessEvents()
        {
            var h=World.State.Hardware;
            if(h.Penalties>seenPenalties){Hud.ShowPenalty(h.Penalties-seenPenalties);seenPenalties=h.Penalties;}
            if(seenFirstProduct==0&&h.FirstProductTick>0){seenFirstProduct=h.FirstProductTick;HardwareAudio.Play("ready");Notify("首款产品下线！星铸 S1 已完成装配、检测和包装，入库后自动分配给订单。");}
            while(seenSettlements<h.Settlements.Count){var s=h.Settlements[seenSettlements++];Hud.ShowSettlement(s);View.CelebrateDelivery(h.Orders.FirstOrDefault(o=>o.Id==s.Order)?.Factory??Factory);Notify("交付完成："+s.Name+" · 收入 "+Money(s.Income));}
            if(seenEvent!=h.Market.Current){seenEvent=h.Market.Current;var e=World.CurrentEvent;Notify(e!=null?"市场事件「"+e.Name+"」："+e.Detail:"市场回归平稳。");Hud.FlashMarket();}
        }
        public CommandResult Submit(WorldCommand command)
        {
            command.Factory=command.Factory==0?Factory:command.Factory;World.Enqueue(command);World.FlushCommands();var r=World.LastResult;
            LastActionFailed=!r.Success;
            if(command.Kind!=CommandKind.Drive){Message=r.Message;Notices=Notices.Concat(new[]{Message}).Distinct().Reverse().Take(3).Reverse().ToArray();}
            if(command.Kind==CommandKind.Build&&r.Success){SelectedNode=r.Entity;Drawer="设备";HardwareAudio.Play("thud");View?.MarkBuilt(r.Entity);}
            else if(command.Kind==CommandKind.AcceptOrder&&r.Success)HardwareAudio.Play("stamp");
            else if(command.Kind==CommandKind.Purchase&&r.Success)HardwareAudio.Play("coin",.6f,.8f);
            else if(!r.Success&&command.Kind!=CommandKind.Drive)HardwareAudio.Play("defect",.5f);
            Hud?.Refresh();return r;
        }
        private void Update()
        {
            if(World==null)return;if(!verifying)HandleInput();
            if(!Paused){accumulator+=Time.unscaledDeltaTime*Speed;var steps=0;while(accumulator>=1d/60&&steps++<64){View.BeforeTick();World.Tick();accumulator-=1d/60;}accumulator=Math.Min(accumulator,.5);}
            PollBusinessEvents();
            var production=World.State.Nodes.Where(n=>n.Factory==Factory&&Config.Economy.Recipe(n.RecipeId)!=null).ToArray();
            Audio.SetActivity(production.Length==0?0:production.Count(n=>n.RunState==NodeState.Working)/(float)production.Length,Speed,Paused);
            UpdateCamera(Time.unscaledDeltaTime);
            View.Present((float)(accumulator*60));
            uiTime+=Time.unscaledDeltaTime;saveTime+=Time.unscaledDeltaTime;
            if(uiTime>.25f){uiTime=0;Hud.Refresh();}
            if(saveTime>=60&&!verifying&&!Showcasing){saveTime=0;Save();}
            if(savedRevision!=World.Revision){savedRevision=World.Revision;View.Rebuild();}
        }
        // Frames the built line plus the suggested empty slots so the opening layout fills the view.
        public void FocusFactory()
        {
            var f=World.Factory(Factory);var points=World.State.Nodes.Where(n=>n.Factory==Factory).Select(n=>new Vector2(n.X,n.Z)).ToList();
            if(Factory==1&&Config.Economy.Hardware.Slots!=null)points.AddRange(Config.Economy.Hardware.Slots.Select(s=>new Vector2(s.X,s.Z)));
            var c=points.Count==0?new Vector2(20,16):new Vector2(points.Average(p=>p.x),points.Average(p=>p.y));
            cameraFocus=new Vector3((f.X+c.x)*1.2f,0,(f.Z+c.y)*1.2f);ViewCamera.orthographicSize=DefaultZoom;
        }
        public void SetDrawer(string drawer){Placing=null;Moving=false;Drawer=Drawer==drawer?"":drawer;Hud.RebuildDrawer();}
        public string PlacingRecipe="";
        public void BeginBuild(BuildingKind kind)=>BeginBuild(kind,"");
        public void BeginBuild(BuildingKind kind,string recipe){if(Truck.Manual)return;Placing=kind;PlacingRecipe=recipe??"";Moving=false;Rotation=0;Drawer="";Interaction=HardwareInteraction.Placing;Hud.RebuildDrawer();}
        public void BeginMove(){var n=World.Node(SelectedNode);if(n==null)return;Placing=n.Kind;Moving=true;Rotation=n.Rotation;Drawer="";Hud.RebuildDrawer();}
        private void ResetPlacement()
        {
            LastActionFailed=false;Placing=null;Moving=false;Rotation=0;Interaction=Paused?HardwareInteraction.Modal:HardwareInteraction.Browsing;View?.HideGhost();
        }
        public void ToggleDriving()
        {
            SetDrawer("运输");
        }
        public void FocusLogistics(){FocusMine();}
        public void FocusMine(){var r=World.State.Resources[0];cameraFocus=new Vector3(r.X*1.2f,0,r.Z*1.2f);ViewCamera.orthographicSize=16;}
        public void FocusSupplier(){var h=Config.Economy.Hardware;cameraFocus=new Vector3(h.SupplierX*1.2f,0,h.SupplierZ*1.2f);ViewCamera.orthographicSize=16;}
        public void FocusTruck(){cameraFocus=new Vector3(Truck.Position.X*1.2f,0,Truck.Position.Z*1.2f);ViewCamera.orthographicSize=16;}
        public void FocusNode(int id){var n=World.Node(id);if(n==null)return;Factory=n.Factory;SelectedNode=id;var f=World.Factory(Factory);cameraFocus=new Vector3((f.X+n.X)*1.2f,0,(f.Z+n.Z)*1.2f);Drawer="设备";View.Rebuild();Hud.RebuildDrawer();}
        public void TogglePause(){pan=false;Paused=!Paused;Interaction=Paused?HardwareInteraction.Modal:Truck.Manual?HardwareInteraction.Driving:HardwareInteraction.Browsing;Hud.ShowPause(Paused);}
        public void ChangeFactory(){ResetPlacement();if(Factory==1&&World.Factory(2).Owned){Factory=2;}else Factory=1;SelectedNode=0;FocusFactory();View.Rebuild();}
        public int HoverNode; public Vector2 HoverScreen;
        public int NodeAt(int x,int z)=>World.State.Nodes.FirstOrDefault(n=>n.Factory==Factory&&Math.Abs(n.X-x)<2&&Math.Abs(n.Z-z)<2)?.Id??0;
        private void HandleInput()
        {
            var k=Keyboard.current;var m=Mouse.current;if(k==null||!cameraFocused||TextInputFocused()){pan=false;return;}
            if(k.hKey.wasPressedThisFrame)Hud.ToggleTutorial();
            if(Showcasing)
            {
                if(k.escapeKey.wasPressedThisFrame){ExitShowcase();return;}
                if(k.tabKey.wasPressedThisFrame)NextShowcaseShot();if(k.f12Key.wasPressedThisFrame)StartCoroutine(ExportCapsules());
                // Camera-only controls while presenting; no building, drawers or selection.
                MoveView(new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)),Time.unscaledDeltaTime);
                if(k.qKey.wasPressedThisFrame)RotateViewStep(-1);if(k.eKey.wasPressedThisFrame)RotateViewStep(1);
                if(m!=null&&!PointerOverUi(m.position.ReadValue()))ZoomView(m.scroll.ReadValue().y);
                return;
            }
            if(k.escapeKey.wasPressedThisFrame){if(Paused){TogglePause();return;}if(Placing.HasValue){Placing=null;Moving=false;}else if(Drawer!=""){Drawer="";Hud.RebuildDrawer();}else TogglePause();}
            if(Paused)return;
            if(k.spaceKey.wasPressedThisFrame){TogglePause();return;}
            if(k.tabKey.wasPressedThisFrame)ToggleDriving();
            if(k.rKey.wasPressedThisFrame)Rotation=(Rotation+1)%4;
            MoveView(new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)),Time.unscaledDeltaTime);
            if(k.qKey.wasPressedThisFrame)RotateViewStep(-1);
            if(k.eKey.wasPressedThisFrame)RotateViewStep(1);
            if(m==null)return;
            var pointer=m.position.ReadValue();var over=PointerOverUi(pointer);
            if(m.middleButton.wasPressedThisFrame||m.rightButton.wasPressedThisFrame){pan=!over;lastMouse=pointer;}
            if(pan&&(m.middleButton.isPressed||m.rightButton.isPressed)){PanView(pointer-lastMouse);lastMouse=pointer;return;}
            if(m.middleButton.wasReleasedThisFrame||m.rightButton.wasReleasedThisFrame){pan=false;return;}
            if(over){View.HideGhost();HoverNode=0;return;}
            ZoomView(m.scroll.ReadValue().y);
            var ray=ViewCamera.ScreenPointToRay(pointer);if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance))return;var hit=ray.GetPoint(distance);var f=World.Factory(Factory);
            var x=Mathf.RoundToInt(hit.x/1.2f)-f.X;var z=Mathf.RoundToInt(hit.z/1.2f)-f.Z;
            HoverNode=Placing.HasValue?0:NodeAt(x,z);HoverScreen=pointer;
            if(Placing.HasValue)
            {
                View.Ghost(x,z,Rotation,World.PlacementError(Factory,x,z,Moving?SelectedNode:0)==null);
                if(m.leftButton.wasPressedThisFrame){var r=Submit(new WorldCommand{Kind=Moving?CommandKind.Move:CommandKind.Build,Factory=Factory,Node=SelectedNode,Building=Placing.Value,X=x,Z=z,Rotation=Rotation,Recipe=Moving?"":PlacingRecipe});if(r.Success){Placing=null;Moving=false;Hud.RebuildDrawer();}}
            }
            else{View.HideGhost();if(m.leftButton.wasPressedThisFrame){var resource=World.State.Resources.FirstOrDefault(r=>r.Discovered&&Math.Abs(hit.x/1.2f-r.X)<3&&Math.Abs(hit.z/1.2f-r.Z)<3);if(resource!=null){Submit(new WorldCommand{Kind=CommandKind.Collect,Resource=resource.Id,TargetFactory=Factory});Drawer="运输";Hud.RebuildDrawer();return;}SelectedNode=NodeAt(x,z);Drawer=SelectedNode==0?"":"设备";Interaction=SelectedNode==0?HardwareInteraction.Browsing:HardwareInteraction.Selected;Hud.RebuildDrawer();}}
        }
        public bool Save()
        {
            if(Showcasing){Message="展示模式不保存；退出后再保存战役";return false;}
            try{World.CaptureState();Directory.CreateDirectory(Path.GetDirectoryName(SavePath));File.WriteAllText(SavePath+".tmp",JsonUtility.ToJson(World.State));if(File.Exists(SavePath))File.Replace(SavePath+".tmp",SavePath,SavePath+".bak");else File.Move(SavePath+".tmp",SavePath);Message="已保存显卡战役";return true;}catch(Exception e){Message="保存失败："+e.Message;return false;}
        }
        public bool Load()
        {
            ExitShowcase();
            foreach(var path in new[]{SavePath,SavePath+".bak"})if(File.Exists(path))try{var candidate=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(File.ReadAllText(path)));World=candidate;SyncNotifications();Notices=Array.Empty<string>();accumulator=0;saveTime=0;Speed=1;ResetPlacement();SelectedNode=0;Factory=1;Drawer="";FocusFactory();View.Rebuild();Hud.RebuildDrawer();Message=path.EndsWith(".bak")?"已恢复备份":"已继续显卡战役";return true;}catch(Exception e){Message="存档无法读取："+e.Message;}
            if(File.Exists(Path.Combine(Application.persistentDataPath,"AssemblyZero-Hardware-v2.json")))Message="旧版 v2 存档与新规则不兼容，已保留原文件；请开始新战役。";
            return false;
        }
        public void NewGame(){ExitShowcase();World=new WorldSimulation(Config.Economy,null,verifying?0:NewSeed());SyncNotifications();Notices=Array.Empty<string>();accumulator=0;saveTime=0;Speed=1;ResetPlacement();SelectedNode=0;Factory=1;Drawer="";FocusFactory();View.Rebuild();Hud.RebuildDrawer();Message="新战役开始；跟随左上角目标打通第一条产线";}
    }
}
