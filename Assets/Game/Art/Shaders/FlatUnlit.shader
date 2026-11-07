Shader "FlashBeat/FlatUnlit" {
	Properties {
		_MainTex("Main Texture", 2D) = "white" { }
		_Color("Tint", Color) = (1, 1, 1, 1)
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
		[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
	}

	SubShader {
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

		Pass {
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			Cull [_Cull]
			Lighting Off
			Fog { Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex   : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv  : TEXCOORD0;
			};

			uniform sampler2D _MainTex;
			uniform fixed4 _MainTex_ST;
			uniform fixed4 _Color;

			v2f vert(appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				return o;
			}

			fixed4 frag(v2f v) : SV_Target {
				return tex2D(_MainTex, v.uv) * _Color;
			}

			ENDCG
		}
	}
}
