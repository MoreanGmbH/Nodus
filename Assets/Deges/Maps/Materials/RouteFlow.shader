Shader "Nodus/RouteFlow" {
    Properties {
        _BaseColor ("Route Color", Color) = (1, 0.55, 0.1, 0.9)
        _FlowSpeed ("Flow Speed", Float) = 0.8
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FlowSpeed;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input input) {
                Output output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Output input) : SV_Target {
                float progress = frac(input.uv.x - _Time.y * _FlowSpeed);
                float highlight = step(0.18, progress) * (1 - step(0.68, progress));
                half4 color = _BaseColor;
                color.a *= lerp(0.30, 1, highlight);
                return color;
            }
            ENDHLSL
        }
    }
}
