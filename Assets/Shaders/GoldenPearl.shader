Shader "Custom/GoldenPearl"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)
        _Shimmer("Shimmer", Range(0, 1.5)) = 0.65
        _ShimmerSpeed("Shimmer Speed", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex GoldenPearlVertex
            #pragma fragment GoldenPearlFragment
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Shimmer;
            float _ShimmerSpeed;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f GoldenPearlVertex(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 GoldenPearlFragment(v2f input) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, input.texcoord);
                float luminance = dot(sprite.rgb, float3(0.299, 0.587, 0.114));
                float goldLevel = saturate(luminance * 6.0 + 0.15);
                float3 gold = lerp(float3(0.58, 0.18, 0.015), float3(1.0, 0.88, 0.25), goldLevel);
                float scanPosition = frac(input.texcoord.x + _Time.y * _ShimmerSpeed);
                float scan = exp(-pow((scanPosition - 0.5) * 8.0, 2.0));
                gold += scan * _Shimmer * float3(0.24, 0.16, 0.035);

                fixed alpha = sprite.a * input.color.a;
                return fixed4(gold * input.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
