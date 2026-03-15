using UnityEngine;
using System.Collections.Generic;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralShape
    {
        private int m_ActiveBoolCount;
        private int m_ActiveMaskBoolCount;

        /// <summary>
        /// Возвращает модифицированный материал с установленными параметрами SDF.
        /// Управляет выбором между использованием пула материалов и созданием уникального экземпляра.
        /// </summary>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            // --- 1. Подготовка данных основной фигуры ---
            if (m_ShapeType == ShapeType.Path)
            {
                if (m_FlattenedPath == null) m_FlattenedPath = new List<Vector2>();
                if (m_FlattenedPath.Count == 0)
                {
                    PathUtils.FlattenPath(m_ShapePath, m_FlattenedPath);
                }
            }

            Matrix4x4 worldToLocal = rectTransform.worldToLocalMatrix;
            Vector2 selfCenterOffset = GetGeometricCenterOffset();
            m_ActiveBoolCount = 0;
            s_VisitedShapes.Clear();
            s_VisitedShapes.Add(this);
            
            // Проверка, является ли один из операторов путем, и подготовка его данных
            ProceduralShape firstPathOperator = null;
            foreach(var op in BooleanOperations) {
                if (op.SourceShape != null && op.SourceShape.m_ShapeType == ShapeType.Path) {
                    firstPathOperator = op.SourceShape;
                    if (firstPathOperator.m_FlattenedPath == null || firstPathOperator.m_FlattenedPath.Count == 0)
                        PathUtils.FlattenPath(firstPathOperator.m_ShapePath, firstPathOperator.m_FlattenedPath);
                    break;
                }
            }

            // Сбор всех активных булевых операций
            CollectBooleanOps(this, BooleanOperation.Union, ref m_ActiveBoolCount, worldToLocal, selfCenterOffset);
            s_VisitedShapes.Clear();

            // --- 2. Подготовка данных маски (если есть) ---
            bool hasMask = false;
            Vector4 maskParams = Vector4.zero;
            Vector4 maskSize = Vector4.zero;
            Vector4 maskShape = Vector4.zero;
            Vector4 maskFillParams = Vector4.zero;
            Vector4 maskFillOffset = Vector4.zero;
            Texture maskTex = null;
            Matrix4x4 maskMatrix = Matrix4x4.identity;
            m_ActiveMaskBoolCount = 0;

            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
                m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled && m_CachedMask.Shape != null && m_CachedMask.Shape != this)
            {
                hasMask = true;
                ProceduralShape maskS = m_CachedMask.Shape;
                
                // Расчет матрицы перехода из локального пространства текущего объекта в пространство SDF маски
                Matrix4x4 childLocalToWorld = rectTransform.localToWorldMatrix;
                Matrix4x4 maskWorldToLocal = maskS.rectTransform.worldToLocalMatrix;
                Vector2 maskSizeRaw = maskS.rectTransform.rect.size;
                Vector2 maskPivot = maskS.rectTransform.pivot;
                Vector2 maskPivotOffset = maskS.GetGeometricCenterOffset();
                Vector3 maskRectCenterFromPivot = new Vector3((0.5f - maskPivot.x) * maskSizeRaw.x, (0.5f - maskPivot.y) * maskSizeRaw.y, 0f);
                Vector3 maskTotalCenterCorrection = maskRectCenterFromPivot + (Vector3)maskPivotOffset;
                Matrix4x4 maskCenterTranslate = Matrix4x4.Translate(-maskTotalCenterCorrection);
                Matrix4x4 localToMaskSDF = maskCenterTranslate * maskWorldToLocal * childLocalToWorld;
                
                Vector2 childGeomCenterLocal = rectTransform.rect.center + selfCenterOffset;
                Matrix4x4 childGeomToLocal = Matrix4x4.Translate(new Vector3(childGeomCenterLocal.x, childGeomCenterLocal.y, 0));
                maskMatrix = localToMaskSDF * childGeomToLocal;

                Vector2 mScale = maskS.ShapeScale;
                maskSize = new Vector4(maskSizeRaw.x * mScale.x, maskSizeRaw.y * mScale.y, 0, 0);
                maskParams = new Vector4(1f, (float)maskS.m_ShapeType, maskS.m_CornerSmoothing, m_CachedMask.Softness + maskS.m_EdgeSoftness);
                maskShape = maskS.GetPackedShapeParams();
                maskTex = maskS.mainTexture;
                
                ShapeFill mFill = maskS.MainFill;
                int maskRowIndex = GradientAtlasManager.GetAtlasRow(mFill);
                float maskAlphaMult = maskS.color.a;
                if (mFill.Type == FillType.Solid) maskAlphaMult *= mFill.SolidColor.a;
                
                maskFillParams = new Vector4((float)mFill.Type, mFill.GradientAngle, mFill.GradientScale, (float)maskRowIndex);
                maskFillOffset = new Vector4(mFill.GradientOffset.x, mFill.GradientOffset.y, maskAlphaMult, 0);
                
                Matrix4x4 worldToMaskSDF = maskCenterTranslate * maskWorldToLocal;
                
                s_VisitedShapes.Clear();
                s_VisitedShapes.Add(maskS);
                CollectMaskBooleanOps(maskS, BooleanOperation.Union, ref m_ActiveMaskBoolCount, worldToMaskSDF);
                s_VisitedShapes.Clear();
            }

            // --- 3. Использование ShaderState и пула материалов ---
            ShaderState state = ProceduralMaterialPool.TempState;
            state.Clear();
            state.BaseMatId = baseMaterial.GetInstanceID();
            state.MainTex = mainTexture;
            state.PatternTex = MainFill.Type == FillType.Pattern ? MainFill.PatternTexture : null;
            state.InternalPadding = m_InternalPadding;

            if (m_ShapeType == ShapeType.Path && m_FlattenedPath != null)
            {
                ApplyPathDataToState(state.PathData, out state.PathPointCount, m_FlattenedPath);
            }

            if (firstPathOperator != null)
            {
                ApplyPathDataToState(state.BoolPathData, out state.BoolPathPointCount, firstPathOperator.m_FlattenedPath);
            }

            state.BoolCount = m_ActiveBoolCount;
            if (m_ActiveBoolCount > 0)
            {
                System.Array.Copy(m_ShaderOps, state.BoolOpType, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderShapeParams, state.BoolShapeParams, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderTransform, state.BoolTransform, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderSize, state.BoolSize, m_ActiveBoolCount);
            }

            state.HasMask = hasMask;
            if (hasMask)
            {
                state.MaskMatrix = maskMatrix;
                state.MaskParams = maskParams;
                state.MaskSize = maskSize;
                state.MaskShape = maskShape;
                state.MaskTex = maskTex;
                state.MaskFillParams = maskFillParams;
                state.MaskFillOffset = maskFillOffset;
                
                state.MaskBoolCount = m_ActiveMaskBoolCount;
                if (m_ActiveMaskBoolCount > 0)
                {
                    System.Array.Copy(m_MaskShaderOps, state.MaskBoolOpType, m_ActiveMaskBoolCount);
                    System.Array.Copy(m_MaskShaderShapeParams, state.MaskBoolShapeParams, m_ActiveMaskBoolCount);
                    System.Array.Copy(m_MaskShaderTransform, state.MaskBoolTransform, m_ActiveMaskBoolCount);
                    System.Array.Copy(m_MaskShaderSize, state.MaskBoolSize, m_ActiveMaskBoolCount);
                }
            }

            Material matToUse = ProceduralMaterialPool.GetMaterial(state, baseMaterial);
            
            if (m_InstanceMaterial != null && m_InstanceMaterial != matToUse) 
            {
                ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
            }
            
            m_InstanceMaterial = matToUse;
            return m_InstanceMaterial;
        }

        /// <summary> Упаковывает и передает данные точек пути в массивы состояния. </summary>
        private void ApplyPathDataToState(Vector4[] targetArray, out int pointCount, List<Vector2> points)
        {
            pointCount = Mathf.Min(points.Count, 128); 
            for (int i = 0; i < pointCount; i += 2)
            {
                float x1 = points[i].x;
                float y1 = points[i].y;
                float x2 = (i + 1 < pointCount) ? points[i + 1].x : 0;
                float y2 = (i + 1 < pointCount) ? points[i + 1].y : 0;
                targetArray[i / 2] = new Vector4(x1, y1, x2, y2);
            }
        }

        /// <summary> Рекурсивно собирает все активные булевы операции для передачи в шейдер. </summary>
        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (s_VisitedShapes.Contains(input.SourceShape)) continue; 

                ProceduralShape other = input.SourceShape;
                s_VisitedShapes.Add(other);
                
                // Расчет эффективной операции с учетом инверсии родительской операции вычитания
                BooleanOperation effectiveOp = input.Operation;
                if (parentOp == BooleanOperation.Subtraction)
                {
                    if (input.Operation == BooleanOperation.Union) effectiveOp = BooleanOperation.Subtraction;
                    else if (input.Operation == BooleanOperation.Subtraction) effectiveOp = BooleanOperation.Union;
                }

                AddShapeToShader(other, effectiveOp, count, rootWorldToLocal, rootCenterOffset, input.Smoothness);
                count++;

                CollectBooleanOps(other, input.Operation, ref count, rootWorldToLocal, rootCenterOffset);
            }
        }

        /// <summary> Подготавливает параметры конкретной фигуры-оператора для шейдера. </summary>
        private void AddShapeToShader(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            Vector3 otherPivotOffset = shape.GetGeometricCenterOffset();
            Vector3 otherCenterWorld = otherRect.TransformPoint((Vector3)otherRect.rect.center + otherPivotOffset);
            Vector3 targetPosInRootLocal = rootWorldToLocal.MultiplyPoint3x4(otherCenterWorld);
            Vector3 finalPos = targetPosInRootLocal - rootCenterOffset;

            float relativeRotation = otherRect.eulerAngles.z - rectTransform.eulerAngles.z;

            float customParam = shape.m_CornerSmoothing;
            if (shape.m_ShapeType == ShapeType.Line) customParam = shape.m_LineWidth;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, customParam, smoothness); 
            m_ShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_ShaderTransform[index] = new Vector4(finalPos.x, finalPos.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector2 otherScale = shape.ShapeScale; 
            Vector3 lossyScaleRatio = new Vector3(
                rectTransform.lossyScale.x != 0 ? otherRect.lossyScale.x / rectTransform.lossyScale.x : 0, 
                rectTransform.lossyScale.y != 0 ? otherRect.lossyScale.y / rectTransform.lossyScale.y : 0, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * otherScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * otherScale.y;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
        
        /// <summary> Сбор булевых операций для маски. </summary>
        private void CollectMaskBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 worldToMaskSDF)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (s_VisitedShapes.Contains(input.SourceShape)) continue;

                ProceduralShape other = input.SourceShape;
                s_VisitedShapes.Add(other);
                
                BooleanOperation effectiveOp = input.Operation;
                if (parentOp == BooleanOperation.Subtraction)
                {
                    if (input.Operation == BooleanOperation.Union) effectiveOp = BooleanOperation.Subtraction;
                    else if (input.Operation == BooleanOperation.Subtraction) effectiveOp = BooleanOperation.Union;
                }

                AddMaskShapeToShader(other, effectiveOp, count, worldToMaskSDF, input.Smoothness);
                count++;

                CollectMaskBooleanOps(other, input.Operation, ref count, worldToMaskSDF);
            }
        }

        /// <summary> Подготовка параметров фигуры-оператора маски для шейдера. </summary>
        private void AddMaskShapeToShader(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 worldToMaskSDF, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            Vector2 size = otherRect.rect.size;
            Vector2 pivot = otherRect.pivot;
            Vector2 pivotOffset = shape.GetGeometricCenterOffset();
            Vector3 localGeomCenter = new Vector3((0.5f - pivot.x) * size.x, (0.5f - pivot.y) * size.y, 0f) + (Vector3)pivotOffset;
            Vector3 worldGeomCenter = otherRect.TransformPoint(localGeomCenter);
            Vector3 posInMaskSDF = worldToMaskSDF.MultiplyPoint3x4(worldGeomCenter);
            
            float maskWorldRot = m_CachedMask.Shape.transform.eulerAngles.z;
            float otherWorldRot = otherRect.eulerAngles.z;
            float relativeRotation = otherWorldRot - maskWorldRot;

            float customParam = shape.m_CornerSmoothing;
            if (shape.m_ShapeType == ShapeType.Line) customParam = shape.m_LineWidth;

            m_MaskShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, customParam, smoothness); 
            m_MaskShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_MaskShaderTransform[index] = new Vector4(posInMaskSDF.x, posInMaskSDF.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                m_CachedMask.Shape.transform.lossyScale.x != 0 ? otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x : 0, 
                m_CachedMask.Shape.transform.lossyScale.y != 0 ? otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y : 0, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale.y;

            m_MaskShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}