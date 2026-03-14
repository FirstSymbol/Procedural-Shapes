#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProceduralShapes.Runtime;

namespace ProceduralShapes.Editor
{
    [CustomEditor(typeof(ProceduralShape))]
    [CanEditMultipleObjects]
    public class ProceduralShapeEditor : UnityEditor.UI.GraphicEditor
    {
        private SerializedProperty m_DisableRendering;
        private SerializedProperty m_EdgeSoftness;
        private SerializedProperty m_InternalPadding;
        private SerializedProperty m_ShapeScale2D;
        private SerializedProperty m_LinkScale;
        private SerializedProperty m_ShapePivot;
        private SerializedProperty m_ShapeRotation;
        private SerializedProperty m_ShapeType, m_CornerRadius, m_CornerSmoothing;
        private SerializedProperty m_PolygonSides, m_PolygonRounding; // Rotation removed
        private SerializedProperty m_StarPoints, m_StarRatio, m_StarRoundingOuter, m_StarRoundingInner; // Rotation removed
        private SerializedProperty m_CapsuleRounding;
        private SerializedProperty m_LineStart, m_LineEnd, m_LineWidth;
        private SerializedProperty m_RingInnerRadius, m_RingStartAngle, m_RingEndAngle;
        private SerializedProperty m_ShapePath;
        private SerializedProperty m_MainFill, m_BooleanOperations, m_Effects;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_DisableRendering = serializedObject.FindProperty("m_DisableRendering");
            m_EdgeSoftness = serializedObject.FindProperty("m_EdgeSoftness");
            m_InternalPadding = serializedObject.FindProperty("m_InternalPadding");
            m_ShapeScale2D = serializedObject.FindProperty("m_ShapeScale2D");
            m_LinkScale = serializedObject.FindProperty("m_LinkScale");
            m_ShapePivot = serializedObject.FindProperty("m_ShapePivot");
            m_ShapeRotation = serializedObject.FindProperty("m_ShapeRotation"); // Added
            
            m_ShapeType = serializedObject.FindProperty("m_ShapeType");
            m_CornerRadius = serializedObject.FindProperty("m_CornerRadius");
            m_CornerSmoothing = serializedObject.FindProperty("m_CornerSmoothing");
            
            m_PolygonSides = serializedObject.FindProperty("m_PolygonSides");
            m_PolygonRounding = serializedObject.FindProperty("m_PolygonRounding");

            m_StarPoints = serializedObject.FindProperty("m_StarPoints");
            m_StarRatio = serializedObject.FindProperty("m_StarRatio");
            m_StarRoundingOuter = serializedObject.FindProperty("m_StarRoundingOuter");
            m_StarRoundingInner = serializedObject.FindProperty("m_StarRoundingInner");

            m_CapsuleRounding = serializedObject.FindProperty("m_CapsuleRounding");
            m_LineStart = serializedObject.FindProperty("m_LineStart");
            m_LineEnd = serializedObject.FindProperty("m_LineEnd");
            m_LineWidth = serializedObject.FindProperty("m_LineWidth");

            m_RingInnerRadius = serializedObject.FindProperty("m_RingInnerRadius");
            m_RingStartAngle = serializedObject.FindProperty("m_RingStartAngle");
            m_RingEndAngle = serializedObject.FindProperty("m_RingEndAngle");

            m_ShapePath = serializedObject.FindProperty("m_ShapePath");

            m_MainFill = serializedObject.FindProperty("MainFill");
            m_BooleanOperations = serializedObject.FindProperty("BooleanOperations");
            m_Effects = serializedObject.FindProperty("Effects");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            GUIStyle sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎨 Procedural Shape", titleStyle);
            EditorGUILayout.Space(5);
            
            // --- DISABLE RENDERING TOGGLE ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(m_DisableRendering, new GUIContent("Disable Rendering", "If checked, this shape won't be drawn but can still be used as a Cutter for other shapes."));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            // --- БЛОК 1: SHAPE ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            EditorGUILayout.LabelField("1. Geometry", sectionTitle);
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(m_ShapeType);
            
