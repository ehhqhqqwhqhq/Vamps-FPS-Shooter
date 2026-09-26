// VAMP weapon camo: tri-planar projection in the model's own space (scaled to metres) so a camo texture wraps any
// imported gun regardless of its UVs, and stays glued to the gun while it moves. Simple lit (main light + shadows +
// ambient + a soft specular). URP only.
Shader "VAMP/Camo"
{
    Properties
    {
        _CamoTex ("Camo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Scale ("Tiles per metre", Float) = 3.2
        _Smoothness ("Smoothness", Range(0,1)) = 0.55
        _Specular ("Specular", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _CamoTex_ST;
            half4 _Tint;
            float _Scale;
            half _Smoothness;
            half _Specular;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CamoTex);
            SAMPLER(sampler_CamoTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 camoPos : TEXCOORD2;
                float3 camoNormal : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                // Object space scaled to metres (the imported meshes are scaled on their transform).
                float s = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                o.camoPos = v.positionOS.xyz * s;
                o.camoNormal = normalize(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 w = abs(i.camoNormal);
                w = pow(w, 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 q = i.camoPos * _Scale;
                half3 cx = SAMPLE_TEXTURE2D(_CamoTex, sampler_CamoTex, q.zy).rgb;
                half3 cy = SAMPLE_TEXTURE2D(_CamoTex, sampler_CamoTex, q.xz + 0.37).rgb;
                half3 cz = SAMPLE_TEXTURE2D(_CamoTex, sampler_CamoTex, q.xy + 0.71).rgb;
                half3 albedo = (cx * w.x + cy * w.y + cz * w.z) * _Tint.rgb;

                float3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                half ndl = saturate(dot(n, light.direction));
                half3 ambient = SampleSH(n);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 h = normalize(light.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), exp2(10.0 * _Smoothness + 1.0)) * _Specular;

                half3 color = albedo * (ambient + light.color * ndl * atten) + light.color * spec * atten;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert(Attributes v) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                float4 c = TransformWorldToHClip(ApplyShadowBias(ws, nw, _LightDirection));
                #if UNITY_REVERSED_Z
                    c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };

            float4 vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
