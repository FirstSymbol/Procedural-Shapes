using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Ключ для идентификации уникального состояния материала в пуле.
    /// Включает в себя ID базового материала и набор хешей специфичных параметров.
    /// </summary>
    public struct MaterialHashKey : IEquatable<MaterialHashKey>
    {
        public int BaseMatId;
        public int PatternId;
        public float Padding;
        public int Hash1;
        public int Hash2;

        public bool Equals(MaterialHashKey other)
        {
            return BaseMatId == other.BaseMatId &&
                   PatternId == other.PatternId &&
                   Padding == other.Padding &&
                   Hash1 == other.Hash1 &&
                   Hash2 == other.Hash2;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + BaseMatId;
                hash = hash * 23 + PatternId;
                hash = hash * 23 + Padding.GetHashCode();
                hash = hash * 23 + Hash1;
                hash = hash * 23 + Hash2;
                return hash;
            }
        }
    }

    /// <summary> Утилиты для быстрого формирования хеш-сумм. </summary>
    public static class HashUtils
    {
        public static void Add(ref int hash, float value)
        {
            unchecked
            {
                hash = (hash ^ value.GetHashCode()) * 16777619;
            }
        }

        public static void Add(ref int hash, Vector4 v)
        {
            Add(ref hash, v.x);
            Add(ref hash, v.y);
            Add(ref hash, v.z);
            Add(ref hash, v.w);
        }
    }

    /// <summary>
    /// Пул материалов для процедурных фигур.
    /// Позволяет повторно использовать инстансы материалов с одинаковыми параметрами,
    /// значительно снижая нагрузку на CPU и память.
    /// </summary>
    public static class ProceduralMaterialPool
    {
        private class PoolEntry
        {
            public Material Material;
            public int RefCount; // Счетчик ссылок для управления временем жизни материала
        }

        private static Dictionary<MaterialHashKey, PoolEntry> s_Pool = new Dictionary<MaterialHashKey, PoolEntry>();
        private static Dictionary<int, MaterialHashKey> s_InstanceToKey = new Dictionary<int, MaterialHashKey>();

        /// <summary>
        /// Возвращает материал из пула по ключу или создает новый, если совпадений не найдено.
        /// </summary>
        public static Material GetMaterial(MaterialHashKey key, Material baseMat)
        {
            if (s_Pool.TryGetValue(key, out var entry))
            {
                entry.RefCount++;
                return entry.Material;
            }

            // Создание нового инстанса материала на основе базового
            Material newMat = new Material(baseMat);
            newMat.hideFlags = HideFlags.HideAndDontSave;
            
            s_Pool[key] = new PoolEntry { Material = newMat, RefCount = 1 };
            s_InstanceToKey[newMat.GetInstanceID()] = key;
            return newMat;
        }

        /// <summary>
        /// Освобождает материал. Если счетчик ссылок достигает нуля, материал удаляется из памяти.
        /// </summary>
        public static void ReleaseMaterial(Material mat)
        {
            if (mat == null) return;
            int id = mat.GetInstanceID();
            
            if (s_InstanceToKey.TryGetValue(id, out var key))
            {
                if (s_Pool.TryGetValue(key, out var entry))
                {
                    entry.RefCount--;
                    if (entry.RefCount <= 0)
                    {
                        // Окончательное удаление материала
                        s_Pool.Remove(key);
                        s_InstanceToKey.Remove(id);
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(entry.Material);
                        else
                            UnityEngine.Object.DestroyImmediate(entry.Material);
                    }
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
            foreach (var entry in s_Pool.Values)
            {
                if (entry.Material != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(entry.Material);
                    else
                        UnityEngine.Object.DestroyImmediate(entry.Material);
                }
            }
            s_Pool.Clear();
            s_InstanceToKey.Clear();
        }
    }
}