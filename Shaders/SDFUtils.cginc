#ifndef SDF_UTILS_INCLUDED
#define SDF_UTILS_INCLUDED

uniform float4 _PathData[64];
uniform int _PathPointCount;

uniform float4 _BoolPathData[64];
uniform int _BoolPathPointCount;

float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

float smax(float a, float b, float k) {
    return -smin(-a, -b, k);
}

float smin_op(float d1, float d2, float op, float k) {
    if (op < 1.5) return smin(d1, d2, k);
    if (op < 2.5) return smax(d1, -d2, k);
    if (op < 3.5) return smax(d1, d2, k);
    return d1;
}

float hard_op(float d1, float d2, float op) {
    if (op < 1.5) return min(d1, d2); 
    if (op < 2.5) return max(d1, -d2); 
    if (op < 3.5) return max(d1, d2); 
    if (op < 4.5) return max(min(d1, d2), -max(d1, d2)); 
    return d1;
}

float GetPerimeterMapping(float2 p, float2 halfSize, float shapeType) {
    if (shapeType < 0.5) { // Rectangle
        float w = halfSize.x;
        float h = halfSize.y;
        float2 absP = abs(p);
        if (absP.x * h > absP.y * w) {
            if (p.x > 0) return 2.0 * w + (h - p.y);
            else return 4.0 * w + 2.0 * h + (p.y + h);
        } else {
            if (p.y > 0) return p.x + w;
            else return 2.0 * w + 2.0 * h + (w - p.x);
        }
    }
    return (atan2(p.y, p.x) + 3.14159265) * (halfSize.x + halfSize.y) * 0.5;
}

float hash(float2 p) {
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float noise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
}

