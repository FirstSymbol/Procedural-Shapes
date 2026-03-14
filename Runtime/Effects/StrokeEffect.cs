using System;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
  [Serializable]
  public class StrokeEffect : ProceduralEffect
  {
    [Min(0)] public float Width = 2f;
    public StrokeAlignment Alignment = StrokeAlignment.Inside;
    [Min(0)] public float DashSize = 0f;
    [Min(0)] public float DashSpace = 0f;

    public StrokeEffect() { Fill.SolidColor = Color.black; }
  }
}