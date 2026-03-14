#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ProceduralShapes.Runtime;
using System.Collections.Generic;
using System;

namespace ProceduralShapes.Editor
{
    [CustomEditor(typeof(ProceduralShape))]
    [CanEditMultipleObjects]
    public class ProceduralShapeEditor : UnityEditor.UI.GraphicEditor
    {
        private VisualTreeAsset m_VisualTree;
        private StyleSheet m_StyleSheet;

        private VisualElement m_BooleanListContainer;
        private VisualElement m_EffectsListContainer;
        private VisualElement m_SpecificContainer;

        private SerializedProperty m_DisableRendering;
        private SerializedProperty m_EdgeSoftness, m_InternalPadding;
        private SerializedProperty m_ShapeScale2D, m_LinkScale, m_ShapePivot, m_ShapeType;
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
            m_ShapeType = serializedObject.FindProperty("m_ShapeType");
            m_MainFill = serializedObject.FindProperty("MainFill");
            m_BooleanOperations = serializedObject.FindProperty("BooleanOperations");
            m_Effects = serializedObject.FindProperty("Effects");

            m_VisualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_ProjectContent/GitPlugins/ProceduralShapes/Editor/UI/ProceduralShapeEditor.uxml");
            m_StyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_ProjectContent/GitPlugins/ProceduralShapes/Editor/UI/ProceduralShapeEditorStyle.uss");
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (m_VisualTree == null) return base.CreateInspectorGUI();

            VisualElement root = new VisualElement();
            m_VisualTree.CloneTree(root);
            if (m_StyleSheet != null) root.styleSheets.Add(m_StyleSheet);

            m_BooleanListContainer = root.Q<VisualElement>("boolean-list-container");
            m_EffectsListContainer = root.Q<VisualElement>("effects-list-container");
            m_SpecificContainer = root.Q<VisualElement>("shape-specific-params");

            // 1. Link Scale Logic
            Button linkButton = root.Q<Button>("link-scale-button");
            UpdateLinkScaleIcon(linkButton);
            linkButton.clicked += () => {
                m_LinkScale.boolValue = !m_LinkScale.boolValue;
                if (m_LinkScale.boolValue)
                {
                    m_ShapeScale2D.vector2Value = new Vector2(m_ShapeScale2D.vector2Value.x, m_ShapeScale2D.vector2Value.x);
                }
                serializedObject.ApplyModifiedProperties();
                UpdateLinkScaleIcon(linkButton);
            };

