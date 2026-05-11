Shader "TurboPlanes/UI/SpeedLines"
{
    // Procedural radial speed-lines overlay for a Canvas Image (manga-style).
    // White stripes radiate from screen center, alpha-faded so the inner area
    // stays clear and the lines only appear toward the edges. _Intensity (0..1)
    // is driven from script; the shader handles the visual time-based shimmer
    // (per-stripe alpha pulsing + slow whole-pattern rotation) so the effect
    // feels alive even when intensity is held constant.
    Properties
    {
        _Color         ("Tint",            Color) = (1, 1, 1, 1)
        _Intensity     ("Intensity",       Range(0, 1)) = 0.0
        _StripeCount   ("Stripe Count",    Range(8, 256)) = 80
        _StripeWidth   ("Stripe Width",    Range(0.05, 0.95)) = 0.55
        _InnerRadius   ("Inner Radius",    Range(0, 1)) = 0.35
        _OuterRadius   ("Outer Radius",    Range(0, 2)) = 1.10
        _AspectRatio   ("Aspect (W/H)",    Float) = 1.7777
        _StripeJitter  ("Stripe Jitter",   Range(0, 1)) = 0.45
        _ShimmerSpeed  ("Shimmer Speed",   Range(0, 12)) = 4.0
        _ShimmerAmount ("Shimmer Amount",  Range(0, 1)) = 0.40
        _RotationSpeed ("Rotation Speed",  Range(-1, 1)) = 0.05

        // Standard UI mask / stencil plumbing so this works inside masked canvases.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "RenderType"       = "Transparent"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            float4 _Color;
            float  _Intensity;
            float  _StripeCount;
            float  _StripeWidth;
            float  _InnerRadius;
            float  _OuterRadius;
            float  _AspectRatio;
            float  _StripeJitter;
            float  _ShimmerSpeed;
            float  _ShimmerAmount;
            float  _RotationSpeed;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            // Cheap deterministic hash for per-stripe randomness.
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // UV → centered, aspect-corrected so stripes look truly radial
                // even on widescreen.
                float2 c = IN.texcoord - 0.5;
                c.x *= _AspectRatio;

                float r     = length(c) * 2.0;
                // Slow rotation of the whole pattern keeps it from looking stamped.
                float theta = atan2(c.y, c.x) + _Time.y * _RotationSpeed;

                // Stripes via fractional angle. Higher StripeWidth → thicker lines.
                float t = theta * _StripeCount / (2.0 * 3.14159265);
                float stripeIdx = floor(t);
                float inSlice   = step(_StripeWidth, frac(t + 0.5));
                float stripe    = 1.0 - inSlice;  // 1 inside the stripe, 0 outside

                // Per-stripe alpha jitter (deterministic, hides the perfect grid).
                float baseJitter = lerp(1.0, hash11(stripeIdx), _StripeJitter);

                // Time-based shimmer: each stripe pulses with a unique phase so
                // the wave isn't synchronized across the screen.
                float phase   = hash11(stripeIdx + 17.0) * 6.28318;
                float shimmer = lerp(1.0,
                                     0.5 + 0.5 * sin(_Time.y * _ShimmerSpeed + phase),
                                     _ShimmerAmount);

                // Radial fade keeps the center clear and ramps lines in toward edges.
                float radialFade = smoothstep(_InnerRadius, _OuterRadius, r);

                float a = stripe * baseJitter * shimmer * radialFade * _Intensity * IN.color.a;
                return float4(IN.color.rgb, a);
            }
            ENDCG
        }
    }
}
