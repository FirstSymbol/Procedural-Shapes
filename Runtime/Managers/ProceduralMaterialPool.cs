using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Хранит полное состояние параметров шейдера для материала.
    /// Используется как ключ для пула материалов.
    /// </summary>
    public class ShaderState : IEquatable<ShaderState>
    {
        public ShapeType ShapeType;
        public int BaseMatId;
        public Texture MainTex;
        public Texture PatternTex;
        public float InternalPadding;
        public bool HasNoise;

        public int PathPointCount;
        public Vector4[] PathData;

        public int BoolPathPointCount;
        public Vector4[] BoolPathData;

        public int BoolCount;
        public Vector4[] BoolOpType;
        public Vector4[] BoolShapeParams;
        public Vector4[] BoolTransform;
        public Vector4[] BoolSize;

        public bool HasMask;
        public Matrix4x4 MaskMatrix;
        public Vector4 MaskParams;
        public Vector4 MaskSize;
        public Vector4 MaskShape;
        public Texture MaskTex;
        public Vector4 MaskFillParams;
        public Vector4 MaskFillOffset;

        public int MaskBoolCount;
        public Vector4[] MaskBoolOpType;
        public Vector4[] MaskBoolShapeParams;
        public Vector4[] MaskBoolTransform;
        public Vector4[] MaskBoolSize;

        public void Clear()
        {
            ShapeType = ShapeType.Rectangle;
            BaseMatId = 0; MainTex = null; PatternTex = null; InternalPadding = 0;
            PathPointCount = 0; BoolPathPointCount = 0; BoolCount = 0;
            HasMask = false; MaskBoolCount = 0; HasNoise = false;
        }

        private void EnsureArrays() {
            if (PathData == null) PathData = new Vector4[64];
            if (BoolPathData == null) BoolPathData = new Vector4[64];
            if (BoolOpType == null) {
                BoolOpType = new Vector4[8];
                BoolShapeParams = new Vector4[8];
                BoolTransform = new Vector4[8];
                BoolSize = new Vector4[8];
            }
            if (MaskBoolOpType == null) {
                MaskBoolOpType = new Vector4[8];
                MaskBoolShapeParams = new Vector4[8];
                MaskBoolTransform = new Vector4[8];
                MaskBoolSize = new Vector4[8];
            }
        }

        public ShaderState Clone()
        {
            var clone = new ShaderState();
            clone.ShapeType = ShapeType;
            clone.BaseMatId = BaseMatId;
            clone.MainTex = MainTex;
            clone.PatternTex = PatternTex;
            clone.InternalPadding = InternalPadding;
            clone.HasNoise = HasNoise;

            clone.PathPointCount = PathPointCount;
            if (PathPointCount > 0) {
                clone.PathData = new Vector4[64];
                Array.Copy(PathData, clone.PathData, 64);
            }

            clone.BoolPathPointCount = BoolPathPointCount;
            if (BoolPathPointCount > 0) {
                clone.BoolPathData = new Vector4[64];
                Array.Copy(BoolPathData, clone.BoolPathData, 64);
            }

            clone.BoolCount = BoolCount;
            if (BoolCount > 0)
            {
                clone.BoolOpType = new Vector4[8];
                clone.BoolShapeParams = new Vector4[8];
                clone.BoolTransform = new Vector4[8];
                clone.BoolSize = new Vector4[8];
                Array.Copy(BoolOpType, clone.BoolOpType, 8);
                Array.Copy(BoolShapeParams, clone.BoolShapeParams, 8);
                Array.Copy(BoolTransform, clone.BoolTransform, 8);
                Array.Copy(BoolSize, clone.BoolSize, 8);
            }

            clone.HasMask = HasMask;
            if (HasMask)
            {
                clone.MaskMatrix = MaskMatrix;
                clone.MaskParams = MaskParams;
                clone.MaskSize = MaskSize;
                clone.MaskShape = MaskShape;
                clone.MaskTex = MaskTex;
                clone.MaskFillParams = MaskFillParams;
                clone.MaskFillOffset = MaskFillOffset;

                clone.MaskBoolCount = MaskBoolCount;
                if (MaskBoolCount > 0)
                {
                    clone.MaskBoolOpType = new Vector4[8];
                    clone.MaskBoolShapeParams = new Vector4[8];
                    clone.MaskBoolTransform = new Vector4[8];
                    clone.MaskBoolSize = new Vector4[8];
                    Array.Copy(MaskBoolOpType, clone.MaskBoolOpType, 8);
                    Array.Copy(MaskBoolShapeParams, clone.MaskBoolShapeParams, 8);
                    Array.Copy(MaskBoolTransform, clone.MaskBoolTransform, 8);
                    Array.Copy(MaskBoolSize, clone.MaskBoolSize, 8);
                }
            }
            return clone;
        }

        public bool Equals(ShaderState other)
        {
            if (other == null) return false;
            if (ShapeType != other.ShapeType || BaseMatId != other.BaseMatId || MainTex != other.MainTex || PatternTex != other.PatternTex || 
                Mathf.Abs(InternalPadding - other.InternalPadding) > 0.001f || HasMask != other.HasMask || HasNoise != other.HasNoise) return false;

            if (PathPointCount != other.PathPointCount) return false;
            if (PathPointCount > 0) {
                for (int i = 0; i < (PathPointCount + 1) / 2; i++) if (PathData[i] != other.PathData[i]) return false;
            }

            if (BoolPathPointCount != other.BoolPathPointCount) return false;
            if (BoolPathPointCount > 0) {
                for (int i = 0; i < (BoolPathPointCount + 1) / 2; i++) if (BoolPathData[i] != other.BoolPathData[i]) return false;
            }

            if (BoolCount != other.BoolCount) return false;
            if (BoolCount > 0) {
                for (int i = 0; i < BoolCount; i++)
                {
                    if (BoolOpType[i] != other.BoolOpType[i] || BoolShapeParams[i] != other.BoolShapeParams[i] ||
                        BoolTransform[i] != other.BoolTransform[i] || BoolSize[i] != other.BoolSize[i]) return false;
                }
            }

            if (HasMask)
            {
                if (MaskMatrix != other.MaskMatrix || MaskParams != other.MaskParams || MaskSize != other.MaskSize ||
                    MaskShape != other.MaskShape || MaskTex != other.MaskTex || MaskFillParams != other.MaskFillParams ||
                    MaskFillOffset != other.MaskFillOffset) return false;

                if (MaskBoolCount != other.MaskBoolCount) return false;
                if (MaskBoolCount > 0) {
                    for (int i = 0; i < MaskBoolCount; i++)
                    {
                        if (MaskBoolOpType[i] != other.MaskBoolOpType[i] || MaskBoolShapeParams[i] != other.MaskBoolShapeParams[i] ||
                            MaskBoolTransform[i] != other.MaskBoolTransform[i] || MaskBoolSize[i] != other.MaskBoolSize[i]) return false;
                    }
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked {
                hash = hash * 23 + (int)ShapeType;
                hash = hash * 23 + BaseMatId;
                hash = hash * 23 + (MainTex ? MainTex.GetInstanceID() : 0);
                hash = hash * 23 + InternalPadding.GetHashCode();
                hash = hash * 23 + BoolCount;
                hash = hash * 23 + PathPointCount;
                hash = hash * 23 + (HasMask ? 1 : 0);
                hash = hash * 23 + (HasNoise ? 1 : 0);
                if (BoolCount > 0 && BoolTransform != null) hash = hash * 23 + BoolTransform[0].GetHashCode();
                if (HasMask) hash = hash * 23 + MaskMatrix.GetHashCode();
            }
            return hash;
        }

        public void ApplyToMaterial(Material mat)
        {
            mat.DisableKeyword("SHAPE_RECTANGLE");
            mat.DisableKeyword("SHAPE_ELLIPSE");
            mat.DisableKeyword("SHAPE_POLYGON");
            mat.DisableKeyword("SHAPE_STAR");
            mat.DisableKeyword("SHAPE_CAPSULE");
            mat.DisableKeyword("SHAPE_LINE");
            mat.DisableKeyword("SHAPE_RING");
            mat.DisableKeyword("SHAPE_PATH");
            mat.DisableKeyword("SHAPE_TRIANGLE");
            mat.DisableKeyword("SHAPE_HEART");

            switch (ShapeType)
            {
                case ProceduralShapes.Runtime.ShapeType.Rectangle: mat.EnableKeyword("SHAPE_RECTANGLE"); break;
                case ProceduralShapes.Runtime.ShapeType.Ellipse: mat.EnableKeyword("SHAPE_ELLIPSE"); break;
                case ProceduralShapes.Runtime.ShapeType.Polygon: mat.EnableKeyword("SHAPE_POLYGON"); break;
                case ProceduralShapes.Runtime.ShapeType.Star: mat.EnableKeyword("SHAPE_STAR"); break;
                case ProceduralShapes.Runtime.ShapeType.Capsule: mat.EnableKeyword("SHAPE_CAPSULE"); break;
                case ProceduralShapes.Runtime.ShapeType.Line: mat.EnableKeyword("SHAPE_LINE"); break;
                case ProceduralShapes.Runtime.ShapeType.Ring: mat.EnableKeyword("SHAPE_RING"); break;
                case ProceduralShapes.Runtime.ShapeType.Path: mat.EnableKeyword("SHAPE_PATH"); break;
                case ProceduralShapes.Runtime.ShapeType.Triangle: mat.EnableKeyword("SHAPE_TRIANGLE"); break;
                case ProceduralShapes.Runtime.ShapeType.Heart: mat.EnableKeyword("SHAPE_HEART"); break;
            }

            if (BoolCount > 0) mat.EnableKeyword("HAS_BOOLEANS"); else mat.DisableKeyword("HAS_BOOLEANS");
            if (HasMask) mat.EnableKeyword("HAS_MASK"); else mat.DisableKeyword("HAS_MASK");
            if (HasNoise) mat.EnableKeyword("HAS_NOISE"); else mat.DisableKeyword("HAS_NOISE");

            if (MainTex) mat.SetTexture("_MainTex", MainTex);
            if (PatternTex) mat.SetTexture("_PatternTex", PatternTex);
            mat.SetFloat("_InternalPadding", InternalPadding);

            if (PathPointCount > 0)
            {
                mat.SetVectorArray("_PathData", PathData);
                mat.SetInt("_PathPointCount", PathPointCount);
            }

            if (BoolPathPointCount > 0)
            {
                mat.SetVectorArray("_BoolPathData", BoolPathData);
                mat.SetInt("_BoolPathPointCount", BoolPathPointCount);
            }

            mat.SetInt("_BoolParams1", BoolCount);
            if (BoolCount > 0)
            {
                mat.SetVectorArray("_BoolData_OpType", BoolOpType);
                mat.SetVectorArray("_BoolData_ShapeParams", BoolShapeParams);
                mat.SetVectorArray("_BoolData_Transform", BoolTransform);
                mat.SetVectorArray("_BoolData_Size", BoolSize);
            }

            if (HasMask)
            {
                mat.SetVector("_MaskMatrixX", MaskMatrix.GetRow(0));
                mat.SetVector("_MaskMatrixY", MaskMatrix.GetRow(1));
                mat.SetVector("_MaskMatrixZ", MaskMatrix.GetRow(2));
                mat.SetVector("_MaskMatrixW", MaskMatrix.GetRow(3));
                mat.SetVector("_MaskParams", MaskParams);
                mat.SetVector("_MaskSize", MaskSize);
                mat.SetVector("_MaskShape", MaskShape);
                mat.SetTexture("_MaskTex", MaskTex ? MaskTex : Texture2D.whiteTexture);
                mat.SetVector("_MaskFillParams", MaskFillParams);
                mat.SetVector("_MaskFillOffset", MaskFillOffset);
                mat.SetInt("_MaskBoolParams", MaskBoolCount);
                if (MaskBoolCount > 0)
                {
                    mat.SetVectorArray("_MaskBoolOpType", MaskBoolOpType);
                    mat.SetVectorArray("_MaskBoolShapeParams", MaskBoolShapeParams);
                    mat.SetVectorArray("_MaskBoolTransform", MaskBoolTransform);
                    mat.SetVectorArray("_MaskBoolSize", MaskBoolSize);
                }
            }
            else
            {
                mat.SetVector("_MaskParams", Vector4.zero);
                mat.SetInt("_MaskBoolParams", 0);
            }
        }
    }

    /// <summary>
    /// Пул материалов для процедурных фигур.
    /// Позволяет повторно использовать инстансы материалов с одинаковыми параметрами шейдера,
    /// значительно снижая нагрузку на CPU и память (Draw Call Batching).
    /// </summary>
    public static class ProceduralMaterialPool
    {
        public static ShaderState TempState = new ShaderState();

        private class PoolEntry
        {
            public Material Material;
            public int RefCount;
            public ShaderState State;
        }

        private static Dictionary<int, List<PoolEntry>> s_Pool = new Dictionary<int, List<PoolEntry>>();
        private static Dictionary<int, PoolEntry> s_InstanceToEntry = new Dictionary<int, PoolEntry>();

        /// <summary>
        /// Возвращает материал из пула по ключу (состоянию) или создает новый, если совпадений не найдено.
        /// </summary>
        public static Material GetMaterial(ShaderState state, Material baseMat)
        {
            int hash = state.GetHashCode();
            if (s_Pool.TryGetValue(hash, out var list))
            {
                foreach (var entry in list)
                {
                    if (entry.State.Equals(state))
                    {
                        entry.RefCount++;
                        return entry.Material;
                    }
                }
            }
            else
            {
                list = new List<PoolEntry>();
                s_Pool[hash] = list;
            }

            Material newMat = new Material(baseMat);
            newMat.hideFlags = HideFlags.HideAndDontSave;
            state.ApplyToMaterial(newMat);

            PoolEntry newEntry = new PoolEntry { Material = newMat, RefCount = 1, State = state.Clone() };
            list.Add(newEntry);
            s_InstanceToEntry[newMat.GetInstanceID()] = newEntry;

            return newMat;
        }

        /// <summary>
        /// Освобождает материал. Если счетчик ссылок достигает нуля, материал удаляется из памяти.
        /// </summary>
        public static void ReleaseMaterial(Material mat)
        {
            if (mat == null) return;
            int id = mat.GetInstanceID();
            
            if (s_InstanceToEntry.TryGetValue(id, out var entry))
            {
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    int hash = entry.State.GetHashCode();
                    if (s_Pool.TryGetValue(hash, out var list))
                    {
                        list.Remove(entry);
                        if (list.Count == 0) s_Pool.Remove(hash);
                    }
                    s_InstanceToEntry.Remove(id);
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(entry.Material);
                    else
                        UnityEngine.Object.DestroyImmediate(entry.Material);
                }
            }
            else
            {
                // Если материал не из пула (уникальный), он просто уничтожается
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(mat);
                else
                    UnityEngine.Object.DestroyImmediate(mat);
            }
        }
        
        /// <summary> Очистка всего пула материалов. </summary>
        public static void ClearAll()
        {
            foreach (var list in s_Pool.Values)
            {
                foreach (var entry in list)
                {
                    if (entry.Material != null)
                    {
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(entry.Material);
                        else
                            UnityEngine.Object.DestroyImmediate(entry.Material);
                    }
                }
            }
            s_Pool.Clear();
            s_InstanceToEntry.Clear();
        }
    }
}