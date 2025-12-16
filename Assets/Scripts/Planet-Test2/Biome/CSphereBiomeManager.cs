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

    [Header("Biome Hysteresis / Inertia")]
    [Tooltip("How many in-game DAYS a cell must stay in a NEW biome's range before switching.")]
    public float daysToSwitchBiome = 10f;

    [Tooltip("How many in-game DAYS to 'recover' back toward stable (prevents flip-flop). Lower = faster recovery.")]
    public float daysToRecover = 5f;

    [Tooltip("If true, we only allow switching to the single best-matching biome (instead of first match).")]
    public bool useBestMatchInsteadOfFirst = false;

    [Header("Biome Visualization")]
    [Tooltip("If true, cells that are transitioning will blend between current and candidate biome colors.")]
    public bool visualizeTransitions = true;

    [Range(0f, 1f)]
    [Tooltip("Optional: ease the blend so early progress is more visible (0=linear, 1=more eased).")]
    public float transitionEase = 0.35f;

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

        // Used only if useBestMatchInsteadOfFirst = true
        public float Score(bool isLand, float tempC, float humidity01)
        {
            if (landOnly && !isLand) return -1f;
            if (waterOnly && isLand) return -1f;

            // If outside range, hard reject
            if (tempC < minTempC || tempC > maxTempC) return -1f;
            if (humidity01 < minHumidity01 || humidity01 > maxHumidity01) return -1f;

            // Prefer center of the range (simple “distance to center” score)
            float tCenter = 0.5f * (minTempC + maxTempC);
            float hCenter = 0.5f * (minHumidity01 + maxHumidity01);

            float tHalf = Mathf.Max(0.0001f, 0.5f * (maxTempC - minTempC));
            float hHalf = Mathf.Max(0.0001f, 0.5f * (maxHumidity01 - minHumidity01));

            float tNorm = Mathf.Abs(tempC - tCenter) / tHalf;        // 0 center -> 1 edge
            float hNorm = Mathf.Abs(humidity01 - hCenter) / hHalf;

            // Higher is better
            return 1f - 0.5f * (tNorm + hNorm);
        }
    }

    [Header("Biome Table")]
    [Tooltip("Biomes are checked from top to bottom; first match wins.")]
    public BiomeDefinition[] biomes = new BiomeDefinition[10];

    Texture2D cellMap;
    int n;                  // cellsPerFace

    // --- Hysteresis state per cell ---
    private int[] currentBiomeIndex;      // committed biome
    private int[] candidateBiomeIndex;    // target we are “considering”
    private float[] candidateProgress01;  // 0..1 progress toward switching

    private bool didInitialBuild = false;

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
        TryEnsureBiomeStateArrays();
        TryInitialBuild();
    }
    void OnEnable()
    {
        // Helpful when entering play mode / reloading scripts with ExecuteAlways
        TryInitTexture();
        TryEnsureBiomeStateArrays();
        TryInitialBuild();
    }
    void Update()
    {
        if (!TryInitTexture())
            return;

        if (!tempManager || !humidityManager || !sunRotate)
            return;

        // Ensure upstream systems
        if (!tempManager.EnsureInitialized())
            return;

        if (!humidityManager.EnsureInitialized())
            return;

        TryEnsureBiomeStateArrays();
        TryInitialBuild();

        // --- Time accumulation ---
        float currentDayT = sunRotate.GetTimeOfDay(); // 0..1 over a full day

        // First frame after initialization: capture baseline but DO NOT early-return forever.
        if (lastDayT < 0f)
        {
            lastDayT = currentDayT;
            biomeAccumulatedHours = 0f;
            return;
        }

        float deltaDay = currentDayT - lastDayT;
        if (deltaDay < 0f) deltaDay += 1f;

        float simDeltaHours = deltaDay * 24f;
        biomeAccumulatedHours += simDeltaHours;

        bool didAnyBiomeStep = false;

        // --- FIXED-STEP BIOME UPDATES (exact 24h chunks) ---
        while (biomeAccumulatedHours >= biomeMinStepHours)
        {
            DoBiomeStep(biomeMinStepHours); // always exactly 24h
            biomeAccumulatedHours -= biomeMinStepHours;
            didAnyBiomeStep = true;
        }

        // --- Paint once per frame if anything changed ---
        if (didAnyBiomeStep)
        {
            PaintBiomeMapFromCommitted();
        }

        lastDayT = currentDayT;
    }

    private void TryInitialBuild()
    {
        if (didInitialBuild) return;
        if (!blockMesh || blockMesh.TotalCells <= 0) return;
        if (!tempManager || !tempManager.IsInitialized) return;
        if (!humidityManager || !humidityManager.IsInitialized) return;

        // Commit initial biomes immediately (no waiting for first step)
        InitializeCommittedBiomesFromCurrentClimate();
        PaintBiomeMapFromCommitted();

        didInitialBuild = true;
    }

    private bool TryInitTexture()
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
    private void TryEnsureBiomeStateArrays()
    {
        if (!blockMesh) return;

        int totalCells = blockMesh.TotalCells;
        if (totalCells <= 0) return;

        if (currentBiomeIndex == null || currentBiomeIndex.Length != totalCells)
        {
            currentBiomeIndex = new int[totalCells];
            candidateBiomeIndex = new int[totalCells];
            candidateProgress01 = new float[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                currentBiomeIndex[i] = -1;
                candidateBiomeIndex[i] = -1;
                candidateProgress01[i] = 0f;
            }

            didInitialBuild = false; // force rebuild after resize/regenerate
            lastDayT = -1f;          // resync time
            biomeAccumulatedHours = 0f;
        }
    }
    private void InitializeCommittedBiomesFromCurrentClimate()
    {
        int totalCells = blockMesh.TotalCells;

        for (int cell = 0; cell < totalCells; cell++)
        {
            if (!blockMesh.cellIsLand[cell])
            {
                currentBiomeIndex[cell] = -1;
                candidateBiomeIndex[cell] = -1;
                candidateProgress01[cell] = 0f;
                continue;
            }

            float tempC = tempManager.GetCellTemperature(cell);
            float hum01 = humidityManager.GetCellHumidity01(cell);

            int b = EvaluateBiomeIndex(true, tempC, hum01);
            currentBiomeIndex[cell] = b;
            candidateBiomeIndex[cell] = -1;
            candidateProgress01[cell] = 0f;
        }
    }

    private void DoBiomeStep(float stepHours)
    {
        int totalCells = blockMesh.TotalCells;
        float stepDays = stepHours / 24f;

        float switchRate = (daysToSwitchBiome <= 0.0001f) ? 9999f : (stepDays / daysToSwitchBiome);
        float recoverRate = (daysToRecover <= 0.0001f) ? 9999f : (stepDays / daysToRecover);

        for (int cell = 0; cell < totalCells; cell++)
        {
            if (!blockMesh.cellIsLand[cell])
                continue;

            float tempC = tempManager.GetCellTemperature(cell);
            float hum01 = humidityManager.GetCellHumidity01(cell);

            int target = EvaluateBiomeIndex(true, tempC, hum01);
            int current = currentBiomeIndex[cell];

            // If we have no committed biome yet, snap immediately.
            if (current < 0)
            {
                currentBiomeIndex[cell] = target;
                candidateBiomeIndex[cell] = -1;
                candidateProgress01[cell] = 0f;
                continue;
            }

            // If target is same as current, decay any pending transition (recovery).
            if (target == current)
            {
                candidateBiomeIndex[cell] = -1;
                candidateProgress01[cell] = Mathf.Max(0f, candidateProgress01[cell] - recoverRate);
                continue;
            }

            // Target differs: accumulate toward switching.
            if (candidateBiomeIndex[cell] != target)
            {
                // New candidate (conditions changed) -> reset progress
                candidateBiomeIndex[cell] = target;
                candidateProgress01[cell] = 0f;
            }

            candidateProgress01[cell] += switchRate;

            if (candidateProgress01[cell] >= 1f)
            {
                currentBiomeIndex[cell] = target;
                candidateBiomeIndex[cell] = -1;
                candidateProgress01[cell] = 0f;
            }
        }
    }
    private void PaintBiomeMapFromCommitted()
    {
        int totalCells = blockMesh.TotalCells;

        for (int cell = 0; cell < totalCells; cell++)
        {
            Color c;

            if (!blockMesh.cellIsLand[cell])
            {
                c = Color.black; // water handled elsewhere
            }
            else
            {
                int cur = currentBiomeIndex != null ? currentBiomeIndex[cell] : -1;

                // Base committed color
                Color curCol = (cur >= 0 && biomes != null && cur < biomes.Length && biomes[cur] != null)
                    ? biomes[cur].color
                    : Color.magenta;

                if (!visualizeTransitions)
                {
                    c = curCol;
                }
                else
                {
                    int cand = (candidateBiomeIndex != null) ? candidateBiomeIndex[cell] : -1;
                    float p = (candidateProgress01 != null) ? candidateProgress01[cell] : 0f;

                    // If we have an active candidate and some progress, blend toward it
                    if (cand >= 0 && p > 0f && biomes != null && cand < biomes.Length && biomes[cand] != null)
                    {
                        Color candCol = biomes[cand].color;

                        // Ease so you see motion earlier (optional, but feels nicer)
                        float eased = p;
                        if (transitionEase > 0f)
                        {
                            // simple ease-in-out-ish curve
                            float smooth = p * p * (3f - 2f * p); // SmoothStep
                            eased = Mathf.Lerp(p, smooth, transitionEase);
                        }

                        c = Color.Lerp(curCol, candCol, eased);
                    }
                    else
                    {
                        c = curCol;
                    }
                }
            }

            var (px, py) = CellToPixel(cell);
            cellMap.SetPixel(px, py, c);
        }

        cellMap.Apply();
        groundMaterial.SetTexture("_CellMap", cellMap);
    }
    private int EvaluateBiomeIndex(bool isLand, float tempC, float humidity01)
    {
        if (biomes == null || biomes.Length == 0)
            return -1;

        if (!useBestMatchInsteadOfFirst)
        {
            for (int i = 0; i < biomes.Length; i++)
            {
                var b = biomes[i];
                if (b != null && b.Contains(isLand, tempC, humidity01))
                    return i;
            }
            return -1;
        }
        else
        {
            int best = -1;
            float bestScore = -1f;

            for (int i = 0; i < biomes.Length; i++)
            {
                var b = biomes[i];
                if (b == null) continue;

                float s = b.Score(isLand, tempC, humidity01);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }

            return best;
        }
    }

    private (int x, int y) CellToPixel(int cell)
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
        int cur = (currentBiomeIndex != null && cellIndex < currentBiomeIndex.Length)
            ? currentBiomeIndex[cellIndex]
            : -1;

        int cand = (candidateBiomeIndex != null && cellIndex < candidateBiomeIndex.Length)
            ? candidateBiomeIndex[cellIndex]
            : -1;

        float p = (candidateProgress01 != null && cellIndex < candidateProgress01.Length)
            ? candidateProgress01[cellIndex]
            : 0f;

        string curName = (cur >= 0 && cur < biomes.Length && biomes[cur] != null) ? biomes[cur].name : "Unassigned";

        if (cand >= 0 && p > 0f && cand < biomes.Length && biomes[cand] != null)
        {
            string candName = biomes[cand].name;
            int pct = Mathf.RoundToInt(p * 100f);
            return $"{curName} -> {candName} ({pct}%)";
        }

        return curName;
    }
}
