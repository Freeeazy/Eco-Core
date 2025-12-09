using UnityEngine;

public class HumidityManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Planet mesh that holds per-cell direction / elevation / land flags.")]
    public CubeSphereBlockMesh planet;

    [Tooltip("SunRotate controller (for timeScale, day/year progression).")]
    public SunRotate sunRotate;

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

    [Tooltip("Land responds faster (1 = same as base, >1 = faster).")]
    public float landResponseMultiplier = 1.0f;

    [Tooltip("Water responds slower (1 = same as base, <1 = slower).")]
    public float waterResponseMultiplier = 0.5f;

    [Tooltip("Minimum simulated hours to accumulate before doing a humidity update step.")]
    public float minStepHours = 0.1f;   // ~6 in-game minutes at 1x

    [Header("Debug")]
    [SerializeField] private float accumulatedSimHours = 0f;
    [SerializeField] private float lastTimeOfDay = -1f;
    [SerializeField] private float effectiveMinStepHoursDebug;

    // Internal arrays (0–1 range for humidity)
    private float[] baselineHumidity01;
    private float[] currentHumidity01;

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

        // Same trick as TemperatureManager: scale step size by timeScale so cost stays stable. :contentReference[oaicite:0]{index=0}
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

        if (baselineHumidity01 == null || baselineHumidity01.Length != totalCells)
        {
            baselineHumidity01 = new float[totalCells];
            currentHumidity01 = new float[totalCells];

            BuildBaselineHumidity();
            ApplyCoastalBoost();

            // Start current = baseline
            for (int i = 0; i < totalCells; i++)
            {
                currentHumidity01[i] = baselineHumidity01[i];
            }

            // Write initial humidity (0–100) back to planet for shaders / debug
            if (planet.cellHumidity == null || planet.cellHumidity.Length != totalCells)
            {
                planet.cellHumidity = new float[totalCells];
            }
            for (int i = 0; i < totalCells; i++)
            {
                planet.cellHumidity[i] = currentHumidity01[i] * 100f;
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
        int steps = planet.CellsPerFace;

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
    /// Second pass: land cells near water get a coastal humidity boost.
    /// </summary>
    private void ApplyCoastalBoost()
    {
        int totalCells = planet.TotalCells;
        int steps = planet.CellsPerFace;

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

            if (neighborCount > 0 && waterNeighbors > 0 && coastalBoostMax > 0f)
            {
                float waterFraction = (float)waterNeighbors / neighborCount; // 0..1
                baselineHumidity01[i] = Mathf.Clamp01(
                    baselineHumidity01[i] + waterFraction * coastalBoostMax
                );
            }
        }
    }

    /// <summary>
    /// Sim step: for now, humidity just relaxes toward the baseline field.
    /// Later we can fold in temperature, evaporation, etc.
    /// </summary>
    private void DoHumidityStep(float deltaHours)
    {
        int totalCells = planet.TotalCells;

        for (int i = 0; i < totalCells; i++)
        {
            bool isLand = planet.cellIsLand[i];

            float target = baselineHumidity01[i]; // dynamic stuff can be layered on later
            float response = baseResponsePerHour *
                             (isLand ? landResponseMultiplier : waterResponseMultiplier);

            currentHumidity01[i] += (target - currentHumidity01[i]) * response * deltaHours;
        }

        // Write back to planet in 0–100 for shaders / other systems
        for (int i = 0; i < totalCells; i++)
        {
            planet.cellHumidity[i] = Mathf.Clamp01(currentHumidity01[i]) * 100f;
        }
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
}
