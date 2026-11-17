// 스킬 이펙트용 그라디언트 맵 셰이더.
// 이펙트 그림은 명암(회색)만 담고 있고, 스킬 색(SpriteRenderer.color)으로 색을 입힌다.
// 명암 0 = 어두운 외곽선 색(스킬 색 쪽으로 조금 섞임), 0.5 = 스킬 색, 1 = 거의 흰색(따뜻한 흰빛).
// 그래서 같은 그림이 빨강·주황·노랑 등 어떤 스킬 색으로도 외곽선·면·하이라이트 3단 명암을 갖는다.
// Resources 폴더에 있어 빌드에도 포함되고 SkillFxPlayer가 Shader.Find로 찾는다. 2D 조명의 영향은 받지 않는다.
Shader "Tower/SkillFxGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _DarkColor ("Outline Tone", Color) = (0.22, 0.13, 0.16, 1)
        _HighColor ("Highlight Tone", Color) = (1, 0.98, 0.9, 1)
        _DarkTint ("Outline Tint Mix", Range(0, 1)) = 0.35
        _HighMix ("Highlight Mix", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _DarkColor;
            fixed4 _HighColor;
            half _DarkTint;
            half _HighMix;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                half value = tex.r;
                half3 tint = i.color.rgb;
                half3 dark = lerp(_DarkColor.rgb, tint, _DarkTint);
                half3 high = lerp(tint, _HighColor.rgb, _HighMix);
                half3 low = lerp(dark, tint, saturate(value * 2));
                half3 top = lerp(tint, high, saturate(value * 2 - 1));
                half3 color = value < 0.5 ? low : top;
                return fixed4(color, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
