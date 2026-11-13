Shader "MageHero/ToonWater"
{
    Properties { _BaseColor("Color del agua", Color) = (0.33,0.71,0.84,0.8) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct A { float4 pos : POSITION; };
            struct V { float4 cs : SV_POSITION; half fog : TEXCOORD0; };

            V vert(A i) { V o; o.cs = TransformObjectToHClip(i.pos.xyz); o.fog = ComputeFogFactor(o.cs.z); return o; }

            half4 frag(V i) : SV_Target
            {
                half a = saturate(_BaseColor.a + sin(_Time.y * 1.5) * 0.04);
                half3 c = MixFog(_BaseColor.rgb, i.fog);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
