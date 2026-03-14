using UnityEngine;
using System.Collections.Generic;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralShape
    {
        private static readonly int _BoolParams1 = Shader.PropertyToID("_BoolParams1");
        private static readonly int _BoolData_OpType = Shader.PropertyToID("_BoolData_OpType");
        private static readonly int _BoolData_ShapeParams = Shader.PropertyToID("_BoolData_ShapeParams");
        private static readonly int _BoolData_Transform = Shader.PropertyToID("_BoolData_Transform");
        private static readonly int _BoolData_Size = Shader.PropertyToID("_BoolData_Size");
        private static readonly int _InternalPadding = Shader.PropertyToID("_InternalPadding");
        
        private static readonly int _MaskParams = Shader.PropertyToID("_MaskParams");
        private static readonly int _MaskMatrixX = Shader.PropertyToID("_MaskMatrixX");
        private static readonly int _MaskMatrixY = Shader.PropertyToID("_MaskMatrixY");
        private static readonly int _MaskMatrixZ = Shader.PropertyToID("_MaskMatrixZ");
        private static readonly int _MaskMatrixW = Shader.PropertyToID("_MaskMatrixW");
        
        private static readonly int _MaskSize = Shader.PropertyToID("_MaskSize");
        private static readonly int _MaskShape = Shader.PropertyToID("_MaskShape");
        private static readonly int _MaskTex = Shader.PropertyToID("_MaskTex");
        private static readonly int _MaskFillParams = Shader.PropertyToID("_MaskFillParams");
        private static readonly int _MaskFillOffset = Shader.PropertyToID("_MaskFillOffset");
        
        private static readonly int _MaskBoolParams = Shader.PropertyToID("_MaskBoolParams");
        private static readonly int _MaskBoolOpType = Shader.PropertyToID("_MaskBoolOpType");
        private static readonly int _MaskBoolShapeParams = Shader.PropertyToID("_MaskBoolShapeParams");
        private static readonly int _MaskBoolTransform = Shader.PropertyToID("_MaskBoolTransform");
        private static readonly int _MaskBoolSize = Shader.PropertyToID("_MaskBoolSize");

        private static readonly int _PathData = Shader.PropertyToID("_PathData");
        private static readonly int _PathPointCount = Shader.PropertyToID("_PathPointCount");

        private int m_ActiveBoolCount;
        private int m_ActiveMaskBoolCount;
        [System.NonSerialized] private int m_LastBaseMatId = 0;

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (BooleanOperations.Count == 0 && m_CachedMask == null && MainFill.Type != FillType.Pattern && m_ShapeType != ShapeType.Path)
            {
                if (m_InstanceMaterial != null)
                {
                    ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
                    m_InstanceMaterial = null;
                }
                return base.GetModifiedMaterial(baseMaterial);
            }

            // 1. Gather Data (Needed for both Hash and Applying)
            if (m_ShapeType == ShapeType.Path && (m_FlattenedPath == null || m_FlattenedPath.Count == 0))
            {
                if (m_FlattenedPath == null) m_FlattenedPath = new List<Vector2>();
                PathUtils.FlattenPath(m_ShapePath, m_FlattenedPath);
            }

            Matrix4x4 worldToLocal = rectTransform.worldToLocalMatrix;
            Vector2 selfCenterOffset = GetGeometricCenterOffset();
            m_ActiveBoolCount = 0;
            CollectBooleanOps(this, BooleanOperation.Union, ref m_ActiveBoolCount, worldToLocal, selfCenterOffset);

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
                CollectMaskBooleanOps(maskS, BooleanOperation.Union, ref m_ActiveMaskBoolCount, worldToMaskSDF);
            }

            // 2. Compute "Style Key" (Excluding transforms for pooling/reuse)
            int styleH1 = 17;
            HashUtils.Add(ref styleH1, m_ActiveBoolCount);
            for (int i = 0; i < m_ActiveBoolCount; i++) {
                HashUtils.Add(ref styleH1, m_ShaderOps[i].x); // Op
                HashUtils.Add(ref styleH1, m_ShaderOps[i].y); // Type
                HashUtils.Add(ref styleH1, m_ShaderOps[i].z); // Smoothing
                HashUtils.Add(ref styleH1, m_ShaderOps[i].w); // Smoothness
                HashUtils.Add(ref styleH1, m_ShaderShapeParams[i]);
            }

            if (m_ShapeType == ShapeType.Path && m_FlattenedPath != null)
            {
                HashUtils.Add(ref styleH1, m_FlattenedPath.Count);
                HashUtils.Add(ref styleH1, m_ShapePath.Closed ? 1f : 0f);
                HashUtils.Add(ref styleH1, m_ShapePath.Thickness);
                foreach (var pt in m_FlattenedPath) { HashUtils.Add(ref styleH1, pt.x); HashUtils.Add(ref styleH1, pt.y); }
            }

            int styleH2 = 31;
            if (hasMask) {
                HashUtils.Add(ref styleH2, 1f);
                HashUtils.Add(ref styleH2, maskParams.y);
                HashUtils.Add(ref styleH2, maskParams.z);
                HashUtils.Add(ref styleH2, maskShape);
                HashUtils.Add(ref styleH2, maskFillParams.x);
                HashUtils.Add(ref styleH2, m_ActiveMaskBoolCount);
                for (int i = 0; i < m_ActiveMaskBoolCount; i++) {
                    HashUtils.Add(ref styleH2, m_MaskShaderOps[i].x);
                    HashUtils.Add(ref styleH2, m_MaskShaderOps[i].y);
                    HashUtils.Add(ref styleH2, m_MaskShaderShapeParams[i]);
                }
            }

            MaterialHashKey styleKey = new MaterialHashKey();
            styleKey.BaseMatId = baseMaterial.GetInstanceID();
            styleKey.PatternId = (MainFill.Type == FillType.Pattern && MainFill.PatternTexture != null) ? MainFill.PatternTexture.GetInstanceID() : 0;
            styleKey.Padding = m_InternalPadding;
            styleKey.Hash1 = styleH1;
            styleKey.Hash2 = styleH2;

            // 3. Get/Reuse Material
            Material matToUse = ProceduralMaterialPool.GetMaterial(styleKey, baseMaterial);
            
            if (m_InstanceMaterial != null && m_InstanceMaterial != matToUse)
            {
                ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
            }
            
            m_InstanceMaterial = matToUse;
            m_LastBaseMatId = styleKey.BaseMatId;

            // 4. Apply Properties (ALWAYS update uniforms)
            m_InstanceMaterial.CopyPropertiesFromMaterial(baseMaterial); 
            m_InstanceMaterial.SetTexture("_MainTex", mainTexture);
            
            if (MainFill.Type == FillType.Pattern && MainFill.PatternTexture != null)
                m_InstanceMaterial.SetTexture("_PatternTex", MainFill.PatternTexture);

            if (m_ShapeType == ShapeType.Path && m_FlattenedPath != null)
            {
                int count = Mathf.Min(m_FlattenedPath.Count, 128);
                Vector4[] pathData = new Vector4[64];
                for (int i = 0; i < count; i += 2)
                {
                    float x1 = m_FlattenedPath[i].x;
                    float y1 = m_FlattenedPath[i].y;
                    float x2 = (i + 1 < count) ? m_FlattenedPath[i + 1].x : 0;
                    float y2 = (i + 1 < count) ? m_FlattenedPath[i + 1].y : 0;
                    pathData[i / 2] = new Vector4(x1, y1, x2, y2);
                }
                m_InstanceMaterial.SetVectorArray(_PathData, pathData);
                m_InstanceMaterial.SetInt(_PathPointCount, count);
            }

            m_InstanceMaterial.SetFloat(_InternalPadding, m_InternalPadding);
            m_InstanceMaterial.SetInt(_BoolParams1, m_ActiveBoolCount);
            if (m_ActiveBoolCount > 0)
            {
                m_InstanceMaterial.SetVectorArray(_BoolData_OpType, m_ShaderOps);
                m_InstanceMaterial.SetVectorArray(_BoolData_ShapeParams, m_ShaderShapeParams);
                m_InstanceMaterial.SetVectorArray(_BoolData_Transform, m_ShaderTransform);
                m_InstanceMaterial.SetVectorArray(_BoolData_Size, m_ShaderSize);
            }

            if (hasMask)
            {
                m_InstanceMaterial.SetVector(_MaskMatrixX, maskMatrix.GetRow(0));
                m_InstanceMaterial.SetVector(_MaskMatrixY, maskMatrix.GetRow(1));
                m_InstanceMaterial.SetVector(_MaskMatrixZ, maskMatrix.GetRow(2));
                m_InstanceMaterial.SetVector(_MaskMatrixW, maskMatrix.GetRow(3));
                m_InstanceMaterial.SetVector(_MaskParams, maskParams);
                m_InstanceMaterial.SetVector(_MaskSize, maskSize);
                m_InstanceMaterial.SetVector(_MaskShape, maskShape);
                m_InstanceMaterial.SetTexture(_MaskTex, maskTex != null ? maskTex : Texture2D.whiteTexture);
                m_InstanceMaterial.SetVector(_MaskFillParams, maskFillParams);
                m_InstanceMaterial.SetVector(_MaskFillOffset, maskFillOffset);
                m_InstanceMaterial.SetInt(_MaskBoolParams, m_ActiveMaskBoolCount);
                if (m_ActiveMaskBoolCount > 0)
                {
                    m_InstanceMaterial.SetVectorArray(_MaskBoolOpType, m_MaskShaderOps);
                    m_InstanceMaterial.SetVectorArray(_MaskBoolShapeParams, m_MaskShaderShapeParams);
                    m_InstanceMaterial.SetVectorArray(_MaskBoolTransform, m_MaskShaderTransform);
                    m_InstanceMaterial.SetVectorArray(_MaskBoolSize, m_MaskShaderSize);
                }
            }
            else
            {
                m_InstanceMaterial.SetVector(_MaskParams, Vector4.zero); 
                m_InstanceMaterial.SetInt(_MaskBoolParams, 0);
            }
            
            return m_InstanceMaterial;
        }

        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (input.SourceShape == this) continue; 

                ProceduralShape other = input.SourceShape;
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

        private void AddShapeToShader(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            Vector3 otherPivotOffset = shape.GetGeometricCenterOffset();
            Vector3 otherCenterWorld = otherRect.TransformPoint((Vector3)otherRect.rect.center + otherPivotOffset);
            Vector3 targetPosInRootLocal = rootWorldToLocal.MultiplyPoint3x4(otherCenterWorld);
            Vector3 finalPos = targetPosInRootLocal - rootCenterOffset;

            float relativeRotation = otherRect.eulerAngles.z - rectTransform.eulerAngles.z;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            m_ShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_ShaderTransform[index] = new Vector4(finalPos.x, finalPos.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector2 otherScale = shape.ShapeScale; 
            Vector3 lossyScaleRatio = new Vector3(otherRect.lossyScale.x / rectTransform.lossyScale.x, otherRect.lossyScale.y / rectTransform.lossyScale.y, 1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * otherScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * otherScale.y;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
        
        private void CollectMaskBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 worldToMaskSDF)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (input.SourceShape == m_CachedMask.Shape) continue;

                ProceduralShape other = input.SourceShape;
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

            m_MaskShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            m_MaskShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_MaskShaderTransform[index] = new Vector4(posInMaskSDF.x, posInMaskSDF.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x, 
                otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale.y;

            m_MaskShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}
