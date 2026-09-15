using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class ShaderEffect_CorruptedVram : MonoBehaviour {

	public float shift = 10;
	private Texture texture;
		private Material material;
		private static readonly int ShiftId = Shader.PropertyToID("_ValueX");
		private static readonly int TextureId = Shader.PropertyToID("_Texture");

		void Awake ()
		{
			if (material != null) return;
			material = new Material( Shader.Find("Hidden/Distortion") );
			material.hideFlags = HideFlags.DontSave;
			texture = Resources.Load<Texture>("Checkerboard-big");
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
		
	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
			material.SetFloat(ShiftId, shift);
			material.SetTexture(TextureId, texture);
		Graphics.Blit (source, destination, material);
	}
}
