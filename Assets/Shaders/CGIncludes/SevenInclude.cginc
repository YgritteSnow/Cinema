// Upgrade NOTE: commented out 'float4 unity_LightmapST', a built-in variable
// Upgrade NOTE: commented out 'sampler2D unity_Lightmap', a built-in variable
// Upgrade NOTE: commented out 'sampler2D unity_LightmapInd', a built-in variable
// Upgrade NOTE: replaced tex2D unity_Lightmap with UNITY_SAMPLE_TEX2D


#ifndef SEVEN_CG_INCLUDED
#define SEVEN_CG_INCLUDED

#include "UnityCG.cginc"
#include "AutoLight.cginc"

// sampler2D unity_Lightmap;
// float4 unity_LightmapST;
// sampler2D unity_LightmapInd;

float sevenLightMapMultiply;

inline half3 SevenLightmap (float2 uv)
{

	fixed4 lightmap = UNITY_SAMPLE_TEX2D (unity_Lightmap, uv);
	half3 lm  = 8 *sevenLightMapMultiply* lightmap.a *  lightmap.rgb ;

	return  lm;
}



#endif
