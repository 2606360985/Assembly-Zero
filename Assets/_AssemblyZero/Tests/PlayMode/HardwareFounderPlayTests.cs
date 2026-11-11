using System.Collections;
using System.Linq;
using AssemblyZero.Domain.EP04;
using AssemblyZero.Unity.EP04;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AssemblyZero.Tests
{
    public sealed class HardwareFounderPlayTests
    {
        private HardwareGame game;
        private IEnumerator Open()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/Hardware_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Hardware_Business");
#endif
            yield return null;yield return null;game=Object.FindFirstObjectByType<HardwareGame>();game.AutosaveEnabled=false;
        }
        private void Finish(){game.Founder.SkipDialogue();game.Founder.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="回到工厂 / 继续目标").onClick.Invoke();}
        [UnityTest] public IEnumerator Founder_IdentityDialogueAndReplayFreezeBusinessButFramesAdvance()
        {
            yield return Open();Assert.IsTrue(game.Founder.IsBlocking);var tick=game.World.State.Tick;var frame=Time.frameCount;
            yield return new WaitForSecondsRealtime(.1f);Assert.Greater(Time.frameCount,frame);Assert.AreEqual(tick,game.World.State.Tick);
            game.Speed=4;game.Founder.ConfirmIdentity(1,"沈工");yield return null;yield return null;Assert.AreEqual("intro",game.Founder.CurrentScene);
            var cash=game.World.State.Money;Assert.IsFalse(game.Submit(new WorldCommand{Kind=CommandKind.Build,Building=BuildingKind.Tester,X=28,Z=8}).Success);Assert.AreEqual(cash,game.World.State.Money);
            game.Founder.ToggleHidden();Assert.IsFalse(game.Founder.DialogueVisible);yield return null;game.Founder.ToggleHidden();
            Finish();Assert.IsFalse(game.Founder.IsBlocking);Assert.AreEqual(4,game.Speed);Assert.IsFalse(game.Paused);yield return null;yield return null;Assert.Greater(game.World.State.Tick,tick);
            var saved=JsonUtility.ToJson(game.World.State);game.Founder.Play("intro",true);game.Founder.SkipDialogue();game.Founder.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="结束回放").onClick.Invoke();Assert.AreEqual(saved,JsonUtility.ToJson(game.World.State),"Replay must not mutate campaign state");
        }
        [UnityTest] public IEnumerator Founder_ContractSkipPreservesExplicitDecisionAndRealQualityTask()
        {
            yield return Open();game.Founder.ConfirmIdentity(0,"林序");yield return null;yield return null;Finish();
            game.Founder.RequestContract(1);game.Founder.SkipDialogue();Assert.AreEqual(HardwareOrderPhase.Available,game.World.State.Hardware.Orders[0].Phase);
            game.Founder.Sign(2);Assert.AreEqual(HardwareOrderPhase.Active,game.World.State.Hardware.Orders[0].Phase);Assert.AreEqual(2,game.World.Narrative.QualityMode);Assert.IsFalse(game.World.NarrativeQualityReady(1));
            game.World.CaptureState();var loaded=new WorldSimulation(game.Config.Economy,JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(game.World.State)));Assert.AreEqual(2,loaded.Narrative.QualityMode);
        }
        [UnityTest] public IEnumerator Founder_ArtExpressionsAndDialogueFitSupportedScales()
        {
            yield return Open();game.Founder.ConfirmIdentity(1,"沈知微");yield return null;yield return null;
            foreach(var art in new[]{"lin","shen","client_studio","client_gaming","client_research","workshop","acceptance","first_product","studio","gaming","ai_lab","compute","energy","design","energy_control","orbital","deep_space"})Assert.IsNotNull(Resources.Load<Texture2D>("Founder/Art/"+art),art);
            var scaler=game.Hud.GetComponentInChildren<UnityEngine.UI.CanvasScaler>();
            foreach(var scale in new[]{1f,1.15f,1.3f})
            {
                scaler.referenceResolution=new Vector2(1920/scale,1080/scale);yield return null;Canvas.ForceUpdateCanvases();
                var text=game.Founder.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Dialogue body");text.ForceMeshUpdate();Assert.IsFalse(text.isTextOverflowing,"Body overflow at "+scale);Assert.LessOrEqual(text.textInfo.lineCount,3);
            }
        }
        [UnityTest] public IEnumerator Founder_ProductStoryUsesTheActualDesignedSkuPreview()
        {
            yield return Open();game.Founder.ConfirmIdentity(0,"林序");yield return null;yield return null;Finish();
            var result=game.Submit(new WorldCommand{Kind=CommandKind.DesignSku,Name="首创铜橙",Memory=1,Cooler=HardwareCatalog.Heat,Fans=2,Length=1,Shell=3,Backplate=true,Market=TargetMarket.Gaming});Assert.IsTrue(result.Success,result.Message);
            game.World.ObserveNarrative();yield return null;yield return null;
            Assert.AreEqual("product",game.Founder.CurrentScene);Assert.AreEqual("首创铜橙",game.World.Narrative.LastProduct);
            var image=game.Founder.GetComponentsInChildren<UnityEngine.UI.RawImage>().First(i=>i.name=="Actual SKU preview");Assert.IsNotNull(image.texture);Assert.IsInstanceOf<RenderTexture>(image.texture);
            Assert.IsTrue(game.View.GetComponentsInChildren<Transform>().Any(t=>t.name=="Backplate"));
        }
    }
}
