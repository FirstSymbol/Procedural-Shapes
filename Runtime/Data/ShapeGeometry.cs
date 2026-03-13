using UnityEngine;
using System;

namespace ProceduralShapes.Runtime
{
    [Serializable]
    public class ShapeGeometry
    {
        public ShapeType ShapeType = ShapeType.Rectangle;
        
        // Rectangle
        public Vector4 CornerRadius = Vector4.zero;
        [Range(0f, 1f)] public float CornerSmoothing = 0f;
        
        // Polygon
        [Range(3, 128)] public int PolygonSides = 5;
        [Range(0f, 1f)] public float PolygonRounding = 0f;
        public float PolygonRotation = 0f;

        // Star
        [Range(3, 128)] public int StarPoints = 5;
        [Range(0.01f, 1f)] public float StarRatio = 0.5f;
        [Range(0f, 1f)] public float StarRoundingOuter = 0f;
        [Range(0f, 1f)] public float StarRoundingInner = 0f;
        public float StarRotation = 0f;

        /// <summary>
        /// Упаковывает специфичные для формы параметры в Vector4 для шейдера.
        /// </summary>
        public Vector4 GetPackedParams()
        {
            switch (ShapeType)
            {
                case ShapeType.Rectangle:
                    return CornerRadius;
                case ShapeType.Polygon:
                    return new Vector4(PolygonSides, PolygonRounding, PolygonRotation * Mathf.Deg2Rad, 0);
                case ShapeType.Star:
                    return new Vector4(StarPoints, StarRatio, StarRoundingOuter, StarRoundingInner);
                default:
                    return Vector4.zero;
            }
        }

        public float GetRotationRad()
        {
             if (ShapeType == ShapeType.Polygon) return PolygonRotation * Mathf.Deg2Rad;
             if (ShapeType == ShapeType.Star) return StarRotation * Mathf.Deg2Rad;
             return 0f;
        }
    }
}