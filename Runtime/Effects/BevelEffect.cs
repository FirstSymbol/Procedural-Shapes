using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Эффект фаски (скоса), создающий иллюзию объема за счет добавления бликов и теней по краям.
    /// </summary>
    [Serializable]
    public class BevelEffect : ProceduralEffect
    {
        /// <summary>
        /// Расстояние (глубина) фаски.
        /// </summary>
        public float Distance = 5f;
        
        /// <summary>
        /// Прозрачность блика (светлой части).
        /// </summary>
        [Range(0f, 1f)] public float HighlightAlpha = 0.8f;
        
        /// <summary>
        /// Прозрачность тени (темной части).
        /// </summary>
        [Range(0f, 1f)] public float ShadowAlpha = 0.8f;
        
        /// <summary>
        /// Угол падения света в градусах.
        /// </summary>
        [Range(0, 360)] public float Angle = 135f;

        public BevelEffect() { Fill.Type = FillType.Solid; Fill.SolidColor = Color.white; }
    }
}