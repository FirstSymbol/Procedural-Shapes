using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Представляет отдельную точку в пути фигуры.
    /// </summary>
    [Serializable]
    public struct PathPoint
    {
        /// <summary> Координаты основной точки. </summary>
        public Vector2 Position;
        /// <summary> Первая контрольная точка для кривой Безье. </summary>
        public Vector2 ControlPoint1;
        /// <summary> Вторая контрольная точка для кривой Безье. </summary>
        public Vector2 ControlPoint2;
        /// <summary> Тип точки (линия или сегмент Безье). </summary>
        public PathPointType Type;
        
        /// <summary>
        /// Инициализирует новую точку пути как простую линию.
        /// </summary>
        /// <param name="pos">Позиция точки.</param>
        public PathPoint(Vector2 pos)
        {
            Position = pos;
            ControlPoint1 = pos;
            ControlPoint2 = pos;
            Type = PathPointType.Line;
        }
    }

    /// <summary>
    /// Описание пути (контура) произвольной формы.
    /// </summary>
    [Serializable]
    public class ShapePath
    {
        /// <summary> Флаг, указывающий, замкнут ли путь. </summary>
        public bool Closed = true;
        /// <summary> Толщина линии контура. </summary>
        [Range(0.1f, 100f)] public float Thickness = 5f;
        /// <summary> Тип наконечника (начала и конца) линии. </summary>
        public LineCap Cap = LineCap.Round;
        /// <summary> Тип соединения сегментов линии. </summary>
        public LineJoint Joint = LineJoint.Round;
        
        /// <summary> Список точек, составляющих путь. </summary>
        public List<PathPoint> Points = new List<PathPoint>()
        {
            new PathPoint(new Vector2(-50, -50)),
            new PathPoint(new Vector2(0, 50)),
            new PathPoint(new Vector2(50, -50))
        };
    }
}
