// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "* Character/Skin And Eyeball" {
	Properties {
 		[Toggle(ZL_CHARACTER_CUSTOM_ON)]_CustomFace("叠加纹身和瞳孔染色",Float) = 0
        [NoScaleOffset]_MainTex ("Base Color", 2D) = "white" {}
		[NoScaleOffset]_BumpTex ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset]_SpecTex ("Specular (R：高光 G：瞳孔染色区 B：未使用)", 2D) = "white" {}
		[ShowWhenHasKeyword(ZL_CHARACTER_CUSTOM_ON)]_Tattoo ("纹身Mask",2D) = "black" {}
		[ShowWhenHasKeyword(ZL_CHARACTER_CUSTOM_ON)]_TattooChannel("纹身Mask通道选择", Vector) = (1,1,1,1)
		[ShowWhenHasKeyword(ZL_CHARACTER_CUSTOM_ON)]_TattooTint("纹身颜色(Alpha透明度)", Color) = (1,1,1,1)
		[NoScaleOffset]_RimLightTex ("边光颜色查找表", 2D) = "white" {}

		[Space][Space][Space]_HalfLambert( "半Lambert光照程度", Range(0,1)) = 1
		[Space][Space][Space]_KeyLightColorAmount( "受灯光颜色影响的程度", Range(0,1)) = 1
		[Space][Space][Space]_AmbientColorAmount( "受环境光颜色影响的程度", Range(0,1) ) = 1
		[Space][Space][Space]_RimAmount("边光强度", Range(0,2)) = 1
		[Space][Space][Space]_colMtlSpecular("高光颜色", Color) = (1,1,1,1)
		[Space][Space][Space]_Power("光泽度", float) = 15.0
		[Space][Space][Space]_SpecularMultiple("高光强度", float) = 1.0

		[Space][Space][Space][NoScaleOffset]_ReflectionTex ("眼球反射图(顶点色R通道区分眼球材质)", 2D) = "black" {}

		_EyeLightGlossiness("眼神光大小",float) = 3
		_EyeLightAmont("眼神光强度",float) = 0.8

 		_MainUVOffset("UV偏移(xy表示脸贴图占身体贴图的百分比，zw表示眼球贴图占脸贴图的百分比)",Vector) = (1,1,4,4)
		_EyeReflectionUVOffset("眼神光偏移(xy表示偏移幅度，zw修正初始偏移)",Vector) = (0,0,0,0)
		[ShowWhenHasKeyword(ZL_CHARACTER_CUSTOM_ON)]_EyeTint("瞳孔染色",Color) = (0.125,0.1,0.1)
		//_DebugRotationAngle("Debug Rotation Angle(如果能绕眼球处正确转动表示模型正确，w为眼球的高度)",Vector) = (0,0,0,0)
		//_DebugColorAngle("Debug Color Angle(如果能绕眼球处正确转动表示模型正确，w为眼球的高度)",Vector) = (0,0,0,0)

		_Color("这值由光照图去改变，美术保持默认白色不要动", Color) = (1,1,1)
		[HideInInspector]_SelfIlluminateColor("Emission Color",Color) = (0,0,0)
		[HideInInspector]_SelfIlluminated("自发光倍增（用于闪白特效）", Float) = 0

   }

	CGINCLUDE
	#include "UnityCG.cginc"
	#include "Lighting.cginc"
	#include "AutoLight.cginc"
	#include "../CGIncludes/ZL_CGInclude.cginc"

	float3 glb_CalcReflection(float3 N, float3 L)
	{
		return 2.0f * dot(N, L) * N  - L;
	}

	sampler2D _MainTex;
	sampler2D _BumpTex;
	sampler2D _SpecTex;
	sampler2D _RimLightTex;

	#if ZL_CHARACTER_CUSTOM_ON
	sampler2D _Tattoo;
	float4 _Tattoo_ST;
	float4 _TattooChannel;
	float4 _TattooTint;
	float4 _EyeTint;
	#endif


	float _AmbientColorAmount;
	float _KeyLightColorAmount;
	float4 _MainUVOffset;
	float _Power;
	float _SpecularMultiple;
	float4 _colMtlSpecular;
	float _RimAmount;
	float _HalfLambert;
	sampler2D _ReflectionTex;
	float4 _EyeReflectionUVOffset;
	float _EyeLightGlossiness;
	float _EyeLightAmont;
	float4 _DebugRotationAngle;
	float4 _DebugColorAngle;
	float4 _SelfIlluminateColor;
	float4 _Color;
	float _SelfIlluminated;

    struct v2f {
        float4 pos : SV_POSITION;
		float4 uvBase           : TEXCOORD0;
		#if ZL_CHARACTER_CUSTOM_ON
			float4 tattooUV			: TEXCOORD5;
		#endif
		float3 ViewDir          : TEXCOORD1;
		float3 LightDir         : TEXCOORD2;
		float3 normalWorld		: TEXCOORD3;
		float4 eyeLightOffset	: TEXCOORD4;
		float4 vertColor		: COLOR;
 		//SHADOW_COORDS(6)
 		float4 depthAndFesnel : TEXCOORD6;//x:depthInAlpha,y:fesnelBlink
		UNITY_FOG_COORDS(7)
   };
        
		float4x4 GetRotationMatrix(float4 angle)
		{
			float radX = radians(angle.x);
			float radY = radians(angle.y);
			float radZ = radians(angle.z);

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

			float4x4 bipSpaceRotation = mul(xRotation,yRotation);
			bipSpaceRotation = mul(zRotation,bipSpaceRotation);
			return bipSpaceRotation;
		}

    v2f vert (appdata_full v) {
        v2f o;
		UNITY_INITIALIZE_OUTPUT(v2f,o);
        o.pos = UnityObjectToClipPos( v.vertex );

		v.normal = normalize(v.normal);
		v.tangent = normalize(v.tangent);
        //float3 binormal = normalize( cross(v.normal,v.tangent) * v.tangent.w);
		float3 binormal = normalize( cross(v.normal,v.tangent) * v.tangent.w);
       	float3x3 rotation = float3x3( v.tangent.xyz, binormal, v.normal );
       	

       	o.ViewDir  = normalize(mul(rotation, ObjSpaceViewDir(v.vertex)));

		//Max输出的模型，想要在模型空间进行相机位置和方向的计算，需要对模型轴向进行偏移才能结果正确：
		//1：如果没有蒙皮：X轴需要旋转-90度，其它两轴不动，才能和Unity的坐标轴方向对上
		//2：如果有Bip骨骼的蒙皮：X轴需要旋转90度，Y轴不动，Z轴需要旋转-90度，才能和Unity的坐标轴方向对上
		//用Bone骨骼的，还没有测试
		float4 axisFix = float4(90, 0, -90, 0);
		#if  ZL_VERTEX_CALCULATE_BIP
		#else
			axisFix = float4(-90, 0, 0, 0);
		#endif

		//o.eyeLightOffset = ObjSpaceViewDir(v.vertex);//让ViewDir变成线性平滑的，如果直接用正常的ViewDir，因为带曲度导致UV有拉伸
		o.eyeLightOffset = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1));
		o.eyeLightOffset = o.eyeLightOffset - v.vertex;
		float4x4 bipSpaceColorMatrix = GetRotationMatrix(axisFix);
		o.eyeLightOffset = normalize(mul(bipSpaceColorMatrix, o.eyeLightOffset));
	 


	 	o.LightDir = normalize(mul(rotation, ObjSpaceLightDir(v.vertex)));
     	o.normalWorld = UnityObjectToWorldNormal(v.normal);
		o.uvBase.xy = v.texcoord.xy * _MainUVOffset.xy;
		o.uvBase.zw = v.texcoord.xy * _MainUVOffset.zw;

		#if ZL_CHARACTER_CUSTOM_ON
			o.tattooUV.xy = TRANSFORM_TEX(v.texcoord,_Tattoo);
		#endif
		//顶点色R用来区分皮肤与眼球
		o.vertColor = v.color;
		//TRANSFER_SHADOW(o);
		UNITY_TRANSFER_FOG(o,o.pos);

		o.depthAndFesnel.x = ZL_DEPTH_IN_ALPHA(v.vertex);
	 	float3 localView = normalize(ObjSpaceViewDir(v.vertex));
		o.depthAndFesnel.y = (1 - saturate(dot(normalize( v.normal), localView))) * 2;
        return o;
    }
        

 fixed4 frag (v2f Input) : COLOR
	{ 

	//return Input.eyeLightOffset.rgbr;
		//灯光的亮度
		float keyLightIntensity = Luminance(_LightColor0.rgb * _Color.rgb);//_Color从外部传入Lightmap颜色

		//环境光颜色
		float3 SH9Color = ShadeSH9(float4(normalize(Input.normalWorld.xyz),1)) * _Color.rgb;
		//return SH9Color.rgbr;


		//环境光的亮度
		float ambientIntensity = Luminance(SH9Color);

		//灯光颜色的供献程度，由变量_KeyLightColorAmount控制Lerp值
		float3 keyLight = lerp( float3(1,1,1) * keyLightIntensity, _LightColor0.rgb, _KeyLightColorAmount);

		//环境光颜色的供献程度，由变量_AmbientColorAmount控制Lerp值
		float3 ambient = lerp( float3(1,1,1) * ambientIntensity, SH9Color, _AmbientColorAmount);

		//Diffuse贴图
		float4 colBaseTex  = tex2D(_MainTex,Input.uvBase);

		//高光贴图
		float4 colSpecularTex = tex2D(_SpecTex,Input.uvBase);

		//法线图
		//float3 Normal = UnpackNormal(tex2D(_BumpTex,Input.uvBase));
		float3 Normal = UnpackNormalMapWithAlpha(tex2D(_BumpTex,Input.uvBase));
		
		#if ZL_CHARACTER_CUSTOM_ON
			//纹身
			float4 tattoo = tex2D(_Tattoo,Input.tattooUV);
		#endif

		float3 LightDir	= normalize(Input.LightDir);
		float3 ViewDir  = normalize(Input.ViewDir);
		float3 ViewReflection = glb_CalcReflection(Normal, ViewDir);

		//正常光照
		float fNDotL = saturate(dot(Normal, LightDir));
		float halfLambert = fNDotL * 0.5 + 0.5;
		float brightness = lerp(fNDotL, halfLambert, _HalfLambert);


		float fNdotV = saturate(dot(Normal,ViewDir));
		float3 RimLight = tex2D(_RimLightTex, float2(fNdotV, 0.0)).rgb * _RimAmount;


		float fVRDotL = saturate(dot(ViewReflection, LightDir));

		
		float3 vSpecColor = keyLightIntensity * _SpecularMultiple * _colMtlSpecular.rgb * colSpecularTex.r ;
		vSpecColor *= pow(fVRDotL+0.001, _Power);    
		
		float3 rampColor = brightness * keyLight * ambient;





		/*
		//笑傲眼神光方式
		float2 HalfVec;
		HalfVec.x = dot(-Input.normalWorld, UNITY_MATRIX_V[0].xyz);
		HalfVec.y = dot(-Input.normalWorld, UNITY_MATRIX_V[1].xyz);
		float2 camReflect;
		camReflect.x = (HalfVec.x + 1) * 0.5;
		camReflect.y = (1 - HalfVec.y) * 0.5;
		float4 colReflectTex = tex2D(_ReflectionTex, camReflect.xy);
		float eye_power = pow(colReflectTex.r, _EyeLightGlossiness) * _EyeLightAmont;
		
		//偏移扭曲方式
		float2 uv = Input.uvBase.xy;
		float biasLerp = abs(Input.uvBase.y - 0.5) * 4;
		biasLerp = pow(biasLerp, 1.6);
		float bias = lerp(1,0,biasLerp);
		uv.x += (ViewDir.x * (0.4 + -0.8 * bias));

		fixed4 tex1 = tex2D(_ReflectionTex, uv);
		fixed4 tex2 = tex2D(_ReflectionTex, Input.uvBase);
		fixed4 test = tex1;
		return colBaseTex * fNDotL + test * specTex;
		*/
		
		//直接偏移UV方式
		fixed4 colReflectTex = tex2D(_ReflectionTex, Input.uvBase.zw + _EyeReflectionUVOffset.xy * Input.eyeLightOffset.xy + _EyeReflectionUVOffset.zw);
		float eye_power = pow(colReflectTex.r, _EyeLightGlossiness) * _EyeLightAmont;
		#if ZL_DEBUG_MODE_ON
			return Input.eyeLightOffset.xyzx + eye_power;
		#endif

		

		//顶点色R用来区分皮肤与眼球
		#if ZL_CHARACTER_CUSTOM_ON
			fixed4 finalTattooMask = tattoo * _TattooChannel;
			float3 skinColor = lerp(colBaseTex.rgb, _TattooTint.rgb,_TattooTint.a * clamp(finalTattooMask.r + finalTattooMask.g + finalTattooMask.b,0,1));// = (rampColor + RimLight) * colBaseTex.rgb + vSpecColor;
			skinColor = skinColor * (rampColor + RimLight) + vSpecColor;
			float3 eyeColor = lerp(colBaseTex.rgb ,colBaseTex.rgb * _EyeTint.rgb * 2, colSpecularTex.g);
			eyeColor = eyeColor * brightness + eye_power * colSpecularTex.r;
		#else
			float3 skinColor = (rampColor + RimLight) * colBaseTex.rgb + vSpecColor;
			float3 eyeColor = colBaseTex.rgb * brightness + eye_power * colSpecularTex.g;
		#endif

		float3 ResultColor = lerp(eyeColor, skinColor, Input.vertColor.r);
		//ResultColor = lerp(ResultColor * ambient, ResultColor, SHADOW_ATTENUATION(Input));
		ResultColor += _SelfIlluminated * Input.depthAndFesnel.y * _SelfIlluminateColor;
		UNITY_APPLY_FOG(Input.fogCoord, ResultColor);

		return half4(ResultColor.rgb, Input.depthAndFesnel.x);
	}



	ENDCG

	SubShader {
	    Pass {
	    	Tags { "Queue"="Geometry" "LightMode"="ForwardBase"  "IgnoreProjector"="True" "RenderType"="Opaque" }	
			Cull Back
			Lighting Off
			Fog { Mode Off }
	        CGPROGRAM
			
	        #pragma vertex vert
	        #pragma target 3.0
	        #pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest 
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			#pragma multi_compile_fog		
			#pragma multi_compile ZL_CHARACTER_CUSTOM_OFF ZL_CHARACTER_CUSTOM_ON
			#pragma multi_compile ZL_DEBUG_MODE_OFF ZL_DEBUG_MODE_ON
			#pragma multi_compile ZL_VERTEX_CALCULATE_BIP ZL_VERTEX_CALCULATE_BONE ZL_VERTEX_CALCULATE_NOSKIN
	        ENDCG
	    }

		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"

	}

	FallBack "Diffuse"

}

