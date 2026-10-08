using System;
using UnityEngine;
namespace AssemblyZero.Unity
{
    public sealed class InserterAuthoring : MonoBehaviour
    {
        public int StableId, PartTypeId, SocketId, StationPiece;
        public int[] PickupDistances = new int[2];
        public int PortWidth = 1024, PickingTicks = 12, CarryingTicks = 24, PlacingTicks = 12, ReturningTicks = 24;
        public Transform Grip;
        public Transform[] Joints = Array.Empty<Transform>();
        public Vector3[] Axes = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.up, Vector3.forward, Vector3.up };
        public Vector2[] Limits = { new Vector2(-360,360), new Vector2(-170,170), new Vector2(-170,170), new Vector2(-360,360), new Vector2(-170,170), new Vector2(-360,360) };
        public Renderer StatusLight;
        public float Reach = 3.3f;
        public Vector3 RestOffset = new Vector3(0, 2.4f, .55f);
        public Vector3 RestPosition => transform.TransformPoint(RestOffset);
        public float PositionError { get; private set; }
        public float RotationError { get; private set; }
        private float[] angles;
        private Quaternion[] rests;
        private readonly double[,] matrix = new double[6, 7];
        private readonly double[,] jacobian = new double[6, 6];
        private void Initialize()
        {
            if (angles != null) return;
            angles = new float[6]; rests = new Quaternion[6]; for (var i = 0; i < 6; i++) rests[i] = Quaternion.identity;
            angles[1] = -45; angles[2] = -90; angles[4] = -45; Apply();
        }
        private void Apply() { for (var i = 0; i < 6; i++) Joints[i].localRotation = rests[i] * Quaternion.AngleAxis(angles[i], Axes[i]); }
        public void ResetPose() { Initialize(); Array.Clear(angles, 0, 6); angles[1] = -45; angles[2] = -90; angles[4] = -45; Apply(); Solve(RestPosition, InserterPresenter.ToolRotation, 80); }
        public bool CanReach(Vector3 p, Quaternion q) { ResetPose(); Solve(p, q, 160); return PositionError < .04f && RotationError < 5; }
        // Seed the spherical wrist solver with the fixed two-link arm solution.
        // This prevents the numerical solve from choosing the opposite yaw branch.
        private void Seed(Vector3 position, Quaternion rotation)
        {
            var flange = position - rotation * Vector3.up * .3f;
            var v = transform.InverseTransformVector(flange - Joints[1].position);
            var radial = new Vector2(v.x, v.z).magnitude;
            var distance = Mathf.Max(.001f, Mathf.Sqrt(radial * radial + v.y * v.y));
            var upper = Joints[2].localPosition.magnitude; var fore = Joints[3].localPosition.magnitude;
            var elbow = Mathf.Acos(Mathf.Clamp((distance * distance - upper * upper - fore * fore) / (2 * upper * fore), -1, 1));
            var shoulder = Mathf.Atan2(radial, v.y) - Mathf.Acos(Mathf.Clamp((distance * distance + upper * upper - fore * fore) / (2 * distance * upper), -1, 1));
            angles[0] += Mathf.DeltaAngle(angles[0], Mathf.Atan2(-v.z, v.x) * Mathf.Rad2Deg);
            angles[1] = -shoulder * Mathf.Rad2Deg; angles[2] = -elbow * Mathf.Rad2Deg;
            angles[3] = 0; angles[4] = -180 - angles[1] - angles[2];
            angles[5] += Mathf.DeltaAngle(angles[5], angles[0] - 180);
            for (var i = 0; i < 6; i++) angles[i] = Mathf.Clamp(angles[i], Limits[i].x, Limits[i].y);
            Apply();
        }
        public void Solve(Vector3 position, Quaternion rotation, int iterations = 32)
        {
            Initialize();
            Seed(position, rotation);
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var delta = position - Grip.position; var qr = rotation * Quaternion.Inverse(Grip.rotation);
                if (qr.w < 0) qr = new Quaternion(-qr.x,-qr.y,-qr.z,-qr.w);
                qr.ToAngleAxis(out var degrees, out var direction); if (degrees > 180) degrees -= 360;
                if (!float.IsFinite(direction.x)) direction = Vector3.zero;
                var orient = direction * degrees * Mathf.Deg2Rad * .45f;
                if (delta.magnitude < .002f && Mathf.Abs(degrees) < .3f) break;
                var error = new[] { (double)delta.x, delta.y, delta.z, orient.x, orient.y, orient.z };
                for (var j = 0; j < 6; j++)
                {
                    var axis = Joints[j].TransformDirection(Axes[j]).normalized; var linear = Vector3.Cross(axis, Grip.position - Joints[j].position);
                    jacobian[0,j] = linear.x; jacobian[1,j] = linear.y; jacobian[2,j] = linear.z;
                    jacobian[3,j] = axis.x * .45; jacobian[4,j] = axis.y * .45; jacobian[5,j] = axis.z * .45;
                }
                for (var row = 0; row < 6; row++) { for (var col = 0; col < 6; col++) { double sum = row == col ? .012 : 0; for (var j = 0; j < 6; j++) sum += jacobian[row,j] * jacobian[col,j]; matrix[row,col] = sum; } matrix[row,6] = error[row]; }
                for (var k = 0; k < 6; k++)
                {
                    var divisor = matrix[k,k]; for (var c = k; c <= 6; c++) matrix[k,c] /= divisor;
                    for (var r = 0; r < 6; r++) if (r != k) { var factor = matrix[r,k]; for (var c = k; c <= 6; c++) matrix[r,c] -= factor * matrix[k,c]; }
                }
                for (var j = 0; j < 6; j++) { double value = 0; for (var r = 0; r < 6; r++) value += jacobian[r,j] * matrix[r,6]; angles[j] = Mathf.Clamp(angles[j] + Mathf.Clamp((float)value * Mathf.Rad2Deg, -12, 12), Limits[j].x, Limits[j].y); }
                Apply();
            }
            PositionError = Vector3.Distance(position, Grip.position); RotationError = Quaternion.Angle(rotation, Grip.rotation);
        }
    }
}
