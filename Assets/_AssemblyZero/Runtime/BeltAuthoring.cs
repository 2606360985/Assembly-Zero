using AssemblyZero.Domain;
using UnityEngine;

namespace AssemblyZero.Unity
{
    public sealed class BeltAuthoring : MonoBehaviour
    {
        public Vector2Int Grid;
        public GridDirection Direction = GridDirection.East;
        public BeltPieceShape Shape;
        [Min(1)] public int SpeedUnitsPerTick = 8;
        public bool StartsLine;
        public bool EndsLine;
        public int StablePieceId;
        public Vector2Int OutputGrid => Grid + DirectionVector(Direction);
        public static GridDirection TurnRight(GridDirection direction) => (GridDirection)(((int)direction + 1) & 3);
        public static Vector2Int DirectionVector(GridDirection direction) => direction switch { GridDirection.North => Vector2Int.up, GridDirection.East => Vector2Int.right, GridDirection.South => Vector2Int.down, _ => Vector2Int.left };
    }
}
