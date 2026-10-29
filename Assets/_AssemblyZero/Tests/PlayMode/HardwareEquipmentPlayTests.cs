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
    public sealed class HardwareEquipmentPlayTests
    {
        private HardwareGame game;
        private InputSettings originalSettings,testSettings;
        private HideFlags originalFlags;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool savedShortcuts,savedTutorial;
        private string shortcutValue;
        private int tutorialValue;

        [SetUp] public void Setup()
        {
            savedShortcuts=PlayerPrefs.HasKey(HardwareHud.BuildShortcutPreference);shortcutValue=PlayerPrefs.GetString(HardwareHud.BuildShortcutPreference,"");
            savedTutorial=PlayerPrefs.HasKey(HardwareHud.TutorialPreference);tutorialValue=PlayerPrefs.GetInt(HardwareHud.TutorialPreference,1);
            PlayerPrefs.DeleteKey(HardwareHud.BuildShortcutPreference);PlayerPrefs.SetInt(HardwareHud.TutorialPreference,0);
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
            if(savedShortcuts)PlayerPrefs.SetString(HardwareHud.BuildShortcutPreference,shortcutValue);else PlayerPrefs.DeleteKey(HardwareHud.BuildShortcutPreference);
            if(savedTutorial)PlayerPrefs.SetInt(HardwareHud.TutorialPreference,tutorialValue);else PlayerPrefs.DeleteKey(HardwareHud.TutorialPreference);PlayerPrefs.Save();
        }
        private IEnumerator Open()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/Hardware_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Hardware_Business");
#endif
            yield return null;yield return null;game=Object.FindFirstObjectByType<HardwareGame>();game.SendMessage("OnApplicationFocus",true);
        }
        private Button Action(string name)=>game.Hud.GetComponentsInChildren<Button>().First(b=>b.name==name);
        private Button Slot(int index)=>Action("Build shortcut "+(index+1));
        private Button Card(BuildingKind kind)=>Action("Equipment card "+kind);
        private TMP_InputField Search()=>game.Hud.transform.Find("Hardware HUD/Equipment backdrop/Equipment library/Equipment search").GetComponent<TMP_InputField>();
        private int CardCount()=>game.Hud.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Equipment card "));

        [UnityTest] public IEnumerator Equipment_SevenCompleteSlotsAndLibraryFitEveryUiScale()
        {
            yield return Open();var scaler=game.Hud.GetComponentInChildren<CanvasScaler>();var canvas=(RectTransform)scaler.transform;
            foreach(var scale in new[]{1f,1.15f,1.3f})
            {
                scaler.referenceResolution=new Vector2(1920/scale,1080/scale);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var bar=(RectTransform)canvas.Find("Construction");
                Assert.AreEqual(7,bar.GetComponentsInChildren<HardwareBuildDragHandle>().Count());
                foreach(var button in bar.GetComponentsInChildren<Button>())
                {
                    var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                    var min=bar.InverseTransformPoint(corners[0]);var max=bar.InverseTransformPoint(corners[2]);
                    Assert.That(min.x,Is.GreaterThanOrEqualTo(bar.rect.xMin));Assert.That(max.x,Is.LessThanOrEqualTo(bar.rect.xMax));
                    Assert.That(min.y,Is.GreaterThanOrEqualTo(bar.rect.yMin));Assert.That(max.y,Is.LessThanOrEqualTo(bar.rect.yMax));
                    Assert.IsFalse(button.GetComponentInChildren<TMP_Text>().isTextOverflowing,button.name+" at "+scale);
                }
                game.Hud.OpenEquipmentLibrary();game.Hud.Refresh();Canvas.ForceUpdateCanvases();
                var panel=(RectTransform)canvas.Find("Equipment backdrop/Equipment library");var panelCorners=new Vector3[4];panel.GetWorldCorners(panelCorners);
                Assert.GreaterOrEqual(canvas.InverseTransformPoint(panelCorners[0]).y,canvas.rect.yMin);
                Assert.LessOrEqual(canvas.InverseTransformPoint(panelCorners[2]).y,canvas.rect.yMax);
                Assert.AreEqual(9,CardCount());Assert.IsNotNull(Card(BuildingKind.Rework));
                foreach(var card in panel.GetComponentsInChildren<HardwareBuildDragHandle>())
                    Assert.IsTrue(card.GetComponentsInChildren<TMP_Text>().All(t=>!t.isTextOverflowing),card.name);
                game.Hud.CloseEquipmentLibrary();
            }
        }

        [UnityTest] public IEnumerator Equipment_CategoriesSearchAndNoResultsKeepTheCatalogAccessible()
        {
            yield return Open();Action("全部设备").onClick.Invoke();Assert.AreEqual(9,CardCount());
            Action("Equipment category 材料加工").onClick.Invoke();Assert.AreEqual(2,CardCount());
            Search().text="散热";Assert.AreEqual(1,CardCount());Assert.IsNotNull(Card(BuildingKind.Manufacturer));
            Search().text="不存在的设备";Assert.AreEqual(0,CardCount());
            Assert.IsTrue(game.Hud.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("没有匹配设备")));
            Search().text="";Assert.AreEqual(2,CardCount());Action("Equipment category 全部").onClick.Invoke();
            Search().text="gpu";Assert.AreEqual(1,CardCount());Assert.IsNotNull(Card(BuildingKind.GPUAssembly));
            Search().text="";Assert.AreEqual(9,CardCount());
            var before=game.World.State.Nodes.Count;var money=game.World.State.Money;Card(BuildingKind.Rework).onClick.Invoke();
            Assert.AreEqual(BuildingKind.Rework,game.Placing);Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
            Assert.AreEqual(before,game.World.State.Nodes.Count);Assert.AreEqual(money,game.World.State.Money);
        }

        [UnityTest] public IEnumerator Equipment_DragDropSwapsAndPersistsWithoutBuildingOrSpending()
        {
            yield return Open();game.Hud.OpenEquipmentLibrary();Canvas.ForceUpdateCanvases();var before=game.World.State.Nodes.Count;var money=game.World.State.Money;
            var source=Card(BuildingKind.Rework);var target=Slot(0);var point=RectTransformUtility.WorldToScreenPoint(null,target.transform.position);
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left,eligibleForClick=true,pointerDrag=source.gameObject};
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.beginDragHandler);Assert.IsFalse(pointer.eligibleForClick);
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.AreEqual(target.gameObject,hits[0].gameObject,"The drag ghost must not intercept slot drops");
            ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.dropHandler);ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.endDragHandler);
            Assert.AreEqual(BuildingKind.Rework,game.Hud.BuildShortcutAt(0));Assert.IsTrue(game.Hud.EquipmentLibraryOpen);Assert.IsNull(game.Placing);
            source=Slot(0);target=Slot(6);pointer.pointerDrag=source.gameObject;
            ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.dropHandler);ExecuteEvents.Execute(source.gameObject,pointer,ExecuteEvents.endDragHandler);
            Assert.AreEqual(BuildingKind.Packaging,game.Hud.BuildShortcutAt(0));Assert.AreEqual(BuildingKind.Rework,game.Hud.BuildShortcutAt(6));
            Assert.AreEqual(before,game.World.State.Nodes.Count);Assert.AreEqual(money,game.World.State.Money);Assert.IsTrue(PlayerPrefs.HasKey(HardwareHud.BuildShortcutPreference));
            game.Hud.CloseEquipmentLibrary();var focus=game.CameraFocus;var zoom=game.ViewCamera.orthographicSize;
            ExecuteEvents.Execute(Slot(0).gameObject,pointer,ExecuteEvents.beginDragHandler);Assert.IsFalse(game.CanNavigate);
            game.MoveView(Vector2.one,.1f);game.ZoomView(120);Assert.AreEqual(focus,game.CameraFocus);Assert.AreEqual(zoom,game.ViewCamera.orthographicSize);
            ExecuteEvents.Execute(Slot(0).gameObject,pointer,ExecuteEvents.endDragHandler);
            game.NewGame();Assert.AreEqual(BuildingKind.Rework,game.Hud.BuildShortcutAt(6));
            yield return Open();Assert.AreEqual(BuildingKind.Packaging,game.Hud.BuildShortcutAt(0));Assert.AreEqual(BuildingKind.Rework,game.Hud.BuildShortcutAt(6));
        }

        [UnityTest] public IEnumerator Equipment_PinAndRightClickReplacementPreserveFixedSlotOrder()
        {
            yield return Open();game.Hud.OpenEquipmentLibrary();Action("Pin equipment Rework").onClick.Invoke();Slot(4).onClick.Invoke();
            Assert.AreEqual(BuildingKind.Rework,game.Hud.BuildShortcutAt(4));Assert.IsNull(game.Placing);Assert.AreEqual(BuildingKind.Warehouse,game.Hud.BuildShortcutAt(0));
            game.Hud.CloseEquipmentLibrary();var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right};
            ExecuteEvents.Execute(Slot(4).gameObject,pointer,ExecuteEvents.pointerClickHandler);Assert.IsTrue(game.Hud.EquipmentLibraryOpen);
            Card(BuildingKind.Sales).onClick.Invoke();Assert.AreEqual(BuildingKind.Sales,game.Hud.BuildShortcutAt(4));Assert.IsNull(game.Placing);
            Assert.AreEqual(BuildingKind.Warehouse,game.Hud.BuildShortcutAt(0));Assert.AreEqual(BuildingKind.Tester,game.Hud.BuildShortcutAt(5));
        }

        [UnityTest] public IEnumerator Equipment_SearchFocusEscapePauseAndHotkeysDoNotMoveTheCamera()
        {
            yield return Open();game.Hud.OpenEquipmentLibrary();yield return null;var start=game.CameraFocus;
            game.MoveView(Vector2.one,.1f);game.ZoomView(120);game.RotateViewStep(1);Assert.AreEqual(start,game.CameraFocus);Assert.AreEqual(0,game.TargetCameraYaw);
            var tick=game.World.State.Tick;yield return new WaitForSecondsRealtime(.1f);Assert.Greater(game.World.State.Tick,tick);
            Search().Select();Search().ActivateInputField();yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.IsNull(game.Placing);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.IsFalse(game.Hud.EquipmentLibraryOpen);Assert.IsFalse(game.Paused);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit7));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.AreEqual(BuildingKind.Packaging,game.Placing);
            game.Placing=null;game.Hud.OpenEquipmentLibrary();game.TogglePause();yield return null;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(2,2)},hits);
            Assert.AreEqual("Modal blocker",hits[0].gameObject.name);game.TogglePause();game.Hud.CloseEquipmentLibrary();
        }

        [UnityTest] public IEnumerator Equipment_DeviceActionsUpgradeMoveAndFindAutomaticRework()
        {
            yield return Open();var warehouse=game.World.State.Nodes.First(n=>n.Kind==BuildingKind.Warehouse);game.FocusNode(warehouse.Id);yield return null;
            var actions=game.Hud.transform.Find("Hardware HUD/Context drawer/Viewport/Content/Device quick actions");Assert.IsNotNull(actions);
            var money=game.World.State.Money;Action("升级：提速，并降低次品率").onClick.Invoke();Assert.AreEqual(2,warehouse.Level);Assert.Less(game.World.State.Money,money);
            Action("移动 / 旋转").onClick.Invoke();Assert.IsTrue(game.Moving);Assert.AreEqual(BuildingKind.Warehouse,game.Placing);game.Placing=null;game.Moving=false;
            var result=game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Tester,X=28,Z=8});Assert.IsTrue(result.Success,result.Message);var tester=result.Entity;
            game.FocusNode(tester);Action("返修次品").onClick.Invoke();Assert.AreEqual(BuildingKind.Rework,game.Placing);
            game.Placing=null;result=game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Rework,X=20,Z=24});Assert.IsTrue(result.Success,result.Message);var rework=result.Entity;
            game.FocusNode(tester);Action("返修次品").onClick.Invoke();Assert.AreEqual(rework,game.SelectedNode);
            Action("补充显存").onClick.Invoke();Assert.AreEqual("采购",game.Drawer);Assert.IsTrue(game.Hud.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("显存包 ×")));
            game.World.AssertConserved();
        }

        [UnityTest] public IEnumerator Equipment_GuideFindsDevicesRemovedFromTheQuickbar()
        {
            yield return Open();game.Hud.AssignBuildShortcut(4,BuildingKind.Rework);game.Hud.SetTutorialEnabled(true);
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);yield return null;yield return null;
            Assert.IsTrue(game.Hud.EquipmentLibraryOpen);Assert.IsNotNull(Card(BuildingKind.GPUAssembly));Assert.AreEqual("build:GPUAssembly",game.Hud.CurrentGuideTarget);
            game.Hud.CloseEquipmentLibrary();yield return null;yield return null;Assert.IsFalse(game.Hud.EquipmentLibraryOpen,"Closing the library must not fight the guide");
            game.Hud.FollowGuide();Assert.AreEqual(BuildingKind.GPUAssembly,game.Placing);Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
        }

        [UnityTest] public IEnumerator Equipment_TransitionsCloseTheLibraryAndPreserveProcurement()
        {
            yield return Open();game.BeginBuild(BuildingKind.Processor);game.Hud.OpenEquipmentLibrary();game.Hud.CloseEquipmentLibrary();
            Assert.AreEqual(BuildingKind.Processor,game.Placing);Assert.AreEqual(HardwareInteraction.Placing,game.Interaction);
            game.Hud.OpenEquipmentLibrary();game.SetDrawer("采购");Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
            game.Hud.OpenProcurementOverview();Assert.IsTrue(game.Hud.ProcurementOpen);game.Hud.OpenEquipmentLibrary();Assert.IsFalse(game.Hud.ProcurementOpen);Assert.IsTrue(game.Hud.EquipmentLibraryOpen);
            game.Hud.OpenProcurementOverview();Assert.IsFalse(game.Hud.EquipmentLibraryOpen);Assert.IsTrue(game.Hud.ProcurementOpen);game.Hud.CloseProcurementOverview();
            game.Hud.OpenEquipmentLibrary();game.World.Factory(2).Owned=true;game.ChangeFactory();Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
            game.Hud.OpenEquipmentLibrary();game.NewGame();Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
            game.Hud.OpenEquipmentLibrary();game.EnterShowcase();Assert.IsFalse(game.Hud.EquipmentLibraryOpen);game.ExitShowcase();Assert.IsFalse(game.Hud.EquipmentLibraryOpen);
        }

        [UnityTest] public IEnumerator Equipment_LargerCatalogUsesVerticalGridAndKeepsShortcutsStable()
        {
            yield return Open();var original=game.Config;var expanded=Object.Instantiate(original);
            expanded.Economy=JsonUtility.FromJson<EconomySettings>(JsonUtility.ToJson(original.Economy));
            expanded.Economy.Hardware.Buildings=expanded.Economy.Hardware.Buildings.Concat(Enumerable.Range(100,12).Select(i=>new HardwareBuilding{Kind=(BuildingKind)i,Name="扩展设备 "+i,Cost=10000})).ToArray();
            try
            {
                game.Config=expanded;game.Hud.OpenEquipmentLibrary();Canvas.ForceUpdateCanvases();Assert.AreEqual(21,CardCount());
                var scroll=game.Hud.transform.Find("Hardware HUD/Equipment backdrop/Equipment library/Equipment scroll").GetComponent<ScrollRect>();
                Assert.IsFalse(scroll.horizontal);Assert.IsTrue(scroll.vertical);Assert.Greater(scroll.content.rect.height,scroll.viewport.rect.height);
                Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(1).Within(.001f));scroll.verticalNormalizedPosition=0;game.Hud.Refresh();Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.001f));
                Action("Equipment category 其他").onClick.Invoke();Assert.AreEqual(12,CardCount());Search().text="扩展设备 103";Assert.AreEqual(1,CardCount());
                Assert.AreEqual(BuildingKind.Warehouse,game.Hud.BuildShortcutAt(0));Assert.AreEqual(BuildingKind.Packaging,game.Hud.BuildShortcutAt(6));
            }
            finally{game.Hud.CloseEquipmentLibrary();game.Config=original;Object.Destroy(expanded);}
        }
    }
}
