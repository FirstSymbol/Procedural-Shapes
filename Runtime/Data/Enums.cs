namespace ProceduralShapes.Runtime
{
    public enum ShapeType { Rectangle = 0, Ellipse = 1, Polygon = 2, Star = 3, Capsule = 4, Line = 5, Ring = 6, None = 7, Path = 8, Triangle = 9, Heart = 10 }
    public enum StrokeAlignment { Inside = 0, Center = 1, Outside = 2 }
    public enum FillType { Solid = 0, LinearGradient = 1, RadialGradient = 2, AngularGradient = 3, Pattern = 4 }
    public enum BooleanOperation { None = 0, Union = 1, Subtraction = 2, Intersection = 3, XOR = 4 }
    
    public enum PathPointType { Line = 0, Bezier = 1 }
    public enum LineCap { Butt = 0, Round = 1, Square = 2 }
    public enum LineJoint { Miter = 0, Round = 1, Bevel = 2 }
}