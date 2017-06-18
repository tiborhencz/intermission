Shader "Hidden/VolumeRenderer"
{
	HLSLINCLUDE
	#include "UnityCG.cginc"

	struct Input
	{
		float4 vertex : POSITION;
	};

	struct Varyings
	{
		float4 pos : SV_POSITION;
		float4 vertex : TEXCOORD0;
		float4 screenPos : TEXCOORD1;
		float3 worldPos : NORMAL;
	};

	float scale;

	float _CutLimitMin;
	float _CutLimitMax;
	int _CutAxis;

	Varyings vertex(in Input input)
	{
		Varyings output;
		float s = _CutLimitMax - _CutLimitMin;
		float offset = (1. - s) * 0.5 - (1. - _CutLimitMax);
		if (_CutAxis == 0)
		{
			input.vertex.x = s * input.vertex.x + offset;
		}
		else if (_CutAxis == 1)
		{
			input.vertex.y = s * input.vertex.y + offset;
		}
		else
		{
			input.vertex.z = s * input.vertex.z + offset;
		}
		output.pos = UnityObjectToClipPos(input.vertex);
		output.vertex = input.vertex;
		output.screenPos = ComputeScreenPos(output.pos);
		output.worldPos = mul(unity_ObjectToWorld, float4(input.vertex.xyz, 1.)).xyz;
		return output;
	}

	fixed4 frag_raypos(in Varyings input) : SV_Target
	{
		return input.vertex + 0.5;
	}

	Texture2D<float3> _Front;
	SamplerState _Sampler_Front;
	Texture2D<float3> _Back;
	SamplerState _Sampler_Back;

	Texture2D _DensitySpectrum;
	SamplerState _Sampler_DensitySpectrum;
	Texture2D _DensitySpectrumIntensityCurve;
	SamplerState _Sampler_DensitySpectrumIntensityCurve;

	Texture3D _Volume;
	SamplerState _Sampler_Volume;
	float3 _Volume_TexelSize;
	int _Iterations;
	float _DensityAmplification;
	float _High;
	float _Low;
	float _DensitySpectrumOffset;
	float _SpecPower;
	float3 _LightPos;


	float getDensity(in float3 pos)
	{
		return _Volume.SampleLevel(_Sampler_Volume, pos, 0.).r;
	}

	float3 getGradient(in float3 pos)
	{
		float X = getDensity(pos + float3(_Volume_TexelSize.x, 0., 0.)) - getDensity(pos - float3(_Volume_TexelSize.x, 0., 0.));
		float Y = getDensity(pos + float3(0., _Volume_TexelSize.y, 0.)) - getDensity(pos - float3(0., _Volume_TexelSize.y, 0.));
		float Z = getDensity(pos + float3(0., 0., _Volume_TexelSize.z)) - getDensity(pos - float3(0., 0., _Volume_TexelSize.z));
		return normalize(float3(X, Y, Z));
	}

	float calcAO(in float3 pos, in float3 nor)
	{
		float occ = 0.0;
	    float sca = 1.0;
	    for (int i = 0; i < 5; i++)
	    {
	        float hr = 0.06 + 0.1 * float(i) / 5.0;
	        float3 aopos =  nor * -hr + pos;
	        float dd = getDensity(aopos);
	        occ += (saturate(dd - _Low)) * sca;
	        sca *= 0.9;
	    }
	    return saturate(occ);
	}

	float3 isLit(in float3 pos, in float3 n, in float3 lightDir)
	{
		float3 p = pos + n * -.025;
		float light = 1.;
		float st = 0.9;
		for (int i = 0; i < 16; i++, p += lightDir * -0.025)
		{
			float dd = getDensity(p);
			if (dd > _Low)
			{
				light *= (1. - dd*dd);
			}
		}
		return saturate(light);
	}

	fixed4 frag_density(in Varyings input) : SV_Target
	{
		float4 output = 0.;
		float2 uv = input.screenPos.xy / input.screenPos.w;
		float3 front = _Front.SampleLevel(_Sampler_Front, uv, 0.);
		float3 back = _Back.SampleLevel(_Sampler_Back, uv, 0.);
		float rcpIter = rcp(_Iterations);
		float3 dir = back - front;
		float sqrDist = dot(dir, dir);
		dir = normalize(dir) * rcpIter * 1.7320508076; // longest diagonal of a cube is √3
		float3 p = 0;
		float4 firstColor = 0;
		bool set = false;
		int i = 0;
		float3 hitPos = 0.;
		bool hit = false;
		for (int i = 0; i < _Iterations; i++, p += dir)
		{
			fixed sample = getDensity(front + p);
			bool visible = sample.r > _Low && sample.r < _High && dot(p, p) < sqrDist;
			float4 col = _DensitySpectrum.SampleLevel(_Sampler_DensitySpectrum, float2(sample.r + _DensitySpectrumOffset, .5), 0.);
			col = min(col * sample.r * _DensityAmplification, 1.);
			output += (1.0f - output.a) * col * visible;
			if (sample.r > _Low && !hit)
			{
				hitPos = front + p;
				hit = true;
			}
			if (output.a > 0.95)// || i > _Iterations * 0.5 && output.a == 0.)
				break;
		}

		float3 n = getGradient(hitPos);

		float3 lightPos = _LightPos;
		float diffuseTerm = dot(lightPos, n) * .7;

  		float3 lightDir = normalize(lightPos - hitPos);
  		float3 h = normalize(lightDir - normalize(_WorldSpaceCameraPos.xyz - hitPos));
 
  		float specTerm = pow(saturate(dot(h, n)), _SpecPower);

  		float occ = calcAO(hitPos, n);
  		float shadowTerm = isLit(hitPos, n, lightDir);
		output.rgb = saturate(output.rgb + diffuseTerm * 0.5 + specTerm * 0.6);
  		output.rgb -= occ;
  		output.rgb *= 0.6 + 0.4 * shadowTerm;
		return fixed4(output.rgb, output.a );
		//return fixed4(output.rgb * (output.a > 0.01), output.a * _Amplification * rcpIter);
	}

	ENDHLSL

	SubShader
	{
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#pragma exclude_renderers d3d11_9x
			#pragma exclude_renderers d3d9
			#pragma vertex vertex
			#pragma fragment frag_density
			ENDHLSL
			
		}

		Pass
		{
			Cull Front
			HLSLPROGRAM
			#pragma vertex vertex
			#pragma fragment frag_raypos
			ENDHLSL
		}

		Pass
		{
			Cull Back
			HLSLPROGRAM
			#pragma vertex vertex
			#pragma fragment frag_raypos
			ENDHLSL
		}
	}
}
