Shader "Custom/Terrain Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _LineTex ("Albedo (RGB)", 2D) = "black" {}
        _NodeColTex ("Node Colors (RGBA)", 2D) = "black" {}
        _SelectionTex ("Selection (RGBA)", 2D) = "black" {}
        // _Glossiness ("Smoothness", Range(0,1)) = 0.0
        // _Metallic ("Metallic", Range(0,1)) = 0.0
        _NumLevels("Num Levels", Float) = 10.0
        _HeightMap("Height Map", 2D) = "black" {}
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Lambert fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.3

        fixed4 _Color;
        sampler2D _LineTex;
        sampler2D _NodeColTex;
        sampler2D _SelectionTex;
        sampler2D _HeightMap;
        sampler2D _BumpMap; 

        float _NumLevels;
        float _MaxHeight;
        float _CurvatureRadius;
        int _UseHeightMap;

        struct Input
        {
            float2 uv_LineTex;
            float3 worldPos;
        };

        float inverseLerp(float a, float b, float value)
        {
            return saturate((value - a) / (b - a));
        }

        float getSphericalHeight(float3 worldPos)
        {
            float3 ray = worldPos - float3(0, -_CurvatureRadius, 0);
            return length(ray) - _CurvatureRadius;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 linkLineCol = tex2D (_LineTex, IN.uv_LineTex);
            fixed4 nodeCol = tex2D (_NodeColTex, IN.uv_LineTex);
            float heightFactor;
            if (_UseHeightMap == 1)
                heightFactor = tex2D (_HeightMap, IN.uv_LineTex);
            else
                heightFactor = inverseLerp(0, _MaxHeight + 0.01, getSphericalHeight(IN.worldPos));
            float modds = fmod(heightFactor, 1.0 / _NumLevels);
            float stepFactor = heightFactor - modds + ceil(modds * _NumLevels) / _NumLevels;

            // lower whiteness intensity by multiplying contour color by 0.7
            stepFactor *= 0.7;
            stepFactor = 0;

            // combine background color and node color texture
            fixed4 nodeColAndBg = fixed4(_Color.rgb * (1 - nodeCol.a) + nodeCol.rgb * nodeCol.a, 1);

            // use node colors and background as base colors, then screen with contour line texture and link lines
            // o.Albedo = 1 - (1 - nodeColAndBg) * (1 - (linkLineCol.rgb));
            o.Albedo = 1 - (1 - nodeColAndBg) * (1 - (stepFactor + linkLineCol.rgb));

            // add selection highlighting
            fixed4 selCol = tex2D (_SelectionTex, IN.uv_LineTex);
            
            // if transparency set to 0, we are only doing outlines, so just add color (this is my own convention)
            if (selCol.a == 0)
                o.Albedo.rgb += selCol.rgb;
            // otherwise, multiply colors
            else
                o.Albedo.rgb *= selCol.rgb;

            // o.Albedo.rgb = float3(1.0, 0.0, 0.0);

            // o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_LineTex));
            o.Alpha = linkLineCol.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
