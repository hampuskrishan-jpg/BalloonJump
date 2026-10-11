// Mjuka runda partiklar (dammkorn i luften).
Shader "Ballong/Soft"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.col = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);
                return fixed4(i.col.rgb, i.col.a * saturate(1.0 - d) * saturate(1.0 - d));
            }
            ENDCG
        }
    }
}
