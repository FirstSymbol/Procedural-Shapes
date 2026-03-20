using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // for VertexHelper
using ProceduralShapes.Runtime;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Входные данные для булевой операции с процедурными спрайтами (World Space).
    /// </summary>
    [System.Serializable]
    public class BooleanInputSprite
    {
        public BooleanOperation Operation = BooleanOperation.Subtraction;
        public ProceduralSprite SourceShape;
        [Range(0f, 200f)] public float Smoothness = 0f;
    }

    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Procedural Shapes/Procedural Sprite (World)")]
    public partial class ProceduralSprite : MonoBehaviour, IProceduralShape
    {
        [Tooltip("Размер спрайта в мировых единицах")]
        [SerializeField] private Vector2 m_Size = new Vector2(1f, 1f);
        public Vector2 Size
        {
            get => m_Size;
            set { if (m_Size != value) { m_Size = value; SetAllDirty(); } }
        }

        [Tooltip("Цвет (Tint)")]
        [SerializeField] private Color m_Color = Color.white;
        public Color color
        {
            get => m_Color;
            set { if (m_Color != value) { m_Color = value; SetAllDirty(); } }
        }

        [Tooltip("Отключить отрисовку, сохранив функционал резака.")]
        [SerializeField] private bool m_DisableRendering = false;
        public bool DisableRendering
        {
            get => m_DisableRendering;
            set { if (m_DisableRendering != value) { m_DisableRendering = value; SetAllDirty(); } }
        }

        [Header("Определение формы")]
        public ShapeType m_ShapeType = ShapeType.Rectangle;
        
        [SerializeField] private Vector2 m_ShapeScale2D = new Vector2(1f, 1f);
        public Vector2 ShapeScale
        {
            get => m_ShapeScale2D;
            set { if (m_ShapeScale2D != value) { m_ShapeScale2D = value; SetAllDirty(); } }
        }

        [SerializeField] private bool m_LinkScale = true;
        public bool LinkScale
        {
            get => m_LinkScale;
            set { if (m_LinkScale != value) { m_LinkScale = value; SetAllDirty(); } }
        }

        [SerializeField] private Vector2 m_ShapePivot = new Vector2(0.5f, 0.5f);
        public Vector2 ShapePivot
        {
            get => m_ShapePivot;
            set { if (m_ShapePivot != value) { m_ShapePivot = value; SetAllDirty(); } }
        }

        public Vector4 m_CornerRadius = Vector4.zero;
        [Range(0f, 1f)] public float m_CornerSmoothing = 0f;
        
        [Range(3, 128)] public int m_PolygonSides = 5;
        [Range(0f, 1f)] public float m_PolygonRounding = 0f;

        [Range(3, 128)] public int m_StarPoints = 5;
        [Range(0.01f, 1f)] public float m_StarRatio = 0.5f;
        [Range(0f, 1f)] public float m_StarRoundingOuter = 0f;
        [Range(0f, 1f)] public float m_StarRoundingInner = 0f;

        [Range(0f, 1f)] public float m_CapsuleRounding = 1f;

        public Vector2 m_LineStart = new Vector2(-0.5f, 0);
        public Vector2 m_LineEnd = new Vector2(0.5f, 0);
        [Range(0.01f, 10f)] public float m_LineWidth = 0.1f;

        [Range(0f, 1f)] public float m_RingInnerRadius = 0.5f;
        [Range(0f, 360f)] public float m_RingStartAngle = 0f;
        [Range(0f, 360f)] public float m_RingEndAngle = 360f;
        
        [Header("Настройки пути")]
        public ShapePath m_ShapePath = new ShapePath();
        [HideInInspector] public List<Vector2> m_FlattenedPath = new List<Vector2>();

        [Range(-10f, 10f)] public float m_InternalPadding = 0f;
        [Range(0f, 10f)] public float m_EdgeSoftness = 0.05f;

        [Header("Шум на краях")]
        [Range(0f, 50f)] public float m_EdgeNoiseAmount = 0f;
        [Range(0.01f, 1f)] public float m_EdgeNoiseScale = 0.1f;

        [Header("Булевы операции")]
        public List<BooleanInputSprite> BooleanOperations = new List<BooleanInputSprite>();

        [Tooltip("Растягивать фигуру по всему размеру вместо сохранения пропорций.")]
        [SerializeField] private bool m_StretchToFill = false;
        public bool StretchToFill
        {
            get => m_StretchToFill;
            set { if (m_StretchToFill != value) { m_StretchToFill = value; SetAllDirty(); } }
        }

        [Header("Внешний вид")]
        public ShapeFill MainFill = new ShapeFill();
        [SerializeReference] public List<ProceduralEffect> Effects = new List<ProceduralEffect>();

        // IProceduralShape properties
        public ShapeType ShapeType => m_ShapeType;
        public Vector4 CornerRadius => m_CornerRadius;
        public float CornerSmoothing => m_CornerSmoothing;
        public int PolygonSides => m_PolygonSides;
        public float PolygonRounding => m_PolygonRounding;
        public int StarPoints => m_StarPoints;
        public float StarRatio => m_StarRatio;
        public float StarRoundingOuter => m_StarRoundingOuter;
        public float StarRoundingInner => m_StarRoundingInner;
        public float CapsuleRounding => m_CapsuleRounding;
        public Vector2 LineStart => m_LineStart;
        public Vector2 LineEnd => m_LineEnd;
        public float LineWidth => m_LineWidth;
        public float RingInnerRadius => m_RingInnerRadius;
        public float RingStartAngle => m_RingStartAngle;
        public float RingEndAngle => m_RingEndAngle;
        public ShapePath ShapePath => m_ShapePath;
        public float InternalPadding => m_InternalPadding;
        public float EdgeSoftness => m_EdgeSoftness;
        public float EdgeNoiseAmount => m_EdgeNoiseAmount;
        public float EdgeNoiseScale => m_EdgeNoiseScale;

        ShapeFill IProceduralShape.MainFill => MainFill;
        List<ProceduralEffect> IProceduralShape.Effects => Effects;

        List<BooleanInput> IProceduralShape.BooleanOperations => null; // Not using UI Booleans

        [System.NonSerialized] private int m_MainFillAtlasIndex = -1;
        [System.NonSerialized] private List<int> m_EffectAtlasIndices = new List<int>();
        [System.NonSerialized] private bool m_TextureDirty = true;
        
        public int MainFillAtlasIndex => m_MainFillAtlasIndex;
        public List<int> EffectAtlasIndices => m_EffectAtlasIndices;

        private Material m_InstanceMaterial; 
        private bool m_InstanceMaterialIsPooled = true; 
        
        private MeshFilter m_MeshFilter;
        private MeshRenderer m_MeshRenderer;
        private Mesh m_Mesh;
        private VertexHelper m_VertexHelper;

        private static Material s_DefaultMaterial;
        private static Shader s_SpriteShader;

        private const int MAX_OPS = 8;
        private Vector4[] m_ShaderOps = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderShapeParams = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderTransform = new Vector4[MAX_OPS];
        private Vector4[] m_ShaderSize = new Vector4[MAX_OPS];

        private static readonly Vector3[] s_Corners = new Vector3[4];

        internal static readonly HashSet<ProceduralSprite> ActiveShapes = new HashSet<ProceduralSprite>();

        public Rect GetRect()
        {
            return new Rect(-m_Size.x * 0.5f, -m_Size.y * 0.5f, m_Size.x, m_Size.y);
        }

        public Vector2 GetGeometricCenterOffset()
        {
            return new Vector2((m_ShapePivot.x - 0.5f) * m_Size.x, (m_ShapePivot.y - 0.5f) * m_Size.y);
        }

        public Matrix4x4 WorldToLocalMatrix => transform.worldToLocalMatrix;

        [System.NonSerialized] private uint m_Version = 0;
        public uint Version => m_Version;
        [System.NonSerialized] private bool m_IsNotifying = false;
        public event System.Action OnShapeChanged;
        [System.NonSerialized] private List<ProceduralSprite> m_SubscribedSources = new List<ProceduralSprite>();

        private static Shader GetSpriteShader()
        {
            if (s_SpriteShader == null) s_SpriteShader = Shader.Find("Sprites/ProceduralShapes/Shape");
            return s_SpriteShader;
        }

        public Material defaultMaterial
        {
            get
            {
                if (s_DefaultMaterial == null)
                {
                    Shader shader = GetSpriteShader();
                    if (shader != null) 
                    {
                        s_DefaultMaterial = new Material(shader);
                        s_DefaultMaterial.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return s_DefaultMaterial;
            }
        }

        private void Awake()
        {
            m_MeshFilter = GetComponent<MeshFilter>();
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_VertexHelper = new VertexHelper();
            m_Mesh = new Mesh();
            m_Mesh.name = "ProceduralSpriteMesh";
            m_Mesh.hideFlags = HideFlags.HideAndDontSave;
            m_Mesh.MarkDynamic();
            m_MeshFilter.sharedMesh = m_Mesh;
        }

        private void OnEnable()
        {
            ActiveShapes.Add(this);
            RefreshDependencies();
            SetAllDirty();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update += EditorUpdate;
            }
#endif
        }

        private void OnDisable()
        {
            ActiveShapes.Remove(this);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
            foreach (var s in m_SubscribedSources)
            {
                if (s != null) s.OnShapeChanged -= HandleDependencyChanged;
            }
            m_SubscribedSources.Clear();
            OnShapeChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (m_InstanceMaterial != null) ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
            if (m_Mesh != null)
            {
                if (Application.isPlaying) Destroy(m_Mesh);
                else DestroyImmediate(m_Mesh);
            }
        }

        partial void OnShapeChangedCustom();

        public void SetAllDirty()
        {
            if (m_IsNotifying) return;
            m_IsNotifying = true;

            m_TextureDirty = true;
            m_Version++;
            
            RebuildMesh();
            UpdateMaterial();
            OnShapeChangedCustom();

            OnShapeChanged?.Invoke();
            m_IsNotifying = false;
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

        private void RebuildMesh()
        {
            if (m_MeshFilter == null) m_MeshFilter = GetComponent<MeshFilter>();
            if (m_VertexHelper == null) m_VertexHelper = new VertexHelper();
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh();
                m_Mesh.name = "ProceduralSpriteMesh";
                m_Mesh.hideFlags = HideFlags.HideAndDontSave;
                m_Mesh.MarkDynamic();
            }

            OnPopulateMesh(m_VertexHelper);
            m_VertexHelper.FillMesh(m_Mesh);
            m_MeshFilter.sharedMesh = m_Mesh;
        }

        private void UpdateMaterial()
        {
            if (m_MeshRenderer == null) m_MeshRenderer = GetComponent<MeshRenderer>();
            Material mat = GetModifiedMaterial(defaultMaterial);
            if (m_MeshRenderer.sharedMaterial != mat)
            {
                m_MeshRenderer.sharedMaterial = mat;
            }
            
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            m_MeshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", m_Color);
            m_MeshRenderer.SetPropertyBlock(mpb);
        }

        private Vector3 m_LastLossyScale;
        private void LateUpdate()
        {
            if (this == null || !isActiveAndEnabled) return;
            bool dirty = false;
            bool selfChanged = transform.hasChanged;
            
            if (selfChanged && transform.lossyScale != m_LastLossyScale)
            {
                m_LastLossyScale = transform.lossyScale;
                dirty = true;
            }

            foreach (var op in BooleanOperations)
            {
                if (op.SourceShape != null && (selfChanged || op.SourceShape.transform.hasChanged))
                {
                    if (CheckRelativeTransformDirty(op.SourceShape)) dirty = true;
                }
            }

            if (dirty) SetAllDirty();
            transform.hasChanged = false;
        }

        private void RefreshDependencies()
        {
            foreach (var s in m_SubscribedSources)
            {
                if (s != null) s.OnShapeChanged -= HandleDependencyChanged;
            }
            m_SubscribedSources.Clear();

            if (!isActiveAndEnabled) return;

            HashSet<ProceduralSprite> uniqueSources = new HashSet<ProceduralSprite>();
            foreach (var op in BooleanOperations)
            {
                if (op.SourceShape != null && op.SourceShape != this) 
                    uniqueSources.Add(op.SourceShape);
            }

            foreach (var s in uniqueSources)
            {
                s.OnShapeChanged += HandleDependencyChanged;
                m_SubscribedSources.Add(s);
            }
        }

        private void HandleDependencyChanged() { SetAllDirty(); }

        private Dictionary<int, Matrix4x4> m_KnownRelativeTransforms = new Dictionary<int, Matrix4x4>();

        private bool CheckRelativeTransformDirty(ProceduralSprite other)
        {
            if (other == null) return false;
            int id = other.GetInstanceID();
            Matrix4x4 relativeMatrix = transform.worldToLocalMatrix * other.transform.localToWorldMatrix;
            
            if (!m_KnownRelativeTransforms.TryGetValue(id, out Matrix4x4 lastMatrix) || !MatricesAreClose(lastMatrix, relativeMatrix))
            {
                m_KnownRelativeTransforms[id] = relativeMatrix;
                return true;
            }
            return false;
        }

        private bool MatricesAreClose(Matrix4x4 a, Matrix4x4 b)
        {
            for(int i = 0; i < 16; i++) {
                if(Mathf.Abs(a[i] - b[i]) > 0.0001f) return false;
            }
            return true;
        }

#if UNITY_EDITOR
        protected void OnValidate()
        {
            RefreshDependencies();
            UnityEditor.EditorApplication.delayCall += () => {
                if (this != null) SetAllDirty();
            };
        }

        private void EditorUpdate()
        {
            if (this == null || !isActiveAndEnabled) return;
            
            if (m_MeshFilter != null && m_MeshFilter.sharedMesh == null)
            {
                SetAllDirty();
            }
            else if (m_MeshRenderer != null && m_MeshRenderer.sharedMaterial == null)
            {
                SetAllDirty();
            }

            LateUpdate();
        }

        internal static void ForceUpdateAllSprites()
        {
            foreach (var shape in ActiveShapes)
            {
                if (shape != null)
                {
                    shape.m_InstanceMaterial = null;
                    shape.SetAllDirty();
                }
            }
        }
#endif
    }
}