using UnityEngine;

namespace Kino
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Analog Glitch")]
    public class AnalogGlitch : MonoBehaviour
    {
        #region Public Properties

        // Scan line jitter

        [SerializeField, Range(0, 1)]
        float _scanLineJitter = 0;

        public float scanLineJitter {
            get { return _scanLineJitter; }
            set { _scanLineJitter = value; }
        }

        // Vertical jump

        [SerializeField, Range(0, 1)]
        float _verticalJump = 0;

        public float verticalJump {
            get { return _verticalJump; }
            set { _verticalJump = value; }
        }

        // Horizontal shake

        [SerializeField, Range(0, 1)]
        float _horizontalShake = 0;

        public float horizontalShake {
            get { return _horizontalShake; }
            set { _horizontalShake = value; }
        }

        // Color drift

        [SerializeField, Range(0, 1)]
        float _colorDrift = 0;

        public float colorDrift {
            get { return _colorDrift; }
            set { _colorDrift = value; }
        }

        #endregion

        #region Private Properties

        [SerializeField] Shader _shader;

        Material _material;

        float _verticalJumpTime;

        static readonly int ScanLineJitterId = Shader.PropertyToID("_ScanLineJitter");
        static readonly int VerticalJumpId = Shader.PropertyToID("_VerticalJump");
        static readonly int HorizontalShakeId = Shader.PropertyToID("_HorizontalShake");
        static readonly int ColorDriftId = Shader.PropertyToID("_ColorDrift");

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
            ReleaseResources();
        }

        void ReleaseResources()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                _material = new Material(_shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            _verticalJumpTime += Time.deltaTime * _verticalJump * 11.3f;

            var sl_thresh = Mathf.Clamp01(1.0f - _scanLineJitter * 1.2f);
            var sl_disp = 0.002f + Mathf.Pow(_scanLineJitter, 3) * 0.05f;
            _material.SetVector(ScanLineJitterId, new Vector2(sl_disp, sl_thresh));

            var vj = new Vector2(_verticalJump, _verticalJumpTime);
            _material.SetVector(VerticalJumpId, vj);

            _material.SetFloat(HorizontalShakeId, _horizontalShake * 0.2f);

            var cd = new Vector2(_colorDrift * 0.04f, Time.time * 606.11f);
            _material.SetVector(ColorDriftId, cd);

            Graphics.Blit(source, destination, _material);
        }

        #endregion
    }
}
