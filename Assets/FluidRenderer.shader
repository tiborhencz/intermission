Shader "Unlit/FluidRenderer"
{
	Properties
	{
		_MainTex ("Texture", 3D) = "white" {}
		_DepthPosition ("Depth Position", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma multi_compile instancing
			#include "UnityCG.cginc"

			sampler3D _MainTex;
			float4 _MainTex_ST;
			float _DepthPosition;
			
			fixed4 frag (v2f_img i) : SV_Target
			{
				fixed4 col = 0;
				for (float z = 0; z < 1.0; z += 0.1)
				{
					//col += tex3D(_MainTex, float3(z, i.uv.yx));
					col += tex3D(_MainTex, float3(i.uv.xy, z));
				}
				return col;
				//return tex3D(_MainTex, float3(i.uv, _DepthPosition));
			}
			ENDCG
		}
	}
}
