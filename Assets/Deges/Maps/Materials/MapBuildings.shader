Shader "Nodus/MapBuildings" {
    Properties { [MainColor] _BaseColor ("Color", Color) = (0.65, 0.70, 0.75, 1) }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half4 Frag(Varyings input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
