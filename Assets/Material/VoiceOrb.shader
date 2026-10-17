Shader "Millikan/VoiceOrb"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.42, 1)
        _Level ("Level", Range(0, 1)) = 0
        _Core ("Core", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Level;
                half _Core;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half facing = saturate(dot(normal, input.viewDirWS));
                half rim = pow(1 - facing, 2.4);
                half core = pow(facing, 4) * _Core;
                half glow = rim * (0.45 + _Level * 1.6) + core * (0.25 + _Level);
                return half4(_Color.rgb * glow, 1);
            }
            ENDHLSL
        }
    }
}
