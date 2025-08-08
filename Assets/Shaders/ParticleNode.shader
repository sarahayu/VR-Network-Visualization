Shader "Custom/ParticleNode"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle(_VERTEX_COLORS)] _VertexColors ("Vertex Colors", Float) = 0
    }
    SubShader
    {
        Tags {"Queue"="Transparent"  "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass 
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog


            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : VAR_COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float4 tangent : TANGENT, float2 uv : TEXCOORD0, float4 color : COLOR)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.uv = TRANSFORM_TEX(uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.color = color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // sample the texture
                half4 col = tex2D(_MainTex, i.uv);
                if (col.a < 0.01) discard;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col * i.color;
            }

            ENDHLSL
        }
    }
}
