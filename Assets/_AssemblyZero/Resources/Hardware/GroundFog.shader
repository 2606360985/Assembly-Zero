Shader "AssemblyZero/Hardware/GroundFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Ground fog integration"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_GroundFogDensity); SAMPLER(sampler_GroundFogDensity);
            float4 _FogArea, _FogColor, _FogShadow;
            float _FogBottom, _FogTop, _FogExtinction, _FogClock, _FogEdge;
            float Hash3(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7))) * 43758.5453); }
            float Noise3(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash3(i),Hash3(i+float3(1,0,0)),f.x),lerp(Hash3(i+float3(0,1,0)),Hash3(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(Hash3(i+float3(0,0,1)),Hash3(i+float3(1,0,1)),f.x),lerp(Hash3(i+float3(0,1,1)),Hash3(i+1),f.x),f.y),f.z);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float depth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float nearZ=1,farZ=0;
                #else
                    float nearZ=UNITY_NEAR_CLIP_VALUE,farZ=1;
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 origin=ComputeWorldSpacePosition(uv,nearZ,UNITY_MATRIX_I_VP);
                float3 farPoint=ComputeWorldSpacePosition(uv,farZ,UNITY_MATRIX_I_VP);
                float3 direction=normalize(farPoint-origin);
                float3 opaque=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 boxMin=float3(_FogArea.x,_FogBottom,_FogArea.y);
                float3 boxMax=float3(_FogArea.xy+_FogArea.zw,0).xzy; boxMax.y=_FogTop;
                float3 safeDirection=lerp(-1.0,1.0,step(0,direction))*max(abs(direction),.00001);
                float3 a=(boxMin-origin)/safeDirection,b=(boxMax-origin)/safeDirection;
                float3 entry=min(a,b),exit=max(a,b);
                float start=max(0,max(entry.x,max(entry.y,entry.z)));
                float finish=min(dot(opaque-origin,direction),min(exit.x,min(exit.y,exit.z)));
                if (finish <= start) return scene;
                float stride=(finish-start)/24;
                float transmission=1; float3 fog=0;
                [loop] for(int n=0;n<24;n++)
                {
                    float3 p=origin+direction*(start+(n+.5)*stride);
                    float2 fieldUv=(p.xz-_FogArea.xy)/_FogArea.zw;
                    float d=SAMPLE_TEXTURE2D_LOD(_GroundFogDensity,sampler_GroundFogDensity,fieldUv,0).r;
                    float h=saturate((p.y-_FogBottom)/(_FogTop-_FogBottom));
                    float2 edge=min(p.xz-_FogArea.xy,_FogArea.xy+_FogArea.zw-p.xz);
                    d *= (1-smoothstep(.12,1,h))*smoothstep(0,_FogEdge,min(edge.x,edge.y));
                    d *= .7+.6*Noise3(p*float3(.7,1.4,.7)-float3(_FogClock*.08,0,_FogClock*.035));
                    float alpha=1-exp(-d*_FogExtinction*stride);
                    fog += lerp(_FogShadow.rgb,_FogColor.rgb,saturate(h+.2))*alpha*transmission;
                    transmission *= 1-alpha;
                }
                return half4(scene.rgb*transmission+fog,scene.a);
            }
            ENDHLSL
        }
    }
}
