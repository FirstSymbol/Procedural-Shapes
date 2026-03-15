using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Компонент, позволяющий обычным графическим элементам UI (например, Image) 
    /// подвергаться маскированию с помощью ProceduralShapeMask.
    /// Модифицирует меш и материал объекта для поддержки SDF-маскирования.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("UI/Procedural Shapes/Soft Maskable Image")]
    public class ProceduralSoftMaskable : MonoBehaviour, IMaterialModifier, IMeshModifier
    {
        private Graphic m_Graphic;
        private ProceduralShapeMask m_CachedMask;
        private Material m_MaskMaterial;
        private Material m_CustomBaseMat;
        private static Shader s_SoftMaskShader;
        
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
            RefreshMaskSubscription();
            m_Graphic.SetMaterialDirty();
            m_Graphic.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (m_CachedMask != null && m_CachedMask.Shape != null)
                m_CachedMask.Shape.OnShapeChanged -= HandleMaskChanged;

            if (m_MaskMaterial != null)
            {
                ProceduralMaterialPool.ReleaseMaterial(m_MaskMaterial);
                m_MaskMaterial = null;
            }
            
            if (m_CustomBaseMat != null)
            {
                if (Application.isPlaying) Destroy(m_CustomBaseMat);
                else DestroyImmediate(m_CustomBaseMat);
                m_CustomBaseMat = null;
            }
            
            if (m_Graphic != null)
            {
                m_Graphic.SetMaterialDirty();
                m_Graphic.SetVerticesDirty();
            }
        }

        /// <summary> Находит ближайшую родительскую маску и подписывается на ее изменения. </summary>
        private void RefreshMaskSubscription()
        {
            if (m_CachedMask != null && m_CachedMask.Shape != null)
                m_CachedMask.Shape.OnShapeChanged -= HandleMaskChanged;

            m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            if (m_CachedMask != null && m_CachedMask.Shape != null)
                m_CachedMask.Shape.OnShapeChanged += HandleDependencyChanged;
        }

        private void HandleDependencyChanged()
        {
            if (m_Graphic != null) m_Graphic.SetMaterialDirty();
        }

        // Устаревший метод, оставлен для совместимости
        private void HandleMaskChanged() => HandleDependencyChanged();

        private uint m_LastMaskVersion = 0;
        private Vector3 m_LastPos;
        private Quaternion m_LastRot;
        private Vector3 m_LastScale;

        private void OnTransformParentChanged()
        {
            RefreshMaskSubscription();
            if (m_Graphic != null) m_Graphic.SetMaterialDirty();
        }

        private void Update()
        {
            if (m_CachedMask == null || !m_CachedMask.isActiveAndEnabled || m_CachedMask.Shape == null)
                return;

            bool dirty = false;
            
            // Проверка версии маски для перерисовки
            if (m_CachedMask.Shape.Version != m_LastMaskVersion)
            {
                m_LastMaskVersion = m_CachedMask.Shape.Version;
                dirty = true;
            }

            // Проверка изменения позиции/вращения для обновления матрицы
            Transform t = transform;
            if (t.position != m_LastPos || t.rotation != m_LastRot || t.lossyScale != m_LastScale)
            {
                m_LastPos = t.position;
                m_LastRot = t.rotation;
                m_LastScale = t.lossyScale;
                dirty = true;
            }

            if (dirty)
            {
                m_Graphic.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Копирует локальные координаты вершин в UV1 для корректного расчета SDF в шейдере маски.
        /// </summary>
        public void ModifyMesh(VertexHelper vh)
        {
            if (!isActiveAndEnabled) return;
            UIVertex vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv1 = new Vector4(vert.position.x, vert.position.y, 0, 0); 
                vh.SetUIVertex(vert, i);
            }
        }

        public void ModifyMesh(Mesh mesh) { }

        /// <summary>
        /// Подменяет стандартный материал графики на специальный шейдер мягкой маски.
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled) return baseMaterial;

            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
                m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            if (m_CachedMask == null || !m_CachedMask.isActiveAndEnabled || m_CachedMask.Shape == null)
                return baseMaterial;

            ProceduralShape maskShape = m_CachedMask.Shape;
            RectTransform maskRT = maskShape.rectTransform;
            RectTransform imageRT = m_Graphic.rectTransform;
            
            // Расчет матрицы: ImageLocal -> World -> MaskLocal -> MaskSDF
            Matrix4x4 imageLocalToWorld = imageRT.localToWorldMatrix;
            Matrix4x4 maskWorldToLocal = maskRT.worldToLocalMatrix;
            
            Vector2 maskSizeRaw = maskRT.rect.size;
            Vector2 maskPivot = maskRT.pivot;
            Vector2 maskPivotOffset = maskShape.GetGeometricCenterOffset();
            
            Vector3 maskRectCenterFromPivot = new Vector3((0.5f - maskPivot.x) * maskSizeRaw.x, (0.5f - maskPivot.y) * maskSizeRaw.y, 0f);
            Vector3 maskTotalCenterCorrection = maskRectCenterFromPivot + (Vector3)maskPivotOffset;
            
            Matrix4x4 maskCenterTranslate = Matrix4x4.Translate(-maskTotalCenterCorrection);
            Matrix4x4 localToMaskSDF = maskCenterTranslate * maskWorldToLocal * imageLocalToWorld;

            Vector2 maskScale = maskShape.ShapeScale;
            Vector2 maskSize = new Vector2(maskSizeRaw.x * maskScale.x, maskSizeRaw.y * maskScale.y); 

            Texture gradientTex = maskShape.mainTexture; 
            ShapeFill fill = maskShape.MainFill;
            int maskRowIndex = GradientAtlasManager.GetAtlasRow(fill);

            float maskAlphaMult = maskShape.color.a;
            if (fill.Type == FillType.Solid) maskAlphaMult *= fill.SolidColor.a;

            int activeCount = 0;
            Matrix4x4 worldToMaskSDF = maskCenterTranslate * maskWorldToLocal;
            
            ProceduralShape.s_VisitedShapes.Clear();
            ProceduralShape.s_VisitedShapes.Add(maskShape);
            CollectBooleanOps(maskShape, BooleanOperation.Union, ref activeCount, worldToMaskSDF);
            ProceduralShape.s_VisitedShapes.Clear();

            // --- Использование пула материалов через ShaderState ---
            ShaderState state = ProceduralMaterialPool.TempState;
            state.Clear();

            if (s_SoftMaskShader == null) s_SoftMaskShader = Shader.Find("UI/ProceduralShapes/SoftMaskedImage");
            if (m_CustomBaseMat == null)
            {
                m_CustomBaseMat = new Material(s_SoftMaskShader);
                m_CustomBaseMat.hideFlags = HideFlags.HideAndDontSave;
            }
            
            m_CustomBaseMat.CopyPropertiesFromMaterial(baseMaterial);
            m_CustomBaseMat.shaderKeywords = baseMaterial.shaderKeywords;
            
            state.BaseMatId = m_CustomBaseMat.ComputeCRC(); 
            state.HasMask = true;
            state.MaskMatrix = localToMaskSDF;
            state.MaskParams = new Vector4(1f, (float)maskShape.m_ShapeType, maskShape.m_CornerSmoothing, m_CachedMask.Softness + maskShape.m_EdgeSoftness);
            state.MaskSize = new Vector4(maskSize.x, maskSize.y, 0, 0);
            state.MaskShape = maskShape.GetPackedShapeParams();
            state.MaskTex = gradientTex;
            state.MaskFillParams = new Vector4((float)fill.Type, fill.GradientAngle, fill.GradientScale, (float)maskRowIndex);
            state.MaskFillOffset = new Vector4(fill.GradientOffset.x, fill.GradientOffset.y, maskAlphaMult, 0);

            state.MaskBoolCount = activeCount;
            if (activeCount > 0)
            {
                System.Array.Copy(m_ShaderOps, state.MaskBoolOpType, activeCount);
                System.Array.Copy(m_ShaderShapeParams, state.MaskBoolShapeParams, activeCount);
                System.Array.Copy(m_ShaderTransform, state.MaskBoolTransform, activeCount);
                System.Array.Copy(m_ShaderSize, state.MaskBoolSize, activeCount);
            }

            Material matToUse = ProceduralMaterialPool.GetMaterial(state, m_CustomBaseMat);
            
            if (m_MaskMaterial != null) 
            {
                ProceduralMaterialPool.ReleaseMaterial(m_MaskMaterial);
            }
            
            m_MaskMaterial = matToUse;

            return m_MaskMaterial;
        }

        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 worldToMaskSDF)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (ProceduralShape.s_VisitedShapes.Contains(input.SourceShape)) continue;

                ProceduralShape other = input.SourceShape;
                ProceduralShape.s_VisitedShapes.Add(other);
                
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
            Vector3 posInMaskSDF = worldToMaskSDF.MultiplyPoint3x4(worldGeomCenter);
            
            float maskWorldRot = m_CachedMask.Shape.transform.eulerAngles.z;
            float otherWorldRot = otherRect.eulerAngles.z;
            float relativeRotation = otherWorldRot - maskWorldRot;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            m_ShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_ShaderTransform[index] = new Vector4(posInMaskSDF.x, posInMaskSDF.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                m_CachedMask.Shape.transform.lossyScale.x != 0 ? otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x : 0, 
                m_CachedMask.Shape.transform.lossyScale.y != 0 ? otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y : 0, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale.x;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale.y;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}