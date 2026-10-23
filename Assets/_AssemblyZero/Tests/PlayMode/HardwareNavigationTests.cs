using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity.EP04;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AssemblyZero.Tests
{
    public sealed class HardwareNavigationTests
    {
        private HardwareGame game;
        private bool preferenceExisted;
        private int preference;
        private InputSettings originalSettings,testSettings;
        private HideFlags originalFlags;
        private Keyboard keyboard;
        private Mouse mouse;
        [SetUp] public void Setup()
        {
            preferenceExisted=PlayerPrefs.HasKey(HardwareHud.TutorialPreference);preference=PlayerPrefs.GetInt(HardwareHud.TutorialPreference,1);
            originalSettings=InputSystem.settings;
            if(originalSettings==null){originalSettings=ScriptableObject.CreateInstance<InputSettings>();InputSystem.settings=originalSettings;}
            originalFlags=originalSettings.hideFlags;originalSettings.hideFlags|=HideFlags.DontUnloadUnusedAsset;
            testSettings=Object.Instantiate(originalSettings);testSettings.hideFlags=HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings=testSettings;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
        }
        [TearDown] public void Cleanup()
        {
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}
            if(testSettings!=null)Object.DestroyImmediate(testSettings);
            if(preferenceExisted)PlayerPrefs.SetInt(HardwareHud.TutorialPreference,preference);else PlayerPrefs.DeleteKey(HardwareHud.TutorialPreference);PlayerPrefs.Save();
        }
        private IEnumerator Open()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/Hardware_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Hardware_Business");
#endif
            yield return null;yield return null;game=Object.FindFirstObjectByType<HardwareGame>();game.SendMessage("OnApplicationFocus",true);game.Hud.SetTutorialEnabled(true);
        }
        private Button Action(string id)=>game.Hud.GetComponentsInChildren<Button>().First(b=>b.name==id);
        [UnityTest] public IEnumerator OrbitAnimatesAccumulatesAndReturnsToOrigin()
        {
            yield return Open();var center=game.CameraFocus;var original=game.ViewCamera.transform.position;var rotation=game.ViewCamera.transform.rotation;
            var radius=Vector3.Distance(original,center);var height=original.y;game.RotateViewStep(1);yield return null;
            Assert.That(game.CameraYaw,Is.InRange(.001f,44.999f));
            game.RotateViewStep(1);yield return new WaitForSecondsRealtime(.3f);Assert.That(game.CameraYaw,Is.EqualTo(90).Within(.001f));
            for(var i=0;i<6;i++)game.RotateViewStep(1);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(game.CameraYaw,Is.EqualTo(360).Within(.001f));Assert.That(Vector3.Distance(original,game.ViewCamera.transform.position),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation,game.ViewCamera.transform.rotation),Is.LessThan(.01f));Assert.AreEqual(center,game.CameraFocus);
            Assert.That(game.ViewCamera.transform.position.y,Is.EqualTo(height).Within(.001f));Assert.That(Vector3.Distance(game.ViewCamera.transform.position,center),Is.EqualTo(radius).Within(.001f));
            game.Speed=2;game.RotateViewStep(-1);yield return new WaitForSecondsRealtime(.3f);Assert.That(game.CameraYaw,Is.EqualTo(315).Within(.001f));
        }
        [UnityTest] public IEnumerator MovementZoomInputAndRotatedPicking()
        {
            yield return Open();var start=game.CameraFocus;game.MoveView(Vector2.right,.05f);var straight=Vector3.Distance(start,game.CameraFocus);game.FocusGroundPoint(start);
            game.MoveView(Vector2.one,.05f);Assert.That(Vector3.Distance(start,game.CameraFocus),Is.EqualTo(straight).Within(.0001f));game.FocusGroundPoint(start);
            game.ZoomView(120);Assert.That(game.ViewCamera.orthographicSize,Is.EqualTo(22.3f).Within(.001f));game.ZoomView(10000);Assert.AreEqual(12,game.ViewCamera.orthographicSize);game.ZoomView(-10000);Assert.AreEqual(40,game.ViewCamera.orthographicSize);game.FocusFactory();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.12f);Assert.Greater(Vector3.Dot(game.CameraFocus-start,Vector3.ProjectOnPlane(game.ViewCamera.transform.forward,Vector3.up)),0);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            for(var direction=0;direction<8;direction++)
            {
                var before=game.CameraFocus;var right=Vector3.ProjectOnPlane(game.ViewCamera.transform.right,Vector3.up).normalized;game.PanView(new Vector2(50,0));Assert.Less(Vector3.Dot(game.CameraFocus-before,right),0);
                game.FocusFactory();var f=game.World.Factory(1);var n=game.World.State.Nodes.First(x=>x.Kind==BuildingKind.GPUAssembly);
                var world=new Vector3((f.X+n.X)*1.2f,0,(f.Z+n.Z)*1.2f);var ray=game.ViewCamera.ScreenPointToRay(game.ViewCamera.WorldToScreenPoint(world));Assert.IsTrue(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance));Assert.Less(Vector3.Distance(world,ray.GetPoint(distance)),.001f);
                game.RotateViewStep(1);yield return new WaitForSecondsRealtime(.28f);
            }
        }
        [UnityTest] public IEnumerator InputFocusPauseAndUiPreventUnwantedNavigation()
        {
            yield return Open();var start=game.CameraFocus;game.TogglePause();game.MoveView(Vector2.one,.05f);game.RotateViewStep(1);game.ZoomView(120);Assert.AreEqual(start,game.CameraFocus);Assert.AreEqual(0,game.TargetCameraYaw);Assert.AreEqual(25,game.ViewCamera.orthographicSize);game.TogglePause();
            game.SendMessage("OnApplicationFocus",false);game.MoveView(Vector2.one,.05f);Assert.AreEqual(start,game.CameraFocus);game.SendMessage("OnApplicationFocus",true);
            var input=new GameObject("Test input",typeof(RectTransform),typeof(TMP_InputField));input.transform.SetParent(game.Hud.GetComponentInChildren<Canvas>().transform,false);EventSystem.current.SetSelectedGameObject(input);
            game.MoveView(Vector2.one,.05f);game.RotateViewStep(1);Assert.AreEqual(start,game.CameraFocus);Assert.AreEqual(0,game.TargetCameraYaw);EventSystem.current.SetSelectedGameObject(null);Object.Destroy(input);
            var p=RectTransformUtility.WorldToScreenPoint(null,game.Hud.GetComponentInChildren<HardwareMinimap>().transform.position);InputSystem.QueueStateEvent(mouse,new MouseState{position=p,scroll=new Vector2(0,120)});yield return null;yield return null;Assert.AreEqual(25,game.ViewCamera.orthographicSize);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.H));yield return null;yield return null;Assert.IsFalse(game.Hud.TutorialEnabled);
        }
        [UnityTest] public IEnumerator MinimapClickMovesViewWithoutBusinessSideEffects()
        {
            yield return Open();var map=game.Hud.GetComponentInChildren<HardwareMinimap>();var factory=game.Factory;var zoom=game.ViewCamera.orthographicSize;var money=game.World.State.Money;var nodes=game.World.State.Nodes.Count;var phase=game.Truck.Phase;
            game.BeginBuild(BuildingKind.Processor);
            foreach(var uv in new[]{Vector2.zero,Vector2.one,new Vector2(12f/115,72f/87),new Vector2(36f/115,64f/87),new Vector2(.2f,.2f)})
            {
                var rect=map.rectTransform;var local=new Vector2(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,uv.x),Mathf.Lerp(rect.rect.yMin,rect.rect.yMax,uv.y));
                var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local));
                ExecuteEvents.Execute(map.gameObject,new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Assert.Less(Vector3.Distance(game.CameraFocus,HardwareMinimap.MapToWorld(uv)),.001f);
            }
            var clickPoint=RectTransformUtility.WorldToScreenPoint(null,map.rectTransform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=clickPoint});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=clickPoint,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=clickPoint});yield return null;
            Assert.Less(Vector3.Distance(game.CameraFocus,HardwareMinimap.MapToWorld(new Vector2(.5f,.5f))),.001f);
            Assert.AreEqual(factory,game.Factory);Assert.AreEqual(zoom,game.ViewCamera.orthographicSize);Assert.AreEqual(money,game.World.State.Money);Assert.AreEqual(nodes,game.World.State.Nodes.Count);Assert.AreEqual(phase,game.Truck.Phase);Assert.AreEqual(BuildingKind.Processor,game.Placing);
            game.TogglePause();var stopped=game.CameraFocus;map.Navigate(Vector2.one);Assert.AreEqual(stopped,game.CameraFocus);
        }
        [UnityTest] public IEnumerator TutorialFollowsRealOrderAndClearsAfterDelivery()
        {
            yield return Open();game.Paused=true;yield return null;Assert.AreEqual("nav:订单",game.Hud.CurrentGuideTarget);
            game.Hud.FollowGuide();yield return null;Assert.AreEqual("drawer:接单",game.Hud.CurrentGuideTarget);
            game.SelectedOrder=3;yield return null;Assert.AreEqual("drawer:order:1",game.Hud.CurrentGuideTarget);game.Hud.FollowGuide();Action("接单").onClick.Invoke();yield return null;
            Assert.AreEqual("nav:采购",game.Hud.CurrentGuideTarget);game.Hud.FollowGuide();yield return null;Assert.AreEqual("drawer:purchase",game.Hud.CurrentGuideTarget);
            Action("支付并采购").onClick.Invoke();yield return null;Assert.AreEqual("progress",game.Hud.CurrentGuideTarget);Assert.AreEqual("arrival",HardwareGuide.Evaluate(game.World).Key);
            for(var i=0;i<850*60;i++)game.World.Tick();game.Hud.Refresh();yield return null;Assert.AreEqual("nav:订单",game.Hud.CurrentGuideTarget);
            game.Hud.FollowGuide();yield return null;Assert.AreEqual("drawer:预留合格库存",game.Hud.CurrentGuideTarget);Action("预留合格库存").onClick.Invoke();yield return null;Assert.AreEqual("drawer:整单交付",game.Hud.CurrentGuideTarget);
            Action("整单交付").onClick.Invoke();yield return null;Assert.IsTrue(game.Hud.TutorialComplete);Assert.AreEqual("",game.Hud.CurrentGuideTarget);Assert.IsFalse(game.Hud.transform.Find("Hardware HUD").GetComponentsInChildren<Transform>().Any(t=>t.name=="Tutorial highlight"&&t.gameObject.activeInHierarchy));game.World.AssertConserved();
        }
        [UnityTest] public IEnumerator TutorialTogglePersistsAndDrawerRebinds()
        {
            yield return Open();game.Hud.FollowGuide();yield return null;game.Hud.RebuildDrawer();yield return null;Assert.AreEqual("drawer:接单",game.Hud.CurrentGuideTarget);
            game.Hud.ToggleTutorial();yield return null;Assert.AreEqual("",game.Hud.CurrentGuideTarget);Assert.AreEqual(0,PlayerPrefs.GetInt(HardwareHud.TutorialPreference));
            yield return OpenWithoutEnabling();Assert.IsFalse(game.Hud.TutorialEnabled);game.Hud.ToggleTutorial();yield return null;Assert.AreEqual("nav:订单",game.Hud.CurrentGuideTarget);
        }
        [UnityTest] public IEnumerator SelfMadeSupplyAndMiningGuideDoNotRepeatDispatch()
        {
            yield return Open();game.Paused=true;
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Manufacturer,X=20,Z=24}).Success);
            game.Hud.FollowGuide();yield return null;Assert.AreEqual("drawer:purchase",game.Hud.CurrentGuideTarget);
            Assert.IsTrue(game.Hud.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("铝材 ×12")));
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Processor,X=12,Z=24}).Success);
            game.Hud.FollowGuide();yield return null;Assert.AreEqual("drawer:mining",game.Hud.CurrentGuideTarget);
            Action("规划循环采集铝原料").onClick.Invoke();yield return null;Assert.AreEqual("progress",game.Hud.CurrentGuideTarget);Assert.IsTrue(game.Truck.Loop);
        }
        private IEnumerator OpenWithoutEnabling()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/Hardware_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Hardware_Business");
