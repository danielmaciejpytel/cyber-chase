using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class ShaderEffect_BleedingColors : MonoBehaviour {

	public float intensity = 3;
	public float shift = 0.5f;
		private Material material;
		private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
		private static readonly int ShiftId = Shader.PropertyToID("_ValueX");

	// Creates a private material used to the effect
		void Awake ()
		{
			if (material != null) return;
			material = new Material( Shader.Find("Hidden/BleedingColors") );
			material.hideFlags = HideFlags.DontSave;
		}

		void OnEnable ()
		{
			if (material == null) Awake();
		}

#if UNITY_EDITOR
		void OnDisable ()
		{
			if (!Application.isPlaying) ReleaseResources();
		}
#endif

		void OnDestroy ()
		{
			ReleaseResources();
		}

		void ReleaseResources ()
		{
			if (material == null) return;
			if (Application.isPlaying) Destroy(material);
			else DestroyImmediate(material);
			material = null;
		}

	// Postprocess the image
	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
			material.SetFloat(IntensityId, intensity);
			material.SetFloat(ShiftId, shift);
		Graphics.Blit (source, destination, material);
	}
}
