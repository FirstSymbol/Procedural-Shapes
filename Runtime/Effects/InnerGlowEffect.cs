using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Эффект внутреннего свечения.
  /// </summary>
  [Serializable]
  public class InnerGlowEffect : ProceduralEffect
  {
    /// <summary>
    /// Степень размытия свечения.
    /// </summary>
    [Min(0)] public float Blur = 10f;
    
    /// <summary>
    /// Растяжение (интенсивность) свечения от краев к центру.
    /// </summary>
    public float Spread = 0f;

    public InnerGlowEffect() { Fill.SolidColor = new Color(1, 1, 1, 0.5f); }
  }
}