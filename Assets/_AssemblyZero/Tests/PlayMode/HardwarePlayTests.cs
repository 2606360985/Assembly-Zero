using System.Collections;
using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity.EP04;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssemblyZero.Tests
{
    public sealed class HardwarePlayTests
    {
        private InputSettings originalSettings, testSettings;private HideFlags originalFlags;
        private Keyboard fixtureKeyboard;
        [SetUp] public void RouteSyntheticInputToGame()
        {
            originalSettings=InputSystem.settings;
            if(originalSettings==null){originalSettings=ScriptableObject.CreateInstance<InputSettings>();InputSystem.settings=originalSettings;}
            originalFlags=originalSettings.hideFlags;originalSettings.hideFlags|=HideFlags.DontUnloadUnusedAsset;
            testSettings=Object.Instantiate(originalSettings);
            testSettings.hideFlags=HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=testSettings;
            if(Keyboard.current==null)fixtureKeyboard=InputSystem.AddDevice<Keyboard>();
        }
        [TearDown] public void RestoreInputSettings(){if(fixtureKeyboard!=null){InputSystem.RemoveDevice(fixtureKeyboard);fixtureKeyboard=null;}if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}if(testSettings!=null)Object.DestroyImmediate(testSettings);}
        [UnityTest] public IEnumerator Hardware_PauseModalBlocksUnderlyingOrderButtons()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.SetDrawer("订单");yield return null;
            var button=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="接单");
            var point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);game.TogglePause();yield return null;
            var results=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},results);
            Assert.IsNotEmpty(results);Assert.AreEqual("Modal blocker",results[0].gameObject.name,"Pause must intercept clicks outside the menu as well");
            var tick=game.World.State.Tick;yield return null;Assert.AreEqual(tick,game.World.State.Tick);
        }
        [UnityTest] public IEnumerator Hardware_QuietShortageOpensCorrectPurchasableMaterial()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.Paused=true;game.SelectedNode=3;
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=3,Recipe="quiet-gpu"}).Success);
            game.SetDrawer("设备");yield return null;
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="采购缺料").onClick.Invoke();yield return null;
            Assert.AreEqual("采购",game.Drawer);
            Assert.IsTrue(game.Hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("低噪散热器 ×")),"A quiet assembly shortage must select its quiet heatsink, not the non-purchasable board or standard heatsink");
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)] [TestCase(30,2)] [TestCase(60,2)] [TestCase(120,2)]
        public void Hardware_TruckSamplingMovesContinuouslyAcrossTickAndSegmentBoundaries(int fps,int speed)
        {
            var v=new WorldVehicle{Phase=VehiclePhase.Travelling,Position=new GridPoint(0,0)};
            for(var i=0;i<20;i++)v.Path.Add(new GridPoint(i,0));var previous=HardwareFactoryView.SampleTruck(v,0);
            for(var frame=1;frame<fps;frame++)
            {
                var clock=frame*60d*speed/fps;var tick=(int)clock;v.PathIndex=tick/10;v.SegmentTicks=tick%10;v.Position=v.Path[v.PathIndex];
                var current=HardwareFactoryView.SampleTruck(v,(float)(clock-tick));
                Assert.That(current.x-previous.x,Is.EqualTo(7.2f*speed/fps).Within(.0001f));previous=current;
            }
        }
        [UnityTest] public IEnumerator Hardware_EnvironmentResetsAndProductStagesAreExplicit()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.Paused=true;
            Assert.AreEqual(HardwareProductForm.Card,HardwareVisualCatalog.Get(HardwareCatalog.QuietRaw).Form);
            Assert.IsTrue(HardwareVisualCatalog.Get(HardwareCatalog.QuietRaw).Quiet);Assert.IsFalse(HardwareVisualCatalog.Get(HardwareCatalog.QuietRaw).Qualified);
            Assert.IsTrue(HardwareVisualCatalog.Get(HardwareCatalog.QuietTested).Qualified);
            var machinery=game.View.transform.Find("Factory machinery");game.View.Rebuild();Assert.AreSame(machinery,game.View.transform.Find("Factory machinery"),"A notification or exploration revision must not rebuild geometry.");
            var position=game.View.TruckVisual.position;yield return null;Assert.AreEqual(position,game.View.TruckVisual.position);
            game.BeginBuild(BuildingKind.Processor);game.Moving=true;game.NewGame();
            Assert.IsNull(game.Placing);Assert.IsFalse(game.Moving);
            Assert.IsFalse(game.View.GetComponentsInChildren<HardwareProductView>().Any(p=>p.Material==HardwareCatalog.Standard||p.Material==HardwareCatalog.Quiet||p.Material==HardwareCatalog.Tested||p.Material==HardwareCatalog.QuietTested),"No finished goods should flash during a rebuild before the first presentation refresh.");
            yield return null;Assert.AreEqual(1,game.View.transform.Cast<Transform>().Count(t=>t.name=="Hardware environment"));
            game.World.AssertConserved();
        }
        private IEnumerator Open()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/Hardware_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Hardware_Business");
