Shader "TurboPlanes/ComicSky"
{
    Properties
    {
        _SkyColorTop ("Sky Top", Color) = (0.30, 0.60, 0.95, 1)
        _SkyColorHorizon ("Sky Horizon", Color) = (0.85, 0.95, 1.0, 1)
        _SunColor ("Sun Color", Color) = (1.0, 0.95, 0.55, 1)
        _SunDirection ("Sun Direction", Vector) = (0.4, 0.7, 0.6, 0)
        _SunSize ("Sun Size (cos threshold)", Range(0.95, 0.999)) = 0.985
        _SunHaloSize ("Sun Halo (cos threshold)", Range(0.9, 0.999)) = 0.965
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudShadowColor ("Cloud Shadow Color", Color) = (0.75, 0.80, 0.9, 1)
        _CloudThreshold ("Cloud Threshold", Range(0.3, 0.8)) = 0.55
        _CloudScale ("Cloud Scale", Range(0.5, 8)) = 2.5
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

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColorTop;
                float4 _SkyColorHorizon;
                float4 _SunColor;
                float4 _SunDirection;
                float _SunSize;
                float _SunHaloSize;
                float4 _CloudColor;
                float4 _CloudShadowColor;
                float _CloudThreshold;
                float _CloudScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDir = IN.positionOS.xyz;
                return OUT;
            }

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash21(i + float2(0,0)), hash21(i + float2(1,0)), u.x),
                    lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x),
                    u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                v += vnoise(p)        * 0.5;
                v += vnoise(p * 2.0)  * 0.25;
                v += vnoise(p * 4.0)  * 0.125;
                v += vnoise(p * 8.0)  * 0.0625;
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.viewDir);

                // Vertical gradient (horizon -> top)
                float t = saturate(dir.y);
                float3 sky = lerp(_SkyColorHorizon.rgb, _SkyColorTop.rgb, smoothstep(0.0, 0.5, t));

                // Sun (hard disc + soft halo)
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = dot(dir, sunDir);
                float halo = smoothstep(_SunHaloSize, _SunSize, sunDot);
                float disc = step(_SunSize, sunDot);
                sky = lerp(sky, _SunColor.rgb * 0.9, halo * 0.45);
                sky = lerp(sky, _SunColor.rgb, disc);

                // Procedural clouds in the upper hemisphere
                if (dir.y > 0.05)
                {
                    float2 cloudUV = dir.xz / max(0.05, dir.y);
                    cloudUV *= _CloudScale * 0.1;
                    float n = fbm(cloudUV);
                    float cloudMask = step(_CloudThreshold, n);
                    float cloudShadowMask = step(_CloudThreshold - 0.06, n) * (1.0 - cloudMask);
                    float horizonFade = smoothstep(0.05, 0.25, dir.y);
                    sky = lerp(sky, _CloudShadowColor.rgb, cloudShadowMask * horizonFade * 0.6);
                    sky = lerp(sky, _CloudColor.rgb, cloudMask * horizonFade);
                }

                return half4(sky, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
