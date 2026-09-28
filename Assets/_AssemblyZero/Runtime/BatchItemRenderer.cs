using System;
using System.Collections.Generic;
using AssemblyZero.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AssemblyZero.Unity
{
    public sealed class BatchItemRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh servoMesh;
        [SerializeField] private Mesh sensorMesh;
        [SerializeField] private Material servoMaterial;
        [SerializeField] private Material sensorMaterial;
        [SerializeField] private Material gapMaterial;
        [SerializeField] private Material boundaryMaterial;
        [SerializeField] private Material endGapMaterial;
        private readonly List<Matrix4x4> servo = new List<Matrix4x4>(10000);
        private readonly List<Matrix4x4> sensor = new List<Matrix4x4>(10000);
        private readonly List<Matrix4x4> gaps = new List<Matrix4x4>(1024);
        private readonly List<Matrix4x4> boundaries = new List<Matrix4x4>(1024);
        private readonly List<Matrix4x4> endGaps = new List<Matrix4x4>(8);
        private readonly Matrix4x4[] batch = new Matrix4x4[1023];
        private RenderParams servoParams;
        private RenderParams sensorParams;
        private RenderParams gapParams, boundaryParams, endGapParams;
        public int VisibleCount { get; private set; }
        public int BatchCount { get; private set; }
        public int DebugMode { get; private set; }
        public float SamplingRatio { get; private set; } = 1f;
        public bool IsRepresentativeSampling => SamplingRatio > 1.001f;
        public string CargoMeshName => servoMesh != null ? servoMesh.name : string.Empty;

        public void Configure(Mesh servo, Mesh sensor, Material servoMat, Material sensorMat, Material gapMat, Material boundaryMat, Material endMat)
        { servoMesh = servo; sensorMesh = sensor; servoMaterial = servoMat; sensorMaterial = sensorMat; gapMaterial = gapMat; boundaryMaterial = boundaryMat; endGapMaterial = endMat; servoParams = BuildParams(servoMaterial); sensorParams = BuildParams(sensorMaterial); gapParams = BuildParams(gapMaterial); boundaryParams = BuildParams(boundaryMaterial); endGapParams = BuildParams(endGapMaterial); }

        private void Awake()
        {
            if (servoMesh == null) servoMesh = BuildServoMesh(); if (sensorMesh == null) sensorMesh = BuildSensorMesh();
            // RenderParams is runtime-only state and is not serialized with the scene. Rebuild every
            // parameter set here, including the Stage 5 diagnostic layers.
            servoParams = BuildParams(servoMaterial); sensorParams = BuildParams(sensorMaterial);
            gapParams = BuildParams(gapMaterial); boundaryParams = BuildParams(boundaryMaterial); endGapParams = BuildParams(endGapMaterial);
        }

        public void SetDebugMode(int stage) => DebugMode = stage;
        public void SetSnapshot(IReadOnlyList<BeltItemSnapshot> items, BakedBeltTopology topology, int stage, int visibleLimit)
        {
            servo.Clear(); sensor.Clear(); gaps.Clear(); boundaries.Clear(); endGaps.Clear(); VisibleCount = Math.Min(items.Count, Math.Max(0, visibleLimit));
            SamplingRatio = VisibleCount > 0 ? items.Count / (float)VisibleCount : 1f;
            for (var i = 0; i < VisibleCount; i++)
            {
                var sourceIndex = VisibleCount == items.Count || VisibleCount <= 1 ? i : (int)((long)i * (items.Count - 1) / (VisibleCount - 1));
                var x = items[sourceIndex]; var p = BeltTopologyBuilder.SampleWorldPose(topology, x.Distance, x.LaneId.Value, out var tangent) + Vector3.down * 0.1f; var matrix = Matrix4x4.TRS(p, Quaternion.LookRotation(tangent, Vector3.up), new Vector3(0.36f, 0.64f, 0.36f)); if (x.ItemType == ItemType.ServoCore) servo.Add(matrix); else sensor.Add(matrix);
                if (stage == 5 && i < 256) { var marker = Matrix4x4.TRS(p + Vector3.up * 0.28f, Quaternion.identity, new Vector3(Mathf.Clamp(x.GapAhead / 48f, 0.05f, 0.38f), 0.045f, 0.045f)); if (x.GapAhead == 0) boundaries.Add(marker); else gaps.Add(marker); if (i == 0 || i == VisibleCount - 1) endGaps.Add(Matrix4x4.TRS(p + Vector3.up * 0.42f, Quaternion.identity, Vector3.one * 0.11f)); }
            }
        }

        private void LateUpdate() { BatchCount = Draw(servoMesh, servoMaterial, servoParams, servo) + Draw(sensorMesh, sensorMaterial, sensorParams, sensor) + Draw(sensorMesh, gapMaterial, gapParams, gaps) + Draw(sensorMesh, boundaryMaterial, boundaryParams, boundaries) + Draw(sensorMesh, endGapMaterial, endGapParams, endGaps); }
        private int Draw(Mesh mesh, Material material, RenderParams parameters, List<Matrix4x4> matrices)
        {
            if (mesh == null || material == null) return 0; var batches = 0;
            for (var offset = 0; offset < matrices.Count; offset += batch.Length) { var count = Math.Min(batch.Length, matrices.Count - offset); matrices.CopyTo(offset, batch, 0, count); Graphics.RenderMeshInstanced(parameters, mesh, 0, batch, count); batches++; }
            return batches;
        }
        private static RenderParams BuildParams(Material material) { var p = new RenderParams(material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, layer = 0, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000) }; return p; }
        private static Mesh BuildServoMesh() { var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var mesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); mesh.name = "ServoCore_RuntimeMesh"; Destroy(primitive); return mesh; }
        private static Mesh BuildSensorMesh() { var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube); var mesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh); mesh.name = "SensorPack_RuntimeMesh"; Destroy(primitive); return mesh; }
    }
}
