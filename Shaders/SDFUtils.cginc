#ifndef SDF_UTILS_INCLUDED
#define SDF_UTILS_INCLUDED

float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

float smax(float a, float b, float k) {
    return -smin(-a, -b, k);
}

float GetBasicSDF(float2 p, float2 halfSize, float shapeType, float smoothing, float4 params) {
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
        // Star (Corrected)
        float n = max(3.0, params.x);
        float ratio = params.y;
        float maxR = min(halfSize.x, halfSize.y);
        
        // Target visual radii
        float visualOuterR = maxR;
        float visualInnerR = maxR * ratio;
        
        // Rounding parameters
        float roundOuter = params.z * maxR * 0.5;
        float roundInner = params.w * maxR * 0.5;
        
        // Skeleton radii (compensate for offset)
        // Ensure skeleton doesn't invert
        float skelOuterR = visualOuterR - roundOuter;
        float skelInnerR = visualInnerR - roundOuter; // Both effectively expand by roundOuter
        
        // If skelInnerR becomes too small (or negative), we have artifacts.
        // We must clamp rounding if geometry is too tight.
        // But for visual consistency, let's just clamp the skeleton radius.
        // skelInnerR = max(skelInnerR, 0.001); // Prevent negative
        
        float an = 3.14159265 / n;
        float a = atan2(p.x, p.y);
        float bn = floor(a / (2.0 * an));
        float f1 = a - (bn + 0.5) * 2.0 * an;
        float f2 = f1 > 0.0 ? f1 - 2.0 * an : f1 + 2.0 * an;
        
        float2 p_sec1 = length(p) * float2(abs(sin(f1)), cos(f1));
        float2 p_sec2 = length(p) * float2(abs(sin(f2)), cos(f2));
        
        // Vertices of the skeleton sector
        float2 v1 = float2(0.0, skelOuterR);
        float2 v2 = float2(skelInnerR * sin(an), skelInnerR * cos(an));
        float2 ba = v2 - v1;
        
        // Distance to segment v1-v2 (one side)
        float2 pa1 = p_sec1 - v1;
        float h1 = clamp(dot(pa1, ba) / dot(ba, ba), 0.0, 1.0);
        float d1 = length(pa1 - ba * h1) * sign(pa1.y * ba.x - pa1.x * ba.y);
        
        // Distance to segment v1-v2 (other side)
        float2 pa2 = p_sec2 - v1;
        float h2 = clamp(dot(pa2, ba) / dot(ba, ba), 0.0, 1.0);
        float d2 = length(pa2 - ba * h2) * sign(pa2.y * ba.x - pa2.x * ba.y);
        
        // Smooth min for inner corners
        float k = roundInner * 2.0 + 0.001;
        // smin(d1, d2)
        float h = clamp(0.5 + 0.5 * (d2 - d1) / k, 0.0, 1.0);
        float d = lerp(d2, d1, h) - k * h * (1.0 - h);
        
        return d - roundOuter;
    }
    
    return 100000.0; // None
}

float CalculateCompoundSDF(float2 p, int count, float4 opTypes[8], float4 transforms[8], float4 sizes[8], float4 shapeParams[8]) 
{
    return 100000.0;
}

#endif