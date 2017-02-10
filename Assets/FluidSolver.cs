using UnityEngine;
using System.Collections;

public class FluidSolver : MonoBehaviour
{
	enum SolverPass
	{
		Boundary = 0,
		Advection = 1,
		Divergence = 2,
		Pressure = 3,
		Gradient = 4,
		ApplyForce = 5,
		InjectColor = 6
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
		public static int Force = Shader.PropertyToID("_Force");
		public static int InjectColor = Shader.PropertyToID("_InjectColor");
		public static int InjectPosition = Shader.PropertyToID("_InjectPosition");
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
		//m_VelocityBuffer.filterMode = FilterMode.Point;
		m_DivergenceBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_PressureBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_ColorBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
		GetComponent<MeshRenderer>().material.mainTexture = m_VelocityBuffer;
		GetComponent<MeshRenderer>().material.mainTexture = m_ColorBuffer;
		ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer);
		Graphics.Blit(Texture2D.blackTexture, m_ColorBuffer);
	}

	void ApplyForce(Vector2 position, Vector2 direction)
	{
		m_FluidSolver.SetVector(Properties.Force, new Vector4(position.x, position.y, direction.x, direction.y));
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer, m_FluidSolver, (int)SolverPass.ApplyForce);
		Debug.Log("Blited" + position);
	}

	void InjectColor(Vector2 position, Color color)
	{
		m_FluidSolver.SetVector(Properties.InjectPosition, new Vector4(position.x, position.y, 0, 0));
		m_FluidSolver.SetColor(Properties.InjectColor, color);
		m_FluidSolver.SetTexture(Properties.Buffer, m_ColorBuffer);
		Graphics.Blit(m_ColorBuffer, m_ColorBuffer, m_FluidSolver, (int)SolverPass.InjectColor);
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

	void Divergence(float step)
	{
		m_FluidSolver.SetTexture(Properties.Buffer, m_VelocityBuffer);
		m_FluidSolver.SetFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Graphics.Blit(m_VelocityBuffer, m_DivergenceBuffer, m_FluidSolver, (int)SolverPass.Divergence);
	}

	void Pressure()
	{
		m_FluidSolver.SetFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale * viscosity);
		m_FluidSolver.SetTexture(Properties.Buffer2, m_DivergenceBuffer);
		m_FluidSolver.SetTexture(Properties.Buffer, m_PressureBuffer);
		for (int i = 0; i < 40; i++)
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

		if (Input.GetMouseButton(0))// && Time.frameCount % 2 == 0)
		{
			Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
			Vector2 pos = new Vector2(Input.mousePosition.x / (float)Screen.width, Input.mousePosition.y / (float)Screen.height);
			ApplyForce(pos,
				//
				mouseDelta / 20);
			InjectColor(pos, new Color(Time.time % 1f, (Time.time + 0.5f) % 1f, (Time.time + 0.25f) % 1f));
		}
		lastMousePosition = Input.mousePosition;

		Divergence(Time.deltaTime);
		Pressure();
		Gradient();
		Advect(Time.deltaTime, 1f, m_ColorBuffer);
	}

	void _OnGUI()
	{
		Graphics.DrawTexture(new Rect(000, 0, 100, 100), m_VelocityBuffer);
		Graphics.DrawTexture(new Rect(100, 0, 100, 100), m_PressureBuffer);
		Graphics.DrawTexture(new Rect(200, 0, 100, 100), m_DivergenceBuffer);
	}
}
