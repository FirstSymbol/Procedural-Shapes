using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [System.Serializable]
    public class BooleanInput
    {
        public BooleanOperation Operation = BooleanOperation.Subtraction;
        public ProceduralShape SourceShape;
        
        [Range(0f, 200f)]
        [Tooltip("Radius of the smooth transition between shapes.")]
        public float Smoothness = 0f;
    }
}