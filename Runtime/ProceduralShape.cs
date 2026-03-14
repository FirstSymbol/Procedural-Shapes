using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Procedural Shapes/Shape")]
    public class ProceduralShape : MaskableGraphic, ICanvasRaycastFilter, ISerializationCallbackReceiver
    {
        [Tooltip("Если включено, фигура не будет отрисовываться, но может использоваться как Cutter для других фигур.")]
        [SerializeField] private bool m_DisableRendering = false;

        // ... (другие поля)

        #region Raycast Filtering (SDF on CPU)
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
                return false;

            Vector2 pivotOffset = GetGeometricCenterOffset();
            Vector2 p = localPoint - (rectTransform.rect.center + pivotOffset);

            if (Mathf.Abs(m_ShapeRotation) > 0.001f)
            {
                float angle = -m_ShapeRotation * Mathf.Deg2Rad;
                float s = Mathf.Sin(angle);
                float c = Mathf.Cos(angle);
                p = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            Vector2 halfSize = rectTransform.rect.size * 0.5f * m_ShapeScale2D;
            float d = GetSDF_CPU(p, halfSize, m_ShapeType, m_CornerSmoothing, GetPackedShapeParams());
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
                    
                    float d2 = GetSDF_CPU(p2, op.SourceShape.rectTransform.rect.size * 0.5f * op.SourceShape.ShapeScale, 
                                         op.SourceShape.m_ShapeType, op.SourceShape.m_CornerSmoothing, op.SourceShape.GetPackedShapeParams());
                    
                    if (op.Operation == BooleanOperation.Union) d = Mathf.Min(d, d2);
                    else if (op.Operation == BooleanOperation.Subtraction) d = Mathf.Max(d, -d2);
                    else if (op.Operation == BooleanOperation.Intersection) d = Mathf.Max(d, d2);
                }
            }

            return d <= m_EdgeSoftness; 
        }

        private float GetSDF_CPU(Vector2 p, Vector2 halfSize, ShapeType type, float smoothing, Vector4 params4)
        {
            float minHalfSize = Mathf.Min(halfSize.x, halfSize.y);
            switch (type)
            {
                case ShapeType.Rectangle:
                    float r = 0;
                    if (p.x < 0 && p.y > 0) r = params4.x;
                    else if (p.x >= 0 && p.y > 0) r = params4.y;
                    else if (p.x >= 0 && p.y <= 0) r = params4.z;
                    else if (p.x < 0 && p.y <= 0) r = params4.w;
                    r = Mathf.Min(r, minHalfSize);
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(r, r);
                    return Mathf.Min(Mathf.Max(q.x, q.y), 0.0f) + Vector2.Max(q, Vector2.zero).magnitude - r;

                case ShapeType.Ellipse:
                    return (new Vector2(p.x / halfSize.x, p.y / halfSize.y).magnitude - 1.0f) * minHalfSize;

                case ShapeType.Polygon:
                {
                    float n = Mathf.Max(3.0f, params4.x);
                    float an = Mathf.PI / n;
                    float a = Mathf.Atan2(p.x, p.y);
                    float bn = Mathf.Floor(a / (2.0f * an));
                    float f = a - (bn + 0.5f) * 2.0f * an;
                    Vector2 p_sec = new Vector2(p.magnitude * Mathf.Abs(Mathf.Sin(f)), p.magnitude * Mathf.Cos(f));
                    float rounding = params4.y * minHalfSize * 0.5f;
                    float rOuter = minHalfSize - rounding;
                    Vector2 closest = new Vector2(Mathf.Clamp(p_sec.x, -rOuter * Mathf.Sin(an), rOuter * Mathf.Sin(an)), rOuter * Mathf.Cos(an));
                    return (p_sec - closest).magnitude * Mathf.Sign(p_sec.y - closest.y) - rounding;
                }

                case ShapeType.Star:
                {
                    float n = Mathf.Max(3.0f, params4.x);
                    float maxR = minHalfSize;
                    
                    float ro = params4.z * maxR * 0.5f;
                    float rOut = Mathf.Max(maxR - ro, 0.001f);
                    float rIn  = Mathf.Max(params4.y * maxR - ro, 0.001f);
                    
                    float an = Mathf.PI / n;
                    float a = Mathf.Atan2(p.x, p.y);
                    float f = Mathf.Abs(a) % (2.0f * an);
                    if (f > an) f = 2.0f * an - f;
                    
                    Vector2 q0 = p.magnitude * new Vector2(Mathf.Sin(f), Mathf.Cos(f));
                    Vector2 q1 = p.magnitude * new Vector2(Mathf.Sin(2.0f * an - f), Mathf.Cos(2.0f * an - f));
                    
                    Vector2 p1 = new Vector2(0.0f, rOut);
                    Vector2 p2 = new Vector2(rIn * Mathf.Sin(an), rIn * Mathf.Cos(an));
                    
                    Vector2 ba = p2 - p1;
                    float ba2 = Mathf.Max(Vector2.Dot(ba, ba), 0.00001f);
                    
                    Vector2 pa0 = q0 - p1;
                    float h0 = Mathf.Clamp01(Vector2.Dot(pa0, ba) / ba2);
                    Vector2 d0 = pa0 - ba * h0;
                    float s0 = (pa0.y * ba.x - pa0.x * ba.y >= 0.0f) ? 1.0f : -1.0f;
                    float dist0 = d0.magnitude * s0;
                    
                    Vector2 pa1 = q1 - p1;
                    float h1 = Mathf.Clamp01(Vector2.Dot(pa1, ba) / ba2);
                    Vector2 d1 = pa1 - ba * h1;
                    float s1 = (pa1.y * ba.x - pa1.x * ba.y >= 0.0f) ? 1.0f : -1.0f;
                    float dist1 = d1.magnitude * s1;
                    
                    float rInner = params4.w * maxR;
                    float finalDist = dist0;
                    if (rInner > 0.001f)
                    {
                        float h = Mathf.Clamp01(0.5f + 0.5f * (dist1 - dist0) / rInner);
                        finalDist = Mathf.Lerp(dist1, dist0, h) - rInner * h * (1.0f - h);
                    }
                    
                    return finalDist - ro;
                }

                case ShapeType.Capsule:
                    float cr = params4.x * minHalfSize;
                    Vector2 ch = Vector2.Max(halfSize - new Vector2(cr, cr), Vector2.zero);
                    Vector2 cq = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - ch;
                    return Vector2.Max(cq, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(cq.x, cq.y), 0.0f) - cr;

                case ShapeType.Line:
                {
                    Vector2 pa = p - new Vector2(params4.x, params4.y);
                    Vector2 ba = new Vector2(params4.z, params4.w) - new Vector2(params4.x, params4.y);
                    float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                    return (pa - ba * h).magnitude - smoothing * 0.5f;
                }

                case ShapeType.Ring:
                {
                    float innerR = params4.x * minHalfSize;
                    float thickness = (minHalfSize - innerR) * 0.5f;
                    float midR = (minHalfSize + innerR) * 0.5f;
                    float d = Mathf.Abs(p.magnitude - midR) - thickness;
                    if (Mathf.Abs(params4.z - params4.y) < Mathf.PI * 2.0f)
                    {
                        float ang = Mathf.Atan2(p.x, p.y);
                        float da = frac((ang - params4.y) / (Mathf.PI * 2.0f));
                        float targetDa = frac((params4.z - params4.y) / (Mathf.PI * 2.0f));
                        if (da > targetDa)
                        {
                            Vector2 p1 = new Vector2(Mathf.Sin(params4.y), Mathf.Cos(params4.y)) * midR;
                            Vector2 p2 = new Vector2(Mathf.Sin(params4.z), Mathf.Cos(params4.z)) * midR;
                            d = Mathf.Max(d, Mathf.Min((p - p1).magnitude, (p - p2).magnitude) - thickness);
                        }
                    }
                    return d;
                }

                default:
                    return 100000.0f;
            }
        }

        private float frac(float x) => x - Mathf.Floor(x);
        #endregion

        private void OnDrawGizmos()
        {
            if (m_DisableRendering && !Application.isPlaying)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                
                // Рисуем упрощенный контур для визуализации в Scene View
                Rect r = rectTransform.rect;
                Gizmos.DrawWireCube(r.center + GetGeometricCenterOffset(), r.size * m_ShapeScale2D);
            }
        }

        [Header("Shape Definition")]
        public ShapeType m_ShapeType = ShapeType.Rectangle;
        
        [Tooltip("Uniform scale factor for the shape inside the RectTransform bounds.")]
        [SerializeField, HideInInspector] private float m_ShapeScale = 1.0f;

        [SerializeField] private Vector2 m_ShapeScale2D = new Vector2(1f, 1f);
        [SerializeField] private bool m_LinkScale = true;
        [SerializeField, HideInInspector] private bool m_ScaleMigrated = false;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!m_ScaleMigrated)
            {
                m_ShapeScale2D = new Vector2(m_ShapeScale, m_ShapeScale);
                m_ScaleMigrated = true;
            }
        }

        [Tooltip("Rotation of the shape geometry in degrees.")]
        [SerializeField] private float m_ShapeRotation = 0f;
        
        [Tooltip("Pivot of the shape geometry relative to the RectTransform center. (0.5, 0.5) is centered.")]
        [SerializeField] private Vector2 m_ShapePivot = new Vector2(0.5f, 0.5f);

        public Vector4 m_CornerRadius = Vector4.zero;
        [Range(0f, 1f)] public float m_CornerSmoothing = 0f;
        
        [Range(3, 128)] public int m_PolygonSides = 5;
        [Range(0f, 1f)] public float m_PolygonRounding = 0f;

        [Range(3, 128)] public int m_StarPoints = 5;
        [Range(0.01f, 1f)] public float m_StarRatio = 0.5f;
        [Range(0f, 1f)] public float m_StarRoundingOuter = 0f;
        [Range(0f, 1f)] public float m_StarRoundingInner = 0f;

        // Capsule
        [Range(0f, 1f)] public float m_CapsuleRounding = 1f;

        // Line
        public Vector2 m_LineStart = new Vector2(-50, 0);
        public Vector2 m_LineEnd = new Vector2(50, 0);
        [Range(0.1f, 100f)] public float m_LineWidth = 5f;

        // Ring
        [Range(0f, 1f)] public float m_RingInnerRadius = 0.5f;
        [Range(0f, 360f)] public float m_RingStartAngle = 0f;
        [Range(0f, 360f)] public float m_RingEndAngle = 360f;

        [Range(0f, 100f)]
        [Tooltip("Внутренний отступ (Inset). Положительные значения сужают фигуру, отрицательные — расширяют.")]
        public float m_InternalPadding = 0f;

        [Range(0f, 10f)] 
        [Tooltip("Сглаживание краев (антиалиасинг). Значение около 1.0 обычно оптимально.")]
        public float m_EdgeSoftness = 1.0f;

        [Header("Edge Noise")]
        [Range(0f, 50f)] public float m_EdgeNoiseAmount = 0f;
        [Range(0.01f, 1f)] public float m_EdgeNoiseScale = 0.1f;

        [Header("Boolean Operations")]
        public List<BooleanInput> BooleanOperations = new List<BooleanInput>();

        [Header("Appearance")]
        public ShapeFill MainFill = new ShapeFill();

        [SerializeReference] 
        public List<ProceduralEffect> Effects = new List<ProceduralEffect>();

        private int m_MainFillAtlasIndex = -1;
        private List<int> m_EffectAtlasIndices = new List<int>();
        private bool m_TextureDirty = true;
        private Material m_InstanceMaterial; 
        private ProceduralShapeMask m_CachedMask;
        
        private static Material s_SharedMaterial;
        private static Material sharedMaterial 
        {
            get 
            {
                if (s_SharedMaterial == null) s_SharedMaterial = new Material(Shader.Find("UI/ProceduralShapes/Shape"));
                return s_SharedMaterial;
            }
        }

        #region Gradient Atlas System
        private static Texture2D s_AtlasTexture;
        private static List<GradientDataEntry> s_AtlasRegistry = new List<GradientDataEntry>();
        private const int ATLAS_WIDTH = 256;
        private const int ATLAS_HEIGHT = 512;

        private struct GradientDataEntry 
        { 
            public FillType type; 
            public Color color; 
            public string gradientHash; // Content-based hash
            public int rowIndex;
        }

        private static int GetAtlasRow(ShapeFill fill)
        {
            if (s_AtlasTexture == null)
            {
                s_AtlasTexture = new Texture2D(ATLAS_WIDTH, ATLAS_HEIGHT, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color[] clear = new Color[ATLAS_WIDTH * ATLAS_HEIGHT];
                for(int i=0; i<clear.Length; i++) clear[i] = Color.clear;
                // Row 0 is always white for Solid fill batching
                for(int x=0; x<ATLAS_WIDTH; x++) clear[x] = clear[ATLAS_WIDTH + x] = clear[ATLAS_WIDTH*2 + x] = Color.white;
                
                s_AtlasTexture.SetPixels(clear);
                s_AtlasTexture.Apply();
            }

            if (fill.Type == FillType.Solid) return 0;

            string currentHash = GetGradientHash(fill.Gradient);
            for (int i = 0; i < s_AtlasRegistry.Count; i++)
            {
                var data = s_AtlasRegistry[i];
                if (data.type == fill.Type && data.gradientHash == currentHash)
                    return data.rowIndex;
            }

            int newIndex = (s_AtlasRegistry.Count + 1); // Row 0 is reserved
            if (newIndex * 3 + 3 > ATLAS_HEIGHT) return 0; // Out of space

            s_AtlasRegistry.Add(new GradientDataEntry { type = fill.Type, gradientHash = currentHash, rowIndex = newIndex });
            BakeToAtlas(newIndex, fill);
            return newIndex;
        }

        private static string GetGradientHash(Gradient g)
        {
            if (g == null) return "null";
            var ck = g.colorKeys;
            var ak = g.alphaKeys;
            string hash = $"{ck.Length}_{ak.Length}";
            for (int i = 0; i < ck.Length; i++) hash += $"_{ck[i].color.GetHashCode()}_{ck[i].time}";
            for (int i = 0; i < ak.Length; i++) hash += $"_{ak[i].alpha}_{ak[i].time}";
            return hash;
        }

        private static void BakeToAtlas(int index, ShapeFill fill)
        {
            int rowStart = index * 3;
            Color[] pixels = new Color[ATLAS_WIDTH * 3];
            for (int x = 0; x < ATLAS_WIDTH; x++)
            {
                Color c = fill.Gradient.Evaluate(x / (float)(ATLAS_WIDTH - 1));
                pixels[x] = pixels[ATLAS_WIDTH + x] = pixels[ATLAS_WIDTH * 2 + x] = c;
            }
            s_AtlasTexture.SetPixels(0, rowStart, ATLAS_WIDTH, 3, pixels);
            s_AtlasTexture.Apply();
        }
        #endregion

        private RectTransform m_RectTransform;
        public new RectTransform rectTransform => m_RectTransform ? m_RectTransform : (m_RectTransform = GetComponent<RectTransform>());

        private Matrix4x4 m_LastLocalToWorld = Matrix4x4.identity;
        private Rect m_LastRect = Rect.zero;
        private struct TransformState { public Vector3 pos; public Quaternion rot; public Vector3 scale; public Rect rect; }
        private Dictionary<int, TransformState> m_TrackedStates = new Dictionary<int, TransformState>();
        private bool m_NeedUpdate = true;

        private static readonly int _BoolParams1 = Shader.PropertyToID("_BoolParams1");
        private static readonly int _BoolData_OpType = Shader.PropertyToID("_BoolData_OpType");
        private static readonly int _BoolData_ShapeParams = Shader.PropertyToID("_BoolData_ShapeParams");
        private static readonly int _BoolData_Transform = Shader.PropertyToID("_BoolData_Transform");
        private static readonly int _BoolData_Size = Shader.PropertyToID("_BoolData_Size");
        private static readonly int _AntiAliasing = Shader.PropertyToID("_AntiAliasing");
        private static readonly int _InternalPadding = Shader.PropertyToID("_InternalPadding");
        
        private static readonly int _MaskParams = Shader.PropertyToID("_MaskParams");
        private static readonly int _MaskMatrixX = Shader.PropertyToID("_MaskMatrixX");
        private static readonly int _MaskMatrixY = Shader.PropertyToID("_MaskMatrixY");
        private static readonly int _MaskMatrixZ = Shader.PropertyToID("_MaskMatrixZ");
        private static readonly int _MaskMatrixW = Shader.PropertyToID("_MaskMatrixW");
        
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

        public override Texture mainTexture => s_AtlasTexture != null ? s_AtlasTexture : s_WhiteTexture;

        public bool DisableRendering
        {
            get => m_DisableRendering;
            set { if (m_DisableRendering != value) { m_DisableRendering = value; SetAllDirty(); } }
        }

        public Vector2 ShapeScale
        {
            get => m_ShapeScale2D;
            set { if (m_ShapeScale2D != value) { m_ShapeScale2D = value; SetAllDirty(); } }
        }

        public bool LinkScale
        {
            get => m_LinkScale;
            set { if (m_LinkScale != value) { m_LinkScale = value; SetAllDirty(); } }
        }

        public float ShapeRotation
        {
            get => m_ShapeRotation;
            set { if (Mathf.Abs(m_ShapeRotation - value) > 0.0001f) { m_ShapeRotation = value; SetAllDirty(); } }
        }
        
        public Vector2 ShapePivot
        {
            get => m_ShapePivot;
            set { if (m_ShapePivot != value) { m_ShapePivot = value; SetAllDirty(); } }
        }

        public float InternalPadding
        {
            get => m_InternalPadding;
            set { if (Mathf.Abs(m_InternalPadding - value) > 0.0001f) { m_InternalPadding = value; SetAllDirty(); } }
        }

        public Vector2 GetGeometricCenterOffset()
        {
            Rect r = rectTransform.rect;
            return new Vector2((0.5f - m_ShapePivot.x) * r.width, (0.5f - m_ShapePivot.y) * r.height);
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
        public override void SetAllDirty() { m_TextureDirty = true; m_NeedUpdate = true; base.SetVerticesDirty(); base.SetMaterialDirty(); }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (MainFill != null && MainFill.Type == FillType.Solid)
            {
                color = MainFill.SolidColor;
            }
            SetAllDirty();
        }

        protected override void OnEnable() 
        { 
            base.OnEnable(); 
            m_RectTransform = GetComponent<RectTransform>();
            SetAllDirty(); 
        }
        
        protected override void OnTransformParentChanged() 
        { 
            base.OnTransformParentChanged(); 
            m_CachedMask = null; 
            SetAllDirty(); 
        }
        
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            m_NeedUpdate = true;
        }

        private void LateUpdate()
        {
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            bool dirty = false;

            if (transform.hasChanged)
            {
                Matrix4x4 currentL2W = transform.localToWorldMatrix;
                if (currentL2W != m_LastLocalToWorld)
                {
                    m_LastLocalToWorld = currentL2W;
                    dirty = true;
                }
                
                if (rectTransform.rect != m_LastRect)
                {
                    m_LastRect = rectTransform.rect;
                    dirty = true;
                }
                
                transform.hasChanged = false;
            }

            if (m_CachedMask == null || !m_CachedMask.gameObject.activeInHierarchy)
            {
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
                
                if (m_CachedMask.Shape != null)
                {
                    foreach (var op in m_CachedMask.Shape.BooleanOperations)
                    {
                        if (op.SourceShape != null && CheckTransformDirty(op.SourceShape.transform, op.SourceShape.rectTransform))
                            dirty = true;
                    }
                }
            }

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
            if (m_InstanceMaterial != null) DestroyImmediate(m_InstanceMaterial);
        }

        private void RebuildGradientTexture()
        {
            if (!m_TextureDirty) return;

            m_MainFillAtlasIndex = GetAtlasRow(MainFill);
            m_EffectAtlasIndices.Clear();
            for (int i = 0; i < Effects.Count; i++) 
                m_EffectAtlasIndices.Add(GetAtlasRow(Effects[i].Fill));

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
            if (BooleanOperations.Count == 0 && m_CachedMask == null && MainFill.Type != FillType.Pattern)
            {
                return base.GetModifiedMaterial(baseMaterial);
            }

            Material mat = base.GetModifiedMaterial(baseMaterial);

            if (m_InstanceMaterial == null || m_InstanceMaterial.shader != mat.shader)
            {
                m_InstanceMaterial = new Material(mat);
                m_InstanceMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            
            m_InstanceMaterial.CopyPropertiesFromMaterial(mat);
            m_InstanceMaterial.shaderKeywords = mat.shaderKeywords;

            if (MainFill.Type == FillType.Pattern && MainFill.PatternTexture != null)
            {
                m_InstanceMaterial.SetTexture("_PatternTex", MainFill.PatternTexture);
            }
            
            UpdateBooleanProperties();
            UpdateMask(); 
            
            return m_InstanceMaterial;
        }

        private void UpdateBooleanProperties()
        {
            int activeCount = 0;
            Matrix4x4 worldToLocal = rectTransform.worldToLocalMatrix;
            Vector3 selfCenterOffset = GetGeometricCenterOffset();

            CollectBooleanOps(this, BooleanOperation.Union, ref activeCount, worldToLocal, selfCenterOffset);

            m_InstanceMaterial.SetFloat(_InternalPadding, m_InternalPadding);
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
                
                // Calculate matrix: ChildLocal -> World -> MaskLocal -> MaskSDF
                Matrix4x4 childLocalToWorld = rectTransform.localToWorldMatrix;
                Matrix4x4 maskWorldToLocal = maskShape.rectTransform.worldToLocalMatrix;
                
                // Mask Local -> Mask SDF
                Vector2 maskSizeRaw = maskShape.rectTransform.rect.size;
                Vector2 maskPivot = maskShape.rectTransform.pivot;
                Vector2 maskPivotOffset = maskShape.GetGeometricCenterOffset();
                
                Vector3 maskRectCenterFromPivot = new Vector3((0.5f - maskPivot.x) * maskSizeRaw.x, (0.5f - maskPivot.y) * maskSizeRaw.y, 0f);
                Vector3 maskTotalCenterCorrection = maskRectCenterFromPivot + (Vector3)maskPivotOffset;
                
                Matrix4x4 maskCenterTranslate = Matrix4x4.Translate(-maskTotalCenterCorrection);
                Matrix4x4 maskRotateToSDF = Matrix4x4.Rotate(Quaternion.Euler(0, 0, -maskShape.ShapeRotation));
                
                // Final Unified Matrix (no more child center correction needed here, because we map directly from vertex pos)
                Matrix4x4 localToMaskSDF = maskRotateToSDF * maskCenterTranslate * maskWorldToLocal * childLocalToWorld;

                // Child vertices are already relative to the child's rect center in OnPopulateMesh.
                // We need to offset the matrix to account for the fact that the shader input 'p' is child's local coord + child's center offset.
                Vector2 childGeomCenterLocal = rectTransform.rect.center + GetGeometricCenterOffset();
                Matrix4x4 childGeomToLocal = Matrix4x4.Translate(new Vector3(childGeomCenterLocal.x, childGeomCenterLocal.y, 0));
                
                Matrix4x4 finalMatrix = localToMaskSDF * childGeomToLocal;

                m_InstanceMaterial.SetVector(_MaskMatrixX, finalMatrix.GetRow(0));
                m_InstanceMaterial.SetVector(_MaskMatrixY, finalMatrix.GetRow(1));
                m_InstanceMaterial.SetVector(_MaskMatrixZ, finalMatrix.GetRow(2));
                m_InstanceMaterial.SetVector(_MaskMatrixW, finalMatrix.GetRow(3));
                
                Vector2 maskScale = maskShape.ShapeScale;
                Vector2 maskSize = new Vector2(maskSizeRaw.x * maskScale.x, maskSizeRaw.y * maskScale.y);
                
                m_InstanceMaterial.SetVector(_MaskParams, new Vector4(1f, (float)maskShape.m_ShapeType, maskShape.m_CornerSmoothing, m_CachedMask.Softness));
                m_InstanceMaterial.SetVector(_MaskSize, new Vector4(maskSize.x, maskSize.y, 0, 0));
                m_InstanceMaterial.SetVector(_MaskShape, maskShape.GetPackedShapeParams());
                
                Texture gradientTex = maskShape.mainTexture;
                m_InstanceMaterial.SetTexture(_MaskTex, gradientTex != null ? gradientTex : Texture2D.whiteTexture);
                
                ShapeFill fill = maskShape.MainFill;
                float vCoord = 1.5f / ATLAS_HEIGHT;
                
                m_InstanceMaterial.SetVector(_MaskFillParams, new Vector4((float)fill.Type, fill.GradientAngle, fill.GradientScale, vCoord));
                m_InstanceMaterial.SetVector(_MaskFillOffset, new Vector4(fill.GradientOffset.x, fill.GradientOffset.y, 0, 0));
                
                int activeMaskCount = 0;
                Matrix4x4 worldToMaskSDF = maskRotateToSDF * maskCenterTranslate * maskWorldToLocal;
                CollectMaskBooleanOps(maskShape, BooleanOperation.Union, ref activeMaskCount, worldToMaskSDF);
                
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
            
            if (shape.m_ShapeType == ShapeType.Rectangle) m_ShaderShapeParams[index] = shape.m_CornerRadius;
            else if (shape.m_ShapeType == ShapeType.Polygon) m_ShaderShapeParams[index] = new Vector4(shape.m_PolygonSides, shape.m_PolygonRounding, 0, 0);
            else if (shape.m_ShapeType == ShapeType.Star) m_ShaderShapeParams[index] = new Vector4(shape.m_StarPoints, shape.m_StarRatio, shape.m_StarRoundingOuter, shape.m_StarRoundingInner);
            else if (shape.m_ShapeType == ShapeType.Capsule) m_ShaderShapeParams[index] = new Vector4(shape.m_CapsuleRounding, 0, 0, 0);
            else if (shape.m_ShapeType == ShapeType.Line) m_ShaderShapeParams[index] = new Vector4(shape.m_LineStart.x, shape.m_LineStart.y, shape.m_LineEnd.x, shape.m_LineEnd.y);
            else if (shape.m_ShapeType == ShapeType.Ring) m_ShaderShapeParams[index] = new Vector4(shape.m_RingInnerRadius, shape.m_RingStartAngle * Mathf.Deg2Rad, shape.m_RingEndAngle * Mathf.Deg2Rad, 0);

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
            float relativeRotation = otherWorldRot - maskWorldRot - m_CachedMask.Shape.ShapeRotation + shape.ShapeRotation;

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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
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
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 1, m_EffectAtlasIndices[i], shadow.Fill, new Vector3(shadow.Offset.x, shadow.Offset.y, shadow.Blur), new Vector4(shadow.Spread, m_ShapeRotation * Mathf.Deg2Rad, shadow.Fill.GradientOffset.x, shadow.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is OuterGlowEffect glow && glow.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 1, m_EffectAtlasIndices[i], glow.Fill, new Vector3(0, 0, glow.Blur), new Vector4(glow.Spread, m_ShapeRotation * Mathf.Deg2Rad, glow.Fill.GradientOffset.x, glow.Fill.GradientOffset.y));

            // Main Fill & Stroke use Tight Mesh
            DrawLayerMesh(vh, minX, maxX, minY, maxY, rect, 0, 0, MainFill, new Vector3(m_InternalPadding, m_EdgeSoftness, mainBlurRadius), new Vector4(0, m_ShapeRotation * Mathf.Deg2Rad, MainFill.GradientOffset.x, MainFill.GradientOffset.y), maxExpand);

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is InnerShadowEffect inner && inner.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 3, i + 1, inner.Fill, new Vector3(inner.Offset.x, inner.Offset.y, inner.Blur), new Vector4(inner.Spread, m_ShapeRotation * Mathf.Deg2Rad, inner.Fill.GradientOffset.x, inner.Fill.GradientOffset.y));

            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is StrokeEffect stroke && stroke.Enabled)
                    DrawLayerMesh(vh, minX, maxX, minY, maxY, rect, 2, i + 1, stroke.Fill, new Vector3(m_InternalPadding, m_EdgeSoftness, 0), new Vector4(stroke.Width, (float)stroke.Alignment, stroke.Fill.GradientOffset.x, stroke.Fill.GradientOffset.y), maxExpand);
            
            for (int i = 0; i < Effects.Count; i++)
                if (Effects[i] is BlurEffect blur && blur.Enabled)
                    DrawLayerQuad(vh, minX, maxX, minY, maxY, rect, 4, i + 1, blur.Fill, new Vector3(m_InternalPadding, m_EdgeSoftness, 0), new Vector4(blur.Radius, m_ShapeRotation * Mathf.Deg2Rad, blur.Fill.GradientOffset.x, blur.Fill.GradientOffset.y));
        }

        private void DrawLayerMesh(VertexHelper vh, float minX, float maxX, float minY, float maxY, Rect baseRect, int effectType, int textureRowIndex, ShapeFill fill, Vector3 normalData, Vector4 tangentData, float expansion)
        {
            bool isRoundedStar = m_ShapeType == ShapeType.Star && (m_StarRoundingOuter > 0.01f || m_StarRoundingInner > 0.01f);
            
            if (m_ShapeType == ShapeType.Rectangle || m_ShapeType == ShapeType.Line || m_ShapeType == ShapeType.Capsule || expansion > 5f || isRoundedStar || BooleanOperations.Count > 0)
            {
                // Для прямоугольников, линий, капсул, составных фигур и сильно размытых фигур Quad все еще эффективен или необходим
                DrawLayerQuad(vh, minX, maxX, minY, maxY, baseRect, effectType, textureRowIndex, fill, normalData, tangentData);
                return;
            }

            // Генерируем Tight Mesh (Полигон)
            int segments = 32;
            if (m_ShapeType == ShapeType.Polygon) segments = m_PolygonSides;
            else if (m_ShapeType == ShapeType.Star) segments = m_StarPoints * 2;

            Vector2 pivotOffset = GetGeometricCenterOffset();
            float cx = baseRect.center.x + pivotOffset.x;
            float cy = baseRect.center.y + pivotOffset.y;
            float hw = baseRect.width * 0.5f * m_ShapeScale2D.x + expansion;
            float hh = baseRect.height * 0.5f * m_ShapeScale2D.y + expansion;

            int startVert = vh.currentVertCount;
            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
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

            // Center vertex
            vert.position = new Vector3(cx, cy);
            vert.uv0 = Vector2.zero;
            vh.AddVert(vert);

            float angleStep = 360f / segments;
            float rotOffset = m_ShapeRotation;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (i * angleStep + rotOffset) * Mathf.Deg2Rad;
                float s = Mathf.Sin(angle);
                float c = Mathf.Cos(angle);
                
                float r = 1f;
                if (m_ShapeType == ShapeType.Star && (i % 2 != 0)) r = m_StarRatio;

                Vector2 pos = new Vector2(cx + s * hw * r, cy + c * hh * r);
                vert.position = pos;
                vert.uv0 = new Vector2(pos.x - cx, pos.y - cy);
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
            return Vector4.zero;
        }

        private Vector4 GetPackedBaseData(Rect rect, int effectType, float smoothing)
        {
            float packedShapeData = (float)m_ShapeType + (Mathf.Clamp01(smoothing / 1000f) * 0.99f);
            return new Vector4(rect.width * m_ShapeScale2D.x, rect.height * m_ShapeScale2D.y, packedShapeData, effectType);
        }

        private Vector4 GetPackedFillParams(int rowIndex, ShapeFill fill)
        {
            float packedRowAndHeight = rowIndex + (ATLAS_HEIGHT * 100f);
            float type = (float)fill.Type;
            
            // Пакуем NoiseAmount в дробную часть угла
            float angle = fill.GradientAngle;
            float packedNoiseAmount = Mathf.Floor(angle) + (Mathf.Clamp(m_EdgeNoiseAmount, 0f, 50f) / 100f);
            
            // Если шум активен, используем uv3.w для масштаба шума
            float scaleOrNoise = fill.GradientScale;
            if (m_EdgeNoiseAmount > 0.001f)
            {
                scaleOrNoise = m_EdgeNoiseScale;
            }

            if (fill.Type == FillType.Pattern)
            {
                // Для паттерна угол не важен, используем только для шума
                packedNoiseAmount = 0f + (Mathf.Clamp(m_EdgeNoiseAmount, 0f, 50f) / 100f);
            }
            
            return new Vector4(packedRowAndHeight, type, packedNoiseAmount, scaleOrNoise);
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
            Vector4 uv1_shapeParams = Vector4.zero;
            float customSmoothing = m_CornerSmoothing;

            if (m_ShapeType == ShapeType.Rectangle) uv1_shapeParams = m_CornerRadius;
            else if (m_ShapeType == ShapeType.Polygon) uv1_shapeParams = new Vector4(m_PolygonSides, m_PolygonRounding, 0, 0);
            else if (m_ShapeType == ShapeType.Star) uv1_shapeParams = new Vector4(m_StarPoints, m_StarRatio, m_StarRoundingOuter, m_StarRoundingInner);
            else if (m_ShapeType == ShapeType.Capsule) uv1_shapeParams = new Vector4(m_CapsuleRounding, 0, 0, 0);
            else if (m_ShapeType == ShapeType.Line) 
            {
                uv1_shapeParams = new Vector4(m_LineStart.x, m_LineStart.y, m_LineEnd.x, m_LineEnd.y);
                customSmoothing = m_LineWidth; // Используем для толщины линии
            }
            else if (m_ShapeType == ShapeType.Ring)
            {
                uv1_shapeParams = new Vector4(m_RingInnerRadius, m_RingStartAngle * Mathf.Deg2Rad, m_RingEndAngle * Mathf.Deg2Rad, 0);
            }

            float packedShapeData = (float)m_ShapeType + (Mathf.Clamp01(customSmoothing / 1000f) * 0.99f);
            
            float scaledW = baseRect.width * m_ShapeScale2D.x;
            float scaledH = baseRect.height * m_ShapeScale2D.y;
            Vector4 uv2_baseData = new Vector4(scaledW, scaledH, packedShapeData, effectType);
            
            float packedRowAndHeight = textureRowIndex + (ATLAS_HEIGHT * 100f);
            Vector4 uv3_fillParams = new Vector4(packedRowAndHeight, (float)fill.Type, fill.GradientAngle, fill.GradientScale);

            Vector2 pivotOffset = GetGeometricCenterOffset();

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color; 
            
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

            int startIndex = vh.currentVertCount;
            float cx = baseRect.center.x + pivotOffset.x;
            float cy = baseRect.center.y + pivotOffset.y;
            
            vert.position = new Vector3(minX, minY); vert.uv0 = new Vector2(minX - cx, minY - cy); vh.AddVert(vert);
            vert.position = new Vector3(minX, maxY); vert.uv0 = new Vector2(minX - cx, maxY - cy); vh.AddVert(vert);
            vert.position = new Vector3(maxX, maxY); vert.uv0 = new Vector2(maxX - cx, maxY - cy); vh.AddVert(vert);
            vert.position = new Vector3(maxX, minY); vert.uv0 = new Vector2(maxX - cx, minY - cy); vh.AddVert(vert);

            vh.AddTriangle(startIndex + 0, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex + 0);
        }
    }
}