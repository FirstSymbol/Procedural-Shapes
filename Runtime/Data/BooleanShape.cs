using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [System.Serializable]
    public class BooleanShape
    {
        public BooleanOperation Operation = BooleanOperation.None;
        public ShapeType Type = ShapeType.Rectangle;
        
        public Vector2 Size = new Vector2(100, 100);
        public Vector2 Offset = Vector2.zero;
        public float Rotation = 0f;

        [Header("Shape Definition")]
        public Vector4 CornerRadius = Vector4.zero;
        [Range(0f, 1f)] public float CornerSmoothing = 0f;
        
        [Range(3, 128)] public int PolygonSides = 5;
        [Range(0f, 1f)] public float PolygonRounding = 0f;

        [Range(3, 128)] public int StarPoints = 5;
        [Range(0.01f, 1f)] public float StarRatio = 0.5f;
        [Range(0f, 1f)] public float StarRoundingOuter = 0f;
        [Range(0f, 1f)] public float StarRoundingInner = 0f;

        public Vector4 GetPackedParams()
        {
            if (Type == ShapeType.Rectangle) return CornerRadius;
            else if (Type == ShapeType.Polygon) return new Vector4(PolygonSides, PolygonRounding, 0, 0);
            else if (Type == ShapeType.Star) return new Vector4(StarPoints, StarRatio, StarRoundingOuter, StarRoundingInner);
            return Vector4.zero;
        }
    }
}