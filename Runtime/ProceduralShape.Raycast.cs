using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralShape
    {
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
                return false;

            Vector2 pivotOffset = GetGeometricCenterOffset();
            Vector2 p = localPoint - (rectTransform.rect.center + pivotOffset);

            Vector2 halfSize = rectTransform.rect.size * 0.5f * m_ShapeScale2D;
            float d = SDFMathUtils.GetSDF_CPU(p, halfSize, m_ShapeType, m_CornerSmoothing, GetPackedShapeParams());
            d += m_InternalPadding;

            if (BooleanOperations.Count > 0)
            {
                foreach (var op in BooleanOperations)
                {
                    if (op.SourceShape == null || !op.SourceShape.isActiveAndEnabled || op.Operation == BooleanOperation.None) continue;
                    
                    Vector3 worldPos = rectTransform.TransformPoint(localPoint);
                    Vector2 otherLocal = op.SourceShape.rectTransform.InverseTransformPoint(worldPos);
                    Vector2 otherPivot = op.SourceShape.GetGeometricCenterOffset();
                    Vector2 p2 = otherLocal - (op.SourceShape.rectTransform.rect.center + otherPivot);
                    
                    float d2 = SDFMathUtils.GetSDF_CPU(p2, op.SourceShape.rectTransform.rect.size * 0.5f * op.SourceShape.ShapeScale, 
                                         op.SourceShape.m_ShapeType, op.SourceShape.m_CornerSmoothing, op.SourceShape.GetPackedShapeParams());
                    
                    if (op.Operation == BooleanOperation.Union) d = Mathf.Min(d, d2);
                    else if (op.Operation == BooleanOperation.Subtraction) d = Mathf.Max(d, -d2);
                    else if (op.Operation == BooleanOperation.Intersection) d = Mathf.Max(d, d2);
                }
            }

            return d <= m_EdgeSoftness; 
        }
    }
}