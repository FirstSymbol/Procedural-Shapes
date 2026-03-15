using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Входные данные для булевой операции с процедурными фигурами.
    /// </summary>
    [System.Serializable]
    public class BooleanInput
    {
        /// <summary> Операция (объединение, вычитание, пересечение). </summary>
        public BooleanOperation Operation = BooleanOperation.Subtraction;
        
        /// <summary> Фигура, участвующая в операции. </summary>
        public ProceduralShape SourceShape;
        
        /// <summary> Радиус мягкого перехода между фигурами. </summary>
        [Range(0f, 200f)]
        [Tooltip("Радиус плавного перехода между фигурами.")]
        public float Smoothness = 0f;
    }
}
