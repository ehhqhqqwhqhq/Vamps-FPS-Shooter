// VAMP sky: three-colour gradient (zenith / horizon / ground), a sun disc with a soft glow and horizon haze, and a
// drifting cloud layer projected on a dome. Colours come from the map's lighting theme (VampArtBuilder.SetupDayLighting).
Shader "VAMP/Sky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.22, 0.45, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.75, 0.85, 0.95, 1)
        _BottomColor ("Ground", Color) = (0.35, 0.33, 0.32, 1)
        _HorizonSharpness ("Horizon sharpness", Range(0.5, 8)) = 2.5
        [HDR] _SunColor ("Sun", Color) = (4, 3.6, 3, 1)
        _SunDir ("Sun direction (towards the sun)", Vector) = (0.3, 0.6, 0.4, 0)
        _SunSize ("Sun size", Range(0.0005, 0.05)) = 0.0025
        _SunGlow ("Sun glow", Range(0, 4)) = 0.8
        _CloudTex ("Clouds", 2D) = "black" {}
        [HDR] _CloudColor ("Cloud light", Color) = (1.2, 1.15, 1.1, 1)
        _CloudShade ("Cloud shade", Color) = (0.55, 0.6, 0.7, 1)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.45
        _CloudScale ("Cloud scale", Float) = 0.14
        _CloudSpeed ("Cloud speed", Float) = 0.004
        _Exposure ("Exposure", Range(0.2, 3)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor, _CloudShade;
                float4 _SunDir, _CloudTex_ST;
                float _HorizonSharpness, _SunSize, _SunGlow, _CloudCover, _CloudScale, _CloudSpeed, _Exposure;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float y = d.y;

                // Gradient: horizon band blends into the zenith above and the ground below.
                float up = pow(saturate(y), 1.0 / _HorizonSharpness);
                float down = pow(saturate(-y * 3.0), 0.6);
                half3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                sky = lerp(sky, _BottomColor.rgb, down);

                // Sun: disc + glow + warm haze near the horizon on the sun side.
                float cosA = dot(d, sun);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, cosA);
                float glow = pow(saturate(cosA), 180.0) * 1.2 + pow(saturate(cosA), 24.0) * 0.1;
                float haze = pow(saturate(cosA * 0.5 + 0.5), 4.0) * pow(1.0 - saturate(abs(y)), 6.0);
                sky += _SunColor.rgb * (glow * _SunGlow * 0.35 + haze * 0.18) + _SunColor.rgb * disc * 0.45 * step(0.0, y);

                // Clouds on a dome above the horizon.
                if (y > 0.0)
                {
                    float2 uv = d.xz / (y + 0.12) * _CloudScale + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    half n = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv).r;
                    half n2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, uv * 2.3 + 0.37).r;
                    half dens = saturate((n * 0.75 + n2 * 0.25 - (1.0 - _CloudCover)) * 3.0);
                    dens *= smoothstep(0.0, 0.18, y);                        // fade out at the horizon
                    half lit = saturate(0.55 + 0.45 * cosA) + pow(saturate(cosA), 12.0) * 1.5;
                    half3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, lit * (1.0 - n2 * 0.35));
                    sky = lerp(sky, cloud, dens * 0.92);
                }
                return half4(sky * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
