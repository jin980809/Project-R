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
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            // Unity 6 URP는 SpriteRenderer.color를 정점 색이 아니라 unity_SpriteColor로 넘기므로 URP 라이브러리로 쓴다(SpriteSilhouette와 같은 방식).
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex GradientVertex
            #pragma fragment GradientFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _HighColor;
                half _DarkTint;
                half _HighMix;
            CBUFFER_END

            Varyings GradientVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 GradientFragment(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half value = tex.r;
                half3 tint = i.color.rgb;
                half3 dark = lerp(_DarkColor.rgb, tint, _DarkTint);
                half3 high = lerp(tint, _HighColor.rgb, _HighMix);
                half3 low = lerp(dark, tint, saturate(value * 2));
                half3 top = lerp(tint, high, saturate(value * 2 - 1));
                half3 color = top;
                if (value < 0.5)
                {
                    color = low;
                }
                return half4(color, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
