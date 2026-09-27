// VAMP characters: skin pattern projected tri-planar on the character's REST pose (baked into UV3 by the art builder),
// so the pattern sticks to the body while it animates. Full URP PBR lighting, plus a team-coloured fresnel rim
// (_RimColor) so allies / enemies stay readable whatever skin they wear.
Shader "VAMP/Character"
{
    Properties
    {
        [MainColor] _BaseColor ("Colour", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Pattern", 2D) = "white" {}
        _PatternScale ("Pattern tiles per metre", Float) = 1.5
        _PatternStrength ("Pattern strength", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _PatternGlow ("Pattern glow (emission from bright pattern)", Range(0,4)) = 0
        [HDR] _RimColor ("Team rim", Color) = (0,0,0,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _PatternScale;
            half _PatternStrength;
            half _Metallic;
            half _Smoothness;
            half4 _EmissionColor;
            half _PatternGlow;
            half4 _RimColor;
            half _RimPower;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 rest : TEXCOORD3;   // rest-pose position (metres), baked by the art builder
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 rest : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.rest = v.rest;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                // Tri-planar on the rest pose: blend weights from the rest position's local "roundness" are not
                // available, so use a soft three-way blend of the projections (fine for stylised patterns).
                float3 q = i.rest * _PatternScale;
                half3 px = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.zy).rgb;
                half3 py = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xz + 0.37).rgb;
                half3 pz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, q.xy + 0.71).rgb;
                float3 w = abs(n);
                w = pow(w, 3.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                half3 pattern = px * w.x + py * w.y + pz * w.z;
                half3 tex = lerp(half3(1, 1, 1), pattern, _PatternStrength);
                half3 albedo = _BaseColor.rgb * tex;

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = n;
                input.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                half rim = pow(1.0 - saturate(dot(n, input.viewDirectionWS)), _RimPower);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1;
                s.emission = _EmissionColor.rgb + pattern * _PatternGlow * _PatternStrength * _BaseColor.rgb + _RimColor.rgb * rim;
                s.alpha = 1;

                half4 c = UniversalFragmentPBR(input, s);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                c.a = 1;
                return c;
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
