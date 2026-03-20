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

            Vector2 stretch = GetStretchScale();
            p.x *= stretch.x;
            p.y *= stretch.y;

            // Проверка SDF для основной фигуры
            Vector2 halfSize = rectTransform.rect.size * 0.5f * m_ShapeScale2D;
            halfSize.x *= stretch.x;
            halfSize.y *= stretch.y;

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
                    Vector2 otherPivot = op.SourceShape.GetGeometricCenterOffset();
                    Vector3 targetPosInRootLocal = rectTransform.worldToLocalMatrix.MultiplyPoint3x4(op.SourceShape.transform.TransformPoint((Vector3)(op.SourceShape.rectTransform.rect.center + otherPivot)));
                    Vector3 finalPos = targetPosInRootLocal - (Vector3)pivotOffset;
                    finalPos.x *= stretch.x;
                    finalPos.y *= stretch.y;

                    Vector2 p2_shader = p - new Vector2(finalPos.x, finalPos.y);

                    float relativeRot = op.SourceShape.rectTransform.eulerAngles.z - rectTransform.eulerAngles.z;
                    float rotRad = relativeRot * Mathf.Deg2Rad;
                    float s = Mathf.Sin(-rotRad);
                    float c = Mathf.Cos(-rotRad);
                    Vector2 p2 = new Vector2(p2_shader.x * c - p2_shader.y * s, p2_shader.x * s + p2_shader.y * c);

                    Vector3 lossyScaleRatio = new Vector3(
                        rectTransform.lossyScale.x != 0 ? op.SourceShape.rectTransform.lossyScale.x / rectTransform.lossyScale.x : 0, 
                        rectTransform.lossyScale.y != 0 ? op.SourceShape.rectTransform.lossyScale.y / rectTransform.lossyScale.y : 0, 
                        1f);
                    float finalW = op.SourceShape.rectTransform.rect.width * lossyScaleRatio.x * op.SourceShape.ShapeScale.x * stretch.x;
                    float finalH = op.SourceShape.rectTransform.rect.height * lossyScaleRatio.y * op.SourceShape.ShapeScale.y * stretch.y;

                    float d2 = SDFMathUtils.GetSDF_CPU(p2, new Vector2(finalW * 0.5f, finalH * 0.5f), 
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