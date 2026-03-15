using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Эффект обводки (контура) объекта.
  /// </summary>
  [Serializable]
  public class StrokeEffect : ProceduralEffect
  {
    /// <summary>
    /// Ширина обводки.
    /// </summary>
    [Min(0)] public float Width = 2f;
    
    /// <summary>
    /// Выравнивание обводки относительно края (снаружи, внутри или по центру).
    /// </summary>
    public StrokeAlignment Alignment = StrokeAlignment.Inside;
    
    /// <summary>
    /// Размер штриха для пунктирной линии. Если 0, линия сплошная.
    /// </summary>
    [Min(0)] public float DashSize = 0f;
    
    /// <summary>
    /// Размер пропуска между штрихами для пунктирной линии.
    /// </summary>
    [Min(0)] public float DashSpace = 0f;

    public StrokeEffect() { Fill.SolidColor = Color.black; }
  }
}