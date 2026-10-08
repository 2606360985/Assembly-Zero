using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class InserterPresenter : MonoBehaviour
    {
        public static Quaternion ToolRotation => Quaternion.Euler(180, 0, 0);
        public EP02LayoutAuthoring Layout;
        private MaterialPropertyBlock block;
        public static Vector3 Motion(InserterState state, float t, Vector3 rest, Vector3 pickup, Vector3 socket)
        {
            var across = socket - pickup; across.y = 0;
            // Both outbound and inbound motions use the same side of the pedestal,
            // avoiding a yaw flip above the base and leaving room for the payload.
            var aroundBase = Vector3.Cross(Vector3.up, across).normalized * Mathf.Sin(t * Mathf.PI) * .8f;
            return state switch
            {
                InserterState.Picking => Vector3.Lerp(rest, pickup, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .35f + aroundBase,
                InserterState.Carrying => Vector3.Lerp(pickup, socket + Vector3.up * .32f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .7f + aroundBase,
                InserterState.Placing => Vector3.Lerp(socket + Vector3.up * .32f, socket, t),
                InserterState.Returning => Vector3.Lerp(socket, rest, t),
                _ => rest
            };
        }
        public void Present(AssemblySnapshot snapshot, EP02BakedLayout topology, float fraction)
        {
            foreach (var author in Layout.Inserters)
            {
                var arm = snapshot.Inserters.Single(a => a.Id == author.StableId);
                var product = topology.Product.Sample(topology.Product.CenterDistance(author.StationPiece), out var tangent);
                var socket = product + Quaternion.LookRotation(tangent) * Layout.SocketOffset(author.SocketId);
                var pickup = topology.Supply[Mathf.Clamp(arm.PickupLane,0,1)].Sample(arm.PickupDistance, out _) + Vector3.up * .26f;
                var query = topology.Supply[0].Sample(author.PickupDistances[0], out _) + Vector3.up * .26f;
                foreach (var item in snapshot.Items) if (item.ItemId == arm.ReservedItem) { query = topology.Supply[item.Lane].Sample(item.Distance, out _) + Vector3.up * .26f; break; }
                var t = Mathf.SmoothStep(0, 1, (float)arm.Progress(snapshot.Tick, fraction)); var rest = author.RestPosition;
                var target = arm.State == InserterState.WaitingForTarget && arm.HeldItem != 0 ? socket + Vector3.up * .32f : Motion(arm.State, t, rest, arm.State == InserterState.Picking ? query : pickup, socket);
                author.Solve(target, ToolRotation);
                if (author.StatusLight != null) { block ??= new MaterialPropertyBlock(); var color = arm.State == InserterState.Disabled ? Color.gray : arm.State == InserterState.WaitingForTarget && arm.HeldItem != 0 ? Color.red : arm.HeldItem != 0 ? Color.cyan : arm.ReservedItem != 0 ? Color.yellow : Color.green; block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color); author.StatusLight.SetPropertyBlock(block); }
            }
        }
    }
}