            PropertyField scaleField = root.Q<PropertyField>("scale-field");
            scaleField.RegisterValueChangeCallback(evt => {
                if (m_LinkScale.boolValue)
                {
                    serializedObject.Update(); // Ensure we have latest
                    Vector2 val = m_ShapeScale2D.vector2Value;
                    if (Mathf.Abs(val.x - val.y) > 0.001f)
                    {
                        m_ShapeScale2D.vector2Value = new Vector2(val.x, val.x);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            });

            // 2. Shape Specific Params
            root.TrackPropertyValue(m_ShapeType, (prop) => RefreshShapeSpecificParams());
            RefreshShapeSpecificParams();

            // 3. Lists
            RefreshBooleanList();
            root.Q<Button>("add-boolean-button").clicked += () => {
                m_BooleanOperations.InsertArrayElementAtIndex(m_BooleanOperations.arraySize);
                serializedObject.ApplyModifiedProperties();
                RefreshBooleanList();
            };

            RefreshEffectsList();
            root.Q<Button>("add-effect-button").clicked += ShowEffectMenu;

            // 4. Tools
            root.Q<Button>("bake-collider-button").clicked += () => BakeToCollider((ProceduralShape)target);

            // 5. Raycast Controls
            root.Q<VisualElement>("raycast-controls").Add(new IMGUIContainer(() => RaycastControlsGUI()));

            // CRITICAL: Bind the root to the serialized object
            root.Bind(serializedObject);

            return root;
        }

        private void UpdateLinkScaleIcon(Button btn)
        {
            btn.text = m_LinkScale.boolValue ? "🔗" : "🔓";
            btn.style.color = m_LinkScale.boolValue ? new Color(0.3f, 0.6f, 1f) : Color.white;
        }

        private void RefreshShapeSpecificParams()
        {
            if (m_SpecificContainer == null) return;
            m_SpecificContainer.Clear();
            
            serializedObject.Update();
            ShapeType type = (ShapeType)m_ShapeType.enumValueIndex;
            if (type == ShapeType.None) return;

            switch (type)
            {
                case ShapeType.Rectangle:
                    m_SpecificContainer.Add(new Label("Corner Radii") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
                    VisualElement grid = new VisualElement() { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, justifyContent = Justify.SpaceBetween } };
                    
                    SerializedProperty radiusProp = serializedObject.FindProperty("m_CornerRadius");
                    grid.Add(CreateCompactFieldFromProp(radiusProp.FindPropertyRelative("x"), "TL"));
                    grid.Add(CreateCompactFieldFromProp(radiusProp.FindPropertyRelative("y"), "TR"));
                    grid.Add(CreateCompactFieldFromProp(radiusProp.FindPropertyRelative("w"), "BL"));
                    grid.Add(CreateCompactFieldFromProp(radiusProp.FindPropertyRelative("z"), "BR"));
                    
                    m_SpecificContainer.Add(grid);
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_CornerSmoothing"), "Smoothing"));
                    break;
                case ShapeType.Polygon:
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_PolygonSides"), "Sides"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_PolygonRounding"), "Rounding"));
                    break;
                case ShapeType.Star:
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_StarPoints"), "Points"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_StarRatio"), "Inner Ratio"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_StarRoundingOuter"), "Outer Rounding"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_StarRoundingInner"), "Inner Rounding"));
                    break;
                case ShapeType.Capsule:
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_CapsuleRounding"), "Rounding"));
                    break;
                case ShapeType.Line:
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_LineStart"), "Start Point"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_LineEnd"), "End Point"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_LineWidth"), "Width"));
                    break;
                case ShapeType.Ring:
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_RingInnerRadius"), "Inner Radius"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_RingStartAngle"), "Start Angle"));
                    m_SpecificContainer.Add(new PropertyField(serializedObject.FindProperty("m_RingEndAngle"), "End Angle"));
                    break;
                case ShapeType.Path:
                    PropertyField pathField = new PropertyField(serializedObject.FindProperty("m_ShapePath"), "Vector Path");
                    pathField.RegisterValueChangeCallback(evt => {
                        foreach (var targetObj in targets) {
                            var shape = targetObj as ProceduralShape;
                            if (shape != null) { shape.m_FlattenedPath.Clear(); shape.SetAllDirty(); }
                        }
                    });
                    m_SpecificContainer.Add(pathField);
                    break;
                default:
                    m_SpecificContainer.Add(new Label("No specific parameters for this shape type.") { 
                        style = { opacity = 0.5f, unityFontStyleAndWeight = FontStyle.Italic, marginTop = 4, alignSelf = Align.Center } 
                    });
                    break;
            }
            
            // Re-bind specific container to ensure new fields are connected
            m_SpecificContainer.Bind(serializedObject);
        }

        private VisualElement CreateCompactFieldFromProp(SerializedProperty prop, string label)
        {
            VisualElement row = new VisualElement() { style = { flexDirection = FlexDirection.Row, width = Length.Percent(48), marginBottom = 4 } };
            row.Add(new Label(label) { style = { width = 25, unityTextAlign = TextAnchor.MiddleLeft, fontSize = 10, opacity = 0.8f } });
            PropertyField field = new PropertyField(prop, "");
            field.style.flexGrow = 1;
            row.Add(field);
            return row;
        }

        private void RefreshBooleanList()
        {
            if (m_BooleanListContainer == null) return;
            m_BooleanListContainer.Clear();
            serializedObject.Update();

            for (int i = 0; i < m_BooleanOperations.arraySize; i++)
            {
                int index = i;
                SerializedProperty item = m_BooleanOperations.GetArrayElementAtIndex(i);
                
                VisualElement itemRoot = new VisualElement() { name = "bool-item-" + i };
                itemRoot.AddToClassList("ps-list-item");

                VisualElement header = new VisualElement() { name = "header" };
                header.AddToClassList("ps-list-item-header");
                header.Add(new Label($"Op {i + 1}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

                header.Add(CreateIconButton("▲", () => MoveListElement(m_BooleanOperations, index, -1, RefreshBooleanList), index > 0));
                header.Add(CreateIconButton("▼", () => MoveListElement(m_BooleanOperations, index, 1, RefreshBooleanList), index < m_BooleanOperations.arraySize - 1));
                header.Add(CreateIconButton("✕", () => {
                    m_BooleanOperations.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    RefreshBooleanList();
                }));

                itemRoot.Add(header);

                VisualElement content = new VisualElement() { name = "content" };
                content.AddToClassList("ps-list-item-content");
                content.Add(new PropertyField(item.FindPropertyRelative("Operation")));
                content.Add(new PropertyField(item.FindPropertyRelative("SourceShape")));
                content.Add(new PropertyField(item.FindPropertyRelative("Smoothness")));
                itemRoot.Add(content);

                m_BooleanListContainer.Add(itemRoot);
            }
            
            m_BooleanListContainer.Bind(serializedObject);
        }

        private void RefreshEffectsList()
        {
            if (m_EffectsListContainer == null) return;
            m_EffectsListContainer.Clear();
            serializedObject.Update();

            for (int i = 0; i < m_Effects.arraySize; i++)
            {
                int index = i;
                SerializedProperty effectProp = m_Effects.GetArrayElementAtIndex(i);
                SerializedProperty enabledProp = effectProp.FindPropertyRelative("Enabled");

                string effectName = GetEffectDisplayName(effectProp.managedReferenceFullTypename);

                VisualElement itemRoot = new VisualElement();
                itemRoot.AddToClassList("ps-list-item");

                VisualElement header = new VisualElement();
                header.AddToClassList("ps-list-item-header");

                Toggle enabledToggle = new Toggle() { value = enabledProp.boolValue, style = { marginRight = 4 } };
                enabledToggle.RegisterValueChangedCallback(evt => { 
                    enabledProp.boolValue = evt.newValue; 
                    serializedObject.ApplyModifiedProperties(); 
                    ((ProceduralShape)target).SetAllDirty();
                });
                header.Add(enabledToggle);

                header.Add(new Label(effectName) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

                header.Add(CreateIconButton("▲", () => MoveListElement(m_Effects, index, -1, RefreshEffectsList), index > 0));
                header.Add(CreateIconButton("▼", () => MoveListElement(m_Effects, index, 1, RefreshEffectsList), index < m_Effects.arraySize - 1));
                header.Add(CreateIconButton("✕", () => {
                    m_Effects.DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                    RefreshEffectsList();
                }));

                itemRoot.Add(header);

                VisualElement content = new VisualElement();
                content.AddToClassList("ps-list-item-content");
                
                SerializedProperty child = effectProp.Copy();
                SerializedProperty end = effectProp.GetEndProperty();
                if (child.NextVisible(true))
                {
                    do {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        if (child.name == "Enabled") continue;
                        if (child.name == "Fill") {
                            content.Add(new Label("Effect Fill") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });
                            content.Add(new PropertyField(child));
                        } else {
                            content.Add(new PropertyField(child));
                        }
                    } while (child.NextVisible(false));
                }

                itemRoot.Add(content);
                m_EffectsListContainer.Add(itemRoot);
            }
            
            m_EffectsListContainer.Bind(serializedObject);
        }

        private string GetEffectDisplayName(string typeName)
        {
            if (typeName.Contains("DropShadow")) return "Drop Shadow";
            if (typeName.Contains("InnerShadow")) return "Inner Shadow";
            if (typeName.Contains("OuterGlow")) return "Outer Glow";
            if (typeName.Contains("InnerGlow")) return "Inner Glow";
            if (typeName.Contains("Stroke")) return "Stroke";
            if (typeName.Contains("Blur")) return "Blur";
            if (typeName.Contains("Bevel")) return "Bevel (3D)";
            return "Effect";
        }

        private VisualElement CreateIconButton(string text, Action onClick, bool enabled = true)
        {
            Button btn = new Button(onClick) { text = text };
            btn.AddToClassList("ps-icon-button");
            btn.SetEnabled(enabled);
            return btn;
        }

        private void MoveListElement(SerializedProperty list, int index, int dir, Action onDone)
        {
            list.MoveArrayElement(index, index + dir);
            serializedObject.ApplyModifiedProperties();
            onDone?.Invoke();
        }

        private void ShowEffectMenu()
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

        private void AddEffect(ProceduralEffect effect)
        {
            ProceduralShape shape = (ProceduralShape)target;
            Undo.RecordObject(shape, "Add Effect");
            shape.Effects.Add(effect);
            serializedObject.Update();
            EditorUtility.SetDirty(shape);
            shape.SetAllDirty();
            RefreshEffectsList();
        }

        // --- SCENE VIEW HANDLES ---

        private void OnSceneGUI()
        {
            ProceduralShape shape = (ProceduralShape)target;
            if (shape == null) return;

            RectTransform rt = shape.rectTransform;
            Vector2 size = rt.rect.size;
            Vector2 pivotOffset = shape.GetGeometricCenterOffset();
            float hw = size.x * 0.5f * shape.ShapeScale.x;
            float hh = size.y * 0.5f * shape.ShapeScale.y;
            
            Matrix4x4 geomMatrix = rt.localToWorldMatrix * Matrix4x4.Translate((Vector3)rt.rect.center + (Vector3)pivotOffset);

            using (new Handles.DrawingScope(geomMatrix))
            {
                if (shape.m_ShapeType == ShapeType.Rectangle)
                {
                    DrawRectangleHandles(shape, hw, hh);
                }
                else if (shape.m_ShapeType == ShapeType.Line)
                {
                    DrawLineHandles(shape, rt, geomMatrix);
                }
                else if (shape.m_ShapeType == ShapeType.Star)
                {
                    DrawStarHandles(shape, size);
                }
                else if (shape.m_ShapeType == ShapeType.Ring)
                {
                    DrawRingHandles(shape, size);
                }
                else if (shape.m_ShapeType == ShapeType.Path)
                {
                    DrawPathHandles(shape, geomMatrix);
                }

                DrawGradientHandles(shape, hw, hh);
            }
        }

        private void DrawRectangleHandles(ProceduralShape shape, float hw, float hh)
        {
            Vector4 radii = shape.m_CornerRadius;
            EditorGUI.BeginChangeCheck();

            Handles.color = Color.cyan;
            radii.y = ModernHandle(new Vector3(hw, hh, 0), new Vector3(-1, -1, 0), radii.y, "TR", hw, hh, Color.cyan);
            radii.x = ModernHandle(new Vector3(-hw, hh, 0), new Vector3(1, -1, 0), radii.x, "TL", hw, hh, Color.cyan);
            radii.z = ModernHandle(new Vector3(hw, -hh, 0), new Vector3(-1, 1, 0), radii.z, "BR", hw, hh, Color.cyan);
            radii.w = ModernHandle(new Vector3(-hw, -hh, 0), new Vector3(1, 1, 0), radii.w, "BL", hw, hh, Color.cyan);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Change Corner Radii");
                shape.m_CornerRadius = radii;
                shape.SetAllDirty();
            }
        }

        private float ModernHandle(Vector3 corner, Vector3 dir, float radius, string label, float hw, float hh, Color color)
        {
            Vector3 pos = corner + dir.normalized * radius;
            float size = HandleUtility.GetHandleSize(pos) * 0.05f;
            
            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
            Handles.DrawDottedLine(corner, pos, 2f);
            
            Handles.color = color;
            Vector3 newPos = Handles.FreeMoveHandle(pos, size, Vector3.zero, ModernHandleCap);
            
            float newRadius = Mathf.Clamp(Vector3.Distance(corner, newPos), 0, Mathf.Min(hw, hh));
            
            if (newRadius > 5f) {
                GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } };
                Handles.Label(pos + dir * 10f, $"{label}: {newRadius:F0}", style);
            }
            
            return newRadius;
        }

        private void DrawLineHandles(ProceduralShape shape, RectTransform rt, Matrix4x4 geomMatrix)
        {
            Matrix4x4 rtToGeom = geomMatrix.inverse * rt.localToWorldMatrix;
            Vector3 start = rtToGeom.MultiplyPoint3x4(shape.m_LineStart);
            Vector3 end = rtToGeom.MultiplyPoint3x4(shape.m_LineEnd);
            
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.white;
            start = Handles.FreeMoveHandle(start, HandleUtility.GetHandleSize(start) * 0.08f, Vector3.zero, ModernHandleCap);
            end = Handles.FreeMoveHandle(end, HandleUtility.GetHandleSize(end) * 0.08f, Vector3.zero, ModernHandleCap);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Move Line Points");
                shape.m_LineStart = rt.worldToLocalMatrix.MultiplyPoint3x4(geomMatrix.MultiplyPoint3x4(start));
                shape.m_LineEnd = rt.worldToLocalMatrix.MultiplyPoint3x4(geomMatrix.MultiplyPoint3x4(end));
                shape.SetAllDirty();
            }
        }

        private void DrawStarHandles(ProceduralShape shape, Vector2 size)
        {
            float dist = (size.x * 0.5f) * shape.m_StarRatio * shape.ShapeScale.x;
            Vector3 handlePos = new Vector3(dist, 0, 0);
            
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(1f, 0.5f, 0f); // Orange for ratio
            handlePos = Handles.FreeMoveHandle(handlePos, HandleUtility.GetHandleSize(handlePos) * 0.08f, Vector3.zero, ModernHandleCap);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Change Star Ratio");
                shape.m_StarRatio = Mathf.Clamp01(handlePos.magnitude / (size.x * 0.5f * shape.ShapeScale.x));
                shape.SetAllDirty();
            }
        }

        private void DrawRingHandles(ProceduralShape shape, Vector2 size)
        {
            float dist = (size.x * 0.5f) * shape.m_RingInnerRadius * shape.ShapeScale.x;
            Vector3 handlePos = new Vector3(0, dist, 0);
            
            EditorGUI.BeginChangeCheck();
            Handles.color = Color.yellow;
            handlePos = Handles.FreeMoveHandle(handlePos, HandleUtility.GetHandleSize(handlePos) * 0.08f, Vector3.zero, ModernHandleCap);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Change Ring Inner Radius");
                shape.m_RingInnerRadius = Mathf.Clamp01(handlePos.magnitude / (size.x * 0.5f * shape.ShapeScale.x));
                shape.SetAllDirty();
            }
        }

        private void DrawPathHandles(ProceduralShape shape, Matrix4x4 geomMatrix)
        {
            var path = shape.m_ShapePath;
            if (path == null || path.Points == null) return;

            for (int i = 0; i < path.Points.Count; i++)
            {
                var pt = path.Points[i];
                Vector3 pos = pt.Position;
                
                EditorGUI.BeginChangeCheck();
                Handles.color = Color.green;
                Vector3 newPos = Handles.FreeMoveHandle(pos, HandleUtility.GetHandleSize(pos) * 0.08f, Vector3.zero, ModernHandleCap);
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
                    Handles.color = new Color(1, 0.9f, 0);
                    DrawControlPoint(ref pt.ControlPoint1, pos, shape, path, i, new Color(1, 0.8f, 0));
                    DrawControlPoint(ref pt.ControlPoint2, pos, shape, path, i, new Color(1, 0.8f, 0));
                    path.Points[i] = pt;
                }
            }
        }

        private void DrawControlPoint(ref Vector2 cp, Vector2 anchor, ProceduralShape shape, ShapePath path, int idx, Color color)
        {
            Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            Handles.DrawDottedLine(anchor, cp, 2f);
            EditorGUI.BeginChangeCheck();
            Handles.color = color;
            Vector3 newCp = Handles.FreeMoveHandle(cp, HandleUtility.GetHandleSize(cp) * 0.05f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(shape, "Move Control Point");
                cp = newCp;
                shape.m_FlattenedPath.Clear();
                shape.SetAllDirty();
            }
        }

        private void DrawGradientHandles(ProceduralShape shape, float hw, float hh)
        {
            if (shape.MainFill.Type == FillType.Solid || shape.MainFill.Type == FillType.Pattern) return;

            Color gradColor = new Color(1, 1, 0);
            Vector2 offset = shape.MainFill.GradientOffset;
            Vector3 center = new Vector3(offset.x * hw, offset.y * hh, 0);
            
            EditorGUI.BeginChangeCheck();
            Handles.color = gradColor;
            Vector3 newCenter = Handles.FreeMoveHandle(center, HandleUtility.GetHandleSize(center) * 0.1f, Vector3.zero, ModernHandleCap);
            
            float angleRad = shape.MainFill.GradientAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);
            float scale = shape.MainFill.GradientScale * Mathf.Max(hw, hh);
            
            Vector3 anglePos = newCenter + dir * scale;
            Vector3 newAnglePos = Handles.FreeMoveHandle(anglePos, HandleUtility.GetHandleSize(anglePos) * 0.08f, Vector3.zero, ModernHandleCap);
            
            Handles.color = new Color(gradColor.r, gradColor.g, gradColor.b, 0.4f);
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

        private void ModernHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(controlID, HandleUtility.DistanceToCircle(position, size));
                return;
            }

            if (eventType == EventType.Repaint)
            {
                Color baseColor = Handles.color;
                bool isHover = HandleUtility.nearestControl == controlID;
                bool isActive = GUIUtility.hotControl == controlID;

                Color color = baseColor;
                if (isActive) color = Color.white;
                else if (isHover) color = Color.Lerp(baseColor, Color.white, 0.5f);

                // Glow/Shadow
                Handles.color = new Color(0, 0, 0, 0.4f);
                Handles.DrawSolidDisc(position, Vector3.forward, size * 1.3f);
                
                // Outer Border
                Handles.color = Color.black;
                Handles.DrawSolidDisc(position, Vector3.forward, size);
                
                // Fill
                Handles.color = color;
                Handles.DrawSolidDisc(position, Vector3.forward, size * 0.75f);
                
                // Center Dot for more detail
                Handles.color = Color.black;
                Handles.DrawSolidDisc(position, Vector3.forward, size * 0.2f);

                Handles.color = baseColor; // Restore
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
                float angle = (i * angleStep) * Mathf.Deg2Rad;
                float r = 1f;
                if (shape.m_ShapeType == ShapeType.Star && (i % 2 != 0)) r = shape.m_StarRatio;

                points[i] = new Vector2(Mathf.Sin(angle) * hw * r, Mathf.Cos(angle) * hh * r) + shape.GetGeometricCenterOffset();
            }

            collider.points = points;
            EditorUtility.SetDirty(collider);
        }
    }
}
#endif
