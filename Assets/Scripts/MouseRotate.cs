using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseRotate : MonoBehaviour
{
	public float speed = 1f;
	public bool useAlt;

	Vector2 m_PreviousMousePosition;
	Vector3 m_EulerAngles;

	void Start()
	{
		m_EulerAngles = transform.localEulerAngles;
	}

	void Update()
	{
		if ((!useAlt || Input.GetKey(KeyCode.LeftAlt)) && Input.GetMouseButton(0))
		{
			Vector2 delta = (Vector2)Input.mousePosition - m_PreviousMousePosition;
			m_EulerAngles.y += delta.x * Time.deltaTime * speed;
			m_EulerAngles.x += -delta.y * Time.deltaTime * speed;
			transform.localEulerAngles = m_EulerAngles;
		}
		m_PreviousMousePosition = Input.mousePosition;
	}
}
