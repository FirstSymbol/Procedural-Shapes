using System;

namespace ProceduralShapes.Runtime
{
  /// <summary>
  /// Базовый класс для всех процедурных эффектов.
  /// </summary>
  [Serializable]
  public abstract class ProceduralEffect
  {
    /// <summary>
    /// Включен ли эффект.
    /// </summary>
    public bool Enabled = true;
    
    /// <summary>
    /// Параметры заполнения эффекта (цвет, градиент и т.д.).
    /// </summary>
    public ShapeFill Fill = new ShapeFill();
  }
}