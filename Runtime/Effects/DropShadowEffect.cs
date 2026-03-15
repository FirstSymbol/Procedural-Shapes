using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Эффект отбрасываемой тени.
  /// </summary>
  [Serializable]
  public class DropShadowEffect : ProceduralEffect
  {
    /// <summary>
    /// Смещение тени относительно объекта по осям X и Y.
    /// </summary>
    public Vector2 Offset = new Vector2(0f, -4f);
    
    /// <summary>
    /// Степень размытия тени.
    /// </summary>
    [Min(0)] public float Blur = 10f;
    
    /// <summary>
    /// Растяжение (расширение) тени.
    /// </summary>
    public float Spread = 0f;

    public DropShadowEffect() { Fill.SolidColor = new Color(0, 0, 0, 0.5f); }
  }
}