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
                float3 normal : NORMAL; 
                float4 tangent : TANGENT;
                float4 uv0 : TEXCOORD0;
                float4 shapeParams : TEXCOORD1;
                float4 baseData : TEXCOORD2;
                float4 fillParams : TEXCOORD3;
                float4 worldPosition : TEXCOORD4;
            };

            sampler2D _MainTex;
            sampler2D _PatternTex;
            float4 _ClipRect;
            
            int _BoolParams1; 
            float4 _BoolData_OpType[8];     
            float4 _BoolData_ShapeParams[8];
            float4 _BoolData_Transform[8];  
            float4 _BoolData_Size[8];       

            // Mask Matrix
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
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.color = v.color;
                o.normal = v.normal;
                o.tangent = v.tangent;
                o.uv0 = v.texcoord0;
                o.shapeParams = v.texcoord1;
                o.baseData = v.texcoord2;
                o.fillParams = v.texcoord3;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 p_orig = i.uv0.xy;
                float2 p = p_orig;
                float shapeType = floor(i.baseData.z);
                float customSmoothing = frac(i.baseData.z) / 0.99 * 1000.0;
                float effectType = i.baseData.w; 
                
                float aa = 1.0;
                float blur = 0.0;
                float internalPadding = 0.0;
                
                if (effectType == 1.0 || effectType == 3.0) { // Shadows
                    p -= i.normal.xy; // Original project offset logic
                    blur = i.normal.z;
                    aa = max(i.tangent.y, 0.001); 
                } else { // Main Fill, Stroke, Blur
                    internalPadding = i.normal.x;
                    aa = max(i.normal.y, 0.001);
                    blur = i.normal.z;
                }
                
                float noiseAmount = frac(i.fillParams.z) * 100.0;
                float noiseScale = i.fillParams.w;
                float2 noiseOffset = 0;
                if (noiseAmount > 0.001) {
                    float n = noise(p_orig * noiseScale * 0.1);
                    noiseOffset = (n * 2.0 - 1.0) * noiseAmount;
                }
                
                float2 halfSize = i.baseData.xy * 0.5;

                // Base Distances
                float d = GetBasicSDF(p + noiseOffset, halfSize, shapeType, customSmoothing, i.shapeParams);
                float d_orig = GetBasicSDF(p_orig + noiseOffset, halfSize, shapeType, customSmoothing, i.shapeParams);

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

                        // Sample shifted (d)
                        float2 p2 = p - boolTrans.xy;
                        if (abs(boolTrans.z) > 0.0001) {
                            float s = sin(-boolTrans.z); float c = cos(-boolTrans.z);
                            p2 = float2(p2.x * c - p2.y * s, p2.x * s + p2.y * c);
                        }
                        float d2 = GetBasicSDF(p2 + noiseOffset, boolSize * 0.5, boolType, boolSmooth, boolShapeParams);
                        if (smoothBlend > 0.001) d = smin_op(d, d2, boolOp, smoothBlend);
                        else d = hard_op(d, d2, boolOp);

                        // Sample original (d_orig)
                        float2 p2_orig = p_orig - boolTrans.xy;
                        if (abs(boolTrans.z) > 0.0001) {
                            float s = sin(-boolTrans.z); float c = cos(-boolTrans.z);
                            p2_orig = float2(p2_orig.x * c - p2_orig.y * s, p2_orig.x * s + p2_orig.y * c);
                        }
                        float d2_orig = GetBasicSDF(p2_orig + noiseOffset, boolSize * 0.5, boolType, boolSmooth, boolShapeParams);
                        if (smoothBlend > 0.001) d_orig = smin_op(d_orig, d2_orig, boolOp, smoothBlend);
                        else d_orig = hard_op(d_orig, d2_orig, boolOp);
                    }
                }
                
                d += internalPadding;
                d_orig += internalPadding;

                float spread = i.tangent.x;
                float mask = 0;

                if (effectType == 1.0) { // Drop Shadow
                    d -= spread;
                    mask = smoothstep(max(blur, aa), -max(blur, aa), d);
                }
                else if (effectType == 2.0) { // Stroke
                    float alignment = i.tangent.y;
                    float strokeOffset = (alignment == 0) ? -spread * 0.5 : ((alignment == 2) ? spread * 0.5 : 0);
                    float strokeD = abs(d_orig - strokeOffset) - spread * 0.5;
                    if (i.uv0.z > 0.001) {
                        float perimeter = (shapeType == 5.0) ? (p_orig + noiseOffset).x : GetPerimeterMapping(p_orig + noiseOffset, halfSize, shapeType);
                        if (frac(perimeter / (i.uv0.z + i.uv0.w)) > (i.uv0.z / (i.uv0.z + i.uv0.w))) discard;
                    }
                    mask = smoothstep(aa, -aa, strokeD);
                }
                else if (effectType == 3.0) { // Inner Shadow/Glow
                    float baseD = d + spread;
                    mask = saturate(smoothstep(-max(blur, 0.001), 0, baseD));
                    mask *= smoothstep(aa, -aa, d_orig); 
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
                float2 gradOffset = i.tangent.zw;

                float4 colorSample;
                if (fillType > 3.5) { // Pattern
                    float2 patternUV = (p_orig / halfSize * 0.5 + 0.5) * gradScale + gradOffset;
                    colorSample = tex2D(_PatternTex, patternUV);
                } else {
                    float2 gradP = p_orig - (halfSize * gradOffset);
                    gradP /= max(gradScale, 0.001);
                    float t = 0.5;
                    if (fillType > 0.5 && fillType < 1.5) { // Linear
                        float rad = gradAngle * 0.0174533;
                        float2 dir = float2(cos(rad), sin(rad));
                        t = (dot(gradP, dir) / max(abs(dir.x*halfSize.x)+abs(dir.y*halfSize.y), 0.001)) * 0.5 + 0.5;
                    } else if (fillType > 1.5 && fillType < 2.5) { // Radial
                        t = length(gradP) / max(max(halfSize.x, halfSize.y), 0.001);
                    } else if (fillType > 2.5 && fillType < 3.5) { // Angular
                        t = frac((atan2(gradP.y, gradP.x) - gradAngle * 0.0174533) / 6.28318 + 0.5);
                    }
                    float vCoord = (rowIndex * 3.0 + 1.5) / 512.0;
                    colorSample = tex2D(_MainTex, float2(saturate(t), vCoord));
                }

                float4 finalColor = colorSample * i.color;
                
                if (effectType == 5.0) {
                    float dist = max(i.normal.z, 0.5);
                    float2 bDir = float2(cos(i.tangent.z), sin(i.tangent.z));
                    float diff = GetBasicSDF(p_orig + noiseOffset + bDir * dist, halfSize, shapeType, customSmoothing, i.shapeParams) - 
                                 GetBasicSDF(p_orig + noiseOffset - bDir * dist, halfSize, shapeType, customSmoothing, i.shapeParams);
                    float highlight = saturate(diff / (dist * 2.0)) * i.tangent.x;
                    float shadow = saturate(-diff / (dist * 2.0)) * i.tangent.y;
                    float baseMask = smoothstep(aa, -aa, d_orig);
                    if (baseMask <= 0.001) discard;
                    finalColor = (shadow > highlight) ? float4(0,0,0, shadow) : float4(1,1,1, highlight);
                    mask = baseMask;
                }
                
                finalColor.a *= mask; 
                finalColor.rgb *= finalColor.a;

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
                    if (mFillType > 0.5 && mFillType < 1.5) {
                        float rad = mGradAngle * 0.0174533;
                        float2 dir = float2(cos(rad), sin(rad));
                        mt = (dot(mGradP, dir) / max(abs(dir.x*mHalfSize.x)+abs(dir.y*mHalfSize.y), 0.001)) * 0.5 + 0.5;
                    } else if (mFillType > 1.5 && mFillType < 2.5) {
                        mt = length(mGradP) / max(max(mHalfSize.x, mHalfSize.y), 0.001);
                    } else if (mFillType > 2.5 && mFillType < 3.5) {
                        mt = frac((atan2(mGradP.y, mGradP.x) - mGradAngle * 0.0174533) / 6.28318 + 0.5);
                    }
                    
                    float mVCoord = (mRowIndex * 3.0 + 1.5) / 512.0;
                    float mFillAlpha = 1.0;
                    if (mFillType > 0.5) { 
                         mFillAlpha = tex2D(_MaskTex, float2(saturate(mt), mVCoord)).a;
                    } else {
                         mFillAlpha = tex2D(_MaskTex, float2(0.5, mVCoord)).a; 
                    }

                    float mTotalAlpha = mAlpha * mFillAlpha * mBaseAlpha;
                    finalColor.a *= mTotalAlpha;
                    finalColor.rgb *= mTotalAlpha;
                }

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