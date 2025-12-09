using UnityEngine;

public class HumidityManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Planet mesh that holds per-cell direction / elevation / land flags.")]
    public CubeSphereBlockMesh planet;

    [Tooltip("SunRotate controller (for timeScale, day/year progression).")]
    public SunRotate sunRotate;

    [Tooltip("Temperature manager providing per-cell temperatures (°C).")]
    public TemperatureManager tempManager;

    [Header("Baseline Humidity Shaping")]
    [Tooltip("Baseline humidity at the equator (0–1).")]
    [Range(0f, 1f)]
    public float equatorHumidity = 1.0f;

    [Tooltip("Baseline humidity at the poles (0–1).")]
    [Range(0f, 1f)]
    public float poleHumidity = 0.1f;

    [Tooltip("Center of the subtropical dry belt in |lat| normalized [0,1] (≈30° -> ~0.333).")]
    [Range(0f, 1f)]
    public float desertBeltCenterLatAbs = 0.333f;

    [Tooltip("Width of the subtropical dry belt in |lat| normalized units.")]
    [Range(0.01f, 1f)]
    public float desertBeltWidth = 0.15f;

    [Tooltip("Maximum humidity reduction in the desert belt (0–1).")]
    [Range(0f, 1f)]
    public float desertBeltDepth = 0.5f;

    [Tooltip("Extra humidity over oceans vs land (0–1).")]
    [Range(0f, 1f)]
    public float oceanHumidityBonus = 0.25f;

    [Tooltip("How much high elevation dries out cells (0–1).")]
    [Range(0f, 1f)]
    public float mountainDryStrength = 0.3f;

    [Tooltip("Approximate elevation where mountains 'max out' (world units).")]
    public float mountainHeightWorldUnits = 20f;

    [Header("Coastal Humidity")]
    [Tooltip("Max humidity boost for a land cell fully surrounded by water (0–1).")]
    [Range(0f, 1f)]
    public float coastalBoostMax = 0.3f;

    [Header("Simulation Settings")]
    [Tooltip("How strongly tiles move toward target per simulated hour (0–1).")]
    [Range(0f, 1f)]
    public float baseResponsePerHour = 0.05f;

    [Tooltip("Global multiplier for land response (kept for tuning).")]
    public float landResponseMultiplier = 1.0f;

    [Tooltip("Global multiplier for water response (kept for tuning).")]
    public float waterResponseMultiplier = 1.0f;

    [Tooltip("Minimum simulated hours to accumulate before doing a humidity update step.")]
    public float minStepHours = 0.1f;   // ~6 in-game minutes at 1x

    [Header("Dynamic Inertia by Environment")]
    [Tooltip("Volatility for deep inland land (no nearby water). Higher = faster humidity changes.")]
    public float innerLandVolatility = 1.5f;

    [Tooltip("Volatility for coastal land (lots of nearby water). Higher = faster changes; lower = more buffered.")]
    public float coastalLandVolatility = 0.75f;

    [Tooltip("Volatility for oceans/lakes. Should be smallest; they change humidity slowest.")]
    public float oceanVolatility = 0.25f;

    [Header("Temperature Coupling")]
    [Tooltip("Max humidity change from temp deviations (0–1 in humidity space).")]
    [Range(0f, 0.5f)]
    public float maxTempHumidityOffset = 0.15f;

    [Tooltip("°C deviation from climate baseline that counts as 'full' temp effect.")]
    public float tempDeviationForMaxEffect = 20f;

    [Tooltip("Strength of drying/wetting over deep inland land due to temp.")]
    public float inlandTempDryingStrength = 1.0f;

    [Tooltip("Strength of temp-driven humidity changes over coastal land.")]
    public float coastalTempHumidityStrength = 0.5f;

    [Tooltip("Strength of temp-driven humidity changes over oceans.")]
    public float oceanTempHumidityStrength = 0.5f;

    [Header("Debug")]
    [SerializeField] private float accumulatedSimHours = 0f;
    [SerializeField] private float lastTimeOfDay = -1f;
    [SerializeField] private float effectiveMinStepHoursDebug;

    // Internal arrays (0–1 range for humidity)
    private float[] baselineHumidity01;
    private float[] currentHumidity01;

    // For land tiles: how coastal they are (fraction of neighbors that are water, 0..1)
    private float[] landWaterNeighborFraction;

    // Climate baseline temperature per cell (°C), from lat + elevation only (no day/night).
    private float[] baselineTempC;

    public bool IsInitialized =>
        planet &&
        baselineHumidity01 != null &&
        currentHumidity01 != null &&
        baselineHumidity01.Length == planet.TotalCells &&
        currentHumidity01.Length == planet.TotalCells;

    private void Start()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!TryInitialize())
            return;

        if (!sunRotate)
            return;

        float currentDayT = sunRotate.GetTimeOfDay(); // 0..1 over a full day

        // First frame: just initialize baseline.
        if (lastTimeOfDay < 0f)
        {
            lastTimeOfDay = currentDayT;
            return;
        }

        // Day fraction advanced (handles wrap-around).
        float deltaDay = currentDayT - lastTimeOfDay;
        if (deltaDay < 0f)
        {
            deltaDay += 1f;
        }

        float simDeltaHours = deltaDay * 24f;
        accumulatedSimHours += simDeltaHours;

        // Same trick as TemperatureManager: step size grows with timeScale so cost stays stable.
        float ts = Mathf.Max(sunRotate.timeScale, 0.0001f);
        float effectiveMinStepHours = minStepHours * ts;
        effectiveMinStepHoursDebug = effectiveMinStepHours;

        if (accumulatedSimHours >= effectiveMinStepHours)
        {
            float stepHours = accumulatedSimHours;
            accumulatedSimHours = 0f;

            DoHumidityStep(stepHours);
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

        if (tempManager != null)
        {
            tempManager.EnsureInitialized();
        }

        if (baselineHumidity01 == null || baselineHumidity01.Length != totalCells)
        {
            baselineHumidity01 = new float[totalCells];
            currentHumidity01 = new float[totalCells];
            landWaterNeighborFraction = new float[totalCells];

            BuildBaselineHumidity();
            ApplyCoastalBoostAndCacheCoastalness();
            BuildBaselineTemperatureForHumidity(); // uses tempManager config if present

            // Start current = *temp-coupled* target if possible, otherwise baseline
            for (int i = 0; i < totalCells; i++)
            {
                if (tempManager != null && tempManager.IsInitialized && baselineTempC != null)
                {
                    currentHumidity01[i] = ComputeHumidityTarget01(i);
                }
                else
                {
                    currentHumidity01[i] = baselineHumidity01[i];
                }
            }

            // sync to planet 0–100
            if (planet.cellHumidity == null || planet.cellHumidity.Length != totalCells)
                planet.cellHumidity = new float[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                planet.cellHumidity[i] = Mathf.Clamp01(currentHumidity01[i]) * 100f;
            }
        }

        return true;
    }

    /// <summary>
    /// Build static baseline humidity from latitude, desert belt, ocean/land, and elevation.
    /// Values stored in baselineHumidity01 as 0–1.
    /// </summary>
    private void BuildBaselineHumidity()
    {
        int totalCells = planet.TotalCells;

        for (int i = 0; i < totalCells; i++)
        {
            bool isLand = planet.cellIsLand[i];
            float lat = planet.cellLatitude[i];      // -1..1 (sin latitude)
            float elev = planet.cellElevation[i];    // relative to base radius

            float latAbs = Mathf.Abs(lat); // 0 at equator, 1 at poles

            // 1) Latitude-based gradient: wet equator -> dry poles
            float baseHum01 = Mathf.Lerp(equatorHumidity, poleHumidity, latAbs);

            // 2) Subtropical dry belt (Hadley cell deserts)
            float x = (latAbs - desertBeltCenterLatAbs) / Mathf.Max(desertBeltWidth, 0.0001f);
            float desertBelt = Mathf.Exp(-x * x); // Gaussian bump 0..1
            baseHum01 -= desertBelt * desertBeltDepth;

            // 3) Oceans more humid than land
            if (!isLand)
            {
                baseHum01 += oceanHumidityBonus;
            }

            // 4) High elevation drier (mountains)
            if (elev > 0f)
            {
                float elevNorm = elev / Mathf.Max(mountainHeightWorldUnits, 0.0001f);
                elevNorm = Mathf.Clamp01(elevNorm);
                baseHum01 -= elevNorm * mountainDryStrength;
            }

            baselineHumidity01[i] = Mathf.Clamp01(baseHum01);
        }
    }

    /// <summary>
    /// Second pass: land cells near water get a coastal humidity boost
    /// AND we cache how coastal each land cell is (0..1 water neighbor fraction).
    /// </summary>
    private void ApplyCoastalBoostAndCacheCoastalness()
    {
        int totalCells = planet.TotalCells;
        int steps = planet.CellsPerFace;

        // Initialize to zero in case of re-gen
        for (int i = 0; i < totalCells; i++)
        {
            landWaterNeighborFraction[i] = 0f;
        }

        for (int i = 0; i < totalCells; i++)
        {
            if (!planet.cellIsLand[i])
                continue;

            int faceIndex = i / (steps * steps);
            int indexInFace = i % (steps * steps);
            int row = indexInFace / steps;
            int col = indexInFace % steps;

            int waterNeighbors = 0;
            int neighborCount = 0;

            // 3x3 neighborhood on same face
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = col + dx;
                    int ny = row + dy;

                    if (nx < 0 || nx >= steps || ny < 0 || ny >= steps)
                        continue;

                    int neighborIndex = faceIndex * (steps * steps) + ny * steps + nx;

                    neighborCount++;
                    if (!planet.cellIsLand[neighborIndex])
                    {
                        waterNeighbors++;
                    }
                }
            }

            float waterFraction = 0f;
            if (neighborCount > 0)
            {
                waterFraction = (float)waterNeighbors / neighborCount; // 0..1
            }

            // Cache for dynamic sim (coastalness)
            landWaterNeighborFraction[i] = waterFraction;

            // Static coastal boost for baseline field
            if (neighborCount > 0 && waterNeighbors > 0 && coastalBoostMax > 0f)
            {
                baselineHumidity01[i] = Mathf.Clamp01(
                    baselineHumidity01[i] + waterFraction * coastalBoostMax
                );
            }
        }
    }

    /// <summary>
    /// Build a "climate baseline" temperature per cell (°C) from latitude + elevation only.
    /// This is what we compare the dynamic temperature field against when adjusting humidity.
    /// </summary>
    private void BuildBaselineTemperatureForHumidity()
    {
        if (tempManager == null || planet == null)
        {
            baselineTempC = null;
            return;
        }

        int totalCells = planet.TotalCells;
        baselineTempC = new float[totalCells];

        for (int i = 0; i < totalCells; i++)
        {
            float latAbs = Mathf.Abs(planet.cellLatitude[i]); // 0 equator -> 1 poles
            float baseLatTemp = Mathf.Lerp(tempManager.equatorTemp, tempManager.poleTemp, latAbs);

            float elevWorld = Mathf.Max(0f, planet.cellElevation[i]); // only positive elevation
            float elevKm = elevWorld / Mathf.Max(tempManager.worldUnitsPerKm, 0.0001f);
            float elevOffset = -tempManager.lapseRatePerKm * elevKm;

            baselineTempC[i] = baseLatTemp + elevOffset;
        }
    }

    /// <summary>
    /// Sim step: humidity relaxes toward a target influenced by:
    /// - Static climate field (baselineHumidity01)
    /// - Temp deviations from baseline (baselineTempC vs current temp)
    /// - Environment inertia (ocean/coastal/inland)
    /// </summary>
    private void DoHumidityStep(float deltaHours)
    {
        int totalCells = planet.TotalCells;

        for (int i = 0; i < totalCells; i++)
        {
            bool isLand = planet.cellIsLand[i];

            // Get the instantaneous target, including temperature effects.
            float target = ComputeHumidityTarget01(i);

            // Inertia / volatility
            float response;
            if (isLand)
            {
                float coastalness = Mathf.Clamp01(landWaterNeighborFraction[i]);
                float volatility = Mathf.Lerp(innerLandVolatility, coastalLandVolatility, coastalness);
                response = baseResponsePerHour * volatility * landResponseMultiplier;
            }
            else
            {
                response = baseResponsePerHour * oceanVolatility * waterResponseMultiplier;
            }

            currentHumidity01[i] += (target - currentHumidity01[i]) * response * deltaHours;
        }

        for (int i = 0; i < totalCells; i++)
            planet.cellHumidity[i] = Mathf.Clamp01(currentHumidity01[i]) * 100f;
    }

    /// <summary>
    /// Get current humidity at a cell (0–1).
    /// </summary>
    public float GetCellHumidity01(int cellIndex)
    {
        if (!IsInitialized)
            return 0f;

        if (cellIndex < 0 || cellIndex >= currentHumidity01.Length)
            return 0f;

        return currentHumidity01[cellIndex];
    }

    private float ComputeHumidityTarget01(int i)
    {
        bool isLand = planet.cellIsLand[i];
        float target = baselineHumidity01[i];

        // --- Temperature coupling block (same as in DoHumidityStep) ---
        if (tempManager != null && baselineTempC != null && i < baselineTempC.Length)
        {
            float currentTemp = tempManager.GetCellTemperature(i); // °C
            float climateTemp = baselineTempC[i];

            float tempDelta = currentTemp - climateTemp;
            if (Mathf.Abs(tempDelta) > 0.001f && maxTempHumidityOffset > 0f)
            {
                float norm = Mathf.Clamp(
                    tempDelta / Mathf.Max(tempDeviationForMaxEffect, 0.0001f),
                    -1f, 1f
                );
                float mag = maxTempHumidityOffset * Mathf.Abs(norm);
                float sign = Mathf.Sign(norm);

                if (isLand)
                {
                    float coastalness = Mathf.Clamp01(landWaterNeighborFraction[i]);
                    float inlandFactor = 1f - coastalness;

                    float inlandEffect = 0f;
                    if (inlandFactor > 0f)
                    {
                        if (sign > 0f) // warmer inland → drier
                            inlandEffect = -mag * inlandTempDryingStrength * inlandFactor;
                        else if (sign < 0f) // colder inland → slightly wetter
                            inlandEffect = mag * inlandTempDryingStrength * 0.5f * inlandFactor;
                    }

                    float coastalEffect = 0f;
                    if (coastalness > 0f)
                    {
                        coastalEffect = mag * coastalTempHumidityStrength * coastalness * sign;
                    }

                    target = Mathf.Clamp01(target + inlandEffect + coastalEffect);
                }
                else
                {
                    // Oceans/lakes
                    float waterEffect = mag * oceanTempHumidityStrength * sign;
                    target = Mathf.Clamp01(target + waterEffect);
                }
            }
        }

        return target;
    }
}