#endif
            yield return null;yield return null;
        }
        [UnityTest] public IEnumerator Hardware_BuildPointerAndUiBlocking()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();Assert.IsNotNull(game);var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                game.BeginBuild(BuildingKind.Processor);var f=game.World.Factory(1);var point=game.ViewCamera.WorldToScreenPoint(new Vector3((f.X+12)*1.2f,0,(f.Z+24)*1.2f));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
                Assert.AreEqual(7,game.World.State.Nodes.Count,game.Message+" pointer="+point+" screen="+Screen.width+"x"+Screen.height+" UI="+string.Join(",",hits.Select(h=>h.gameObject.name)));
                game.BeginBuild(BuildingKind.Manufacturer);InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20)});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20),buttons=1});yield return null;yield return null;Assert.AreEqual(7,game.World.State.Nodes.Count,"HUD click must not build");
                game.Placing=null;game.TogglePause();var tick=game.World.State.Tick;yield return null;yield return null;Assert.AreEqual(tick,game.World.State.Tick);game.World.AssertConserved();
            }
            finally{InputSystem.RemoveDevice(mouse);}
        }
        [UnityTest] public IEnumerator Hardware_TransportPlanningPauseAndFrozenDrivingInput()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                game.Speed=2;game.ToggleDriving();Assert.IsFalse(game.Truck.Manual);Assert.AreEqual("运输",game.Drawer);Assert.AreEqual(2,game.Speed);var p=game.Truck.Position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.35f);Assert.AreEqual(p,game.Truck.Position);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="规划循环采集铝原料").onClick.Invoke();Assert.IsTrue(game.Truck.Loop);Assert.AreEqual(VehiclePhase.Travelling,game.Truck.Phase);
                game.TogglePause();var tick=game.World.State.Tick;var position=game.Truck.Position;yield return new WaitForSeconds(.1f);Assert.AreEqual(tick,game.World.State.Tick);Assert.AreEqual(position,game.Truck.Position);
                game.TogglePause();game.SetDrawer("订单");yield return null;Assert.AreEqual("订单",game.Drawer);game.SetDrawer("采购");yield return null;Assert.AreEqual("采购",game.Drawer);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
        [UnityTest] public IEnumerator Hardware_GuideSelectsFirstOrderAndMissingSupplies()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.Paused=true;game.Hud.FollowGuide();Assert.AreEqual("订单",game.Drawer);Assert.AreEqual(1,game.SelectedOrder);
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="接单").onClick.Invoke();yield return null;game.Hud.FollowGuide();yield return null;
            Assert.AreEqual("采购",game.Drawer);Assert.IsTrue(game.Hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("标准散热器 ×6")));
            Assert.IsTrue(game.View.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh!=null&&f.sharedMesh.name=="Hardware beveled casing"),"Technology equipment should have finished casings.");
        }
    }
}
