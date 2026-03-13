using System;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public abstract class ProceduralEffect
  {
    public bool Enabled = true;
    public ShapeFill Fill = new ShapeFill();
  }
}