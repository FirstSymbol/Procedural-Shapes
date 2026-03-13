using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public class ShapeFill
  {
    public FillType Type = FillType.Solid;
    public Color SolidColor = Color.white;
    public Gradient Gradient = new Gradient();
    [Range(0, 360)] public float GradientAngle = 0f;
    public Vector2 GradientOffset = Vector2.zero;
    [Min(0.01f)] public float GradientScale = 1f;
  }
}