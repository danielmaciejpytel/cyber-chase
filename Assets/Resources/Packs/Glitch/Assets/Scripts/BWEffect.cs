using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class BWEffect : MonoBehaviour {

	public float intensity;
		private Material material;
		private static readonly int BlendId = Shader.PropertyToID("_bwBlend");

	// Creates a private material used to the effect
		void Awake ()
		{
			if (material != null) return;
			material = new Material( Shader.Find("Hidden/BWDiffuse") );
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
		if (intensity == 0)
		{
			Graphics.Blit (source, destination);
			return;
		}

			material.SetFloat(BlendId, intensity);
		Graphics.Blit (source, destination, material);
	}
}
