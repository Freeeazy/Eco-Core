using UnityEngine;

[ExecuteAlways]
public class CSphereBiomeManager : MonoBehaviour
{
    [Header("References")]
    public CubeSphereBlockMesh blockMesh;
    public Material groundMaterial;           // uses SG_CubeSphereGridLit
    public TemperatureManager tempManager;
    public HumidityManager humidityManager;

    [Header("Simulation Settings")]
    [Tooltip("SunRotate used to compute in-game time progression.")]
    public SunRotate sunRotate;

    [Tooltip("Minimum simulated hours before we rebuild the biome map.")]
    public float biomeMinStepHours = 24f;   // once per in-game day by default

    [SerializeField] private float biomeAccumulatedHours = 0f;
    [SerializeField] private float lastDayT = -1f;
    [SerializeField] private float biomeEffectiveMinStepHoursDebug;

    [System.Serializable]
    public class BiomeDefinition
    {
        public string name = "Biome";
        [Tooltip("Color written into the _CellMap for this biome.")]
        public Color color = Color.green;

        [Header("Temperature Range (°C)")]
        public float minTempC = -15f;
        public float maxTempC = 35f;

        [Header("Humidity Range (0–1)")]
        [Range(0f, 1f)] public float minHumidity01 = 0f;
        [Range(0f, 1f)] public float maxHumidity01 = 1f;

        [Header("Filters")]
        [Tooltip("If true, only land cells can use this biome.")]
        public bool landOnly = true;
        [Tooltip("If true, only water cells can use this biome.")]
        public bool waterOnly = false;

        public bool Contains(bool isLand, float tempC, float humidity01)
        {
            if (landOnly && !isLand) return false;
            if (waterOnly && isLand) return false;

            if (tempC < minTempC || tempC > maxTempC) return false;
            if (humidity01 < minHumidity01 || humidity01 > maxHumidity01) return false;

            return true;
        }
    }

    [Header("Biome Table")]
    [Tooltip("Biomes are checked from top to bottom; first match wins.")]
    public BiomeDefinition[] biomes = new BiomeDefinition[10];

    Texture2D cellMap;
    int n;                  // cellsPerFace

