using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Procedural Shapes/Soft Maskable Image")]
    public class ProceduralSoftMaskable : MonoBehaviour, IMaterialModifier
    {
        private Graphic m_Graphic;
        private ProceduralShapeMask m_CachedMask;
        private Material m_MaskMaterial;
        
        // Shader Props
        private static readonly int _MaskWorldToLocalX = Shader.PropertyToID("_MaskWorldToLocalX");
        private static readonly int _MaskWorldToLocalY = Shader.PropertyToID("_MaskWorldToLocalY");
        private static readonly int _MaskWorldToLocalZ = Shader.PropertyToID("_MaskWorldToLocalZ");
        private static readonly int _MaskWorldToLocalW = Shader.PropertyToID("_MaskWorldToLocalW");

        private static readonly int _MaskParams = Shader.PropertyToID("_MaskParams");
        private static readonly int _MaskTrans = Shader.PropertyToID("_MaskTrans");
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

        private const int MAX_OPS = 8;
        private Vector4[] m_ShaderOps = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderShapeParams = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderTransform = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderSize = new Vector4[MAX_OPS];

        private void OnEnable()
        {
            m_Graphic = GetComponent<Graphic>();
            m_Graphic.SetMaterialDirty();
        }

        private void OnDisable()
        {
            if (m_MaskMaterial != null) DestroyImmediate(m_MaskMaterial);
            m_Graphic.SetMaterialDirty();
        }

        private void Update()
        {
            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled)
            {
                m_Graphic.SetMaterialDirty();
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled) return baseMaterial;

            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
                m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            if (m_CachedMask == null || !m_CachedMask.isActiveAndEnabled || m_CachedMask.Shape == null)
                return baseMaterial;

            if (m_MaskMaterial == null)
            {
                m_MaskMaterial = new Material(Shader.Find("UI/ProceduralShapes/SoftMaskedImage"));
                m_MaskMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            m_MaskMaterial.shaderKeywords = baseMaterial.shaderKeywords;

            UpdateMaskData();

            return m_MaskMaterial;
        }

        private void UpdateMaskData()
        {
            ProceduralShape maskShape = m_CachedMask.Shape;
            RectTransform maskRT = maskShape.rectTransform;
            
            // 1. Matrix: World -> Mask Local (Pivot) -> Mask SDF (Center)
            Matrix4x4 worldToLocal = maskRT.worldToLocalMatrix;
            
            Vector2 size = maskRT.rect.size;
            Vector2 pivot = maskRT.pivot;
            Vector2 pivotOffset = maskShape.GetGeometricCenterOffset();
            
            Vector3 rectCenterFromPivot = new Vector3((0.5f - pivot.x) * size.x, (0.5f - pivot.y) * size.y, 0f);
            Vector3 totalCenterCorrection = rectCenterFromPivot + (Vector3)pivotOffset;
            
            Matrix4x4 centerCorrection = Matrix4x4.Translate(-totalCenterCorrection); 
            Matrix4x4 finalMatrix = centerCorrection * worldToLocal;

            m_MaskMaterial.SetVector(_MaskWorldToLocalX, finalMatrix.GetRow(0));
            m_MaskMaterial.SetVector(_MaskWorldToLocalY, finalMatrix.GetRow(1));
            m_MaskMaterial.SetVector(_MaskWorldToLocalZ, finalMatrix.GetRow(2));
            m_MaskMaterial.SetVector(_MaskWorldToLocalW, finalMatrix.GetRow(3));

            // 2. Base Mask Data
            Vector2 maskSize = size * maskShape.ShapeScale; 
            
            Vector4 maskShapeParams = Vector4.zero;
            if (maskShape.m_ShapeType == ShapeType.Rectangle) maskShapeParams = maskShape.m_CornerRadius;
            else if (maskShape.m_ShapeType == ShapeType.Polygon) maskShapeParams = new Vector4(maskShape.m_PolygonSides, maskShape.m_PolygonRounding, 0, 0);
            else if (maskShape.m_ShapeType == ShapeType.Star) maskShapeParams = new Vector4(maskShape.m_StarPoints, maskShape.m_StarRatio, maskShape.m_StarRoundingOuter, maskShape.m_StarRoundingInner);

            m_MaskMaterial.SetVector(_MaskParams, new Vector4(1f, (float)maskShape.m_ShapeType, maskShape.m_CornerSmoothing, m_CachedMask.Softness));
            m_MaskMaterial.SetVector(_MaskSize, new Vector4(maskSize.x, maskSize.y, 0, 0));
            m_MaskMaterial.SetVector(_MaskShape, maskShapeParams);
            
            // FIX: Removed usage of ShapeRotation. Passing 0.
            m_MaskMaterial.SetVector(_MaskTrans, new Vector4(0, 0, 0, 0));
            
            // 3. Fill Data
            Texture gradientTex = maskShape.mainTexture; 
            m_MaskMaterial.SetTexture(_MaskTex, gradientTex != null ? gradientTex : Texture2D.whiteTexture);
            
            ShapeFill fill = maskShape.MainFill;
            float texHeight = gradientTex != null ? gradientTex.height : 3;
            float vCoord = 1.5f / texHeight;
            
            m_MaskMaterial.SetVector(_MaskFillParams, new Vector4((float)fill.Type, fill.GradientAngle, fill.GradientScale, vCoord));
            m_MaskMaterial.SetVector(_MaskFillOffset, new Vector4(fill.GradientOffset.x, fill.GradientOffset.y, 0, 0));

            // 4. Boolean Operations
            int activeCount = 0;
            CollectBooleanOps(maskShape, BooleanOperation.Union, ref activeCount, finalMatrix);
            
            m_MaskMaterial.SetInt(_MaskBoolParams, activeCount);
            if (activeCount > 0)
            {
                m_MaskMaterial.SetVectorArray(_MaskBoolOpType, m_ShaderOps);
                m_MaskMaterial.SetVectorArray(_MaskBoolShapeParams, m_ShaderShapeParams);
                m_MaskMaterial.SetVectorArray(_MaskBoolTransform, m_ShaderTransform);
                m_MaskMaterial.SetVectorArray(_MaskBoolSize, m_ShaderSize);
            }
        }

        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 maskSDFMatrix)
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

                AddShapeToArrays(other, effectiveOp, count, maskSDFMatrix, input.Smoothness);
                count++;

                CollectBooleanOps(other, input.Operation, ref count, maskSDFMatrix);
            }
        }

        private void AddShapeToArrays(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 maskSDFMatrix, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            
            Vector2 size = otherRect.rect.size;
            Vector2 pivot = otherRect.pivot;
            Vector2 pivotOffset = shape.GetGeometricCenterOffset();
            
            Vector3 localGeomCenter = new Vector3((0.5f - pivot.x) * size.x, (0.5f - pivot.y) * size.y, 0f) + (Vector3)pivotOffset;
            Vector3 worldGeomCenter = otherRect.TransformPoint(localGeomCenter);
            
            Vector3 posInMaskSDF = maskSDFMatrix.MultiplyPoint3x4(worldGeomCenter);
            
            // Unified Rotation: Relative to Mask Transform only
            float relativeRotation = otherRect.eulerAngles.z - m_CachedMask.Shape.transform.eulerAngles.z;
            // FIX: Removed shape.ShapeRotation
            float totalRot = relativeRotation * Mathf.Deg2Rad;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            
            if (shape.m_ShapeType == ShapeType.Rectangle) m_ShaderShapeParams[index] = shape.m_CornerRadius;
            else if (shape.m_ShapeType == ShapeType.Polygon) m_ShaderShapeParams[index] = new Vector4(shape.m_PolygonSides, shape.m_PolygonRounding, 0, 0);
            else if (shape.m_ShapeType == ShapeType.Star) m_ShaderShapeParams[index] = new Vector4(shape.m_StarPoints, shape.m_StarRatio, shape.m_StarRoundingOuter, shape.m_StarRoundingInner);

            m_ShaderTransform[index] = new Vector4(posInMaskSDF.x, posInMaskSDF.y, totalRot, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x, 
                otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}