Shader "EcoCore/CloudShell"
{
    Properties
    {
        _CloudColor   ("Cloud Color", Color) = (1,1,1,1)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.9

        _CloudFrequency ("Cloud Frequency", Float) = 0.25
        _CoverageBias   ("Coverage Bias", Range(0,1)) = 0.4
        _CloudSeed      ("Cloud Seed",   Float) = 42

        _NoiseOffset   ("Noise Time (x=curl, y=boil)", Vector) = (0,0,0,0)
        _PlanetCenter  ("Planet Center", Vector) = (0,0,0,0)

        _ClearanceRadius ("Clearance Radius", Float) = 15
        _ClearanceFade   ("Clearance Fade",   Float) = 5

        _CloudInnerRadius ("Cloud Inner Radius", Float) = 51
        _CloudOuterRadius ("Cloud Outer Radius", Float) = 55

        // Depth / layering
        //_HeightNoiseScale ("Height Noise Scale", Float) = 0.5

        // Curl noise controls
        _CurlFrequency ("Curl Frequency", Float) = 1.0
        _CurlStrength  ("Curl Strength",  Float) = 0.25

        _BoilFrequency ("Boil Frequency (time->slice)", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1; // y = height factor 0..1
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  height      : TEXCOORD3;
            };

            float4 _CloudColor;
            float  _CloudOpacity;

            float  _CloudFrequency;
            float  _CoverageBias;
            float  _CloudSeed;

            float4 _NoiseOffset;
            float4 _PlanetCenter;

            float  _ClearanceRadius;
            float  _ClearanceFade;

            float  _CloudInnerRadius;
            float  _CloudOuterRadius;

            //float  _HeightNoiseScale;

            float  _CurlFrequency;
            float  _CurlStrength;

            float _BoilFrequency;

            // ---- 2D value noise (0..1) -----------------------------------

            float hash21(float2 p)
            {
                p = frac(p * 0.3183099 + float2(0.71, 0.113));
                return frac(23.17 * dot(p, float2(p.x + 0.11, p.y + 0.17)));
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ---- 3D value noise (0..1) -----------------------------------

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));

                float3 u = f * f * (3.0 - 2.0 * f);

                float n00 = lerp(n000, n100, u.x);
                float n10 = lerp(n010, n110, u.x);
                float n01 = lerp(n001, n101, u.x);
                float n11 = lerp(n011, n111, u.x);

                float n0 = lerp(n00, n10, u.y);
                float n1 = lerp(n01, n11, u.y);

                return lerp(n0, n1, u.z);
            }

            float3 RotateAroundY(float3 v, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
            
                return float3(
                    c * v.x + s * v.z,
                    v.y,
                   -s * v.x + c * v.z
                );
            }

            // ---- Curl-like 2D flow from scalar noise ---------------------

            float2 CurlNoise2D(float2 uv, float time)
            {
                float2 p = uv * _CurlFrequency;

                float e = 0.01;

                float nUp   = noise2D(p + float2(0, e));
                float nDown = noise2D(p - float2(0, e));
                float nRight= noise2D(p + float2(e, 0));
                float nLeft = noise2D(p - float2(e, 0));

                float2 grad = float2(nRight - nLeft, nUp - nDown);

                // Perpendicular to gradient = curl-ish flow
                float2 curl = float2(grad.y, -grad.x);

                return curl;
            }
            
            float SampleCloudNoise(float3 dir, float height01)
            {
                float tCurl = -_NoiseOffset.x; // westward rotation (radians-ish)
                float tBoil = _NoiseOffset.y; // boiling / time
            
                // rotate the sampling direction around the planet's Y axis
                float3 dRot = RotateAroundY(dir, tCurl);
            
                // optional height warp (0..1 in your frag)
                //float hWarp = (height01 - 0.5) * _HeightNoiseScale;
                //dRot.y = saturate(dRot.y + hWarp);
            
                float baseFreq = _CloudFrequency;
            
                // octave 1
                float3 p0 = dRot * baseFreq;
                p0 += float3(0, tBoil * _BoilFrequency, 0) + _CloudSeed;
            
                // octave 2
                float3 p1 = dRot * (baseFreq * 2.0);
                p1 += float3(0, tBoil * _BoilFrequency + 7.13, 0) + _CloudSeed * 2.0;
            
                float n0 = noise3D(p0);
                float n1 = noise3D(p1);
            
                return n0 * 0.65 + n1 * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = normalize(worldPos - _PlanetCenter.xyz);

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos    = worldPos;
                OUT.worldNormal = worldNormal;
                OUT.uv          = IN.uv;
                OUT.height      = saturate(IN.uv2.y);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 center = _PlanetCenter.xyz;
                float3 dir    = normalize(IN.worldPos - center);

                // radial shell info
                float r      = distance(IN.worldPos, center);
                float innerR = _CloudInnerRadius;
                float outerR = _CloudOuterRadius;
                float shellT = max(outerR - innerR, 1e-4);

                float radialT = saturate((r - innerR) / shellT);

                // height within shell 0..1
                float height01 = radialT;

                // 1) sample 3D noise with curl-based offset & boiling
                float n = SampleCloudNoise(dir, height01);

                // 2) coverage shaping
                float density = 0.0;
                if (n >= _CoverageBias)
                {
                    density = saturate((n - _CoverageBias) / max(1.0 - _CoverageBias, 1e-4));
                }

                // 3) camera clearance bubble
                float inner = _ClearanceRadius;
                float outer = _ClearanceRadius + _ClearanceFade;

                if (outer > inner + 1e-4)
                {
                    float3 camPosWS = GetCameraPositionWS();
                    float dist = distance(camPosWS, IN.worldPos);

                    if (dist <= inner)
                    {
                        density = 0.0;
                    }
                    else if (dist <= outer)
                    {
                        float t = saturate((dist - inner) / (outer - inner));
                        density *= t;
                    }
                }

                density = saturate(density);
                float alpha = density * _CloudOpacity;

                if (alpha <= 0.001)
                    discard;

                float3 col = _CloudColor.rgb;
                return half4(col, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
