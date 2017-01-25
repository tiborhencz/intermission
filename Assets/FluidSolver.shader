﻿Shader "Compute/FluidSolver"
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
	}

	CGINCLUDE
	#include "UnityCG.cginc"

	sampler2D	_Buffer;
	float4		_Buffer_TexelSize;
	sampler2D	_Buffer2;
	float		_Scale;
	half2		_Offset;
	float		_Step;
	float		_InverseCellSize;
	float		_Dissipation;
	float		_PoissonAlphaCoefficient;
	float		_InversePoissonBetaCoefficient;
	float4		_Force;
	
	fixed4 boundary(v2f_img i) : SV_Target
	{
		return _Scale * tex2D(_Buffer, i.uv + _Offset);
	}

	float4 f4texRECTbilerp(sampler2D tex, float2 s)
	{
		float4 st;
		st.xy = floor(s - 0.5 * _Buffer_TexelSize.xy) + 0.5 * _Buffer_TexelSize.xy;
		st.zw = st.xy + _Buffer_TexelSize.xy;

		float2 t = s - st.xy; //interpolating factors 

		float4 tex11 = tex2D(tex, st.xy);
		float4 tex21 = tex2D(tex, st.zy);
		float4 tex12 = tex2D(tex, st.xw);
		float4 tex22 = tex2D(tex, st.zw);

		// bilinear interpolation
		return lerp(lerp(tex11, tex21, t.x), lerp(tex12, tex22, t.x), t.y);
	}

	fixed4 advect(v2f_img i) : SV_Target
	{
		float2 pos = i.uv - _Step * _InverseCellSize * tex2D(_Buffer, i.uv);
		return _Dissipation * tex2D(_Buffer, pos);
	}

	void h4texRECTneighbors(sampler2D tex, half2 s,
                        out half4 left,
                        out half4 right,
                        out half4 bottom,
                        out half4 top)
	{
	  left   = tex2D(tex, s - half2(_Buffer_TexelSize.x, 0)); 
	  right  = tex2D(tex, s + half2(_Buffer_TexelSize.x, 0));
	  bottom = tex2D(tex, s - half2(0, _Buffer_TexelSize.y));
	  top    = tex2D(tex, s + half2(0, _Buffer_TexelSize.y));
	}


	fixed4 divergence(v2f_img i) : SV_Target
	{
		half4 vL, vR, vB, vT;
		h4texRECTneighbors(_Buffer, i.uv, vL, vR, vB, vT);
		return _InverseCellSize * 0.5 * (vR.x - vL.x + vT.y - vB.y);
	}

	fixed4 poissonSolver(v2f_img i) : SV_Target
	{
		//x = _Buffer (Ax = b)
		half4 xL, xR, xB, xT;
		h4texRECTneighbors(_Buffer, i.uv, xL, xR, xB, xT);
		half4 bC = tex2D(_Buffer2, i.uv);
		return (xL + xR + xB + xT + _PoissonAlphaCoefficient * bC) * _InversePoissonBetaCoefficient;
	}

	fixed4 gradient(v2f_img i) : SV_Target
	{
		//pressure = _Buffer2
		half4 pL, pR, pB, pT;
		h4texRECTneighbors(_Buffer2, i.uv, pL, pR, pB, pT);
		half2 grad = half2(pR.x - pL.x, pT.x - pB.x) * _InverseCellSize * 0.5;
		fixed4 uNew = tex2D(_Buffer, i.uv);
		uNew.xy -= grad;
		return uNew;
	}

	fixed4 applyForce(v2f_img i) : SV_Target
	{
		fixed4 velocity = tex2D(_Buffer, i.uv);
		if (distance(i.uv, _Force.xy) < 0.1)
			velocity.xy = _Force.zw;
		return velocity;
	}

	ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		// 0. Boundary - Set velocity to 0 at the boundaries
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment boundary
			ENDCG
		}

		// 1. Advection
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment advect
			ENDCG
		}

		// 2. Poisson Solver (Viscosity)
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment poissonSolver
			ENDCG
		}

		// 3. Divergence
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment divergence
			ENDCG
		}

		// 4. Gradient
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment gradient
			ENDCG
		}

		// 5. Apply Force
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment applyForce
			ENDCG
		}
	}
}
