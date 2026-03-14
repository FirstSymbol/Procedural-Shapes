using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    [Serializable]
    public struct PathPoint
    {
        public Vector2 Position;
        public Vector2 ControlPoint1;
        public Vector2 ControlPoint2;
        public PathPointType Type;
        
        public PathPoint(Vector2 pos)
        {
            Position = pos;
            ControlPoint1 = pos;
            ControlPoint2 = pos;
            Type = PathPointType.Line;
        }
    }

    [Serializable]
    public class ShapePath
    {
        public bool Closed = true;
        [Range(0.1f, 100f)] public float Thickness = 5f;
        public LineCap Cap = LineCap.Round;
        public LineJoint Joint = LineJoint.Round;
        
        public List<PathPoint> Points = new List<PathPoint>()
        {
            new PathPoint(new Vector2(-50, -50)),
            new PathPoint(new Vector2(0, 50)),
            new PathPoint(new Vector2(50, -50))
        };
    }
}