using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AssemblyZero.Unity.Editor
{
    public static class RobotPartAudit
    {
        public static readonly string[] Names = { "Head", "Body", "ArmA", "ArmB", "LegA", "LegB" };
        public static readonly string[] Paths = { "Assets/Prefabs/Robot_head.prefab", "Assets/Prefabs/Robot_body.prefab", "Assets/Prefabs/Robot_arm1.prefab", "Assets/Prefabs/Robot_arm2.prefab", "Assets/Prefabs/Robot_leg1.prefab", "Assets/Prefabs/Robot_leg2.prefab" };
        public sealed class PartAudit { public GameObject Prefab; public Bounds LocalBounds, CommonBounds; public Matrix4x4 Registration; public int MeshCount; }
        public static PartAudit[] Inspect()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Robots.fbx"); if (source == null) throw new InvalidOperationException("Missing Robots.fbx");
            var sourceMeshes = source.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh != null).ToDictionary(x => x.sharedMesh);
            var seen = new HashSet<Mesh>(); var result = new PartAudit[6];
            for (var i = 0; i < 6; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[i]); if (prefab == null) throw new InvalidOperationException("Missing " + Paths[i]);
                if (prefab.GetComponentsInChildren<Collider>(true).Length != 0 || prefab.GetComponentsInChildren<Animator>(true).Length != 0) throw new InvalidOperationException("Unexpected physics/animation in source " + Paths[i]);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true); if (meshes.Length == 0) throw new InvalidOperationException("Empty part");
                Matrix4x4? registration = null; var local = new Bounds(); var common = new Bounds(); var first = true;
                foreach (var mesh in meshes)
                {
                    if (!seen.Add(mesh.sharedMesh) || !sourceMeshes.TryGetValue(mesh.sharedMesh, out var original)) throw new InvalidOperationException("Duplicate or unmatched source mesh " + mesh.name);
                    var a = prefab.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                    var b = source.transform.worldToLocalMatrix * original.transform.localToWorldMatrix;
                    var delta = b * a.inverse;
                    if (registration.HasValue) { for (var j = 0; j < 16; j++) if (Mathf.Abs(registration.Value[j] - delta[j]) > .002f) throw new InvalidOperationException("Part has inconsistent common FBX coordinates: " + Paths[i]); } else registration = delta;
                    Encapsulate(ref local, mesh.sharedMesh.bounds, a, ref first);
                }
                var commonFirst = true; foreach (var mesh in meshes) Encapsulate(ref common, mesh.sharedMesh.bounds, source.transform.worldToLocalMatrix * sourceMeshes[mesh.sharedMesh].transform.localToWorldMatrix, ref commonFirst);
                result[i] = new PartAudit { Prefab = prefab, LocalBounds = local, CommonBounds = common, Registration = registration.Value, MeshCount = meshes.Length };
            }
            if (seen.Count != sourceMeshes.Count) throw new InvalidOperationException("Six prefabs do not cover the complete FBX"); return result;
        }
        private static void Encapsulate(ref Bounds merged, Bounds bounds, Matrix4x4 matrix, ref bool first)
        {
            for (var i = 0; i < 8; i++) { var p = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))); if (first) { merged = new Bounds(p, Vector3.zero); first = false; } else merged.Encapsulate(p); }
        }
        [MenuItem("Tools/Assembly Zero/Audit EP02 Robot Parts")]
        public static void LogAudit() { var parts = Inspect(); Debug.Log("EP02 common-coordinate audit: " + parts.Sum(x => x.MeshCount) + " meshes, no missing/duplicate meshes.\n" + string.Join("\n", parts.Select((p, i) => Names[i] + ": " + p.CommonBounds))); }
    }
}
