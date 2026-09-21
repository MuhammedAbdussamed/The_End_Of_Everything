Shader "TheEnd/ToonLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.42, 0.22, 1)
        _ShadowColor ("Shadow Color", Color) = (0.035, 0.07, 0.08, 1)
        _RimColor ("Rim Color", Color) = (0.18, 0.75, 0.95, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _EmissionStrength ("Rim Emission", Range(0, 3)) = 0.35
        _BandThreshold ("Light Band Threshold", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _RimColor;
                half _RimPower;
                half _EmissionStrength;
                half _BandThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half lightAmount = saturate(dot(normalWS, mainLight.direction));
                lightAmount *= mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half band = smoothstep(_BandThreshold - 0.18h, _BandThreshold + 0.22h, lightAmount);
                half3 surfaceColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, band);
                half3 color = surfaceColor * mainLight.color * (0.55h + 0.65h * lightAmount);

                // A soft stylized falloff keeps the large board readable even when the
                // scene only contains broad, flat meshes. The positions match the two
                // colored point lights authored in SampleScene.
                half coolHalo = saturate(1.0h - distance(input.positionWS, half3(-10.0h, 10.0h, -12.0h)) / 38.0h);
                half warmHalo = saturate(1.0h - distance(input.positionWS, half3(24.0h, 9.0h, 10.0h)) / 34.0h);
                color += half3(0.025h, 0.10h, 0.20h) * coolHalo;
                color += half3(0.20h, 0.035h, 0.008h) * warmHalo;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightCount; lightIndex++)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        half additionalAmount = saturate(dot(normalWS, light.direction));
                        color += _BaseColor.rgb * light.color * additionalAmount * light.distanceAttenuation * 1.2h;
                    }
                #endif

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower);
                color += _RimColor.rgb * rim * _EmissionStrength;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
