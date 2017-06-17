using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageEffect : MonoBehaviour
{
	public Shader shader;

	private Material m_Material;
	public Material material
	{
		get
		{
			if (!m_Material)
			{
				m_Material = new Material(shader);
			}
			return m_Material;
		}
	}

	void OnRenderImage(RenderTexture src, RenderTexture dst)
	{
		Graphics.Blit(src, dst, material);
	}
}
