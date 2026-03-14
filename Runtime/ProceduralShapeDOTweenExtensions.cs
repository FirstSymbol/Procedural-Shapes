using UnityEngine;
#if UNITY_DOTWEEN
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
#endif

namespace ProceduralShapes.Runtime
{
    public static class ProceduralShapeDOTweenExtensions
    {
#if UNITY_DOTWEEN
        public static TweenerCore<Color, Color, ColorOptions> DOColor(this ProceduralShape target, Color endValue, float duration)
        {
            return DOTween.To(() => target.color, x => target.color = x, endValue, duration).SetTarget(target);
        }

        public static TweenerCore<Vector2, Vector2, VectorOptions> DOShapeScale(this ProceduralShape target, Vector2 endValue, float duration)
        {
            return DOTween.To(() => target.ShapeScale, x => target.ShapeScale = x, endValue, duration).SetTarget(target);
        }

        public static TweenerCore<float, float, FloatOptions> DOCornerSmoothing(this ProceduralShape target, float endValue, float duration)
        {
            return DOTween.To(() => target.m_CornerSmoothing, x => { target.m_CornerSmoothing = x; target.SetAllDirty(); }, endValue, duration).SetTarget(target);
        }

        public static TweenerCore<Vector4, Vector4, VectorOptions> DOCornerRadius(this ProceduralShape target, Vector4 endValue, float duration)
        {
            return DOTween.To(() => target.m_CornerRadius, x => { target.m_CornerRadius = x; target.SetAllDirty(); }, endValue, duration).SetTarget(target);
        }

        public static TweenerCore<float, float, FloatOptions> DOInternalPadding(this ProceduralShape target, float endValue, float duration)
        {
            return DOTween.To(() => target.InternalPadding, x => target.InternalPadding = x, endValue, duration).SetTarget(target);
        }
#endif
    }
}
