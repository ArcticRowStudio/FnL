// Unlit sprite shader that makes a painted sky look alive: a slowly scrolling noise field
// nudges the texture lookup sideways (and a little up/down), so the brush-stroke clouds
// billow and stream in the wind without the sprite itself moving or its edges showing.
// The layer also sways slowly sideways. A second noise field darkens/brightens patches as if cloud shadows were passing.
// Everything fades out towards the horizon line so the sea and the sun's glow stay put.
Shader "Custom/2D/Sky Flow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Horizon)]
        _HorizonV ("Horizon (texture V, 0 = bottom)", Range(0, 1)) = 0.165
        _HorizonFade ("Fade-in above horizon", Range(0.001, 0.5)) = 0.12

        [Header(Wind)]
        _WindSpeed ("Wind speed (UV per sec)", Float) = 0.015
        _WindTopBoost ("Extra speed at the top", Range(0, 3)) = 1.6
        _NoiseScale ("Noise scale (X, Y)", Vector) = (10, 30, 0, 0)
        _WarpX ("Sideways warp (UV)", Range(0, 0.03)) = 0.008
        _WarpY ("Vertical warp (UV)", Range(0, 0.02)) = 0.003
        _SwayAmount ("Slow sideways sway (UV)", Range(0, 0.03)) = 0.012
        _SwaySpeed ("Sway speed (rad per sec)", Float) = 0.06

        [Header(Light)]
        _ShadeAmount ("Cloud-shadow brightness swing", Range(0, 0.4)) = 0.14
        _ShadeScale ("Cloud-shadow size", Float) = 8
        _ShadeSpeed ("Cloud-shadow speed", Float) = 0.15

        // Legacy properties, as in Sprite-Unlit-Default.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment SkyFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _HorizonV;
                float _HorizonFade;
                float _WindSpeed;
                float _WindTopBoost;
                float4 _NoiseScale;
                float _WarpX;
                float _WarpY;
                float _SwayAmount;
                float _SwaySpeed;
                float _ShadeAmount;
                float _ShadeScale;
                float _ShadeSpeed;
            CBUFFER_END

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3 octaves, 0..1, each octave drifting a little faster so the field churns
            // rather than sliding as one rigid sheet.
            float Fbm(float2 p, float t)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int o = 0; o < 3; o++)
                {
                    v += amp * ValueNoise(p + float2(t * (1.0 + 0.6 * o), t * 0.15 * o));
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 SkyFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;

                // 0 at and below the horizon, 1 up in the clouds.
                float sky = smoothstep(_HorizonV, _HorizonV + _HorizonFade, uv.y);
                // High clouds ride faster winds.
                float height = saturate((uv.y - _HorizonV) / max(1.0 - _HorizonV, 1e-3));
                float wind = _WindSpeed * (1.0 + _WindTopBoost * height);

                // Noise is sampled in a space that scrolls with the wind; stretched in X so the
                // distortion follows the painting's horizontal strokes.
                float2 np = uv * _NoiseScale.xy - float2(t * wind * _NoiseScale.x, 0);
                float nx = Fbm(np, t * 0.05);
                float ny = Fbm(np + float2(17.3, 5.1), t * 0.04);

                // The whole cloud layer also leans slowly back and forth (minutes per swing),
                // so the clouds really travel instead of only rippling in place.
                float sway = sin(t * _SwaySpeed) * _SwayAmount;
                float2 warped = uv + float2((nx - 0.5) * _WarpX + sway, (ny - 0.5) * _WarpY) * sky;
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warped) * input.color;

                // Broad soft patches of light and shade drifting across.
                float2 sp = uv * float2(_ShadeScale, _ShadeScale * 2.5) - float2(t * _ShadeSpeed, 0);
                float shade = Fbm(sp + 40.0, t * 0.03) - 0.5;
                col.rgb *= 1.0 + shade * 2.0 * _ShadeAmount * sky;

                return col;
            }
            ENDHLSL
        }
    }
}
