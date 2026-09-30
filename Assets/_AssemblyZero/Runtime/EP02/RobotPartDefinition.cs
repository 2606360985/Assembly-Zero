using System;
using UnityEngine;

namespace AssemblyZero.Unity
{
    [Serializable] public struct PartPose
    {
        public Vector3 Position, Euler, Scale;
        public PartPose(Vector3 position, Vector3 euler, float scale) { Position = position; Euler = euler; Scale = Vector3.one * scale; }
        public void Apply(Transform target) { target.localPosition = Position; target.localRotation = Quaternion.Euler(Euler); target.localScale = Scale; }
    }
    [CreateAssetMenu(menuName = "Assembly Zero/Robot Part")]
    public sealed class RobotPartDefinition : ScriptableObject
    {
        public int PartTypeId, SocketId;
        public string PartName, CarrierType = "StandardPallet";
        public GameObject SourcePrefab, WrapperPrefab;
        public PartPose BeltPose, GripPose, AssemblyPose;
        public Vector3 AssemblySocketPosition;
        public Color Color = Color.cyan;
        public int OccupancyUnits = 768;
        public Sprite Icon;
    }
}
