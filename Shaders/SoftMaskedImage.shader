Shader "UI/ProceduralShapes/SoftMaskedImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InternalPadding ("Internal Padding", Float) = 0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        // Mask Data
        _MaskWorldToLocalX ("Mask Matrix X", Vector) = (1,0,0,0)
        _MaskWorldToLocalY ("Mask Matrix Y", Vector) = (0,1,0,0)
        _MaskWorldToLocalZ ("Mask Matrix Z", Vector) = (0,0,1,0)
        _MaskWorldToLocalW ("Mask Matrix W", Vector) = (0,0,0,1)

        _MaskParams ("Mask Params", Vector) = (0,0,0,0) 
        _MaskTrans ("Mask Transform", Vector) = (0,0,0,0)
        _MaskSize ("Mask Size", Vector) = (0,0,0,0)
        _MaskShape ("Mask Shape Params", Vector) = (0,0,0,0)
        
        _MaskTex ("Mask Gradient Tex", 2D) = "white" {}
        _MaskFillParams ("Mask Fill Params", Vector) = (0,0,0,0) 
        _MaskFillOffset ("Mask Fill Offset", Vector) = (0,0,0,0) 
        
        _MaskBoolParams ("Bool Count", Int) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
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
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _InternalPadding;
            
            float4 _MaskWorldToLocalX;
            float4 _MaskWorldToLocalY;
            float4 _MaskWorldToLocalZ;
            float4 _MaskWorldToLocalW;

            float4 _MaskParams; 
            float4 _MaskTrans;  
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
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex); 
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float4 color = tex2D(_MainTex, i.texcoord) * i.color;
                
                if (_MaskParams.x > 0.5) {
                    float3 worldPos = i.worldPosition.xyz;
                    float4x4 worldToMask = float4x4(
                        _MaskWorldToLocalX,
                        _MaskWorldToLocalY,
                        _MaskWorldToLocalZ,
                        _MaskWorldToLocalW
                    );
                    
                    float2 p = mul(worldToMask, float4(worldPos, 1.0)).xy;
                    
                    float maskType = _MaskParams.y;
                    float maskSmooth = _MaskParams.z;
                    float maskFeather = _MaskParams.w;
                    
                    // Base Mask SDF
                    float2 maskP = p;
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
                    
                    float d = GetBasicSDF(maskP, _MaskSize.xy * 0.5, maskType, maskSmooth, _MaskShape);
                    d += _InternalPadding;
                    
                    int boolCount = _MaskBoolParams;
                    if (boolCount > 0) {
                        for (int k = 0; k < 8; k++) {
                            if (k >= boolCount) break;

                            float boolOp = _MaskBoolOpType[k].x;
                            float boolType = _MaskBoolOpType[k].y;
                            float boolSmooth = _MaskBoolOpType[k].z;
                            float smoothBlend = _MaskBoolOpType[k].w; 
                            
                            float4 boolTrans = _MaskBoolTransform[k]; 
                            float2 boolSize = _MaskBoolSize[k].xy;
                            float4 boolShapeParams = _MaskBoolShapeParams[k];

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

                            if (smoothBlend > 0.001) {
                                if (boolOp < 1.5) d = smin(d, d2, smoothBlend);
                                else if (boolOp < 2.5) d = smax(d, -d2, smoothBlend);
                                else if (boolOp < 3.5) d = smax(d, d2, smoothBlend);
                            } else {
                                if (boolOp < 1.5) d = min(d, d2); 
                                else if (boolOp < 2.5) d = max(d, -d2); 
                                else if (boolOp < 3.5) d = max(d, d2); 
                                else if (boolOp < 4.5) d = max(min(d, d2), -max(d, d2)); 
                            }
                        }
                    }
                    
                    float shapeAlpha = smoothstep(max(0.001, maskFeather), -max(0.001, maskFeather), d);
                    
                    float2 halfSize = _MaskSize.xy * 0.5;
                    float fillType = _MaskFillParams.x;
                    float gradAngle = _MaskFillParams.y;
                    float gradScale = _MaskFillParams.z;
                    float2 gradOffset = _MaskFillOffset.xy; 

                    // FIX: Use rotated coordinates for gradient calculation
                    float2 gradP = maskP - (halfSize * gradOffset);
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
                    
                    float vCoord = _MaskFillParams.w;
                    float fillAlpha = 1.0;
                    if (fillType > 0.5) { 
                         fillAlpha = tex2D(_MaskTex, float2(saturate(t), vCoord)).a;
                    } else {
                         fillAlpha = tex2D(_MaskTex, float2(0.5, vCoord)).a; 
                    }

                    color.a *= shapeAlpha * fillAlpha;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}