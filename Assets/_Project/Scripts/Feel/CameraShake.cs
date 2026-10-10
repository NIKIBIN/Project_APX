using UnityEngine;
using UnityEngine.Rendering;

namespace APX.Feel
{
    /// <summary>
    /// Trauma-based screen shake. Events add trauma (0..1); the shake strength is trauma squared, so small
    /// hits barely move the camera while big ones really kick. The offset is applied only while this camera
    /// renders, so it never fights the camera's own positioning (e.g. room cuts) or drifts. Runs on real time.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Global multiplier, e.g. from an accessibility setting. 0 disables shaking.")]
        [SerializeField, Range(0f, 1f)] float intensity = 1f;
        [Tooltip("Largest offset at full trauma, in world units.")]
        [SerializeField, Min(0f)] float maxOffset = 0.35f;
        [Tooltip("Largest roll at full trauma, in degrees.")]
        [SerializeField, Min(0f)] float maxRoll = 1.2f;
        [SerializeField, Min(0.1f)] float frequency = 22f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField, Min(0.01f)] float decay = 1.6f;

        Camera _camera;
        float _trauma;
        float _seed;
        bool _applied;
        Vector3 _savedPosition;
        Quaternion _savedRotation;

        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp01(value);
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _seed = Random.value * 100f;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _trauma = 0f;
        }

        void Update() => _trauma = Mathf.Max(0f, _trauma - decay * Time.unscaledDeltaTime);

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>Keeps trauma at least at <paramref name="minimum"/>; call every frame for a sustained rumble.</summary>
        public void HoldTrauma(float minimum) => _trauma = Mathf.Clamp01(Mathf.Max(_trauma, minimum));

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            float shake = _trauma * _trauma * intensity;
            if (rendering != _camera || shake <= 0f)
                return;

            float time = Time.unscaledTime * frequency;
            Vector3 offset = new Vector3(Noise(time, 0f), Noise(time, 1f), 0f) * (maxOffset * shake);
            float roll = Noise(time, 2f) * maxRoll * shake;

            _savedPosition = transform.position;
            _savedRotation = transform.rotation;
            transform.SetPositionAndRotation(_savedPosition + offset, _savedRotation * Quaternion.Euler(0f, 0f, roll));
            _applied = true;
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != _camera || !_applied)
                return;

            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            _applied = false;
        }

        // Smooth noise in -1..1, one independent channel per axis.
        float Noise(float time, float channel) => Mathf.PerlinNoise(_seed + channel * 17.3f, time) * 2f - 1f;
    }
}
