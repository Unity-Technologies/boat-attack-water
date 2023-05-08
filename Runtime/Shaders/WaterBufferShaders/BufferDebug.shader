Shader "Boat Attack/Water/WaterBuffer/BufferDebug"
{
    Properties
    {
        _Buffer1 ("Texture", 2D) = "white" {}
        _Buffer2 ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		//Blend One One
		LOD 100

        Pass
        {
            Name "WaterFX"
			Tags{"LightMode" = "WaterFX"}
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

            ///Buffer Outputs
            ///Buffer 1 layout
            /// R = Foam 0 - 1 : no foam, full foam
            /// G = NormalWS.x 0 - 1 : -x, +x
            /// B = NormalWS.z 0 - 1 : -z : z
            /// A = Displacement
            /// Buffer 2 layout
            /// R = Flow.r
            /// G = Flow.g
            /// B = WaterDepth
            /// A = ???
            struct Output
            {
                half4 buffer1 : SV_Target0;
                half4 buffer2 : SV_Target1;
            };

            TEXTURE2D(_Buffer1); SAMPLER(sampler_Buffer1);
            TEXTURE2D(_Buffer2); SAMPLER(sampler_Buffer2);

            Varyings vert (Attributes input)
            {
                Varyings output;
                const VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                return output;
            }

            Output frag (const Varyings input) : SV_Target
            {
                Output output;
                // sample the texture
                half4 col = SAMPLE_TEXTURE2D(_Buffer1, sampler_Buffer1, input.uv);

                half grad = input.uv.y;
                half fifty = round(input.uv.x);
                half single = input.uv.x * 16 - 1;

                half4 buffer1 = grad * (1-fifty);
                half4 buffer2 = grad * fifty;


                half b1 = 1-saturate(floor(abs(single)));
                half b2 = 1-saturate(floor(abs(single - 2)));
                half b3 = 1-saturate(floor(abs(single - 4)));
                half b4 = 1-saturate(floor(abs(single - 6)));
                half4 bufferAmask = half4(b1, b2, b3, b4);
                half4 bufferA = buffer1 * bufferAmask;
                output.buffer1 = bufferA + half4(0, 0.5, 0.5, 0.5) * (1-bufferAmask);
                half b5 = 1-saturate(floor(abs(single - 8)));
                half b6 = 1-saturate(floor(abs(single - 10)));
                half b7 = 1-saturate(floor(abs(single - 12)));
                half b8 = 1-saturate(floor(abs(single - 14)));
                output.buffer2 = buffer2 * half4(b5, b6, b7, b8);
                return output;
            }
            ENDHLSL
        }
    }
}
