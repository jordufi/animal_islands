// Stylised low-poly water (La Serenisima). One mesh, one pass, no depth / opaque texture, no reflections.
// The mesh is built by the setup from the layout (IslaZombieSetup.VenBuildWater) and carries in its vertex colour:
//   R = distance to the nearest quay / 3 m (0 at the quay edge, 1 in open water)   -> shallow-deep colour, foam band
//   G = 1 over a street (only visible in the acqua alta)                           -> light, see-through
// Facets: flat normals from screen derivatives, exaggerated so the small waves (they must not hit the quays) read
// as big low-poly facets; a stepped highlight gives white glints on the facets facing the sun.
Shader "Island/LowPolyWater"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.05, 0.34, 0.44, 1)
        _Shallow ("Shallow", Color) = (0.16, 0.6, 0.6, 1)
        _Street ("Flooded street", Color) = (0.36, 0.66, 0.62, 1)
        _Foam ("Foam", Color) = (0.93, 0.97, 0.92, 1)
        _StreetAlpha ("Flooded street alpha", Range(0, 1)) = 0.5
        _WaveHeight ("Wave height (m)", Float) = 0.09
        _WaveSpeed ("Wave speed", Float) = 1.1
        _Facets ("Facet strength", Float) = 6
        _FoamWidth ("Foam width (0-1 of 3 m)", Range(0, 0.5)) = 0.16
        _Glint ("Glint threshold", Range(0.8, 1)) = 0.995
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep, _Shallow, _Street, _Foam;
                half _StreetAlpha, _Facets, _FoamWidth, _Glint;
                float _WaveHeight, _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            float Waves(float2 p, float t)
            {
                return sin(p.x * 0.55 + t * 1.3) * 0.5
                     + sin(p.y * 0.7 - t * 1.05) * 0.35
                     + sin((p.x + p.y) * 1.1 + t * 1.9) * 0.15;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                // calmer against the quays and in the streets (no wave through a doorway)
                float calm = saturate(v.color.r * 2.0) * (1.0 - v.color.g * 0.6);
                ws.y += Waves(ws.xz, _Time.y * _WaveSpeed) * _WaveHeight * (0.35 + 0.65 * calm);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dx = ddx(i.positionWS), dy = ddy(i.positionWS);
                float3 n = normalize(cross(dx, dy));
                n *= n.y < 0 ? -1.0 : 1.0;   // ddy's sign depends on the graphics API: the water always faces up
                n = normalize(float3(n.x * _Facets, n.y, n.z * _Facets));   // the facets read big, the waves stay small

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = lerp(0.55h, 1.0h, light.shadowAttenuation);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                half depth = i.color.r, street = i.color.g;
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0.1, 0.9, depth));
                col = lerp(col, _Street.rgb, street);

                // faceted light: the sun plus a fixed tilt term, so every facet gets its own shade even with a low sun
                half ndl = saturate(dot(n, light.direction));
                half tilt = dot(n.xz, float2(0.55, 0.35));
                half lit = saturate(0.72h + ndl * 0.45h + tilt * 0.9h);
                col *= lit * shadow * (0.6h + 0.4h * light.color) + SampleSH(float3(0, 1, 0)) * 0.12h;

                // horizon: a little lighter
                half fres = pow(1.0h - saturate(dot(n, v)), 4.0h);
                col = lerp(col, _Shallow.rgb * 1.35h, fres * 0.35h);

                // glints on the facets that face the sun
                float3 h = normalize(light.direction + v);
                // only some facets sparkle (hash of the flat normal: the same for the whole facet)
                half pick = step(0.7, frac(sin(dot(floor(n.xz * 24.0), float2(127.1, 311.7))) * 43758.5453));
                half glint = step(_Glint, dot(n, h)) * pick * light.shadowAttenuation * (1.0h - street);
                col = lerp(col, _Foam.rgb, glint * 0.7h);

                // foam band along the quays, lapping in and out
                float t = _Time.y * _WaveSpeed;
                float wob = sin(i.positionWS.x * 1.7 + t * 1.6) * 0.5 + sin(i.positionWS.z * 2.1 - t * 1.2) * 0.5;
                half foam = step(depth, _FoamWidth * (0.75 + 0.25 * wob)) * (1.0h - street);
                half ring = step(abs(depth - (_FoamWidth * 1.9 + 0.05 * sin(t * 0.9))), 0.018) * (1.0h - street) * step(0.5, frac(dot(i.positionWS.xz, float2(0.31, 0.27)) + t * 0.05) + 0.25);
                col = lerp(col, _Foam.rgb * (0.7h + 0.3h * shadow), saturate(foam + ring * 0.7h));

                half alpha = lerp(1.0h, _StreetAlpha, street);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
