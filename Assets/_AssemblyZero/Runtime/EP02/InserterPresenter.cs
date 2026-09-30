using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class InserterPresenter : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public Transform AnimationOnlyGrip;
        private MaterialPropertyBlock block;
        public void Present(AssemblySnapshot snapshot, BakedBeltTopology topology, float fraction)
        {
            foreach (var author in Layout.Inserters)
            {
                var active = false;
                foreach (var arm in snapshot.Inserters) if (arm.Id == author.StableId)
                {
                    active = true; var position = Target(arm, snapshot, topology, fraction); Solve(author, position);
                    var color = arm.State == InserterState.WaitingForTarget ? Color.red : arm.HeldItem != 0 ? Color.cyan : arm.ReservedItem != 0 ? Color.yellow : Color.green;
                    Colorize(author.StatusLight, color); break;
                }
                if (!active) { Solve(author, author.transform.position + Vector3.up * 2); Colorize(author.StatusLight, Color.gray); }
            }
            if (AnimationOnlyGrip != null) { AnimationOnlyGrip.gameObject.SetActive(snapshot.Inserters.Count == 1); AnimationOnlyGrip.position = new Vector3(3, 1.8f + Mathf.Sin((snapshot.Tick + fraction) * .06f) * .7f, -2.8f); }
        }
        private Vector3 Target(InserterSnapshot arm, AssemblySnapshot snapshot, BakedBeltTopology topology, float fraction)
        {
            var author = Layout.Arm(arm.Id); var rest = author.transform.position + Vector3.up * 2;
            var pickup = BeltTopologyBuilder.SampleWorld(topology, arm.PickupDistance, arm.PickupLane) + Vector3.up * .28f;
            var preferred = arm.Id <= 1 && snapshot.Inserters.Count <= 2 ? Layout.Arm(0) : author;
            var query = BeltTopologyBuilder.SampleWorld(topology, preferred.PreferredDistance, 0) + Vector3.up * .28f;
            foreach (var item in snapshot.Items) if (item.ItemId == arm.ReservedItem) { query = BeltTopologyBuilder.SampleWorld(topology, item.Distance, item.Lane) + Vector3.up * .28f; break; }
            var socket = Layout.Socket(arm.TargetSocket).transform.position; var t = (float)arm.Progress(snapshot.Tick, fraction);
            switch (arm.State)
            {
                case InserterState.Reserving: return rest;
                case InserterState.Picking: return Vector3.Lerp(rest, query, Mathf.SmoothStep(0, 1, t));
                case InserterState.Carrying: return Vector3.Lerp(pickup, socket + Vector3.up * .5f, Mathf.SmoothStep(0, 1, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
                case InserterState.Placing: return Vector3.Lerp(socket + Vector3.up * .5f, socket, t);
                case InserterState.Returning: return Vector3.Lerp(socket, rest, t);
                case InserterState.WaitingForTarget: return arm.HeldItem != 0 ? socket + Vector3.up * 1.1f : rest;
                default: return rest;
            }
        }
        // Analytic two-link geometry; no physics or animation callback controls ownership.
        private static void Solve(InserterAuthoring arm, Vector3 target)
        {
            var shoulder = arm.transform.position + Vector3.up * .8f; var direction = target - shoulder; var length = arm.Reach * .5f;
            var distance = Mathf.Clamp(direction.magnitude, .01f, length * 2 - .001f); var forward = direction.normalized;
            var bend = Vector3.ProjectOnPlane(Vector3.up, forward).normalized; if (bend.sqrMagnitude < .01f) bend = Vector3.forward;
            var elbow = shoulder + forward * (distance * .5f) + bend * Mathf.Sqrt(Mathf.Max(0, length * length - distance * distance * .25f));
            arm.Shoulder.position = shoulder; arm.Elbow.position = elbow; arm.Grip.position = target; arm.Grip.rotation = Quaternion.identity;
            Link(arm.Links[0], shoulder, elbow); Link(arm.Links[1], elbow, target);
        }
        private static void Link(Transform link, Vector3 a, Vector3 b) { link.position = (a + b) * .5f; link.rotation = Quaternion.FromToRotation(Vector3.up, b - a); link.localScale = new Vector3(.12f, (b - a).magnitude * .5f, .12f); }
        private void Colorize(Renderer r, Color color) { if (r == null) return; block ??= new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color * 1.5f); r.SetPropertyBlock(block); }
    }
}
