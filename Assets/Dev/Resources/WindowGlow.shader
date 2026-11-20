Shader "Solbit/Window Glow"
{
    Properties
    {
        [MainColor] _BaseColor("Glass color", Color) = (1,1,1,1)
        [HDR] _EmissionColor("Window light", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes { float3 position : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 position : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.position);
                output.position = TransformWorldToHClip(output.world);
                output.normal = TransformObjectToWorldNormal(input.normal);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normal);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half3 lighting = max(SampleSH(normal), half3(.15, .15, .15)) +
                    sun.color * saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                return half4(_BaseColor.rgb * lighting + _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}