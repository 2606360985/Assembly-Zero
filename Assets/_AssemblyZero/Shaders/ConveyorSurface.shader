Shader "AssemblyZero/ConveyorSurface"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.025, 0.035, 0.05, 1)
        _StripeColor("Moving Stripe", Color) = (0.08, 0.32, 0.38, 1)
        _FlowSpeed("Flow Speed", Float) = 1.2
        _Tiling("Stripe Tiling", Float) = 10
        _Direction("Flow Direction", Vector) = (1, 0, 0, 0)
        [Toggle] _Corner("Corner 90", Float) = 0
        _TurnSign("Turn Sign (+1 Left, -1 Right)", Float) = 1
        _SurfaceSize("Surface Size (Cells)", Float) = 0.9
        _BeltHalfWidth("Belt Half Width (Cells)", Float) = 0.39
        _Metallic("Metallic", Range(0,1)) = 0.15
        _Smoothness("Smoothness", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float fogFactor : TEXCOORD3; float2 cellPos : TEXCOORD4; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _StripeColor;
                float _FlowSpeed;
                float _Tiling;
                float4 _Direction;
                float _Corner;
                float _TurnSign;
                float _SurfaceSize;
                float _BeltHalfWidth;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                output.cellPos = input.positionOS.xz * _SurfaceSize;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float flowCoord;
                if (_Corner > 0.5)
                {
                    float side = _TurnSign >= 0 ? 1.0 : -1.0;
                    float2 v = input.cellPos - float2(-0.5, 0.5 * side);
                    clip(_BeltHalfWidth - abs(length(v) - 0.5));
                    flowCoord = saturate(atan2(v.x, -side * v.y) / 1.5707963);
                }
                else
                {
                    float2 direction = normalize(_Direction.xy + float2(0.0001, 0));
                    flowCoord = dot(input.uv, direction);
                }
                float flow = flowCoord * _Tiling - _Time.y * _FlowSpeed;
                float stripe = smoothstep(0.06, 0.16, abs(frac(flow) - 0.5));
                half3 albedo = lerp(_StripeColor.rgb, _BaseColor.rgb, stripe);
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 lit = albedo * (0.3h + ndl * mainLight.color * 0.7h);
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
