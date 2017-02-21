Shader "Compute/FluidSolver"
{
	Properties
	{
		_Buffer ("Vector Field Buffer", 2D) = "black" {}
		_Buffer2 ("Vector Field Buffer 2", 2D) = "black" {}
		_Scale ("Scale", Float) = 1
		_Offset ("Offset", Vector) = (0, 0, 0, 0)
		_Step ("Step", Float) = 0
		_InverseCellSize ("Inverse Cell Size", Float) = 0
		_Dissipation ("Advection Dissipation", Float) = 1
		_PoissonAlphaCoefficient ("Poisson Alpha Coefficient", Float) = 1
		_InversePoissonBetaCoefficient ("Inverse Poisson Beta Coefficient", Float) = 1
		_Force ("Force", Vector) = (0, 0, 0, 0)
		_InjectColor ("Inject Color", Color) = (1, 1, 1, 1)
		_InjectPosition ("Inject Position", Vector) = (0, 0, 0, 0)
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D	_Buffer;
	float4		_Buffer_TexelSize;
	sampler2D	_Buffer2;
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

	float2 sampleVelocity(sampler2D tex, float2 uv)
	{
		float2 offset = 0;
		float2 scale = 1;
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
		return scale * tex2D(tex, uv + offset * _Buffer_TexelSize).xy;
	}

	float samplePressure(sampler2D tex, float2 uv)
	{
		float2 offset = 0;
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
		return tex2D(tex, uv + offset * _Buffer_TexelSize).x;
	}

	float4 advect(v2f_img i) : SV_Target
	{
		float2 pos = i.uv - _Step * _InverseCellSize * tex2D(_Buffer2, i.uv);
		return _Dissipation * tex2D(_Buffer, pos);
	}

	float divergence(v2f_img i) : SV_Target
	{
		float2 vL, vR, vB, vT;
		vL = sampleVelocity(_Buffer, i.uv - half2(_Buffer_TexelSize.x, 0)); 
	  	vR = sampleVelocity(_Buffer, i.uv + half2(_Buffer_TexelSize.x, 0));
	  	vB = sampleVelocity(_Buffer, i.uv - half2(0, _Buffer_TexelSize.y));
	  	vT = sampleVelocity(_Buffer, i.uv + half2(0, _Buffer_TexelSize.y));
		return _InverseCellSize * 0.5 * ((vR.x - vL.x) + (vT.y - vB.y));
	}

	float pressure(v2f_img i) : SV_Target
	{
		float L = samplePressure(_Buffer, i.uv - float2(_Buffer2_TexelSize.x, 0)).x;
		float R = samplePressure(_Buffer, i.uv + float2(_Buffer2_TexelSize.x, 0)).x;
		float B = samplePressure(_Buffer, i.uv - float2(0, _Buffer2_TexelSize.y)).x;
		float T = samplePressure(_Buffer, i.uv + float2(0, _Buffer2_TexelSize.y)).x;

		float bC = tex2D(_Buffer2, i.uv).x;

		return (L + R + B + T + _PoissonAlphaCoefficient * bC) * .25;
	}

	float2 gradient(v2f_img i) : SV_Target
	{
		float pL, pR, pB, pT;
	  	pL = samplePressure(_Buffer2, i.uv - half2(_Buffer_TexelSize.x, 0)); 
		pR = samplePressure(_Buffer2, i.uv + half2(_Buffer_TexelSize.x, 0));
		pB = samplePressure(_Buffer2, i.uv - half2(0, _Buffer_TexelSize.y));
		pT = samplePressure(_Buffer2, i.uv + half2(0, _Buffer_TexelSize.y));
		float2 grad = float2(pR.x - pL.x, pT.x - pB.x) * _InverseCellSize * 0.5;
		float2 uNew = tex2D(_Buffer, i.uv).xy;
		uNew -= grad;
		return uNew;
	}

	float2 applyForce(v2f_img i) : SV_Target
	{
		float2 velocity = tex2D(_Buffer, i.uv).xy;
		if (distance(i.uv, _Force.xy) < 0.05)
		{
			velocity = _Force.zw;
		}
		return velocity;
	}

	fixed4 injectColor(v2f_img i) : SV_Target
	{
		fixed4 col = tex2D(_Buffer, i.uv);
		if (distance(i.uv, _InjectPosition.xy) < 0.05)
		{
			return _InjectColor;
		}
		else
		{
			return col;
		}
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
			#pragma vertex vert_img
			#pragma fragment advect
			ENDCG
		}

		// 1. Divergence
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment divergence
			ENDCG
		}

		// 2. Pressure
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment pressure
			ENDCG
		}

		// 3. Gradient
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment gradient
			ENDCG
		}

		// 4. Apply Force
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment applyForce
			ENDCG
		}

		// 5. Inject Color
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment injectColor
			ENDCG
		}
	}
}
