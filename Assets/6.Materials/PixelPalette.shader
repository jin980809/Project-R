// 픽셀 화면판(PixelView) 전용: 렌더 텍스처의 각 픽셀을 팔레트(Endesga 32)에서 가장 가까운 색으로 바꾼다.
// 맵, 캐릭터, 몬스터, 이펙트가 모두 같은 색 묶음으로 그려져 한 화면에서 어울리게 된다.
// 가까운 색은 사람 눈에 가까운 Oklab 거리로 고르고, _Strength로 원래 색과 섞는 정도를 정한다.
Shader "Project/PixelPalette"
{
    Properties
    {
        [MainTexture] _MainTex ("Screen", 2D) = "black" {}
        _Strength ("Strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Strength;
            CBUFFER_END

            // C#(PixelView)에서 넣는다: 팔레트 색(선형 RGB)과 그 Oklab 값
            float4 _PaletteLinear[64];
            float4 _PaletteLab[64];
            float _PaletteCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float3 LinearToOklab(float3 c)
            {
                float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
                float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
                float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;
                l = pow(max(l, 0.0), 1.0 / 3.0);
                m = pow(max(m, 0.0), 1.0 / 3.0);
                s = pow(max(s, 0.0), 1.0 / 3.0);
                return float3(
                    0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                    1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                    0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 color = source.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                color = SRGBToLinear(color);
                #endif
                float3 lab = LinearToOklab(color);
                float best = 1e9;
                float3 picked = color;
                int count = (int)_PaletteCount;
                for (int i = 0; i < 64; i++)
                {
                    if (i >= count)
                    {
                        break;
                    }
                    float3 d = lab - _PaletteLab[i].xyz;
                    // 밝기 차이를 조금 더 무겁게 본다(어두운 바닥이 다른 색으로 튀지 않게).
                    float dist = d.x * d.x * 1.4 + d.y * d.y + d.z * d.z;
                    if (dist < best)
                    {
                        best = dist;
                        picked = _PaletteLinear[i].rgb;
                    }
                }
                float3 result = lerp(color, picked, _Strength);
                #if defined(UNITY_COLORSPACE_GAMMA)
                result = LinearToSRGB(result);
                #endif
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
