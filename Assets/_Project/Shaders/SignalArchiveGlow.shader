Shader "Chamber/Signal Archive Glow"
{
    Properties
    {
        [HDR] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 2
        _Radial ("Soft point", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Intensity;
                float _Radial;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = input.uv * 2 - 1;
                float falloff = saturate(1 - dot(offset, offset));
                // Lines and the filament mesh both put their width in UV.y.
                // A broad soft shoulder retains color instead of a clipped,
                // one-pixel wire; derivatives keep its outer edge stable.
                float across = abs(input.uv.y * 2 - 1);
                float feather = max(fwidth(across), 0.025);
                float ribbon = pow(saturate(1 - across * across), 1.35);
                ribbon *= 1 - smoothstep(1 - feather, 1, across);
                return half4(input.color.rgb * _Intensity,
                    input.color.a * lerp(ribbon, falloff * falloff, _Radial));
            }
            ENDHLSL
        }
    }
}
