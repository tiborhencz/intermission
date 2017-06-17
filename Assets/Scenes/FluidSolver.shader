Shader "Compute/FluidSolver"
{
	CGINCLUDE
	#include "UnityCG.cginc"
	#pragma vertex vert
	#define UV float3(i.uv, _Layer)

	v2f_img vert(appdata_img v)
	{
		v2f_img o;
		o.pos = float4(v.vertex.xy * 2, 0, v.vertex.w);
		o.uv = v.texcoord;
		return o;
	}

	sampler3D	_Buffer;
	float4		_Buffer_TexelSize;
	sampler3D	_Buffer2;
	float4		_Buffer2_TexelSize;
	float		_Scale;
	half2		_Offset;
	float		_Step;
	float		_InverseCellSize;
	float		_Dissipation;
	float		_PoissonAlphaCoefficient;
	float		_InversePoissonBetaCoefficient;
	float4		_Force;
	fixed4		_InjectColor;
	float2		_InjectPosition;
	float		_Layer;

	float3 sampleVelocity(sampler3D tex, float3 uv)
	{
		float3 offset = 0;
		float3 scale = 1;
		if (uv.x < 0)
		{
			offset.x = 1;
			scale.x = -1;
		}
		else if (uv.x > 1)
		{
			offset.x = -1;
			scale.x = -1;
		}
		if (uv.y < 0)
		{
			offset.y = 1;
			scale.y = -1;
		}
		else if (uv.y > 1)
		{
			offset.y = -1;
			scale.y = -1;
		}
		if (uv.z < 0)
		{
			offset.z = 1;
			scale.z = -1;
		}
		else if (uv.z > 1)
		{
			offset.z = -1;
			scale.z = -1;
		}
		return scale * tex3D(tex, uv + offset * _Buffer_TexelSize).xyz;
	}

	float samplePressure(sampler3D tex, float3 uv)
	{
		float3 offset = 0;
		if (uv.x < 0)
		{
			offset.x = 1;
		}
		else if (uv.x > 1)
		{
			offset.x = -1;
		}
		if (uv.y < 0)
		{
			offset.y = 1;
		}
		else if (uv.y > 1)
		{
			offset.y = -1;
		}
		if (uv.z < 0)
		{
			offset.z = 1;
		}
		else if (uv.z > 1)
		{
			offset.z = -1;
		}
		return tex3D(tex, uv + offset * _Buffer_TexelSize).x;
	}

	float4 advect(v2f_img i) : SV_Target
	{
		float3 pos = UV - _Step * _InverseCellSize * tex3D(_Buffer2, UV);
		return _Dissipation * tex3D(_Buffer, pos);
	}

	float divergence(v2f_img i) : SV_Target
	{
		float3 vL, vR, vB, vT, vD, vU;
		vL = sampleVelocity(_Buffer, UV - half3(_Buffer_TexelSize.x, 0, 0));
	  	vR = sampleVelocity(_Buffer, UV + half3(_Buffer_TexelSize.x, 0, 0));
	  	vB = sampleVelocity(_Buffer, UV - half3(0, _Buffer_TexelSize.y, 0));
	  	vT = sampleVelocity(_Buffer, UV + half3(0, _Buffer_TexelSize.y, 0));
	  	vD = sampleVelocity(_Buffer, UV - half3(0, 0, _Buffer_TexelSize.y));
	  	vU = sampleVelocity(_Buffer, UV + half3(0, 0, _Buffer_TexelSize.y));
		return _InverseCellSize * 0.5 * ((vR.x - vL.x) + (vT.y - vB.y) + (vU.z - vD.z));
	}

	float pressure(v2f_img i) : SV_Target
	{
		float L = samplePressure(_Buffer, UV - float3(_Buffer2_TexelSize.x, 0, 0)).x;
		float R = samplePressure(_Buffer, UV + float3(_Buffer2_TexelSize.x, 0, 0)).x;
		float B = samplePressure(_Buffer, UV - float3(0, _Buffer2_TexelSize.y, 0)).x;
		float T = samplePressure(_Buffer, UV + float3(0, _Buffer2_TexelSize.y, 0)).x;
		float D = samplePressure(_Buffer, UV - float3(0, 0, _Buffer2_TexelSize.y)).x;
		float U = samplePressure(_Buffer, UV + float3(0, 0, _Buffer2_TexelSize.y)).x;

		float bC = tex3D(_Buffer2, UV).x;


		return (L + R + B + T + D + U + _PoissonAlphaCoefficient * bC) / 6;
	}

	float3 gradient(v2f_img i) : SV_Target
	{
		float pL, pR, pB, pT, pD, pU;
	  	pL = samplePressure(_Buffer2, UV - half3(_Buffer_TexelSize.x, 0, 0)); 
		pR = samplePressure(_Buffer2, UV + half3(_Buffer_TexelSize.x, 0, 0));
		pB = samplePressure(_Buffer2, UV - half3(0, _Buffer_TexelSize.y, 0));
		pT = samplePressure(_Buffer2, UV + half3(0, _Buffer_TexelSize.y, 0));
		pD = samplePressure(_Buffer2, UV - half3(0, 0, _Buffer_TexelSize.y));
		pU = samplePressure(_Buffer2, UV + half3(0, 0, _Buffer_TexelSize.y));
		float3 grad = float3(pR - pL, pT - pB, pU - pD) * _InverseCellSize * 0.5;
		float3 uNew = tex3D(_Buffer, UV).xyz;
		uNew -= grad;
		return uNew;
	}

	float3 applyForce(v2f_img i) : SV_Target
	{
		float3 velocity = tex3D(_Buffer, UV).xyz;
		if (distance(UV, float3(_Force.xy, 0.5)) < 0.1)
		{
			velocity = float3(_Force.zw, 0);
		}
		return velocity;
	}

	fixed4 injectColor(v2f_img i) : SV_Target
	{
		fixed4 col = tex3D(_Buffer, UV);
		if (distance(UV, float3(_InjectPosition.xy, 0.5)) < 0.1)
		{
			return _InjectColor;
		}
		else
		{
			return col;
		}
	}

	fixed4 clear(v2f_img i) : SV_Target
	{
		return 0;
	}
	ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		// 0. Advection
		Pass
		{
			CGPROGRAM
			#pragma fragment advect
			ENDCG
		}

		// 1. Divergence
		Pass
		{
			CGPROGRAM
			#pragma fragment divergence
			ENDCG
		}

		// 2. Pressure
		Pass
		{
			CGPROGRAM
			#pragma fragment pressure
			ENDCG
		}

		// 3. Gradient
		Pass
		{
			CGPROGRAM
			#pragma fragment gradient
			ENDCG
		}

		// 4. Apply Force
		Pass
		{
			CGPROGRAM
			#pragma fragment applyForce
			ENDCG
		}

		// 5. Inject Color
		Pass
		{
			CGPROGRAM
			#pragma fragment injectColor
			ENDCG
		}

		// 6. Clear
		Pass
		{
			CGPROGRAM
			#pragma fragment clear
			ENDCG
		}
	}
}
