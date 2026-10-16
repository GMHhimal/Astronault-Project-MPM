// Starfield: additive soft points coloured by vertex colour, visible from either side.
// URP's Unlit shader ignores vertex colour and culls back faces, which together made
// every star in the earlier build invisible from inside the sky sphere.
Shader "OrionAR/StarPoint"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "StarPoint"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2.0 - 1.0;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Bright core with a soft halo: reads as a star, not a square.
                float d2 = dot(i.uv, i.uv);
                float core = saturate(1.0 - d2 * 4.0);
                float halo = saturate(1.0 - d2);
                float a = core * core + halo * halo * halo * 0.35;
                return half4(i.color.rgb * i.color.a * a * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
