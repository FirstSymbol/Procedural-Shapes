using UnityEngine;
using System.Collections.Generic;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralShape
    {
        /// <summary>
        /// Проверяет, попадает ли точка экрана в границы процедурной фигуры.
        /// Использует CPU-реализацию SDF для точного определения границ.
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
                return false;

            Vector2 pivotOffset = GetGeometricCenterOffset();
            Vector2 p = localPoint - (rectTransform.rect.center + pivotOffset);

            Vector2 stretch = GetStretchScale();
            p.x *= stretch.x;
            p.y *= stretch.y;

            float d = 0;
            if (m_ShapeType == ShapeType.Path)
            {
                d = GetPathDistance_CPU(p);
            }
            else
            {
                Vector2 halfSize = rectTransform.rect.size * 0.5f * m_ShapeScale2D;
                halfSize.x *= stretch.x;
                halfSize.y *= stretch.y;
                d = SDFMathUtils.GetSDF_CPU(p, halfSize, m_ShapeType, m_CornerSmoothing, GetPackedShapeParams());
            }

            d += m_InternalPadding;

            // Учет булевых операций (резаков)
            if (BooleanOperations.Count > 0)
            {
                foreach (var op in BooleanOperations)
                {
                    if (op.SourceShape == null || !op.SourceShape.isActiveAndEnabled || op.Operation == BooleanOperation.None) continue;
                    
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
                    
                    if (op.Operation == BooleanOperation.Union) d = Mathf.Min(d, d2);
                    else if (op.Operation == BooleanOperation.Subtraction) d = Mathf.Max(d, -d2);
                    else if (op.Operation == BooleanOperation.Intersection) d = Mathf.Max(d, d2);
                }
            }

            return d <= m_EdgeSoftness; 
        }

        private float GetPathDistance_CPU(Vector2 p)
        {
            if (m_FlattenedPath == null || m_FlattenedPath.Count < 2) 
            {
                 PathUtils.FlattenPath(m_ShapePath, m_FlattenedPath);
            }
            
            if (m_FlattenedPath == null || m_FlattenedPath.Count < 2) return 100000.0f;

            float minD2 = float.MaxValue;
            bool inside = false;
            Vector2 stretch = GetStretchScale();

            for (int i = 0, j = m_FlattenedPath.Count - 1; i < m_FlattenedPath.Count; j = i, i++)
            {
                Vector2 vi = new Vector2(m_FlattenedPath[i].x * stretch.x, m_FlattenedPath[i].y * stretch.y);
                Vector2 vj = new Vector2(m_FlattenedPath[j].x * stretch.x, m_FlattenedPath[j].y * stretch.y);

                // Segment distance
                Vector2 e = vj - vi;
                Vector2 w = p - vi;
                float h = Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(Vector2.Dot(e, e), 0.0001f));
                minD2 = Mathf.Min(minD2, (w - e * h).sqrMagnitude);

                // Point-in-polygon (winding)
                if (m_ShapePath.Closed)
                {
                    if ((vi.y >= p.y) != (vj.y >= p.y) &&
                        (p.x < (vj.x - vi.x) * (p.y - vi.y) / Mathf.Max(vj.y - vi.y, 0.0001f) + vi.x))
                    {
                        inside = !inside;
                    }
                }
            }

            float d = Mathf.Sqrt(minD2);
            if (m_ShapePath.Closed)
            {
                return inside ? -d : d;
            }
            return d - m_ShapePath.Thickness * 0.5f;
        }
    }
}