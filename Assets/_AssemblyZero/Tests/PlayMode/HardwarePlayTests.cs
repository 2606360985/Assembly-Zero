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
        [UnityTest] public IEnumerator Hardware_ComputeParkBuildsModelsAndOperatesFromDrawer()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;
            game.SetDrawer("算力");yield return null;
            Assert.IsTrue(game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.StartsWith("搭建算力机柜")));
            var b=game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.ComputeRack,X=12,Z=24});Assert.IsTrue(b.Success,b.Message);
            game.View.Rebuild();yield return null;
            Assert.IsTrue(game.View.GetComponentsInChildren<Transform>().Any(t=>t.name=="Server face"));
            game.SetDrawer("算力");yield return null;
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(x=>x.name=="自动补料：关闭").onClick.Invoke();yield return null;
            Assert.IsTrue(game.World.Automation(1).Purchase);
            var tick=game.World.State.Tick;yield return null;Assert.AreEqual(tick,game.World.State.Tick,"Paused UI must not advance production or service payouts");
            game.World.CaptureState();var json=JsonUtility.ToJson(game.World.State);var loaded=new WorldSimulation(game.Config.Economy,JsonUtility.FromJson<WorldState>(json));
            Assert.IsTrue(loaded.Automation(1).Purchase);Assert.AreEqual(1,loaded.State.Nodes.Count(n=>n.Kind==BuildingKind.ComputeRack));loaded.AssertConserved();
        }
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
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.SetDrawer("订单");yield return null;
            var button=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="接单");
            var point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);game.TogglePause();yield return null;
            var results=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},results);
            Assert.IsNotEmpty(results);Assert.AreEqual("Modal blocker",results[0].gameObject.name,"Pause must intercept clicks outside the menu as well");
            var tick=game.World.State.Tick;yield return null;Assert.AreEqual(tick,game.World.State.Tick);
        }
        [UnityTest] public IEnumerator Hardware_QuietShortageOpensCorrectPurchasableMaterial()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;
            var built=game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.GPUAssembly,X=20,Z=8});Assert.IsTrue(built.Success,built.Message);game.SelectedNode=built.Entity;
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.Recipe,Node=built.Entity,Recipe=HardwareSku.RecipeId(HardwareCatalog.QuietSpec)}).Success);
            game.SetDrawer("设备");yield return null;
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="采购缺料").onClick.Invoke();yield return null;
            Assert.AreEqual("采购",game.Drawer);
            Assert.IsTrue(game.Hud.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("低噪散热器 ×")),"A quiet assembly shortage must select its quiet heatsink, not the non-purchasable board or standard heatsink");
        }
        [UnityTest] public IEnumerator Hardware_DesignerLaunchesSkuAndResearchPageStartsProjects()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;
            game.SetDrawer("产品");yield return null;
            var preview=game.Hud.GetComponentsInChildren<UnityEngine.UI.RawImage>().First(r=>r.name=="SKU preview image");Assert.IsNotNull(preview.texture,"The designer renders a live card preview");
            var before=game.World.State.Hardware.Skus.Count;
            foreach(var caption in new[]{"核心体质","目标市场","风扇数量","卡长","背板"})game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name==caption).onClick.Invoke();
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="上市新型号").onClick.Invoke();yield return null;
            Assert.AreEqual(before+1,game.World.State.Hardware.Skus.Count);Assert.AreEqual(3,game.World.RecipesFor(BuildingKind.GPUAssembly).Count());
            var designed=game.World.State.Hardware.Skus.Last();Assert.AreEqual(1,designed.CoreBin);Assert.AreEqual(TargetMarket.Gaming,designed.Market);Assert.AreEqual(1,designed.Fans);Assert.AreEqual(1,designed.Length);Assert.IsTrue(designed.Backplate);
            var key=HardwareVisualKey.Of(new Goods{Material=HardwareCatalog.RawGPU,SpecId=designed.Id,Gen=2,Grade=3},game.World);
            Assert.AreEqual(2,HardwareVisualKey.Gen(key));Assert.AreEqual(3,HardwareVisualKey.Grade(key));Assert.AreEqual(1,HardwareVisualKey.Fans(key));Assert.IsTrue(HardwareVisualKey.Backplate(key));
            game.World.State.Money=5000000;game.SetDrawer("研发");yield return null;
            Assert.IsFalse(game.View.GetComponentsInChildren<Camera>(true).Any(c=>c.name=="SKU preview camera"&&c.enabled),"Leaving the product page stops the preview camera");
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="开始研发").onClick.Invoke();yield return null;
            Assert.AreNotEqual("",game.World.State.Hardware.ResearchActive);
            var lab=game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Lab,X=20,Z=24});Assert.IsTrue(lab.Success,lab.Message);
            game.View.Rebuild();yield return null;game.World.AssertConserved();
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
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;
            Assert.AreEqual(HardwareProductForm.Card,HardwareVisualCatalog.Get(HardwareCatalog.RawGPU,true).Form);
            Assert.IsTrue(HardwareVisualCatalog.Get(HardwareCatalog.RawGPU,true).Quiet);Assert.IsFalse(HardwareVisualCatalog.Get(HardwareCatalog.RawGPU,true).Qualified);
            Assert.IsTrue(HardwareVisualCatalog.Get(HardwareCatalog.Tested,true).Qualified);
            var quietKey=HardwareVisualKey.Of(HardwareCatalog.Tested,game.World.Sku(HardwareCatalog.QuietSpec));
            Assert.AreEqual(HardwareCatalog.Tested,HardwareVisualKey.Material(quietKey));Assert.IsTrue(HardwareVisualKey.Quiet(quietKey));
            var machinery=game.View.transform.Find("Factory machinery");game.View.Rebuild();Assert.AreSame(machinery,game.View.transform.Find("Factory machinery"),"A notification or exploration revision must not rebuild geometry.");
            var position=game.View.TruckVisual.position;yield return null;Assert.AreEqual(position,game.View.TruckVisual.position);
            game.BeginBuild(BuildingKind.Processor);game.Moving=true;game.NewGame();
            Assert.IsNull(game.Placing);Assert.IsFalse(game.Moving);
            Assert.IsFalse(game.View.GetComponentsInChildren<HardwareProductView>().Any(p=>p.Material==HardwareCatalog.Standard||p.Material==HardwareCatalog.Tested),"No finished goods should flash during a rebuild before the first presentation refresh.");
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
            yield return null;yield return null;Object.FindFirstObjectByType<HardwareGame>().AutosaveEnabled=false;Object.FindFirstObjectByType<HardwareGame>().Founder.SetPresentationEnabled(false);
        }
        [UnityTest] public IEnumerator Hardware_BuildPointerAndUiBlocking()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;Assert.IsNotNull(game);var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                game.BeginBuild(BuildingKind.Processor);var f=game.World.Factory(1);var point=game.ViewCamera.WorldToScreenPoint(new Vector3((f.X+12)*1.2f,0,(f.Z+24)*1.2f));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
                Assert.AreEqual(4,game.World.State.Nodes.Count,game.Message+" pointer="+point+" screen="+Screen.width+"x"+Screen.height+" UI="+string.Join(",",hits.Select(h=>h.gameObject.name)));
                game.BeginBuild(BuildingKind.Manufacturer);InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20)});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height-20),buttons=1});yield return null;yield return null;Assert.AreEqual(4,game.World.State.Nodes.Count,"HUD click must not build");
                game.Placing=null;game.TogglePause();var tick=game.World.State.Tick;yield return null;yield return null;Assert.AreEqual(tick,game.World.State.Tick);game.World.AssertConserved();
            }
            finally{InputSystem.RemoveDevice(mouse);}
        }
        [UnityTest] public IEnumerator Hardware_TransportTakeoverInputPauseAndReturnToPlanning()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                game.SendMessage("OnApplicationFocus",true);game.Speed=2;
                game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="接管小卡车 · Tab").onClick.Invoke();Assert.IsTrue(game.Truck.Manual);Assert.AreEqual("",game.Drawer);Assert.AreEqual(1,game.Speed);var p=game.Truck.Position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.65f);Assert.AreNotEqual(p,game.Truck.Position);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D));yield return new WaitForSeconds(.2f);var heading=game.Truck.Heading;Assert.Greater(heading,0);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.A));yield return new WaitForSeconds(.2f);Assert.Less(game.Truck.Heading,heading);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));yield return new WaitForSeconds(.5f);Assert.Less(game.Truck.Speed,0);
                Assert.AreEqual(HardwareFactoryView.SampleTruck(game.Truck,0),game.View.TruckVisual.position);
                Assert.Greater(game.View.GetComponentsInChildren<Transform>().Count(t=>t.name=="Roadside tree"),40);
                Assert.That(game.Fog.Settings.FactoryDensityMultiplier,Is.EqualTo(.3f).Within(.001f));
                game.TogglePause();var manualTick=game.World.State.Tick;var manualZ=game.Truck.DriveZ;yield return new WaitForSeconds(.1f);Assert.AreEqual(manualTick,game.World.State.Tick);Assert.AreEqual(manualZ,game.Truck.DriveZ);game.TogglePause();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                game.SendMessage("OnApplicationFocus",false);yield return null;Assert.AreEqual(0,game.Truck.Throttle);game.SendMessage("OnApplicationFocus",true);
                game.ToggleDriving();Assert.IsFalse(game.Truck.Manual);Assert.AreEqual(2,game.Speed);game.SetDrawer("运输");yield return null;
                game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="规划循环采集铝原料").onClick.Invoke();Assert.IsTrue(game.Truck.Loop);Assert.AreEqual(VehiclePhase.Travelling,game.Truck.Phase);
                game.TogglePause();var tick=game.World.State.Tick;var position=game.Truck.Position;yield return new WaitForSeconds(.1f);Assert.AreEqual(tick,game.World.State.Tick);Assert.AreEqual(position,game.Truck.Position);
                game.TogglePause();game.SetDrawer("订单");yield return null;Assert.AreEqual("订单",game.Drawer);game.SetDrawer("采购");yield return null;Assert.AreEqual("采购",game.Drawer);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
        [UnityTest] public IEnumerator Hardware_GuideAcceptsFirstOrderThenAsksForTheMissingMachines()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;game.Hud.FollowGuide();Assert.AreEqual("订单",game.Drawer);Assert.AreEqual(1,game.SelectedOrder);
            game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="接单").onClick.Invoke();yield return null;Assert.AreEqual(HardwareOrderPhase.Active,game.World.State.Hardware.Orders[0].Phase);
            Assert.IsTrue(game.View.GetComponentsInChildren<Transform>().Any(t=>t.name=="Suggested slot GPUAssembly"),"The empty assembly slot must be marked on the floor");
            game.Hud.FollowGuide();yield return null;Assert.AreEqual(BuildingKind.GPUAssembly,game.Placing);
            Assert.IsTrue(game.View.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh!=null&&f.sharedMesh.name=="Hardware beveled casing"),"Technology equipment should have finished casings.");
        }
        [UnityTest] public IEnumerator Hardware_SpeedCycleOrderBoardMarketAndSettlementToast()
        {
            yield return Open();var game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;game.Paused=true;
            var speed=game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="速度 1×");
            foreach(var expected in new[]{2,4,8,1}){speed.onClick.Invoke();Assert.AreEqual(expected,game.Speed);}
            Assert.IsTrue(game.Submit(new WorldCommand{Kind=CommandKind.AcceptOrder,Order=1}).Success);HardwareWalkthrough.BuildChain(game.World);
            for(var i=0;i<240*60&&game.World.State.Hardware.Settlements.Count==0;i++)game.World.Tick();Assert.AreEqual(1,game.World.State.Hardware.Settlements.Count);
            game.World.Tick();yield return null;yield return null;
            Assert.IsTrue(game.Hud.transform.Find("Hardware HUD/Settlement toast").gameObject.activeSelf,"Automatic delivery must announce the settlement");
            game.SetDrawer("订单");yield return null;Assert.Greater(game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="接单"),1,"The offer board opens after the first contract");
            game.SetDrawer("市场");yield return null;Assert.IsTrue(game.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.StartsWith("现货卖出")));
            game.World.AssertConserved();
        }
    }
}
