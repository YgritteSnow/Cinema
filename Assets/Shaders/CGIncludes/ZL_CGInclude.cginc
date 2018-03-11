#ifndef ZL_CG_INCLUDE
#define ZL_CG_INCLUDE

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
#include "TerrainEngine.cginc"


//带UV2的tangent结构体
struct appdata_tan_uv2 {
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;
};

//用于光照计算的结构
struct LightingVectors {
	float3 viewDir;
	float3 lightDir;
	float3 vertexNormal;
	float3 worldNormal;
	float3 normalMap;
	float2 lightmapUV;
	float3 specularParameters;//x:glossiness  y:specularLevel  z:specularControlTexture
	float shadow;
};

//w为worldSpaceNormal.x，用来算SH9和反射
#define TANGENT_SPACE_VERCTORS(idx1,idx2,idx3) \
	float4 tangentSpaceLightDir	: TEXCOORD##idx1; \
	float4 tangentSpaceViewDir	: TEXCOORD##idx2; \
	float4 tangentSpaceVertexNormal	: TEXCOORD##idx3;


//Unity自带的TANGENT_SPACE_ROTATION不会去normalize向量，缩放物体时法线贴图显示会出问题，也不会创建LightDir和ViewDir，
#define TANGENT_SPACE_CALCULATE \
	v.normal  = normalize(v.normal); \
	v.tangent  = normalize(v.tangent); \
	float3 binormal = normalize(cross(v.normal.xyz, v.tangent.xyz) * v.tangent.w); \
	float3x3 rotation = float3x3( v.tangent.xyz, binormal, v.normal ); \
	float3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal)); \
	o.tangentSpaceLightDir = float4(normalize(mul(rotation, ObjSpaceLightDir(v.vertex))), worldNormal.x);  \
	o.tangentSpaceViewDir = float4(normalize(mul(rotation, ObjSpaceViewDir(v.vertex))), worldNormal.y);  \
	o.tangentSpaceVertexNormal = float4(normalize(mul(rotation, v.normal)).xyz, worldNormal.z);


//世界距阵定义
#define WORLD_MATRIX(idx1,idx2,idx3) \
float4 worldMatrixRow0 : TEXCOORD##idx1; \
float4 worldMatrixRow1 : TEXCOORD##idx2; \
float4 worldMatrixRow2 : TEXCOORD##idx3;


//存worldPos是因为以后计算高度雾或者一些特殊效果有用。
#define CALCULATE_WORLD_MATRIX \
	float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; \
	fixed3 worldNormal = UnityObjectToWorldNormal(v.normal); \
	fixed3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz); \
	fixed tangentSign = v.tangent.w * unity_WorldTransformParams.w; \
	fixed3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign; \
	o.worldMatrixRow0 = float4(worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x); \
	o.worldMatrixRow1 = float4(worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y); \
	o.worldMatrixRow2 = float4(worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z);


//光照图参数(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float ZL_LightMapContrast;
float ZL_LightMapMultiply;
//RGBM的解码Lightmap的方式，效果和原始的Lightmap一样(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
inline half3 DecodeLightmapRGBM (half4 lightmap)
{
	half d = saturate(  (lightmap.a - 0.22) * (ZL_LightMapContrast + 1) + 0.22);
	half3 lm  = 5 * ZL_LightMapMultiply * d *  lightmap.rgb ;
	return  lm;
}


//自已UmpackNormal，算法和UnpackNormal的DXT5nm方式一样，好处是可以使用NormalMap的B通道来控制高光，坏处是像素感明显，给场景用
inline float3 UnpackNormalMap(fixed4 normalmapColor)
{
	float3 normal;
	normal.xy = normalmapColor.rg * 2 - 1;
	normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
	return normal;
}

//自已UmpackNormal，算法和UnpackNormal的DXT5nm方式一样，好处是可以使用NormalMap的B通道来控制高光，坏处是像素感明显，给角色用
inline float3 UnpackNormalMapWithAlpha(fixed4 normalmapColor)
{
	float3 normal;
	normal.xy = normalmapColor.ra * 2 - 1;
	normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
	return normal;
}

