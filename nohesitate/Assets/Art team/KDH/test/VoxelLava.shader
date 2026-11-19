Shader "Custom/VoxelLava"
{
    Properties
    {
        [HDR] _DeepColor ("식어가는 용암 껍질 색", Color) = (0.16, 0.03, 0.01, 1)
        [HDR] _MidColor ("용암 기본 주황색", Color) = (1.2, 0.25, 0.02, 1)
        [HDR] _HotColor ("고열 노란색 균열", Color) = (2.5, 1.4, 0.2, 1)
        _EmissionPower ("전체 발광 강도", Range(0.5, 5.0)) = 1.6
        _PatternScale ("용암 무늬 크기", Float) = 0.45
        _FlowSpeed ("흐르는 속도", Float) = 0.25
        _VoxelPixelSize ("복셀 도트 각짐 정도 (0 = 부드러움)", Float) = 6.0
        _WaveHeight ("표면 출렁임 높이", Range(0.0, 0.3)) = 0.05
        _EdgeGlowWidth ("벽 접촉면 발광 두께", Range(0.05, 2.0)) = 0.45
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _MidColor;
                float4 _HotColor;
                float _EmissionPower;
                float _PatternScale;
                float _FlowSpeed;
                float _VoxelPixelSize;
                float _WaveHeight;
                float _EdgeGlowWidth;
            CBUFFER_END

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 용암 덩어리와 균열을 만드는 보로노이(Voronoi) 노이즈
            float Voronoi(float2 uv, float timeOffset)
            {
                float2 n = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;

                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 neighbor = float2(i, j);
                        float2 rnd = Hash22(n + neighbor);
                        rnd = 0.5 + 0.45 * sin(timeOffset + 6.2831 * rnd);
                        float2 diff = neighbor + rnd - f;
                        minDist = min(minDist, length(diff));
                    }
                }
                return minDist;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // 용암 표면이 천천히 일렁이는 효과
                float wave = sin(worldPos.x * 1.4 + _Time.y * 1.8) * cos(worldPos.z * 1.4 + _Time.y * 1.5);
                worldPos.y += wave * _WaveHeight;

                output.worldPos = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 평면 크기를 늘려도 패턴이 찌그러지지 않도록 월드 XZ 좌표 사용
                float2 uv = input.worldPos.xz * _PatternScale;

                // 복셀 건축물과 어울리도록 격자 단위로 픽셀화
                if (_VoxelPixelSize > 0.01)
                {
                    uv = floor(uv * _VoxelPixelSize) / _VoxelPixelSize;
                }

                float t = _Time.y * _FlowSpeed;
                float v1 = Voronoi(uv + float2(t, t * 0.6), _Time.y * 0.8);
                float v2 = Voronoi(uv * 1.8 - float2(t * 0.7, -t * 0.9), _Time.y * 1.1);

                // 마그마 균열 강도 계산
                float crack = smoothstep(0.15, 0.75, v1 * 0.65 + v2 * 0.35);

                // 벽이나 기둥과 맞닿는 경계선 감지 (깊이 버퍼 활용)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEyeDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float contactGlow = 1.0 - saturate((sceneEyeDepth - surfaceEyeDepth) / max(0.01, _EdgeGlowWidth));
                contactGlow = pow(contactGlow, 2.0);

                float heat = saturate(crack + contactGlow * 0.55);

                // 3단계 온도 색상 블렌딩 (어두운 껍질 -> 주황 -> 밝은 노랑)
                float3 col = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.55, heat));
                col = lerp(col, _HotColor.rgb, smoothstep(0.55, 1.0, heat));

                col *= _EmissionPower;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}