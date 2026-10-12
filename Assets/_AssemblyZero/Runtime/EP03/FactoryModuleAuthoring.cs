using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace AssemblyZero.Unity
{
    [Serializable] public sealed class FactoryPortAuthoring
    {
        public string Id;
        public int Material;
        public bool Output;
        public Transform Anchor, Pickup, Place, Grip;
    }
    public sealed class FactoryModuleAuthoring : MonoBehaviour
    {
        public string InstanceId;
        public Vector2Int Footprint;
        public Transform Visual;
        public FactoryPortAuthoring[] Ports=Array.Empty<FactoryPortAuthoring>();
        public BoxCollider FunctionalBounds;
        public Renderer Status;
        public TextMesh Label;
        public string InterfaceSignature()
        {
            string V(Vector3 v)=>v.x.ToString("F4",CultureInfo.InvariantCulture)+","+v.y.ToString("F4",CultureInfo.InvariantCulture)+","+v.z.ToString("F4",CultureInfo.InvariantCulture);
            return InstanceId+"|"+Footprint+"|"+V(transform.position)+"|"+V(transform.eulerAngles)+"|"+string.Join(";",Ports.OrderBy(p=>p.Id,StringComparer.Ordinal).Select(p=>p.Id+":"+p.Material+":"+p.Output+":"+V(transform.InverseTransformPoint(p.Anchor.position))+":"+V(transform.InverseTransformDirection(p.Anchor.forward))+":"+V(transform.InverseTransformPoint(p.Pickup.position))+":"+V(transform.InverseTransformPoint(p.Place.position))+":"+V(transform.InverseTransformPoint(p.Grip.position))));
        }
        public void Validate()
        {
            if(string.IsNullOrEmpty(InstanceId)||Visual==null||FunctionalBounds==null||Ports.Length==0||Ports.Select(p=>p.Id).Distinct().Count()!=Ports.Length||Ports.Any(p=>p.Anchor==null||p.Pickup==null||p.Place==null||p.Grip==null||new[]{p.Anchor,p.Pickup,p.Place,p.Grip}.Any(t=>t.IsChildOf(Visual))))throw new InvalidOperationException("Invalid module interface: "+InstanceId);
        }
    }
}
