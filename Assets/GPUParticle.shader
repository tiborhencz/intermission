Shader "Hidden/GPUParticle"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D _MainTex;

	fixed4 frag_init(v2f_img i) : SV_Target
	{
		fixed4 col = tex2D(_MainTex, i.uv);
		// just invert the colors
		col = 1 - col;
		return col;
	}
	ENDCG

	SubShader
	{
		Pass
		{
			CGPROGRAM
            #pragma target 3.0
			#pragma vertex vert_img
			#pragma fragment frag_init
			ENDCG
		}
	}
}
