﻿
/*
********注意，直接使用UsePass "Hidden/MetaPass/META"会导致客户端变紫，编辑器中一切正常，原因不明******
*/

Shader "Hidden/MetaPass"
{
	CGINCLUDE
		#include "UnityStandardMeta.cginc"

		float _ColorBleedInRender = 0;

		v2f_meta vert_meta2 (VertexInput v)
		{
			v2f_meta o;
			o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy, unity_LightmapST, unity_DynamicLightmapST);
			o.uv = TexCoords(v);
			return o;
		}

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
	
	SubShader
	{
		Pass {
			Name "Meta"
			Tags { "Queue"="Geometry" "LightMode" = "Meta" "RenderType"="Opaque"}
			//Cull Off Lighting Off ZWrite On 

			CGPROGRAM
				#pragma vertex vert_meta
				#pragma fragment frag_meta2
			ENDCG
		}
	}
}
