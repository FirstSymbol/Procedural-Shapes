Shader "UI/ProceduralShapes/Shape"
{
    Properties
    {
        [HideInInspector] _MainTex ("Gradient Palette", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        // Mask Properties
        _MaskParams ("Mask Params", Vector) = (0,0,0,0) 
        _MaskTrans ("Mask Transform", Vector) = (0,0,0,0)
        _MaskSize ("Mask Size", Vector) = (0,0,0,0)
        _MaskShape ("Mask Shape Params", Vector) = (0,0,0,0)
        
        // Mask Fill Data (For Transparency)
        _MaskTex ("Mask Gradient Tex", 2D) = "white" {}
        _MaskFillParams ("Mask Fill Params", Vector) = (0,0,0,0) 
        _MaskFillOffset ("Mask Fill Offset", Vector) = (0,0,0,0) 
        
        // Mask Boolean Ops
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
                float3 normal : NORMAL; // x=unused, y=Softness(AA), z=BlurRadius
                float4 tangent : TANGENT;
                float2 uv0 : TEXCOORD0;
                float4 shapeParams : TEXCOORD1;
                float4 baseData : TEXCOORD2;
                float4 fillParams : TEXCOORD3;
                float4 worldPosition : TEXCOORD4;
            };

            sampler2D _MainTex;
            float4 _ClipRect;
            
            // Boolean Arrays (Max 8 ops)
            int _BoolParams1; 
            float4 _BoolData_OpType[8];     
            float4 _BoolData_ShapeParams[8];
            float4 _BoolData_Transform[8];  
            float4 _BoolData_Size[8];       

            // Global Mask Uniforms
            float4 _MaskParams; 
            float4 _MaskTrans;  
            float4 _MaskSize;   
            float4 _MaskShape;  
            
            sampler2D _MaskTex;
            float4 _MaskFillParams;
            float4 _MaskFillOffset;
            
            // Mask Boolean Ops
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
                o.uv0 = v.texcoord0.xy;
                o.shapeParams = v.texcoord1;
                o.baseData = v.texcoord2;
                o.fillParams = v.texcoord3;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 p = i.uv0;
                float shapeType = floor(i.baseData.z);
                float cornerSmoothing = frac(i.baseData.z) / 0.99;
                float effectType = i.baseData.w; 
                
                if (effectType == 1.0) {
                    p -= i.normal.xy; // Offset for DropShadow passed in normal.xy?
                    // Wait, previous code used i.normal.xy for DropShadow offset.
                    // But now we use i.normal.y for AA and i.normal.z for Blur.
                    // We have a conflict.
                    // Let's re-map:
                    // DropShadow Offset was passed in DrawLayerQuad as 'normalData'.
                    // Main Shape passes (0, Softness, Blur)
                    // DropShadow passes (OffsetX, OffsetY, Blur)
                    // So for DropShadow, Softness is missing from normal.y?
                    // We can assume standard softness for DropShadow or pack it elsewhere?
                    // Or pack Offset in Tangent?
                    // Tangent is (Spread, Alignment, GradOffX, GradOffY). Full.
                    // Normal is (OffX, OffY, Blur).
                    // We need a place for AA.
                    // DropShadow usually implies soft edges anyway (Blur).
                    // If EffectType == 1 (Shadow), normal.xy is Offset. normal.z is Blur.
                    // We can hardcode AA for shadow or use Blur as AA.
                    // For Main Shape (EffectType 0), normal.x is unused, normal.y = AA, normal.z = Blur.
                }
                
                // --- RESOLVE NORMAL DATA USAGE ---
                // Default AA
                float aa = 1.0; 
                
                if (effectType == 1.0) {
                    // Drop Shadow: normal.xy = Offset, normal.z = Blur
                    p -= i.normal.xy;
                    // AA for shadow is usually irrelevant if blurred, but let's use fixed reasonable value
                    aa = 1.0; 
                } else if (effectType == 3.0) {
                    // Inner Shadow: normal.xy = Offset, normal.z = Blur
                    // Same conflict.
                    aa = 1.0;
                } else {
                    // Main Shape / Stroke / Blur Layer
                    // normal.y = AA
                    aa = max(i.normal.y, 0.001);
                }
                
                float2 halfSize = i.baseData.xy * 0.5;

                // 1. Вычисляем SDF (только базовая фигура)
                float d = GetBasicSDF(p, halfSize, shapeType, cornerSmoothing, i.shapeParams);

                // --- BOOLEAN OPERATIONS LOOP ---
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

                        if (boolType > 1.5) { 
                            float innerRot = boolTrans.w;
                            if (abs(innerRot) > 0.0001) {
                                float s2 = sin(-innerRot);
                                float c2 = cos(-innerRot);
                                p2 = float2(p2.x * c2 - p2.y * s2, p2.x * s2 + p2.y * c2);
                            }
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
                
                // Mask Logic
                float maskFillAlpha = 1.0;
                
                // --- GLOBAL MASKING ---
                if (_MaskParams.x > 0.5) {
                    float maskType = _MaskParams.y;
                    float maskSmooth = _MaskParams.z;
                    float maskFeather = _MaskParams.w;
                    
                    float2 maskP = p - _MaskTrans.xy;
                    float maskRot = _MaskTrans.z;
                    if (abs(maskRot) > 0.0001) {
                         float s = sin(-maskRot);
                         float c = cos(-maskRot);
                         maskP = float2(maskP.x * c - maskP.y * s, maskP.x * s + maskP.y * c);
                    }
                    if (maskType > 1.5) {
                         float innerR = _MaskTrans.w;
                         if (abs(innerR) > 0.0001) {
                             float s2 = sin(-innerR);
                             float c2 = cos(-innerR);
                             maskP = float2(maskP.x * c2 - maskP.y * s2, maskP.x * s2 + maskP.y * c2);
                         }
                    }
                    
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
                            
                            if (bType > 1.5) { 
                                float iRot = bTrans.w;
                                if (abs(iRot) > 0.0001) {
                                    float s2 = sin(-iRot);
                                    float c2 = cos(-iRot);
                                    p3 = float2(p3.x * c2 - p3.y * s2, p3.x * s2 + p3.y * c2);
                                }
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
                    float2 mGradOffset = _MaskFillOffset.xy;
                    float mVCoord = _MaskFillParams.w;

                    float2 mGradP = maskP - (_MaskSize.xy * 0.5 * mGradOffset);
                    mGradP /= max(mGradScale, 0.001);

                    float mt = 0.5;
                    if (mFillType == 1.0) { 
                        float rad = mGradAngle * 0.0174533;
                        float2 dir = float2(cos(rad), sin(rad));
                        mt = (dot(mGradP, dir) / max(abs(dir.x*_MaskSize.x*0.5)+abs(dir.y*_MaskSize.y*0.5), 0.001)) * 0.5 + 0.5;
                    } else if (mFillType == 2.0) { 
                        mt = length(mGradP) / max(max(_MaskSize.x*0.5, _MaskSize.y*0.5), 0.001);
                    } else if (mFillType == 3.0) { 
                        mt = frac((atan2(mGradP.y, mGradP.x) - mGradAngle * 0.0174533) / 6.28318 + 0.5);
                    }
                    
                    if (mFillType > 0.5) { 
                         maskFillAlpha = tex2D(_MaskTex, float2(saturate(mt), mVCoord)).a;
                    } else {
                         maskFillAlpha = tex2D(_MaskTex, float2(0.5, mVCoord)).a; 
                    }
                }

                float spread = i.tangent.x;
                if (effectType == 1.0) d -= spread; 
                else if (effectType == 2.0) { 
                    float alignment = i.tangent.y;
                    float strokeOffset = (alignment == 0) ? -spread * 0.5 : ((alignment == 2) ? spread * 0.5 : 0);
                    d = abs(d - strokeOffset) - spread * 0.5;
                }
                else if (effectType == 3.0) d += spread; 

                float blur = max(i.normal.z, aa);
                
                float mask = 0;
                if (effectType == 3.0) { 
                    float baseD = d - spread; 
                    mask = smoothstep(-blur, blur, d) * smoothstep(aa, -aa, baseD);
                } else {
                    mask = smoothstep(blur, -blur, d);
                }

                if (mask <= 0.001) discard;

                float rowIndex = fmod(i.fillParams.x, 100.0);
                float texHeight = floor(i.fillParams.x / 100.0);
                float fillType = i.fillParams.y;
                float gradAngle = i.fillParams.z;
                float gradScale = i.fillParams.w;
                float2 gradOffset = i.tangent.zw;

                float2 gradP = p - (halfSize * gradOffset);
                if (effectType == 1.0 || effectType == 3.0) gradP -= i.normal.xy; // Offset for Shadows from normal.xy?
                // Wait, if effectType == 1 (Shadow), we ALREADY subtracted offset from p at start of shader.
                // "if (effectType == 1.0) p -= i.normal.xy;"
                // So here 'p' is already shifted.
                // Do we need to shift gradP?
                // Gradient texture coordinates should stay with the shadow?
                // If p is shifted, gradP (derived from p) is shifted. So the texture moves with the shadow. Correct.
                
                gradP /= max(gradScale, 0.001);

                float t = 0.5;
                if (fillType == 1.0) { 
                    float rad = gradAngle * 0.0174533;
                    float2 dir = float2(cos(rad), sin(rad));
                    t = (dot(gradP, dir) / max(abs(dir.x*halfSize.x)+abs(dir.y*halfSize.y), 0.001)) * 0.5 + 0.5;
                } else if (fillType == 2.0) { 
                    t = length(gradP) / max(max(halfSize.x, halfSize.y), 0.001);
                } else if (fillType == 3.0) { 
                    t = frac((atan2(gradP.y, gradP.x) - gradAngle * 0.0174533) / 6.28318 + 0.5);
                }

                float4 colorSample = tex2D(_MainTex, float2(saturate(t), (rowIndex * 3.0 + 1.5) / texHeight));
                float4 finalColor = colorSample * i.color;
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