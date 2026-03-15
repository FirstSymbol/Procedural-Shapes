using UnityEngine;
using System;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Описание геометрии и специфических параметров процедурной фигуры.
    /// </summary>
    [Serializable]
    public class ShapeGeometry
    {
        /// <summary> Тип геометрической фигуры. </summary>
        public ShapeType ShapeType = ShapeType.Rectangle;
        
        [Header("Rectangle Settings")]
        /// <summary> Радиусы скругления углов прямоугольника (x: верх-лево, y: верх-право, z: низ-право, w: низ-лево). </summary>
        public Vector4 CornerRadius = Vector4.zero;
        /// <summary> Сглаживание углов прямоугольника (0 - линейное, 1 - максимально плавное). </summary>
        [Range(0f, 1f)] public float CornerSmoothing = 0f;
        
        [Header("Polygon Settings")]
        /// <summary> Количество сторон многоугольника. </summary>
        [Range(3, 128)] public int PolygonSides = 5;
        /// <summary> Степень скругления углов многоугольника. </summary>
        [Range(0f, 1f)] public float PolygonRounding = 0f;
        /// <summary> Угол поворота многоугольника в градусах. </summary>
        public float PolygonRotation = 0f;

        [Header("Star Settings")]
        /// <summary> Количество лучей у звезды. </summary>
        [Range(3, 128)] public int StarPoints = 5;
        /// <summary> Соотношение внутреннего радиуса к внешнему (0.5 - обычная звезда). </summary>
        [Range(0.01f, 1f)] public float StarRatio = 0.5f;
        /// <summary> Скругление внешних вершин звезды. </summary>
        [Range(0f, 1f)] public float StarRoundingOuter = 0f;
        /// <summary> Скругление внутренних вершин звезды. </summary>
        [Range(0f, 1f)] public float StarRoundingInner = 0f;
        /// <summary> Угол поворота звезды в градусах. </summary>
        public float StarRotation = 0f;

        /// <summary>
        /// Упаковывает специфичные для формы параметры в Vector4 для передачи в шейдер.
        /// </summary>
        /// <returns>Вектор с упакованными параметрами.</returns>
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

        /// <summary>
        /// Получает угол поворота фигуры в радианах.
        /// </summary>
        /// <returns>Угол поворота в радианах.</returns>
        public float GetRotationRad()
        {
             if (ShapeType == ShapeType.Polygon) return PolygonRotation * Mathf.Deg2Rad;
             if (ShapeType == ShapeType.Star) return StarRotation * Mathf.Deg2Rad;
             return 0f;
        }
    }
}
