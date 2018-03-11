
Shader "* Character/PBR Weapon" {
	Properties {
		[NoScaleOffset]_MainTex ("BaseColor", 2D) = "white" {}
		[NoScaleOffset]_MetallicGlossMap("MSF(R:Metallic G:Smoothness B:Flow)", 2D) = "white" {}
		[NoScaleOffset]_MaskMap("Mask(R:Alpha G:ClothTint B:Emission)", 2D) = "white" {}
		[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}
		[Space]_CutOut("Alpha Cut off",Range(0,1)) = 0.5
		[Space]_ClothTint1("Cloth Tint 1", Color) = (1,1,1)
		[Space]_ClothTint2("Cloth Tint 2", Color) = (1,1,1)
		[Space]_SelfIlluminateColor("Emission Color", Color) = (0,0,0)
		[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0
		_FlowTex("Flow Texture", 2D) = "black" {}
		_FlowColor("Flow Color", Color) = (1,1,1)
		axisAngle("旋转角度",Vector) = (0,10,0,0)
		translation("移动距离(W:速度)",Vector) = (0,0.1,0,0.1)
		translationOffset("移动偏移",Vector) = (0,0,0,0)
		fitness("粗细(X：速度 Y：幅度)",Vector) = (0,0,0,0)

		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
	}


	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		
		#pragma surface surf Standard keepalpha vertex:vert exclude_path:prepass nolightmap noforwardadd
		#pragma skip_variants FOG_EXP FOG_EXP2 POINT SPOT POINT_COOKIE DIRECTIONAL_COOKIE DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE VERTEXLIGHT_ON
		//fullforwardshadows
		#pragma target 3.0

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
		float4 axisAngle;
		float4 translation;
		float4 translationOffset;
		float4 fitness;

		half _Glossiness;
		half _Metallic;
		fixed4 _SelfIlluminateColor;
		fixed4 _ClothTint1;
		fixed4 _ClothTint2;
		half _FlowSpeed;
		float4 _FlowTex_ST;
		fixed4 _FlowColor;

		struct Input 
		{
			float2 uv_MainTex;
			float2 depthAndFesnel;//x:depthInAlpha,y:fesnelBlink
		};

		float4x4 RotationMatrix(float4 axisAngle,float4 color)
		{
			float speed = color.b * 2.0 - 1.0;
			axisAngle *= _Time.y * speed;
	
			float radX = radians(axisAngle.x);
			float radY = radians(axisAngle.y);
			float radZ = radians(axisAngle.z);

			float sinX = sin(radX);
			float cosX = cos(radX);
			float sinY = sin(radY);
			float cosY = cos(radY);
			float sinZ = sin(radZ);
			float cosZ = cos(radZ);
	
	
			float4x4 xRotation = 
			{
				float4(1,0,0,0),
				float4(0,cosX,-sinX,0),
				float4(0,sinX,cosX,0),
				float4(0,0,0,1)
			};

			float4x4 yRotation = 
			{
				float4(cosY,0,sinY,0),
				float4(0,1,0,0),
				float4(-sinY,0,cosY,0),
				float4(0,0,0,1)
			};

			float4x4 zRotation = 
			{
				float4(cosZ,-sinZ,0,0),
				float4(sinZ,cosZ,0,0),
				float4(0,0,1,0),
				float4(0,0,0,1)
			};

			float4x4 CombineRotation = mul(xRotation,yRotation);
			CombineRotation = mul(CombineRotation,zRotation);

			return CombineRotation;
		}

		float4x4 MoveMatrix(float4 translation,float4 translationOffset,float4 color)
		{
			//顶点色控制幅度
			float amount = 1.0 - color.g;
			translation *= amount;

			//移动速度0到1
			float translationLerp = sin(_Time.y * translation.w) * 0.5 + 0.5;
	
			//距离
			float4 newTranslation = lerp(-translation, translation, translationLerp);
	
			//顶点色控制方向和速度
			float speed = color.r * 2.0 - 1.0;
			newTranslation *= speed;
	
			float4x4 translationMatrix = 
			{
				float4(1,0,0,newTranslation.x + translationOffset.x),
				float4(0,1,0,newTranslation.y + translationOffset.y),
				float4(0,0,1,newTranslation.z + translationOffset.z),
				float4(0,0,0,1)
			};
			return translationMatrix;
		}

		void vert (inout appdata_full v, out Input o) 
		{
			UNITY_INITIALIZE_OUTPUT(Input,o);

			//顶点色Alpha控制粗细
			float amount = 1.0 - v.color.a;
			float4 newPos = float4(v.vertex.rgb + v.normal.rgb * amount * sin(_Time.y * fitness.x) * fitness.y, 1);
			newPos = mul(MoveMatrix(translation,translationOffset,v.color), newPos);
			newPos = mul(RotationMatrix(axisAngle,v.color), newPos);
			v.vertex = newPos;

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

			//装备染色
			fixed2 clothTintMask = saturate(fixed2(detailFlowMask.g - 0.5, -detailFlowMask.g + 0.5 ) * 2) ;
			fixed clothTintArea = clothTintMask.x + clothTintMask.y;
			//fixed3 clothTintLuminance = Luminance(baseColor.rgb * clothTintArea);//美术说要自已变灰，所以不用进行Luminance计算
			fixed3 clothTintArea1Color = baseColor * _ClothTint1.a * _ClothTint1 * clothTintMask.x;
			fixed3 clothTintArea2Color = baseColor * _ClothTint2.a * _ClothTint2 * clothTintMask.y;

			//流光UV
			float2 flowUV = TRANSFORM_TEX(IN.uv_MainTex,_FlowTex);
			flowUV.x += _Time * _FlowTex_ST.z;
			flowUV.y += _Time * _FlowTex_ST.w;
			fixed4 flowTex = tex2D(_FlowTex, flowUV);


			//Alpha
			clip(detailFlowMask.r - _CutOut);

			o.Albedo = baseColor.rgb * (1 - clothTintArea) + clothTintArea1Color + clothTintArea2Color;
			o.Albedo *= _Color.rgb;//从外部传入Lightmap颜色
			o.Metallic = msf.r;
			o.Smoothness = msf.g;
			o.Emission = detailFlowMask.b * _SelfIlluminateColor + msf.b * flowTex * _FlowColor * _FlowColor.a + _SelfIlluminated * IN.depthAndFesnel.y * _SelfIlluminateColor;
			//o.Normal = UnpackNormal(normal);
			o.Normal = UnpackNormalMapWithAlpha(normal); 
			o.Alpha = IN.depthAndFesnel.x;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