#endif
            yield return null;yield return null;game=Object.FindFirstObjectByType<HardwareGame>();game.SendMessage("OnApplicationFocus",true);
        }
        [UnityTest] public IEnumerator UiScalingKeepsMapAndGuidanceInsideCanvas()
        {
            yield return Open();var scaler=game.Hud.GetComponentInChildren<CanvasScaler>();var canvas=(RectTransform)scaler.transform;var map=game.Hud.GetComponentInChildren<HardwareMinimap>().rectTransform;var construction=(RectTransform)canvas.Find("Construction");
            foreach(var scale in new[]{1f,1.15f,1.3f})
            {
                scaler.referenceResolution=new Vector2(1920/scale,1080/scale);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,map);var buildBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,construction);
                var panelBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,map.parent);
                Assert.GreaterOrEqual(bounds.min.x,canvas.rect.xMin);Assert.LessOrEqual(bounds.max.y,canvas.rect.yMax);
                Assert.That(panelBounds.min.x,Is.EqualTo(canvas.rect.xMin+84).Within(.1f));
                Assert.That(panelBounds.min.y,Is.EqualTo(canvas.rect.yMin+128).Within(.1f));
                Assert.Greater(panelBounds.min.y,buildBounds.max.y);
                var goalBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,canvas.Find("Tracked order"));
                Assert.Less(panelBounds.max.y,goalBounds.min.y);
                game.Hud.FollowGuide();yield return null;var target=Action("接单");var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,target.transform.position)},hits);
                var drawerCorners=new Vector3[4];((RectTransform)canvas.Find("Context drawer")).GetWorldCorners(drawerCorners);
                Assert.That(canvas.InverseTransformPoint(drawerCorners[0]).y,Is.EqualTo(buildBounds.max.y+12).Within(.1f));
                Assert.IsTrue(hits.Any(h=>h.gameObject==target.gameObject),"Highlight decoration must not intercept clicks");Assert.IsFalse(hits.Any(h=>h.gameObject.name=="Highlight edge"));
            }
        }
    }
}
