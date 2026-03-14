using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [Serializable]
    public class BevelEffect : ProceduralEffect
    {
        public float Distance = 5f;
        [Range(0f, 1f)] public float HighlightAlpha = 0.8f;
        [Range(0f, 1f)] public float ShadowAlpha = 0.8f;
        [Range(0, 360)] public float Angle = 135f;

        public BevelEffect() { Fill.Type = FillType.Solid; Fill.SolidColor = Color.white; }
    }
}