//计算NormalMap光照，并且返回normal
inline half CalculateNormalmap(float4 worldMatrixRow0,float4 worldMatrixRow1,float4 worldMatrixRow2, float4 normalmapColor, out fixed3 normal)
{
	normal = UnpackNormalMap(normalmapColor);
	//地形的Addpass用
	//normal = normalmapColor.rgb;

	fixed3 worldN;
	worldN.x = dot(worldMatrixRow0.xyz, normal);
	worldN.y = dot(worldMatrixRow1.xyz, normal);
	worldN.z = dot(worldMatrixRow2.xyz, normal);
	normal = worldN;
	half diff = saturate(dot(normalize(normal), _WorldSpaceLightPos0.xyz));
	return diff;
}


//传统的BulinnPhong高光：specularParameters  x:glossiness  y:specularLevel  z:specularControlTexture
inline float CalculateSpecular(float3 normal,float3 viewDir, float3 lightDir, float3 specularParameters)
{
	float3 halfVector = viewDir + lightDir;
	fixed nh = max (0.001, dot(normalize(normal), normalize(halfVector)));
	float spec = pow (nh, specularParameters.x) * specularParameters.y * specularParameters.z;
	return spec;
}

//传入参数，算出普通Lambert光照，方便修改
//gspecularParameters参数为：  x:glossiness  y:specularLevel  z:specularControlTexture
inline half3 SceneLighting(float3 mapNormal,float3 vertexNormal, float3 worldNormal,float2 lightmapUV, float3 lightDir, float3 viewDir, float3 specularParameters, float shadow)
{
	half3 finalLight = 1;
	half3 ambientLighting = ShadeSH9(float4(normalize(worldNormal),1));
	float darknessClamp = saturate(dot(normalize(vertexNormal), normalize(lightDir)));
	half lambertLight = saturate(dot(normalize(mapNormal), normalize(lightDir)));

	//#if ZL_LIGHTMAP_ON
		//finalLight *= DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap,lightmapUV));
		half3 lightmapColor = DecodeLightmapRGBM(UNITY_SAMPLE_TEX2D(unity_Lightmap,lightmapUV));
		finalLight *= lightmapColor;
	//#else
		//finalLight = lerp(ambientLighting, _LightColor0.rgb, lambertLight);
	//#endif


	//#if ZL_NORMALMAP_ON && ZL_LIGHTMAP_ON
		half normalmapLighting = lambertLight + (1 - darknessClamp);//去除无法线贴图影响的实时光照暗部，否则lightmap的暗部太黑
		finalLight *= normalmapLighting;
	//#endif


	//#if ZL_SPECULAR_ON
		float3 specValue = CalculateSpecular(mapNormal,viewDir,lightDir,specularParameters) * darknessClamp * _LightColor0.rgb;

		//#if ZL_LIGHTMAP_ON
		specValue *= lightmapColor;
		//#endif

		finalLight += specValue;
	//#endif


	//#if ZL_LIGHTMAP_ON
		//光照图阴影和实时阴影不太好调合
		half3 frontSideShadowColor = ambientLighting * finalLight;//这样至少当环境色为白色时，还能返回正常颜色
		half3 backSideShadowColor = finalLight;
		half3 shadowCorrect = lerp(backSideShadowColor, frontSideShadowColor,  darknessClamp);//根据模型顶点光照决定正面和背面的阴影分别是什么样子的，正面是环境光*原始颜色，背面是原始颜色
		finalLight = lerp(shadowCorrect, finalLight, shadow);
	//#else
		//finalLight = lerp(ambientLighting, finalLight, shadow);
	//#endif

	return finalLight;
}

