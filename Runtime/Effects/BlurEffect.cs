using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Эффект размытия (блюра).
    /// </summary>
    [System.Serializable]
    public class BlurEffect : ProceduralEffect
    {
        /// <summary>
        /// Радиус размытия. Чем выше значение, тем сильнее размытие.
        /// </summary>
        [Range(0f, 100f)]
        public float Radius = 10f;
    }
}