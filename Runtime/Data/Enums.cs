namespace ProceduralShapes.Runtime
{
    /// <summary>
    /// Перечисление типов геометрических фигур.
    /// </summary>
    public enum ShapeType 
    { 
        /// <summary> Прямоугольник. </summary>
        Rectangle = 0, 
        /// <summary> Эллипс или круг. </summary>
        Ellipse = 1, 
        /// <summary> Многоугольник. </summary>
        Polygon = 2, 
        /// <summary> Звезда. </summary>
        Star = 3, 
        /// <summary> Капсула. </summary>
        Capsule = 4, 
        /// <summary> Линия. </summary>
        Line = 5, 
        /// <summary> Кольцо. </summary>
        Ring = 6, 
        /// <summary> Отсутствие фигуры. </summary>
        None = 7, 
        /// <summary> Произвольный путь. </summary>
        Path = 8, 
        /// <summary> Треугольник. </summary>
        Triangle = 9, 
        /// <summary> Сердце. </summary>
        Heart = 10 
    }

    /// <summary>
    /// Выравнивание обводки относительно границы фигуры.
    /// </summary>
    public enum StrokeAlignment 
    { 
        /// <summary> Внутри границы. </summary>
        Inside = 0, 
        /// <summary> По центру границы. </summary>
        Center = 1, 
        /// <summary> Снаружи границы. </summary>
        Outside = 2 
    }

    /// <summary>
    /// Тип заливки фигуры.
    /// </summary>
    public enum FillType 
    { 
        /// <summary> Сплошной цвет. </summary>
        Solid = 0, 
        /// <summary> Линейный градиент. </summary>
        LinearGradient = 1, 
        /// <summary> Радиальный градиент. </summary>
        RadialGradient = 2, 
        /// <summary> Угловой (конический) градиент. </summary>
        AngularGradient = 3, 
        /// <summary> Текстурный паттерн. </summary>
        Pattern = 4 
    }

    /// <summary>
    /// Булевы операции для объединения фигур.
    /// </summary>
    public enum BooleanOperation 
    { 
        /// <summary> Операция не применяется. </summary>
        None = 0, 
        /// <summary> Объединение фигур. </summary>
        Union = 1, 
        /// <summary> Вычитание (разность) фигур. </summary>
        Subtraction = 2, 
        /// <summary> Пересечение фигур. </summary>
        Intersection = 3, 
        /// <summary> Исключающее ИЛИ (XOR). </summary>
        XOR = 4 
    }
    
    /// <summary>
    /// Тип точки в пути фигуры.
    /// </summary>
    public enum PathPointType 
    { 
        /// <summary> Прямая линия до следующей точки. </summary>
        Line = 0, 
        /// <summary> Кривая Безье. </summary>
        Bezier = 1 
    }

    /// <summary>
    /// Тип наконечника линии (начала и конца).
    /// </summary>
    public enum LineCap 
    { 
        /// <summary> Плоский срез. </summary>
        Butt = 0, 
        /// <summary> Скругленный наконечник. </summary>
        Round = 1, 
        /// <summary> Квадратный наконечник. </summary>
        Square = 2 
    }

    /// <summary>
    /// Тип соединения сегментов линии.
    /// </summary>
    public enum LineJoint 
    { 
        /// <summary> Острое соединение (под углом). </summary>
        Miter = 0, 
        /// <summary> Скругленное соединение. </summary>
        Round = 1, 
        /// <summary> Скошенное соединение. </summary>
        Bevel = 2 
    }
}
