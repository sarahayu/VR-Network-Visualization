using UnityEngine;

namespace VidiGraph
{
    /// <summary>
    /// A single glowing sphere that orbits the network center like a planet,
    /// with an optional faint ring showing the orbit path.
    /// Shown while voice commands are being processed.
    /// Setup: attach to an empty GameObject at the network's horizontal center.
    /// </summary>
    [AddComponentMenu("VidiGraph/Network Loading Comet")]
    public class NetworkLoadingComet : MonoBehaviour
    {
        [Header("Orbit")]
        public float Radius = 2.5f;
        public float OrbitHeight = 1.2f;
        [Range(20f, 360f)] public float OrbitDegreesPerSecond = 90f;

        [Header("Planet")]
        public Color PlanetColor = new Color(0.4f, 0.92f, 1f);
        [Range(0.03f, 0.25f)] public float PlanetSize = 0.12f;

        [Header("Orbit Ring")]
        public bool ShowOrbitRing = true;
        [Range(0f, 1f)] public float RingBrightness = 0.25f;

        const int RingSegments = 80;

        GameObject   _planet;
        LineRenderer _ring;
        float        _angleDeg;

        void Awake()
        {
            BuildPlanet();
            if (ShowOrbitRing) BuildRing();
            gameObject.SetActive(false);
        }

        void BuildPlanet()
        {
            _planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_planet.GetComponent<Collider>());
            _planet.transform.SetParent(transform, worldPositionStays: false);
            _planet.transform.localScale = Vector3.one * PlanetSize;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor",     PlanetColor);
            mpb.SetColor("_Color",         PlanetColor);
            // Emission drives glow when Bloom post-process is active
            mpb.SetColor("_EmissionColor", PlanetColor * 3f);
            _planet.GetComponent<Renderer>().SetPropertyBlock(mpb);
        }

        void BuildRing()
        {
            var go = new GameObject("_OrbitRing");
            go.transform.SetParent(transform, worldPositionStays: false);

            _ring = go.AddComponent<LineRenderer>();
            _ring.useWorldSpace    = false;
            _ring.loop             = true;
            _ring.positionCount    = RingSegments;
            _ring.startWidth       = 0.006f;
            _ring.endWidth         = 0.006f;
            _ring.numCapVertices   = 0;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows   = false;

            Color rc = new Color(PlanetColor.r * RingBrightness,
                                 PlanetColor.g * RingBrightness,
                                 PlanetColor.b * RingBrightness);
            _ring.startColor = rc;
            _ring.endColor   = rc;

            for (int i = 0; i < RingSegments; i++)
            {
                float a = (float)i / RingSegments * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * Radius, OrbitHeight, Mathf.Sin(a) * Radius));
            }
        }

        void Update()
        {
            _angleDeg = (_angleDeg + OrbitDegreesPerSecond * Time.deltaTime) % 360f;
            float a = _angleDeg * Mathf.Deg2Rad;
            _planet.transform.localPosition =
                new Vector3(Mathf.Cos(a) * Radius, OrbitHeight, Mathf.Sin(a) * Radius);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
