Shader "Boat Attack/Water"
{
	Properties
	{
		_DitherPattern ("Dithering Pattern", 2D) = "bump" {}
		[Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline" = "UniversalPipeline" }
		ZWrite On

		Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency

		Pass
		{
			Name "WaterShading"
			Tags{"LightMode" = "UniversalForward"}

			HLSLPROGRAM
			#pragma prefer_hlslcc gles
			/////////////////SHADER FEATURES//////////////////
			#pragma shader_feature _REFLECTION_CUBEMAP _REFLECTION_PROBES _REFLECTION_PLANARREFLECTION
			#pragma multi_compile _ USE_STRUCTURED_BUFFER
			#pragma multi_compile _ _STATIC_SHADER
			#pragma multi_compile _ _BOATATTACK_WATER_DEBUG

			// -------------------------------------
            // Universal Pipeline keywords/
			//#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING

			//--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

			////////////////////INCLUDES//////////////////////
			#include "WaterCommon.hlsl"

			//non-tess
			#pragma vertex WaterVertex
			#pragma fragment WaterFragment

			ENDHLSL
		}
	}
	FallBack "Hidden/InternalErrorShader"
}
