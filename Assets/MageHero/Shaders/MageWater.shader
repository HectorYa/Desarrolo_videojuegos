Shader "MageHero/ToonWater"
{
    // Agua estilizada (toon) para URP, sin texturas: todo es ruido procedural.
    //  · color claro en la orilla y oscuro en lo hondo (usa la textura de profundidad de la cámara)
    //  · espuma irregular y animada donde el agua toca orillas, rocas, pilotes o tablones
    //  · líneas de corriente que avanzan en la dirección del río y destellos del sol
    //  · ondas circulares de impactos (WaterRipples.Add desde C#)
    //  · borde suave en la orilla: el agua nunca termina en una línea recta y dura
    Properties
    {
        _BaseColor("Color del agua", Color) = (0.33,0.71,0.84,0.8)
        _DeepTint("Tinte en lo profundo", Color) = (0.08,0.28,0.5,1)
        _DeepDist("Distancia para el color profundo", Float) = 2.2
        _FoamColor("Color de la espuma", Color) = (1,1,1,1)
        _FoamDist("Ancho de la espuma en orillas", Float) = 0.9
        _FlowDir("Dirección de la corriente (x,z)", Vector) = (1,0,0.35,0)
        _FlowSpeed("Velocidad de la corriente", Float) = 0.45
        _LineScale("Escala de las líneas de corriente", Float) = 0.45
        _LineAmount("Cantidad de líneas", Range(0,1)) = 0.5
        _Sparkle("Destellos del sol", Range(0,2)) = 0.9
        _WaveHeight("Altura de la marea", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _DeepTint, _FoamColor;
                float4 _FlowDir;
                float _DeepDist, _FoamDist, _FlowSpeed, _LineScale, _LineAmount, _Sparkle, _WaveHeight;
            CBUFFER_END

            // Ondas de impacto (las rellena WaterRipples.cs). xyz = posición, w = instante de inicio (en _MH_Now).
            #define MH_RIPPLES 12
            float4 _MH_Ripples[MH_RIPPLES];
            float4 _MH_RippleParams[MH_RIPPLES];   // x = intensidad (0 = vacía), y = tamaño
            float _MH_Now;

            struct A { float4 pos : POSITION; };
            struct V
            {
                float4 cs : SV_POSITION;
                float3 ws : TEXCOORD0;
                float4 sp : TEXCOORD1;
                half fog  : TEXCOORD2;
            };

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
            }
            float Fbm(float2 p) { return Noise(p) * 0.55 + Noise(p * 2.07 + 7.3) * 0.3 + Noise(p * 4.31 + 3.1) * 0.15; }

            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                // marea suave de frecuencia baja: la orilla "respira" y la espuma se mueve con ella
                ws.y += (sin(_Time.y * 0.8) * 0.6 + sin(ws.x * 0.12 + ws.z * 0.09 + _Time.y * 1.1) * 0.4) * _WaveHeight;
                o.ws = ws;
                o.cs = TransformWorldToHClip(ws);
                o.sp = ComputeScreenPos(o.cs);
                o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 flow = normalize(_FlowDir.xz + 1e-4) * _FlowSpeed;
                float2 p = i.ws.xz;

                // ---- profundidad del agua (distancia entre la superficie y lo que hay detrás)
                float2 suv = i.sp.xy / i.sp.w;
                float raw = SampleSceneDepth(suv);
                // UNITY_REVERSED_Z solo existe en DirectX/Vulkan/Metal: en WebGL/OpenGL no está definida, así que
                // se usa con #if (como valor dentro de una expresión rompe el shader y el agua sale rosa).
                #if UNITY_REVERSED_Z
                    float rawOrtho = 1.0 - raw;
                #else
                    float rawOrtho = raw;
                #endif
                float sceneEye = (unity_OrthoParams.w > 0.5)
                    ? lerp(_ProjectionParams.y, _ProjectionParams.z, rawOrtho)
                    : LinearEyeDepth(raw, _ZBufferParams);
                float waterEye = -TransformWorldToView(i.ws).z;
                float depth = max(0.0, sceneEye - waterEye);

                // ---- color base: claro en la orilla, oscuro en lo hondo
                float deepK = saturate(depth / max(0.01, _DeepDist));
                half3 shallow = _BaseColor.rgb * 1.12 + 0.05;
                half3 deep = lerp(_BaseColor.rgb, _DeepTint.rgb, 0.65);
                half3 col = lerp(shallow, deep, deepK * deepK * (3.0 - 2.0 * deepK));

                // ---- normal perturbada por ruido que avanza con la corriente (destellos y reflejo)
                float2 q = p * 0.7 + flow * t;
                float e = 0.15;
                float h0 = Fbm(q), hx = Fbm(q + float2(e, 0)), hz = Fbm(q + float2(0, e));
                float3 n = normalize(float3((h0 - hx) * 2.2, 1.0, (h0 - hz) * 2.2));
                float3 v = normalize(GetWorldSpaceViewDir(i.ws));

                // ---- líneas de corriente estilo cartoon: dos capas de ruido que se cruzan
                float2 lp = p * _LineScale;
                float n1 = Noise(lp + flow * t * 1.0);
                float n2 = Noise(lp * 1.37 + 11.7 - flow * t * 0.35 + float2(sin(t * 0.3), cos(t * 0.27)) * 0.4);
                float streak = smoothstep(0.035, 0.012, abs(n1 - n2)) * _LineAmount;
                streak *= saturate(depth * 1.5);                       // no en la espuma de la orilla
                col = lerp(col, shallow * 1.18 + 0.08, streak);

                // ---- espuma irregular de orilla (y alrededor de cualquier objeto que toque el agua)
                float foamNoise = Fbm(p * 1.6 + flow * t * 0.8);
                float edge = _FoamDist * (0.55 + foamNoise * 0.9);
                float foam = smoothstep(edge, edge * 0.82, depth);
                // segunda línea de espuma que se aleja de la orilla y se desvanece
                float wave = frac(t * 0.35 + foamNoise * 0.5);
                float ring = depth - edge * (1.2 + wave * 1.4);
                float foam2 = smoothstep(0.07, 0.0, abs(ring)) * (1.0 - wave) * step(0.45, Noise(p * 2.3 + t * 0.2));
                foam = saturate(foam + foam2 * 0.8);

                // ---- ondas circulares de impactos (tablones, astillas...)
                float rip = 0.0;
                [unroll]
                for (int k = 0; k < MH_RIPPLES; k++)
                {
                    float str = _MH_RippleParams[k].x;
                    if (str <= 0.0) continue;
                    float size = _MH_RippleParams[k].y;
                    float age = _MH_Now - _MH_Ripples[k].w;
                    float life = 1.6 + size * 0.9;
                    if (age < 0.0 || age > life) continue;
                    float d = distance(p, _MH_Ripples[k].xz);
                    float r1 = age * (1.6 + size * 1.2);
                    float fade = 1.0 - age / life;
                    float w1 = smoothstep(0.16 * size + 0.05, 0.0, abs(d - r1));
                    float w2 = smoothstep(0.1 * size + 0.04, 0.0, abs(d - r1 * 0.62)) * 0.6;
                    rip += (w1 + w2) * fade * fade * str;
                }
                foam = saturate(foam + rip);

                // ---- luz: reflejo del cielo en los bordes (fresnel) y destellos del sol
                Light L = GetMainLight();
                float fres = pow(1.0 - saturate(dot(v, n)), 4.0);
                col = lerp(col, shallow * 1.3 + 0.12, fres * 0.45);
                float3 hv = normalize(L.direction + v);
                float spec = pow(saturate(dot(n, hv)), 220.0);
                col += step(0.35, spec) * _Sparkle * L.color;
                col *= lerp(0.85, 1.0, saturate(L.direction.y + 0.3));

                col = lerp(col, _FoamColor.rgb, foam);

                // ---- transparencia: borde suave en la orilla, más opaca en lo hondo, espuma casi opaca
                half a = lerp(_BaseColor.a * 0.7, min(1.0, _BaseColor.a + 0.12), deepK);
                a = max(a, foam * 0.95);
                a *= saturate(depth / 0.08);
                a = saturate(a + sin(t * 1.5) * 0.02);

                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
