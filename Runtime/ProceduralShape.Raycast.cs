using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralShape
    {
        /// <summary>
        /// Проверяет, попадает ли точка экрана в границы процедурной фигуры.
        /// Использует CPU-реализацию SDF для точного определения границ, включая булевы операции.
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            // Перевод точки экрана в локальные координаты RectTransform
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
                return false;

            // Расчет смещения относительно центра геометрии
            Vector2 pivotOffset = GetGeometricCenterOffset();
            Vector2 p = localPoint - (rectTransform.rect.center + pivotOffset);

            // Проверка SDF для основной фигуры
            Vector2 halfSize = rectTransform.rect.size * 0.5f * m_ShapeScale2D;
            float d = SDFMathUtils.GetSDF_CPU(p, halfSize, m_ShapeType, m_CornerSmoothing, GetPackedShapeParams());
            d += m_InternalPadding;

            // Учет булевых операций (резаков) при проверке Raycast
            if (BooleanOperations.Count > 0)
            {
                foreach (var op in BooleanOperations)
                {
                    if (op.SourceShape == null || !op.SourceShape.isActiveAndEnabled || op.Operation == BooleanOperation.None) continue;
                    
                    // Перевод точки в локальное пространство фигуры-оператора
                    Vector3 worldPos = rectTransform.TransformPoint(localPoint);
                    Vector2 otherLocal = op.SourceShape.rectTransform.InverseTransformPoint(worldPos);
                    Vector2 otherPivot = op.SourceShape.GetGeometricCenterOffset();
                    Vector2 p2 = otherLocal - (op.SourceShape.rectTransform.rect.center + otherPivot);
                    
                    float d2 = SDFMathUtils.GetSDF_CPU(p2, op.SourceShape.rectTransform.rect.size * 0.5f * op.SourceShape.ShapeScale, 
                                         op.SourceShape.m_ShapeType, op.SourceShape.m_CornerSmoothing, op.SourceShape.GetPackedShapeParams());
                    
                    // Комбинирование результатов SDF по правилам булевой логики
                    if (op.Operation == BooleanOperation.Union) d = Mathf.Min(d, d2);
                    else if (op.Operation == BooleanOperation.Subtraction) d = Mathf.Max(d, -d2);
                    else if (op.Operation == BooleanOperation.Intersection) d = Mathf.Max(d, d2);
                }
            }

            // Точка считается валидной, если расстояние до границы меньше или равно мягкости края (антиалиасинга)
            return d <= m_EdgeSoftness; 
        }
    }
}