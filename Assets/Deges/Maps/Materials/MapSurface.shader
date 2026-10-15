Shader "Nodus/MapSurface" {
    Properties {
        _BaseColor ("Color", Color) = (0.55, 0.63, 0.68, 1)
        _SurfaceAlpha ("Opacity", Range(0, 1)) = 1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _SurfaceAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input) {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target {
                return half4(_BaseColor.rgb, _BaseColor.a * _SurfaceAlpha);
            }
            ENDHLSL
        }
    }
}
