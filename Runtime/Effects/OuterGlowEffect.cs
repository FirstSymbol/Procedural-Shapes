using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Эффект внешнего свечения.
  /// </summary>
  [Serializable]
  public class OuterGlowEffect : ProceduralEffect
  {
    /// <summary>
    /// Степень размытия свечения за пределами объекта.
    /// </summary>
    [Min(0)] public float Blur = 10f;
    
    /// <summary>
    /// Растяжение (радиус) свечения.
    /// </summary>
    public float Spread = 5f;

    public OuterGlowEffect() 
    { 
        Fill.SolidColor = new Color(1f, 1f, 0f, 0.5f); 
    }
  }
}