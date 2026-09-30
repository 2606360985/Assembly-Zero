using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02HudStageButton : MonoBehaviour
    {
        public EP02SimulationDriver Driver;
        public int Stage;
        private void Awake() { GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Select); }
        private void Select() { Driver.ResetStage(Stage); }
    }
}
