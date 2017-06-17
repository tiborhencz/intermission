using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class DatasetImporter : MonoBehaviour
{
	[MenuItem("Volume/Import Texture")]
	static void Import()
	{
		Texture3D output;
		string[] files = (from file in Directory.GetFiles("Assets/cthead-8bit")
			where !file.EndsWith(".meta") select file).ToArray();
		Texture2D first = AssetDatabase.LoadAssetAtPath<Texture2D>(files[0]);
		output = new Texture3D(first.width, first.height, files.Length, TextureFormat.ARGB32, false);
		Color[] pixels = new Color[files.Length * first.width * first.height];
		int idx = 0;
		for (int i = 0; i < files.Length; i++)
		{
			Texture2D slice = AssetDatabase.LoadAssetAtPath<Texture2D>(files[i]);
			for (int y = 0; y < first.height; y++)
			{
				for (int x = 0; x < first.width; x++, idx++)
				{
					pixels[idx] = slice.GetPixel(x, y);
				}
			}
		}
		output.SetPixels(pixels);
		output.Apply();
		AssetDatabase.CreateAsset(output, "Assets/cthead-8bit.asset");
	}
}
