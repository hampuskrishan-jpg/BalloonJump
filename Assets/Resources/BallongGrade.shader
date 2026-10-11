// Filmiskt efterfilter: glöd (bloom), färgkorrigering mot kallt blågrått, kontrast och filmbrus.
Shader "Ballong/Grade"
{
    Properties { _MainTex ("", 2D) = "white" {} _Bloom ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float2 _Dir;
    float _BloomStrength, _Desat, _Grain, _Cave;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    // 0: plocka ut de ljusaste delarna (det som ska glöda)
    fixed4 fragBright (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        float3 c = (tex2D(_MainTex, i.uv + d * float2(-1,-1)).rgb + tex2D(_MainTex, i.uv + d * float2(1,-1)).rgb +
                    tex2D(_MainTex, i.uv + d * float2(-1, 1)).rgb + tex2D(_MainTex, i.uv + d * float2(1, 1)).rgb) * 0.25;
        float l = max(c.r, max(c.g, c.b));
        return fixed4(c * saturate((l - 0.55) * 2.0), 1);
    }

    // 1: suddig (gauss) i riktning _Dir
    fixed4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * _Dir;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
        c += (tex2D(_MainTex, i.uv + d * 1.38).rgb + tex2D(_MainTex, i.uv - d * 1.38).rgb) * 0.316;
        c += (tex2D(_MainTex, i.uv + d * 3.23).rgb + tex2D(_MainTex, i.uv - d * 3.23).rgb) * 0.07;
        return fixed4(c, 1);
    }

    float hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

    // 2: slutbild
    fixed4 fragFinal (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _BloomStrength;
        float l = dot(c, float3(0.299, 0.587, 0.114));
        c = lerp(float3(l, l, l), c, 1.0 - _Desat);                       // dämpade färger
        float3 shadowTint = lerp(float3(0.0, 0.035, 0.07), float3(0.0, 0.04, 0.08), _Cave);
        c += shadowTint * (1.0 - smoothstep(0.0, 0.5, l));               // kalla, blågröna skuggor
        c += float3(0.05, 0.025, -0.02) * smoothstep(0.45, 1.0, l);      // varma högdagrar
        c = saturate(c);
        c = lerp(c, c * c * (3.0 - 2.0 * c), 0.45);                      // filmisk kontrast (S-kurva)
        c += (hash(i.uv * _ScreenParams.xy + _Time.y * 61.0) - 0.5) * _Grain; // filmbrus
        return fixed4(c, 1);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragBright
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragBlur
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragFinal
               ENDCG }
    }
}
