Shader "Custom/VoxelTorchFire"
{
    Properties
    {
        _BaseMap ("텍스처 (있을 경우)", 2D) = "white" {}
        _BaseColor ("색상 보정 (Tint)", Color) = (1.0, 0.9, 0.75, 1)
        _Alpha ("불투명도 (낮을수록 투명)", Range(0.15, 1.0)) = 0.62
        [HDR] _GlowTint ("발광 색상", Color) = (1.8, 0.65, 0.12, 1)
        _EmissionPower ("빛나는 강도", Range(0.0, 4.0)) = 1.35
        _UseVertexColor ("버텍스 컬러 사용 (1=켜기, 0=끄기)", Float) = 1.0
        _FlickerSpeed ("일렁임 속도", Float) = 7.0
        _FlickerAmount ("불꽃 흔들림 크기", Range(0.0, 0.08)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float  localY     : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Alpha;
                float4 _GlowTint;
                float  _EmissionPower;
                float  _UseVertexColor;
                float  _FlickerSpeed;
                float  _FlickerAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;

                // 아래쪽(나무와 맞닿는 부분)은 고정하고 위쪽 불꽃 끝만 살랑거리게 흔듦
                float heightMask = saturate(pos.y + 0.35);
                float t = _Time.y * _FlickerSpeed;
                pos.x += sin(t + pos.y * 8.0) * _FlickerAmount * heightMask;
                pos.z += cos(t * 0.85 + pos.y * 8.0) * _FlickerAmount * heightMask;

                output.positionCS = TransformObjectToHClip(pos);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.localY = pos.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 vCol = (_UseVertexColor > 0.5) ? input.color.rgb : half3(1.0, 1.0, 1.0);

                // 원본 복셀 색상 유지
                half3 baseRGB = texCol.rgb * vCol * _BaseColor.rgb;

                // 시간에 따라 미세하게 숨 쉬듯 밝아지는 발광 효과
                float pulse = 0.88 + 0.12 * sin(_Time.y * (_FlickerSpeed * 1.3));
                half3 glow = baseRGB * _GlowTint.rgb * _EmissionPower * pulse;

                half3 finalRGB = baseRGB + glow;
                return half4(finalRGB, _Alpha);
            }
            ENDHLSL
        }
    }
}