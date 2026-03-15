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
        public int BaseMatId;
        public Texture MainTex;
        public Texture PatternTex;
        public float InternalPadding;

        public int PathPointCount;
        public Vector4[] PathData = new Vector4[64];

        public int BoolPathPointCount;
        public Vector4[] BoolPathData = new Vector4[64];

        public int BoolCount;
        public Vector4[] BoolOpType = new Vector4[8];
        public Vector4[] BoolShapeParams = new Vector4[8];
        public Vector4[] BoolTransform = new Vector4[8];
        public Vector4[] BoolSize = new Vector4[8];

        public bool HasMask;
        public Matrix4x4 MaskMatrix;
        public Vector4 MaskParams;
        public Vector4 MaskSize;
        public Vector4 MaskShape;
        public Texture MaskTex;
        public Vector4 MaskFillParams;
        public Vector4 MaskFillOffset;

        public int MaskBoolCount;
        public Vector4[] MaskBoolOpType = new Vector4[8];
        public Vector4[] MaskBoolShapeParams = new Vector4[8];
        public Vector4[] MaskBoolTransform = new Vector4[8];
        public Vector4[] MaskBoolSize = new Vector4[8];

        public void Clear()
        {
            BaseMatId = 0; MainTex = null; PatternTex = null; InternalPadding = 0;
            PathPointCount = 0; BoolPathPointCount = 0; BoolCount = 0;
            HasMask = false; MaskBoolCount = 0;
        }

        public ShaderState Clone()
        {
            var clone = new ShaderState();
            clone.BaseMatId = BaseMatId;
            clone.MainTex = MainTex;
            clone.PatternTex = PatternTex;
            clone.InternalPadding = InternalPadding;

            clone.PathPointCount = PathPointCount;
            if (PathPointCount > 0) Array.Copy(PathData, clone.PathData, PathPointCount > 64 ? 64 : PathPointCount);

            clone.BoolPathPointCount = BoolPathPointCount;
            if (BoolPathPointCount > 0) Array.Copy(BoolPathData, clone.BoolPathData, BoolPathPointCount > 64 ? 64 : BoolPathPointCount);

            clone.BoolCount = BoolCount;
            if (BoolCount > 0)
            {
                Array.Copy(BoolOpType, clone.BoolOpType, BoolCount);
                Array.Copy(BoolShapeParams, clone.BoolShapeParams, BoolCount);
                Array.Copy(BoolTransform, clone.BoolTransform, BoolCount);
                Array.Copy(BoolSize, clone.BoolSize, BoolCount);
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
                    Array.Copy(MaskBoolOpType, clone.MaskBoolOpType, MaskBoolCount);
                    Array.Copy(MaskBoolShapeParams, clone.MaskBoolShapeParams, MaskBoolCount);
                    Array.Copy(MaskBoolTransform, clone.MaskBoolTransform, MaskBoolCount);
                    Array.Copy(MaskBoolSize, clone.MaskBoolSize, MaskBoolCount);
                }
            }
            return clone;
        }

        public bool Equals(ShaderState other)
        {
            if (other == null) return false;
            if (BaseMatId != other.BaseMatId || MainTex != other.MainTex || PatternTex != other.PatternTex || 
                Mathf.Abs(InternalPadding - other.InternalPadding) > 0.001f || HasMask != other.HasMask) return false;

            if (PathPointCount != other.PathPointCount) return false;
            for (int i = 0; i < (PathPointCount > 64 ? 64 : PathPointCount); i++) if (PathData[i] != other.PathData[i]) return false;

            if (BoolPathPointCount != other.BoolPathPointCount) return false;
            for (int i = 0; i < (BoolPathPointCount > 64 ? 64 : BoolPathPointCount); i++) if (BoolPathData[i] != other.BoolPathData[i]) return false;

            if (BoolCount != other.BoolCount) return false;
            for (int i = 0; i < BoolCount; i++)
            {
                if (BoolOpType[i] != other.BoolOpType[i] || BoolShapeParams[i] != other.BoolShapeParams[i] ||
                    BoolTransform[i] != other.BoolTransform[i] || BoolSize[i] != other.BoolSize[i]) return false;
            }

            if (HasMask)
            {
                if (MaskMatrix != other.MaskMatrix || MaskParams != other.MaskParams || MaskSize != other.MaskSize ||
                    MaskShape != other.MaskShape || MaskTex != other.MaskTex || MaskFillParams != other.MaskFillParams ||
                    MaskFillOffset != other.MaskFillOffset) return false;

                if (MaskBoolCount != other.MaskBoolCount) return false;
                for (int i = 0; i < MaskBoolCount; i++)
                {
                    if (MaskBoolOpType[i] != other.MaskBoolOpType[i] || MaskBoolShapeParams[i] != other.MaskBoolShapeParams[i] ||
                        MaskBoolTransform[i] != other.MaskBoolTransform[i] || MaskBoolSize[i] != other.MaskBoolSize[i]) return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked {
                hash = hash * 23 + BaseMatId;
                hash = hash * 23 + (MainTex ? MainTex.GetInstanceID() : 0);
                hash = hash * 23 + InternalPadding.GetHashCode();
                hash = hash * 23 + BoolCount;
                hash = hash * 23 + PathPointCount;
                hash = hash * 23 + (HasMask ? 1 : 0);
                if (BoolCount > 0) hash = hash * 23 + BoolTransform[0].GetHashCode();
                if (HasMask) hash = hash * 23 + MaskMatrix.GetHashCode();
            }
            return hash;
        }

        public void ApplyToMaterial(Material mat)
        {
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