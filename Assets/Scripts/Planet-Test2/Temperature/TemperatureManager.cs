using UnityEngine;

public class TemperatureManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Planet mesh that holds per-cell direction / elevation / land flags.")]
    public CubeSphereBlockMesh planet;

    [Tooltip("SunRotate controller (for timeScale, day/year progression).")]
    public SunRotate sunRotate;

    [Tooltip("Transform whose forward points FROM planet TO sun (usually your Directional Light).")]
    public Transform sunDirectionTransform;

    [Header("Latitude Temperature")]
    [Tooltip("Baseline temperature at the equator (°C).")]
    public float equatorTemp = 25f;

    [Tooltip("Baseline temperature at the poles (°C).")]
    public float poleTemp = -15f;

    [Header("Day / Night Contribution")]
    [Tooltip("Max daytime warming for land when sun is directly overhead (°C).")]
    public float dayBoostLand = 15f;

    [Tooltip("Night cooling offset for land at midnight (°C).")]
    public float nightDropLand = 10f;

    [Tooltip("Max daytime warming for water (oceans) (°C).")]
    public float dayBoostWater = 8f;

    [Tooltip("Night cooling offset for water (oceans) (°C).")]
    public float nightDropWater = 5f;

    [Header("Elevation (Lapse Rate)")]
    [Tooltip("Assumed conversion from world units to kilometers for elevation.")]
    public float worldUnitsPerKm = 1f;

    [Tooltip("Temperature drop per kilometer of elevation (°C / km).")]
    public float lapseRatePerKm = 6f;

    [Header("Simulation Settings")]
    [Tooltip("How strongly tiles move toward target per simulated hour (0–1).")]
    [Range(0f, 1f)]
    public float baseResponsePerHour = 0.1f;

    [Tooltip("Land responds faster (1 = same as base, >1 = faster).")]
    public float landResponseMultiplier = 1.5f;

    [Tooltip("Water responds slower (1 = same as base, <1 = slower).")]
    public float waterResponseMultiplier = 0.5f;

    [Tooltip("Minimum simulated hours to accumulate before doing a temp update step.")]
    public float minStepHours = 0.1f;   // ~6 in-game minutes

    [Header("Debug")]
    [SerializeField] private float accumulatedSimHours = 0f;
    [SerializeField] private float lastTimeOfDay = -1f;
    [SerializeField] private float effectiveMinStepHoursDebug;

    // Current temperatures per cell (°C)
    private float[] currentTemps;

    public bool IsInitialized => currentTemps != null && currentTemps.Length == (planet ? planet.TotalCells : 0);

    private void Start()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!TryInitialize())
            return;

        if (!sunRotate || !sunDirectionTransform)
            return;

        // Use SunRotate's normalized time-of-day (0–1) as the single source of truth.
        float currentDayT = sunRotate.GetTimeOfDay(); // 0..1 over a full day

        // First frame: just initialize baseline.
        if (lastTimeOfDay < 0f)
        {
            lastTimeOfDay = currentDayT;
            return;
        }

        // Compute how much of a day has passed since last frame, with wrap-around at midnight.
        float deltaDay = currentDayT - lastTimeOfDay;
        if (deltaDay < 0f)
        {
            // We wrapped past 1 -> 0, so add 1.
            deltaDay += 1f;
        }

        // Convert fraction of a day to in-game hours (24 hours per day).
        float simDeltaHours = deltaDay * 24f;
        accumulatedSimHours += simDeltaHours;

        float ts = Mathf.Max(sunRotate.timeScale, 0.0001f);      // avoid zero
        float effectiveMinStepHours = minStepHours * ts;         // 1:1 scaling (5x -> 5 * minStepHours) ;)
        effectiveMinStepHoursDebug = effectiveMinStepHours;

        if (accumulatedSimHours >= effectiveMinStepHours)
        {
            float stepHours = accumulatedSimHours;
            accumulatedSimHours = 0f;

            DoTemperatureStep(stepHours);
        }

        lastTimeOfDay = currentDayT;
    }

    private bool TryInitialize()
    {
        if (!planet)
            return false;

        int totalCells = planet.TotalCells;
        if (totalCells <= 0)
            return false;

        if (currentTemps == null || currentTemps.Length != totalCells)
        {
            currentTemps = new float[totalCells];

            // Initialize temps to a reasonable starting value based on current sun position.
            Vector3 sunDir = GetSunDirection();
            for (int i = 0; i < totalCells; i++)
            {
                currentTemps[i] = ComputeTargetTemperature(i, sunDir);
            }
        }

        return true;
    }

    private void DoTemperatureStep(float deltaHours)
    {
        Vector3 sunDir = GetSunDirection();
        int totalCells = planet.TotalCells;

        for (int i = 0; i < totalCells; i++)
        {
            float target = ComputeTargetTemperature(i, sunDir);

            bool isLand = planet.cellIsLand[i];
            float response = baseResponsePerHour *
                             (isLand ? landResponseMultiplier : waterResponseMultiplier);

            // Exponential smoothing toward target.
            currentTemps[i] += (target - currentTemps[i]) * response * deltaHours;
        }
    }

    private Vector3 GetSunDirection()
    {
        // Direction of sunlight: from tile -> sun
        Vector3 planetPos = planet.transform.position;
        Vector3 sunPos = sunDirectionTransform.position;

        return (sunPos - planetPos).normalized;
    }

    /// <summary>
    /// Compute the "ideal" instantaneous temperature for a given cell,
    /// based on latitude, day/night, and elevation.
    /// </summary>
    private float ComputeTargetTemperature(int cellIndex, Vector3 sunDir)
    {
        Vector3 n = planet.cellCenterDirection[cellIndex];
        bool isLand = planet.cellIsLand[cellIndex];

        // 1) Latitude baseline: equator hot, poles cold.
        float latFactor = Mathf.Abs(planet.cellLatitude[cellIndex]); // 0 (equator) -> 1 (poles)
        float baseLatTemp = Mathf.Lerp(equatorTemp, poleTemp, latFactor);

        // 2) Day/night via local sun height.
        float sunDot = Vector3.Dot(n, sunDir);   // -1..1
        float sunExposure = Mathf.Clamp01(sunDot); // 0..1 (0 at/below horizon, 1 when overhead)

        float dayBoost = isLand ? dayBoostLand : dayBoostWater;
        float nightDrop = isLand ? nightDropLand : nightDropWater;

        float diurnal = -nightDrop + dayBoost * sunExposure;

        // 3) Elevation: cooler as we go up.
        // Only apply positive elevation (above base radius); oceans can stay flat unless you want deep water effects.
        float elevationWorldUnits = Mathf.Max(0f, planet.cellElevation[cellIndex]);
        float elevationKm = elevationWorldUnits / Mathf.Max(worldUnitsPerKm, 0.0001f);
        float elevationOffset = -lapseRatePerKm * elevationKm;

        float target = baseLatTemp + diurnal + elevationOffset;

        return target;
    }

    /// <summary>
    /// Get the current simulated temperature at a given cell index (°C).
    /// </summary>
    public float GetCellTemperature(int cellIndex)
    {
        if (!IsInitialized)
            return 0f;

        if (cellIndex < 0 || cellIndex >= currentTemps.Length)
            return 0f;

        return currentTemps[cellIndex];
    }
}
