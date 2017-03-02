Shader "Compute/FluidSolver"
{
	CGINCLUDE
	#include "UnityCG.cginc"
	#pragma vertex vert
	#define UV float3(i.uv, 0.5)

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

	float2 sampleVelocity(sampler3D tex, float2 uv)
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
		return scale * tex3D(tex, float3(uv + offset * _Buffer_TexelSize, 0.5)).xy;
	}

	float samplePressure(sampler3D tex, float2 uv)
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
		return tex3D(tex, float3(uv + offset * _Buffer_TexelSize, 0.5)).x;
	}

	float4 advect(v2f_img i) : SV_Target
	{
		float2 pos = i.uv - _Step * _InverseCellSize * tex3D(_Buffer2, float3(i.uv, 0.5));
		return _Dissipation * tex3D(_Buffer, float3(pos, 0.5));
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

		float bC = tex3D(_Buffer2, float3(i.uv, 0.5)).x;

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
		float2 uNew = tex3D(_Buffer, float3(i.uv, 0.5)).xy;
		uNew -= grad;
		return uNew;
	}

	float2 applyForce(v2f_img i) : SV_Target
	{
		float2 velocity = tex3D(_Buffer, float3(i.uv, 0.5)).xy;
		if (distance(i.uv, _Force.xy) < 0.1)
		{
			velocity = _Force.zw;
		}
		return velocity;
	}

	fixed4 injectColor(v2f_img i) : SV_Target
	{
		fixed4 col = tex3D(_Buffer, float3(i.uv, 0.5));
		if (distance(i.uv, _InjectPosition.xy) < 0.1)
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
