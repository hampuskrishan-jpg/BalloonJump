// Halvgenomskinligt sken: ljusstrålar, giftig rök och raketlåga. Färg och alfa från vertexfärgen.
Shader "Ballong/Glow"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BallongFogCol, _BallongFogParams;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float fog : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                o.col.rgb = GammaToLinearSpace(o.col.rgb);
                #endif
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(wpos, _WorldSpaceCameraPos);
                o.fog = saturate((d - _BallongFogParams.x) / max(1.0, _BallongFogParams.y - _BallongFogParams.x));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(i.col.rgb, i.col.a * (1.0 - i.fog));
            }
            ENDCG
        }
    }
}
