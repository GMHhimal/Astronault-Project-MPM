// Thin additive fresnel shell around a planet: Earth's blue limb, or a faint grey rim
// that lifts the Moon off a black sky.
Shader "OrionAR/Atmosphere"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.62, 1.0, 1)
        _Power ("Rim Power", Float) = 3.2
        _Intensity ("Intensity", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Atmosphere"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Power;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 vdir = normalize(i.viewWS);
                float rim = pow(saturate(1.0 - dot(n, vdir)), _Power);
                return half4(_Color.rgb * rim * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