    void Reset()
    {
        // Humidity bands (you can tweak these later if you want different splits)
        const float HUM_DRY_MAX = 0.33f;
        const float HUM_MODERATE_MIN = 0.33f;
        const float HUM_MODERATE_MAX = 0.66f;
        const float HUM_WET_MIN = 0.66f;

        // Temperature bands (°C) for "cold / mild / hot"
        const float TEMP_COLD_MIN = -15f;
        const float TEMP_COLD_MAX = 2f;
        const float TEMP_MILD_MIN = 0f;
        const float TEMP_MILD_MAX = 22f;
        const float TEMP_HOT_MIN = 20f;
        const float TEMP_HOT_MAX = 35f;

        biomes = new BiomeDefinition[10];

        // 0) Polar / Ice Caps  (extra cold, any humidity)
        biomes[0] = new BiomeDefinition
        {
            name = "Polar Ice",
            // brighter / whiter than tundra
            color = new Color(0.92f, 0.96f, 1.00f),
            minTempC = -100f,
            maxTempC = -10f,
            minHumidity01 = 0.0f,
            maxHumidity01 = 1.0f,
            landOnly = true
        };

        // ---- COLD row ----

        // 1) Tundra (Cold + Dry)
        biomes[1] = new BiomeDefinition
        {
            name = "Tundra",
            color = new Color(0.80f, 0.84f, 0.88f), // pale grey-blue
            minTempC = TEMP_COLD_MIN,
            maxTempC = TEMP_COLD_MAX,
            minHumidity01 = 0.0f,
            maxHumidity01 = HUM_DRY_MAX,
            landOnly = true
        };

        // 2) Taiga (Cold + Moderate)
        biomes[2] = new BiomeDefinition
        {
            name = "Taiga",
            color = new Color(0.18f, 0.35f, 0.22f), // muted dark green
            minTempC = TEMP_COLD_MIN,
            maxTempC = TEMP_COLD_MAX,
            minHumidity01 = HUM_MODERATE_MIN,
            maxHumidity01 = HUM_MODERATE_MAX,
            landOnly = true
        };

        // 3) Boreal Forest (Cold + Wet)
        biomes[3] = new BiomeDefinition
        {
            name = "Boreal Forest",
            color = new Color(0.08f, 0.28f, 0.26f), // dark teal-green
            minTempC = TEMP_COLD_MIN,
            maxTempC = TEMP_COLD_MAX,
            minHumidity01 = HUM_WET_MIN,
            maxHumidity01 = 1.0f,
            landOnly = true
        };

        // ---- MILD row ----

        // 4) Steppe (Mild + Dry)
        biomes[4] = new BiomeDefinition
        {
            name = "Steppe",
            color = new Color(0.82f, 0.72f, 0.35f), // dusty yellow-brown
            minTempC = TEMP_MILD_MIN,
            maxTempC = TEMP_MILD_MAX,
            minHumidity01 = 0.0f,
            maxHumidity01 = HUM_DRY_MAX,
            landOnly = true
        };

        // 5) Forest (Mild + Moderate)
        biomes[5] = new BiomeDefinition
        {
            name = "Forest",
            color = new Color(0.18f, 0.45f, 0.20f), // medium green
            minTempC = TEMP_MILD_MIN,
            maxTempC = TEMP_MILD_MAX,
            minHumidity01 = HUM_MODERATE_MIN,
            maxHumidity01 = HUM_MODERATE_MAX,
            landOnly = true
        };

        // 6) Rainforest (Mild + Wet)
        biomes[6] = new BiomeDefinition
        {
            name = "Rainforest",
            color = new Color(0.10f, 0.55f, 0.20f), // bright lush green
            minTempC = TEMP_MILD_MIN,
            maxTempC = TEMP_MILD_MAX,
            minHumidity01 = HUM_WET_MIN,
            maxHumidity01 = 1.0f,
            landOnly = true
        };

        // ---- HOT row ----

        // 7) Desert (Hot + Dry)
        biomes[7] = new BiomeDefinition
        {
            name = "Desert",
            color = new Color(0.93f, 0.84f, 0.45f), // sandy
            minTempC = TEMP_HOT_MIN,
            maxTempC = TEMP_HOT_MAX,
            minHumidity01 = 0.0f,
            maxHumidity01 = HUM_DRY_MAX,
            landOnly = true
        };

        // 8) Savanna (Hot + Moderate)
        biomes[8] = new BiomeDefinition
        {
            name = "Savanna",
            color = new Color(0.80f, 0.70f, 0.30f), // golden grass
            minTempC = TEMP_HOT_MIN,
            maxTempC = TEMP_HOT_MAX,
            minHumidity01 = HUM_MODERATE_MIN,
            maxHumidity01 = HUM_MODERATE_MAX,
            landOnly = true
        };

        // 9) Jungle (Hot + Wet)
        biomes[9] = new BiomeDefinition
        {
            name = "Jungle",
            color = new Color(0.05f, 0.45f, 0.12f), // deep dense green
            minTempC = TEMP_HOT_MIN,
            maxTempC = TEMP_HOT_MAX,
            minHumidity01 = HUM_WET_MIN,
            maxHumidity01 = 1.0f,
            landOnly = true
        };
    }

    void Start()
    {
        TryInitTexture();
    }

