using UnityEngine;
using System.Collections;

namespace Intermission
{
	public class ParticleSystem : MonoBehaviour
	{
		public Shader particleShader;
		public uint numberOfParticles;

		private Material m_Material;
		public RenderTexture m_ParticleBuffer;

		void Start()
		{
			//m_Material = new Material(particleShader);
			uint v = numberOfParticles;
			uint dim = uint.MaxValue;
			for (uint i = 1; i < 4096; i *= 2)
			{
				if (i * i > v)
				{
					dim = i;
					break;
				}
			}
			if (dim == uint.MaxValue)
			{
				Debug.Log("numberOfParticles must be between 1 and " + (4096 * 4096));
				return;
			}
			Debug.Log("dim:" + dim);
			m_ParticleBuffer = new RenderTexture((int)dim, (int)dim, 0, RenderTextureFormat.ARGB32);
		}

		void Destroy()
		{
			Destroy(m_ParticleBuffer);
		}
	}
}
