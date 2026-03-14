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
                float2 p = i.uv0.xy;
                float shapeType = floor(i.baseData.z);
                float customSmoothing = frac(i.baseData.z) / 0.99 * 1000.0;
                float effectType = i.baseData.w; 
                
                float aa = 1.0;
                float blur = 0.0;
                float internalPadding = 0.0;
                
                if (effectType == 1.0 || effectType == 3.0) { // Shadows
                    p -= i.normal.xy; 
                    blur = i.normal.z;
                    aa = 1.0; 
                } else { // Main Fill, Stroke, Blur
                    internalPadding = i.normal.x;
                    aa = max(i.normal.y, 0.001);
                    blur = i.normal.z;
                }
                
                blur = max(blur, aa);
                
                // --- EDGE NOISE DISTORTION ---
                float noiseAmount = frac(i.fillParams.z) * 100.0;
                float noiseScale = i.fillParams.w;
                
                float2 noiseP = p;
                if (noiseAmount > 0.001) {
                    float n = noise(p * noiseScale * 0.1);
                    noiseP += (n * 2.0 - 1.0) * noiseAmount;
                }
                
                float2 halfSize = i.baseData.xy * 0.5;

                float d = GetBasicSDF(noiseP, halfSize, shapeType, customSmoothing, i.shapeParams);
                d += internalPadding;

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

                        float2 p2 = p - boolTrans.xy;
                        float rot = boolTrans.z;
                        if (abs(rot) > 0.0001) {
                            float s = sin(-rot);
                            float c = cos(-rot);
                            p2 = float2(p2.x * c - p2.y * s, p2.x * s + p2.y * c);
                        }

                        float d2 = GetBasicSDF(p2, boolSize * 0.5, boolType, boolSmooth, boolShapeParams);

                        if (smoothBlend > 0.001) 
                        {
                            if (boolOp < 1.5) d = smin(d, d2, smoothBlend);
                            else if (boolOp < 2.5) d = smax(d, -d2, smoothBlend);
                            else if (boolOp < 3.5) d = smax(d, d2, smoothBlend);
                        }
                        else 
                        {
                            if (boolOp < 1.5) d = min(d, d2); 
                            else if (boolOp < 2.5) d = max(d, -d2); 
                            else if (boolOp < 3.5) d = max(d, d2); 
                            else if (boolOp < 4.5) d = max(min(d, d2), -max(d, d2)); 
                        }
                    }
                }
                
                float maskFillAlpha = 1.0;
                
                // --- GLOBAL MASKING ---
                if (_MaskParams.x > 0.5) {
                    float maskType = _MaskParams.y;
                    float maskSmooth = _MaskParams.z;
                    float maskFeather = _MaskParams.w;
                    
                    float4x4 childToMask = float4x4(
                        _MaskMatrixX,
                        _MaskMatrixY,
                        _MaskMatrixZ,
                        _MaskMatrixW
                    );
                    
                    float2 maskP = mul(childToMask, float4(p, 0, 1)).xy;
                    
                    float maskD = GetBasicSDF(maskP, _MaskSize.xy * 0.5, maskType, maskSmooth, _MaskShape);
                    
                    int mBoolCount = _MaskBoolParams;
                    if (mBoolCount > 0) {
                        for (int j = 0; j < 8; j++) {
                            if (j >= mBoolCount) break;

                            float bOp = _MaskBoolOpType[j].x;
                            float bType = _MaskBoolOpType[j].y;
                            float bSmooth = _MaskBoolOpType[j].z;
                            float bBlend = _MaskBoolOpType[j].w;
                            
                            float4 bTrans = _MaskBoolTransform[j]; 
                            float2 bSize = _MaskBoolSize[j].xy;
                            float4 bParams = _MaskBoolShapeParams[j];

                            float2 p3 = maskP - bTrans.xy; 
                            float rot3 = bTrans.z;
                            if (abs(rot3) > 0.0001) {
                                float s = sin(-rot3);
                                float c = cos(-rot3);
                                p3 = float2(p3.x * c - p3.y * s, p3.x * s + p3.y * c);
                            }

                            float d3 = GetBasicSDF(p3, bSize * 0.5, bType, bSmooth, bParams);

                            if (bBlend > 0.001) {
                                if (bOp < 1.5) maskD = smin(maskD, d3, bBlend);
                                else if (bOp < 2.5) maskD = smax(maskD, -d3, bBlend);
                                else if (bOp < 3.5) maskD = smax(maskD, d3, bBlend);
                            } else {
                                if (bOp < 1.5) maskD = min(maskD, d3); 
                                else if (bOp < 2.5) maskD = max(maskD, -d3); 
                                else if (bOp < 3.5) maskD = max(maskD, d3); 
                                else if (bOp < 4.5) maskD = max(min(maskD, d3), -max(maskD, d3)); 
                            }
                        }
                    }
                    
                    if (maskFeather > 0.001) {
                        d = smax(d, maskD, maskFeather);
                    } else {
                        d = max(d, maskD);
                    }
                    
                    float mFillType = _MaskFillParams.x;
                    float mGradAngle = _MaskFillParams.y;
                    float mGradScale = _MaskFillParams.z;
                    float mRowIndex = _MaskFillParams.w;
                    float2 mGradOffset = _MaskFillOffset.xy;
                    float mBaseAlpha = _MaskFillOffset.z;

                    float2 mGradP = maskP - (_MaskSize.xy * 0.5 * mGradOffset);
                    mGradP /= max(mGradScale, 0.001);

                    float mt = 0.5;
                    if (mFillType > 0.5 && mFillType < 1.5) { // Linear
                        float rad = mGradAngle * 0.0174533;
                        float2 dir = float2(cos(rad), sin(rad));
                        mt = (dot(mGradP, dir) / max(abs(dir.x*_MaskSize.x*0.5)+abs(dir.y*_MaskSize.y*0.5), 0.001)) * 0.5 + 0.5;
                    } else if (mFillType > 1.5 && mFillType < 2.5) { // Radial
                        mt = length(mGradP) / max(max(_MaskSize.x*0.5, _MaskSize.y*0.5), 0.001);
                    } else if (mFillType > 2.5 && mFillType < 3.5) { // Angular
                        mt = frac((atan2(mGradP.y, mGradP.x) - mGradAngle * 0.0174533) / 6.28318 + 0.5);
                    }
                    
                    float mVCoord = (mRowIndex * 3.0 + 1.5) / 512.0;
                    if (mFillType > 0.5) { 
                         maskFillAlpha = tex2D(_MaskTex, float2(saturate(mt), mVCoord)).a;
                    } else {
                         maskFillAlpha = tex2D(_MaskTex, float2(0.5, mVCoord)).a; 
                    }
                    maskFillAlpha *= mBaseAlpha;
                }

                float spread = i.tangent.x;
                if (effectType == 1.0) d -= spread; 
                else if (effectType == 2.0) { 
                    float alignment = i.tangent.y;
                    float strokeOffset = (alignment == 0) ? -spread * 0.5 : ((alignment == 2) ? spread * 0.5 : 0);
                    d = abs(d - strokeOffset) - spread * 0.5;
                    
                    if (i.uv0.z > 0.001) {
                        float dashLen = i.uv0.z;
                        float spaceLen = i.uv0.w;
                        float perimeter = 0;
                        if (shapeType == 5.0) perimeter = p.x;
                        else perimeter = (atan2(p.y, p.x) + 3.14159265) * length(p);
                        
                        float dashCycle = dashLen + spaceLen;
                        float dashVal = frac(perimeter / dashCycle);
                        if (dashVal > (dashLen / dashCycle)) discard;
                    }
                }
                else if (effectType == 3.0) d += spread; 

                float mask = 0;
                if (effectType == 3.0) { 
                    float baseD = d - spread; 
                    // To fix the gap, we make the mask reach 1.0 exactly at the border (baseD = 0)
                    // and apply the shape's AA separately to ensure a clean cut.
                    mask = saturate(smoothstep(-blur, 0, baseD)) * smoothstep(aa, -aa, baseD);
                } else if (effectType == 5.0) {
                    mask = 1.0; 
                } else {
                    mask = smoothstep(blur, -blur, d);
                }

                if (mask <= 0.001) discard;

                float rowIndex = floor(i.fillParams.x + 0.5);
                float fillType = i.fillParams.y;
                float gradAngle = i.fillParams.z;
                float gradScale = i.fillParams.w;
                float2 gradOffset = i.tangent.zw;

                float4 colorSample;
                if (fillType > 3.5) { // Pattern
                    float2 patternUV = p / halfSize * 0.5 + 0.5;
                    patternUV = patternUV * gradScale + gradOffset;
                    colorSample = tex2D(_PatternTex, patternUV);
                } else {
                    float2 gradP = p - (halfSize * gradOffset);
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
                    float distance = max(i.normal.z, 0.5);
                    float hAlpha = i.tangent.x;
                    float sAlpha = i.tangent.y;
                    float bAngle = i.tangent.z;
                    
                    float2 bDir = float2(cos(bAngle), sin(bAngle));
                    float d1 = GetBasicSDF(noiseP + bDir * distance, halfSize, shapeType, customSmoothing, i.shapeParams) + internalPadding;
                    float d2 = GetBasicSDF(noiseP - bDir * distance, halfSize, shapeType, customSmoothing, i.shapeParams) + internalPadding;
                    
                    float diff = d1 - d2;
                    float bVal = diff / (distance * 2.0); 
                    
                    float highlight = saturate(bVal) * hAlpha;
                    float shadow = saturate(-bVal) * sAlpha;
                    
                    float baseMask = smoothstep(aa, -aa, d);
                    if (baseMask <= 0.001) discard;
                    
                    float4 bColor = float4(1,1,1, highlight);
                    if (shadow > highlight) bColor = float4(0,0,0, shadow);
                    
                    finalColor = bColor;
                    mask = baseMask;
                }
                
                finalColor.a *= mask;
                finalColor.a *= maskFillAlpha; 
                finalColor.rgb *= finalColor.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}