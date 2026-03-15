using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Эффект внутренней тени, создающий впечатление вдавленности объекта.
  /// </summary>
  [Serializable]
  public class InnerShadowEffect : ProceduralEffect
  {
    /// <summary>
    /// Смещение тени внутри объекта.
    /// </summary>
    public Vector2 Offset = new Vector2(0f, -4f);
    
    /// <summary>
    /// Степень размытия тени.
    /// </summary>
    [Min(0)] public float Blur = 10f;
    
    /// <summary>
    /// Растяжение тени внутри объекта.
    /// </summary>
    public float Spread = 0f;

    public InnerShadowEffect() { Fill.SolidColor = new Color(0, 0, 0, 0.5f); }
  }
}