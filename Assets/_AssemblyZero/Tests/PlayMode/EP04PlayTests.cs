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
    public sealed class EP04PlayTests
    {
        [UnityTest] public IEnumerator EP04_PlayerCanBuildThroughButtonsAndPointerThenSell()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_AssemblyZero/Scenes/EP04_Business.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("EP04_Business");
#endif
            yield return null;var game=Object.FindFirstObjectByType<EP04Game>();Assert.IsNotNull(game);game.Paused=true;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                var labels=new[]{"仓库 · 首座免费","制造机 · 180","销售站 · 首座免费"};var xs=new[]{5,15,32};
                for(var i=0;i<3;i++)
                {
                    var button=Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b=>b.name==labels[i]);button.onClick.Invoke();
                    var f=game.World.Factory(1);var point=game.ViewCamera.WorldToScreenPoint(new Vector3((f.X+xs[i])*1.2f,0,(f.Z+16)*1.2f));
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y),buttons=1});yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});yield return null;
                    Assert.AreEqual(i+1,game.World.State.Nodes.Count,game.Message);
                }
                for(var i=0;i<6000;i++)game.World.Tick();yield return null;Assert.Greater(game.World.State.Sold,0);game.World.AssertConserved();
                var before=game.World.State.Tick;yield return null;yield return null;Assert.AreEqual(before,game.World.State.Tick,"Paused UI must not advance simulation.");
                game.SetView(true);yield return null;Assert.IsTrue(game.MapMode);
                Assert.IsTrue(Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Any(b=>b.name=="当前车辆调拨"));
                LogAssert.NoUnexpectedReceived();
            }
            finally{InputSystem.RemoveDevice(mouse);}
        }
    }
}
