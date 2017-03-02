using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class FluidSolver : MonoBehaviour
{
	enum SolverPass
	{
		Advection = 0,
		Divergence = 1,
		Pressure = 2,
		Gradient = 3,
		ApplyForce = 4,
		InjectColor = 5,
		Clear = 6
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
		public static int DepthPosition = Shader.PropertyToID("_DepthPosition");
	}

	public Shader shader;
	public float viscosity = 1f;
	[Range(1, 50)]
	public int iterations = 20;
	public Mesh quadMesh;

	public Material m_FluidSolver;
	public Texture ColorTexture;
	public int width = 32;
	public int height = 32;
	public int depth = 32;
	public bool simulate = true;

	private RenderTexture m_VelocityBuffer;
	private RenderTexture m_DivergenceBuffer;
	private RenderTexture m_PressureBuffer;
	private RenderTexture m_ColorBuffer;
	private RenderTexture m_TempTexture;
	private float m_GridScale = 1f;
	private CommandBuffer m_CommandBuffer;
	public Material m_AMT;

	void Start()
	{
		m_FluidSolver = new Material(shader);
		m_VelocityBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_VelocityBuffer.volumeDepth = depth;
		m_VelocityBuffer.filterMode = FilterMode.Bilinear;
		m_VelocityBuffer.isVolume = true;
		Debug.Log(m_VelocityBuffer.dimension);
		//m_VelocityBuffer.filterMode = FilterMode.Point;
		m_DivergenceBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_DivergenceBuffer.volumeDepth = depth;
		m_DivergenceBuffer.isVolume = true;
		m_PressureBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_PressureBuffer.volumeDepth = depth;
		m_PressureBuffer.isVolume = true;
		m_ColorBuffer = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		m_ColorBuffer.volumeDepth = depth;
		m_ColorBuffer.isVolume = true;
		m_TempTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
		//GetComponent<MeshRenderer>().material.mainTexture = m_VelocityBuffer;
		GetComponent<MeshRenderer>().material.mainTexture = m_ColorBuffer;
		//GetComponent<MeshRenderer>().material.mainTexture = m_PressureBuffer;
		//ApplyForce(new Vector2(0.5f, 0.5f), new Vector2(-1f, 1f));
		Graphics.Blit(m_VelocityBuffer, m_VelocityBuffer);
		m_CommandBuffer = new CommandBuffer();
		//Blit(m_ColorBuffer, SolverPass.Clear);

		Camera.main.AddCommandBuffer(CameraEvent.AfterImageEffects, m_CommandBuffer);
	}

	void Blit(RenderTexture dest, SolverPass pass)
	{
		for (int i = 0; i < depth; i++)
		{
			m_CommandBuffer.SetRenderTarget(dest, 0, CubemapFace.Unknown, i);
			m_CommandBuffer.SetGlobalFloat("_Layer", (i + 0.5f) / 32f);
			m_CommandBuffer.DrawMesh(quadMesh, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 2f), m_FluidSolver, 0, (int)pass);
		}
		Debug.Log("BLITED");
	}

	void ApplyForce(Vector2 position, Vector2 direction)
	{
		m_CommandBuffer.SetGlobalVector(Properties.Force, new Vector4(position.x, position.y, direction.x, direction.y));
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer);
		Blit(m_VelocityBuffer, SolverPass.ApplyForce);
	}

	void InjectColor(Vector2 position, Color color)
	{
		m_CommandBuffer.SetGlobalVector(Properties.InjectPosition, new Vector4(position.x, position.y, 0, 0));
		m_CommandBuffer.SetGlobalColor(Properties.InjectColor, color);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_ColorBuffer);
		Blit(m_ColorBuffer, SolverPass.InjectColor);
	}

	void Advect(float step, float dissipation, RenderTexture target)
	{
		m_CommandBuffer.SetGlobalFloat(Properties.Step, step);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		m_CommandBuffer.SetGlobalFloat(Properties.Dissipation, dissipation);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, target);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_VelocityBuffer);
		Blit(target, SolverPass.Advection);
	}

	void Divergence(float step)
	{
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Blit(m_DivergenceBuffer, SolverPass.Divergence);
	}

	void Pressure()
	{
		m_CommandBuffer.SetGlobalFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale * viscosity);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_DivergenceBuffer);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_PressureBuffer);
		for (int i = 0; i < iterations; i++)
		{
			Blit(m_PressureBuffer, SolverPass.Pressure);
		}
	}

	void Gradient()
	{
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_PressureBuffer);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Blit(m_VelocityBuffer, SolverPass.Gradient);
	}

	Vector2 lastMousePosition;
	void LateUpdate()
	{
		m_CommandBuffer.Clear();
		if (Input.GetKeyDown(KeyCode.Space))
			simulate = !simulate;
		if (!simulate)
			return;
		Advect(Time.deltaTime, 0.999f, m_VelocityBuffer);
		if (Input.GetMouseButton(0))// && Time.frameCount % 2 == 0)
		{
			Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
			Vector2 pos = new Vector2(Input.mousePosition.x / (float)Screen.width, Input.mousePosition.y / (float)Screen.height);
			ApplyForce(pos, mouseDelta / 20);
			InjectColor(pos, new Color(Time.time % 1f, (Time.time + 0.5f) % 1f, (Time.time + 0.25f) % 1f));
			//m_CommandBuffer = new CommandBuffer();
			//m_FluidSolver.SetTexture("_Buffer1", ColorTexture);
			//Blit(m_ColorBuffer, SolverPass.Divergence);
			//Graphics.ExecuteCommandBuffer(m_CommandBuffer);
		}


		lastMousePosition = Input.mousePosition;

		Divergence(Time.deltaTime);

		Pressure();
		Gradient();
		Advect(Time.deltaTime, 0.999f, m_ColorBuffer);
	}

	void _OnGUI()
	{
		Graphics.DrawTexture(new Rect(000, 0, 100, 100), m_VelocityBuffer);
		Graphics.DrawTexture(new Rect(100, 0, 100, 100), m_PressureBuffer);
		Graphics.DrawTexture(new Rect(200, 0, 100, 100), m_DivergenceBuffer);
	}
}
