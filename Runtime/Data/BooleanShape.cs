using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Описание дополнительной фигуры для булевых операций (внутри шейдера).
    /// </summary>
    [System.Serializable]
    public class BooleanShape
    {
        /// <summary> Тип выполняемой булевой операции. </summary>
        public BooleanOperation Operation = BooleanOperation.None;
        /// <summary> Тип геометрии дополнительной фигуры. </summary>
        public ShapeType Type = ShapeType.Rectangle;
        
        /// <summary> Размеры дополнительной фигуры. </summary>
        public Vector2 Size = new Vector2(100, 100);
        /// <summary> Смещение относительно центра основной фигуры. </summary>
        public Vector2 Offset = Vector2.zero;
        /// <summary> Угол поворота в градусах. </summary>
        public float Rotation = 0f;

        [Header("Shape Definition")]
        /// <summary> Радиусы скругления углов прямоугольника (для Rectangle). </summary>
        public Vector4 CornerRadius = Vector4.zero;
        /// <summary> Сглаживание углов прямоугольника. </summary>
        [Range(0f, 1f)] public float CornerSmoothing = 0f;
        
        /// <summary> Количество сторон многоугольника (для Polygon). </summary>
        [Range(3, 128)] public int PolygonSides = 5;
        /// <summary> Степень скругления углов многоугольника. </summary>
        [Range(0f, 1f)] public float PolygonRounding = 0f;

        /// <summary> Количество лучей у звезды (для Star). </summary>
        [Range(3, 128)] public int StarPoints = 5;
        /// <summary> Соотношение радиусов звезды. </summary>
        [Range(0.01f, 1f)] public float StarRatio = 0.5f;
        /// <summary> Скругление внешних вершин звезды. </summary>
        [Range(0f, 1f)] public float StarRoundingOuter = 0f;
        /// <summary> Скругление внутренних вершин звезды. </summary>
        [Range(0f, 1f)] public float StarRoundingInner = 0f;

        /// <summary>
        /// Упаковывает специфичные для формы параметры в Vector4 для передачи в шейдер.
        /// </summary>
        /// <returns>Вектор с упакованными параметрами.</returns>
        public Vector4 GetPackedParams()
        {
            if (Type == ShapeType.Rectangle) return CornerRadius;
            else if (Type == ShapeType.Polygon) return new Vector4(PolygonSides, PolygonRounding, 0, 0);
            else if (Type == ShapeType.Star) return new Vector4(StarPoints, StarRatio, StarRoundingOuter, StarRoundingInner);
            return Vector4.zero;
        }
    }
}
