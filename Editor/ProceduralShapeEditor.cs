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
        private SerializedProperty m_ShapeScale;
        private SerializedProperty m_ShapePivot;
        private SerializedProperty m_ShapeType, m_CornerRadius, m_CornerSmoothing;
        private SerializedProperty m_PolygonSides, m_PolygonRounding, m_PolygonRotation;
        private SerializedProperty m_StarPoints, m_StarRatio, m_StarRoundingOuter, m_StarRoundingInner, m_StarRotation;
        private SerializedProperty m_MainFill, m_BooleanOperations, m_Effects;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_DisableRendering = serializedObject.FindProperty("m_DisableRendering");
            m_EdgeSoftness = serializedObject.FindProperty("m_EdgeSoftness");
            m_ShapeScale = serializedObject.FindProperty("m_ShapeScale");
            m_ShapePivot = serializedObject.FindProperty("m_ShapePivot");
            m_ShapeType = serializedObject.FindProperty("m_ShapeType");
            m_CornerRadius = serializedObject.FindProperty("m_CornerRadius");
            m_CornerSmoothing = serializedObject.FindProperty("m_CornerSmoothing");
            
            m_PolygonSides = serializedObject.FindProperty("m_PolygonSides");
            m_PolygonRounding = serializedObject.FindProperty("m_PolygonRounding");
            m_PolygonRotation = serializedObject.FindProperty("m_PolygonRotation");

            m_StarPoints = serializedObject.FindProperty("m_StarPoints");
            m_StarRatio = serializedObject.FindProperty("m_StarRatio");
            m_StarRoundingOuter = serializedObject.FindProperty("m_StarRoundingOuter");
            m_StarRoundingInner = serializedObject.FindProperty("m_StarRoundingInner");
            m_StarRotation = serializedObject.FindProperty("m_StarRotation");

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
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(m_DisableRendering, new GUIContent("Disable Rendering", "If checked, this shape won't be drawn but can still be used as a Cutter for other shapes."));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5);
            EditorGUILayout.LabelField("1. Geometry", sectionTitle);
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(m_ShapeType);
            
            bool isNone = (m_ShapeType.enumValueIndex == (int)ShapeType.None);

            if (!isNone)
            {
                EditorGUILayout.PropertyField(m_ShapeScale, new GUIContent("Shape Scale", "Uniform scale factor for the shape inside the RectTransform bounds."));
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
                    EditorGUILayout.PropertyField(m_PolygonRotation);
                }
                else if (m_ShapeType.enumValueIndex == (int)ShapeType.Star)
                {
                    EditorGUILayout.PropertyField(m_StarPoints);
                    EditorGUILayout.PropertyField(m_StarRatio);
                    EditorGUILayout.PropertyField(m_StarRotation);
                    EditorGUILayout.PropertyField(m_StarRoundingOuter);
                    EditorGUILayout.PropertyField(m_StarRoundingInner);
                }
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(m_EdgeSoftness, new GUIContent("Edge Softness (AA)"));

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();

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
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Op {i + 1}", EditorStyles.boldLabel, GUILayout.Width(40));
                
                GUILayout.FlexibleSpace();
                
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
                menu.AddItem(new GUIContent("Stroke"), false, () => AddEffect(new StrokeEffect()));
                menu.AddItem(new GUIContent("Blur"), false, () => AddEffect(new BlurEffect()));
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

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets) ((ProceduralShape)t).SetAllDirty();
            }
        }

        private bool DrawEffectItem(SerializedProperty listProp, int index)
        {
            SerializedProperty effectProp = listProp.GetArrayElementAtIndex(index);
            SerializedProperty enabledProp = effectProp.FindPropertyRelative("Enabled");
            
            string rawName = effectProp.managedReferenceFullTypename;
            string effectName = rawName.Contains("DropShadow") ? "Drop Shadow" : 
                                rawName.Contains("InnerShadow") ? "Inner Shadow" : 
                                rawName.Contains("Stroke") ? "Stroke" : "Blur";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20));
            effectProp.isExpanded = EditorGUILayout.Foldout(effectProp.isExpanded, effectName, true);
            
            GUILayout.FlexibleSpace();

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