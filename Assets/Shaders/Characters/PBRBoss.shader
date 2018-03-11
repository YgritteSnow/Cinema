
Shader "* Character/PBR Boss" {
	Properties {
		[NoScaleOffset]_MainTex ("BaseColor", 2D) = "white" {}
		[NoScaleOffset]_MetallicGlossMap("MSF(R:Metallic G:Smoothness B:Flow)", 2D) = "white" {}
		[NoScaleOffset]_MaskMap("Mask(R:Alpha G:ClothTint B:Emission)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[Space]_CutOut("Alpha Cut off",Range(0,1)) = 0.5
		[Space]_SelfIlluminateColor("Emission Color", Color) = (0,0,0)
		[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0
		_FlowTex("Flow Texture", 2D) = "black" {}
		_FlowColor("Flow Color", Color) = (1,1,1)


		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
		[Space][Space][Space]_animParams("Animmation Params",Vector) = (1.975, 0.793, 0.375, 0.193)
		[Space][Space][Space]_Wind("Wind params",Vector) = (0,0,0,1)
	}


	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM

		#pragma surface surf Standard keepalpha vertex:vert exclude_path:prepass nolightmap noforwardadd
		#pragma skip_variants FOG_EXP FOG_EXP2 POINT SPOT POINT_COOKIE DIRECTIONAL_COOKIE DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE VERTEXLIGHT_ON
		//fullforwardshadows
		#pragma target 3.0
		#include "TerrainEngine.cginc"
		#include "../CGIncludes/ZL_CGInclude.cginc"

		sampler2D _MainTex;
		sampler2D _BumpMap;
		sampler2D _MetallicGlossMap;
		sampler2D _MaskMap;
		sampler2D _FlowTex;
		float4 _animParams;
		float _CutOut;
		float4 _Color;
		float _SelfIlluminated;


		struct Input 
		{
			float2 uv_MainTex;
			float2 depthAndFesnel;//x:depthInAlpha,y:fesnelBlink
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _SelfIlluminateColor;
		half _FlowSpeed;
		float4 _FlowTex_ST;
		fixed4 _FlowColor;



		void vert (inout appdata_full v, out Input o) 
		{
			UNITY_INITIALIZE_OUTPUT(Input,o);
			float4 newPos = AnimateVertex(v.vertex, v.normal, _animParams);
			v.vertex = lerp(v.vertex, newPos,1 - v.color.g);
			o.depthAndFesnel.x = ZL_DEPTH_IN_ALPHA(v.vertex);
			float3 localView = normalize(ObjSpaceViewDir(v.vertex));
			o.depthAndFesnel.y= (1 - saturate(dot(normalize( v.normal), localView))) * 2;
		}

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 msf = tex2D(_MetallicGlossMap, IN.uv_MainTex);
			fixed4 normal = tex2D(_BumpMap, IN.uv_MainTex);
			fixed4 detailFlowMask = tex2D(_MaskMap, IN.uv_MainTex);

			//流光UV
			float2 flowUV = TRANSFORM_TEX(IN.uv_MainTex,_FlowTex);
			flowUV.x += _Time * _FlowTex_ST.z;
			flowUV.y += _Time * _FlowTex_ST.w;
			fixed4 flowTex = tex2D(_FlowTex, flowUV);


			//Alpha
			clip(detailFlowMask.r - _CutOut);

			o.Albedo = baseColor.rgb;
			o.Albedo *= _Color.rgb;//从外部传入Lightmap颜色
			o.Metallic = msf.r;
			o.Smoothness = msf.g;
			o.Emission = detailFlowMask.b * _SelfIlluminateColor + msf.b * flowTex * _FlowColor * _FlowColor.a + _SelfIlluminated * IN.depthAndFesnel.y * _SelfIlluminateColor;;
			//o.Normal = UnpackNormal(normal);
			o.Normal = UnpackNormalMapWithAlpha(normal); 
			o.Alpha = IN.depthAndFesnel.x;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
