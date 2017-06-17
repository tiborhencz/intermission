using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeRenderer : MonoBehaviour
{
	
	public enum DrawMode
	{
		Density,
		Back,
		Front
	}

	public enum Axis
	{
		X,
		Y,
		Z
	}

	public DrawMode drawMode;
	public Mesh mesh;
	public Texture volumeTexture;
	[Range(1, 1024)]
	public int renderIterations = 64;
	[Range(0.1f, 5f)]
	public float densityAmplification = 1f;
	[Range(0.000001f, 1f)]
	public float secondaryDensityAmplification = 1f;
	[Range(0, 1)]
	public float cutoff = 0;
	[Range(0, 1)]
	public float treshold = 1;
	[Range(0, 1)]
	public float densitySpectrumOffset = 0;
	public Gradient densitySpectrum;
	public AnimationCurve densitySpectrumIntensityCurve;
	public Axis cutAxis;
	[HideInInspector]
	public float cutLimitMin = 0f;
	[HideInInspector]
	public float cutLimitMax = 1f;
	[Range(0, 100)]
	public float specularPower = 1f;



	private Shader m_VolumeRenderShader;
	public Shader volumeRenderShader
	{
		get
		{
			if (!m_VolumeRenderShader)
			{
				m_VolumeRenderShader = Shader.Find("Hidden/VolumeRenderer");
			}
			return m_VolumeRenderShader;
		}
	}

	private Material m_VolumeRenderMaterial;
	public Material volumeRenderMaterial
	{
		get
		{
			if (!m_VolumeRenderMaterial)
			{
				m_VolumeRenderMaterial = new Material(volumeRenderShader);
			}
			return m_VolumeRenderMaterial;
		}
	}

	[HideInInspector]
	private Texture2D m_DensitySpectrumTexture;

	public void UpdateDensitySpectrum()
	{
		if (!m_DensitySpectrumTexture)
		{
			m_DensitySpectrumTexture = new Texture2D(256, 1);
			m_DensitySpectrumTexture.filterMode = FilterMode.Point;
		}
		Color[] pixels = new Color[256];
		for (int i = 0; i < 256; i++)
		{
			pixels[i] = densitySpectrum.Evaluate(i / 255f);
		}
		m_DensitySpectrumTexture.SetPixels(pixels);
		m_DensitySpectrumTexture.Apply();
	}

	void OnRenderObject()
	{
		if (!m_DensitySpectrumTexture)
			UpdateDensitySpectrum();
		RenderTexture orig = RenderTexture.active;
		RenderTexture front = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
		RenderTexture back = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);

		Graphics.SetRenderTarget(back);
		volumeRenderMaterial.SetPass((int)DrawMode.Back);
		Graphics.DrawMeshNow(mesh, transform.localToWorldMatrix);

		Graphics.SetRenderTarget(front);
		volumeRenderMaterial.SetPass((int)DrawMode.Front);
		Graphics.DrawMeshNow(mesh, transform.localToWorldMatrix);

		Graphics.SetRenderTarget(orig);
		volumeRenderMaterial.SetTexture("_Front", front);
		volumeRenderMaterial.SetTexture("_Back", back);
		volumeRenderMaterial.SetTexture("_Volume", volumeTexture);
		volumeRenderMaterial.SetVector("_Volume_TexelSize", new Vector3(1f / (float)volumeTexture.width, 1f / (float)volumeTexture.height, 1f / 99f)); // figure out simethig
		volumeRenderMaterial.SetInt("_Iterations", renderIterations);
		volumeRenderMaterial.SetTexture("_DensitySpectrum", m_DensitySpectrumTexture);
		volumeRenderMaterial.SetFloat("_DensityAmplification", densityAmplification * secondaryDensityAmplification);
		volumeRenderMaterial.SetFloat("_DensitySpectrumOffset", densitySpectrumOffset);
		volumeRenderMaterial.SetFloat("_Low", cutoff);
		volumeRenderMaterial.SetFloat("_High", cutoff + treshold);
		volumeRenderMaterial.SetInt("_CutAxis", (int)cutAxis);
		volumeRenderMaterial.SetFloat("_CutLimitMin", cutLimitMin);
		volumeRenderMaterial.SetFloat("_CutLimitMax", cutLimitMax);
		volumeRenderMaterial.SetFloat("_SpecPower", specularPower);
		volumeRenderMaterial.SetPass((int)drawMode);
		Graphics.DrawMeshNow(mesh, transform.localToWorldMatrix);

		RenderTexture.ReleaseTemporary(front);
		RenderTexture.ReleaseTemporary(back);
	}
}
