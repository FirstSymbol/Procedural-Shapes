using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Procedural Shapes/Shape")]
    public partial class ProceduralShape : MaskableGraphic, ICanvasRaycastFilter, ISerializationCallbackReceiver
    {
        [Tooltip("Если включено, фигура не будет отрисовываться, но может использоваться как Cutter для других фигур.")]
        [SerializeField] private bool m_DisableRendering = false;

        private void OnDrawGizmos()
        {
            if (m_DisableRendering && !Application.isPlaying)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                
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

        [Range(0f, 1f)] public float m_CapsuleRounding = 1f;

        public Vector2 m_LineStart = new Vector2(-50, 0);
        public Vector2 m_LineEnd = new Vector2(50, 0);
        [Range(0.1f, 100f)] public float m_LineWidth = 5f;

        [Range(0f, 1f)] public float m_RingInnerRadius = 0.5f;
        [Range(0f, 360f)] public float m_RingStartAngle = 0f;
        [Range(0f, 360f)] public float m_RingEndAngle = 360f;
        
        [Header("Path Settings")]
        public ShapePath m_ShapePath = new ShapePath();
        [HideInInspector] public List<Vector2> m_FlattenedPath = new List<Vector2>();

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

        [System.NonSerialized] private int m_MainFillAtlasIndex = -1;
        [System.NonSerialized] private List<int> m_EffectAtlasIndices = new List<int>();
        [System.NonSerialized] private bool m_TextureDirty = true;
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

        private RectTransform m_RectTransform;
        public new RectTransform rectTransform => m_RectTransform ? m_RectTransform : (m_RectTransform = GetComponent<RectTransform>());

        private bool m_NeedUpdate = true;

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

        public override Texture mainTexture 
        {
            get 
            {
                RebuildGradientTexture();
                return GradientAtlasManager.AtlasTexture != null ? GradientAtlasManager.AtlasTexture : s_WhiteTexture;
            }
        }

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
            return new Vector2((m_ShapePivot.x - 0.5f) * r.width, (m_ShapePivot.y - 0.5f) * r.height);
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

        public new bool IsActive => gameObject.activeInHierarchy && enabled;
        [System.NonSerialized] private uint m_Version = 0;
        public uint Version => m_Version;

        public override void SetAllDirty() 
        { 
            m_TextureDirty = true; 
            m_NeedUpdate = true; 
            m_Version++;
            base.SetVerticesDirty(); 
            base.SetMaterialDirty(); 
        }

        protected override void OnValidate()
        {
            base.OnValidate();
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
            m_Version++;
        }

        private void LateUpdate()
        {
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            bool dirty = m_NeedUpdate;

            if (transform.hasChanged)
            {
                m_Version++;
                dirty = true;
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
                if (m_CachedMask.Shape != null)
                {
                    if (CheckDependencyDirty(m_CachedMask.Shape)) dirty = true;
                    
                    var maskOps = m_CachedMask.Shape.BooleanOperations;
                    for (int i = 0; i < maskOps.Count; i++)
                    {
                        if (maskOps[i].SourceShape != null && CheckDependencyDirty(maskOps[i].SourceShape))
                            dirty = true;
                    }
                }
            }

            if (BooleanOperations.Count > 0)
            {
                for (int i = 0; i < BooleanOperations.Count; i++)
                {
                    var other = BooleanOperations[i].SourceShape;
                    if (other != null && other.isActiveAndEnabled)
                    {
                        if (CheckDependencyDirty(other)) 
                            dirty = true;
                    }
                }
            }

            if (dirty)
            {
                m_NeedUpdate = false;
                base.SetMaterialDirty();
                base.SetVerticesDirty(); 
            }
        }

        private Dictionary<int, uint> m_KnownDependencyVersions = new Dictionary<int, uint>();

        private bool CheckDependencyDirty(ProceduralShape other)
        {
            int id = other.GetInstanceID();
            uint currentVer = other.Version;
            
            // Also check if the other shape's transform has changed (it might not have its own LateUpdate running yet or ever)
            if (other.transform.hasChanged)
            {
                // We don't clear other.transform.hasChanged here to avoid breaking its own update
                // But we know it's dirty.
                return true; 
            }

            if (!m_KnownDependencyVersions.TryGetValue(id, out uint lastVer) || lastVer != currentVer)
            {
                m_KnownDependencyVersions[id] = currentVer;
                return true;
            }
            return false;
        }

        protected override void OnDestroy() 
        { 
            base.OnDestroy(); 
            if (m_InstanceMaterial != null) ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
        }

        private void RebuildGradientTexture()
        {
            if (!m_TextureDirty) return;

            m_MainFillAtlasIndex = GradientAtlasManager.GetAtlasRow(MainFill);
            m_EffectAtlasIndices.Clear();
            for (int i = 0; i < Effects.Count; i++) 
                m_EffectAtlasIndices.Add(GradientAtlasManager.GetAtlasRow(Effects[i].Fill));

            m_TextureDirty = false;
        }
    }
}