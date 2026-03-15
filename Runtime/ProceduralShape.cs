using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Основной компонент для отрисовки процедурных фигур в Unity UI.
    /// Использует математику SDF (Signed Distance Fields) для рендеринга вектроной графики.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Procedural Shapes/Shape")]
    public partial class ProceduralShape : MaskableGraphic, ICanvasRaycastFilter, ISerializationCallbackReceiver
    {
        [Tooltip("Если включено, фигура не будет отрисовываться, но может использоваться как Cutter (резак) для других фигур.")]
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

        [Header("Определение формы (Shape Definition)")]
        /// <summary> Тип основной геометрической фигуры. </summary>
        public ShapeType m_ShapeType = ShapeType.Rectangle;
        
        [Tooltip("Масштаб фигуры внутри границ RectTransform.")]
        [SerializeField, HideInInspector] private float m_ShapeScale = 1.0f;

        [SerializeField] private Vector2 m_ShapeScale2D = new Vector2(1f, 1f);
        [SerializeField] private bool m_LinkScale = true;
        [SerializeField, HideInInspector] private bool m_ScaleMigrated = false;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Миграция старого одномерного масштаба в новый Vector2
            if (!m_ScaleMigrated)
            {
                m_ShapeScale2D = new Vector2(m_ShapeScale, m_ShapeScale);
                m_ScaleMigrated = true;
            }
        }

        [Tooltip("Точка привязки геометрии относительно центра RectTransform. (0.5, 0.5) — строго по центру.")]
        [SerializeField] private Vector2 m_ShapePivot = new Vector2(0.5f, 0.5f);

        /// <summary> Радиусы скругления углов (X=TL, Y=TR, Z=BR, W=BL). Только для Rectangle. </summary>
        public Vector4 m_CornerRadius = Vector4.zero;
        /// <summary> Плавность скругления углов. </summary>
        [Range(0f, 1f)] public float m_CornerSmoothing = 0f;
        
        /// <summary> Количество сторон многоугольника. </summary>
        [Range(3, 128)] public int m_PolygonSides = 5;
        /// <summary> Скругление углов многоугольника. </summary>
        [Range(0f, 1f)] public float m_PolygonRounding = 0f;

        /// <summary> Количество лучей звезды. </summary>
        [Range(3, 128)] public int m_StarPoints = 5;
        /// <summary> Соотношение внутреннего и внешнего радиуса звезды. </summary>
        [Range(0.01f, 1f)] public float m_StarRatio = 0.5f;
        /// <summary> Скругление внешних углов звезды. </summary>
        [Range(0f, 1f)] public float m_StarRoundingOuter = 0f;
        /// <summary> Скругление внутренних углов звезды. </summary>
        [Range(0f, 1f)] public float m_StarRoundingInner = 0f;

        /// <summary> Скругление концов капсулы. </summary>
        [Range(0f, 1f)] public float m_CapsuleRounding = 1f;

        /// <summary> Начальная точка линии в локальных координатах. </summary>
        public Vector2 m_LineStart = new Vector2(-50, 0);
        /// <summary> Конечная точка линии в локальных координатах. </summary>
        public Vector2 m_LineEnd = new Vector2(50, 0);
        /// <summary> Толщина линии. </summary>
        [Range(0.1f, 100f)] public float m_LineWidth = 5f;

        /// <summary> Внутренний радиус кольца (0-1). </summary>
        [Range(0f, 1f)] public float m_RingInnerRadius = 0.5f;
        /// <summary> Начальный угол сегмента кольца. </summary>
        [Range(0f, 360f)] public float m_RingStartAngle = 0f;
        /// <summary> Конечный угол сегмента кольца. </summary>
        [Range(0f, 360f)] public float m_RingEndAngle = 360f;
        
        [Header("Настройки пути (Path Settings)")]
        /// <summary> Данные векторного пути. </summary>
        public ShapePath m_ShapePath = new ShapePath();
        /// <summary> Оптимизированный список точек после аппроксимации кривых Безье. </summary>
        [HideInInspector] public List<Vector2> m_FlattenedPath = new List<Vector2>();

        [Range(-100f, 100f)]
        [Tooltip("Внутренний отступ (Inset). Положительные значения сужают фигуру, отрицательные — расширяют.")]
        public float m_InternalPadding = 0f;

        [Range(0f, 10f)] 
        [Tooltip("Сглаживание краев (антиалиасинг). Значение около 1.0 обычно оптимально.")]
        public float m_EdgeSoftness = 1.0f;

        [Header("Шум на краях (Edge Noise)")]
        /// <summary> Интенсивность шума на границах фигуры. </summary>
        [Range(0f, 50f)] public float m_EdgeNoiseAmount = 0f;
        /// <summary> Масштаб (частота) шума. </summary>
        [Range(0.01f, 1f)] public float m_EdgeNoiseScale = 0.1f;

        [Header("Булевы операции (Boolean Operations)")]
        /// <summary> Список фигур, которые взаимодействуют с текущей (вычитание, объединение и т.д.). </summary>
        public List<BooleanInput> BooleanOperations = new List<BooleanInput>();

        [Header("Внешний вид (Appearance)")]
        /// <summary> Настройки заливки основной фигуры. </summary>
        public ShapeFill MainFill = new ShapeFill();

        /// <summary> Список дополнительных эффектов (тени, свечение, обводка). </summary>
        [SerializeReference] 
        public List<ProceduralEffect> Effects = new List<ProceduralEffect>();

        [System.NonSerialized] private int m_MainFillAtlasIndex = -1;
        [System.NonSerialized] private List<int> m_EffectAtlasIndices = new List<int>();
        [System.NonSerialized] private bool m_TextureDirty = true;
        private Material m_InstanceMaterial; 
        private ProceduralShapeMask m_CachedMask;
        
        private static Material s_DefaultMaterial;
        private static Shader s_ShapeShader;

        /// <summary> Возвращает шейдер, используемый для отрисовки фигур. </summary>
        private static Shader GetShapeShader()
        {
            if (s_ShapeShader == null)
            {
                s_ShapeShader = Shader.Find("UI/ProceduralShapes/Shape");
                if (s_ShapeShader == null)
                {
                    Debug.LogError("[ProceduralShape] Шейдер 'UI/ProceduralShapes/Shape' не найден! Убедитесь, что плагин установлен корректно.");
                }
            }
            return s_ShapeShader;
        }

        private RectTransform m_RectTransform;
        /// <summary> Кешированная ссылка на RectTransform. </summary>
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

        /// <summary> Основная текстура графического элемента (атлас градиентов). </summary>
        public override Texture mainTexture 
        {
            get 
            {
                RebuildGradientTexture();
                return GradientAtlasManager.AtlasTexture != null ? GradientAtlasManager.AtlasTexture : s_WhiteTexture;
            }
        }

        internal static readonly HashSet<ProceduralShape> ActiveShapes = new HashSet<ProceduralShape>();

        /// <summary> Отключить отрисовку, сохранив функционал резака. </summary>
        public bool DisableRendering
        {
            get => m_DisableRendering;
            set { if (m_DisableRendering != value) { m_DisableRendering = value; SetAllDirty(); } }
        }

        /// <summary> Масштаб фигуры. </summary>
        public Vector2 ShapeScale
        {
            get => m_ShapeScale2D;
            set { if (m_ShapeScale2D != value) { m_ShapeScale2D = value; SetAllDirty(); } }
        }

        /// <summary> Связать масштаб по X и Y. </summary>
        public bool LinkScale
        {
            get => m_LinkScale;
            set { if (m_LinkScale != value) { m_LinkScale = value; SetAllDirty(); } }
        }

        /// <summary> Пивот геометрии. </summary>
        public Vector2 ShapePivot
        {
            get => m_ShapePivot;
            set { if (m_ShapePivot != value) { m_ShapePivot = value; SetAllDirty(); } }
        }

        /// <summary> Внутренний отступ. </summary>
        public float InternalPadding
        {
            get => m_InternalPadding;
            set { if (Mathf.Abs(m_InternalPadding - value) > 0.0001f) { m_InternalPadding = value; SetAllDirty(); } }
        }

        /// <summary> Рассчитывает смещение центра геометрии на основе пивота. </summary>
        public Vector2 GetGeometricCenterOffset()
        {
            Rect r = rectTransform.rect;
            return new Vector2((m_ShapePivot.x - 0.5f) * r.width, (m_ShapePivot.y - 0.5f) * r.height);
        }

        public override Material defaultMaterial
        {
            get
            {
                if (s_DefaultMaterial == null)
                {
                    Shader shader = GetShapeShader();
                    if (shader != null) 
                    {
                        s_DefaultMaterial = new Material(shader);
                        s_DefaultMaterial.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return s_DefaultMaterial ?? base.defaultMaterial;
            }
        }

        /// <summary> Событие, вызываемое при любом изменении параметров фигуры. </summary>
        public event System.Action OnShapeChanged;
        [System.NonSerialized] private bool m_IsNotifying = false;
        [System.NonSerialized] private List<ProceduralShape> m_SubscribedSources = new List<ProceduralShape>();

        public new bool IsActive => gameObject.activeInHierarchy && enabled;
        [System.NonSerialized] private uint m_Version = 0;
        /// <summary> Версия изменений объекта. Используется зависимыми объектами для отслеживания изменений. </summary>
        public uint Version => m_Version;

        /// <summary> Помечает объект как требующий полной перерисовки и обновления зависимостей. </summary>
        public override void SetAllDirty() 
        { 
            if (m_IsNotifying) return;
            m_IsNotifying = true;

            m_TextureDirty = true; 
            m_NeedUpdate = true; 
            m_Version++;
            base.SetVerticesDirty(); 
            base.SetMaterialDirty(); 

            OnShapeChanged?.Invoke();
            m_IsNotifying = false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            RefreshDependencies();
            SetAllDirty();
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            m_InstanceMaterial = null;
            m_CachedMask = null;
            m_SubscribedSources.Clear();
            EnsureCanvasChannels();
        }

        protected override void OnEnable() 
        { 
            base.OnEnable(); 
            ActiveShapes.Add(this);
            m_RectTransform = GetComponent<RectTransform>();
            RefreshDependencies();
            EnsureCanvasChannels();
            SetAllDirty(); 
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update += EditorUpdate;
            }
#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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

        private void EnsureCanvasChannels()
        {
            if (canvas != null)
            {
                var channels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
                if ((canvas.additionalShaderChannels & channels) != channels)
                {
                    canvas.additionalShaderChannels |= channels;
                }
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null || !isActiveAndEnabled) return;
            CheckForChanges();
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void InitEditorHooks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += (scene) => ForceUpdateAllShapes();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => ForceUpdateAllShapes();
        }

        private static void ForceUpdateAllShapes()
        {
            ProceduralMaterialPool.ClearAll();
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
        
        protected override void OnTransformParentChanged() 
        { 
            base.OnTransformParentChanged(); 
            m_CachedMask = null; 
            EnsureCanvasChannels();
            RefreshDependencies();
            SetAllDirty(); 
        }
        
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetAllDirty();
        }

        private Vector3 m_LastPos;
        private Quaternion m_LastRot;
        private Vector3 m_LastScale;

        private void LateUpdate()
        {
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            bool dirty = false;
            
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                dirty = true;
            }

            Transform t = transform;
            if (m_LastPos != t.position || m_LastRot != t.rotation || m_LastScale != t.lossyScale)
            {
                m_LastPos = t.position;
                m_LastRot = t.rotation;
                m_LastScale = t.lossyScale;
                dirty = true;
            }

            foreach (var op in BooleanOperations)
            {
                if (op.SourceShape != null && CheckDependencyDirty(op.SourceShape)) 
                    dirty = true;
            }

            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled && m_CachedMask.Shape != null)
            {
                if (CheckDependencyDirty(m_CachedMask.Shape)) dirty = true;
            }

            if (dirty)
            {
                SetAllDirty();
            }
        }

        /// <summary>
        /// Обновляет подписки на изменения других фигур, участвующих в булевых операциях или маскировании.
        /// </summary>
        private void RefreshDependencies()
        {
            foreach (var s in m_SubscribedSources)
            {
                if (s != null) s.OnShapeChanged -= HandleDependencyChanged;
            }
            m_SubscribedSources.Clear();

            if (!isActiveAndEnabled) return;

            m_CachedMask = GetComponentInParent<ProceduralShapeMask>();

            HashSet<ProceduralShape> uniqueSources = new HashSet<ProceduralShape>();
            foreach (var op in BooleanOperations)
            {
                if (op.SourceShape != null && op.SourceShape != this) 
                    uniqueSources.Add(op.SourceShape);
            }

            if (m_CachedMask != null && m_CachedMask.isActiveAndEnabled && m_CachedMask.Shape != null && m_CachedMask.Shape != this)
            {
                uniqueSources.Add(m_CachedMask.Shape);
            }

            foreach (var s in uniqueSources)
            {
                s.OnShapeChanged += HandleDependencyChanged;
                m_SubscribedSources.Add(s);
            }
        }

        private void HandleDependencyChanged()
        {
            SetAllDirty();
        }

        private struct TransformData 
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 lossyScale;
            
            public TransformData(Transform t)
            {
                position = t.position;
                rotation = t.rotation;
                lossyScale = t.lossyScale;
            }
            
            public bool Equals(TransformData other)
            {
                return position == other.position && rotation == other.rotation && lossyScale == other.lossyScale;
            }
        }

        private Dictionary<int, uint> m_KnownDependencyVersions = new Dictionary<int, uint>();
        private Dictionary<int, TransformData> m_KnownDependencyTransforms = new Dictionary<int, TransformData>();

        /// <summary> Проверяет, изменилась ли зависимая фигура. </summary>
        private bool CheckDependencyDirty(ProceduralShape other)
        {
            int id = other.GetInstanceID();
            uint currentVer = other.Version;
            
            if (other.transform.hasChanged)
            {
                return true; 
            }

            TransformData currentData = new TransformData(other.transform);
            if (!m_KnownDependencyTransforms.TryGetValue(id, out TransformData lastData) || !lastData.Equals(currentData))
            {
                m_KnownDependencyTransforms[id] = currentData;
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

        /// <summary> Обновляет данные в атласе градиентов для основной заливки и эффектов. </summary>
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