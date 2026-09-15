using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class ShaderEffect_CRT : MonoBehaviour {

	public float scanlineIntensity = 100;
	public int scanlineWidth = 1;
//	public Color scanlineColor = Color.black;
//	public bool tVBulge = true;
	private Material material_Displacement;
		private Material material_Scanlines;
		private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
		private static readonly int WidthId = Shader.PropertyToID("_ValueX");

		void Awake ()
		{
			if (material_Scanlines != null) return;
			material_Scanlines = new Material( Shader.Find("Hidden/Scanlines") );
			material_Scanlines.hideFlags = HideFlags.DontSave;
		}

		void OnEnable ()
		{
			if (material_Scanlines == null) Awake();
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
			if (material_Scanlines == null) return;
			if (Application.isPlaying) Destroy(material_Scanlines);
			else DestroyImmediate(material_Scanlines);
			material_Scanlines = null;
		}

	void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
			material_Scanlines.SetFloat(IntensityId, scanlineIntensity * 0.01f);
			material_Scanlines.SetFloat(WidthId, scanlineWidth);

		Graphics.Blit (source, destination, material_Scanlines);

	}
}
