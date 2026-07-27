Shader "Hidden/PudinKiller/VFXMeshPreview"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.58, 0.76, 1, 1)
        _Mode("Mode", Float) = 0
        _PreviewLightDir("Preview Light Direction", Vector) = (0.35, 0.8, 0.45, 0)
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
            Cull Back
            ZWrite On
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _PreviewLightDir;
                float _Mode;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
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
