Shader "Custom/VertexColorLit"
{
    Properties
    {
        _Gloss ("Smoothness", Range(0,1)) = 0.2
        _Spec  ("Specular", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            half _Gloss;
            half _Spec;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 nrm : TEXCOORD0;
                fixed4 col : COLOR;
                float3 wpos: TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos  = UnityObjectToClipPos(v.vertex);
                o.nrm  = UnityObjectToWorldNormal(v.normal);
                o.col  = v.color;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.nrm);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);

                // diffuse
                float ndl = max(0, dot(N, L));
                fixed3 diffuse = i.col.rgb * (_LightColor0.rgb * ndl);

                // simple specular
                float3 H = normalize(L + V);
                float spec = pow(max(0, dot(N, H)), lerp(8, 128, _Gloss)) * _Spec;

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * i.col.rgb;

                return fixed4(ambient + diffuse + spec, 1);
            }
            ENDCG
        }
    }
}
