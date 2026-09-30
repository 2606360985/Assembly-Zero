using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class EP02AudioPresenter : MonoBehaviour
    {
        public AudioSource Source;
        public AudioClip Pick, Lock, Scan, Complete, Reject, Alarm;
        private long presentedTick = -1;
        public void ResetEvents() { presentedTick = -1; }
        public void Present(AssemblySnapshot snapshot)
        {
            foreach (var e in snapshot.Events) if (e.Tick > presentedTick && Source != null) { var clip = e.Kind switch { AssemblyEventKind.Pick => Pick, AssemblyEventKind.Lock => Lock, AssemblyEventKind.Scan => Scan, AssemblyEventKind.Complete => Complete, AssemblyEventKind.Reject => Reject, _ => Alarm }; if (clip != null) Source.PlayOneShot(clip); }
            presentedTick = snapshot.Tick;
        }
    }
}
