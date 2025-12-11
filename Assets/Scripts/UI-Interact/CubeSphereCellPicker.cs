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
    private int lastCellIndex = -1;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // auto-find collider if not wired
        if (!planetCollider && planet)
            planetCollider = planet.GetComponent<MeshCollider>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick(Input.mousePosition);

        if (lastCellIndex >= 0)
        {
            UpdateCellInfo(lastCellIndex);
        }
    }

    private void HandleClick(Vector3 screenPos)
    {
        if (!planet || !planetCollider) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask))
        {
            if (hit.collider != planetCollider)
                return;

            Mesh mesh = planetCollider.sharedMesh;
            if (!mesh) return;

            // Use main triangles only (submesh 0 indices)
            int[] tris = mesh.triangles;

            int triStart = hit.triangleIndex * 3;
            int v0 = tris[triStart + 0];
            int v1 = tris[triStart + 1];
            int v2 = tris[triStart + 2];

            // Each cell uses 8 vertices in order -> decode cell index
            int cellVertexIndex = Mathf.Min(v0, Mathf.Min(v1, v2));
            int cellIndex = cellVertexIndex / 8;

            // Store for continuous updates
            lastCellIndex = cellIndex;

            // Immediately refresh UI once on click
            UpdateCellInfo(cellIndex);
        }
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
