Shader "Solbit/Season and Festival"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _UseVertexColor("Use particle color", Float) = 0
        _Bands("Projection bands", Float) = 0
        _Glow("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _UseVertexColor, _Bands, _Glow;
            CBUFFER_END

            struct Attributes
            {
                float3 position : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings result;
                const float3 world = TransformObjectToWorld(input.position);
                result.position = TransformWorldToHClip(world);
                result.height = world.y;
                result.color = lerp(half4(1, 1, 1, 1), input.color, _UseVertexColor);
                result.uv = input.uv;
                return result;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color * _BaseColor;
                half band = smoothstep(.25, .62, frac(input.height * 2.7 - _Time.y * .85));
                color.a *= lerp(1, .48 + .52 * band, _Bands);
                color.rgb *= _Glow;
                return color;
            }
            ENDHLSL
        }
    }
}