            bool isNone = (m_ShapeType.enumValueIndex == (int)ShapeType.None);

            if (!isNone)
            {
                EditorGUILayout.BeginHorizontal();
                if (m_LinkScale.boolValue)
                {
                    EditorGUI.BeginChangeCheck();
                    float newScale = EditorGUILayout.FloatField(new GUIContent("Shape Scale", "Uniform scale factor for the shape inside the RectTransform bounds."), m_ShapeScale2D.vector2Value.x);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_ShapeScale2D.vector2Value = new Vector2(newScale, newScale);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(m_ShapeScale2D, new GUIContent("Shape Scale", "Scale factor for the shape inside the RectTransform bounds."));
                }
                
                if (GUILayout.Button(m_LinkScale.boolValue ? "🔗" : "🔓", GUILayout.Width(30)))
                {
                    m_LinkScale.boolValue = !m_LinkScale.boolValue;
                    if (m_LinkScale.boolValue)
                    {
                        m_ShapeScale2D.vector2Value = new Vector2(m_ShapeScale2D.vector2Value.x, m_ShapeScale2D.vector2Value.x);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(m_ShapeRotation, new GUIContent("Shape Rotation", "Rotation of the shape geometry in degrees."));
                EditorGUILayout.PropertyField(m_ShapePivot, new GUIContent("Shape Pivot", "Geometric pivot relative to RectTransform center. (0.5, 0.5) is centered."));
                EditorGUILayout.Space(5);

                if (m_ShapeType.enumValueIndex == (int)ShapeType.Rectangle)
                {
                    EditorGUILayout.LabelField("Corner Radii", EditorStyles.boldLabel);
                    
                    Vector4 c = m_CornerRadius.vector4Value;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("TL", GUILayout.Width(20)); c.x = EditorGUILayout.FloatField(c.x);
                    GUILayout.Label("TR", GUILayout.Width(20)); c.y = EditorGUILayout.FloatField(c.y);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("BL", GUILayout.Width(20)); c.w = EditorGUILayout.FloatField(c.w);
                    GUILayout.Label("BR", GUILayout.Width(20)); c.z = EditorGUILayout.FloatField(c.z);
                    GUILayout.EndHorizontal();
                    m_CornerRadius.vector4Value = c;

                    EditorGUILayout.PropertyField(m_CornerSmoothing);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Polygon)
                {
                    EditorGUILayout.PropertyField(m_PolygonSides);
                    EditorGUILayout.PropertyField(m_PolygonRounding);
                    // Rotation removed
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Star)
                {
                    EditorGUILayout.PropertyField(m_StarPoints);
                    EditorGUILayout.PropertyField(m_StarRatio);
                    // Rotation removed
                    EditorGUILayout.PropertyField(m_StarRoundingOuter);
                    EditorGUILayout.PropertyField(m_StarRoundingInner);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Capsule)
                {
                    EditorGUILayout.PropertyField(m_CapsuleRounding);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Line)
                {
                    EditorGUILayout.PropertyField(m_LineStart);
                    EditorGUILayout.PropertyField(m_LineEnd);
                    EditorGUILayout.PropertyField(m_LineWidth);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Ring)
                {
                    EditorGUILayout.PropertyField(m_RingInnerRadius);
                    EditorGUILayout.PropertyField(m_RingStartAngle);
                    EditorGUILayout.PropertyField(m_RingEndAngle);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Path)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(m_ShapePath, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        foreach (var targetObj in targets)
                        {
                            var shape = targetObj as ProceduralShape;
                            if (shape != null)
                            {
                                shape.m_FlattenedPath.Clear();
                                shape.SetAllDirty();
                            }
                        }
                    }
                }
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(m_InternalPadding, new GUIContent("Internal Padding (Offset)"));
            EditorGUILayout.PropertyField(m_EdgeSoftness, new GUIContent("Edge Softness (AA)"));

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // --- БЛОК 2: BOOLEAN OPERATIONS ---
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("2. Boolean Operations", sectionTitle);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                m_BooleanOperations.InsertArrayElementAtIndex(m_BooleanOperations.arraySize);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            for (int i = 0; i < m_BooleanOperations.arraySize; i++)
            {
                SerializedProperty item = m_BooleanOperations.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Header Row with Reorder Buttons
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Op {i + 1}", EditorStyles.boldLabel, GUILayout.Width(40));
                
                GUILayout.FlexibleSpace();
                
                // UP Button
                EditorGUI.BeginDisabledGroup(i == 0);
                if (GUILayout.Button("▲", GUILayout.Width(20)))
                {
                    m_BooleanOperations.MoveArrayElement(i, i - 1);
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break; 
                }
                EditorGUI.EndDisabledGroup();

                // DOWN Button
                EditorGUI.BeginDisabledGroup(i == m_BooleanOperations.arraySize - 1);
                if (GUILayout.Button("▼", GUILayout.Width(20)))
                {
                    m_BooleanOperations.MoveArrayElement(i, i + 1);
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break; 
                }
                EditorGUI.EndDisabledGroup();

                // REMOVE Button
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    m_BooleanOperations.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break; 
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(item.FindPropertyRelative("Operation"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("SourceShape"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("Smoothness"));
                
                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
            }
            
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // --- БЛОК 3: FILL ---
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            EditorGUILayout.LabelField("3. Fill & Color", sectionTitle);
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(m_MainFill, true);
            
            GUILayout.Space(5);
            RaycastControlsGUI(); 
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // --- БЛОК 4: EFFECTS ---
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("4. Effects", sectionTitle);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Drop Shadow"), false, () => AddEffect(new DropShadowEffect()));
                menu.AddItem(new GUIContent("Inner Shadow"), false, () => AddEffect(new InnerShadowEffect()));
                menu.AddItem(new GUIContent("Outer Glow"), false, () => AddEffect(new OuterGlowEffect()));
                menu.AddItem(new GUIContent("Inner Glow"), false, () => AddEffect(new InnerGlowEffect()));
                menu.AddItem(new GUIContent("Stroke"), false, () => AddEffect(new StrokeEffect()));
                menu.AddItem(new GUIContent("Blur"), false, () => AddEffect(new BlurEffect()));
                menu.AddItem(new GUIContent("Bevel (Fake 3D)"), false, () => AddEffect(new BevelEffect()));
                menu.ShowAsContext();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            for (int i = 0; i < m_Effects.arraySize; i++)
            {
                if (DrawEffectItem(m_Effects, i)) break;
            }
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // --- БЛОК 5: TOOLS ---
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("5. Tools", sectionTitle);
            if (GUILayout.Button("Bake to PolygonCollider2D"))
            {
                BakeToCollider((ProceduralShape)target);
            }
            EditorGUILayout.EndVertical();

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets) ((ProceduralShape)t).SetAllDirty();
            }
        }

        private void BakeToCollider(ProceduralShape shape)
        {
            PolygonCollider2D collider = shape.GetComponent<PolygonCollider2D>();
            if (collider == null) collider = shape.gameObject.AddComponent<PolygonCollider2D>();

            Undo.RecordObject(collider, "Bake SDF to Collider");

            int segments = 32;
            if (shape.m_ShapeType == ShapeType.Polygon) segments = shape.m_PolygonSides;
            else if (shape.m_ShapeType == ShapeType.Star) segments = shape.m_StarPoints * 2;

            Vector2[] points = new Vector2[segments];
            Rect rect = shape.rectTransform.rect;
            float hw = rect.width * 0.5f * shape.ShapeScale.x;
            float hh = rect.height * 0.5f * shape.ShapeScale.y;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i * angleStep + shape.ShapeRotation) * Mathf.Deg2Rad;
                float r = 1f;
                if (shape.m_ShapeType == ShapeType.Star && (i % 2 != 0)) r = shape.m_StarRatio;

                points[i] = new Vector2(Mathf.Sin(angle) * hw * r, Mathf.Cos(angle) * hh * r) + shape.GetGeometricCenterOffset();
            }

