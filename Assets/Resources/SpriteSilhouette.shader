// 스프라이트 실루엣 셰이더: 그림의 모양(알파)만 쓰고 색은 SpriteRenderer.color 한 가지로 칠한다.
// 강화한 무기 둘레의 빛 외곽선과 반짝이(UpgradeGlow)에 쓴다. 2D 조명의 영향은 받지 않는다.
// Unity 6 URP는 SpriteRenderer.color를 정점 색이 아니라 unity_SpriteColor로 넘기므로 URP 라이브러리로 쓴다(Sprite-Unlit-Default와 같은 방식).
// Resources 폴더에 있어 빌드에도 포함되고 UpgradeGlow가 Shader.Find로 찾는다.
Shader "Tower/SpriteSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex SilhouetteVertex
            #pragma fragment SilhouetteFragment
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
                half4 _Color;
            CBUFFER_END

            Varyings SilhouetteVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 SilhouetteFragment(Varyings i) : SV_Target
            {
                half alpha = step(0.5h, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a);
                return half4(i.color.rgb, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
}