    void Update()
    {
        // Make sure texture + references are valid
        if (!TryInitTexture())
            return;

        if (!tempManager || !humidityManager || !sunRotate)
            return;

        // Make sure temp/humidity systems are initialized
        if (!tempManager.EnsureInitialized())
            return;

        if (!humidityManager.EnsureInitialized())
            return;

        // --- Time accumulation (same idea as TemperatureManager) ---
        float currentDayT = sunRotate.GetTimeOfDay(); // 0..1 over a full day

        // First frame: just capture baseline
        if (lastDayT < 0f)
        {
            lastDayT = currentDayT;
            return;
        }

        float deltaDay = currentDayT - lastDayT;
        if (deltaDay < 0f)
            deltaDay += 1f; // wrap 1 -> 0

        float simDeltaHours = deltaDay * 24f;
        biomeAccumulatedHours += simDeltaHours;

        // Let timeScale stretch the step the same way TemperatureManager does
        float ts = Mathf.Max(sunRotate.timeScale, 0.0001f);
        float effectiveMinStepHours = biomeMinStepHours * ts;
        biomeEffectiveMinStepHoursDebug = effectiveMinStepHours;

        if (biomeAccumulatedHours >= effectiveMinStepHours)
        {
            // Rebuild biomes based on the *current* temp + humidity fields
            BuildBiomeMap();
            biomeAccumulatedHours = 0f;
        }

        lastDayT = currentDayT;
    }

    bool TryInitTexture()
    {
        if (!blockMesh || !groundMaterial)
            return false;

        int totalCells = blockMesh.TotalCells;
        if (totalCells <= 0)
            return false;

        n = blockMesh.CellsPerFace;

        if (cellMap == null || cellMap.width != n || cellMap.height != n * 6)
        {
            cellMap = new Texture2D(n, n * 6, TextureFormat.RGBA32, false);
            cellMap.filterMode = FilterMode.Point;
            cellMap.wrapMode = TextureWrapMode.Clamp;
            groundMaterial.SetTexture("_CellMap", cellMap);
        }

        return true;
    }

    void BuildBiomeMap()
    {
        int totalCells = blockMesh.TotalCells;

        for (int cell = 0; cell < totalCells; cell++)
        {
            bool isLand = blockMesh.cellIsLand[cell];

            Color c;

            if (!isLand)
            {
                // Let ocean material handle water; keep this black.
                c = Color.black;
            }
            else
            {
                float tempC = 0f;
                float humidity01 = 0f;

                if (tempManager && tempManager.IsInitialized)
                    tempC = tempManager.GetCellTemperature(cell);

                if (humidityManager && humidityManager.IsInitialized)
                    humidity01 = humidityManager.GetCellHumidity01(cell);

                c = EvaluateBiomeColor(isLand, tempC, humidity01);
            }

            var (px, py) = CellToPixel(cell);
            cellMap.SetPixel(px, py, c);
        }

        cellMap.Apply();
        groundMaterial.SetTexture("_CellMap", cellMap);
    }

    Color EvaluateBiomeColor(bool isLand, float tempC, float humidity01)
    {
        if (biomes != null)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                var b = biomes[i];
                if (b != null && b.Contains(isLand, tempC, humidity01))
                    return b.color;
            }
        }

        // Fallback so "unmapped" regions are obvious.
        return Color.magenta;
    }

    (int x, int y) CellToPixel(int cell)
    {
        int n2 = n * n;
        int face = cell / n2;
        int idx = cell % n2;
        int cy = idx / n;
        int cx = idx % n;

        int px = cx;
        int py = face * n + cy; // 6 faces stacked vertically
        return (px, py);
    }

    // Handy if you ever want to debug-paint a single cell.
    public void PaintCell(int cell, Color color)
    {
        if (cellMap == null) return;
        var (px, py) = CellToPixel(cell);
        cellMap.SetPixel(px, py, color);
        cellMap.Apply();
    }
    public string GetBiomeName(int cellIndex)
    {
        if (biomes == null || cellIndex < 0 || cellIndex >= blockMesh.TotalCells)
            return "Unknown";

        bool isLand = blockMesh.cellIsLand[cellIndex];

        float tempC = tempManager && tempManager.IsInitialized
            ? tempManager.GetCellTemperature(cellIndex)
            : 0f;

        float humidity01 = humidityManager && humidityManager.IsInitialized
            ? humidityManager.GetCellHumidity01(cellIndex)
            : 0f;

        for (int i = 0; i < biomes.Length; i++)
        {
            var b = biomes[i];
            if (b != null && b.Contains(isLand, tempC, humidity01))
                return b.name;
        }

        return "Unassigned";
    }
}
