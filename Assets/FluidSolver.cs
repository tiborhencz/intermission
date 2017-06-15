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

	class DoubleRenderTexture
	{
		RenderTexture m_RenderTexture0;
		RenderTexture m_RenderTexture1;

		public RenderTexture renderTexture0 { get { return m_RenderTexture0; } }
		public RenderTexture renderTexture1 { get { return m_RenderTexture1; } }
		public RenderTexture active { get { return m_First ? m_RenderTexture0 : m_RenderTexture1; } }

		bool m_First = true;

		public void Swap()
		{
			m_First = !m_First;
		}

		RenderTexture CreateTexture(int texWidth, int texHeight, int texDepth)
		{
			RenderTexture rt = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBFloat);
			rt.dimension = TextureDimension.Tex3D;
			rt.volumeDepth = texDepth;
			rt.filterMode = FilterMode.Trilinear;
			return rt;
		}

		public DoubleRenderTexture(int texWidth, int texHeight, int texDepth)
		{
			m_RenderTexture0 = CreateTexture(texWidth, texHeight, texDepth);
			m_RenderTexture1 = CreateTexture(texWidth, texHeight, texDepth);
		}
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

	private DoubleRenderTexture m_VelocityBuffer;
	private DoubleRenderTexture m_DivergenceBuffer;
	private DoubleRenderTexture m_PressureBuffer;
	private DoubleRenderTexture m_ColorBuffer;
	private DoubleRenderTexture m_TempTexture;
	private float m_GridScale = 1f;
	private CommandBuffer m_CommandBuffer;
	public Material m_ColorMaterial;

	DoubleRenderTexture CreateBuffer(int texWidth, int texHeight, int texDepth)
	{
		DoubleRenderTexture rt =  new DoubleRenderTexture(texWidth, texHeight, texDepth);
		Blit(rt.renderTexture0, SolverPass.Clear);
		Blit(rt.renderTexture1, SolverPass.Clear);
		return rt;
	}

	void Start()
	{
		m_FluidSolver = new Material(shader);
		m_CommandBuffer = new CommandBuffer();
		m_VelocityBuffer = CreateBuffer(width, height, depth);
		m_DivergenceBuffer = CreateBuffer(width, height, depth);
		m_PressureBuffer = CreateBuffer(width, height, depth);
		m_ColorBuffer = CreateBuffer(128, 128, 128);
		m_ColorMaterial = GetComponent<MeshRenderer>().material;
		m_ColorMaterial.mainTexture = m_ColorBuffer.active;
		Camera.main.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, m_CommandBuffer);
	}

	void Blit(RenderTexture dest, SolverPass pass)
	{
		for (int i = 0; i < dest.volumeDepth; i++)
		{
			m_CommandBuffer.SetRenderTarget(dest, 0, CubemapFace.Unknown, i);
			m_CommandBuffer.SetGlobalFloat("_Layer", (i + 0.5f) / (float)dest.volumeDepth);
			m_CommandBuffer.DrawMesh(quadMesh, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * 2f), m_FluidSolver, 0, (int)pass);
		}
	}

	void ApplyForce(Vector2 position, Vector2 direction)
	{
		m_CommandBuffer.SetGlobalVector(Properties.Force, new Vector4(position.x, position.y, direction.x, direction.y));
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer.active);
		m_VelocityBuffer.Swap();
		Blit(m_VelocityBuffer.active, SolverPass.ApplyForce);
	}

	void InjectColor(Vector2 position, Color color)
	{
		m_CommandBuffer.SetGlobalVector(Properties.InjectPosition, new Vector4(position.x, position.y, 0, 0));
		m_CommandBuffer.SetGlobalColor(Properties.InjectColor, color);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_ColorBuffer.active);
		m_ColorBuffer.Swap();
		Blit(m_ColorBuffer.active, SolverPass.InjectColor);
	}

	void Advect(float step, float dissipation, DoubleRenderTexture target)
	{
		m_CommandBuffer.SetGlobalFloat(Properties.Step, step);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		m_CommandBuffer.SetGlobalFloat(Properties.Dissipation, dissipation);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, target.active);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_VelocityBuffer.active);
		if (target == m_VelocityBuffer)
			m_VelocityBuffer.Swap();
		Blit(target.active, SolverPass.Advection);
	}

	void Divergence(float step)
	{
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer.active);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		Blit(m_DivergenceBuffer.active, SolverPass.Divergence);
	}

	void Pressure()
	{
		m_CommandBuffer.SetGlobalFloat(Properties.PoissonAlphaCoefficient, -m_GridScale * m_GridScale * viscosity);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_DivergenceBuffer.active);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_PressureBuffer.active);
		m_PressureBuffer.Swap();
		for (int i = 0; i < iterations; i++)
		{
			Blit(m_PressureBuffer.active, SolverPass.Pressure);
		}
	}

	void Gradient()
	{
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer, m_VelocityBuffer.active);
		m_CommandBuffer.SetGlobalTexture(Properties.Buffer2, m_PressureBuffer.active);
		m_CommandBuffer.SetGlobalFloat(Properties.InverseCellSize, 1f / m_GridScale);
		m_VelocityBuffer.Swap();
		Blit(m_VelocityBuffer.active, SolverPass.Gradient);
	}

	Vector2 lastMousePosition;
	void LateUpdate()
	{
		m_CommandBuffer.Clear();
		if (Input.GetKeyDown(KeyCode.Space))
			simulate = !simulate;
		if (Input.GetKeyDown(KeyCode.R))
		{
			Blit(m_ColorBuffer.renderTexture0, SolverPass.Clear);
			Blit(m_ColorBuffer.renderTexture1, SolverPass.Clear);
		}
		if (!simulate)
			return;
		Advect(Time.deltaTime, 0.999f, m_VelocityBuffer);
		if (Input.GetMouseButton(0))
		{
			Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
			Vector2 pos = new Vector2(Input.mousePosition.x / (float)Screen.width, Input.mousePosition.y / (float)Screen.height);
			ApplyForce(pos, mouseDelta / 20);
			InjectColor(pos, new Color(1, 1, 1, 0.2f));
		}
		else
		m_ColorBuffer.Swap();

		lastMousePosition = Input.mousePosition;

		Divergence(Time.deltaTime);
		Pressure();
		Gradient();
		Advect(Time.deltaTime, 0.999f, m_ColorBuffer);
		m_CommandBuffer.SetGlobalTexture("_MainTex", m_ColorBuffer.active);
		//m_ColorMaterial.mainTexture = m_ColorBuffer.active;
	}

	void _OnGUI()
	{
		Graphics.DrawTexture(new Rect(000, 0, 100, 100), m_VelocityBuffer.active);
		Graphics.DrawTexture(new Rect(100, 0, 100, 100), m_PressureBuffer.active);
		Graphics.DrawTexture(new Rect(200, 0, 100, 100), m_DivergenceBuffer.active);
	}
}
