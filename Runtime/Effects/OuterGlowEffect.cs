using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public class OuterGlowEffect : ProceduralEffect
  {
    [Min(0)] public float Blur = 10f;
    public float Spread = 5f;

    public OuterGlowEffect() 
    { 
        Fill.SolidColor = new Color(1f, 1f, 0f, 0.5f); 
    }
  }
}
