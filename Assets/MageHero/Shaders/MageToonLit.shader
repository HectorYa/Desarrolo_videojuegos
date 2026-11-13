Shader "MageHero/ToonLit"
{
    Properties
    {
        _BaseColor("Color base", Color) = (1,1,1,1)
        _ShadeColor("Color sombra", Color) = (0.4,0.4,0.6,1)
        _Threshold("Umbral de sombra", Range(0,1)) = 0.35
        [Toggle] _UseVertexColor("Usar color de vértice (terreno y props)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _ShadeColor; half _Threshold; half _UseVertexColor;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; half4 col : COLOR; };
            struct V { float4 cs : SV_POSITION; float3 nWS : TEXCOORD0; float3 pWS : TEXCOORD1; half4 col : COLOR; half fog : TEXCOORD2; };

            V vert(A i)
            {
                V o;
                o.pWS = TransformObjectToWorld(i.pos.xyz);
                o.cs  = TransformWorldToHClip(o.pWS);
                o.nWS = TransformObjectToWorldNormal(i.n);
                o.col = i.col;
                o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                Light l = GetMainLight(TransformWorldToShadowCoord(i.pWS));
                half ndl = saturate(dot(normalize(i.nWS), l.direction)) * l.shadowAttenuation;
                half band = smoothstep(_Threshold - 0.02, _Threshold + 0.02, ndl);
                half3 baseC = _BaseColor.rgb, shadeC = _ShadeColor.rgb;
                if (_UseVertexColor > 0.5h) { baseC *= i.col.rgb; shadeC = baseC * half3(0.55h, 0.58h, 0.72h); }
                half3 c = lerp(shadeC, baseC, band) * (0.6h + 0.4h * l.color);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _ShadeColor; half _Threshold; half _UseVertexColor;
            CBUFFER_END
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half4 _ShadeColor; half _Threshold; half _UseVertexColor;
            CBUFFER_END

            struct AD { float4 pos : POSITION; };
            struct VD { float4 cs : SV_POSITION; };
            VD vertD(AD i) { VD o; o.cs = TransformObjectToHClip(i.pos.xyz); return o; }
            half fragD(VD i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
