using UnityEngine;

namespace VidiGraph
{
    /// <summary>
    /// A ribbon/swirl trail that orbits the network center while a voice command is processed.
    /// Setup: place this GameObject at the network's horizontal center (floor level is fine —
    /// OrbitHeight lifts the trail up). Assign a transparent/particle material to Trail Material,
    /// or leave empty to attempt an automatic shader lookup.
    /// </summary>
    [AddComponentMenu("VidiGraph/Network Loading Comet")]
    public class NetworkLoadingComet : MonoBehaviour
    {
        [Header("Orbit")]
        public float Radius = 1.0f;
        public float OrbitHeight = 1.2f;
        [Range(20f, 360f)] public float OrbitDegreesPerSecond = 100f;

        [Header("Trail")]
        public Color TrailColor = new Color(0.4f, 0.92f, 1f);
        [Range(0.02f, 0.4f)] public float HeadWidth = 0.08f;
        [Range(0.3f, 4f)] public float TrailDuration = 1.5f;

        [Header("Material")]
        [Tooltip("Assign any transparent/particle material. Leave empty to use automatic fallback.")]
        public Material TrailMaterial;

        GameObject    _emitter;
        TrailRenderer _trail;
        float         _angleDeg;

        void Awake()
        {
            _emitter = new GameObject("_TrailEmitter");
            _emitter.transform.SetParent(transform, worldPositionStays: false);
            _emitter.transform.localPosition = new Vector3(Radius, OrbitHeight, 0f);

            _trail = _emitter.AddComponent<TrailRenderer>();
            _trail.time               = TrailDuration;
            _trail.widthMultiplier    = HeadWidth;
            _trail.numCapVertices     = 6;
            _trail.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows     = false;
            _trail.generateLightingData = false;
            _trail.autodestruct       = false;
            _trail.emitting           = true;
            _trail.minVertexDistance  = 0.02f;

            // Wide at head, tapers to nothing at tail
            _trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -2f),
                new Keyframe(1f, 0f, -2f, 0f)
            );

            // Opaque bright head → transparent tail
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(TrailColor, 0f),
                        new GradientColorKey(TrailColor, 1f) },
                new[] { new GradientAlphaKey(1f,  0f),
                        new GradientAlphaKey(0.5f, 0.5f),
                        new GradientAlphaKey(0f,   1f) }
            );
            _trail.colorGradient = grad;

            _trail.material = ResolveMaterial();

            gameObject.SetActive(false);
        }

        Material ResolveMaterial()
        {
            if (TrailMaterial != null) return TrailMaterial;

            // URP first, Built-in fallback
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default");

            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", TrailColor);
            mat.SetColor("_Color",     TrailColor);
            return mat;
        }

        void Update()
        {
            _angleDeg = (_angleDeg + OrbitDegreesPerSecond * Time.deltaTime) % 360f;
            float a = _angleDeg * Mathf.Deg2Rad;
            _emitter.transform.localPosition =
                new Vector3(Mathf.Cos(a) * Radius, OrbitHeight, Mathf.Sin(a) * Radius);
        }

        public void Show()
        {
            _trail.Clear(); // prevent snap artifact on re-enable
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
