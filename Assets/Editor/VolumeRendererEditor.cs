using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VolumeRenderer))]
public class VolumeRendererEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		VolumeRenderer renderer = target as VolumeRenderer;
		EditorGUILayout.MinMaxSlider("Cut Limits", ref renderer.cutLimitMin, ref renderer.cutLimitMax, 0f, 1f);
		if (GUI.changed)
		{
			renderer.UpdateDensitySpectrum();
			EditorUtility.SetDirty(target);
		}
	}
}
