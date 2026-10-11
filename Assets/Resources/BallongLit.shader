// Realistisk belysning (Unitys Standard-ljus med skuggor). Färgen kommer från vertexfärgen.
// Vertexfärgens alfa < 0,5 betyder blank yta (glas, ballonger, mynt, vatten).
// _Detail lägger på lite smuts och variation i putsen så att husen inte ser plastiga ut.
Shader "Ballong/Lit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Detail ("Detail", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        #pragma require 2darray

        fixed4 _Color;
        float _Detail;
        float _BallongDepthDark;
        UNITY_DECLARE_TEX2DARRAY(_Photo);
        float _PhotoScale[24];
        float _HasPhoto;

        struct Input
        {
            float4 vcol;
            float matId;
            float2 cloth;
            float3 worldPos;
            float3 worldNormal;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
            o.matId = v.texcoord.x;
            o.cloth = v.texcoord2.xy;
        }

        float hash (float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float noise (float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                             lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                             lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 c = IN.vcol.rgb * _Color.rgb;
            float3 p = IN.worldPos;
            float n = noise(p * 1.9) * 0.45 + noise(p * 7.3) * 0.25 + noise(p * 0.3) * 0.3;
            float streak = noise(float3(p.x * 3.0, p.y * 0.25, p.z * 3.0)); // regnränder nedför väggarna
            float id = floor(IN.matId + 0.5);
            float grime = _Detail;
            if (id < -0.5 && _HasPhoto > 0.5)
            {
                // tyg på gubben: följer kroppens yta (UV i meter) så att mönstret rör sig med honom
                float slice = -id;
                float3 t = UNITY_SAMPLE_TEX2DARRAY(_Photo, float3(IN.cloth / _PhotoScale[(int)slice], slice)).rgb;
                float tl = dot(t, float3(0.333, 0.333, 0.334));
                c = c * lerp(0.55, 1.45, saturate(tl * 1.6)) * lerp(float3(1,1,1), t / max(tl, 0.05), 0.25);
                grime = 0;
            }
            else if (id > 0.5 && _HasPhoto > 0.5)
            {
                // riktig fototextur, projicerad från tre håll (triplanar) i världskoordinater
                float s = 1.0 / _PhotoScale[(int)id];
                float3 w = pow(abs(normalize(IN.worldNormal)), 4.0);
                w /= (w.x + w.y + w.z);
                float3 q = p * s;
                float3 t = UNITY_SAMPLE_TEX2DARRAY(_Photo, float3(q.zy, id)).rgb * w.x
                         + UNITY_SAMPLE_TEX2DARRAY(_Photo, float3(q.xz, id)).rgb * w.y
                         + UNITY_SAMPLE_TEX2DARRAY(_Photo, float3(q.xy, id)).rgb * w.z;
                float lum = dot(c, float3(0.333, 0.333, 0.334));
                float3 tint = c / max(lum, 0.15);
                c = t * lerp(float3(1, 1, 1), tint, 0.5) * lerp(0.7, 1.25, saturate(lum * 1.4));
                grime *= 0.6;
            }
            c *= lerp(1.0, 0.75 + 0.35 * n - 0.15 * streak, grime); // väderbitet: fläckar och regnränder
            // djupet: långt nere i gatan blir det mörkare
            float depth = saturate((_WorldSpaceCameraPos.y - p.y - 10.0) / 90.0);
            c *= lerp(1.0, _BallongDepthDark, depth);
            o.Albedo = c;
            float a = IN.vcol.a;
            o.Smoothness = a < 0.5 ? saturate(1.0 - a * 2.0) : 0.1 + 0.08 * n * _Detail;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
