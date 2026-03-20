using System.Collections.Generic;
using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public interface IProceduralShape
    {
        ShapeType ShapeType { get; }
        Vector2 ShapeScale { get; }
        Vector4 CornerRadius { get; }
        float CornerSmoothing { get; }
        int PolygonSides { get; }
        float PolygonRounding { get; }
        int StarPoints { get; }
        float StarRatio { get; }
        float StarRoundingOuter { get; }
        float StarRoundingInner { get; }
        float CapsuleRounding { get; }
        Vector2 LineStart { get; }
        Vector2 LineEnd { get; }
        float LineWidth { get; }
        float RingInnerRadius { get; }
        float RingStartAngle { get; }
        float RingEndAngle { get; }
        ShapePath ShapePath { get; }
        
        float InternalPadding { get; }
        float EdgeSoftness { get; }
        float EdgeNoiseAmount { get; }
        float EdgeNoiseScale { get; }
        
        ShapeFill MainFill { get; }
        List<ProceduralEffect> Effects { get; }
        List<BooleanInput> BooleanOperations { get; }

        Rect GetRect();
        Vector2 GetGeometricCenterOffset();
        Vector4 GetPackedShapeParams();
        
        int MainFillAtlasIndex { get; }
        List<int> EffectAtlasIndices { get; }
        
        Matrix4x4 WorldToLocalMatrix { get; }
    }
}