float GetBasicSDF(float2 p, float2 halfSize, float shapeType, float smoothing, float4 params, bool isOperator = false) {
    if (shapeType < 0.5) { 
        // Rectangle
        float r = 0;
        if (p.x < 0 && p.y > 0) r = params.x;
        else if (p.x >= 0 && p.y > 0) r = params.y;
        else if (p.x >= 0 && p.y <= 0) r = params.z;
        else if (p.x < 0 && p.y <= 0) r = params.w;
        
        r = min(r, min(halfSize.x, halfSize.y));
        float2 q = abs(p) - halfSize + r;
        
        if (smoothing > 0.001 && r > 0.001) {
            float n = lerp(2.0, 4.5, smoothing); 
            float2 q0 = max(q, 0.0);
            float cornerDist = pow(pow(abs(q0.x), n) + pow(abs(q0.y), n), 1.0 / n);
            return min(max(q.x, q.y), 0.0) + cornerDist - r;
        }
        return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
    }
    else if (shapeType < 1.5) {
        // Ellipse
        return (length(p / halfSize) - 1.0) * min(halfSize.x, halfSize.y);
    }
    else if (shapeType < 2.5) {
        // Polygon
        float n = max(3.0, params.x); 
        float an = 3.14159265 / n;
        float a = atan2(p.x, p.y); 
        float bn = floor(a / (2.0 * an));
        float f = a - (bn + 0.5) * 2.0 * an; 
        float2 p_sec = length(p) * float2(abs(sin(f)), cos(f)); 
        float maxR = min(halfSize.x, halfSize.y);
        float rounding = params.y * maxR * 0.5;
        float rOuter = maxR - rounding;
        float2 closest = float2(clamp(p_sec.x, -rOuter * sin(an), rOuter * sin(an)), rOuter * cos(an));
        return length(p_sec - closest) * sign(p_sec.y - closest.y) - rounding;
    }
    else if (shapeType < 3.5) {
        // Star
        float n = max(3.0, params.x);
        float maxR = min(halfSize.x, halfSize.y);
        float ro = params.z * maxR * 0.5;
        float rOut = max(maxR - ro, 0.001);
        float rIn  = max(params.y * maxR - ro, 0.001);
        float an = 3.1415926535 / n;
        float a = atan2(p.x, p.y);
        float f = fmod(abs(a), 2.0 * an);
        if (f > an) f = 2.0 * an - f;
        float2 q0 = length(p) * float2(sin(f), cos(f));
        float2 q1 = length(p) * float2(sin(2.0 * an - f), cos(2.0 * an - f));
        float2 p1 = float2(0.0, rOut);
        float2 p2 = float2(rIn * sin(an), rIn * cos(an));
        float2 ba = p2 - p1;
        float ba2 = max(dot(ba, ba), 0.00001);
        float2 pa0 = q0 - p1;
        float h0 = clamp(dot(pa0, ba) / ba2, 0.0, 1.0);
        float2 d0 = pa0 - ba * h0;
        float s0 = (pa0.y * ba.x - pa0.x * ba.y >= 0.0) ? 1.0 : -1.0;
        float dist0 = length(d0) * s0;
        float2 pa1 = q1 - p1;
        float h1 = clamp(dot(pa1, ba) / ba2, 0.0, 1.0);
        float2 d1 = pa1 - ba * h1;
        float s1 = (pa1.y * ba.x - pa1.x * ba.y >= 0.0) ? 1.0 : -1.0;
        float dist1 = length(d1) * s1;
        float rInner = params.w * maxR;
        float finalDist = dist0;
        if (rInner > 0.001) finalDist = smin(dist0, dist1, rInner);
        return finalDist - ro;
    }
    else if (shapeType < 4.5) {
        // Capsule
        float r = params.x * min(halfSize.x, halfSize.y);
        float2 h = max(halfSize - r, 0.0);
        return length(p - clamp(p, -h, h)) - r;
    }
    else if (shapeType < 5.5) {
        // Line
        float2 a = params.xy;
        float2 b = params.zw;
        float thickness = smoothing * 0.5; 
        float2 pa = p - a, ba = b - a;
        float h = clamp(dot(pa, ba) / max(dot(ba, ba), 0.0001), 0.0, 1.0);
        return length(pa - ba * h) - thickness;
    }
    else if (shapeType < 6.5) {
        // Ring
        float maxR = min(halfSize.x, halfSize.y);
        float innerR = params.x * maxR;
        float thickness = (maxR - innerR) * 0.5;
        float midR = (maxR + innerR) * 0.5;
        float d = abs(length(p) - midR) - thickness;
        if (abs(params.z - params.y) < 6.28) {
            float a = atan2(p.x, p.y);
            float da = frac((a - params.y) / 6.28318);
            float targetDa = frac((params.z - params.y) / 6.28318);
            if (da > targetDa) {
                float2 p1 = midR * float2(sin(params.y), cos(params.y));
                float2 p2 = midR * float2(sin(params.z), cos(params.z));
                d = max(d, min(length(p - p1), length(p - p2)) - thickness);
            }
        }
        return d;
    }
    else if (shapeType < 8.5) {
        // Path
        bool isClosed = params.x > 0.5;
        float thickness = params.y;
        int count = isOperator ? _BoolPathPointCount : _PathPointCount;
        if (count < 2) return 100000.0;
        float d = 1e10;
        float s = 1.0;
        if (isClosed) {
            float2 pt0 = isOperator ? _BoolPathData[0].xy : _PathData[0].xy;
            d = dot(p - pt0, p - pt0);
            for(int i = 0, j = count - 1; i < count; j = i, i++) {
                int i1 = i / 2; int i2 = j / 2;
                float4 di = isOperator ? _BoolPathData[i1] : _PathData[i1];
                float4 dj = isOperator ? _BoolPathData[i2] : _PathData[i2];
                float2 vi = (i % 2 == 0) ? di.xy : di.zw;
                float2 vj = (j % 2 == 0) ? dj.xy : dj.zw;
                float2 e = vj - vi; float2 w = p - vi;
                float2 b = w - e * clamp(dot(w, e) / max(dot(e, e), 0.0001), 0.0, 1.0);
                d = min(d, dot(b, b));
                bool cond1 = p.y >= vi.y; bool cond2 = p.y < vj.y; bool cond3 = e.x * w.y > e.y * w.x;
                if((cond1 && cond2 && cond3) || (!cond1 && !cond2 && !cond3)) s *= -1.0;
            }
            return s * sqrt(d);
        } else {
            for(int i = 0; i < count - 1; i++) {
                int i1 = i / 2; int i2 = (i + 1) / 2;
                float4 di = isOperator ? _BoolPathData[i1] : _PathData[i1];
                float4 dj = isOperator ? _BoolPathData[i2] : _PathData[i2];
                float2 vi = (i % 2 == 0) ? di.xy : di.zw;
                float2 vj = ((i + 1) % 2 == 0) ? dj.xy : dj.zw;
                float2 e = vj - vi; float2 w = p - vi;
                float h = clamp(dot(w, e) / max(dot(e, e), 0.0001), 0.0, 1.0);
                float2 b = w - e * h; d = min(d, dot(b, b));
            }
            return sqrt(d) - thickness * 0.5;
        }
    }
    else if (shapeType < 9.5) {
        // Triangle
        float r = min(halfSize.x, halfSize.y);
        p.y += r * 0.25; 
        const float k = 1.7320508;
        p.x = abs(p.x) - r;
        p.y = p.y + r/k;
        if( p.x+k*p.y>0.0 ) p = float2(p.x-k*p.y,-k*p.x-p.y)/2.0;
        p.x -= clamp( p.x, -2.0*r, 0.0 );
        return -length(p)*sign(p.y);
    }
    else if (shapeType < 10.5) {
        // Heart
        float r = min(halfSize.x, halfSize.y);
        p.x = abs(p.x); p.y += r * 0.5; p /= r; 
        float d = 0;
        if( p.y+p.x>1.0 ) {
            float2 diff = p - float2(0.25, 0.75);
            d = sqrt(dot(diff, diff)) - 0.3535534;
        } else {
            float2 diff1 = p - float2(0.00, 1.00);
            float2 diff2 = p - 0.5 * max(p.x+p.y, 0.0);
            d = sqrt(min(dot(diff1, diff1), dot(diff2, diff2))) * sign(p.x-p.y);
        }
        return d * r;
    }
    return 100000.0;
}

float CalculateCompoundSDF(float2 p, int count, float4 opTypes[8], float4 transforms[8], float4 sizes[8], float4 shapeParams[8]) 
{
    return 100000.0;
}

#endif