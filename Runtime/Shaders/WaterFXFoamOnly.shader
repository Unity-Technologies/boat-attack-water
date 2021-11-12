Shader "Boat Attack/Water/Buffer/FoamOnly"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Intensity ("Intensity", Float) = 1.0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		Blend One One
		ColorMask R 0
		LOD 100

		Pass
		{
			Name "WaterFX"
			Tags{"LightMode" = "WaterFX"}
			Blend One One

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define _NORMALMAP 1
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				half4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				half4 color : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			half _Intensity;
			
			v2f vert (appdata v)
			{
				v2f o;
				half3 posWS = TransformObjectToWorld(v.vertex.xyz);
				o.vertex = TransformWorldToHClip(posWS);
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				half4 col = tex2D(_MainTex, i.uv) * i.color;
				return half4(col.rgb, 0) * _Intensity;
			}
			ENDHLSL
		}
	}
}
