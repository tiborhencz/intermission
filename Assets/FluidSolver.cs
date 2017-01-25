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
		Pressure = 4,
		Gradient = 5,
		ApplyForce = 6
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
	public Texture ColorTexture;
	private RenderTexture m_VelocityBuffer;
	private RenderTexture m_DivergenceBuffer;
	private RenderTexture m_PressureBuffer;
	private RenderTexture m_ColorBuffer;
	private float m_GridScale = 1f;

	void Start()
	{
		m_FluidSolver = new Material(shader);
		int width = 256;
		int height = 256;
		m_VelocityBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_DivergenceBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_PressureBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_ColorBuffer = new RenderTexture(ColorTexture.width, ColorTexture.height, 0, RenderTextureFormat.ARGBFloat);
		GetComponent<MeshRenderer>().material.mainTexture = m_VelocityBuffer; //m_ColorBuffer;
		ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer);
		Graphics.Blit(ColorTexture, m_ColorBuffer);
	}

	void ApplyForce(Vector2 position, Vector2 direction)
	{
		m_FluidSolver.SetVector(Properties.Force, new Vector4(position.x, position.y, direction.x, direction.y));
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer, m_FluidSolver, (int)SolverPass.ApplyForce);
		Debug.Log("Blited" + position);
	}

	void Advect(float step, float dissipation, RenderTexture target)
	{
		m_FluidSolver.SetFloat(Properties.Step, step);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		m_FluidSolver.SetFloat(Properties.Dissipation, dissipation);
		m_FluidSolver.SetTexture(Properties.Buffer, target);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_VelocityBuffer);
		Graphics.Blit(target, target, m_FluidSolver, (int)SolverPass.Advection);
	}

	void Poisson(float step, int iterations)
	{
		float centerFactor = m_GridScale * m_GridScale / (viscosity * step);
		float stencilFactor = 1.0f / (4.0f + centerFactor);
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, centerFactor);
		m_FluidSolver.SetFloat(Properties.InversePoissonBetaCoefficient, stencilFactor);
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		for (int i = 0; i < iterations; i++)
		{
			Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer, m_FluidSolver, (int)SolverPass.PoissonSolver);
		}
	}

	void Divergence(float step)
	{
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_VelocityBuffer, m_DivergenceBuffer, m_FluidSolver, (int)SolverPass.Divergence);
	}

	void Pressure()
	{
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_DivergenceBuffer);
		for (int i = 0; i < 20; i++)
		{
			//Boundary(m_PressureBuffer, 1);
			Graphics.Blit(m_PressureBuffer, m_PressureBuffer, m_FluidSolver, (int)SolverPass.Pressure);
		}
	}

	void Gradient()
	{
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_PressureBuffer);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_PressureBuffer, m_VelocityBuffer, m_FluidSolver, (int)SolverPass.Gradient);
	}

	void Boundary(RenderTexture tex, float scale)
	{
		m_FluidSolver.SetFloat(Properties.Scale, scale);
		Graphics.Blit(tex, tex, m_FluidSolver, (int)SolverPass.Boundary);
	}

	Vector2 lastMousePosition;
	void Update()
	{
		Advect(Time.deltaTime, 1f, m_VelocityBuffer);

		if (Input.GetMouseButton(0))
		{
			Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
			ApplyForce(new Vector2(Input.mousePosition.x / (float)Screen.width, Input.mousePosition.y / (float)Screen.height),
				//new Vector2(mouseDelta.x / (float)Screen.width, mouseDelta.y / (float)Screen.height));
				new Vector2(0, 1));
		}
		lastMousePosition = Input.mousePosition;

		Divergence(Time.deltaTime);
		Pressure();
		Gradient();
		Advect(Time.deltaTime, 1f, m_ColorBuffer);
	}

	void OnGUI()
	{
		Graphics.DrawTexture(new Rect(000, 0, 100, 100), m_VelocityBuffer);
		Graphics.DrawTexture(new Rect(100, 0, 100, 100), m_PressureBuffer);
		Graphics.DrawTexture(new Rect(200, 0, 100, 100), m_DivergenceBuffer);
	}
}
