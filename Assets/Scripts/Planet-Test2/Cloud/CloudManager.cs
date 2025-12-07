using UnityEngine;

/// <summary>
/// Drives the EcoCore/CloudShell shader: sets planet center, camera position,
/// noise time, clearance, and visuals. No mesh rebuilding involved.
/// 
/// Refactored:
/// - Single time parameter controls both curl flow + vertical 3D noise "boiling".
/// - No latitude bands; movement randomness is handled in the shader via curl noise.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class CloudManager : MonoBehaviour
{
    [Header("Links")]
    public Transform planetCenter;
    public Transform cameraTransform;
    public SunRotate sunRotate;

    [Header("Noise / Coverage")]
    [Tooltip("Lower = larger cloud blobs.")]
    public float cloudFrequency = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Bias for coverage. Higher = fewer clouds.")]
    public float coverageBias = 0.4f;

    public int cloudSeed = 42;

    [Header("Animation")]
    public bool animate = true;

    [Tooltip("Speed of curl/advection (x in _NoiseOffset).")]
    public float curlSpeed = 0.25f;

    [Tooltip("Speed of boiling (z-slice scrolling, y in _NoiseOffset).")]
    public float boilSpeed = 0.25f;

    [Header("Depth / Layering")]
    [Tooltip("How strongly shell height affects the 3D noise pattern.")]
    public float heightNoiseScale = 0.5f;

    [Header("Camera Clearance")]
    public float clearanceRadius = 15f;  // fully clear
    public float clearanceFade = 5f;   // fade band

    [Header("Visuals")]
    public Color cloudColor = Color.white;
    [Range(0f, 1f)] public float cloudOpacity = 0.9f;

    private Material _mat;

    // single time parameter used by shader for curl & vertical noise scrolling
    //private float _timeParam = 0f;

    // remember last day fraction from SunRotate so we can handle wrap-around
    [SerializeField, Range(0f, 1f)]
    private float lastTimeOfDay = -1f;

    private float _curlTime = 0f;
    private float _boilTime = 0f;

    private void Awake()
    {
        var mr = GetComponent<MeshRenderer>();
        _mat = mr.material; // per-instance material
    }

    private void Update()
    {
        if (_mat == null || planetCenter == null || cameraTransform == null)
            return;

        // ----------------------------------------------------------------
        // 1. Advance time parameter
        // ----------------------------------------------------------------
        if (animate)
        {
            float delta = 0f;

            if (sunRotate != null)
            {
                float currentDayT = sunRotate.GetTimeOfDay();
                if (lastTimeOfDay < 0f)
                {
                    lastTimeOfDay = currentDayT;
                }
                else
                {
                    float deltaDay = currentDayT - lastTimeOfDay;
                    if (deltaDay < 0f)
                        deltaDay += 1f;
                    delta = deltaDay;
                    lastTimeOfDay = currentDayT;
                }
            }
            else
            {
                delta = Time.deltaTime;
            }

            _curlTime += curlSpeed * delta;
            _boilTime += boilSpeed * delta;
        }

        // ----------------------------------------------------------------
        // 2. Push parameters to the material
        // ----------------------------------------------------------------

        // Core noise / coverage
        _mat.SetFloat("_CloudFrequency", cloudFrequency);
        _mat.SetFloat("_CoverageBias", coverageBias);
        _mat.SetFloat("_CloudSeed", cloudSeed);

        // Pack curl into x, boil into y
        _mat.SetVector("_NoiseOffset", new Vector4(_curlTime, _boilTime, 0f, 0f));

        _mat.SetVector("_PlanetCenter", planetCenter.position);
        _mat.SetFloat("_HeightNoiseScale", heightNoiseScale);

        // Camera clearance + visuals
        _mat.SetFloat("_ClearanceRadius", clearanceRadius);
        _mat.SetFloat("_ClearanceFade", clearanceFade);

        _mat.SetColor("_CloudColor", cloudColor);
        _mat.SetFloat("_CloudOpacity", cloudOpacity);
    }
}
