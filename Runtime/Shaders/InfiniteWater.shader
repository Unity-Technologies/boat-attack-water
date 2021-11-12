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
			#pragma shader_feature _REFLECTION_CUBEMAP _REFLECTION_PROBES _REFLECTION_PLANARREFLECTION
			#pragma shader_feature _ _STATIC_SHADER
			#pragma multi_compile _ _BOATATTACK_WATER_DEBUG
            // -------------------------------------
            // Lightweight Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

			// make fog work
			#pragma multi_compile_fog

            ////////////////////INCLUDES//////////////////////
			#include "WaterCommon.hlsl"
			#include "InfiniteWater.hlsl"

			#pragma vertex InfiniteWaterVertex
			#pragma fragment InfiniteWaterFragment

		    //float4x4 _InvViewProjection;

			struct Output
			{
				half4 color : SV_Target;
				float depth : SV_Depth;
			};

            float3 UnprojectPoint(float3 p)
            {
                float4x4 mat = mul(unity_MatrixV, glstate_matrix_projection);
                float4 unprojectedPoint =  mul(mat, float4(p, 1));
                return unprojectedPoint.xyz / unprojectedPoint.w;
            }

			Varyings InfiniteWaterVertex(Attributes input)
			{
				Varyings output = (Varyings)0;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.uv.xy = input.texcoord;

				float3 cameraOffset = GetCameraPositionWS();
            	input.positionOS.xz *= _BoatAttack_Water_DistanceBlend; // scale range to blend distance
            	input.positionOS.y *= cameraOffset.y - _WaveHeight * 2; // scale height to camera
				input.positionOS.y -= cameraOffset.y - _WaveHeight * 2;
				//input.positionOS.xz = input.positionOS.y < 0 ? half2(0, 0) : input.positionOS.xz;
				//
				//input.positionOS = 1000;

				//cameraOffset.y *= 0.0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = vertexInput.positionCS;

				//float3 pos = output.positionCS;
				//pos.z = _ProjectionParams.y;
				//output.positionWS = UnprojectPoint(pos); // near position
				//pos.z = _ProjectionParams.z;
				//output.preWaveSP = UnprojectPoint(pos); // far postion

				output.positionWS = vertexInput.positionWS;
				output.screenPosition = ComputeScreenPos(vertexInput.positionCS);

				float3 viewPos = vertexInput.positionVS;
				output.viewDirectionWS.xyz = UNITY_MATRIX_IT_MV[2].xyz;
				output.viewDirectionWS.w = length(viewPos / viewPos.z);

				return output;
			}

			//half4 InfiniteWaterFragment(Varyings i) : SV_Target
			Output InfiniteWaterFragment(Varyings i)
			{
			    //float t = -i.positionWS.y / (i.preWaveSP.y - i.positionWS.y);

            	
			    //if(t > 0)
			    //    discard;

			    half4 screenUV = 0.0;
	            screenUV.xy  = i.screenPosition.xy / i.screenPosition.w; // screen UVs
	            screenUV.zw  = screenUV.xy; // screen UVs
                //half2 screenUV = i.screenPosition.xy / i.screenPosition.w; // screen UVs

                half4 waterBufferA = WaterBufferA(screenUV.xy);
                half4 waterBufferB = WaterBufferB(screenUV.xy);

				InfinitePlane plane = WorldPlane(i.screenPosition, i.viewDirectionWS);
				i.positionWS = plane.positionWS;
				float3 normal = half3(0.0, 1.0, 0.0);
                half3 viewDirectionWS = GetCameraPositionWS() - i.positionWS;
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
            	output.depth = plane.depth;// min(1-plane.depth, 1-WaterNearFade(i.positionWS));
            	return output;
                //outColor = half4(frac(i.positionWS), 1); //color;
				//outDepth = 1;// 1-plane.depth;
				//return half4(frac(plane.positionWS * 0.1), 1);
				//return color;
			}
			ENDHLSL
		}
	}
	FallBack "Hidden/InternalErrorShader"
}
