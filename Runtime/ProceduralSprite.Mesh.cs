using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralSprite
    {
        private void OnPopulateMesh(VertexHelper vh)
        {
            RebuildGradientTexture();

            if (m_DisableRendering)
            {
                vh.Clear();
                return;
            }

            vh.Clear();
            Rect rect = GetRect();
            
            float minX = rect.xMin;
            float maxX = rect.xMax;
            float minY = rect.yMin;
            float maxY = rect.yMax;
            
            Vector2 pivotOffset = GetGeometricCenterOffset();
            float cx = rect.center.x + pivotOffset.x;
            float cy = rect.center.y + pivotOffset.y;
            
            float hw = rect.width * 0.5f * m_ShapeScale2D.x;
            float hh = rect.height * 0.5f * m_ShapeScale2D.y;
            
            minX = Mathf.Min(minX, cx - hw);
            maxX = Mathf.Max(maxX, cx + hw);
            minY = Mathf.Min(minY, cy - hh);
            maxY = Mathf.Max(maxY, cy + hh);

            if (BooleanOperations.Count > 0)
            {
                Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                ExpandBoundsRecursive(this, ref minX, ref maxX, ref minY, ref maxY, worldToLocal);
            }

            float maxExpand = 0f;
            float mainBlurRadius = 0f;
            
            foreach (var effect in Effects)
            {
                if (!effect.Enabled) continue;
                if (effect is DropShadowEffect shadow)
                    maxExpand = Mathf.Max(maxExpand, Mathf.Max(Mathf.Abs(shadow.Offset.x), Mathf.Abs(shadow.Offset.y)) + shadow.Blur * 2f + Mathf.Max(0, shadow.Spread));
                else if (effect is OuterGlowEffect glow)
                    maxExpand = Mathf.Max(maxExpand, glow.Blur * 2f + Mathf.Max(0, glow.Spread));
                else if (effect is StrokeEffect stroke)
                    maxExpand = Mathf.Max(maxExpand, stroke.Alignment == StrokeAlignment.Outside ? stroke.Width : (stroke.Alignment == StrokeAlignment.Center ? stroke.Width * 0.5f : 0f));
                else if (effect is BlurEffect blur)
                    mainBlurRadius = Mathf.Max(mainBlurRadius, blur.Radius);
                else if (effect is BevelEffect bevel)
                    maxExpand = Mathf.Max(maxExpand, bevel.Distance);
            }
            
            maxExpand = Mathf.Max(maxExpand, m_EdgeSoftness);
            maxExpand = Mathf.Max(maxExpand, mainBlurRadius * 2f);
            
            minX -= maxExpand;
            maxX += maxExpand;
            minY -= maxExpand;
            maxY += maxExpand;

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is DropShadowEffect shadow && shadow.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 1, m_EffectAtlasIndices[i], shadow.Fill, new Vector3(shadow.Offset.x, shadow.Offset.y, shadow.Blur), new Vector4(shadow.Spread, m_EdgeSoftness, shadow.Fill.GradientOffset.x, shadow.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is OuterGlowEffect glow && glow.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 1, m_EffectAtlasIndices[i], glow.Fill, new Vector3(0, 0, glow.Blur), new Vector4(glow.Spread, m_EdgeSoftness, glow.Fill.GradientOffset.x, glow.Fill.GradientOffset.y));

            DrawLayerMesh(vh, minX, maxX, minY, maxY, rect, 0, m_MainFillAtlasIndex, MainFill, new Vector3(m_InternalPadding, m_EdgeSoftness, mainBlurRadius), new Vector4(0, 0, MainFill.GradientOffset.x, MainFill.GradientOffset.y), maxExpand);

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is InnerShadowEffect inner && inner.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 3, m_EffectAtlasIndices[i], inner.Fill, new Vector3(inner.Offset.x, inner.Offset.y, inner.Blur), new Vector4(inner.Spread, m_EdgeSoftness, inner.Fill.GradientOffset.x, inner.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is InnerGlowEffect iglow && iglow.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 3, m_EffectAtlasIndices[i], iglow.Fill, new Vector3(0, 0, iglow.Blur), new Vector4(iglow.Spread, m_EdgeSoftness, iglow.Fill.GradientOffset.x, iglow.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is StrokeEffect stroke && stroke.Enabled)
                    DrawLayerMesh(vh, minX, maxX, minY, maxY, rect, 2, m_EffectAtlasIndices[i], stroke.Fill, new Vector3(m_InternalPadding, m_EdgeSoftness, 0), new Vector4(stroke.Width, (float)stroke.Alignment, stroke.Fill.GradientOffset.x, stroke.Fill.GradientOffset.y), maxExpand, new Vector2(stroke.DashSize, stroke.DashSpace));
            
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is BevelEffect bevel && bevel.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 5, m_EffectAtlasIndices[i], bevel.Fill, new Vector3(m_InternalPadding, m_EdgeSoftness, bevel.Distance), new Vector4(bevel.HighlightAlpha, bevel.ShadowAlpha, bevel.Angle * Mathf.Deg2Rad, 0));
        }

        private void DrawLayerMesh(VertexHelper vh, float minX, float maxX, float minY, float maxY, Rect baseRect, int effectType, int textureRowIndex, ShapeFill fill, Vector3 normalData, Vector4 tangentData, float expansion, Vector2 dashData = default)
        {
            bool isRoundedStar = m_ShapeType == ShapeType.Star && (m_StarRoundingOuter > 0.01f || m_StarRoundingInner > 0.01f);
            
            if (m_ShapeType == ShapeType.Rectangle || m_ShapeType == ShapeType.Line || m_ShapeType == ShapeType.Capsule || expansion > 5f || isRoundedStar || BooleanOperations.Count > 0 || m_ShapeType == ShapeType.Path)
            {
                DrawLayerQuad(vh, minX, maxX, minY, maxY, baseRect, effectType, textureRowIndex, fill, normalData, tangentData, dashData);
                return;
            }

            int segments = 32;
            if (m_ShapeType == ShapeType.Polygon) segments = m_PolygonSides;
            else if (m_ShapeType == ShapeType.Star) segments = m_StarPoints * 2;
            else if (m_ShapeType == ShapeType.Triangle) segments = 3;

            Vector2 pivotOffset = GetGeometricCenterOffset();
            float cx = baseRect.center.x + pivotOffset.x;
            float cy = baseRect.center.y + pivotOffset.y;
            
            Vector2 ls = transform.lossyScale;
            ls.x = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
            ls.y = Mathf.Max(Mathf.Abs(ls.y), 0.001f);
            Vector2 uvScale = new Vector2(ls.x * GetStretchScale().x, ls.y * GetStretchScale().y);

            float hwBase = baseRect.width * 0.5f * m_ShapeScale2D.x;
            float hhBase = baseRect.height * 0.5f * m_ShapeScale2D.y;

            if (!m_StretchToFill && (m_ShapeType == ShapeType.Polygon || m_ShapeType == ShapeType.Star || m_ShapeType == ShapeType.Ring || m_ShapeType == ShapeType.Triangle || m_ShapeType == ShapeType.Heart))
            {
                float maxR = Mathf.Min(hwBase * ls.x, hhBase * ls.y);
                hwBase = maxR / ls.x;
                hhBase = maxR / ls.y;
            }

            float radialExpansion = expansion;
            if (m_ShapeType == ShapeType.Star) radialExpansion = expansion * 3.5f;
            else if (m_ShapeType == ShapeType.Polygon || m_ShapeType == ShapeType.Triangle || m_ShapeType == ShapeType.Heart) radialExpansion = expansion * 2f;

            // In local space, radial expansion must also be inverse-scaled so it is uniform in world space!
            float expandX = radialExpansion / ls.x;
            float expandY = radialExpansion / ls.y;

            int startVert = vh.currentVertCount;
            UIVertex vert = UIVertex.simpleVert;
            vert.color = fill.Type == FillType.Solid ? fill.SolidColor : Color.white;
            vert.normal = normalData;
            
            Vector4 finalTangent = tangentData;
            if (fill.Type == FillType.Pattern)
            {
                finalTangent = new Vector4(fill.PatternTiling.x, fill.PatternTiling.y, fill.PatternOffset.x, fill.PatternOffset.y);
            }
            vert.tangent = finalTangent;
            
            vert.uv1 = GetPackedShapeParams();
            vert.uv2 = GetPackedBaseData(baseRect, effectType, m_CornerSmoothing);
            vert.uv3 = GetPackedFillParams(textureRowIndex, fill);

            vert.position = new Vector3(cx, cy);
            vert.uv0 = new Vector4(0, 0, dashData.x, dashData.y);
            vh.AddVert(vert);

            float angleStep = 360f / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i * angleStep) * Mathf.Deg2Rad;
                float s = Mathf.Sin(angle);
                float c = Mathf.Cos(angle);
                
                float r = 1f;
                if (m_ShapeType == ShapeType.Star && (i % 2 != 0)) r = m_StarRatio;

                Vector2 pos = new Vector2(cx + s * hwBase * r + s * expandX, cy + c * hhBase * r + c * expandY);
                vert.position = pos;
                vert.uv0 = new Vector4((pos.x - cx) * uvScale.x, (pos.y - cy) * uvScale.y, dashData.x, dashData.y);
                vh.AddVert(vert);

                if (i > 0)
                {
                    vh.AddTriangle(startVert, startVert + i, startVert + i + 1);
                }
            }
        }

        public Vector4 GetPackedShapeParams()
        {
            if (m_ShapeType == ShapeType.Rectangle) return m_CornerRadius;
            if (m_ShapeType == ShapeType.Polygon) return new Vector4(m_PolygonSides, m_PolygonRounding, 0, 0);
            if (m_ShapeType == ShapeType.Star) return new Vector4(m_StarPoints, m_StarRatio, m_StarRoundingOuter, m_StarRoundingInner);
            if (m_ShapeType == ShapeType.Capsule) return new Vector4(m_CapsuleRounding, 0, 0, 0);
            if (m_ShapeType == ShapeType.Line) return new Vector4(m_LineStart.x, m_LineStart.y, m_LineEnd.x, m_LineEnd.y);
            if (m_ShapeType == ShapeType.Ring) return new Vector4(m_RingInnerRadius, m_RingStartAngle * Mathf.Deg2Rad, m_RingEndAngle * Mathf.Deg2Rad, 0);
            if (m_ShapeType == ShapeType.Path) return new Vector4(m_ShapePath.Closed ? 1f : 0f, m_ShapePath.Thickness, 0, 0);
            if (m_ShapeType == ShapeType.Triangle || m_ShapeType == ShapeType.Heart) return new Vector4(0, 0, 0, 0);
            return Vector4.zero;
        }

        public Vector2 GetStretchScale()
        {
            Rect r = GetRect();
            Vector2 ls = transform.lossyScale;
            ls.x = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
            ls.y = Mathf.Max(Mathf.Abs(ls.y), 0.001f);
            
            float scaledW = r.width * m_ShapeScale2D.x * ls.x;
            float scaledH = r.height * m_ShapeScale2D.y * ls.y;
            if (!m_StretchToFill || scaledW <= 0.001f || scaledH <= 0.001f) return Vector2.one;
            float minSize = Mathf.Min(scaledW, scaledH);
            return new Vector2(minSize / scaledW, minSize / scaledH);
        }

        private Vector4 GetPackedBaseData(Rect rect, int effectType, float smoothing)
        {
            Vector2 ls = transform.lossyScale;
            ls.x = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
            ls.y = Mathf.Max(Mathf.Abs(ls.y), 0.001f);
            Vector2 stretch = GetStretchScale();
            return new Vector4(rect.width * m_ShapeScale2D.x * ls.x * stretch.x, rect.height * m_ShapeScale2D.y * ls.y * stretch.y, smoothing, effectType);
        }

        private Vector4 GetPackedFillParams(int rowIndex, ShapeFill fill)
        {
            float packedRow = (float)rowIndex;
            float type = (float)fill.Type;
            
            float angle = fill.GradientAngle;
            float packedNoiseAmount = Mathf.Floor(angle) + (Mathf.Clamp(m_EdgeNoiseAmount, 0f, 50f) / 100f);
            
            float scaleOrNoise = fill.GradientScale;
            if (m_EdgeNoiseAmount > 0.001f)
            {
                scaleOrNoise = m_EdgeNoiseScale;
            }

            if (fill.Type == FillType.Pattern)
            {
                packedNoiseAmount = 0f + (Mathf.Clamp(m_EdgeNoiseAmount, 0f, 50f) / 100f);
            }
            
            return new Vector4(packedRow, type, packedNoiseAmount, scaleOrNoise);
        }

        internal static readonly HashSet<ProceduralSprite> s_VisitedShapes = new HashSet<ProceduralSprite>();

        private void ExpandBoundsRecursive(ProceduralSprite currentShape, ref float minX, ref float maxX, ref float minY, ref float maxY, Matrix4x4 rootWorldToLocal)
        {
            s_VisitedShapes.Clear();
            s_VisitedShapes.Add(this);
            ExpandBoundsRecursiveInternal(currentShape, ref minX, ref maxX, ref minY, ref maxY, rootWorldToLocal);
            s_VisitedShapes.Clear();
        }

        private void ExpandBoundsRecursiveInternal(ProceduralSprite currentShape, ref float minX, ref float maxX, ref float minY, ref float maxY, Matrix4x4 rootWorldToLocal)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (s_VisitedShapes.Contains(input.SourceShape)) continue;

                ProceduralSprite other = input.SourceShape;
                s_VisitedShapes.Add(other);
                
                Transform rt = other.transform;
                Rect r = other.GetRect();
                
                Vector2 scale = other.ShapeScale;
                Vector2 pivotOffset = other.GetGeometricCenterOffset();
                
                float margin = other.m_EdgeSoftness;
                if (other.m_InternalPadding < 0) margin -= other.m_InternalPadding;
                margin += input.Smoothness; 
                
                float hw = r.width * 0.5f * scale.x + margin;
                float hh = r.height * 0.5f * scale.y + margin;
                
                float cx = r.center.x + pivotOffset.x;
                float cy = r.center.y + pivotOffset.y;

                if (other.m_ShapeType == ShapeType.Path && other.m_ShapePath != null && other.m_ShapePath.Points != null)
                {
                    foreach (var pt in other.m_ShapePath.Points)
                    {
                        Vector3 worldPt = rt.TransformPoint(pt.Position);
                        Vector3 localPt = rootWorldToLocal.MultiplyPoint3x4(worldPt);
                        minX = Mathf.Min(minX, localPt.x);
                        maxX = Mathf.Max(maxX, localPt.x);
                        minY = Mathf.Min(minY, localPt.y);
                        maxY = Mathf.Max(maxY, localPt.y);

                        if (pt.Type == PathPointType.Bezier)
                        {
                            Vector3 cp1 = rt.TransformPoint(pt.ControlPoint1);
                            Vector3 cp2 = rt.TransformPoint(pt.ControlPoint2);
                            Vector3 lcp1 = rootWorldToLocal.MultiplyPoint3x4(cp1);
                            Vector3 lcp2 = rootWorldToLocal.MultiplyPoint3x4(cp2);
                            minX = Mathf.Min(minX, lcp1.x, lcp2.x);
                            maxX = Mathf.Max(maxX, lcp1.x, lcp2.x);
                            minY = Mathf.Min(minY, lcp1.y, lcp2.y);
                            maxY = Mathf.Max(maxY, lcp1.y, lcp2.y);
                        }
                    }
                    float thick = other.m_ShapePath.Thickness * 0.5f + margin;
                    minX -= thick; maxX += thick; minY -= thick; maxY += thick;
                }
                else if (other.m_ShapeType == ShapeType.Line)
                {
                    Vector3 worldStart = rt.TransformPoint(other.m_LineStart + pivotOffset);
                    Vector3 worldEnd = rt.TransformPoint(other.m_LineEnd + pivotOffset);
                    Vector3 localStart = rootWorldToLocal.MultiplyPoint3x4(worldStart);
                    Vector3 localEnd = rootWorldToLocal.MultiplyPoint3x4(worldEnd);
                    
                    float lineMargin = margin + other.m_LineWidth * 0.5f;
                    
                    minX = Mathf.Min(minX, localStart.x - lineMargin, localEnd.x - lineMargin);
                    maxX = Mathf.Max(maxX, localStart.x + lineMargin, localEnd.x + lineMargin);
                    minY = Mathf.Min(minY, localStart.y - lineMargin, localEnd.y - lineMargin);
                    maxY = Mathf.Max(maxY, localStart.y + lineMargin, localEnd.y + lineMargin);
                }
                else
                {
                    if (other.m_ShapeType == ShapeType.Rectangle && other.m_CornerSmoothing > 0.001f)
                    {
                        hw += Mathf.Max(other.m_CornerRadius.x, other.m_CornerRadius.y, other.m_CornerRadius.z, other.m_CornerRadius.w) * 0.5f;
                        hh += Mathf.Max(other.m_CornerRadius.x, other.m_CornerRadius.y, other.m_CornerRadius.z, other.m_CornerRadius.w) * 0.5f;
                    }

                    s_Corners[0] = new Vector3(cx - hw, cy - hh, 0);
                    s_Corners[1] = new Vector3(cx - hw, cy + hh, 0);
                    s_Corners[2] = new Vector3(cx + hw, cy + hh, 0);
                    s_Corners[3] = new Vector3(cx + hw, cy - hh, 0);
                    
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 worldPt = rt.TransformPoint(s_Corners[i]);
                        Vector3 localPt = rootWorldToLocal.MultiplyPoint3x4(worldPt);
                        minX = Mathf.Min(minX, localPt.x);
                        maxX = Mathf.Max(maxX, localPt.x);
                        minY = Mathf.Min(minY, localPt.y);
                        maxY = Mathf.Max(maxY, localPt.y);
                    }
                }

                ExpandBoundsRecursiveInternal(other, ref minX, ref maxX, ref minY, ref maxY, rootWorldToLocal);
            }
        }

        private void DrawLayerQuad(VertexHelper vh, float minX, float maxX, float minY, float maxY, Rect baseRect, int effectType, int textureRowIndex, ShapeFill fill, Vector3 normalData, Vector4 tangentData, Vector2 dashData = default)
        {
            Vector4 uv1_shapeParams = GetPackedShapeParams();
            float customSmoothing = m_CornerSmoothing;
            if (m_ShapeType == ShapeType.Line) customSmoothing = m_LineWidth;

            Vector4 uv2_baseData = GetPackedBaseData(baseRect, effectType, customSmoothing);
            
            Vector4 uv3_fillParams = GetPackedFillParams(textureRowIndex, fill);

            Vector2 pivotOffset = GetGeometricCenterOffset();

            int startIndex = vh.currentVertCount;
            float cx = baseRect.center.x + pivotOffset.x;
            float cy = baseRect.center.y + pivotOffset.y;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = fill.Type == FillType.Solid ? fill.SolidColor : Color.white; 
            vert.normal = normalData;
            
            Vector4 finalTangent = tangentData;
            if (fill.Type == FillType.Pattern)
            {
                finalTangent = new Vector4(fill.PatternTiling.x, fill.PatternTiling.y, fill.PatternOffset.x, fill.PatternOffset.y);
            }
            vert.tangent = finalTangent;
            
            vert.uv1 = uv1_shapeParams;
            vert.uv2 = uv2_baseData;
            vert.uv3 = uv3_fillParams;
            
            Vector2 ls = transform.lossyScale;
            ls.x = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
            ls.y = Mathf.Max(Mathf.Abs(ls.y), 0.001f);
            Vector2 uvScale = new Vector2(ls.x * GetStretchScale().x, ls.y * GetStretchScale().y);

            vert.position = new Vector3(minX, minY); vert.uv0 = new Vector4((minX - cx) * uvScale.x, (minY - cy) * uvScale.y, dashData.x, dashData.y); vh.AddVert(vert);
            vert.position = new Vector3(minX, maxY); vert.uv0 = new Vector4((minX - cx) * uvScale.x, (maxY - cy) * uvScale.y, dashData.x, dashData.y); vh.AddVert(vert);
            vert.position = new Vector3(maxX, maxY); vert.uv0 = new Vector4((maxX - cx) * uvScale.x, (maxY - cy) * uvScale.y, dashData.x, dashData.y); vh.AddVert(vert);
            vert.position = new Vector3(maxX, minY); vert.uv0 = new Vector4((maxX - cx) * uvScale.x, (minY - cy) * uvScale.y, dashData.x, dashData.y); vh.AddVert(vert);

            vh.AddTriangle(startIndex + 0, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex + 0);
        }
    }
}