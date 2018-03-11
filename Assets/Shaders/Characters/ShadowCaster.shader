﻿Shader "* Character/ShadowCaster"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_AlphaCutout("Alpha Cutout",Range(0,1)) = 0.5
	}

	SubShader
	{
		Tags { "Queue"="Transparent"  "IgnoreProjector"="True" "RenderType"="Transparent"}

		Pass {
			Name "Caster"
			Tags { "LightMode" = "ShadowCaster" }
			Cull Off
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _AlphaCutout;

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
				fixed4 texcol = tex2D( _MainTex, i.uv );
				clip( texcol.a - _AlphaCutout);
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}

	}
}
