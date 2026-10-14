Shader "Nodus/BridgeComparison"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.7,0.7,0.7,1)
        _NodusComparisonSide ("Side", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _NodusComparisonSide;
            CBUFFER_END
            float _NodusComparisonEnabled;
            float _NodusComparisonCut;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_NodusComparisonEnabled > 0.5)
                {
                    float x = input.positionCS.x / _ScaledScreenParams.x;
                    if (_NodusComparisonSide < 0.5) clip(_NodusComparisonCut - x);
                    else clip(x - _NodusComparisonCut);
                }
                float lighting = 0.72 + 0.28 * saturate(dot(normalize(input.normalWS), normalize(float3(0.35,0.85,0.4))));
                return half4(_BaseColor.rgb * lighting, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
