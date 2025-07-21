Shader "Custom/Minimap"
{
    Properties
    {
        _Color ("Darken", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _FOV ("FOV", float) = 100
    }
    SubShader
    {
        Tags {"Queue"="Transparent"  "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200

        Pass
        {            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv           : TEXCOORD0;
                float4 pos          : SV_POSITION;
            };            

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float4 tangent : TANGENT, float2 uv : TEXCOORD0)
            {
                v2f OUT;                
                OUT.pos = UnityObjectToClipPos(vertex);
                OUT.uv = uv;
                return OUT;
            }

            sampler2D _MainTex;
            float _FOV;
            half4 _Color;

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.uv);
                if (color.a < 0.01) discard;

                half4 darken;

                float cosFOV = cos(_FOV * 3.14 / 180 / 2);

                float2 vec = IN.uv - float2(0.5, 0.5);
                float2 forward = float2(0, -1);
                float cosVec = dot(vec, forward) / sqrt(vec.x * vec.x + vec.y * vec.y);
 
                if (cosVec > cosFOV) darken = half4(1, 1, 1, 1);
                else darken = _Color;

                return color * darken;
            }

            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
