using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public static class GradientAtlasManager
    {
        private static Texture2D s_AtlasTexture;
        private static List<GradientDataEntry> s_AtlasRegistry = new List<GradientDataEntry>();
        public const int ATLAS_WIDTH = 256;
        public const int ATLAS_HEIGHT = 512;

        private struct GradientDataEntry 
        { 
            public FillType type; 
            public string gradientHash; 
            public int rowIndex;
        }

        public static Texture2D AtlasTexture => s_AtlasTexture;

        public static int GetAtlasRow(ShapeFill fill)
        {
            if (s_AtlasTexture == null)
            {
                s_AtlasTexture = new Texture2D(ATLAS_WIDTH, ATLAS_HEIGHT, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color[] clear = new Color[ATLAS_WIDTH * ATLAS_HEIGHT];
                for(int i=0; i<clear.Length; i++) clear[i] = Color.clear;
                // Row 0 is always white for Solid fill batching
                for(int x=0; x<ATLAS_WIDTH; x++) clear[x] = clear[ATLAS_WIDTH + x] = clear[ATLAS_WIDTH*2 + x] = Color.white;
                
                s_AtlasTexture.SetPixels(clear);
                s_AtlasTexture.Apply();
            }

            if (fill.Type == FillType.Solid) return 0;

            string currentHash = GetGradientHash(fill.Gradient);
            for (int i = 0; i < s_AtlasRegistry.Count; i++)
            {
                var data = s_AtlasRegistry[i];
                if (data.type == fill.Type && data.gradientHash == currentHash)
                    return data.rowIndex;
            }

            int newIndex = (s_AtlasRegistry.Count + 1); // Row 0 is reserved
            if (newIndex * 3 + 3 >= ATLAS_HEIGHT) 
            {
                ClearAtlas(); // Atlas full, emergency reset
                return GetAtlasRow(fill); 
            }

            s_AtlasRegistry.Add(new GradientDataEntry { type = fill.Type, gradientHash = currentHash, rowIndex = newIndex });
            BakeToAtlas(newIndex, fill);
            return newIndex;
        }

        private static System.Text.StringBuilder s_HashBuilder = new System.Text.StringBuilder();
        private static string GetGradientHash(Gradient g)
        {
            if (g == null) return "null";
            s_HashBuilder.Clear();
            var ck = g.colorKeys;
            var ak = g.alphaKeys;
            s_HashBuilder.Append(ck.Length).Append(ak.Length).Append(g.mode.GetHashCode());
            for (int i = 0; i < ck.Length; i++) 
                s_HashBuilder.Append(ck[i].color.r).Append(ck[i].color.g).Append(ck[i].color.b).Append(ck[i].time);
            for (int i = 0; i < ak.Length; i++) 
                s_HashBuilder.Append(ak[i].alpha).Append(ak[i].time);
            return s_HashBuilder.ToString();
        }

        private static void BakeToAtlas(int index, ShapeFill fill)
        {
            int rowStart = index * 3;
            Color[] pixels = new Color[ATLAS_WIDTH * 3];
            for (int x = 0; x < ATLAS_WIDTH; x++)
            {
                Color c = fill.Gradient.Evaluate(x / (float)(ATLAS_WIDTH - 1));
                pixels[x] = pixels[ATLAS_WIDTH + x] = pixels[ATLAS_WIDTH * 2 + x] = c;
            }
            s_AtlasTexture.SetPixels(0, rowStart, ATLAS_WIDTH, 3, pixels);
            s_AtlasTexture.Apply();
        }

        public static void ClearAtlas()
        {
            if (s_AtlasTexture != null) Object.DestroyImmediate(s_AtlasTexture);
            s_AtlasRegistry.Clear();
            s_AtlasTexture = null;
        }
    }
}