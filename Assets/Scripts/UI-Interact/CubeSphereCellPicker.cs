using UnityEngine;
using TMPro;

[RequireComponent(typeof(Camera))]
public class CubeSphereCellPicker : MonoBehaviour
{
    [Header("Planet")]
    [SerializeField] private CubeSphereBlockMesh planet;
    [SerializeField] private MeshCollider planetCollider;

    [Header("Managers")]
    [SerializeField] private TemperatureManager temperatureManager;
    [SerializeField] private CSphereBiomeManager biomeManager;

    [Header("Highlight Material (your ground material that uses the shadergraph)")]
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material waterMaterial;

    [Header("Shader Property Names")]
    [SerializeField] private string hoverCellProp = "_HoverCell";
    [SerializeField] private string selectedCellProp = "_SelectedCell";

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI cellLocationText;
    [SerializeField] private TextMeshProUGUI cellFaceText;
    [SerializeField] private TextMeshProUGUI cellLatText;
    [SerializeField] private TextMeshProUGUI cellTempText;
    [SerializeField] private TextMeshProUGUI cellHumidityText;
    [SerializeField] private TextMeshProUGUI cellBiomeText;

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 10000f;
    [SerializeField] private LayerMask raycastMask = ~0;

    private Camera cam;

    private Mesh cachedMesh;
    private int[] cachedTris;

    private int hoveredCellIndex = -1;
    private int selectedCellIndex = -1;

    public int SelectedCellIndex => selectedCellIndex;
    public int HoveredCellIndex => hoveredCellIndex;
    public CubeSphereBlockMesh Planet => planet;
    public MeshCollider PlanetCollider => planetCollider;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // auto-find collider if not wired
        if (!planetCollider && planet)
            planetCollider = planet.GetComponent<MeshCollider>();

        CacheMeshData();
        PushHoverSelectedToMaterial();
    }

    private void CacheMeshData()
    {
        if (!planetCollider) return;

        cachedMesh = planetCollider.sharedMesh;
        if (cachedMesh != null)
            cachedTris = cachedMesh.triangles; // cache once so hover doesn’t allocate/work hard
    }

    private void Update()
    {
        // In case mesh regenerates at runtime (rare, but can happen in editor/ExecuteAlways workflows)
        if (planetCollider && planetCollider.sharedMesh != cachedMesh)
            CacheMeshData();

        // Hover every frame
        hoveredCellIndex = GetCellUnderMouse(Input.mousePosition);

        // Click selects
        if (Input.GetMouseButtonDown(0) && hoveredCellIndex >= 0)
        {
            selectedCellIndex = hoveredCellIndex;
            UpdateCellInfo(selectedCellIndex); // refresh UI on click
        }

        // Keep UI updating for selected cell (your existing behavior)
        if (selectedCellIndex >= 0)
            UpdateCellInfo(selectedCellIndex);

        // Feed highlight ints to shader
        PushHoverSelectedToMaterial();
    }

    private int GetCellUnderMouse(Vector3 screenPos)
    {
        if (!planet || !planetCollider || cachedMesh == null || cachedTris == null || cachedTris.Length == 0)
            return -1;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
            return -1;

        if (hit.collider != planetCollider)
            return -1;

        int triStart = hit.triangleIndex * 3;
        if (triStart + 2 >= cachedTris.Length)
            return -1;

        int v0 = cachedTris[triStart + 0];
        int v1 = cachedTris[triStart + 1];
        int v2 = cachedTris[triStart + 2];

        // Each cell uses 8 vertices in order -> decode cell index (your exact logic) :contentReference[oaicite:1]{index=1}
        int cellVertexIndex = Mathf.Min(v0, Mathf.Min(v1, v2));
        int cellIndex = cellVertexIndex / 8;

        // Safety clamp
        if (cellIndex < 0 || cellIndex >= planet.TotalCells)
            return -1;

        return cellIndex;
    }

    private void PushHoverSelectedToMaterial()
    {
        if (!groundMaterial) return;

        groundMaterial.SetInt(hoverCellProp, hoveredCellIndex);
        groundMaterial.SetInt(selectedCellProp, selectedCellIndex);

        if (!waterMaterial) return;

        waterMaterial.SetInt(hoverCellProp, hoveredCellIndex);
        waterMaterial.SetInt(selectedCellProp, selectedCellIndex);
    }

    /// <summary>
    /// Updates the debug UI for a given cell index (location, face, latitude, temperature).
    /// Called every frame for the last clicked cell.
    /// </summary>
    private void UpdateCellInfo(int cellIndex)
    {
        if (!planet)
            return;

        int totalCells = planet.TotalCells;
        if (cellIndex < 0 || cellIndex >= totalCells)
            return;

        int steps = Mathf.Max(1, planet.cellsPerFace);
        int cellsPerFace = steps * steps;

        int faceIndex = cellIndex / cellsPerFace;
        int indexInFace = cellIndex % cellsPerFace;

        int y = indexInFace / steps;
        int x = indexInFace % steps;

        if (cellLocationText)
            cellLocationText.text = $"Cell: ({x}, {y})";

        if (cellFaceText)
            cellFaceText.text = $"Face: {faceIndex + 1}";

        if (cellLatText && planet.cellLatitude != null && cellIndex < planet.cellLatitude.Length)
        {
            // dir.y is sin(latitude); convert to degrees.
            float sinLat = Mathf.Clamp(planet.cellLatitude[cellIndex], -1f, 1f);
            float latDeg = Mathf.Asin(sinLat) * Mathf.Rad2Deg;
            cellLatText.text = $"Lat: {latDeg:F1}°";
        }

        if (cellTempText && temperatureManager && temperatureManager.IsInitialized)
        {
            float tempC = temperatureManager.GetCellTemperature(cellIndex);
            float tempF = (tempC * 9f / 5f) + 32f;

            cellTempText.text = $"Temp: {tempC:F1} °C / {tempF:F1} °F";
        }

        if (cellHumidityText && planet.cellHumidity != null && cellIndex < planet.cellHumidity.Length)
        {
            float hum = planet.cellHumidity[cellIndex]; // already 0–100
            cellHumidityText.text = $"Humidity: {hum:F1} %";
        }

        if (cellBiomeText && biomeManager)
        {
            string biome = biomeManager.GetBiomeName(cellIndex);
            cellBiomeText.text = $"Biome: {biome}";
        }
    }
}
