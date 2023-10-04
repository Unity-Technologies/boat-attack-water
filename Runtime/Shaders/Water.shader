Shader "Boat Attack/Water"
{
    Properties
    {
        [Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On
        Cull off

        Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency

        Pass
        {
            Name "WaterShading"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            /////////////////SHADER FEATURES//////////////////
            #pragma multi_compile_fragment _REFLECTION_CUBEMAP _REFLECTION_PROBE _REFLECTION_PLANARREFLECTION _REFLECTION_SSR
            #pragma multi_compile_fragment _ _DISPERSION
            #pragma multi_compile _ USE_STRUCTURED_BUFFER
            #pragma shader_feature_local _STATIC_SHADER
            #pragma multi_compile _ BOAT_ATTACK_WATER_DEBUG_DISPLAY

            #pragma multi_compile_fragment _ _SSR_SAMPLES_LOW _SSR_SAMPLES_MEDIUM _SSR_SAMPLES_HIGH 
            #pragma multi_compile_fragment _ _SHADOW_SAMPLES_LOW _SHADOW_SAMPLES_MEDIUM _SHADOW_SAMPLES_HIGH 

            // Universal Pipeline keywords/
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            
            #if UNITY_VERSION >= 202330
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma multi_compile _ _FORWARD_PLUS
            #endif

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
