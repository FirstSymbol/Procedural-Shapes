using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Описание заливки процедурной фигуры.
    /// </summary>
    [Serializable]
    public class ShapeFill
    {
        /// <summary> Тип заливки (сплошная, градиент, паттерн). </summary>
        public FillType Type = FillType.Solid;

        /// <summary> Основной цвет при использовании сплошной заливки. </summary>
        public Color SolidColor = Color.white;

        /// <summary> Настройки градиента. </summary>
        public Gradient Gradient = new Gradient();

        /// <summary> Угол наклона градиента в градусах. </summary>
        [Range(0, 360)] public float GradientAngle = 0f;

        /// <summary> Смещение градиента относительно центра фигуры. </summary>
        public Vector2 GradientOffset = Vector2.zero;

        /// <summary> Масштаб (плотность) градиента. </summary>
        [Min(0.01f)] public float GradientScale = 1f;

        [Header("Pattern Settings")]
        /// <summary> Текстура паттерна для заливки. </summary>
        public Texture2D PatternTexture;

        /// <summary> Масштабирование (тайлинг) паттерна. </summary>
        public Vector2 PatternTiling = Vector2.one;

        /// <summary> Смещение текстуры паттерна. </summary>
        public Vector2 PatternOffset = Vector2.zero;
    }
}
