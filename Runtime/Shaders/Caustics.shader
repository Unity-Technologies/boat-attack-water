Shader "Boat Attack/Water/Caustics"
{
    Properties
    {
        _Size("Size", Float) = 0.25
        [NoScaleOffset]_CausticMap("Caustics", 2D) = "white" {}
        _WaterLevel("WaterLevel", Float) = 0
        _BlendDistance("BlendDistance", Float) = 3

        //Color blends
        [HideInInspector] _SrcBlend("__src", Float) = 2.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
    }
    SubShader
    {
        ZWrite Off

        Pass
        {
            Blend [_SrcBlend] [_DstBlend], One Zero

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define WATER_HEIGHT _WaterBodyToWorld._24
            #define WATER_POSITION _WaterBodyToWorld._14_24_34
            
            #pragma multi_compile _ _DEBUG
            #pragma multi_compile _ _STATIC_SHADER

            #if UNITY_VERSION >= 202330
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma multi_compile _ _FORWARD_PLUS
            #endif

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 screenpos : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_CausticMap); SAMPLER(sampler_CausticMap);
            TEXTURE2D(_AbsorptionScatteringRamp); SAMPLER(sampler_AbsorptionScatteringRamp);

            half _Size;
            half _MaxDepth;
            half _BlendDistance;
            half4x4 _MainLightDir;
            half4x4 _WaterBodyToWorld;

            // World Posision reconstriction
            float3 ReconstructWorldPos(half2 screenPos, float depth)
            {
                float4x4 mat = UNITY_MATRIX_I_VP;
#if UNITY_REVERSED_Z
                mat._12_22_32_42 = -mat._12_22_32_42;              
#else
                depth = depth * 2 - 1;
#endif
                float4 raw = mul(mat, float4(screenPos * 2 - 1, depth, 1));
                float3 worldPos = raw.rgb / raw.a;
                return worldPos;
            }

            // Can be done per-vertex
            float2 CausticUVs(float2 rawUV, float2 offset)
            {
                float2 uv = rawUV * _Size;
                return uv + offset * 0.1;
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.screenpos = ComputeScreenPos(output.positionCS);
                
                return output;
            }
            
            real4 frag (Varyings input) : SV_Target
            {
                float4 screenPos = input.screenpos / input.screenpos.w;
                
                // Get depth
                real depth = SampleSceneDepth(screenPos.xy);
                
                // Get main light
                Light MainLight = GetMainLight();
                
                // Reconstruct Position of objects in depth map
                float4 WorldPos = float4(ReconstructWorldPos(screenPos.xy, depth).xyz, 1.0);
                WorldPos.xz += GetCameraPositionWS().xz;
                WorldPos.xyz -= WATER_POSITION;
                WorldPos = mul(WorldPos, _WaterBodyToWorld);
                
                // Get light direction and use it to rotate the world position
                float3 LightUVs = mul(WorldPos, _MainLightDir).xyz;

#if defined(_STATIC_SHADER)
	            float time = 0;
#else
	            float time = _Time.x;
#endif

                // Read wave texture for noise to offset cautics UVs
                float2 uv = WorldPos.xz * 0.025 + time * 0.25;
                float waveOffset = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv).w - 0.5;

                float2 causticUV = CausticUVs(LightUVs.xy, waveOffset);
                
                float LodLevel = abs(WorldPos.y - WATER_HEIGHT) * 4 / _BlendDistance;
                float4 A = SAMPLE_TEXTURE2D_LOD(_CausticMap, sampler_CausticMap, causticUV + time, LodLevel);
                float4 B = SAMPLE_TEXTURE2D_LOD(_CausticMap, sampler_CausticMap, causticUV * 2.0 - time, LodLevel);
                
                float CausticsDriver = (A.z * B.z) * 10 + A.z + B.z;
                
                // Mask caustics from above water and fade below
                half level = WATER_HEIGHT;
                half upperMask = saturate(-WorldPos.y + level);
                half lowerMask = 1 - saturate(LodLevel * 0.125);
                CausticsDriver *= min(upperMask, lowerMask);
                
                // Fake light dispersion
                half3 Caustics = CausticsDriver * half3(B.w, B.x, A.w) * MainLight.color * MainLight.distanceAttenuation;
                
#ifdef _DEBUG
                return real4(Caustics, 1.0);
#endif
                // Add 1 for blending level to work nicely
                return real4(Caustics + 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}
