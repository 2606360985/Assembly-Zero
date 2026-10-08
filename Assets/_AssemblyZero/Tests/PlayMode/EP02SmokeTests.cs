using System.Collections;
using System.Linq;
using AssemblyZero.Domain;
using AssemblyZero.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace AssemblyZero.Tests
{
    public sealed class EP02SmokeTests
    {
        [UnityTest]public IEnumerator ProductionPresentationAndRestartDoNotOwnLogistics()
        {
            yield return SceneManager.LoadSceneAsync("EP02_Demo",LoadSceneMode.Single);yield return null;
            var d=Object.FindFirstObjectByType<EP02SimulationDriver>();d.Paused=true;Assert.IsNull(d.StartupError);Assert.AreEqual(6,d.Layout.Inserters.Length);Assert.AreEqual(0,Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length);Assert.IsNull(GameObject.Find("Stage 1"));
            for(var i=0;i<32;i++){d.StepTicks(250);yield return null;}Assert.Greater(d.Simulation.Statistics.CompletedRobots,0);Assert.Greater(d.Simulation.Snapshot.ExitedProducts,0);
            var hash=d.Simulation.CalculateStateHash();var count=d.Simulation.Statistics.CompletedRobots;var payloads=d.PartPresenter.VisiblePayloads;
            for(var i=0;i<5;i++){d.Present();yield return null;}Assert.AreEqual(hash,d.Simulation.CalculateStateHash());Assert.AreEqual(count,d.Simulation.Statistics.CompletedRobots);Assert.AreEqual(payloads,d.PartPresenter.VisiblePayloads);
            foreach(var item in d.Simulation.Snapshot.Items)
            {
                var anchor=GameObject.Find("Item "+item.ItemId+" - "+d.Layout.Part(item.Part.Value).PartName).GetComponent<PartVisualAnchor>();
                if(item.Ownership==ItemOwnership.HeldByInserter)Assert.AreEqual(d.Layout.Arm(item.OwnerId).Grip,anchor.PayloadRoot.parent);
                if(item.Ownership==ItemOwnership.InAssemblySocket||item.Ownership==ItemOwnership.CompletedProduct)Assert.AreEqual(d.PartPresenter.ProductSocket(item.ProductId,item.OwnerId),anchor.PayloadRoot.parent);
            }
            d.Restart();yield return null;yield return null;Assert.AreEqual(d.Simulation.Snapshot.Items.Count,Object.FindObjectsByType<PartVisualAnchor>(FindObjectsSortMode.None).Length);Assert.AreEqual(0,d.PartPresenter.VisibleProducts);
        }
    }
}
