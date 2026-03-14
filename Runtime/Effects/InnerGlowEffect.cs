using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public class InnerGlowEffect : ProceduralEffect
  {
    [Min(0)] public float Blur = 10f;
    public float Spread = 0f;

    public InnerGlowEffect() { Fill.SolidColor = new Color(1, 1, 1, 0.5f); }
  }
}