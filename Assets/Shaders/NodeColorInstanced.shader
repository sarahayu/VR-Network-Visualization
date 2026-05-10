Shader "Custom/Node Color Instanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

        _AmbientStrength  ("Ambient Strength",  Range(0,1))   = 0.10
        _DiffuseStrength  ("Diffuse Strength",  Range(0,1))   = 0.90
        _SpecularColor    ("Specular Color",     Color)        = (1,1,1,1)
        _SpecularStrength ("Specular Strength",  Range(0,1))   = 0.30
        _Shininess        ("Shininess",          Range(4,256)) = 48
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AmbientStrength;
                float  _DiffuseStrength;
                float4 _SpecularColor;
                float  _SpecularStrength;
                float  _Shininess;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv)
                                * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Compute shadow coordinates so the node receives cast shadows
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight    = GetMainLight(shadowCoord);
                float3 lightDir    = normalize(mainLight.direction);
                float  shadow      = mainLight.shadowAttenuation;

                // Ambient — not occluded by shadows (fills in the dark side)
                float3 ambient  = _AmbientStrength * baseColor.rgb;

                // Diffuse (Lambert) — attenuated by both NdotL and shadow
                float  NdotL    = max(0.0, dot(normalWS, lightDir));
                float3 diffuse  = _DiffuseStrength * NdotL * shadow * baseColor.rgb;

                // Specular (Blinn-Phong) — also attenuated by shadow
                float3 halfDir  = normalize(lightDir + viewDirWS);
                float  NdotH    = max(0.0, dot(normalWS, halfDir));
                float3 specular = _SpecularStrength
                                * pow(NdotH, _Shininess)
                                * shadow
                                * _SpecularColor.rgb;

                return half4(saturate(ambient + diffuse + specular), baseColor.a);
            }
            ENDHLSL
        }
    }

    // Fallback for non-URP or older hardware — plain unlit so nodes are still visible
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AmbientStrength;
                float  _DiffuseStrength;
                float4 _SpecularColor;
                float  _SpecularStrength;
                float  _Shininess;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv)
                     * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            }
            ENDHLSL
        }
    }
}
