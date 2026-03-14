using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public static class SDFMathUtils
    {
        public static float GetSDF_CPU(Vector2 p, Vector2 halfSize, ShapeType type, float smoothing, Vector4 params4)
        {
            float minHalfSize = Mathf.Min(halfSize.x, halfSize.y);
            switch (type)
            {
                case ShapeType.Rectangle:
                {
                    float r = 0;
                    if (p.x < 0 && p.y > 0) r = params4.x;
                    else if (p.x >= 0 && p.y > 0) r = params4.y;
                    else if (p.x >= 0 && p.y <= 0) r = params4.z;
                    else if (p.x < 0 && p.y <= 0) r = params4.w;
                    r = Mathf.Min(r, minHalfSize);
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(r, r);
                    return Mathf.Min(Mathf.Max(q.x, q.y), 0.0f) + Vector2.Max(q, Vector2.zero).magnitude - r;
                }

                case ShapeType.Ellipse:
                    return (new Vector2(p.x / halfSize.x, p.y / halfSize.y).magnitude - 1.0f) * minHalfSize;

                case ShapeType.Polygon:
                {
                    float n = Mathf.Max(3.0f, params4.x);
                    float an = Mathf.PI / n;
                    float a = Mathf.Atan2(p.x, p.y);
                    float bn = Mathf.Floor(a / (2.0f * an));
                    float f = a - (bn + 0.5f) * 2.0f * an;
                    Vector2 p_sec = new Vector2(p.magnitude * Mathf.Abs(Mathf.Sin(f)), p.magnitude * Mathf.Cos(f));
                    float rounding = params4.y * minHalfSize * 0.5f;
                    float rOuter = minHalfSize - rounding;
                    Vector2 closest = new Vector2(Mathf.Clamp(p_sec.x, -rOuter * Mathf.Sin(an), rOuter * Mathf.Sin(an)), rOuter * Mathf.Cos(an));
                    return (p_sec - closest).magnitude * Mathf.Sign(p_sec.y - closest.y) - rounding;
                }

                case ShapeType.Star:
                {
                    float n = Mathf.Max(3.0f, params4.x);
                    float maxR = minHalfSize;
                    
                    float ro = params4.z * maxR * 0.5f;
                    float rOut = Mathf.Max(maxR - ro, 0.001f);
                    float rIn  = Mathf.Max(params4.y * maxR - ro, 0.001f);
                    
                    float an = Mathf.PI / n;
                    float a = Mathf.Atan2(p.x, p.y);
                    float f = Mathf.Abs(a) % (2.0f * an);
                    if (f > an) f = 2.0f * an - f;
                    
                    Vector2 q0 = p.magnitude * new Vector2(Mathf.Sin(f), Mathf.Cos(f));
                    Vector2 q1 = p.magnitude * new Vector2(Mathf.Sin(2.0f * an - f), Mathf.Cos(2.0f * an - f));
                    
                    Vector2 p1 = new Vector2(0.0f, rOut);
                    Vector2 p2 = new Vector2(rIn * Mathf.Sin(an), rIn * Mathf.Cos(an));
                    
                    Vector2 ba = p2 - p1;
                    float ba2 = Mathf.Max(Vector2.Dot(ba, ba), 0.00001f);
                    
                    Vector2 pa0 = q0 - p1;
                    float h0 = Mathf.Clamp01(Vector2.Dot(pa0, ba) / ba2);
                    Vector2 d0 = pa0 - ba * h0;
                    float s0 = (pa0.y * ba.x - pa0.x * ba.y >= 0.0f) ? 1.0f : -1.0f;
                    float dist0 = d0.magnitude * s0;
                    
                    Vector2 pa1 = q1 - p1;
                    float h1 = Mathf.Clamp01(Vector2.Dot(pa1, ba) / ba2);
                    Vector2 d1 = pa1 - ba * h1;
                    float s1 = (pa1.y * ba.x - pa1.x * ba.y >= 0.0f) ? 1.0f : -1.0f;
                    float dist1 = d1.magnitude * s1;
                    
                    float rInner = params4.w * maxR;
                    float finalDist = dist0;
                    if (rInner > 0.001f)
                    {
                        float h = Mathf.Clamp01(0.5f + 0.5f * (dist1 - dist0) / rInner);
                        finalDist = Mathf.Lerp(dist1, dist0, h) - rInner * h * (1.0f - h);
                    }
                    
                    return finalDist - ro;
                }

                case ShapeType.Capsule:
                    float cr = params4.x * minHalfSize;
                    Vector2 ch = Vector2.Max(halfSize - new Vector2(cr, cr), Vector2.zero);
                    Vector2 cq = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - ch;
                    return Vector2.Max(cq, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(cq.x, cq.y), 0.0f) - cr;

                case ShapeType.Line:
                {
                    Vector2 pa = p - new Vector2(params4.x, params4.y);
                    Vector2 ba = new Vector2(params4.z, params4.w) - new Vector2(params4.x, params4.y);
                    float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                    return (pa - ba * h).magnitude - smoothing * 0.5f;
                }

                case ShapeType.Ring:
                {
                    float innerR = params4.x * minHalfSize;
                    float thickness = (minHalfSize - innerR) * 0.5f;
                    float midR = (minHalfSize + innerR) * 0.5f;
                    float d = Mathf.Abs(p.magnitude - midR) - thickness;
                    if (Mathf.Abs(params4.z - params4.y) < Mathf.PI * 2.0f)
                    {
                        float ang = Mathf.Atan2(p.x, p.y);
                        float da = frac((ang - params4.y) / (Mathf.PI * 2.0f));
                        float targetDa = frac((params4.z - params4.y) / (Mathf.PI * 2.0f));
                        if (da > targetDa)
                        {
                            Vector2 p1 = new Vector2(Mathf.Sin(params4.y), Mathf.Cos(params4.y)) * midR;
                            Vector2 p2 = new Vector2(Mathf.Sin(params4.z), Mathf.Cos(params4.z)) * midR;
                            d = Mathf.Max(d, Mathf.Min((p - p1).magnitude, (p - p2).magnitude) - thickness);
                        }
                    }
                    return d;
                }

                case ShapeType.Triangle:
                {
                    float r = Mathf.Min(halfSize.x, halfSize.y);
                    Vector2 pt = p;
                    pt.y += r * 0.25f;
                    float k = Mathf.Sqrt(3.0f);
                    pt.x = Mathf.Abs(pt.x) - r;
                    pt.y = pt.y + r / k;
                    if (pt.x + k * pt.y > 0.0f) pt = new Vector2(pt.x - k * pt.y, -k * pt.x - pt.y) / 2.0f;
                    pt.x -= Mathf.Clamp(pt.x, -2.0f * r, 0.0f);
                    return -pt.magnitude * Mathf.Sign(pt.y);
                }
                case ShapeType.Heart:
                {
                    float r = Mathf.Min(halfSize.x, halfSize.y);
                    Vector2 pt = p;
                    pt.x = Mathf.Abs(pt.x);
                    pt.y += r * 0.5f;
                    pt /= r;
                    
                    float d = 0;
                    if( pt.y+pt.x>1.0f )
                        d = Mathf.Sqrt(Vector2.SqrMagnitude(pt - new Vector2(0.25f,0.75f))) - 0.3535534f;
                    else
                        d = Mathf.Sqrt(Mathf.Min(Vector2.SqrMagnitude(pt - new Vector2(0.00f,1.00f)),
                                     Vector2.SqrMagnitude(pt - 0.5f*Mathf.Max(pt.x+pt.y,0.0f)*Vector2.one))) * Mathf.Sign(pt.x-pt.y);
                    return d * r;
                }
                default:
                    return 100000.0f;
            }
        }

        public static float frac(float x) => x - Mathf.Floor(x);
    }
}