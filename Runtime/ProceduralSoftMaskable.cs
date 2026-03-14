using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Procedural Shapes/Soft Maskable Image")]
    public class ProceduralSoftMaskable : MonoBehaviour, IMaterialModifier, IMeshModifier
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
        private static readonly int _InternalPadding = Shader.PropertyToID("_InternalPadding");
        
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
            m_Graphic.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (m_MaskMaterial != null) DestroyImmediate(m_MaskMaterial);
            if (m_Graphic != null)
            {
                m_Graphic.SetMaterialDirty();
                m_Graphic.SetVerticesDirty();
            }
        }

        private void Update()
        {
            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled)
            {
                m_Graphic.SetMaterialDirty();
            }
        }

        public void ModifyMesh(VertexHelper vh)
        {
            if (!isActiveAndEnabled) return;
            UIVertex vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv1 = new Vector4(vert.position.x, vert.position.y, 0, 0); // Preserve pure local position
                vh.SetUIVertex(vert, i);
            }
        }

        public void ModifyMesh(Mesh mesh) { }

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
            m_MaskMaterial.CopyPropertiesFromMaterial(baseMaterial);
            m_MaskMaterial.shaderKeywords = baseMaterial.shaderKeywords;

            UpdateMaskData();

            return m_MaskMaterial;
        }

        private void UpdateMaskData()
        {
            ProceduralShape maskShape = m_CachedMask.Shape;
            RectTransform maskRT = maskShape.rectTransform;
            RectTransform imageRT = m_Graphic.rectTransform;
            
            // Calculate matrix: ImageLocal -> World -> MaskLocal -> MaskSDF
            Matrix4x4 imageLocalToWorld = imageRT.localToWorldMatrix;
            Matrix4x4 maskWorldToLocal = maskRT.worldToLocalMatrix;
            
            // Mask Local -> Mask SDF involves shifting by pivot and applying negative rotation
            Vector2 maskSizeRaw = maskRT.rect.size;
            Vector2 maskPivot = maskRT.pivot;
            Vector2 maskPivotOffset = maskShape.GetGeometricCenterOffset();
            
            Vector3 maskRectCenterFromPivot = new Vector3((0.5f - maskPivot.x) * maskSizeRaw.x, (0.5f - maskPivot.y) * maskSizeRaw.y, 0f);
            Vector3 maskTotalCenterCorrection = maskRectCenterFromPivot + (Vector3)maskPivotOffset;
            
            Matrix4x4 maskCenterTranslate = Matrix4x4.Translate(-maskTotalCenterCorrection);
            Matrix4x4 maskRotateToSDF = Matrix4x4.Rotate(Quaternion.Euler(0, 0, -maskShape.ShapeRotation));
            
            // Final Unified Matrix
            Matrix4x4 localToMaskSDF = maskRotateToSDF * maskCenterTranslate * maskWorldToLocal * imageLocalToWorld;

            m_MaskMaterial.SetVector(_MaskWorldToLocalX, localToMaskSDF.GetRow(0));
            m_MaskMaterial.SetVector(_MaskWorldToLocalY, localToMaskSDF.GetRow(1));
            m_MaskMaterial.SetVector(_MaskWorldToLocalZ, localToMaskSDF.GetRow(2));
            m_MaskMaterial.SetVector(_MaskWorldToLocalW, localToMaskSDF.GetRow(3));

            // 2. Base Mask Data
            Vector2 maskScale = maskShape.ShapeScale;
            Vector2 maskSize = new Vector2(maskSizeRaw.x * maskScale.x, maskSizeRaw.y * maskScale.y); 
            
            m_MaskMaterial.SetVector(_MaskParams, new Vector4(1f, (float)maskShape.m_ShapeType, maskShape.m_CornerSmoothing, m_CachedMask.Softness));
            m_MaskMaterial.SetVector(_MaskSize, new Vector4(maskSize.x, maskSize.y, 0, 0));
            m_MaskMaterial.SetVector(_MaskShape, maskShape.GetPackedShapeParams());
            
            // 3. Fill Data
            Texture gradientTex = maskShape.mainTexture; 
            m_MaskMaterial.SetTexture("_MaskTex", gradientTex != null ? gradientTex : Texture2D.whiteTexture);

            ShapeFill fill = maskShape.MainFill;
            int maskRowIndex = GradientAtlasManager.GetAtlasRow(fill);

            float maskAlphaMult = maskShape.color.a;
            if (fill.Type == FillType.Solid) maskAlphaMult *= fill.SolidColor.a;

            m_MaskMaterial.SetVector(_MaskFillParams, new Vector4((float)fill.Type, fill.GradientAngle, fill.GradientScale, (float)maskRowIndex));
            m_MaskMaterial.SetVector(_MaskFillOffset, new Vector4(fill.GradientOffset.x, fill.GradientOffset.y, maskAlphaMult, 0));

            // 4. Boolean Operations
            int activeCount = 0;
            // For booleans, we just need World -> MaskSDF
            Matrix4x4 worldToMaskSDF = maskRotateToSDF * maskCenterTranslate * maskWorldToLocal;
            CollectBooleanOps(maskShape, BooleanOperation.Union, ref activeCount, worldToMaskSDF);
            
            m_MaskMaterial.SetInt(_MaskBoolParams, activeCount);
            if (activeCount > 0)
            {
                m_MaskMaterial.SetVectorArray(_MaskBoolOpType, m_ShaderOps);
                m_MaskMaterial.SetVectorArray(_MaskBoolShapeParams, m_ShaderShapeParams);
                m_MaskMaterial.SetVectorArray(_MaskBoolTransform, m_ShaderTransform);
                m_MaskMaterial.SetVectorArray(_MaskBoolSize, m_ShaderSize);
            }
        }

        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 worldToMaskSDF)
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

                AddShapeToArrays(other, effectiveOp, count, worldToMaskSDF, input.Smoothness);
                count++;

                CollectBooleanOps(other, input.Operation, ref count, worldToMaskSDF);
            }
        }

        private void AddShapeToArrays(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 worldToMaskSDF, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            
            Vector2 size = otherRect.rect.size;
            Vector2 pivot = otherRect.pivot;
            Vector2 pivotOffset = shape.GetGeometricCenterOffset();
            
            Vector3 localGeomCenter = new Vector3((0.5f - pivot.x) * size.x, (0.5f - pivot.y) * size.y, 0f) + (Vector3)pivotOffset;
            Vector3 worldGeomCenter = otherRect.TransformPoint(localGeomCenter);
            
            // Position in Mask SDF Space
            Vector3 posInMaskSDF = worldToMaskSDF.MultiplyPoint3x4(worldGeomCenter);
            
            // Relative rotation: Other World Rotation - Mask World Rotation - Mask Shape Rotation + Other Shape Rotation
            float maskWorldRot = m_CachedMask.Shape.transform.eulerAngles.z;
            float otherWorldRot = otherRect.eulerAngles.z;
            float relativeRotation = otherWorldRot - maskWorldRot - m_CachedMask.Shape.ShapeRotation + shape.ShapeRotation;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            m_ShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_ShaderTransform[index] = new Vector4(posInMaskSDF.x, posInMaskSDF.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x, 
                otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale.y;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}