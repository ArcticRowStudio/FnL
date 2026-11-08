Shader "UI/ScopeVignette"
{
    Properties
    {
        _ScopeCenter ("Scope Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _ApertureRadius ("Aperture Radius", Float) = 0.32
        _SmoothWidth ("Smooth Width", Float) = 0.08
        _Aspect ("Aspect Ratio", Float) = 1.777778
        _HousingColor ("Housing Color", Color) = (0.0196, 0.0274, 0.0431, 1.0)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _HousingColor;
            float4 _ScopeCenter;
            float _ApertureRadius;
            float _SmoothWidth;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 delta = IN.texcoord - _ScopeCenter.xy;
                delta.x *= _Aspect;
                float dist = length(delta);

                float alpha = smoothstep(_ApertureRadius, _ApertureRadius + _SmoothWidth, dist);

                // Multi-coated optical glass retainer & outer bevel reflections
                if (abs(dist - _ApertureRadius * 1.015) < 0.0035)
                {
                    alpha = max(alpha, 0.35);
                }
                if (abs(dist - (_ApertureRadius + _SmoothWidth) * 0.985) < 0.003)
                {
                    alpha = max(alpha, 0.55);
                }

                fixed4 col = _HousingColor;
                col.a *= alpha * IN.color.a;
                return col;
            }
            ENDCG
        }
    }
}
