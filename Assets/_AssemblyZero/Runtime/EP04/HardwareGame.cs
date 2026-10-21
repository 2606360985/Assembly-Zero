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
        public EP04Config Config; public TMP_FontAsset Font; public Camera ViewCamera; public GameObject MachineVisual,ArmVisual;
        public WorldSimulation World {get;private set;} public HardwareHud Hud {get;private set;} public HardwareFactoryView View {get;private set;}
        public HardwareInteraction Interaction; public int SelectedNode,Factory=1,Speed=1; public bool Paused;
        public BuildingKind? Placing; public bool Moving; public int Rotation; public string Message="欢迎来到星铸显卡工厂：接首单，留意散热器缺口。";
        public string Drawer=""; public int SelectedOrder=1; public string[] Notices=Array.Empty<string>();
        private double accumulator; private float uiTime,saveTime; private int previousSpeed=1; private Vector3 cameraFocus; private bool pan; private Vector2 lastMouse; private int savedRevision=-1;
        public string SavePath=>Path.Combine(Application.persistentDataPath,"AssemblyZero-Hardware-v2.json");
        private bool verifying;
        public WorldVehicle Truck=>World.State.Vehicles.First(v=>v.Role==VehicleRole.Pickup);
        public static string Money(long cents)=>"¥"+(cents/100m).ToString("N2");
        private void Start()
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            World=new WorldSimulation(Config.Economy);Hud=gameObject.AddComponent<HardwareHud>();Hud.Initialize(this);View=gameObject.AddComponent<HardwareFactoryView>();View.Initialize(this);FocusFactory();
            verifying=Environment.GetCommandLineArgs().Contains("--hardware-verify");if(verifying)StartCoroutine(VerifyPlayer());
        }
        public CommandResult Submit(WorldCommand command)
        {
            command.Factory=command.Factory==0?Factory:command.Factory;World.Enqueue(command);World.FlushCommands();var r=World.LastResult;
            if(command.Kind!=CommandKind.Drive){Message=r.Message;Notices=Notices.Concat(new[]{Message}).Distinct().Reverse().Take(3).Reverse().ToArray();}
            if(command.Kind==CommandKind.Build&&r.Success){SelectedNode=r.Entity;Drawer="设备";}
            Hud?.Refresh();return r;
        }
        private void Update()
        {
            if(World==null)return;if(!verifying)HandleInput();
            if(!Paused){accumulator+=Time.unscaledDeltaTime*(Truck.Manual?1:Speed);var steps=0;while(accumulator>=1d/60&&steps++<32){World.Tick();accumulator-=1d/60;}accumulator=Math.Min(accumulator,.5);}
            if(Truck.Manual){cameraFocus=new Vector3((float)Truck.DriveX*1.2f,0,(float)Truck.DriveZ*1.2f);ViewCamera.orthographicSize=12;}
            ViewCamera.transform.position=cameraFocus+new Vector3(26,42,-29);ViewCamera.transform.rotation=Quaternion.LookRotation(cameraFocus-ViewCamera.transform.position);
            View.Present((float)(accumulator*60));
            uiTime+=Time.unscaledDeltaTime;saveTime+=Time.unscaledDeltaTime;
            if(uiTime>.25f){uiTime=0;Hud.Refresh();}
            if(saveTime>=60&&!verifying){saveTime=0;Save();}
            if(savedRevision!=World.Revision){savedRevision=World.Revision;View.Rebuild();}
        }
        public void FocusFactory(){var f=World.Factory(Factory);cameraFocus=new Vector3((f.X+20)*1.2f,0,(f.Z+16)*1.2f);ViewCamera.orthographicSize=25;}
        public void SetDrawer(string drawer){Placing=null;Moving=false;Drawer=Drawer==drawer?"":drawer;Hud.RebuildDrawer();}
        public void BeginBuild(BuildingKind kind){if(Truck.Manual)return;Placing=kind;Moving=false;Rotation=0;Drawer="";Interaction=HardwareInteraction.Placing;Hud.RebuildDrawer();}
        public void BeginMove(){var n=World.Node(SelectedNode);if(n==null)return;Placing=n.Kind;Moving=true;Rotation=n.Rotation;Drawer="";Hud.RebuildDrawer();}
        public void ToggleDriving()
        {
            if(Truck.Manual){Submit(new WorldCommand{Kind=CommandKind.ReleaseControl});Speed=previousSpeed;Interaction=HardwareInteraction.Browsing;FocusFactory();}
            else if(Submit(new WorldCommand{Kind=CommandKind.TakeControl}).Success){previousSpeed=Speed;Speed=1;Drawer="";Placing=null;Interaction=HardwareInteraction.Driving;}
            Hud.RebuildDrawer();
        }
        public void TogglePause(){Paused=!Paused;Interaction=Paused?HardwareInteraction.Modal:Truck.Manual?HardwareInteraction.Driving:HardwareInteraction.Browsing;Hud.ShowPause(Paused);}
        public void ChangeFactory(){if(Factory==1&&World.Factory(2).Owned){Factory=2;}else Factory=1;SelectedNode=0;FocusFactory();View.Rebuild();}
        private void HandleInput()
        {
            var k=Keyboard.current;var m=Mouse.current;if(k==null||m==null)return;
            var focused=EventSystem.current?.currentSelectedGameObject;
            if(focused!=null&&(focused.GetComponent<TMP_InputField>()!=null||focused.GetComponent<UnityEngine.UI.InputField>()!=null))return;
            if(k.escapeKey.wasPressedThisFrame){if(Paused){TogglePause();return;}if(Placing.HasValue){Placing=null;Moving=false;}else if(Drawer!=""){Drawer="";Hud.RebuildDrawer();}else TogglePause();}
            if(Paused)return;
            if(k.spaceKey.wasPressedThisFrame){TogglePause();return;}
            if(k.tabKey.wasPressedThisFrame)ToggleDriving();
            if(Truck.Manual)
            {
                var throttle=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);var steer=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);
                if(Truck.Throttle!=throttle||Truck.Steer!=steer)Submit(new WorldCommand{Kind=CommandKind.Drive,Throttle=throttle,Steer=steer});
                if(k.eKey.wasPressedThisFrame)Submit(new WorldCommand{Kind=CommandKind.Interact});return;
            }
            if(k.rKey.wasPressedThisFrame)Rotation=(Rotation+1)%4;
            var pointer=m.position.ReadValue();var over=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
            if(m.middleButton.wasPressedThisFrame||m.rightButton.wasPressedThisFrame){pan=!over;lastMouse=pointer;}
            if(pan&&(m.middleButton.isPressed||m.rightButton.isPressed)){var delta=pointer-lastMouse;cameraFocus+=new Vector3(-delta.x,0,-delta.y)*ViewCamera.orthographicSize/500;lastMouse=pointer;return;}
            if(m.middleButton.wasReleasedThisFrame||m.rightButton.wasReleasedThisFrame){pan=false;return;}
            if(over){View.HideGhost();return;}
            ViewCamera.orthographicSize=Mathf.Clamp(ViewCamera.orthographicSize-m.scroll.ReadValue().y*.015f,12,40);
            var ray=ViewCamera.ScreenPointToRay(pointer);if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance))return;var hit=ray.GetPoint(distance);var f=World.Factory(Factory);
            var x=Mathf.RoundToInt(hit.x/1.2f)-f.X;var z=Mathf.RoundToInt(hit.z/1.2f)-f.Z;
            if(Placing.HasValue)
            {
                View.Ghost(x,z,Rotation,World.PlacementError(Factory,x,z,Moving?SelectedNode:0)==null);
                if(m.leftButton.wasPressedThisFrame){var r=Submit(new WorldCommand{Kind=Moving?CommandKind.Move:CommandKind.Build,Factory=Factory,Node=SelectedNode,Building=Placing.Value,X=x,Z=z,Rotation=Rotation});if(r.Success){Placing=null;Moving=false;Hud.RebuildDrawer();}}
            }
            else{View.HideGhost();if(m.leftButton.wasPressedThisFrame){SelectedNode=World.State.Nodes.FirstOrDefault(n=>n.Factory==Factory&&Math.Abs(n.X-x)<2&&Math.Abs(n.Z-z)<2)?.Id??0;Drawer=SelectedNode==0?"":"设备";Interaction=SelectedNode==0?HardwareInteraction.Browsing:HardwareInteraction.Selected;Hud.RebuildDrawer();}}
        }
        public bool Save()
        {
            try{World.CaptureState();Directory.CreateDirectory(Path.GetDirectoryName(SavePath));File.WriteAllText(SavePath+".tmp",JsonUtility.ToJson(World.State));if(File.Exists(SavePath))File.Replace(SavePath+".tmp",SavePath,SavePath+".bak");else File.Move(SavePath+".tmp",SavePath);Message="已保存显卡战役";return true;}catch(Exception e){Message="保存失败："+e.Message;return false;}
        }
        public bool Load()
        {
            foreach(var path in new[]{SavePath,SavePath+".bak"})if(File.Exists(path))try{var candidate=new WorldSimulation(Config.Economy,JsonUtility.FromJson<WorldState>(File.ReadAllText(path)));World=candidate;accumulator=0;SelectedNode=0;Factory=1;Drawer="";Speed=1;FocusFactory();View.Rebuild();Hud.RebuildDrawer();Message=path.EndsWith(".bak")?"已恢复备份":"已继续显卡战役";return true;}catch(Exception e){Message="存档无法读取："+e.Message;}
            return false;
        }
        public void NewGame(){World=new WorldSimulation(Config.Economy);accumulator=0;SelectedNode=0;Factory=1;Drawer="";FocusFactory();View.Rebuild();Hud.RebuildDrawer();Message="新战役开始；赠送物料清单见产品页";}
        public void OpenLegacy(){SceneManager.LoadScene("EP04_Business");}
    }
}