            collider.points = points;
            EditorUtility.SetDirty(collider);
        }

        private void OnSceneGUI()
        {
            ProceduralShape shape = (ProceduralShape)target;
            if (shape == null) return;

            RectTransform rt = shape.rectTransform;
            Vector2 size = rt.rect.size;
            Vector2 pivotOffset = shape.GetGeometricCenterOffset();
            float hw = size.x * 0.5f * shape.ShapeScale.x;
            float hh = size.y * 0.5f * shape.ShapeScale.y;
            
            Handles.color = Color.cyan;
            EditorGUI.BeginChangeCheck();

            // Матрица трансформации: от координат фигуры к мировому пространству
            Matrix4x4 geomMatrix = rt.localToWorldMatrix * Matrix4x4.Translate((Vector3)rt.rect.center + (Vector3)pivotOffset) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, -shape.ShapeRotation));

            using (new Handles.DrawingScope(geomMatrix))
            {
                if (shape.m_ShapeType == ShapeType.Rectangle)
                {
                    Vector4 radii = shape.m_CornerRadius;
                    float handleSize = 0.05f;
                    
                    Vector3 trCorner = new Vector3(hw, hh, 0);
                    Vector3 trPos = trCorner + new Vector3(-1, -1, 0).normalized * radii.y;
                    trPos = Handles.FreeMoveHandle(trPos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(trPos)) * handleSize, Vector3.zero, Handles.SphereHandleCap);
                    float newTr = Mathf.Clamp(Vector3.Distance(trCorner, trPos), 0, Mathf.Min(hw, hh));
                    
                    Vector3 tlCorner = new Vector3(-hw, hh, 0);
                    Vector3 tlPos = tlCorner + new Vector3(1, -1, 0).normalized * radii.x;
                    tlPos = Handles.FreeMoveHandle(tlPos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(tlPos)) * handleSize, Vector3.zero, Handles.SphereHandleCap);
                    float newTl = Mathf.Clamp(Vector3.Distance(tlCorner, tlPos), 0, Mathf.Min(hw, hh));

                    Vector3 brCorner = new Vector3(hw, -hh, 0);
                    Vector3 brPos = brCorner + new Vector3(-1, 1, 0).normalized * radii.z;
                    brPos = Handles.FreeMoveHandle(brPos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(brPos)) * handleSize, Vector3.zero, Handles.SphereHandleCap);
                    float newBr = Mathf.Clamp(Vector3.Distance(brCorner, brPos), 0, Mathf.Min(hw, hh));

                    Vector3 blCorner = new Vector3(-hw, -hh, 0);
                    Vector3 blPos = blCorner + new Vector3(1, 1, 0).normalized * radii.w;
                    blPos = Handles.FreeMoveHandle(blPos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(blPos)) * handleSize, Vector3.zero, Handles.SphereHandleCap);
                    float newBl = Mathf.Clamp(Vector3.Distance(blCorner, blPos), 0, Mathf.Min(hw, hh));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(shape, "Change Corner Radii");
                        shape.m_CornerRadius = new Vector4(newTl, newTr, newBr, newBl);
                        shape.SetAllDirty();
                    }
                }
                else if (shape.m_ShapeType == ShapeType.Line)
                {
                    // Для линии используем geomMatrix.inverse, так как точки линии заданы в локальном пространстве RectTransform
                    Matrix4x4 rtToGeom = geomMatrix.inverse * rt.localToWorldMatrix;
                    Vector3 start = rtToGeom.MultiplyPoint3x4(shape.m_LineStart);
                    Vector3 end = rtToGeom.MultiplyPoint3x4(shape.m_LineEnd);
                    
                    start = Handles.FreeMoveHandle(start, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(start)) * 0.1f, Vector3.zero, Handles.CircleHandleCap);
                    end = Handles.FreeMoveHandle(end, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(end)) * 0.1f, Vector3.zero, Handles.CircleHandleCap);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(shape, "Move Line Points");
                        shape.m_LineStart = rt.worldToLocalMatrix.MultiplyPoint3x4(geomMatrix.MultiplyPoint3x4(start));
                        shape.m_LineEnd = rt.worldToLocalMatrix.MultiplyPoint3x4(geomMatrix.MultiplyPoint3x4(end));
                        shape.SetAllDirty();
                    }
                }
                else if (shape.m_ShapeType == ShapeType.Star)
                {
                    float dist = (size.x * 0.5f) * shape.m_StarRatio * shape.ShapeScale.x;
                    Vector3 handlePos = new Vector3(dist, 0, 0);
                    handlePos = Handles.FreeMoveHandle(handlePos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(handlePos)) * 0.05f, Vector3.zero, Handles.DotHandleCap);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(shape, "Change Star Ratio");
                        shape.m_StarRatio = Mathf.Clamp01(handlePos.magnitude / (size.x * 0.5f * shape.ShapeScale.x));
                        shape.SetAllDirty();
                    }
                }
                else if (shape.m_ShapeType == ShapeType.Ring)
                {
                    float dist = (size.x * 0.5f) * shape.m_RingInnerRadius * shape.ShapeScale.x;
                    Vector3 handlePos = new Vector3(0, dist, 0);
                    handlePos = Handles.FreeMoveHandle(handlePos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(handlePos)) * 0.05f, Vector3.zero, Handles.DotHandleCap);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(shape, "Change Ring Inner Radius");
                        shape.m_RingInnerRadius = Mathf.Clamp01(handlePos.magnitude / (size.x * 0.5f * shape.ShapeScale.x));
                        shape.SetAllDirty();
                    }
                }
                else if (shape.m_ShapeType == ShapeType.Path)
                {
                    var path = shape.m_ShapePath;
                    if (path != null && path.Points != null)
                    {
                        Handles.color = Color.green;
                        for (int i = 0; i < path.Points.Count; i++)
                        {
                            var pt = path.Points[i];
                            Vector3 pos = pt.Position;
                            
                            EditorGUI.BeginChangeCheck();
                            Vector3 newPos = Handles.FreeMoveHandle(pos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(pos)) * 0.08f, Vector3.zero, Handles.CircleHandleCap);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(shape, "Move Path Point");
                                Vector2 delta = (Vector2)newPos - pt.Position;
                                pt.Position = newPos;
                                pt.ControlPoint1 += delta;
                                pt.ControlPoint2 += delta;
                                path.Points[i] = pt;
                                shape.m_FlattenedPath.Clear();
                                shape.SetAllDirty();
                            }

                            if (pt.Type == PathPointType.Bezier)
                            {
                                Handles.color = Color.yellow;
                                Vector3 cp1 = pt.ControlPoint1;
                                EditorGUI.BeginChangeCheck();
                                Vector3 newCp1 = Handles.FreeMoveHandle(cp1, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(cp1)) * 0.06f, Vector3.zero, Handles.DotHandleCap);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(shape, "Move Control Point");
                                    pt.ControlPoint1 = newCp1;
                                    path.Points[i] = pt;
                                    shape.m_FlattenedPath.Clear();
                                    shape.SetAllDirty();
                                }
                                Handles.DrawDottedLine(pos, newCp1, 2f);

                                Vector3 cp2 = pt.ControlPoint2;
                                EditorGUI.BeginChangeCheck();
                                Vector3 newCp2 = Handles.FreeMoveHandle(cp2, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(cp2)) * 0.06f, Vector3.zero, Handles.DotHandleCap);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(shape, "Move Control Point");
                                    pt.ControlPoint2 = newCp2;
                                    path.Points[i] = pt;
                                    shape.m_FlattenedPath.Clear();
                                    shape.SetAllDirty();
                                }
                                Handles.DrawDottedLine(pos, newCp2, 2f);
                                Handles.color = Color.green;
                            }

                            if (i < path.Points.Count - 1 || path.Closed)
                            {
                                int nextIdx = (i + 1) % path.Points.Count;
                                var nextPt = path.Points[nextIdx];
                                if (nextPt.Type == PathPointType.Line)
                                {
                                    Handles.DrawLine(pos, nextPt.Position);
                                }
                                else
                                {
                                    Handles.DrawBezier(pos, nextPt.Position, pt.ControlPoint2, nextPt.ControlPoint1, Color.green, null, 2f);
                                }
                            }
                        }
                    }
                }

                // Gradient Controls
                if (shape.MainFill.Type == FillType.LinearGradient || shape.MainFill.Type == FillType.RadialGradient || shape.MainFill.Type == FillType.AngularGradient)
                {
                    Handles.color = Color.yellow;
                    Vector2 offset = shape.MainFill.GradientOffset;
                    Vector3 center = new Vector3(offset.x * hw, offset.y * hh, 0);
                    
                    EditorGUI.BeginChangeCheck();
                    Vector3 newCenter = Handles.FreeMoveHandle(center, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(center)) * 0.08f, Vector3.zero, Handles.CircleHandleCap);
                    
                    float angleRad = shape.MainFill.GradientAngle * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
                    float scale = shape.MainFill.GradientScale * Mathf.Max(hw, hh);
                    
                    Vector3 anglePos = newCenter + dir * scale;
                    Vector3 newAnglePos = Handles.FreeMoveHandle(anglePos, HandleUtility.GetHandleSize(geomMatrix.MultiplyPoint3x4(anglePos)) * 0.08f, Vector3.zero, Handles.DotHandleCap);
                    Handles.DrawDottedLine(newCenter, newAnglePos, 2f);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(shape, "Change Gradient");
                        shape.MainFill.GradientOffset = new Vector2(newCenter.x / (hw == 0 ? 1 : hw), newCenter.y / (hh == 0 ? 1 : hh));
                        
                        Vector3 localDir = newAnglePos - newCenter;
                        shape.MainFill.GradientScale = Mathf.Max(0.01f, localDir.magnitude / Mathf.Max(hw, hh));
                        shape.MainFill.GradientAngle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
                        
                        shape.SetAllDirty();
                    }
                }
            }
        }

        private bool DrawEffectItem(SerializedProperty listProp, int index)
        {
            SerializedProperty effectProp = listProp.GetArrayElementAtIndex(index);
            SerializedProperty enabledProp = effectProp.FindPropertyRelative("Enabled");
            
            string rawName = effectProp.managedReferenceFullTypename;
            string effectName = rawName.Contains("DropShadow") ? "Drop Shadow" : 
                                rawName.Contains("InnerShadow") ? "Inner Shadow" : 
                                rawName.Contains("OuterGlow") ? "Outer Glow" :
                                rawName.Contains("Stroke") ? "Stroke" : "Blur";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
            effectProp.isExpanded = EditorGUILayout.Foldout(effectProp.isExpanded, effectName, true);
            
            GUILayout.FlexibleSpace();

            // UP Button
            EditorGUI.BeginDisabledGroup(index == 0);
            if (GUILayout.Button("▲", GUILayout.Width(20)))
            {
                listProp.MoveArrayElement(index, index - 1);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUI.EndDisabledGroup();

            // DOWN Button
            EditorGUI.BeginDisabledGroup(index == listProp.arraySize - 1);
            if (GUILayout.Button("▼", GUILayout.Width(20)))
            {
                listProp.MoveArrayElement(index, index + 1);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUI.EndDisabledGroup();

            // REMOVE Button
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                listProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            if (effectProp.isExpanded && enabledProp.boolValue)
            {
                GUILayout.Space(5);
                SerializedProperty child = effectProp.Copy();
                SerializedProperty end = effectProp.GetEndProperty();
                
                if (child.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        if (child.name == "Enabled" || child.name == "Color") continue;
                        
                        if (child.name == "Fill") 
                        {
                            EditorGUILayout.LabelField("Effect Color", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(child, true);
                            GUILayout.Space(5);
                        }
                        else 
                        {
                            EditorGUILayout.PropertyField(child, true);
                        }
                    } while (child.NextVisible(false));
                }
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
            return false;
        }

        private void AddEffect(ProceduralEffect effect)
        {
            ProceduralShape shape = (ProceduralShape)target;
            Undo.RecordObject(shape, "Add Effect");
            shape.Effects.Add(effect);
            EditorUtility.SetDirty(shape);
            shape.SetAllDirty();
        }
    }
}
#endif