Shader "* Character/Hair(Ansiotropic)" 
{
	Properties
	{
		_HairTint1("染色(顶点色 R 通道白染黑不染)",Color) = (0.32, 0.15, 0.15, 1)
		_MainTex ("Base Color", 2D) = "white" {}
		_MaskTex ("Mask(R:高光 G：高光散射 B：Alpha)", 2D) = "white" {}
		_AlphaCutout("Alpha Cut Out",Range(0,1)) = 0.1
		[Space][Space][Space]_PrimaryShift("主高光偏移", float) = 0
		_SecondaryShift("次高光偏移", float) = -1
		[Space][Space][Space]_SpecularColor1("主高光颜色", Color) = (1,1,1)
		_SpecularColor2("次高光颜色", Color) = (1,1,1)
		[Space][Space][Space]_SpecularGloss1("主高光大小", float) = 64
		_SpecularGloss2("次高光大小", float) = 64
		[Space][Space][Space]_SpecularLevel1("主高光强度", float) = 1.5
		_SpecularLevel2("次高光强度", float) = 1.5
		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
		[HideInInspector]_SelfIlluminateColor("Emission Color（用于闪白特效）",Color) = (0,0,0)
		[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0
		[Space][Space][Space]_animParams("扰动(xyz控制扰动，w控制强度,顶点色 G 通道黑动白不动))",Vector) = (1.975, 0.793, 0.375, 0.193)
		[Space][Space][Space]_Wind("偏移(xyz控制位移距离，w控制强度,顶点色 G 通道黑动白不动)",Vector) = (0,0,0,0.1)
	}


	CGINCLUDE
	#include "UnityCG.cginc"
	#include "Lighting.cginc"
	#include "../CGIncludes/ZL_CGInclude.cginc"
	#include "TerrainEngine.cginc"

	float _PrimaryShift;
	float _SecondaryShift;
	float4 _SpecularColor1;
	float4 _SpecularColor2;
	float _SpecularGloss1;
	float _SpecularLevel1;
	float _SpecularGloss2;
	float _SpecularLevel2;
	sampler2D _MaskTex;
	float4 _HairTint1;
	sampler2D _MainTex;
	float4 _MainTex_ST;
	float4 _SelfIlluminateColor;
	float _SelfIlluminated;
	float4 _Color;
	float _AlphaCutout;
	float4 _animParams;


	half3 ShiftTangent ( half3 T, half3 N, float shift)
	{
		half3 shiftedT = T + shift * N;
		return normalize( shiftedT);
	}
			
	float StrandSpecular ( half3 T, half3 V, half3 L, float exponent, float specularLevel)
	{
		half3 H = normalize ( L + V );
		float dotTH = dot ( T, H );
		float sinTH = sqrt ( 1.0 - dotTH * dotTH);
		float dirAtten = smoothstep( -1.0, 0.0, dotTH );
		return dirAtten * pow(sinTH, exponent) * specularLevel;
	}
	
	ENDCG


	SubShader
	{
		Tags { "Queue"="Geometry"  "IgnoreProjector"="False" "RenderType"="TransparentCutout"}
		Cull Off
		
		//Alpha Test
		Pass
		{
			Tags { "LightMode"="ForwardBase"} 
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			#pragma multi_compile_fog

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				WORLD_MATRIX(1,2,3)
				float3 worldSpaceLightDir : TEXCOORD4;
				float3 worldSpaceViewDir : TEXCOORD5;
				SHADOW_COORDS(6)
				//UNITY_FOG_COORDS(7)
				float2 depthAndFesnel : TEXCOORD7;//x:depthInAlpha,y:fesnelBlink
			};

			
			v2f vert (appdata_full v)
			{
				v2f o;
				float4 newPos = AnimateVertex(v.vertex, v.normal, _animParams);
				v.vertex = lerp(v.vertex, newPos,1 - v.color.g);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

				CALCULATE_WORLD_MATRIX
				o.worldSpaceLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
				o.worldSpaceViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
				TRANSFER_SHADOW(o);
				//UNITY_TRANSFER_FOG(o,o.pos);

				o.depthAndFesnel.x = ZL_DEPTH_IN_ALPHA(v.vertex);
				float3 localView = normalize(ObjSpaceViewDir(v.vertex));
				o.depthAndFesnel.y = (1 - saturate(dot(normalize( v.normal), localView))) * 2;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				//unpack matrix
				fixed3 tangent = fixed3(i.worldMatrixRow0.x,i.worldMatrixRow1.x,i.worldMatrixRow2.x);;
				fixed3 binormal = fixed3(i.worldMatrixRow0.y,i.worldMatrixRow1.y,i.worldMatrixRow2.y);
				fixed3 normal = fixed3(i.worldMatrixRow0.z,i.worldMatrixRow1.z,i.worldMatrixRow2.z);
				fixed3 ambientLighting = ShadeSH9(fixed4(normalize(normal),1));

				//SamplerState
				fixed4 baseTexture = tex2D(_MainTex, i.uv);
				fixed4 maskTexture = tex2D(_MaskTex, i.uv);

				// shift tangents
				float shiftTex = maskTexture.g - 0.5;
				//如果贴图和模型UV是横向画的发丝，应该用Tangent，现在是纵向的，所以用binormal
				half3 t1 = ShiftTangent (binormal, normal, _PrimaryShift + shiftTex);
				half3 t2 = ShiftTangent (binormal, normal, _SecondaryShift + shiftTex);

				//diffuse lighting
				float3 diffuse = saturate (dot(normal, i.worldSpaceLightDir)) * 0.5 + 0.5;
				diffuse *= _HairTint1 * 2;
			
				//specular Lighting
				float3 specular = _SpecularColor1 * maskTexture.r * StrandSpecular(t1, i.worldSpaceViewDir, i.worldSpaceLightDir, _SpecularGloss1, _SpecularLevel1);
				specular += _SpecularColor2 * maskTexture.r * StrandSpecular (t2, i.worldSpaceViewDir, i.worldSpaceLightDir, _SpecularGloss2, _SpecularLevel2) ;
			
				//final Color
				fixed4 col;
				col.rgb = (diffuse + specular) * baseTexture.rgb * _LightColor0.rgb;
				col.a = i.depthAndFesnel.x;
				clip ( maskTexture.b - _AlphaCutout);

				//fixed4 col = HairLighting (binormal, normal, i.worldSpaceLightDir, i.worldSpaceViewDir, i.uv);

				//Shadow and Fog
				col.rgb = lerp(ambientLighting * col.rgb, col.rgb, SHADOW_ATTENUATION(i)) * _Color.rgb + _SelfIlluminated * i.depthAndFesnel.y * _SelfIlluminateColor;
				//UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}
			ENDCG
		}
		
		//Alpha Blend
		Pass
		{
			Tags { "LightMode"="ForwardBase"}
			Blend SrcAlpha OneMinusSrcAlpha, Zero One
			Cull Off ZWrite off ZTest Less 

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			#pragma multi_compile_fog

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				WORLD_MATRIX(1,2,3)
				float3 worldSpaceLightDir : TEXCOORD4;
				float3 worldSpaceViewDir : TEXCOORD5;
				SHADOW_COORDS(6)
				UNITY_FOG_COORDS(7)
				float2 depthAndFesnel : TEXCOORD8;//x:depthAndFesnel,y:fesnelBlink
			};

			
			v2f vert (appdata_full v)
			{
				v2f o;
				float4 newPos = AnimateVertex(v.vertex, v.normal, _animParams);
				v.vertex = lerp(v.vertex, newPos,1 - v.color.g);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

				CALCULATE_WORLD_MATRIX
				o.worldSpaceLightDir = normalize(UnityWorldSpaceLightDir(worldPos));
				o.worldSpaceViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
				TRANSFER_SHADOW(o);
				UNITY_TRANSFER_FOG(o,o.pos);

				o.depthAndFesnel.x = ZL_DEPTH_IN_ALPHA(v.vertex);
				float3 localView = normalize(ObjSpaceViewDir(v.vertex));
				o.depthAndFesnel.y = (1 - saturate(dot(normalize( v.normal), localView))) * 2;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				//unpack matrix
				fixed3 tangent = fixed3(i.worldMatrixRow0.x,i.worldMatrixRow1.x,i.worldMatrixRow2.x);;
				fixed3 binormal = fixed3(i.worldMatrixRow0.y,i.worldMatrixRow1.y,i.worldMatrixRow2.y);
				fixed3 normal = fixed3(i.worldMatrixRow0.z,i.worldMatrixRow1.z,i.worldMatrixRow2.z);
				fixed3 ambientLighting = ShadeSH9(fixed4(normalize(normal),1));

				//SamplerState
				fixed4 baseTexture = tex2D(_MainTex, i.uv);
				fixed4 maskTexture = tex2D(_MaskTex, i.uv);

				// shift tangents
				float shiftTex = maskTexture.g - 0.5;
				//如果贴图和模型UV是横向画的发丝，应该用Tangent，现在是纵向的，所以用binormal
				half3 t1 = ShiftTangent (binormal, normal, _PrimaryShift + shiftTex);
				half3 t2 = ShiftTangent (binormal, normal, _SecondaryShift + shiftTex);

				//diffuse lighting
				float3 diffuse = saturate (dot(normal, i.worldSpaceLightDir)) * 0.5 + 0.5;
				diffuse *= _HairTint1 * 2;
			
				//specular Lighting
				float3 specular = _SpecularColor1 * maskTexture.r * StrandSpecular(t1, i.worldSpaceViewDir, i.worldSpaceLightDir, _SpecularGloss1, _SpecularLevel1);
				specular += _SpecularColor2 * maskTexture.r * StrandSpecular (t2, i.worldSpaceViewDir, i.worldSpaceLightDir, _SpecularGloss2, _SpecularLevel2) ;
			
				//final Color
				fixed4 col;
				col.rgb = (diffuse + specular) * baseTexture.rgb * _LightColor0.rgb;

				//fixed4 col = HairLighting (binormal, normal, i.worldSpaceLightDir, i.worldSpaceViewDir, i.uv);

				//Shadow and Fog
				col.rgb = lerp(ambientLighting * col.rgb, col.rgb, SHADOW_ATTENUATION(i)) * _Color.rgb + _SelfIlluminated * i.depthAndFesnel.y * _SelfIlluminateColor;
				UNITY_APPLY_FOG(i.fogCoord, col);

				col.a = saturate( maskTexture.b / _AlphaCutout );
				//col.a = baseTexture.b;
				return col;
			}
			ENDCG
		}

		//ShadowCaster
		Pass
		{
			Name "Caster"
			Tags { "LightMode" = "ShadowCaster" }
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			struct v2f 
			{ 
				V2F_SHADOW_CASTER;
				float2  uv : TEXCOORD1;
			};

			v2f vert( appdata_base v )
			{
				v2f o;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			float4 frag( v2f i ) : SV_Target
			{
				fixed4 texcol = tex2D( _MaskTex, i.uv );
				clip( texcol.b - _AlphaCutout );
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
		
	}

	FallBack "Diffuse"

}
               