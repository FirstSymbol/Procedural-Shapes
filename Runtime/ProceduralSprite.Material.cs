using UnityEngine;
using System.Collections.Generic;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralSprite
    {
        private int m_ActiveBoolCount;

        private Material GetModifiedMaterial(Material baseMaterial)
        {
            if (m_ShapeType == ShapeType.Path)
            {
                if (m_FlattenedPath == null) m_FlattenedPath = new List<Vector2>();
                if (m_FlattenedPath.Count == 0)
                {
                    PathUtils.FlattenPath(m_ShapePath, m_FlattenedPath);
                }
            }

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Vector2 selfCenterOffset = GetGeometricCenterOffset();
            m_ActiveBoolCount = 0;
            s_VisitedShapes.Clear();
            s_VisitedShapes.Add(this);
            
            ProceduralSprite firstPathOperator = null;
            foreach(var op in BooleanOperations) {
                if (op.SourceShape != null && op.SourceShape.m_ShapeType == ShapeType.Path) {
                    firstPathOperator = op.SourceShape;
                    if (firstPathOperator.m_FlattenedPath == null || firstPathOperator.m_FlattenedPath.Count == 0)
                        PathUtils.FlattenPath(firstPathOperator.m_ShapePath, firstPathOperator.m_FlattenedPath);
                    break;
                }
            }

            CollectBooleanOps(this, BooleanOperation.Union, ref m_ActiveBoolCount, worldToLocal, selfCenterOffset);
            s_VisitedShapes.Clear();

            ShaderState state = ProceduralMaterialPool.TempState;
            state.Clear();
            state.ShapeType = m_ShapeType;
            state.BaseMatId = baseMaterial.GetInstanceID();
            state.MainTex = GradientAtlasManager.AtlasTexture;
            state.PatternTex = MainFill.Type == FillType.Pattern ? MainFill.PatternTexture : null;
            state.InternalPadding = m_InternalPadding;
            state.HasNoise = m_EdgeNoiseAmount > 0.001f;
            state.HasMask = false;

            Vector2 stretch = GetStretchScale();
            Vector2 rootLs = transform.lossyScale;
            rootLs.x = Mathf.Max(Mathf.Abs(rootLs.x), 0.001f);
            rootLs.y = Mathf.Max(Mathf.Abs(rootLs.y), 0.001f);
            Vector2 rootUvScale = new Vector2(rootLs.x * stretch.x, rootLs.y * stretch.y);

            if (m_ShapeType == ShapeType.Path && m_FlattenedPath != null)
            {
                if (state.PathData == null) state.PathData = new Vector4[64];
                ApplyPathDataToState(state.PathData, out state.PathPointCount, m_FlattenedPath, rootUvScale);
            }

            if (firstPathOperator != null)
            {
                if (state.BoolPathData == null) state.BoolPathData = new Vector4[64];
                
                Vector3 otherLs = firstPathOperator.transform.lossyScale;
                Vector2 opScale = new Vector2(otherLs.x * firstPathOperator.ShapeScale.x * stretch.x, otherLs.y * firstPathOperator.ShapeScale.y * stretch.y);
                
                ApplyPathDataToState(state.BoolPathData, out state.BoolPathPointCount, firstPathOperator.m_FlattenedPath, opScale);
            }

            state.BoolCount = m_ActiveBoolCount;
            if (m_ActiveBoolCount > 0)
            {
                if (state.BoolOpType == null) {
                    state.BoolOpType = new Vector4[8]; state.BoolShapeParams = new Vector4[8];
                    state.BoolTransform = new Vector4[8]; state.BoolSize = new Vector4[8];
                }
                System.Array.Copy(m_ShaderOps, state.BoolOpType, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderShapeParams, state.BoolShapeParams, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderTransform, state.BoolTransform, m_ActiveBoolCount);
                System.Array.Copy(m_ShaderSize, state.BoolSize, m_ActiveBoolCount);
            }

            bool isUnique = m_ActiveBoolCount > 0 || firstPathOperator != null || m_ShapeType == ShapeType.Path;

            if (isUnique)
            {
                if (m_InstanceMaterial == null || m_InstanceMaterialIsPooled)
                {
                    if (m_InstanceMaterial != null) ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
                    m_InstanceMaterial = new Material(baseMaterial);
                    m_InstanceMaterial.hideFlags = HideFlags.HideAndDontSave;
                    m_InstanceMaterialIsPooled = false;
                }
                
                state.ApplyToMaterial(m_InstanceMaterial);
                return m_InstanceMaterial;
            }
            else
            {
                Material matToUse = ProceduralMaterialPool.GetMaterial(state, baseMaterial);
                
                if (m_InstanceMaterial != null) 
                {
                    if (m_InstanceMaterialIsPooled) ProceduralMaterialPool.ReleaseMaterial(m_InstanceMaterial);
                    else if (Application.isPlaying) Destroy(m_InstanceMaterial); else DestroyImmediate(m_InstanceMaterial);
                }

                m_InstanceMaterial = matToUse;
                m_InstanceMaterialIsPooled = true;
                return m_InstanceMaterial;
            }
        }

        private void ApplyPathDataToState(Vector4[] targetArray, out int pointCount, List<Vector2> points, Vector2 scale)
        {
            pointCount = Mathf.Min(points.Count, 128); 
            for (int i = 0; i < pointCount; i += 2)
            {
                float x1 = points[i].x * scale.x;
                float y1 = points[i].y * scale.y;
                float x2 = (i + 1 < pointCount) ? points[i + 1].x * scale.x : 0;
                float y2 = (i + 1 < pointCount) ? points[i + 1].y * scale.y : 0;
                targetArray[i / 2] = new Vector4(x1, y1, x2, y2);
            }
        }

        private void CollectBooleanOps(ProceduralSprite currentShape, BooleanOperation parentOp, ref int count, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset)
        {
            if (currentShape == null) return;
            
            foreach (var input in currentShape.BooleanOperations)
            {
                if (count >= MAX_OPS) return;
                if (input.SourceShape == null || !input.SourceShape.isActiveAndEnabled || input.Operation == BooleanOperation.None) continue;
                if (s_VisitedShapes.Contains(input.SourceShape)) continue; 

                ProceduralSprite other = input.SourceShape;
                s_VisitedShapes.Add(other);
                
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

        private void AddShapeToShader(ProceduralSprite shape, BooleanOperation op, int index, Matrix4x4 rootWorldToLocal, Vector3 rootCenterOffset, float smoothness)
        {
            Vector2 stretch = GetStretchScale();
            Vector2 ls = transform.lossyScale;
            ls.x = Mathf.Max(Mathf.Abs(ls.x), 0.001f);
            ls.y = Mathf.Max(Mathf.Abs(ls.y), 0.001f);

            Transform otherRect = shape.transform;
            Vector3 otherPivotOffset = shape.GetGeometricCenterOffset();
            Vector3 otherCenterWorld = otherRect.TransformPoint((Vector3)shape.GetRect().center + otherPivotOffset);
            Vector3 targetPosInRootLocal = rootWorldToLocal.MultiplyPoint3x4(otherCenterWorld);
            Vector3 finalPos = targetPosInRootLocal - rootCenterOffset;
            finalPos.x *= ls.x * stretch.x;
            finalPos.y *= ls.y * stretch.y;

            float relativeRotation = otherRect.eulerAngles.z - transform.eulerAngles.z;

            float customParam = shape.m_CornerSmoothing;
            if (shape.m_ShapeType == ShapeType.Line) customParam = shape.m_LineWidth;

            float rotRad = relativeRotation * Mathf.Deg2Rad;
            m_ShaderOps[index] = new Vector4((float)op, (float)shape.m_ShapeType, customParam, smoothness); 
            m_ShaderShapeParams[index] = shape.GetPackedShapeParams();
            m_ShaderTransform[index] = new Vector4(finalPos.x, finalPos.y, Mathf.Sin(-rotRad), Mathf.Cos(-rotRad));
            
            Vector2 otherScale = shape.ShapeScale; 
            Vector3 otherLs = otherRect.lossyScale;
            
            float finalW = shape.Size.x * otherLs.x * otherScale.x * stretch.x;
            float finalH = shape.Size.y * otherLs.y * otherScale.y * stretch.y;

            m_ShaderSize[index] = new Vector4(finalW, finalH, 0, 0);
        }
    }
}