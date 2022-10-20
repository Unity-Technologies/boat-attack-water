#ifndef WATER_INPUT_INCLUDED
#define WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

half3 _AbsorptionColor;
half3 _ScatteringColor;
int _BoatAttack_Water_DebugPass;
half _BoatAttack_Water_DistanceBlend;
half _BoatAttack_Water_MicroWaveIntensity;
half _BoatAttack_water_FoamIntensity;
half _WaveHeight;
half _MaxDepth;
half _MaxWaveHeight;
half4 _VeraslWater_DepthCamParams;
float4x4 _InvViewProjection;
half3 _SSR_Settings;

#define SSR_STEP_SIZE _SSR_Settings.x
#define SSR_THICKNESS _SSR_Settings.y

// Screen Effects textures
SAMPLER(sampler_ScreenTextures_point_clamp);
#if defined(_REFLECTION_PLANARREFLECTION)
TEXTURE2D(_PlanarReflectionTexture);
#endif
//#elif defined(_REFLECTION_CUBEMAP)
TEXTURECUBE(_CubemapTexture);
SAMPLER(sampler_CubemapTexture);
//#endif
TEXTURE2D(_WaterBufferA);
TEXTURE2D(_WaterBufferB);
TEXTURE2D(_CameraDepthTexture);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_ScreenTextures_linear_clamp);

// Surface textures
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
TEXTURE2D(_DitherPattern); SAMPLER(sampler_DitherPattern); half4 _DitherPattern_TexelSize;

// Data Textures
TEXTURE2D(_BoatAttack_RampTexture); SAMPLER(sampler_BoatAttack_Linear_Clamp_RampTexture);

///////////////////////////////////////////////////////////////////////////////
//                  				Structs		                             //
///////////////////////////////////////////////////////////////////////////////

struct Attributes // vert struct
{
    float4 positionOS 			    : POSITION;		// vertex positions
	float2	texcoord 				: TEXCOORD0;	// local UVs
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings // fragment struct
{
	float4	uv 						: TEXCOORD0;	// Geometric UVs stored in xy, and world(pre-waves) in zw
	float3	positionWS				: TEXCOORD1;	// world position of the vertices
	float3 	normalWS 				: NORMAL;		// vert normals
	float4 	viewDirectionWS 		: TEXCOORD2;	// view direction
	float3	preWaveSP 				: TEXCOORD3;	// screen position of the verticies before wave distortion
	half2 	fogFactorNoise          : TEXCOORD4;	// x: fogFactor, y: noise
	float4	additionalData			: TEXCOORD5;	// x = distance surface to floor from view, y = distance to surface, z = normalized wave height, w = horizontal movement
	float4	screenPosition			: TEXCOORD6;	// screen position after the waves

	float4	positionCS				: SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

struct VaryingsInfinite // infinite water Varyings
{
	float3	nearPosition			: TEXCOORD0;	// near position of the vertices
    float3	farPosition				: TEXCOORD1;	// far position of the vertices
	float3	positionWS				: TEXCOORD2;	// world position of the vertices
	float4 	viewDirectionWS 		: TEXCOORD3;	// view direction
    half4	screenPosition			: TEXCOORD4;	// screen position after the waves
    float4  positionCS              : SV_POSITION;
};

struct WaterSurfaceData
{
    half3   absorption;
	half3   scattering;
    half3    foam;
    half    foamMask;
};

struct WaterInputData
{
    float3 positionWS;
    float3 normalWS;
    float3 viewDirectionWS;
    float2 reflectionUV;
    float2 refractionUV;
    float4 detailUV;
    float4 shadowCoord;
    half4 waterBufferA;
    half4 waterBufferB;
    half fogCoord;
    float depth;
    half3 GI;
	half3 screenNoise;
};

struct WaterLighting
{
    half3 driectLighting;
    half3 ambientLighting;
    half3 sss;
    half3 shadow;
};

#endif // WATER_INPUT_INCLUDED
