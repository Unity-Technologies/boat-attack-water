Shader "Boat Attack/Water/InfiniteWater"
{
	Properties
	{
		_Size ("size", float) = 3.0
		_DitherPattern ("Dithering Pattern", 2D) = "bump" {}
		[Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
		[KeywordEnum(Off, SSS, Refraction, Reflection, Normal, Fresnel, WaterEffects, Foam, WaterDepth)] _Debug ("Debug mode", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent-101" "RenderPipeline" = "UniversalPipeline" }
		ZWrite off
		Cull off

		Pass
		{
			Name "InfiniteWaterShading"
			Tags{"LightMode" = "UniversalForward"}

			HLSLPROGRAM
			#pragma prefer_hlslcc gles
			/////////////////SHADER FEATURES//////////////////
			#pragma multi_compile_fragment _REFLECTION_CUBEMAP _REFLECTION_PROBES _REFLECTION_PLANARREFLECTION
			#pragma multi_compile _ _STATIC_SHADER
			#pragma multi_compile_fragment _ _BOATATTACK_WATER_DEBUG
            // -------------------------------------
            // Lightweight Pipeline keywords
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

			// make fog work
			#pragma multi_compile_fog

            ////////////////////INCLUDES//////////////////////
			#include "WaterCommon.hlsl"
			#include "InfiniteWater.hlsl"

			#pragma vertex InfiniteWaterVertex
			#pragma fragment InfiniteWaterFragment
			
			struct Output
			{
				half4 color : SV_Target;
				float depth : SV_Depth;
			};
			
			Varyings InfiniteWaterVertex(Attributes input)
			{
				Varyings output = (Varyings)0;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.uv.xy = input.texcoord;

				float3 cameraOffset = GetCameraPositionWS();
            	input.positionOS.xz *= _BoatAttack_Water_DistanceBlend; // scale range to blend distance
            	input.positionOS.y *= cameraOffset.y - _WaveHeight; // scale height to camera
				input.positionOS.y -= cameraOffset.y - _WaveHeight;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.screenPosition = ComputeScreenPos(vertexInput.positionCS);

				float3 viewPos = vertexInput.positionVS;
				output.viewDirectionWS.xyz = UNITY_MATRIX_IT_MV[2].xyz;
				output.viewDirectionWS.w = length(viewPos / viewPos.z);
            	
				return output;
			}

			float _Size;

			Output InfiniteWaterFragment(Varyings i)
			{
			    half4 screenUV = 0.0;
	            screenUV.xy  = i.screenPosition.xy / i.screenPosition.w; // screen UVs
	            screenUV.zw  = screenUV.xy; // screen UVs
                //half2 screenUV = i.screenPosition.xy / i.screenPosition.w; // screen UVs

                half4 waterBufferA = WaterBufferA(screenUV.xy);
                half4 waterBufferB = WaterBufferB(screenUV.xy);

				InfinitePlane plane = WorldPlane(i.viewDirectionWS, i.positionWS);
				i.positionWS = plane.positionWS;
                half3 viewDirectionWS = GetCameraPositionWS().xyz - i.positionWS.xyz;
				float3 viewPos = TransformWorldToView(i.positionWS);
				float4 additionalData = float4(length(viewPos / viewPos.z), length(viewDirectionWS), waterBufferA.w, 0);

				i.fogFactorNoise.x = ComputeFogFactor(TransformWorldToHClip(plane.positionWS).z);
            	
                i.normalWS = half3(0.0, 1.0, 0.0);
                i.viewDirectionWS = normalize(GetCameraPositionWS() - i.positionWS).xyzz;
                i.additionalData = additionalData;
                i.uv = DetailUVs(i.positionWS * (1 / _Size), 1);
            	i.preWaveSP = screenUV.xyz;

                WaterInputData inputData;
                InitializeInputData(i, inputData, screenUV.xy);

                WaterSurfaceData surfaceData;
                InitializeSurfaceData(inputData, surfaceData, additionalData);

                half4 color;
                color.a = 1;
                color.rgb = WaterShading(inputData, surfaceData, additionalData, screenUV.xy);

            	Output output;
            	output.color = color;
            	output.depth = plane.depth;
            	return output;
			}
			ENDHLSL
		}
	}
	FallBack "Hidden/InternalErrorShader"
}
