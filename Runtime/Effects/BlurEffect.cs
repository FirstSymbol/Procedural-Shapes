using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [System.Serializable]
    public class BlurEffect : ProceduralEffect
    {
        [Range(0f, 100f)]
        public float Radius = 10f;
    }
}