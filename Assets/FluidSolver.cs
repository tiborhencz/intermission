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
	private RenderTexture m_VelocityBuffer;
	private RenderTexture m_DivergenceBuffer;
	private RenderTexture m_PressureBuffer;
	private int m_BufferIndex = 0;
	private float m_GridScale = 1f;

	void Start()
	{
		m_FluidSolver = new Material(shader);
		int width = 128;
		int height = 128;
		m_VelocityBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_DivergenceBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_PressureBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		GetComponent<MeshRenderer>().material.mainTexture = m_VelocityBuffer;
		ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer);
	}

	void Blit(SolverPass pass)
	{
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer, m_FluidSolver, (int)pass);
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

	void Poisson(float step, int iterations)
	{
		float centerFactor = m_GridScale * m_GridScale / (viscosity * step);
		float stencilFactor = 1.0f / (4.0f + centerFactor);
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, centerFactor);
		m_FluidSolver.SetFloat(Properties.InversePoissonBetaCoefficient, stencilFactor);
		for (int i = 0; i < iterations; i++)
			Blit(SolverPass.PoissonSolver);
	}

	void Divergence(float step)
	{
		if (m_BufferIndex == 0)
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		}
		else
		{
			m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		}
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_VelocityBuffer, m_DivergenceBuffer, m_FluidSolver, (int)SolverPass.Divergence);

		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale);
		m_FluidSolver.SetFloat(Properties.InversePoissonBetaCoefficient, 0.25f);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_DivergenceBuffer);
		for (int i = 0; i < 20; i++)
		{
			Graphics.Blit(m_PressureBuffer, m_PressureBuffer, m_FluidSolver, (int)SolverPass.PoissonSolver);
		}
	}

	void Gradient()
	{
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_PressureBuffer);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_PressureBuffer, m_VelocityBuffer, m_FluidSolver, (int)SolverPass.Gradient);
	}

	void Update()
	{
		Advect(Time.deltaTime, 1f);
		Poisson(Time.deltaTime, 0);

		if (Input.GetMouseButton(0))
		{
			ApplyForce(new Vector2(Input.mousePosition.x / (float)Screen.width, Input.mousePosition.y / (float)Screen.height), new Vector2(1f, 1f));
		}
		Divergence(Time.deltaTime);
		Gradient();
	}

	void OnGUI()
	{
		//Graphics.DrawTexture(new Rect(000, 0, 100, 100), m_VelocityBuffer);
		//Graphics.DrawTexture(new Rect(100, 0, 100, 100), m_PressureBuffer);
		Graphics.DrawTexture(new Rect(200, 0, 100, 100), m_DivergenceBuffer);
	}
}
