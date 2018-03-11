
#ifndef WOK_CG_INCLUDED
#define WOK_CG_INCLUDED

#include "UnityCG.cginc"

float wokLightMapMultiply;
float wokLightMapContrast;
float4 wokAltitudeFogParams;
float4 wokAltitudeFogColor;
float ZL_ALTITUDE;

sampler2D zl_lut_tex;
sampler2D zl_lut_texB;
float zl_lut_blend;
float4 zl_lut_scale_offset;

sampler2D _InstanceLightMap;

//sampler2D _FogRamp;

inline half3 WokLightmap (float2 uv)
{
#if defined(UNITY_SUPPORT_INSTANCING) && defined(INSTANCING_ON)
	half4 lightmap = tex2D(_InstanceLightMap, uv);
#else
	half4 lightmap = UNITY_SAMPLE_TEX2D (unity_Lightmap, uv);
#endif
	
	half d = saturate(  (lightmap.a - 0.22) * (wokLightMapContrast +1) + 0.22);
	half3 lm  =  5 * wokLightMapMultiply * d *  lightmap.rgb ;

	return  lm;
}


// inline float wokPow(float x, float n){
// 	// Sherical Gaussian approximation: pow(x,n) ~= exp((n+0.775)*(x-1))
// 	return exp((n+0.775) * (x-1));
// }

	#define WOK_FOG_COORDS(idx) UNITY_FOG_COORDS_PACKED(idx, float4)

	float4 WOK_TRANSFER_ALTITUDE_FOG(float z, float3 worldPos, float4 fogParams)
	{

		#if WOK_ALTITUDE_FOG_ON

		float3 camPos = _WorldSpaceCameraPos;
		
		float f = sign(fogParams.w);
		float3 _v = worldPos - camPos;

		float p = worldPos.y - fogParams.x;
		float c = camPos.y - fogParams.x;
		float k = 1- step( 0, (f * c));
		float fDotV =  f * _v.y ;
		float fDotP =  f * p;
		float fDotC =  f * c;
		
		float c1 = k * (fDotP + fDotC);
		float c2 = (1 - 2* k) * fDotP;
		return  float4(z, c1, c2, fDotV);
		#else
			return  float4(z, 0, 0 , 0);
		#endif 
	}


	float3 WOK_SKY_BOX_FOG_COLOR(half3 col, float3 worldPos, float4 fogCoord, float4 fogParams)
	{
		#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
			col.rgb = (unity_FogColor).rgb;
		#endif

		#if WOK_ALTITUDE_FOG_ON 
			half inFog = step(  (worldPos.y - fogParams.x) * fogParams.w,  0);
			col.rgb = lerp((col).rgb,  (wokAltitudeFogColor).rgb,  inFog );
			return col;
		#else
			return col;
		#endif
	}

	float3 WOK_APPLY_FOG_COLOR( float3 col, float4 fogCoord, float4 fogParams)
	{
		#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
			UNITY_CALC_FOG_FACTOR(fogCoord.x);
			col.rgb = lerp((unity_FogColor).rgb, (col).rgb, saturate( unityFogFactor) );
		#endif

		#if WOK_ALTITUDE_FOG_ON 
			
			float c1 = fogCoord.y;
			float c2 = fogCoord.z;
			float fDotV = fogCoord.w;

			float g = min( c2, 0);
			g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

			float f = saturate ( exp2(g) );

			col.rgb = lerp((wokAltitudeFogColor).rgb, (col).rgb,  f );
			return col;
		#else
			return col;
		#endif
	}

	float WOK_FOG_PRECENT( float3 col, float4 fogCoord, float4 fogParams)
	{
		float result = 1;
		#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
			UNITY_CALC_FOG_FACTOR(fogCoord.x);
			result = saturate( unityFogFactor);
		#endif

		#if WOK_ALTITUDE_FOG_ON 
			
			float c1 = fogCoord.y;
			float c2 = fogCoord.z;
			float fDotV = fogCoord.w;

			float g = min( c2, 0);
			g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

			float f = saturate ( exp2(g) );
			result *= f;
		#endif

		return result;
	}


	float WOK_CALC_FOG_FACTOR( float4 fogCoord, float4 fogParams)
	{
		#if WOK_ALTITUDE_FOG_ON 
		// half space fog
		worldPos.y -= fogParams.x;

		float c1 = fogCoord.y;
		float c2 = fogCoord.z;
		float fDotV = fogCoord.w;

		float g = min( c2, 0);
		g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

		return saturate( exp2(g) );
	#else 
		return 1;
	#endif
	}



	half3 apply_lut(sampler2D tex, half3 uvw, half3 scaleOffset)
	{
		uvw.z *= scaleOffset.z;
		half shift = floor(uvw.z);
		uvw.xy = uvw.xy * scaleOffset.z * scaleOffset.xy + scaleOffset.xy * 0.5;
		uvw.x += shift * scaleOffset.y;
		uvw.y = 1 - uvw.y;
		half f = uvw.z - shift;

		uvw.xyz = lerp(tex2D(tex, uvw.xy).rgb, tex2D(tex, uvw.xy + half2(scaleOffset.y, 0)).rgb, uvw.z - shift);
		//uvw.xyz = tex2D(tex, uvw.xy).rgb;
		return uvw;
	}

	half3 ApplyLUT( half3 srcColor)
	{
		half3 cc = apply_lut(zl_lut_tex, saturate(srcColor), zl_lut_scale_offset.xyz);
		#if ZL_PS_LUT_BLEND
			half3 ccB = apply_lut(zl_lut_texB, saturate(srcColor), zl_lut_scale_offset.xyz);
			cc = lerp(cc, ccB, zl_lut_blend);
			return lerp( srcColor, cc, zl_lut_scale_offset.w );
		#else 
			return lerp( srcColor, cc, zl_lut_scale_offset.w );
		#endif
	}


#endif
