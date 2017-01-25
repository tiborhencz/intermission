using UnityEngine;
using System.Collections;

public class FluidSolver : MonoBehaviour
{
	enum SolverPass
	{
		Boundary = 0,
		Advection = 1,
		PoissonSolver = 2,
		Divergence = 3,
		Gradient = 4,
		ApplyForce = 5
	}

	class Properties
	{
		public static int Buffer = Shader.PropertyToID("_Buffer");
		public static int Buffer2 = Shader.PropertyToID("_Buffer2");
		public static int Scale = Shader.PropertyToID("_Scale");
		public static int Offset = Shader.PropertyToID("_Offset");
		public static int Step = Shader.PropertyToID("_Step");
		public static int InverseCellSize = Shader.PropertyToID("_InverseCellSize");
		public static int Dissipation = Shader.PropertyToID("_Dissipation");
		public static int PoissonAlphaCoefficient = Shader.PropertyToID("_PoissonAlphaCoefficient");
		public static int InversePoissonBetaCoefficient = Shader.PropertyToID("_InversePoissonBetaCoefficient");
		public static int Force = Shader.PropertyToID("_Force");
	}

	public Shader shader;
	public float viscosity = 1f;

	public Material m_FluidSolver;
	private RenderTexture m_VelocityBuffer1;
	private RenderTexture m_VelocityBuffer2;
	private RenderTexture m_DivergenceBuffer;
	private RenderTexture m_TextureBuffer;
	private int m_BufferIndex = 0;
	private float m_GridScale = 1f;

	void Start()
	{
		m_FluidSolver = new Material(shader);
		m_VelocityBuffer1 = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat);
		m_VelocityBuffer2 = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat);
		m_DivergenceBuffer = new RenderTexture(m_VelocityBuffer1.width, m_VelocityBuffer1.height, 0, RenderTextureFormat.ARGBFloat);
		GetComponent<MeshRenderer>().material.mainTexture = m_VelocityBuffer1;
		ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		Graphics.Blit(m_VelocityBuffer2, m_VelocityBuffer1);
	}

	void Blit(SolverPass pass)
	{
		if (m_BufferIndex == 0)
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer1);
			m_FluidSolver.SetTexture(Properties.Buffer2, m_VelocityBuffer1);
			Graphics.Blit(m_VelocityBuffer1, m_VelocityBuffer2, m_FluidSolver, (int)pass);
		}
		else
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer2);
			m_FluidSolver.SetTexture(Properties.Buffer2, m_VelocityBuffer2);
			Graphics.Blit(m_VelocityBuffer2, m_VelocityBuffer1, m_FluidSolver, (int)pass);
		}
		m_BufferIndex = Mathf.Abs(m_BufferIndex - 1);
	}

	void ApplyForce(Vector2 position, Vector2 direction)
	{
		m_FluidSolver.SetVector(Properties.Force, new Vector4(position.x, position.y, direction.x, direction.y));
		Blit(SolverPass.ApplyForce);
		Debug.Log("Blited");
	}

	void Advect(float step, float dissipation)
	{
		m_FluidSolver.SetFloat(Properties.Step, step);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		m_FluidSolver.SetFloat(Properties.Dissipation, dissipation);
		Blit(SolverPass.Advection);
	}

	void Poisson(float step)
	{
		float centerFactor = m_GridScale * m_GridScale / (viscosity * step);
		float stencilFactor = 1.0f / (4.0f + centerFactor);
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, centerFactor);
		m_FluidSolver.SetFloat(Properties.InversePoissonBetaCoefficient, stencilFactor);
		for (int i = 0; i < 20; i++)
			Blit(SolverPass.PoissonSolver);
	}

	void Divergence(float step)
	{
		if (m_BufferIndex == 0)
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer2);
		}
		else
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer1);
		}
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_VelocityBuffer1, m_DivergenceBuffer, m_FluidSolver, (int)SolverPass.Divergence);
		/*
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale);
		m_FluidSolver.SetFloat(Properties.InversePoissonBetaCoefficient, 0.25);
		for (int i = 0; i < 20; i++)
		{
			m_FluidSolver.SetTexture(Properties.Buffer2, m_VelocityBuffer2);
			Blit(SolverPass.PoissonSolver);
		}*/
	}

	void Update()
	{
		Advect(Time.deltaTime, 1f);
		Poisson(Time.deltaTime);

		if (Input.GetMouseButton(0))
		{
			ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		}
		Divergence(Time.deltaTime);

	}
}
