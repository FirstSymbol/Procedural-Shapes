using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public static class PathUtils
    {
        public static void FlattenPath(ShapePath path, List<Vector2> output, int resolution = 10)
        {
            output.Clear();
            if (path == null || path.Points.Count < 2) return;

            for (int i = 0; i < path.Points.Count; i++)
            {
                var current = path.Points[i];
                if (i == 0)
                {
                    output.Add(current.Position);
                    continue;
                }

                var prev = path.Points[i - 1];
                if (current.Type == PathPointType.Line)
                {
                    output.Add(current.Position);
                }
                else if (current.Type == PathPointType.Bezier)
                {
                    for (int j = 1; j <= resolution; j++)
                    {
                        float t = j / (float)resolution;
                        output.Add(EvaluateCubicBezier(prev.Position, prev.ControlPoint2, current.ControlPoint1, current.Position, t));
                    }
                }
            }

            if (path.Closed && path.Points.Count > 2)
            {
                var first = path.Points[0];
                var last = path.Points[path.Points.Count - 1];
                if (first.Type == PathPointType.Bezier)
                {
                    for (int j = 1; j <= resolution; j++)
                    {
                        float t = j / (float)resolution;
                        output.Add(EvaluateCubicBezier(last.Position, last.ControlPoint2, first.ControlPoint1, first.Position, t));
                    }
                }
            }
        }

        private static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }
    }
}