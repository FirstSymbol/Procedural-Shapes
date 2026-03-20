Shader "UI/ProceduralShapes/Shape"
{
    Properties
    {
        [HideInInspector] _MainTex ("Gradient Palette", 2D) = "white" {}
        _PatternTex ("Pattern Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AntiAliasing ("Edge Softness (AA)", Range(0.0, 5.0)) = 0.75
        _InternalPadding ("Internal Padding", Float) = 0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        // Mask Matrix (Child Local -> Mask SDF Space)
        _MaskMatrixX ("Mask Matrix X", Vector) = (1,0,0,0)
        _MaskMatrixY ("Mask Matrix Y", Vector) = (0,1,0,0)
        _MaskMatrixZ ("Mask Matrix Z", Vector) = (0,0,1,0)
        _MaskMatrixW ("Mask Matrix W", Vector) = (0,0,0,1)

        _MaskParams ("Mask Params", Vector) = (0,0,0,0) 
        _MaskSize ("Mask Size", Vector) = (0,0,0,0)
        _MaskShape ("Mask Shape Params", Vector) = (0,0,0,0)
        
        _MaskTex ("Mask Gradient Tex", 2D) = "white" {}
        _MaskFillParams ("Mask Fill Params", Vector) = (0,0,0,0) 
        _MaskFillOffset ("Mask Fill Offset", Vector) = (0,0,0,0) 
        
        _MaskBoolParams ("Mask Bool Count", Int) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha 
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local SHAPE_RECTANGLE SHAPE_ELLIPSE SHAPE_POLYGON SHAPE_STAR SHAPE_CAPSULE SHAPE_LINE SHAPE_RING SHAPE_PATH SHAPE_TRIANGLE SHAPE_HEART _
            #pragma multi_compile_local _ HAS_BOOLEANS
            #pragma multi_compile_local _ HAS_MASK
            #pragma multi_compile_local _ HAS_NOISE

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "SDFUtils.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_ui {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;   
                float4 tangent : TANGENT; 
                float4 texcoord0 : TEXCOORD0; 
                float4 texcoord1 : TEXCOORD1; 
                float4 texcoord2 : TEXCOORD2; 
                float4 texcoord3 : TEXCOORD3; 
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 worldPosition : TEXCOORD4;
                
                float4 baseData : TEXCOORD2;
                float4 shapeParams : TEXCOORD1;
                float4 fillParams : TEXCOORD3;
                
                float4 uv0 : TEXCOORD0; 
                float4 effectData : TEXCOORD5; 
                float4 precalc1 : TEXCOORD6; 
                float4 precalc2 : TEXCOORD7; 
                float4 extraData : TEXCOORD8; 
            };

            sampler2D _MainTex;
            sampler2D _PatternTex;
            float4 _ClipRect;
            
            int _BoolParams1; 
            float4 _BoolData_OpType[8];     
            float4 _BoolData_ShapeParams[8];
            float4 _BoolData_Transform[8];  
            float4 _BoolData_Size[8];       

            float4 _MaskMatrixX;
            float4 _MaskMatrixY;
            float4 _MaskMatrixZ;
            float4 _MaskMatrixW;

            float4 _MaskParams; 
            float4 _MaskSize;   
            float4 _MaskShape;  
            
            sampler2D _MaskTex;
            float4 _MaskFillParams;
            float4 _MaskFillOffset;
            
            int _MaskBoolParams;
            float4 _MaskBoolOpType[8];     
            float4 _MaskBoolShapeParams[8];
            float4 _MaskBoolTransform[8];  
            float4 _MaskBoolSize[8]; 

            v2f vert (appdata_ui v) {
                v2f o;
                float4 worldPos = float4(v.vertex.xyz, 1.0);
                o.vertex = UnityObjectToClipPos(worldPos);
                o.worldPosition = worldPos;
                o.worldPosition.w = v.tangent.y;
                o.color = v.color;
                
                o.shapeParams = v.texcoord1;
                o.baseData = v.texcoord2;
                o.fillParams = v.texcoord3;

                float effectType = v.texcoord2.w;
                float blur = 0.0;
                float aa = 1.0;
                float internalPadding = 0.0;
                float spread = v.tangent.x;
                
                float2 p_orig = v.texcoord0.xy;
                float2 p = p_orig;
                
                if (effectType == 1.0 || effectType == 3.0) { 
                    p -= v.normal.xy; 
                    blur = v.normal.z;
                    aa = max(v.tangent.y, 0.001); 
                } else { 
                    internalPadding = v.normal.x;
                    aa = max(v.normal.y, 0.001);
                    blur = v.normal.z;
                }

                o.uv0 = float4(p.x, p.y, p_orig.x, p_orig.y);
                o.effectData = float4(blur, aa, internalPadding, spread);
                o.precalc1 = float4(0,0,0,0);
                o.precalc2 = float4(v.texcoord0.z, v.texcoord0.w, 0, 0); 
                o.extraData = float4(0, v.tangent.y, v.tangent.z, v.tangent.w); 

                float2 halfSize = v.texcoord2.xy * 0.5;
                float4 params = v.texcoord1;

#if defined(SHAPE_POLYGON)
                float n = max(3.0, params.x); 
                float an = 3.14159265 / n;
                float maxR = min(halfSize.x, halfSize.y);
                float rounding = params.y * maxR * 0.5;
                float rOuter = maxR - rounding;
                o.precalc1 = float4(2.0 * an, rOuter * sin(an), rOuter * cos(an), rounding);
#elif defined(SHAPE_STAR)
                float n = max(3.0, params.x);
                float maxR = min(halfSize.x, halfSize.y);
                float ro = params.z * maxR * 0.5;
                float rOut = max(maxR - ro, 0.001);
                float rIn  = max(params.y * maxR - ro, 0.001);
                float an = 3.1415926535 / n;
                float2 p1 = float2(0.0, rOut);
                float2 p2 = float2(rIn * sin(an), rIn * cos(an));
                float2 ba = p2 - p1;
                float ba2 = max(dot(ba, ba), 0.00001);
                o.precalc1 = float4(2.0 * an, rOut, ba2, ro);
                o.precalc2.zw = p2;
                o.extraData.xy = float2(params.w * maxR, 0); 
#elif defined(SHAPE_CAPSULE)
                float r = params.x * min(halfSize.x, halfSize.y);
                o.precalc1 = float4(halfSize.x, halfSize.y, r, 0);
#elif defined(SHAPE_RING)
                float maxR = min(halfSize.x, halfSize.y);
                float innerR = params.x * maxR;
                float thickness = (maxR - innerR) * 0.5;
                float midR = (maxR + innerR) * 0.5;
                float2 p1 = midR * float2(sin(params.y), cos(params.y));
                float2 p2 = midR * float2(sin(params.z), cos(params.z));
                float targetDa = frac((params.z - params.y) / 6.28318);
                o.precalc1 = float4(midR, thickness, targetDa, 0);
                o.precalc2.zw = p1;
                o.extraData.xy = p2;
#endif
                return o;
            }

            float GetMainPerimeterMapping(float2 p, float2 halfSize, float4 params) {
#if defined(SHAPE_RECTANGLE)
                return GetAnyPerimeterMapping(p, halfSize, 0, params);
#elif defined(SHAPE_ELLIPSE)
                return GetAnyPerimeterMapping(p, halfSize, 1, params);
#elif defined(SHAPE_POLYGON)
                return GetAnyPerimeterMapping(p, halfSize, 2, params);
#elif defined(SHAPE_STAR)
                return GetAnyPerimeterMapping(p, halfSize, 3, params);
#elif defined(SHAPE_CAPSULE)
                return GetAnyPerimeterMapping(p, halfSize, 4, params);
#elif defined(SHAPE_LINE)
                return p.x;
#elif defined(SHAPE_RING)
                return GetAnyPerimeterMapping(p, halfSize, 6, params);
#elif defined(SHAPE_TRIANGLE)
                return GetAnyPerimeterMapping(p, halfSize, 9, params);
#else
                return (atan2(p.y, p.x) + 3.14159265) * (halfSize.x + halfSize.y) * 0.5;
#endif
            }

            float GetMainSDF(float2 p, float2 halfSize, float smoothing, float4 params) {
#if defined(SHAPE_RECTANGLE)
                return GetRectangleSDF(p, halfSize, smoothing, params);
#elif defined(SHAPE_ELLIPSE)
                return GetEllipseSDF(p, halfSize);
#elif defined(SHAPE_POLYGON)
                return GetPolygonSDF(p, halfSize, params);
#elif defined(SHAPE_STAR)
                return GetStarSDF(p, halfSize, params);
#elif defined(SHAPE_CAPSULE)
                return GetCapsuleSDF(p, halfSize, params);
#elif defined(SHAPE_LINE)
                return GetLineSDF(p, smoothing, params);
#elif defined(SHAPE_RING)
                return GetRingSDF(p, halfSize, params);
#elif defined(SHAPE_PATH)
                return GetPathSDF(p, params, false);
#elif defined(SHAPE_TRIANGLE)
                return GetTriangleSDF(p, halfSize);
#elif defined(SHAPE_HEART)
                return GetHeartSDF(p, halfSize);
#else
                return 100000.0;
#endif
            }



            float GetMainSDF_Optimized(float2 p, float2 halfSize, float smoothing, float4 params, v2f i) {
#if defined(SHAPE_RECTANGLE)
                return GetRectangleSDF(p, halfSize, smoothing, params);
#elif defined(SHAPE_ELLIPSE)
                return GetEllipseSDF(p, halfSize);
#elif defined(SHAPE_POLYGON)
                return GetPolygonSDF_Precalc(p, i.precalc1);
#elif defined(SHAPE_STAR)
                return GetStarSDF_Precalc(p, i.precalc1, i.precalc2, i.extraData.x);
#elif defined(SHAPE_CAPSULE)
                return GetCapsuleSDF_Precalc(p, i.precalc1);
#elif defined(SHAPE_LINE)
                return GetLineSDF(p, smoothing, params);
#elif defined(SHAPE_RING)
                return GetRingSDF_Precalc(p, params.y, i.precalc1, i.precalc2, i.extraData.xy);
#elif defined(SHAPE_PATH)
                return GetPathSDF(p, params, false);
#elif defined(SHAPE_TRIANGLE)
                return GetTriangleSDF(p, halfSize);
#elif defined(SHAPE_HEART)
                return GetHeartSDF(p, halfSize);
#else
                return 100000.0;
#endif
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 p = i.uv0.xy;
                float2 p_orig = i.uv0.zw;
                float customSmoothing = i.baseData.z;
                float effectType = i.baseData.w; 
                
                float blur = i.effectData.x;
                float aa = i.effectData.y;
                float internalPadding = i.effectData.z;
                float spread = i.effectData.w;
                
                float2 noiseOffset = 0;
#if defined(HAS_NOISE)
                float noiseAmount = frac(i.fillParams.z) * 100.0;
                float noiseScale = i.fillParams.w;
                if (noiseAmount > 0.001) {
                    float n = noise(p_orig * noiseScale * 0.1);
                    noiseOffset = (n * 2.0 - 1.0) * noiseAmount;
                }
#endif
                
                float2 halfSize = i.baseData.xy * 0.5;

                float d = 0;
                float d_orig = 0;
                
                bool needShadowD = (effectType == 1.0 || effectType == 3.0);
                bool needOrigD = (effectType != 1.0);

                float perimeter = 0;
                if (needOrigD) {
                    d_orig = GetMainSDF_Optimized(p_orig + noiseOffset, halfSize, customSmoothing, i.shapeParams, i);
                    if (effectType == 2.0) perimeter = GetMainPerimeterMapping(p_orig + noiseOffset, halfSize, i.shapeParams);
                }
                
                if (needShadowD) {
                    d = GetMainSDF_Optimized(p + noiseOffset, halfSize, customSmoothing, i.shapeParams, i);
                }

#if defined(HAS_BOOLEANS)
                int boolCount = _BoolParams1;
                if (boolCount > 0) {
                    for (int k = 0; k < 8; k++) {
                        if (k >= boolCount) break;
                        float boolOp = _BoolData_OpType[k].x;
                        float boolType = _BoolData_OpType[k].y;
                        float boolSmooth = _BoolData_OpType[k].z;
                        float smoothBlend = _BoolData_OpType[k].w; 
                        float4 boolTrans = _BoolData_Transform[k];
                        float2 boolSize = _BoolData_Size[k].xy;
                        float4 boolShapeParams = _BoolData_ShapeParams[k];

                        bool isPathOp = boolType > 7.5 && boolType < 8.5;

                        if (needOrigD) {
                            float2 p2_orig = p_orig - boolTrans.xy;
                            if (abs(boolTrans.z) > 0.0001 || abs(boolTrans.w - 1.0) > 0.0001) {
                                p2_orig = float2(p2_orig.x * boolTrans.w - p2_orig.y * boolTrans.z, p2_orig.x * boolTrans.z + p2_orig.y * boolTrans.w);
                            }
                            float d2_orig = GetBasicSDF(p2_orig + noiseOffset, boolSize * 0.5, boolType, boolSmooth, boolShapeParams, isPathOp);

                            if (effectType == 2.0) {
                                bool closer = false;
                                if (boolOp < 1.5) closer = d2_orig < d_orig; // Union
                                else if (boolOp < 2.5) closer = -d2_orig > d_orig; // Subtract
                                else if (boolOp < 3.5) closer = d2_orig > d_orig; // Intersect
                                
                                if (closer) {
                                    perimeter = GetAnyPerimeterMapping(p2_orig + noiseOffset, boolSize * 0.5, boolType, boolShapeParams);
                                }
                            }

                            if (smoothBlend > 0.001) d_orig = smin_op(d_orig, d2_orig, boolOp, smoothBlend);
                            else d_orig = hard_op(d_orig, d2_orig, boolOp);
                        }

                        if (needShadowD) {
                            float2 p2 = p - boolTrans.xy;
                            if (abs(boolTrans.z) > 0.0001 || abs(boolTrans.w - 1.0) > 0.0001) {
                                p2 = float2(p2.x * boolTrans.w - p2.y * boolTrans.z, p2.x * boolTrans.z + p2.y * boolTrans.w);
                            }
                            float d2 = GetBasicSDF(p2 + noiseOffset, boolSize * 0.5, boolType, boolSmooth, boolShapeParams, isPathOp);
                            if (smoothBlend > 0.001) d = smin_op(d, d2, boolOp, smoothBlend);
                            else d = hard_op(d, d2, boolOp);
                        }
                    }
                }
#endif
                
                if (needOrigD) d_orig += internalPadding;
                if (needShadowD) d += internalPadding;

                float mask = 0;

                if (effectType == 1.0) { // Drop Shadow
                    d -= spread;
                    mask = smoothstep(max(blur, aa), -max(blur, aa), d);
                }
                else if (effectType == 2.0) { // Stroke
                    float alignment = i.worldPosition.w; 
                    float strokeOffset = (alignment == 0) ? -spread * 0.5 : ((alignment == 2) ? spread * 0.5 : 0);
                    float strokeD = abs(d_orig - strokeOffset) - spread * 0.5;
                    float2 dashData = i.precalc2.xy;
                    if (dashData.x > 0.001) {
                        if (frac(perimeter / (dashData.x + dashData.y)) > (dashData.x / (dashData.x + dashData.y))) discard;
                    }
                    mask = smoothstep(aa, -aa, strokeD);
                }
                else if (effectType == 3.0) { // Inner Shadow/Glow
                    float baseD = d + spread;
                    mask = saturate(smoothstep(-max(blur, 0.001), 0, baseD));
                    mask = min(mask, smoothstep(aa, -aa, d_orig)); 
                }
                else if (effectType == 5.0) { // Bevel
                    mask = 1.0; 
                } else { // Main Fill, Blur
                    mask = smoothstep(max(blur, aa), -max(blur, aa), d_orig);
                }

                if (mask <= 0.001) discard;

                float rowIndex = floor(i.fillParams.x + 0.5);
                float fillType = i.fillParams.y;
                float gradAngle = i.fillParams.z;
                float gradScale = i.fillParams.w;
                float2 gradOffset = float2(i.extraData.z, i.extraData.w); // originally i.tangent.zw

                float4 colorSample;
                if (fillType > 3.5) { // Pattern
                    float2 patternUV = (p_orig / halfSize * 0.5 + 0.5) * gradScale + gradOffset;
                    colorSample = tex2D(_PatternTex, patternUV);
                } else {
                    float t = 0.5;
                    if (fillType > 0.5) {
                        float2 gradP = p_orig - (halfSize * gradOffset);
                        gradP /= max(gradScale, 0.001);
                        if (fillType < 1.5) { // Linear
                            float rad = gradAngle * 0.0174533;
                            float2 dir = float2(cos(rad), sin(rad));
                            t = (dot(gradP, dir) / max(abs(dir.x*halfSize.x)+abs(dir.y*halfSize.y), 0.001)) * 0.5 + 0.5;
                        } else if (fillType < 2.5) { // Radial
                            t = length(gradP) / max(max(halfSize.x, halfSize.y), 0.001);
                        } else if (fillType < 3.5) { // Angular
                            t = frac((atan2(gradP.y, gradP.x) - gradAngle * 0.0174533) / 6.28318 + 0.5);
                        }
                    }
                    float vCoord = (rowIndex * 3.0 + 1.5) / 512.0;
                    colorSample = tex2D(_MainTex, float2(saturate(t), vCoord));
                }

                float4 finalColor = colorSample * i.color;
                
                if (effectType == 5.0) {
                    float dist = max(blur, 0.5); // blur holds normal.z in effectType 5 (bevel distance)
                    float2 bDir = float2(cos(i.extraData.z), sin(i.extraData.z)); // extraData.z holds tangent.z
                    float diff = GetMainSDF_Optimized(p_orig + noiseOffset + bDir * dist, halfSize, customSmoothing, i.shapeParams, i) - 
                                 GetMainSDF_Optimized(p_orig + noiseOffset - bDir * dist, halfSize, customSmoothing, i.shapeParams, i);
                    float highlight = saturate(diff / (dist * 2.0)) * i.effectData.w;
                    float shadow = saturate(-diff / (dist * 2.0)) * i.worldPosition.w;
                    float baseMask = smoothstep(aa, -aa, d_orig);
                    if (baseMask <= 0.001) discard;
                    finalColor = (shadow > highlight) ? float4(0,0,0, shadow) : float4(1,1,1, highlight);
                    mask = baseMask;
                }
                
                finalColor.a *= mask; 
                finalColor.rgb *= finalColor.a;

#if defined(HAS_MASK)
                if (_MaskParams.x > 0.5) {
                    float4x4 localToMaskSDF = float4x4(_MaskMatrixX, _MaskMatrixY, _MaskMatrixZ, _MaskMatrixW);
                    float2 maskP = mul(localToMaskSDF, float4(p_orig, 0.0, 1.0)).xy;
                    
                    float maskType = _MaskParams.y;
                    float maskSmooth = _MaskParams.z;
                    float maskFeather = _MaskParams.w;
                    
                    float mD = GetBasicSDF(maskP, _MaskSize.xy * 0.5, maskType, maskSmooth, _MaskShape);
                    
                    int mBoolCount = _MaskBoolParams;
                    for (int mk = 0; mk < 8; mk++) {
                        if (mk >= mBoolCount) break;
                        float mbOp = _MaskBoolOpType[mk].x;
                        float mbType = _MaskBoolOpType[mk].y;
                        float mbSmooth = _MaskBoolOpType[mk].z;
                        float mbSmoothBlend = _MaskBoolOpType[mk].w;
                        float4 mbTrans = _MaskBoolTransform[mk];
                        float2 mbSize = _MaskBoolSize[mk].xy;
                        float4 mbShapeParams = _MaskBoolShapeParams[mk];

                        float2 mp2 = maskP - mbTrans.xy;
                        if (abs(mbTrans.z) > 0.0001) {
                            float s = sin(-mbTrans.z); float c = cos(-mbTrans.z);
                            mp2 = float2(mp2.x * c - mp2.y * s, mp2.x * s + mp2.y * c);
                        }
                        float md2 = GetBasicSDF(mp2, mbSize * 0.5, mbType, mbSmooth, mbShapeParams);
                        if (mbSmoothBlend > 0.001) mD = smin_op(mD, md2, mbOp, mbSmoothBlend);
                        else mD = hard_op(mD, md2, mbOp);
                    }
                    
                    float mAlpha = smoothstep(max(0.001, maskFeather), -max(0.001, maskFeather), mD);
                    
                    float mFillType = _MaskFillParams.x;
                    float mGradAngle = _MaskFillParams.y;
                    float mGradScale = _MaskFillParams.z;
                    float mRowIndex = _MaskFillParams.w;
                    float2 mGradOffset = _MaskFillOffset.xy; 
                    float mBaseAlpha = _MaskFillOffset.z;

                    float2 mHalfSize = _MaskSize.xy * 0.5;
                    float2 mGradP = maskP - (mHalfSize * mGradOffset);
                    mGradP /= max(mGradScale, 0.001);

                    float mt = 0.5;
                    if (mFillType > 0.5) {
                        if (mFillType < 1.5) {
                            float rad = mGradAngle * 0.0174533;
                            float2 dir = float2(cos(rad), sin(rad));
                            mt = (dot(mGradP, dir) / max(abs(dir.x*mHalfSize.x)+abs(dir.y*mHalfSize.y), 0.001)) * 0.5 + 0.5;
                        } else if (mFillType < 2.5) {
                            mt = length(mGradP) / max(max(mHalfSize.x, mHalfSize.y), 0.001);
                        } else if (mFillType < 3.5) {
                            mt = frac((atan2(mGradP.y, mGradP.x) - mGradAngle * 0.0174533) / 6.28318 + 0.5);
                        }
                    }
                    
                    float mVCoord = (mRowIndex * 3.0 + 1.5) / 512.0;
                    float mFillAlpha = 1.0;
                    if (mFillType > 0.5) { 
                         mFillAlpha = tex2D(_MaskTex, float2(saturate(mt), mVCoord)).a;
                    } else {
                         mFillAlpha = tex2D(_MaskTex, float2(0.5, mVCoord)).a; 
                    }

                    float mTotalAlpha = mAlpha * mFillAlpha * mBaseAlpha;
                    float oldAlpha = finalColor.a;
                    finalColor.a = min(oldAlpha, mTotalAlpha);
                    finalColor.rgb *= (finalColor.a / max(oldAlpha, 0.0001));
                }
#endif

                if (finalColor.a <= 0.001) discard;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}