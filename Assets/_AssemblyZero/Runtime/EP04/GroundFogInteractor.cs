using UnityEngine;

namespace AssemblyZero.Unity.EP04
{
    /// <summary>Attach to a moving visual to displace the ground fog without changing gameplay physics.</summary>
    public sealed class GroundFogInteractor : MonoBehaviour
    {
        internal static readonly System.Collections.Generic.HashSet<GroundFogInteractor> Active = new System.Collections.Generic.HashSet<GroundFogInteractor>();
        [Min(.1f)] public float Radius = 1.4f;
        [Min(0)] public float Strength = 1;
        [Min(1)] public float TeleportDistance = 18;
        public HardwareGroundFog Fog;
        private Vector3 previous;
        private bool sampled;
        private float wakeDistance;
        private int wakeSequence;

        public void ResetMotion() { previous = transform.position; sampled = false; wakeDistance = 0; }
        private void OnEnable() { Active.Add(this); ResetMotion(); }
        private void OnDisable() { Active.Remove(this); ResetMotion(); }
        // Called by the fog after HardwareGame has presented interpolated transforms.
        internal void Sample(HardwareGroundFog target, float dt)
        {
            if (!isActiveAndEnabled || Fog != null && Fog != target) return;
            var position = transform.position;
            if (sampled && dt > 0 && (position - previous).sqrMagnitude < TeleportDistance * TeleportDistance)
            {
                if(target.AddMovement(previous, position, Radius, Strength, dt))
                    target.ShedWake(previous, position, Radius, Strength, dt, ref wakeDistance, ref wakeSequence);
            }
            else wakeDistance = 0;
            previous = position; sampled = true;
        }
    }
}
