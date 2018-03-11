Shader "* Unlit/Unlit"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_TintColor("染色",Color) = (1,1,1,1)
		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		_SelfIlluminateColor("Emission Color",Color) = (0,0,0)//用于闪白材质特效
		_ColorBleedInRender("色溢强度(烘焙时有效)",Range(0,1)) = 1//用于烘焙灯光图
		[Toggle(ZL_LIGHTMAP_ON)]_UseLightmap("灯光图",Float) = 0
		[Toggle(ZL_VERTEX_ANIMATION_ON)]_VertexAnimation("顶点动画",Float) = 0
		_TranslationDistance("移动距离(顶点色控制)",Vector) = (0,1,0,0)
		_TranslationOffset("偏移距离(顶点色控制)",Vector) = (0,0,0,0)
		_TranslationSpeed("移动速度(顶点色控制)",Float) = 10
		_TurbulentSpeed("扰动速度(顶点色控制)",Vector) = (0.5,0.5,0.5,0)
		_TurbulentRange( "扰动幅度(顶点色控制)", Vector) = (0.2,0.2,0.2,0)
		_SinWaveAmount("正弦波幅度(与顶点色无关)",Vector) = (0,0,0,0)
		_SinWaveLength("正弦波长(与顶点色无关)",Vector) = (0,0,0,0)
		_SinWaveSpeed("正弦波速度(与顶点色无关)",Vector) = (0,0,0,0)
		_SinWaveDirection("正弦波方向(与顶点色无关)",Vector) = (0,0,0,0)


		// Blending state
		[HideInInspector] _Mode ("__mode", Float) = 2.0
		[HideInInspector] _SrcBlend ("__src", Float) = 1.0
		[HideInInspector] _DstBlend ("__dst", Float) = 0.0
		[HideInInspector] _ZWrite ("__zw", Float) = 1.0
		[HideInInspector] _CullMode ("__cull", Float) = 2.0
		[HideInInspector] _FogMode ("__fog", Float) = 1.0
	}

	SubShader //LOD300：纹理 + 顶点动画 + 高度雾
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }

		LOD 300
		Blend [_SrcBlend] [_DstBlend]
		ZWrite [_ZWrite]
		Cull [_CullMode]

		Pass
		{
			Tags { "LightMode"="ForwardBase"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile _ ZL_BASE_FOG_ON ZL_ALTITUDE_FOG_ON
			#pragma multi_compile ZL_LIGHTMAP_OFF ZL_LIGHTMAP_ON
			#pragma multi_compile ZL_VERTEX_ANIMATION_OFF ZL_VERTEX_ANIMATION_ON
			#pragma multi_compile ZL_FOG_OFF ZL_BASE_FOG_ON ZL_ALTITUDE_FOG_ON ZL_MIPFOG_ON 
			
			//#pragma multi_compile _ _EMISSION




			#include "UnityCG.cginc"
			#include "../CGIncludes/ZL_CGInclude.cginc"

			struct v2f
			{
				float4 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float4 worldNormal : TEXCOORD4;
				SHADOW_COORDS(5)
				float4 worldPos : TEXCOORD6;
				#if defined(ZL_FOG_OFF)
				 #elif defined(ZL_BASE_FOG_ON)
				 	UNITY_FOG_COORDS(7)
				 #elif defined(ZL_ALTITUDE_FOG_ON) || defined(ZL_MIPFOG_ON)
					ALTITUDE_FOG_COORDS(7)
				 #endif
			};

			float4 _TintColor;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 unity_Lightmap_ST;
			float _Cutoff;
			float4 _Color;
			float4 _SelfIlluminateColor;

			v2f vert (appdata_full v)
			{
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f,o);

				#if defined(ZL_VERTEX_ANIMATION_ON)
					float4 mdlPos = AnimateVertexInWorldSpace(v.vertex, v.color,  _TranslationSpeed,  _TranslationDistance,  _TranslationOffset,  _TurbulentSpeed,  _TurbulentRange, _SinWaveAmount, _SinWaveLength, _SinWaveSpeed, _SinWaveDirection);
					o.pos = mul(UNITY_MATRIX_VP, mdlPos );
				#else
					o.pos = UnityObjectToClipPos(v.vertex);
				#endif

				o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex);

				#if defined(ZL_LIGHTMAP_ON)
					//o.uv.zw = TRANSFORM_TEX(v.texcoord1, unity_Lightmap);
					o.uv.zw = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				o.worldNormal.xyz = UnityObjectToWorldNormal(v.normal);
				TRANSFER_SHADOW(o);

				o.worldPos = ZL_WORLD_POS( v.vertex );

				 #if defined(ZL_FOG_OFF)
				 #elif defined(ZL_BASE_FOG_ON)
				 	UNITY_TRANSFER_FOG(o,o.pos);
				 #elif defined(ZL_ALTITUDE_FOG_ON) || defined(ZL_MIPFOG_ON)
					o.fogCoord = ZL_TRANSFER_FOG(o.pos.z, o.worldPos, ZL_AltitudeFogParams);
				 #endif


				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				col.rgb *= _TintColor.rgb * _Color.rgb;

				//Alpha
				float alpha = i.worldPos.w;
				#if defined(_ALPHATEST_ON)
					//alpha = col.a;
					clip (col.a - _Cutoff);
				#else
					#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
						alpha = col.a * _TintColor.a;
					#endif
				#endif

				//Emission
				col.rgb += _SelfIlluminateColor.rgb;
				
				//Lightmap
				#if defined(ZL_LIGHTMAP_ON)
					col.rgb *= DecodeLightmapRGBM(UNITY_SAMPLE_TEX2D(unity_Lightmap,i.uv.zw));
				#endif

				//Recieve Shadow
				i.worldNormal = normalize(i.worldNormal);
				half3 ambientLighting = ShadeSH9(float4(i.worldNormal.xyz,1));
				col.rgb = lerp(ambientLighting * col.rgb, col.rgb, SHADOW_ATTENUATION(i));

				//Fog
				#if defined(ZL_FOG_OFF)
				 #elif defined(ZL_BASE_FOG_ON)
				 	UNITY_APPLY_FOG(i.fogCoord, col);
				 #elif defined(ZL_ALTITUDE_FOG_ON) || defined(ZL_MIPFOG_ON)
					col.rgb = ZL_APPLY_FOG_COLOR( col.rgb, i.fogCoord, ZL_AltitudeFogParams);
				 #endif

				return half4(col.rgb, alpha);
			}
			ENDCG
		}


		//阴影Pass
		Pass {
			Name "Caster"
			Tags { "LightMode" = "ShadowCaster" }
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma skip_variants SHADOWS_SOFT
			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile ZL_VERTEX_ANIMATION_OFF ZL_VERTEX_ANIMATION_ON
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "../CGIncludes/ZL_CGInclude.cginc"


			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Cutoff;

			struct v2f 
			{ 
				V2F_SHADOW_CASTER;
				float2  uv : TEXCOORD1;
			};

			v2f vert( appdata_full v )
			{
				v2f o;

				#if defined(ZL_VERTEX_ANIMATION_ON)
					v.vertex = AnimateVertexInWorldSpace(v.vertex, v.color,  _TranslationSpeed,  _TranslationDistance,  _TranslationOffset,  _TurbulentSpeed,  _TurbulentRange, _SinWaveAmount, _SinWaveLength, _SinWaveSpeed, _SinWaveDirection);
					v.vertex = mul(unity_WorldToObject, v.vertex);
				#endif

				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			float4 frag( v2f i ) : SV_Target
			{
				#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
					fixed4 col = tex2D(_MainTex, i.uv);
					clip(col.a - _Cutoff);
				#endif
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}


		//用于烘焙色溢的MetaPass，用UsePass会导致客户端变紫，原因不明
		//此Pass不用放在最前面
		//选择物体时显示线框是因为在SubShader上设置了Tags{"LightMode"="ForwardBase"}，这个应该在Pass中设置
		Pass 
		{
			Name "Meta"
			Tags { "LightMode" = "Meta" }
			Cull Off

			CGPROGRAM
				#pragma vertex vert_meta
				#pragma fragment frag_meta2
				#include "UnityStandardMeta.cginc"
				float _ColorBleedInRender = 0;//需要外接Properties变量，否则不生效
				float4 frag_meta2 (v2f_meta i): SV_Target
				{
					FragmentCommonData data = UNITY_SETUP_BRDF_INPUT (i.uv);
					UnityMetaInput o;
					UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);
					fixed4 c = tex2D (_MainTex, i.uv);
					o.Albedo = c.rgb * _ColorBleedInRender;
					o.Emission = Emission(i.uv.xy) * _ColorBleedInRender;
					return UnityMetaFragment(o);
				}
			ENDCG
		}
	}

	SubShader //LOD100：纹理 + 普通雾
	{
		Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }

		LOD 100
		Blend [_SrcBlend] [_DstBlend]
		ZWrite [_ZWrite]
		Cull [_CullMode]

		Pass
		{
			Tags { "LightMode"="ForwardBase"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile _ ZL_BASE_FOG_ON ZL_ALTITUDE_FOG_ON
			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile ZL_LIGHTMAP_OFF ZL_LIGHTMAP_ON
			//#pragma multi_compile _ _EMISSION

			#include "UnityCG.cginc"
			#include "../CGIncludes/ZL_CGInclude.cginc"

			struct v2f
			{
				float4 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				#if defined(ZL_BASE_FOG_ON)
					UNITY_FOG_COORDS(7)
				#endif
			};

			float4 _TintColor;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 unity_Lightmap_ST;
			float _Cutoff;
			float4 _Color;
			float4 _SelfIlluminateColor;

			v2f vert (appdata_full v)
			{
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f,o);

				o.pos = UnityObjectToClipPos(v.vertex);

				o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex);

				#if defined(ZL_LIGHTMAP_ON)
					//o.uv.zw = TRANSFORM_TEX(v.texcoord1, unity_Lightmap);
					o.uv.zw = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				#if defined(ZL_BASE_FOG_ON)
					UNITY_TRANSFER_FOG(o,o.pos);
				#endif

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				col.rgb *= _TintColor.rgb * _Color.rgb;

				//Alpha
				fixed alpha = 1;
				#if defined(_ALPHATEST_ON)
					alpha = col.a;
					clip (alpha - _Cutoff);
				#else
					#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
						alpha = col.a * _TintColor.a;
					#endif
				#endif

				//Tint And Emission
				col.rgb += _SelfIlluminateColor.rgb;
				
				//Lightmap
				#if defined(ZL_LIGHTMAP_ON)
					col.rgb *= DecodeLightmapRGBM(UNITY_SAMPLE_TEX2D(unity_Lightmap,i.uv.zw));
				#endif

				#if defined(ZL_BASE_FOG_ON)
					UNITY_APPLY_FOG(i.fogCoord, col);
				#endif

				return fixed4(col.rgb, alpha);
			}
			ENDCG
		}
	}


	CustomEditor "UnlitShaderEditor"
}

