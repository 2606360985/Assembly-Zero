using System.Collections.Generic;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class PartVisualPresenter : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public int VisiblePayloads => visuals.Count;
        private sealed class Visual { public GameObject Root; public PartVisualAnchor Anchor; public ItemOwnership Ownership; }
        private readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        private readonly HashSet<int> live = new HashSet<int>();
        private readonly List<int> removed = new List<int>();
        private readonly List<GameObject> fadingCarriers = new List<GameObject>();
        public void Clear() { foreach (var v in visuals.Values) { if (v.Anchor != null && v.Anchor.PayloadRoot != null && v.Root != null && v.Anchor.PayloadRoot.parent != v.Root.transform) Destroy(v.Anchor.PayloadRoot.gameObject); if (v.Root != null) Destroy(v.Root); } visuals.Clear(); foreach (var c in fadingCarriers) if (c != null) Destroy(c); fadingCarriers.Clear(); }
        public void Present(AssemblySnapshot snapshot, BakedBeltTopology topology)
        {
            live.Clear();
            foreach (var item in snapshot.Items)
            {
                live.Add(item.ItemId);
                if (!visuals.TryGetValue(item.ItemId, out var v))
                {
                    var definition = Layout.Part(item.Part.Value); var root = Instantiate(definition.WrapperPrefab, transform); root.name = "Item " + item.ItemId + " - " + definition.PartName;
                    v = new Visual { Root = root, Anchor = root.GetComponent<PartVisualAnchor>(), Ownership = item.Ownership }; visuals.Add(item.ItemId, v);
                }
                var anchor = v.Anchor; var def = anchor.Definition;
                if (item.Ownership == ItemOwnership.OnBelt || item.Ownership == ItemOwnership.ReservedOnBelt)
                {
                    v.Root.transform.position = BeltTopologyBuilder.SampleWorldPose(topology, item.Distance, item.Lane, out var tangent); v.Root.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                    anchor.PayloadRoot.SetParent(v.Root.transform, false); def.BeltPose.Apply(anchor.SourceRoot); anchor.Carrier.SetActive(true);
                }
                else
                {
                    if (v.Ownership == ItemOwnership.OnBelt || v.Ownership == ItemOwnership.ReservedOnBelt) SpawnEmptyCarrier(v.Root.transform.position, anchor.Carrier);
                    anchor.Carrier.SetActive(false);
                    if (item.Ownership == ItemOwnership.HeldByInserter) { anchor.PayloadRoot.SetParent(Layout.Arm(item.OwnerId).Grip, false); def.GripPose.Apply(anchor.SourceRoot); }
                    else
                    {
                        var socketId = item.OwnerId;
                        if (item.Ownership == ItemOwnership.CompletedProduct) foreach (var socket in snapshot.Sockets) if (socket.ItemId == item.ItemId) { socketId = socket.Id; break; }
                        anchor.PayloadRoot.SetParent(Layout.Socket(socketId).transform, false); def.AssemblyPose.Apply(anchor.SourceRoot);
                    }
                    anchor.PayloadRoot.localPosition = Vector3.zero; anchor.PayloadRoot.localRotation = Quaternion.identity; anchor.PayloadRoot.localScale = Vector3.one;
                }
                v.Ownership = item.Ownership;
            }
            removed.Clear(); foreach (var pair in visuals) if (!live.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { var v = visuals[id]; if (v.Anchor.PayloadRoot.parent != v.Root.transform) Destroy(v.Anchor.PayloadRoot.gameObject); Destroy(v.Root); visuals.Remove(id); }
            for (var i = fadingCarriers.Count - 1; i >= 0; i--) if (fadingCarriers[i] == null) fadingCarriers.RemoveAt(i);
        }
        private void SpawnEmptyCarrier(Vector3 start, GameObject carrier)
        {
            var copy = Instantiate(carrier, transform); copy.SetActive(true); copy.transform.position = start; copy.AddComponent<EP02CarrierFade>().Target = Layout.RejectPoint.position; fadingCarriers.Add(copy);
        }
        private void OnDestroy() { Clear(); }
    }
    // Purely visual: these empty trays are deliberately absent from the domain ledger.
    public sealed class EP02CarrierFade : MonoBehaviour
    {
        public Vector3 Target; private float age; private Vector3 start, scale;
        private void Start() { start = transform.position; scale = transform.localScale; }
        private void Update() { age += Time.unscaledDeltaTime; transform.position = Vector3.Lerp(start, Target, Mathf.Clamp01(age / 1.2f)); transform.localScale = scale * (1 - Mathf.Clamp01((age - .8f) / .6f)); if (age >= 1.4f) Destroy(gameObject); }
    }
}
