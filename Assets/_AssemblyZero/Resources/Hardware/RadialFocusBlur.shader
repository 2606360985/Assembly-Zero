// Screen-space macro lens: sharp elliptical centre, progressively blurred edges.
// Loaded through Resources so it is always included in player builds.
Shader "Hidden/AssemblyZero/RadialFocusBlur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "RadialFocus"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Strength;
            float _FocusRadius;
            float _Feather;
            float _Aspect;
            float2 _FocusCenter;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 d = (uv - _FocusCenter) * float2(_Aspect, 1);
                float mask = saturate((length(d) - _FocusRadius) / max(_Feather, 1e-4));
                mask = mask * mask * (3 - 2 * mask);
                half4 centre = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float radius = mask * _Strength;
                if (radius <= 1e-5)
                    return centre;
                half4 sum = centre;
                float weight = 1;
                float2 scale = float2(1 / _Aspect, 1) * radius;
                [unroll] for (int k = 0; k < 16; k++)
                {
                    float angle = k * 0.39269908 + (k & 1) * 0.19634954;
                    float ring = (k & 1) ? 0.55 : 1.0;
                    float2 offset = float2(cos(angle), sin(angle)) * ring * scale;
                    sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset);
                    weight += 1;
                }
                return sum / weight;
            }
            ENDHLSL
        }
    }
}
