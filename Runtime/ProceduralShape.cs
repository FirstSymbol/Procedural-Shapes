using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Procedural Shapes/Shape")]
    public class ProceduralShape : MaskableGraphic
    {
        [Tooltip("Если включено, фигура не будет отрисовываться, но может использоваться как Cutter для других фигур.")]
        [SerializeField] private bool m_DisableRendering = false;

        [Header("Shape Definition")]
        public ShapeType m_ShapeType = ShapeType.Rectangle;
        
        [Tooltip("Uniform scale factor for the shape inside the RectTransform bounds.")]
        [SerializeField] private float m_ShapeScale = 1.0f;

        public Vector4 m_CornerRadius = Vector4.zero;
        [Range(0f, 1f)] public float m_CornerSmoothing = 0f;
        
        [Range(3, 128)] public int m_PolygonSides = 5;
        [Range(0f, 1f)] public float m_PolygonRounding = 0f;
        public float m_PolygonRotation = 0f;

        [Range(3, 128)] public int m_StarPoints = 5;
        [Range(0.01f, 1f)] public float m_StarRatio = 0.5f;
        [Range(0f, 1f)] public float m_StarRoundingOuter = 0f;
        [Range(0f, 1f)] public float m_StarRoundingInner = 0f;
        public float m_StarRotation = 0f;

        [Range(0f, 10f)] 
        [Tooltip("Сглаживание краев (антиалиасинг). Значение около 1.0 обычно оптимально.")]
        public float m_EdgeSoftness = 1.0f;

        [Header("Boolean Operations")]
        public List<BooleanInput> BooleanOperations = new List<BooleanInput>();

        [Header("Appearance")]
        public ShapeFill MainFill = new ShapeFill();

        [SerializeReference] 
        public List<ProceduralEffect> Effects = new List<ProceduralEffect>();

        private Texture2D m_GradientTexture;
        private bool m_TextureDirty = true;
        private Material m_InstanceMaterial; 
        private ProceduralShapeMask m_CachedMask;
        
        // Caching Components
        private RectTransform m_RectTransform;
        public new RectTransform rectTransform => m_RectTransform ? m_RectTransform : (m_RectTransform = GetComponent<RectTransform>());

        // --- Change Tracking State ---
        private Matrix4x4 m_LastLocalToWorld = Matrix4x4.identity;
        private Rect m_LastRect = Rect.zero;
        // We use a simple hash approach or a list of tracked states to detect changes in dependencies
        private struct TransformState { public Vector3 pos; public Quaternion rot; public Vector3 scale; public Rect rect; }
        private Dictionary<int, TransformState> m_TrackedStates = new Dictionary<int, TransformState>();
        private bool m_NeedUpdate = true;

        // --- Cache Shader Properties ---
        private static readonly int _BoolParams1 = Shader.PropertyToID("_BoolParams1");
        private static readonly int _BoolData_OpType = Shader.PropertyToID("_BoolData_OpType");
        private static readonly int _BoolData_ShapeParams = Shader.PropertyToID("_BoolData_ShapeParams");
        private static readonly int _BoolData_Transform = Shader.PropertyToID("_BoolData_Transform");
        private static readonly int _BoolData_Size = Shader.PropertyToID("_BoolData_Size");
        private static readonly int _AntiAliasing = Shader.PropertyToID("_AntiAliasing");
        
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
        
        private Vector4[] m_MaskShaderOps = new Vector4[MAX_OPS];
        private Vector4[] m_MaskShaderShapeParams = new Vector4[MAX_OPS];
        private Vector4[] m_MaskShaderTransform = new Vector4[MAX_OPS];
        private Vector4[] m_MaskShaderSize = new Vector4[MAX_OPS];

        private static readonly Vector3[] s_Corners = new Vector3[4];

        public override Texture mainTexture => m_GradientTexture != null ? m_GradientTexture : s_WhiteTexture;

        public bool DisableRendering
        {
            get => m_DisableRendering;
            set { if (m_DisableRendering != value) { m_DisableRendering = value; SetAllDirty(); } }
        }

        public float ShapeScale
        {
            get => m_ShapeScale;
            set { if (Mathf.Abs(m_ShapeScale - value) > 0.0001f) { m_ShapeScale = value; SetAllDirty(); } }
        }

        private static Material m_DefaultMaterial;
        public override Material defaultMaterial
        {
            get
            {
                if (m_DefaultMaterial == null) m_DefaultMaterial = new Material(Shader.Find("UI/ProceduralShapes/Shape"));
                return m_DefaultMaterial ?? base.defaultMaterial;
            }
        }

        public bool IsActive => gameObject.activeInHierarchy && enabled;
        public void SetAllDirty() { m_TextureDirty = true; m_NeedUpdate = true; SetVerticesDirty(); SetMaterialDirty(); }

        protected override void OnEnable() 
        { 
            base.OnEnable(); 
            m_RectTransform = GetComponent<RectTransform>();
            SetAllDirty(); 
        }
        
        protected override void OnTransformParentChanged() 
        { 
            base.OnTransformParentChanged(); 
            m_CachedMask = null; // Invalidate mask cache
            SetAllDirty(); 
        }
        
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            // UI layout changed
            m_NeedUpdate = true;
        }

        private void LateUpdate()
        {
            // Use LateUpdate to catch changes made in Update
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            bool dirty = false;

            // 1. Check Self
            if (transform.hasChanged)
            {
                // This resets the hasChanged flag, which might affect other scripts? 
                // Unity's Transform.hasChanged is generally safe to use if we are the primary logic.
                // But for UI, relying on Matrix check is safer if we want to avoid side effects.
                Matrix4x4 currentL2W = transform.localToWorldMatrix;
                if (currentL2W != m_LastLocalToWorld)
                {
                    m_LastLocalToWorld = currentL2W;
                    dirty = true;
                }
                
                // Also check rect size changes (sometimes handled by OnRectTransformDimensionsChange but safe to double check)
                if (rectTransform.rect != m_LastRect)
                {
                    m_LastRect = rectTransform.rect;
                    dirty = true;
                }
                
                transform.hasChanged = false; // Reset to avoid false positives next frame
            }

            // 2. Check Mask (if exists)
            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
            {
                // Try find mask
                var mask = GetComponentInParent<ProceduralShapeMask>();
                if (mask != m_CachedMask)
                {
                    m_CachedMask = mask;
                    dirty = true;
                }
            }

            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled)
            {
                if (CheckTransformDirty(m_CachedMask.transform, m_CachedMask.Shape.rectTransform)) dirty = true;
                
                // Also check Mask's Booleans!
                if (m_CachedMask.Shape != null)
                {
                    foreach (var op in m_CachedMask.Shape.BooleanOperations)
                    {
                        if (op.SourceShape != null && CheckTransformDirty(op.SourceShape.transform, op.SourceShape.rectTransform))
                            dirty = true;
                    }
                }
            }

            // 3. Check Boolean Inputs
            if (BooleanOperations.Count > 0)
            {
                foreach (var input in BooleanOperations)
                {
                    if (input.SourceShape != null && input.SourceShape.isActiveAndEnabled)
                    {
                        if (CheckTransformDirty(input.SourceShape.transform, input.SourceShape.rectTransform)) 
                            dirty = true;
                    }
                }
            }

            if (dirty || m_NeedUpdate)
            {
                m_NeedUpdate = false;
                SetMaterialDirty();
                
                // Only rebuild vertices if bounds might have changed (optimization: could be refined)
                // But for now, updating geometry on move is required for correct culling and masking.
                SetVerticesDirty(); 
            }
        }

        private bool CheckTransformDirty(Transform t, RectTransform rt)
        {
            int id = t.GetInstanceID();
            TransformState currentState = new TransformState
            {
                pos = t.position,
                rot = t.rotation,
                scale = t.lossyScale,
                rect = rt != null ? rt.rect : Rect.zero
            };

            if (!m_TrackedStates.TryGetValue(id, out TransformState lastState))
            {
                m_TrackedStates[id] = currentState;
                return true;
            }

            if (lastState.pos != currentState.pos || 
                lastState.rot != currentState.rot || 
                lastState.scale != currentState.scale ||
                lastState.rect != currentState.rect)
            {
                m_TrackedStates[id] = currentState;
                return true;
            }

            return false;
        }

        protected override void OnDestroy() 
        { 
            base.OnDestroy(); 
            if (m_GradientTexture != null) DestroyImmediate(m_GradientTexture); 
            if (m_InstanceMaterial != null) DestroyImmediate(m_InstanceMaterial);
        }

        private void RebuildGradientTexture()
        {
            // Only rebuild if texture is actually dirty
            if (!m_TextureDirty && m_GradientTexture != null) return;

            int layers = 1 + Effects.Count;
            int rowCount = layers * 3; 
            
            if (m_GradientTexture == null || m_GradientTexture.height != rowCount)
            {
                if (m_GradientTexture != null) DestroyImmediate(m_GradientTexture);
                m_GradientTexture = new Texture2D(256, rowCount, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear, 
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Color[] pixels = new Color[256 * rowCount];
            BakeFillRow(pixels, 0, MainFill);
            for (int i = 0; i < Effects.Count; i++) BakeFillRow(pixels, i + 1, Effects[i].Fill);

            m_GradientTexture.SetPixels(pixels);
            m_GradientTexture.Apply();
            m_TextureDirty = false;
        }

        private void BakeFillRow(Color[] pixels, int layerIndex, ShapeFill fill)
        {
            int offsetBase = layerIndex * 3 * 256;
            for (int x = 0; x < 256; x++)
            {
                Color c = fill.Type == FillType.Solid ? fill.SolidColor : fill.Gradient.Evaluate(x / 255f);
                pixels[offsetBase + x] = c;             
                pixels[offsetBase + 256 + x] = c;       
                pixels[offsetBase + 512 + x] = c;       
            }
        }

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            Material mat = base.GetModifiedMaterial(baseMaterial);

            if (m_InstanceMaterial == null || m_InstanceMaterial.shader != mat.shader)
            {
                m_InstanceMaterial = new Material(mat);
                m_InstanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            
            m_InstanceMaterial.CopyPropertiesFromMaterial(mat);
            m_InstanceMaterial.shaderKeywords = mat.shaderKeywords;
            
            m_InstanceMaterial.SetFloat(_AntiAliasing, m_EdgeSoftness);
            
            UpdateBooleanProperties();
            UpdateMask(); 
            
            return m_InstanceMaterial;
        }

        private void UpdateBooleanProperties()
        {
            int activeCount = 0;
            // Get Matrix only once
            Matrix4x4 worldToLocal = rectTransform.worldToLocalMatrix;

            // Pass references to avoid struct copying
            CollectBooleanOps(this, BooleanOperation.Union, ref activeCount, worldToLocal);

            m_InstanceMaterial.SetInt(_BoolParams1, activeCount); 
            if (activeCount > 0)
            {
                m_InstanceMaterial.SetVectorArray(_BoolData_OpType, m_ShaderOps);
                m_InstanceMaterial.SetVectorArray(_BoolData_ShapeParams, m_ShaderShapeParams);
                m_InstanceMaterial.SetVectorArray(_BoolData_Transform, m_ShaderTransform);
                m_InstanceMaterial.SetVectorArray(_BoolData_Size, m_ShaderSize);
            }
        }

        private void UpdateMask()
        {
            if (m_InstanceMaterial == null) return;

            // Mask lookup logic moved to CheckForChanges to avoid GetComponent in GetModifiedMaterial loop
            // But we double check here if null (first run)
            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
                m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled && m_CachedMask.Shape != null)
            {
                ProceduralShape maskShape = m_CachedMask.Shape;
                
                if (maskShape == this)
                {
                    m_InstanceMaterial.SetVector(_MaskParams, Vector4.zero);
                    return;
                }
                
                // Calculate Centers and Matrices
                // Optimization: Cache transforms locally to avoid property access overhead? 
                // Unity optimizes transform access in newer versions, but Matrix mult is still CPU work.
                
                Vector3 maskCenterWorld = maskShape.rectTransform.TransformPoint(maskShape.rectTransform.rect.center);
                Vector3 maskCenterInChildPivotFrame = rectTransform.InverseTransformPoint(maskCenterWorld);
                Vector3 maskCenterInChildSDFFrame = maskCenterInChildPivotFrame - (Vector3)rectTransform.rect.center;
                
                float maskWorldRot = maskShape.transform.eulerAngles.z;
                Vector2 maskSize = maskShape.rectTransform.rect.size * maskShape.ShapeScale;
                
                Vector4 maskShapeParams = Vector4.zero;
                if (maskShape.m_ShapeType == ShapeType.Rectangle) maskShapeParams = maskShape.m_CornerRadius;
                else if (maskShape.m_ShapeType == ShapeType.Polygon) maskShapeParams = new Vector4(maskShape.m_PolygonSides, maskShape.m_PolygonRounding, 0, 0);
                else if (maskShape.m_ShapeType == ShapeType.Star) maskShapeParams = new Vector4(maskShape.m_StarPoints, maskShape.m_StarRatio, maskShape.m_StarRoundingOuter, maskShape.m_StarRoundingInner);

                float relRot = maskWorldRot - transform.eulerAngles.z;
                float innerRot = 0;
                if (maskShape.m_ShapeType == ShapeType.Polygon) innerRot = maskShape.m_PolygonRotation * Mathf.Deg2Rad;
                else if (maskShape.m_ShapeType == ShapeType.Star) innerRot = maskShape.m_StarRotation * Mathf.Deg2Rad;
                
                m_InstanceMaterial.SetVector(_MaskParams, new Vector4(1f, (float)maskShape.m_ShapeType, maskShape.m_CornerSmoothing, m_CachedMask.Softness));
                m_InstanceMaterial.SetVector(_MaskTrans, new Vector4(maskCenterInChildSDFFrame.x, maskCenterInChildSDFFrame.y, relRot * Mathf.Deg2Rad, innerRot));
                m_InstanceMaterial.SetVector(_MaskSize, new Vector4(maskSize.x, maskSize.y, 0, 0));
                m_InstanceMaterial.SetVector(_MaskShape, maskShapeParams);
                
                // Mask Fill
                Texture gradientTex = maskShape.mainTexture;
                m_InstanceMaterial.SetTexture(_MaskTex, gradientTex != null ? gradientTex : Texture2D.whiteTexture);
                
                ShapeFill fill = maskShape.MainFill;
                float texHeight = gradientTex != null ? gradientTex.height : 3;
                float vCoord = 1.5f / texHeight;
                
                m_InstanceMaterial.SetVector(_MaskFillParams, new Vector4((float)fill.Type, fill.GradientAngle, fill.GradientScale, vCoord));
                m_InstanceMaterial.SetVector(_MaskFillOffset, new Vector4(fill.GradientOffset.x, fill.GradientOffset.y, 0, 0));
                
                // Mask Booleans
                int activeMaskCount = 0;
                RectTransform maskRT = maskShape.rectTransform;
                Vector3 maskCenterOffset = (Vector3)maskRT.rect.center;
                Matrix4x4 maskWorldToLocal = maskRT.worldToLocalMatrix;
                
                CollectMaskBooleanOps(maskShape, BooleanOperation.Union, ref activeMaskCount, maskWorldToLocal, maskCenterOffset);
                
                m_InstanceMaterial.SetInt(_MaskBoolParams, activeMaskCount);
                if (activeMaskCount > 0)
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
        }

        private void CollectBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 rootWorldToLocal)
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

                AddShapeToShader(other, effectiveOp, count, rootWorldToLocal, input.Smoothness);
                count++;

                // Removed isRoot param as logic is consistent
                CollectBooleanOps(other, input.Operation, ref count, rootWorldToLocal);
            }
        }

        private void AddShapeToShader(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 rootWorldToLocal, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            Vector3 otherWorldPos = otherRect.position; 
            Vector3 localPos = rootWorldToLocal.MultiplyPoint3x4(otherWorldPos);
            float relativeRotation = otherRect.eulerAngles.z - rectTransform.eulerAngles.z;

            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            
            if (shape.m_ShapeType == ShapeType.Rectangle) m_ShaderShapeParams[index] = shape.m_CornerRadius;
            else if (shape.m_ShapeType == ShapeType.Polygon) m_ShaderShapeParams[index] = new Vector4(shape.m_PolygonSides, shape.m_PolygonRounding, shape.m_PolygonRotation * Mathf.Deg2Rad, 0);
            else if (shape.m_ShapeType == ShapeType.Star) m_ShaderShapeParams[index] = new Vector4(shape.m_StarPoints, shape.m_StarRatio, shape.m_StarRoundingOuter, shape.m_StarRoundingInner);

            m_ShaderTransform[index] = new Vector4(localPos.x, localPos.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            float otherScale = shape.m_ShapeScale; 
            Vector3 lossyScaleRatio = new Vector3(otherRect.lossyScale.x / rectTransform.lossyScale.x, otherRect.lossyScale.y / rectTransform.lossyScale.y, 1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * otherScale;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * otherScale;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);

            if (shape.m_ShapeType == ShapeType.Star)
                m_ShaderTransform[index].w = shape.m_StarRotation * Mathf.Deg2Rad;
            else if (shape.m_ShapeType == ShapeType.Polygon)
                m_ShaderTransform[index].w = shape.m_PolygonRotation * Mathf.Deg2Rad;
        }
        
        private void CollectMaskBooleanOps(ProceduralShape currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 maskWorldToLocal, Vector3 maskCenterOffset)
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

                AddMaskShapeToShader(other, effectiveOp, count, maskWorldToLocal, maskCenterOffset, input.Smoothness);
                count++;

                CollectMaskBooleanOps(other, input.Operation, ref count, maskWorldToLocal, maskCenterOffset);
            }
        }

        private void AddMaskShapeToShader(ProceduralShape shape, BooleanOperation op, int index, Matrix4x4 maskWorldToLocal, Vector3 maskCenterOffset, float smoothness)
        {
            RectTransform otherRect = shape.rectTransform;
            Vector3 otherWorldPos = otherRect.position; 
            
            Vector3 localPos = maskWorldToLocal.MultiplyPoint3x4(otherWorldPos);
            localPos -= maskCenterOffset;
            
            float relativeRotation = otherRect.eulerAngles.z - m_CachedMask.Shape.transform.eulerAngles.z;

            m_MaskShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, shape.m_CornerSmoothing, smoothness); 
            
            if (shape.m_ShapeType == ShapeType.Rectangle) m_MaskShaderShapeParams[index] = shape.m_CornerRadius;
            else if (shape.m_ShapeType == ShapeType.Polygon) m_MaskShaderShapeParams[index] = new Vector4(shape.m_PolygonSides, shape.m_PolygonRounding, shape.m_PolygonRotation * Mathf.Deg2Rad, 0);
            else if (shape.m_ShapeType == ShapeType.Star) m_MaskShaderShapeParams[index] = new Vector4(shape.m_StarPoints, shape.m_StarRatio, shape.m_StarRoundingOuter, shape.m_StarRoundingInner);

            m_MaskShaderTransform[index] = new Vector4(localPos.x, localPos.y, relativeRotation * Mathf.Deg2Rad, 0);
            
            Vector3 lossyScaleRatio = new Vector3(
                otherRect.lossyScale.x / m_CachedMask.Shape.transform.lossyScale.x, 
                otherRect.lossyScale.y / m_CachedMask.Shape.transform.lossyScale.y, 
                1f);
            
            float finalW = otherRect.rect.width * lossyScaleRatio.x * shape.ShapeScale;
            float finalH = otherRect.rect.height * lossyScaleRatio.y * shape.ShapeScale;

            m_MaskShaderSize[index] = new Vector4(finalW, finalH, 0, 0);

            if (shape.m_ShapeType == ShapeType.Star)
                m_MaskShaderTransform[index].w = shape.m_StarRotation * Mathf.Deg2Rad;
            else if (shape.m_ShapeType == ShapeType.Polygon)
                m_MaskShaderTransform[index].w = shape.m_PolygonRotation * Mathf.Deg2Rad;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (m_TextureDirty || m_GradientTexture == null || m_GradientTexture.height != (Effects.Count + 1) * 3)
                RebuildGradientTexture();

            if (m_DisableRendering)
            {
                vh.Clear();
                return;
            }

            vh.Clear();
            Rect rect = rectTransform.rect;
            
            float minX = rect.xMin;
            float maxX = rect.xMax;
            float minY = rect.yMin;
            float maxY = rect.yMax;
            
            if (m_ShapeScale > 1.0f)
            {
                float cx = rect.center.x;
                float cy = rect.center.y;
                float hw = rect.width * 0.5f * m_ShapeScale;
                float hh = rect.height * 0.5f * m_ShapeScale;
                minX = Mathf.Min(minX, cx - hw);
                maxX = Mathf.Max(maxX, cx + hw);
                minY = Mathf.Min(minY, cy - hh);
                maxY = Mathf.Max(maxY, cy + hh);
            }

            // Only recurse if we have operations (Optimization)
            if (BooleanOperations.Count > 0)
            {
                Matrix4x4 worldToLocal = rectTransform.worldToLocalMatrix;
                ExpandBoundsRecursive(this, ref minX, ref maxX, ref minY, ref maxY, worldToLocal);
            }

            float maxExpand = 0f;
            float mainBlurRadius = 0f;
            
            foreach (var effect in Effects)
            {
                if (!effect.Enabled) continue;
                if (effect is DropShadowEffect shadow)
                    maxExpand = Mathf.Max(maxExpand, Mathf.Max(Mathf.Abs(shadow.Offset.x), Mathf.Abs(shadow.Offset.y)) + shadow.Blur * 2f + Mathf.Max(0, shadow.Spread));
                else if (effect is StrokeEffect stroke)
                    maxExpand = Mathf.Max(maxExpand, stroke.Alignment == StrokeAlignment.Outside ? stroke.Width : (stroke.Alignment == StrokeAlignment.Center ? stroke.Width * 0.5f : 0f));
                else if (effect is BlurEffect blur)
                    mainBlurRadius = Mathf.Max(mainBlurRadius, blur.Radius);
            }
            
            maxExpand = Mathf.Max(maxExpand, mainBlurRadius * 2f);
            
            minX -= maxExpand;
            maxX += maxExpand;
            minY -= maxExpand;
            maxY += maxExpand;

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is DropShadowEffect shadow && shadow.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 1, i + 1, shadow.Fill, new Vector3(shadow.Offset.x, shadow.Offset.y, shadow.Blur), new Vector4(shadow.Spread, 0, shadow.Fill.GradientOffset.x, shadow.Fill.GradientOffset.y));

            DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 0, 0, MainFill, new Vector3(0, 0, mainBlurRadius), new Vector4(0, 0, MainFill.GradientOffset.x, MainFill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is InnerShadowEffect inner && inner.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 3, i + 1, inner.Fill, new Vector3(inner.Offset.x, inner.Offset.y, inner.Blur), new Vector4(inner.Spread, 0, inner.Fill.GradientOffset.x, inner.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is StrokeEffect stroke && stroke.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 2, i + 1, stroke.Fill, Vector3.zero, new Vector4(stroke.Width, (float)stroke.Alignment, stroke.Fill.GradientOffset.x, stroke.Fill.GradientOffset.y));
            
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is BlurEffect blur && blur.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 4, i + 1, blur.Fill, Vector3.zero, new Vector4(blur.Radius, 0, blur.Fill.GradientOffset.x, blur.Fill.GradientOffset.y));
        }

        private void ExpandBoundsRecursive(ProceduralShape currentShape, ref float minX, ref float maxX, ref float minY, ref float maxY, Matrix4x4 rootWorldToLocal)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (input.SourceShape == this) continue;

                ProceduralShape other = input.SourceShape;
                RectTransform rt = other.rectTransform;
                
                rt.GetWorldCorners(s_Corners);
                
                for (int i = 0; i < 4; i++)
                {
                    Vector3 localPt = rootWorldToLocal.MultiplyPoint3x4(s_Corners[i]);
                    minX = Mathf.Min(minX, localPt.x);
                    maxX = Mathf.Max(maxX, localPt.x);
                    minY = Mathf.Min(minY, localPt.y);
                    maxY = Mathf.Max(maxY, localPt.y);
                }

                ExpandBoundsRecursive(other, ref minX, ref maxX, ref minY, ref maxY, rootWorldToLocal);
            }
        }

        private void DrawLayerQuad(VertexHelper vh, float minX, float maxX, float minY, float maxY, Rect baseRect, int effectType, int textureRowIndex, ShapeFill fill, Vector3 normalData, Vector4 tangentData)
        {
            float polyCosR = 1f, polySinR = 0f;
            float baseWidth = baseRect.width, baseHeight = baseRect.height;
            float rotation = 0f;

            if (m_ShapeType == ShapeType.Polygon) rotation = m_PolygonRotation;
            else if (m_ShapeType == ShapeType.Star) rotation = m_StarRotation;

            if (rotation != 0f)
            {
                float rad = -rotation * Mathf.Deg2Rad; 
                polyCosR = Mathf.Cos(rad); polySinR = Mathf.Sin(rad);
                if (effectType == 1 || effectType == 3)
                {
                    float ox = normalData.x, oy = normalData.y;
                    normalData.x = ox * polyCosR - oy * polySinR;
                    normalData.y = ox * polySinR + oy * polyCosR;
                }
            }

            Vector4 uv1_shapeParams = Vector4.zero;
            if (m_ShapeType == ShapeType.Rectangle) uv1_shapeParams = m_CornerRadius;
            else if (m_ShapeType == ShapeType.Polygon) uv1_shapeParams = new Vector4(m_PolygonSides, m_PolygonRounding, 0, 0);
            else if (m_ShapeType == ShapeType.Star) uv1_shapeParams = new Vector4(m_StarPoints, m_StarRatio, m_StarRoundingOuter, m_StarRoundingInner);

            float packedShapeData = (float)m_ShapeType + (m_CornerSmoothing * 0.99f);
            
            float scaledW = baseWidth * m_ShapeScale;
            float scaledH = baseHeight * m_ShapeScale;
            Vector4 uv2_baseData = new Vector4(scaledW, scaledH, packedShapeData, effectType);
            
            float packedRowAndHeight = textureRowIndex + (m_GradientTexture.height * 100f);
            Vector4 uv3_fillParams = new Vector4(packedRowAndHeight, (float)fill.Type, fill.GradientAngle, fill.GradientScale);

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color; 
            vert.uv1 = uv1_shapeParams;
            vert.uv2 = uv2_baseData;
            vert.uv3 = uv3_fillParams;
            vert.normal = normalData;
            vert.tangent = tangentData;

            int startIndex = vh.currentVertCount;
            Vector2 GetRotatedUV0(float x, float y) => new Vector2(x * polyCosR - y * polySinR, x * polySinR + y * polyCosR);

            vert.position = new Vector3(minX, minY); vert.uv0 = GetRotatedUV0(minX - baseRect.center.x, minY - baseRect.center.y); vh.AddVert(vert);
            vert.position = new Vector3(minX, maxY); vert.uv0 = GetRotatedUV0(minX - baseRect.center.x, maxY - baseRect.center.y); vh.AddVert(vert);
            vert.position = new Vector3(maxX, maxY); vert.uv0 = GetRotatedUV0(maxX - baseRect.center.x, maxY - baseRect.center.y); vh.AddVert(vert);
            vert.position = new Vector3(maxX, minY); vert.uv0 = GetRotatedUV0(maxX - baseRect.center.x, minY - baseRect.center.y); vh.AddVert(vert);

            vh.AddTriangle(startIndex + 0, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex + 0);
        }
    }
}