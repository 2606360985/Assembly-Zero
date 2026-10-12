using System.Collections.Generic;
using System.Linq;
using AssemblyZero.Domain;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class PartVisualPresenter : MonoBehaviour
    {
        public EP02LayoutAuthoring Layout;
        public int VisiblePayloads => visuals.Count;
        public int VisibleProducts => products.Count;
        private sealed class Visual { public GameObject Root; public PartVisualAnchor Anchor; }
        private readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        private readonly Dictionary<int, GameObject> products = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Transform[]> productSockets = new Dictionary<int, Transform[]>();
        public Transform ProductSocket(int product, int socket) => productSockets[product][socket];
        public void Clear()
        {
            foreach (var v in visuals.Values) { if (v.Anchor.PayloadRoot != null && v.Anchor.PayloadRoot.parent != v.Root.transform) Release(v.Anchor.PayloadRoot.gameObject); Release(v.Root); }
            foreach (var p in products.Values) Release(p);
            visuals.Clear(); products.Clear(); productSockets.Clear();
        }
        public void Present(AssemblySnapshot snapshot, EP02BakedLayout topology)
        {
            foreach (var p in snapshot.Products)
            {
                if (!products.TryGetValue(p.Id, out var root))
                {
                    root = Instantiate(Layout.ProductCarrierPrefab, transform); root.name = "Product " + p.Id; products.Add(p.Id, root);
                    var sockets = new Transform[6]; foreach (var part in Layout.Parts) { var s = new GameObject(part.PartName + " Socket").transform; s.SetParent(root.transform, false); s.localPosition = Layout.SocketOffset(part.SocketId); sockets[part.SocketId] = s; } productSockets.Add(p.Id, sockets);
                }
                root.transform.position = topology.Product.Sample(p.Distance, out var tangent); root.transform.rotation = Quaternion.LookRotation(tangent);
            }
            var live = new HashSet<int>();
            foreach (var item in snapshot.Items)
            {
                live.Add(item.ItemId);
                if (!visuals.TryGetValue(item.ItemId, out var v))
                {
                    var d = Layout.Part(item.Part.Value); var root = Instantiate(d.WrapperPrefab, transform); root.name = "Item " + item.ItemId + " - " + d.PartName;
                    v = new Visual { Root = root, Anchor = root.GetComponent<PartVisualAnchor>() }; visuals.Add(item.ItemId, v);
                }
                var anchor = v.Anchor; var def = anchor.Definition;
                if (item.Ownership == ItemOwnership.OnBelt || item.Ownership == ItemOwnership.ReservedOnBelt)
                {
                    v.Root.transform.position = topology.Supply[item.Lane].Sample(item.Distance, out var tangent); v.Root.transform.rotation = Quaternion.LookRotation(tangent);
                    anchor.PayloadRoot.SetParent(v.Root.transform, false); def.BeltPose.Apply(anchor.SourceRoot); anchor.Carrier.SetActive(true);
                }
                else
                {
                    anchor.Carrier.SetActive(false);
                    if (item.Ownership == ItemOwnership.HeldByInserter)
                    {
                        var grip = Layout.Arm(item.OwnerId).Grip; anchor.PayloadRoot.SetParent(grip, false); def.GripPose.Apply(anchor.SourceRoot);
                        anchor.PayloadRoot.localRotation = Quaternion.Inverse(grip.rotation) * products[item.ProductId].transform.rotation;
                    }
                    else { anchor.PayloadRoot.SetParent(ProductSocket(item.ProductId, item.OwnerId), false); def.AssemblyPose.Apply(anchor.SourceRoot); }
                }
            }
            foreach (var id in visuals.Keys.Where(id => !live.Contains(id)).ToArray()) { var v = visuals[id]; if (v.Anchor.PayloadRoot.parent != v.Root.transform) Release(v.Anchor.PayloadRoot.gameObject); Release(v.Root); visuals.Remove(id); }
            var active = new HashSet<int>(snapshot.Products.Select(p => p.Id));
            foreach (var id in products.Keys.Where(id => !active.Contains(id)).ToArray()) { Release(products[id]); products.Remove(id); productSockets.Remove(id); }
        }
        private static void Release(GameObject go) { if(go==null)return; if(Application.isPlaying)UnityEngine.Object.Destroy(go);else UnityEngine.Object.DestroyImmediate(go); }
        private void OnDestroy() { Clear(); }
    }
}
