Shader "Chamber/Horizon Window"
{
    Properties
    {
        _Open ("Aperture", Range(0,1)) = 0
        _Clock ("Simulation clock", Float) = 0
        _Aim ("Beacon offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Open;
                float _Clock;
                float4 _Aim;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Stars(float2 p,float scale)
            {
                p*=scale;
                float2 cell=floor(p);
                float seed=Hash(cell);
                float2 offset=float2(seed,Hash(cell+19.3))*0.65+0.175;
                float d=length(frac(p)-offset);
                float width=max(fwidth(d),0.025);
                return (1-smoothstep(0.015,0.015+width,d))*step(0.87,seed)*(0.4+seed);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p=input.uv*2-1;
                float radius=length(p);
                clip(1-radius);
                float angle=atan2(p.y,p.x);
                float t=_Clock*0.07;
                float2 drift=p+float2(t*0.06,t*0.025);
                float star=Stars(drift,22)+Stars(drift*1.4+15,39)*0.6;
                float cloud=0.5+0.5*sin(p.x*7+p.y*4+t+sin(p.y*8-t));
                cloud*=0.5+0.5*sin(p.y*6-p.x*2-t*0.7);
                float3 sky=float3(0.004,0.008,0.027)+cloud*float3(0.12,0.028,0.28);
                sky+=star*float3(1.0,1.35,1.8);
                // A slowly turning accretion ribbon surrounds a dark horizon.
                float2 q=p-float2(0.02,0.03);
                q=float2(q.x*0.96-q.y*0.28,q.x*0.28+q.y*0.96);
                float ellipse=length(float2(q.x,q.y*3.4));
                float ribbon=exp(-abs(ellipse-0.51)*45);
                float threads=0.65+0.35*sin(ellipse*170-angle*3+t*12);
                sky+=ribbon*threads*float3(1.6,0.6,2.6);
                float hole=length(p-float2(0.02,0.03));
                sky*=smoothstep(0.15,0.17,hole);
                sky+=exp(-abs(hole-0.178)*140)*float3(0.3,0.8,1.7);
                // Rings travel toward the edge as the engine opens.
                float travel=frac(log(max(radius,0.04))*2.5-t*1.8);
                float tunnel=pow(saturate(1-abs(travel-0.5)*2),22)*radius*0.12;
                sky+=tunnel*float3(0.3,0.7,1.7);
                float rim=pow(saturate(radius),22);
                sky+=rim*float3(0.25,0.7,1.4);
                float2 beacon=p-_Aim.xy*0.45;
                float crosshair=(1-smoothstep(0.003,0.008,abs(beacon.x)))*step(abs(beacon.y),0.045)
                    +(1-smoothstep(0.003,0.008,abs(beacon.y)))*step(abs(beacon.x),0.045);
                sky=lerp(float3(0.005,0.01,0.025)+crosshair*float3(0.2,0.6,1),sky,_Open);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
