Shader "Hidden/PudinKiller/VFXMeshPreview"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.58, 0.76, 1, 1)
        _BackfaceColor("Backface Color", Color) = (1, 0.08, 0.05, 1)
        _WireColor("Wire Color", Color) = (0.025, 0.035, 0.05, 1)
        [NoScaleOffset] _CheckerTexture("Checker Texture", 2D) = "white" {}
        [Toggle] _UseCheckerTexture("Use Checker Texture", Float) = 0
        _Mode("Mode", Float) = 0
        _PreviewLightDir("Preview Light Direction", Vector) = (0.35, 0.8, 0.45, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle] _BackfacePass("Backface Pass", Float) = 0
        [Toggle] _WirePass("Wire Pass", Float) = 0
        _WireDepthBias("Wire Depth Bias", Float) = 0
        [Toggle] _ZWrite("Z Write", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Preview"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_CheckerTexture);
            SAMPLER(sampler_CheckerTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BackfaceColor;
                float4 _WireColor;
                float4 _PreviewLightDir;
                float _Mode;
                float _BackfacePass;
                float _WirePass;
                float _WireDepthBias;
                float _UseCheckerTexture;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                if (_WirePass > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        output.positionCS.z += _WireDepthBias * output.positionCS.w;
                    #else
                        output.positionCS.z -= _WireDepthBias * output.positionCS.w;
                    #endif
                }
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_WirePass > 0.5)
                {
                    return _WireColor;
                }

                if (_BackfacePass > 0.5)
                {
                    return _BackfaceColor;
                }

                float3 normal = normalize(input.normalWS);

                if (_Mode > 4.5)
                {
                    return input.color;
                }

                if (_Mode > 3.5)
                {
                    return half4(normal * 0.5 + 0.5, 1.0);
                }

                if (_Mode > 2.5)
                {
                    if (_UseCheckerTexture > 0.5)
                    {
                        half3 checkerColor =
                            SAMPLE_TEXTURE2D(_CheckerTexture, sampler_CheckerTexture, input.uv).rgb;
                        return half4(checkerColor, 1.0);
                    }

                    float2 cells = floor(input.uv * 10.0);
                    float checker = fmod(cells.x + cells.y, 2.0);
                    float3 dark = float3(0.08, 0.08, 0.08);
                    float3 light = float3(0.82, 0.82, 0.82);
                    return half4(lerp(dark, light, checker), 1.0);
                }

                if (_Mode > 0.5)
                {
                    return _BaseColor;
                }

                float lighting = saturate(dot(normal, normalize(_PreviewLightDir.xyz)));
                lighting = 0.25 + lighting * 0.75;
                return half4(_BaseColor.rgb * lighting, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