//树叶、草顶点动画
float4 _TranslationDistance;//移动距离
float4 _TranslationOffset;//起始偏移距离
float _TranslationSpeed;//移动速度
float4 _TurbulentSpeed;//扰动速度
float4 _TurbulentRange;//扰动幅度
float4 _SinWaveAmount;//正弦波幅度
float4 _SinWaveLength;//正弦波长
float4 _SinWaveSpeed;//正弦波速度
float4 _SinWaveDirection;//正弦波方向
inline float4 AnimateVertexInWorldSpace(float4 vertex, float4 vertexColor, float _TranslationSpeed, float4 _TranslationDistance, float4 _TranslationOffset, float4 _TurbulentSpeed, float4 _TurbulentRange, float4 _SinWaveAmount, float4 _SinWaveLength, float4 _SinWaveSpeed, float4 _SinWaveDirection)
{
	float4 sinWave = sin(vertex * _SinWaveLength + _Time.y * _SinWaveSpeed + dot(vertex, _SinWaveDirection)) * _SinWaveAmount;
	sinWave *= vertexColor.a;

	//乱流
	float4 vWavesIn = _Time.y * _TurbulentSpeed;
	// 1.975, 0.793, 0.375, 0.193 are good frequencies
	float4 vWaves = (frac( vWavesIn * float4(1.975, 0.793, 0.375, 0.193) * frac(float4(vertexColor.rgb,1)) ) * 2.0 - 1.0);
	vWaves = SmoothTriangleWave( vWaves ) * vertexColor.a * _TurbulentRange;

	//移动速度0到1
	float translationLerp = sin(_Time * _TranslationSpeed + _Time *  frac(vertexColor)) * 0.5 + 0.5;
	//距离
	float4 newTranslation = vertexColor.a * lerp(-_TranslationDistance + vWaves, _TranslationDistance + vWaves, translationLerp);
	//偏移
	_TranslationOffset *= vertexColor.a;

	//在世界空间操作，避免Batch后自身坐标位置就没了
	float4x4 translationMatrix = 
	{
		float4(1,0,0,newTranslation.x + _TranslationOffset.x + sinWave.x),
		float4(0,1,0,newTranslation.y + _TranslationOffset.y + sinWave.y),
		float4(0,0,1,newTranslation.z + _TranslationOffset.z + sinWave.z),
		float4(0,0,0,1)
	};
	float4 mdlPos = mul(unity_ObjectToWorld, vertex);
	mdlPos = mul(translationMatrix, mdlPos);
	return mdlPos;
}

