// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// ---------
// | B | A |
// ---------
// | R | G |
// ---------
Shader "* Character/Equip NormalMap.." {
		Properties
		{
			//OneMask TwoMask  transparentCullout_On
			_MainTex ("Base (RGB)", 2D) = "grey" {}
			_MaskMap("Mask (RGB)", 2D) = "black" {}
			[ShowWhenHasKeyword(CustomFace_On)] _FaceTex("Face (RGB)", 2D) = "grey" {}

			[ShowWhenHasKeyword(TwoMask)] _DetailMap("Detail (RGB)", 2D) = "grey" {}
			[ShowWhenHasKeyword(TwoMask)] _MaskBMap ("MaskB (R:alpha G: skin B:tint)", 2D) = "blue" {}
			_EnvMap ("EnvCap (RGB)", 2D) = "white" {}
			[ShowWhenHasKeywordDrawer(TwoMask, transparentCullout_On)] _AlphaRef ("AlphaTest Ref", Range(0,1)) = 0.5
			[ShowWhenHasKeyword(TwoMask)] _Tint1 ("装备换色 ", Color) = (0.5,0.5,0.5,1)
			[ShowWhenHasKeyword(TwoMask)] _Tint2 ("装备换色2 ", Color) = (0.5,0.5,0.5,1)
			[ShowWhenHasKeyword(TwoMask)] _TintHair ("发色", Color) = (0.5,0.5,0.5,1)
			[ShowWhenHasKeyword(TwoMask)] _TintSkin ("肤色", Color) =  (0.5,0.5,0.5,1)

			[ShowWhenHasKeyword(TwoMask)] _RampMap("RampMap", 2D) = "grey" {}
			_FlowLightMap ("Flow Light (RGB)", 2D) = "black" {}
			_FlowColor ("流光颜色", Color) =  (1,0.6,0.2,1)
			_FlowMultiplier("Flow Multiply", float) = 5

			
			_FlowThreshold("NoiseThreshold", float) = 0.75
			// _SHLightingScale ("LightProbe影响系数", float) = 0.6
			_MatCapScale ("MatCap影响系数", float) = 1
			// _SHScale ("SH影响系数", float) = 0.6
			[HideInInspector]_SelfIlluminateColor("Emission Color", Color) = (0,0,0)
			[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0


			_Scroll2X ("Scroll2X", float) = 1
			_Scroll2Y ("Scroll2Y", float) = 1
			[ShowWhenHasKeyword(VERTEX_ROTATE_ON)] _RotateParameters("Rotate Center(rgb)  y轴/z轴(a)", Vector) = (0,0,0,1)
			[ShowWhenHasKeyword(VERTEX_ROTATE_ON)] _VerticleAmount("verticleAmount", Range(0,0.5)) = 0
			[ShowWhenHasKeyword(VERTEX_ROTATE_ON)] _RotateSpeed("Rotate Speed", float) = 0

			[Toggle(MATCAP_ACCURATE)] _MatCapAccurate ("Accurate Calculation", Int) = 0
			_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
		}


		
		CGINCLUDE
			#include "UnityCG.cginc"
			#include "../CGIncludes/ZL_CGInclude.cginc"


			float4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _MaskMap;
			sampler2D _FaceTex;
			float4 _FaceTex_ST;

			sampler2D _EnvMap;
			sampler2D _RampMap;
			sampler2D _DetailMap;
			float4 _DetailMap_ST;

			sampler2D _FlowLightMap;
			float4 _FlowLightMap_ST;
			float4 _FlowColor;
			float _FlowThreshold;
			float _FlowMultiplier;

			float _MatCapScale;

			half _Scroll2X;
			half _Scroll2Y;
			#ifdef VERTEX_ROTATE_ON
			float4 _RotateParameters;
			float _RotateSpeed;
			float _VerticleAmount;
			#endif
			// float _SHScale;
			// float _SHLightingScale;
			half4 _SelfIlluminateColor;
			float _SelfIlluminated;


			#ifdef TwoMask
				sampler2D _MaskBMap;
				fixed4 _Tint1;
				fixed4 _Tint2;
				fixed4 _TintHair;
				fixed4 _TintSkin;

				#ifdef transparentCullout_On
					half _AlphaRef;
				#endif
			#endif		
			
			struct appdata {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 tangent : TANGENT;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 pos	: SV_POSITION;
				float4 uv : TEXCOORD0;

					float3 c0 : TEXCOORD1;
					float3 c1 : TEXCOORD2;

					half3 lightDir : TEXCOORD3;

				float4 color : COLOR;
				float fesnelBlink : TEXCOORD4;

				/*
				float4 color : COLOR;
				
				float3 normal : TEXCOORD1;
				half4 capCoord : TEXCOORD2;
				// fixed3 vlight : TEXCOORD2;
				
				half3 lightDir : TEXCOORD4;
				fixed3 ambient : TEXCOORD5;
				*/
			};

			#define TWO_PI	6.28318530718f  

			half4 Rotate(float4 vertex, float4 offsets, half speed, half verticle)
		    {

				half sina, cosa;
				
        		sincos(speed * _Time.y % TWO_PI, sina, cosa);
        		
        		half4 vert = vertex;
				half b = offsets.w;
				float oneMinusB = 1-b;
				float3x3 m1 = float3x3(cosa * oneMinusB + b, - sina * oneMinusB , 0
									, sina * oneMinusB, cosa * oneMinusB + b * cosa, -b * sina
									, 0, b* sina, b * cosa + oneMinusB);
				vert.xyz = mul(m1, vertex.xyz - offsets.xyz);
        		vert.xyz += offsets.xyz + verticle * sina * half3(b, 0, oneMinusB);
        		
        		return vert;
		    }

			v2f vert (appdata v)
			{
				v2f o;
				#ifdef VERTEX_ROTATE_ON
				
					half3 rots = v.color.rgb * 2 - half3(1,1,1);

					half speed = v.color.r * rots.g * _RotateSpeed ;
					half verticle = _VerticleAmount* rots.b;

					v.vertex = lerp( v.vertex,  Rotate( v.vertex, _RotateParameters, speed, verticle) , v.color.r);
				#else

				#endif
				o.pos = UnityObjectToClipPos (v.vertex);
				
				o.uv.xy = TRANSFORM_TEX(v.texcoord,_MainTex).xy;
				o.uv.zw =  TRANSFORM_TEX(v.texcoord, _FlowLightMap) + frac( half2(_Scroll2X, _Scroll2Y) * _Time.x);


					//Faster but less accurate method (especially on non-uniform scaling)
					v.normal = normalize(v.normal);
					v.tangent = normalize(v.tangent);
					TANGENT_SPACE_ROTATION;
					o.c0 = mul(rotation, normalize(UNITY_MATRIX_IT_MV[0].xyz));
					o.c1 = mul(rotation, normalize(UNITY_MATRIX_IT_MV[1].xyz));
					o.lightDir  = mul(rotation, normalize(ObjSpaceLightDir(v.vertex)));
					
				o.color = v.color;

				float3 localView = normalize(ObjSpaceViewDir(v.vertex));
				o.fesnelBlink= (1 - saturate(dot(normalize( v.normal), localView))) * 2;

				/*
				
				
				half3 normalInView = mul (UNITY_MATRIX_MV, fixed4(v.normal,0)).xyz;
				o.capCoord.xy = half2((normalize(normalInView).xy * 0.25) + 0.25)  + half2( 0.5, 0) * (1- v.color.r);		//R: lowerLf

				
				o.capCoord.zw = o.capCoord + half2(0, 0.5);	
				
     			o.viewDir =  (ObjSpaceViewDir(v.vertex));
				o.lightDir =  (ObjSpaceLightDir(v.vertex));
				o.normal = v.normal;
				// o.nDotLUV  = half2( dot ( v.normal,  ObjSpaceLightDir(v.vertex)) * 0.5 + 0.5, 0.5) ;
				// float3 worldNormal = mul((float3x3)_Object2World, v.normal);
				// float3 shl = ShadeSH9(float4(worldNormal,1));
				// o.vlight = shl * _SHLightingScale;
				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				o.ambient = ShadeSH9(half4(worldNormal,1));
				*/
				return o;
			}

			float4 frag (v2f i) : COLOR
			{
				
				half2 flowUV = i.uv.zw;

				half alpha = 1;
				half3 mask = tex2D(_MaskMap, i.uv);
				#ifdef  TwoMask
					half3 maskB = tex2D(_MaskBMap, i.uv);
				#endif

			 	#ifdef transparentCullout_On 
					#ifdef  TwoMask 
						alpha = maskB.r;
						clip( alpha - _AlphaRef);
					#endif
			 	 #endif

			 	i.uv.w = -1;
			 	fixed3 col = tex2Dbias(_MainTex, i.uv);

			 	#ifdef CustomFace_On
			 		fixed3 face = tex2Dbias(_FaceTex, i.uv * 2);
			 		fixed3 faceSkin = tex2D(_FaceTex, i.uv * 2 + fixed2( 0.5,0));
			 		fixed faceMask = 1 - step(0.5, max ( i.uv.x * 2, i.uv.y) );
			 		col = lerp( col, face, faceMask);
			 	#endif

			 	//fixed3 normals =  normalize( half3(tex2D(_DetailMap, i.uv.xy).rg * 2 -1, 1));

				fixed3 normals = UnpackNormalMapWithAlpha(tex2D(_DetailMap, i.uv.xy)); 

				half2 capCoord01 = half2(dot(i.c0, normals), dot(i.c1, normals));
				half frenel = length(capCoord01);
				frenel = frenel * frenel * frenel;

				capCoord01  = capCoord01 * 0.25 + 0.25;
				half4 capCoord = half4(capCoord01.xyxy) + half4(0,0,0,0.5);

				half nDotL = dot(i.lightDir, normals);
				half halfNDotL = nDotL *0.5 + 0.5;
				half edgeMulti = frenel * nDotL;



				
				// g 通常是反射
				fixed3 matcapLowerLf = tex2D(_EnvMap, capCoord.xy);
				// b 通常是变光
				fixed3 matcapUpperLf = tex2D(_EnvMap, capCoord.zw);

				// col = lerp( col, matcapUpperLf * _MatCapScale , edgeMulti * mask.b);
				col += matcapLowerLf * _MatCapScale * mask.g ;
				col += matcapUpperLf * _MatCapScale * mask.b  * halfNDotL; 
				#if TwoMask
					#ifdef CustomFace_On
						maskB = lerp(maskB, faceSkin, faceMask);
					#endif
					half skin = maskB.g;
					

					float3 detail = tex2D(_DetailMap, i.uv * _DetailMap_ST.xy * 20);
					float y = (1 - skin) * 0.9 + 0.05;
					
					half2 nDotLUV =  half2( halfNDotL , y) ;
					fixed3 rampColor = tex2D(_RampMap, nDotLUV);
					col *= (rampColor *2);// * i.ambient;
				#endif


				#ifdef TwoMask 
					
					col += (_TintSkin-0.5) * skin;
			 		fixed2 t = saturate (2* (fixed2(maskB.b, -maskB.b ) + fixed2(-0.5, 0.5)));

				 	fixed base = saturate( i.color.r - t.x - t.y);
		 			//col.rgb *= fixed3(base,base,base) + 2* ( i.color.r * ( t.x * _Tint1.rgb + t.y * _Tint2.rgb)  +  (1-i.color.r) * _TintHair.rgb );
		 			half3 tint = 2* lerp(_TintHair.rgb, ( t.x * _Tint1.rgb + t.y * _Tint2.rgb),  i.color.r );
		 			col.rgb *= fixed3(base,base,base) + tint  ;
			 	#endif

				col.rgb *= _Color.rgb;
			 	fixed4 lightFlow = tex2D (_FlowLightMap, flowUV);
				float v = saturate(saturate(mask.r - _FlowThreshold)*4);
				lightFlow *= v *  _FlowMultiplier;	
				col += lightFlow * col * _FlowColor;
				col += _SelfIlluminated * i.fesnelBlink * _SelfIlluminateColor;//闪白特效
			 	return fixed4(col, alpha);
			 }
		
		ENDCG


		Subshader
		{
			Tags { "Queue"="Geometry+2" "LightMode"="ForwardBase"  "IgnoreProjector"="True" "RenderType"="Opaque"}
			
			Cull Back Lighting Off ZWrite On Fog { Mode Off }
			Pass
			{
				CGPROGRAM

					#pragma multi_compile OneMask TwoMask
					#pragma multi_compile transparentCullout_Off transparentCullout_On
					#pragma multi_compile CustomFace_Off CustomFace_On
					#pragma multi_compile VERTEX_ROTATE_OFF VERTEX_ROTATE_ON
					//#pragma shader_feature MATCAP_ACCURATE
					#pragma exclude_renderers flash
					
					#pragma target 2.0
					#pragma vertex vert
					#pragma fragment frag
					#pragma fragmentoption ARB_precision_hint_fastest
					
				ENDCG
			}

			UsePass "Mobile/VertexLit/SHADOWCASTER"
		}
		
	CustomEditor "CharacterEquipMaterialInspector"
}