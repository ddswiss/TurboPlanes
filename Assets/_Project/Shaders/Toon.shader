Shader "TurboPlanes/Toon"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.5, 0.5, 0.6, 1)
        _LightSteps("Light Steps", Range(1, 4)) = 2
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.5, 16)) = 4
        _RimStrength("Rim Strength", Range(0, 1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float _LightSteps;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = vni.normalWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();

                float NdotL = saturate(dot(N, mainLight.direction));
                float steps = max(1.0, _LightSteps);
                float stepped = floor(NdotL * steps + 0.001) / steps;

                float3 lit = lerp(_ShadowTint.rgb * _BaseColor.rgb, _BaseColor.rgb, stepped);
                lit *= mainLight.color;

                // Optional rim lighting (off by default at strength=0)
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;
                float3 col = lit + _RimColor.rgb * rim;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
