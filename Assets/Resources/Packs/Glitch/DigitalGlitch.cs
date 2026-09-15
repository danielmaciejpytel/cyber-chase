using UnityEngine;

namespace Kino
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Digital Glitch")]
    public class DigitalGlitch : MonoBehaviour
    {
        #region Public Properties

        [SerializeField, Range(0, 1)]
        float _intensity = 0;

        public float intensity {
            get { return _intensity; }
            set { _intensity = value; }
        }

        #endregion

        #region Private Properties

        [SerializeField] Shader _shader;

        Material _material;
        Texture2D _noiseTexture;
        RenderTexture _trashFrame1;
        RenderTexture _trashFrame2;
        Color[] _noisePixels;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        static readonly int TrashTexId = Shader.PropertyToID("_TrashTex");

        #endregion

        #region Private Functions

        static Color RandomColor()
        {
            return new Color(Random.value, Random.value, Random.value, Random.value);
        }

        void SetUpResources()
        {
            if (_material != null) return;

            _material = new Material(_shader);
            _material.hideFlags = HideFlags.DontSave;

            _noiseTexture = new Texture2D(64, 32, TextureFormat.ARGB32, false);
            _noiseTexture.hideFlags = HideFlags.DontSave;
            _noiseTexture.wrapMode = TextureWrapMode.Clamp;
            _noiseTexture.filterMode = FilterMode.Point;

            _trashFrame1 = new RenderTexture(Screen.width, Screen.height, 0);
            _trashFrame2 = new RenderTexture(Screen.width, Screen.height, 0);
            _trashFrame1.hideFlags = HideFlags.DontSave;
            _trashFrame2.hideFlags = HideFlags.DontSave;

            UpdateNoiseTexture();
        }

        void UpdateNoiseTexture()
        {
            var width = _noiseTexture.width;
            var height = _noiseTexture.height;
            if (_noisePixels == null || _noisePixels.Length != width * height)
                _noisePixels = new Color[width * height];

            var color = RandomColor();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (Random.value > 0.89f) color = RandomColor();
                    _noisePixels[y * width + x] = color;
                }
            }

            _noiseTexture.SetPixels(_noisePixels);
            _noiseTexture.Apply();
        }

        void ReleaseResources()
        {
            if (_trashFrame1 != null) _trashFrame1.Release();
            if (_trashFrame2 != null) _trashFrame2.Release();
            DestroyResource(_material);
            DestroyResource(_noiseTexture);
            DestroyResource(_trashFrame1);
            DestroyResource(_trashFrame2);
            _material = null;
            _noiseTexture = null;
            _trashFrame1 = null;
            _trashFrame2 = null;
            _noisePixels = null;
        }

        static void DestroyResource(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }

        #endregion

        #region MonoBehaviour Functions

#if UNITY_EDITOR
        void OnDisable()
        {
            if (!Application.isPlaying) ReleaseResources();
        }
#endif

        void OnDestroy()
        {
            // Runtime toggles retain noise, frame history and the RNG sequence.
            ReleaseResources();
        }

        void Update()
        {
            if (Random.value > Mathf.Lerp(0.9f, 0.5f, _intensity))
            {
                SetUpResources();
                UpdateNoiseTexture();
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            SetUpResources();

            // Update trash frames on a constant interval.
            var fcount = Time.frameCount;
            if (fcount % 13 == 0) Graphics.Blit(source, _trashFrame1);
            if (fcount % 73 == 0) Graphics.Blit(source, _trashFrame2);

            _material.SetFloat(IntensityId, _intensity);
            _material.SetTexture(NoiseTexId, _noiseTexture);
            var trashFrame = Random.value > 0.5f ? _trashFrame1 : _trashFrame2;
            _material.SetTexture(TrashTexId, trashFrame);

            Graphics.Blit(source, destination, _material);
        }

        #endregion
    }
}
