namespace ProceduralShapes.Runtime
{
    public enum ShapeType { Rectangle = 0, Ellipse = 1, Polygon = 2, Star = 3, None = 4 }
    public enum StrokeAlignment { Inside = 0, Center = 1, Outside = 2 }
    public enum FillType { Solid = 0, LinearGradient = 1, RadialGradient = 2, AngularGradient = 3 }
    public enum BooleanOperation { None = 0, Union = 1, Subtraction = 2, Intersection = 3, XOR = 4 }
}