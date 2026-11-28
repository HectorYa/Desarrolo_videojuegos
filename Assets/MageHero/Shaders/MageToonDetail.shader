Shader "MageHero/ToonDetail"
{
    // Toon con detalle procedural (sin texturas) para piedra y madera. Mismo sombreado por bandas que
    // MageHero/ToonLit, con el color base del vértice, más:
    //  Piedra (_Mode = 0): moteado mineral, vetas y cavidades oscuras, musgo en las caras que miran arriba,
    //                      venas de lava que se abren con el daño (_Damage) y laten (_VeinGlow).
    //  Madera (_Mode = 1): vetas a lo largo de la fibra (dirección en UV1, la pone LowPoly.Grain), nudos,
    //                      cantos desgastados y madera mojada/musgosa por debajo de _WetLevel.
    //  Ambos: brillo de borde a contraluz y destello blanco al recibir un golpe (_Flash).
    Properties
    {
        [Enum(Piedra,0,Madera,1)] _Mode("Material", Float) = 0
        _BaseColor("Color base", Color) = (1,1,1,1)
        _Threshold("Umbral de sombra", Range(0,1)) = 0.35
        _DetailScale("Escala del detalle", Float) = 3
        _DetailStrength("Fuerza del detalle", Range(0,1)) = 0.5
        _MossColor("Musgo", Color) = (0.33,0.45,0.2,1)
        _MossAmount("Cantidad de musgo", Range(0,1)) = 0.35
        _VeinColor("Venas de lava (HDR)", Color) = (3.2,1.05,0.22,1)
        _Damage("Daño (venas abiertas)", Range(0,1)) = 0
        _VeinGlow("Brillo de las venas", Float) = 1
        _WetLevel("Altura del agua (madera mojada)", Float) = -1000
        _RimColor("Brillo de borde", Color) = (1,0.9,0.75,1)
        _RimAmount("Cantidad de brillo de borde", Range(0,1)) = 0.35
        _Flash("Destello de golpe", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _MossColor, _VeinColor, _RimColor;
            float _Mode, _Threshold, _DetailScale, _DetailStrength, _MossAmount, _Damage, _VeinGlow, _WetLevel, _RimAmount, _Flash;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos : POSITION; float3 n : NORMAL; half4 col : COLOR; float3 grain : TEXCOORD1; };
            struct V
            {
                float4 cs : SV_POSITION;
                float3 nWS : TEXCOORD0;
                float3 pWS : TEXCOORD1;
                float3 pOS : TEXCOORD2;
                float3 gOS : TEXCOORD3;
                float3 nOS : TEXCOORD4;
                half4 col : COLOR;
                half fog : TEXCOORD5;
            };

            float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1,0,0)), f.x), lerp(Hash3(i + float3(0,1,0)), Hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0,0,1)), Hash3(i + float3(1,0,1)), f.x), lerp(Hash3(i + float3(0,1,1)), Hash3(i + 1), f.x), f.y), f.z);
            }
            float Fbm3(float3 p) { return Noise3(p) * 0.5 + Noise3(p * 2.03 + 5.1) * 0.3 + Noise3(p * 4.07 + 9.7) * 0.2; }

            V vert(A i)
            {
                V o;
                o.pWS = TransformObjectToWorld(i.pos.xyz);
                o.cs  = TransformWorldToHClip(o.pWS);
                o.nWS = TransformObjectToWorldNormal(i.n);
                o.pOS = i.pos.xyz;
                o.nOS = i.n;
                o.gOS = i.grain;
                o.col = i.col;
                o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                half3 baseC = _BaseColor.rgb * i.col.rgb;
                half3 emit = 0;
                float s = _DetailScale;

                if (_Mode < 0.5)
                {
                    // ---------------- PIEDRA
                    float3 p = i.pOS * s;
                    float speck = Noise3(p * 6.0);                                    // moteado mineral fino
                    float big = Fbm3(p * 0.8);                                        // manchas grandes
                    float strata = sin((i.pOS.y + big * 0.35) * s * 5.0) * 0.5 + 0.5; // vetas horizontales
                    float cavity = smoothstep(0.62, 0.3, Fbm3(p * 1.7 + 3.0));      // huecos oscuros
                    half shade = 1.0 + (speck - 0.5) * 0.2 * _DetailStrength
                                     + (big - 0.5) * 0.35 * _DetailStrength
                                     + (strata - 0.5) * 0.08 * _DetailStrength
                                     - cavity * 0.22 * _DetailStrength;
                    baseC *= shade;
                    // pequeños cristales claros
                    baseC = lerp(baseC, baseC * 1.35 + 0.05, step(0.93, Noise3(p * 11.0)) * _DetailStrength);
                    // musgo arriba (y en los huecos), con borde irregular
                    float up = saturate(n.y);
                    float mossN = Fbm3(i.pOS * s * 1.3 + 17.0);
                    float moss = smoothstep(0.55, 0.75, up * 0.8 + mossN * 0.55 + cavity * 0.15) * _MossAmount;
                    baseC = lerp(baseC, _MossColor.rgb * (0.85 + mossN * 0.3), moss);
                    // venas de lava: líneas finas del ruido que se abren a medida que sube el daño
                    // (solo aparecen por zonas: la máscara cubre más superficie cuanto mayor es el daño)
                    float vn = Noise3(i.pOS * s * 0.45 + 31.0);
                    float zone = saturate((Fbm3(i.pOS * s * 0.3 + 71.0) - (0.62 - _Damage * 0.42)) * 6.0) * step(0.02, _Damage);
                    float width = 0.015 + 0.03 * _Damage;
                    float vein = smoothstep(width, width * 0.3, abs(vn - 0.5)) * zone;
                    float pulse = 0.8 + 0.2 * sin(_Time.y * 4.0 + vn * 12.0);
                    baseC = lerp(baseC, baseC * 0.4, smoothstep(width * 2.4, width, abs(vn - 0.5)) * zone); // bordes chamuscados
                    emit += _VeinColor.rgb * vein * _VeinGlow * pulse;
                }
                else
                {
                    // ---------------- MADERA
                    float3 g = i.gOS;
                    g = dot(g, g) > 0.01 ? normalize(g) : float3(0, 1, 0);
                    float along = dot(i.pOS, g);
                    float3 across = i.pOS - g * along;
                    float warp = Noise3(i.pOS * s * 0.6) * 0.6;
                    float rings = frac((length(across * float3(1, 1.6, 1)) * s * 4.0) + warp + Noise3(float3(along * 0.5, 0, 0)) * 0.4);
                    float grainLines = smoothstep(0.0, 0.12, rings) * smoothstep(1.0, 0.75, rings);
                    float fibre = Noise3(across * s * 14.0 + g * along * s * 0.35);
                    half shade = 1.0 - (1.0 - grainLines) * 0.16 * _DetailStrength + (fibre - 0.5) * 0.18 * _DetailStrength;
                    // nudos
                    float knot = Noise3(i.pOS * s * 1.1 + 7.0);
                    shade -= smoothstep(0.82, 0.92, knot) * 0.3 * _DetailStrength;
                    baseC *= shade;
                    // cantos: las caras laterales algo más oscuras (madera curtida)
                    float side = 1.0 - abs(dot(normalize(i.nOS), g));
                    baseC *= lerp(1.0, 0.9, (1.0 - saturate(abs(n.y))) * 0.5);
                    // mojada y con musgo cerca del agua
                    float wet = saturate((_WetLevel + 0.35 - i.pWS.y) / 0.5);
                    float wetEdge = Noise3(i.pWS * 2.5) * 0.25;
                    wet = saturate(wet + wetEdge * wet);
                    baseC = lerp(baseC, baseC * half3(0.55, 0.6, 0.55), wet);
                    float moss = smoothstep(0.55, 0.8, Fbm3(i.pWS * 1.8) + wet * 0.35 + saturate(n.y) * 0.15) * _MossAmount * (0.35 + wet);
                    baseC = lerp(baseC, _MossColor.rgb, moss);
                }

                Light l = GetMainLight(TransformWorldToShadowCoord(i.pWS));
                half ndl = saturate(dot(n, l.direction)) * l.shadowAttenuation;
                half band = smoothstep(_Threshold - 0.02, _Threshold + 0.02, ndl);
                half3 shadeC = baseC * half3(0.55h, 0.58h, 0.72h);
                half3 c = lerp(shadeC, baseC, band) * (0.6h + 0.4h * l.color);

                // brillo de borde (silueta a contraluz)
                float3 v = normalize(GetWorldSpaceViewDir(i.pWS));
                float rim = smoothstep(0.62, 0.78, 1.0 - saturate(dot(v, n)));
                c += _RimColor.rgb * rim * _RimAmount * (0.35 + 0.65 * band);

                c += emit;
                c = lerp(c, half3(1.6, 1.5, 1.4), _Flash);
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertS
            #pragma fragment fragS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct AS { float4 pos : POSITION; float3 n : NORMAL; };
            struct VS { float4 cs : SV_POSITION; };
            VS vertS(AS i)
            {
                VS o;
                float3 pWS = TransformObjectToWorld(i.pos.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.n);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - pWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                o.cs = TransformWorldToHClip(ApplyShadowBias(pWS, nWS, ld));
                #if UNITY_REVERSED_Z
                    o.cs.z = min(o.cs.z, o.cs.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.cs.z = max(o.cs.z, o.cs.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 fragS(VS i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertD
            #pragma fragment fragD
            struct AD { float4 pos : POSITION; };
            struct VD { float4 cs : SV_POSITION; };
            VD vertD(AD i) { VD o; o.cs = TransformObjectToHClip(i.pos.xyz); return o; }
            half fragD(VD i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
