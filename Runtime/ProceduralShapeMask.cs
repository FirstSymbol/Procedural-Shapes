using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(ProceduralShape))]
    [AddComponentMenu("UI/Procedural Shapes/Shape Mask (Soft)")]
    public class ProceduralShapeMask : MonoBehaviour
    {
        [Range(0f, 100f)]
        [Tooltip("Softness of the mask edges (feathering).")]
        public float Softness = 0f;

        public ProceduralShape Shape => m_Shape ? m_Shape : (m_Shape = GetComponent<ProceduralShape>());
        private ProceduralShape m_Shape;

        private void OnEnable() { m_Shape = GetComponent<ProceduralShape>(); }
    }
}