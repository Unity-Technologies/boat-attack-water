Shader "Boat Attack/Water/WaterBuffer/WaterDepthOnly"
{
    Properties
    {
        _Depth ("DepthMap", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
		ZWrite Off
        ZTest Always
		//Blend One One
        LOD 100

        Pass
        {
            Name "WaterFX"
			Tags{"LightMode" = "WaterFX"}

            ColorMask B 1
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            struct Output
            {
                half4 buffer2 : SV_Target1;
            };

            TEXTURE2D(_Depth); SAMPLER(sampler_Depth);

            Varyings vert (Attributes input)
            {
                Varyings output;
                const VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                return output;
            }

            Output frag (const Varyings input)
            {
                Output output;
                // sample the texture
                const half4 d = SAMPLE_TEXTURE2D(_Depth, sampler_Depth, input.uv);
                output.buffer2 = d.rrrr;

                return output;
            }
            ENDHLSL
        }
    }
}
