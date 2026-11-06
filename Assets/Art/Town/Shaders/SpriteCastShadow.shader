// Cast shadow for town sprites (vc_TownShadows): draws the sprite's silhouette flattened and
// slanted onto the ground from a ground line (_GroundY, world space), in one soft colour.
// Height above the ground becomes distance along the shadow direction, like a low sun.
Shader "SUNOD/SpriteCastShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ShadowColor ("Shadow colour", Color) = (0.12, 0.10, 0.22, 0.30)
        _GroundY ("Ground line (world y)", Float) = 0
        _Skew ("Slant (x per unit of height)", Float) = 0.75
        _Squash ("Drop (y per unit of height)", Float) = -0.22
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _GroundY, _Skew, _Squash, _Cutoff;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz);
                float h = max(0.0, w.y - _GroundY);
                w.x += h * _Skew;
                w.y = _GroundY + h * _Squash;
                o.positionCS = TransformWorldToHClip(w);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                clip(a - _Cutoff);
                return half4(_ShadowColor.rgb, _ShadowColor.a);
            }
            ENDHLSL
        }
    }
}
