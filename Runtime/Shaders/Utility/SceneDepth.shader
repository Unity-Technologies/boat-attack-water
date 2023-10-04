Shader "Unlit/SceneDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Zwrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcood : TEXCOORD0;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.texcood = input.texcoord;
                return output;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                // sample the texture
                float depth = SampleSceneDepth(input.texcood);
                depth = Linear01Depth(depth, _ZBufferParams);
                return float4(depth.xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
