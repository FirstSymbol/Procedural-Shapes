using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public class InnerShadowEffect : ProceduralEffect
  {
    public Vector2 Offset = new Vector2(0f, -4f);
    [Min(0)] public float Blur = 10f;
    public float Spread = 0f;

    public InnerShadowEffect() { Fill.SolidColor = new Color(0, 0, 0, 0.5f); }
  }
}