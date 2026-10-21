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
        }
        [TearDown] public void RestoreInputSettings(){if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}if(testSettings!=null)Object.DestroyImmediate(testSettings);}
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
                Assert.AreEqual(7,game.World.State.Nodes.Count,game.Message);
                game.BeginBuild(BuildingKind.Manufacturer);InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20)});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20),buttons=1});yield return null;yield return null;Assert.AreEqual(7,game.World.State.Nodes.Count,"HUD click must not build");
                game.Placing=null;game.TogglePause();var tick=game.World.State.Tick;yield return null;yield return null;Assert.AreEqual(tick,game.World.State.Tick);game.World.AssertConserved();
            }
            finally{InputSystem.RemoveDevice(mouse);}
        }
        [UnityTest] public IEnumerator Hardware_DrivingInputPauseAndDrawerExclusion()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                game.Speed=2;game.ToggleDriving();Assert.IsTrue(game.Truck.Manual);Assert.AreEqual(1,game.Speed);var z=game.Truck.DriveZ;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.35f);Assert.Greater(game.Truck.DriveZ,z);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;game.TogglePause();var tick=game.World.State.Tick;var position=game.Truck.DriveZ;yield return new WaitForSeconds(.1f);Assert.AreEqual(tick,game.World.State.Tick);Assert.AreEqual(position,game.Truck.DriveZ);
                game.TogglePause();game.ToggleDriving();Assert.IsFalse(game.Truck.Manual);Assert.AreEqual(2,game.Speed);game.SetDrawer("订单");yield return null;Assert.AreEqual("订单",game.Drawer);game.SetDrawer("采购");yield return null;Assert.AreEqual("采购",game.Drawer);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
    }
}
