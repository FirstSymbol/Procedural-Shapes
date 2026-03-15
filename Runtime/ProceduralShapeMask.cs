using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Компонент-маска для процедурных фигур.
    /// Позволяет использовать текущую фигуру в качестве мягкой маски для дочерних объектов,
    /// имеющих компонент ProceduralSoftMaskable.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ProceduralShape))]
    [AddComponentMenu("UI/Procedural Shapes/Shape Mask (Soft)")]
    public class ProceduralShapeMask : MonoBehaviour
    {
        [Range(0f, 100f)]
        [Tooltip("Мягкость краев маски (растушевка).")]
        public float Softness = 0f;

        /// <summary> Ссылка на компонент фигуры, определяющий форму маски. </summary>
        public ProceduralShape Shape => m_Shape ? m_Shape : (m_Shape = GetComponent<ProceduralShape>());
        private ProceduralShape m_Shape;

        private void OnEnable() { m_Shape = GetComponent<ProceduralShape>(); }
    }
}