//顶点旋转
float4x4 RotationMatrix(float4 axisAngle/*,float4 color*/)
{
	//float speed = color.b * 2.0 - 1.0;
	//axisAngle *= _Time.y * speed;
	
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

//绕Y轴旋转
inline float3 RotateAroundYInDegrees (float3 dir, float degrees)
{
	float alpha = degrees * UNITY_PI / 180.0;
	float sina, cosa;
	sincos(alpha, sina, cosa);
	float2x2 m = float2x2(cosa, -sina, sina, cosa);
	return float3(mul(m, dir.xz), dir.y).xzy;
}

//求百分比
inline float Percent01(float minValue, float maxValue, float currentValue)
{
	currentValue = clamp(currentValue, minValue, maxValue);
	float denominator = max(maxValue - minValue, 1.192092896e-07F);
	return (currentValue - minValue) / denominator;
}

//暂时无用
inline fixed3 Remap(fixed3 origionColor,fixed start,fixed end)
{
	fixed3 col = origionColor;
	col.r = lerp(start,end,origionColor.r);
	col.g = lerp(start,end,origionColor.g);
	col.b = lerp(start,end,origionColor.b);
	return col;
}



//高度雾全局参数(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float4 ZL_AltitudeFogParams;
float4 ZL_AltitudeFogColor;

//高度雾(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
#define ALTITUDE_FOG_COORDS(idx) UNITY_FOG_COORDS_PACKED(idx, float4)

// MipFogTexture 
sampler2D ZL_FogTexture;

// offset 顺时针转为正，顺时针转90度 = 0.25
half2 WorldDirToSpherical(half3 dir, half xOffset)
{
	//Vecto3.left -> 0.0, 0.5
	//Vecto3.forward -> 0.25, 0.5
	//Vecto3.right -> 0.5, 0.5
	//Vecto3.back -> 0.75, 0.5

	half2 uv =  half2( UNITY_INV_TWO_PI * atan2(-dir.z, dir.x) ,  UNITY_INV_PI *acos(dir.y)) ;
	uv.x += xOffset;
	return uv;
}
	
//高度雾计算(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float4 ZL_TRANSFER_FOG(float z, float3 worldPos, float4 fogParams)
{
//	#if ZL_ALTITUDE_FOG_ON

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
	

	//#else
	#if ZL_MIPFOG_ON
		half3 viewDir = normalize( UnityWorldSpaceViewDir(worldPos));
		return  float4(z, viewDir);
	#else
		return  float4(z, c1, c2, fDotV);
	#endif
	//#endif 
}



//高度雾计算(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float3 ZL_APPLY_FOG_COLOR( float3 col, float4 fogCoord, float4 fogParams)
{
	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
		UNITY_CALC_FOG_FACTOR(fogCoord.x);

		#if defined(SHADER_API_GLES) ||  !defined(ZL_MIPFOG_ON)
			col.rgb = lerp((unity_FogColor).rgb, (col).rgb, saturate( unityFogFactor) );
		#else

			// NaughtyDog Mipfog
			float mipLevel = unityFogFactor * 6; //128的Fog贴图，用6 较为合适，为减少计算直接硬编码
			half3 fogColor = tex2Dlod(ZL_FogTexture, half4(WorldDirToSpherical( fogCoord.yzw, 0.75), 0,  mipLevel));

			fogColor = lerp(fogColor, unity_FogColor, saturate( 2 *(unityFogFactor - 1) + unity_FogColor.a * 3 )  );
			col.rgb = lerp(fogColor.rgb, col.rgb, saturate( unityFogFactor)  );
			
			//col.rg = WorldDirToSpherical( fogCoord.yzw, 0.75);
		#endif

	#endif

	#if defined(ZL_MIPFOG_ON)
		return col;
	#else
		float c1 = fogCoord.y;
		float c2 = fogCoord.z;
		float fDotV = fogCoord.w;

		float g = min( c2, 0);
		g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

		float f = saturate ( exp2(g) );
		col.rgb = lerp(ZL_AltitudeFogColor.rgb, col.rgb,  f );
		return col;
	#endif
}

//高度雾计算(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float3 ZL_APPLY_SIMPLE_FOG_COLOR( float3 col, float4 fogCoord, float4 fogParams)
{
	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
		UNITY_CALC_FOG_FACTOR(fogCoord.x);

		col.rgb = lerp((unity_FogColor).rgb, (col).rgb, saturate( unityFogFactor) );
		return col;
	#else

		return col;
	#endif
}
//高度雾计算(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float ZL_FOG_PRECENT( float3 col, float4 fogCoord, float4 fogParams)
{
	float result = 1;
	#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
		UNITY_CALC_FOG_FACTOR(fogCoord.x);
		result = saturate( unityFogFactor);
	#endif

	//#if ZL_ALTITUDE_FOG_ON 

		float c1 = fogCoord.y;
		float c2 = fogCoord.z;
		float fDotV = fogCoord.w;

		float g = min( c2, 0);
		g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

		float f = saturate ( exp2(g) );
		result *= f;
	//#endif

	return result;
}

//高度雾计算(来自WokInclude.cginc，统一关键字和全局变量为ZL_XXXX)
float ZL_CALC_FOG_FACTOR( float4 fogCoord, float4 fogParams)
{
	//#if ZL_ALTITUDE_FOG_ON 
	// half space fog

	float c1 = fogCoord.y;
	float c2 = fogCoord.z;
	float fDotV = fogCoord.w;

	float g = min( c2, 0);
	g = (fogParams.z * 0.5 * fogCoord.x) * (( c1 - g * g /abs(fDotV)));

	return saturate( exp2(g) );
//#else 
//	return 1;
//#endif
}

inline half ZL_DEPTH_IN_ALPHA(float4 vertex)
{
	float3 posInCam = mul(UNITY_MATRIX_MV, vertex); 
	return saturate( -posInCam.z / 50  ); // 暂时现成定值，最大50米
}


float4 ZL_WORLD_POS(float4 vertex)
{
	half4 worldPos = half4(mul(UNITY_MATRIX_M, vertex).xyz, ZL_DEPTH_IN_ALPHA(vertex));
	
	return worldPos;
}




#endif