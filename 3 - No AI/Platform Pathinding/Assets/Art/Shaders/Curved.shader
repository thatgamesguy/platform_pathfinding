// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Sprites/Curved"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
	_Color("Tint", Color) = (1,1,1,1)
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0
		_Curvature("Curvature", Float) = 0.001
	}

		SubShader
	{
		Tags
	{
		"Queue" = "Transparent"
		"IgnoreProjector" = "True"
		"RenderType" = "Transparent"
		"PreviewType" = "Plane"
		"CanUseSpriteAtlas" = "True"
	}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha

		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ PIXELSNAP_ON
#include "UnityCG.cginc"

	struct appdata_t
	{
		float4 vertex   : POSITION;
		float4 color    : COLOR;
		float2 texcoord : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex   : SV_POSITION;
		fixed4 color : COLOR;
		half2 texcoord  : TEXCOORD0;
	};

	fixed4 _Color;
	uniform float _Curvature;

	v2f vert(appdata_t IN)
	{
		v2f OUT;
		float4 vv = mul(unity_ObjectToWorld, IN.vertex);
		vv.xyz -= _WorldSpaceCameraPos.xyz;

		//Curvature and taper effects are calculated here
		vv = float4(vv.x * (vv.y * _Curvature), (vv.x * vv.x) * -_Curvature, 0.0f, 0.0f);

		//Use this instead if you don't want the taper effect
		//vv = float4( 0.0f, (vv.x * vv.x) * - _Curvature, 0.0f, 0.0f );

		OUT.vertex = UnityObjectToClipPos(IN.vertex) + mul(unity_WorldToObject, vv);
		OUT.texcoord = IN.texcoord;
		OUT.color = IN.color * _Color;
#ifdef PIXELSNAP_ON
		OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif

		return OUT;
	}

	sampler2D _MainTex;

	fixed4 frag(v2f IN) : SV_Target
	{
		fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
	c.rgb *= c.a;
	return c;
	}
		ENDCG
	}
	}
}