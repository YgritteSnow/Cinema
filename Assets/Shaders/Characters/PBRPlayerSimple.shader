Shader "* Character/PBR Player Simple" {
	Properties {
		[NoScaleOffset]_MainTex ("BaseColor", 2D) = "white" {}
		[NoScaleOffset]_MetallicGlossMap("MSS(R:Metallic G:Smoothness B:Unused)", 2D) = "white" {}
		[NoScaleOffset]_MaskMap("Mask(R:Alpha G:ClothTint B:Emission)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[Space]_CutOut("Alpha Cut off",Range(0,1)) = 0.5
		[Space]_ClothTint1("Cloth Tint 1", Color) = (1,1,1)
		[Space]_ClothTint2("Cloth Tint 2", Color) = (1,1,1)
		[Space]_SelfIlluminateColor("Emission Color", Color) = (0,0,0)
		[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0
		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)

		[Space][Space][Space]_animParams("Animmation Params(xyz控制扰动，w控制强度)",Vector) = (1.975, 0.793, 0.375, 0.193)
		[Space][Space][Space]_Wind("Wind params(xyz控制位移距离，w控制强度)",Vector) = (0,0,0,1)
	}

	   
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM

		#pragma surface surf Standard vertex:vert keepalpha exclude_path:prepass nolightmap noforwardadd 
		//fullforwardshadows
		#pragma skip_variants FOG_EXP FOG_EXP2 POINT SPOT POINT_COOKIE DIRECTIONAL_COOKIE DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE VERTEXLIGHT_ON
		#pragma target 3.0
		#include "TerrainEngine.cginc"
		#include "../CGIncludes/ZL_CGInclude.cginc"

		sampler2D _MainTex;
		sampler2D _BumpMap;
		sampler2D _MaskMap;
		sampler2D _MetallicGlossMap;
		float4 _ClothTint1;
		float4 _ClothTint2;
		float4 _animParams;
		float4 _SelfIlluminateColor;
		float _CutOut;
		float4 _Color;
		float _SelfIlluminated;

		struct Input 
		{
			float2 uv_MainTex;
			float2 depthAndFesnel;//x:depthInAlpha,y:fesnelBlink
		};

		void vert (inout appdata_full v, out Input o) 
		{
			UNITY_INITIALIZE_OUTPUT(Input,o);
			float4 newPos = AnimateVertex(v.vertex, v.normal, _animParams);
			v.vertex = lerp(v.vertex, newPos,1 - v.color.g);

			o.depthAndFesnel.x = ZL_DEPTH_IN_ALPHA(v.vertex);
			float3 localView = normalize(ObjSpaceViewDir(v.vertex));
			o.depthAndFesnel.y = (1 - saturate(dot(normalize( v.normal), localView))) * 2;
		}

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 mss = tex2D(_MetallicGlossMap, IN.uv_MainTex);
			fixed4 normal = tex2D(_BumpMap, IN.uv_MainTex);
			fixed4 mask = tex2D(_MaskMap, IN.uv_MainTex);

			//装备染色
			fixed2 clothTintMask = saturate(fixed2(mask.g - 0.5, -mask.g + 0.5 ) * 2) ;
			fixed clothTintArea = clothTintMask.x + clothTintMask.y;
			//fixed3 clothTintLuminance = Luminance(baseColor.rgb * clothTintArea);//美术说要自已变灰，所以不用进行Luminance计算
			fixed3 clothTintArea1Color = baseColor * _ClothTint1.a * _ClothTint1 * clothTintMask.x;
			fixed3 clothTintArea2Color = baseColor * _ClothTint2.a * _ClothTint2 * clothTintMask.y;
			

			//Alpha
			clip(mask.r - _CutOut);

			
			o.Albedo = baseColor.rgb * (1 - clothTintArea) + clothTintArea1Color + clothTintArea2Color;
			o.Albedo *= _Color.rgb;//从外部传入Lightmap颜色
			o.Metallic = mss.r;
			o.Smoothness = mss.g;
			o.Emission = mask.b * _SelfIlluminateColor + _SelfIlluminated * IN.depthAndFesnel.y * _SelfIlluminateColor;
			//o.Normal = UnpackNormal(normal);
			o.Normal = UnpackNormalMapWithAlpha(normal); 
			o.Alpha = IN.depthAndFesnel.x;

		}
		ENDCG
	}
	FallBack "Diffuse"
}
