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
	

	float getBoundary(float2 uv)
	{
		return (uv.x > _Buffer_TexelSize.x * 1.5 &&
			uv.x < 1 - _Buffer_TexelSize.x * 1.5 &&
			uv.y > _Buffer_TexelSize.y * 1.5 &&
			uv.y < 1 - _Buffer_TexelSize.y * 1.5);
	}

	fixed4 boundary(v2f_img i) : SV_Target
	{
	    float2 cellOffset = float2(0.0, 0.0);
    	if(i.uv.x < 0.0)
    		cellOffset.x = 1.0;
    	else if(i.uv.x > 1.0)
    		cellOffset.x = -1.0;
    	if(i.uv.y < 0.0)
    		cellOffset.y = 1.0;
    	else if(i.uv.y > 1.0)
    		cellOffset.y = -1.0;
		return _Scale * tex2D(_Buffer, i.uv + _Offset * _Buffer_TexelSize.xy);
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
		float2 pos = i.uv - _Step * _InverseCellSize * tex2D(_Buffer2, i.uv);
		return _Dissipation * tex2D(_Buffer, pos) * getBoundary(i.uv);
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
		half4 div = half4(_InverseCellSize * 0.5 * ((vR.x - vL.x) + (vT.y - vB.y)), 0, 0, 1);
		return div;
	}

	fixed4 poissonSolver(v2f_img i) : SV_Target
	{
		//x = _Buffer; (Ax = b)
		half4 xL, xR, xB, xT;
		h4texRECTneighbors(_Buffer, i.uv, xL, xR, xB, xT);
		half4 bC = tex2D(_Buffer2, i.uv);
		half4 result = (xL + xR + xB + xT + _PoissonAlphaCoefficient * bC) * _InversePoissonBetaCoefficient;
		result.a = 1.0;
		return result;
	}

	float samplePressue(sampler2D pressure, float2 coord, float invresolution)
	{
		return tex2D(pressure, coord).x;
	}

	fixed4 pressure(v2f_img i) : SV_Target
	{
		float L = samplePressue(_Buffer, i.uv - float2(_Buffer2_TexelSize.x, 0), _Buffer2_TexelSize.x);
		float R = samplePressue(_Buffer, i.uv + float2(_Buffer2_TexelSize.x, 0), _Buffer2_TexelSize.x);
		float B = samplePressue(_Buffer, i.uv - float2(0, _Buffer2_TexelSize.y), _Buffer2_TexelSize.y);
		float T = samplePressue(_Buffer, i.uv + float2(0, _Buffer2_TexelSize.y), _Buffer2_TexelSize.y);

		float bC = tex2D(_Buffer2, i.uv).x;

		return float4((L + R + B + T + _PoissonAlphaCoefficient * bC) * .25, 0, 0, 1);
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
		{
		//	if (i.uv.x < _Force.x)
				velocity.xy = _Force.zw;
		//	else
			//velocity.xy = -_Force.zw;
		}
		return velocity;
	}

	fixed4 injectColor(v2f_img i) : SV_Target
	{
		fixed4 col = tex2D(_Buffer, i.uv);
		if (distance(i.uv, _InjectPosition.xy) < 0.1)
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

		// 2. Divergence
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment divergence
			ENDCG
		}

		// 3. Pressure
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment pressure
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

		// 6. Inject Color
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment injectColor
			ENDCG
		}
	}
}
