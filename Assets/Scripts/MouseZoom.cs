using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseZoom : MonoBehaviour
{
	public float speed = 1f;

	void Update()
	{
		if (Input.mouseScrollDelta.y != 0f)
		{
			Vector3 offset = transform.localPosition;
			offset.z += Input.mouseScrollDelta.y * speed * Time.deltaTime;
			transform.localPosition = offset;
		}
	}
}
