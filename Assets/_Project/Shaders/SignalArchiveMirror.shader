Shader "Chamber/Signal Archive Mirror"
{
    Properties
    {
        _BaseColor ("Floor tint", Color) = (0.008,0.014,0.023,1)
        _ReflectionTex ("Live reflection", 2D) = "black" {}
        _ReflectionStrength ("Reflection strength", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ReflectionStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screen.xy / input.screen.w;
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv).rgb;
                float grazing = 1 - saturate(normalize(GetWorldSpaceViewDir(input.positionWS)).y);
                return half4(_BaseColor.rgb + reflection * _ReflectionStrength * lerp(0.65, 1, grazing), 1);
            }
            ENDHLSL
        }
    }
}
