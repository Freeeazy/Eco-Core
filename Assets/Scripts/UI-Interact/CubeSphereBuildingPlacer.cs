using System.Collections.Generic;
using UnityEngine;

public class CubeSphereBuildingPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CubeSphereCellPicker picker;
    [SerializeField] private CubeSphereBlockMesh planet;

    [Header("Placement")]
    [SerializeField] private KeyCode placeKey = KeyCode.B;
    [SerializeField] private List<BuildingOption> buildingOptions = new List<BuildingOption>();
    [SerializeField] private int selectedBuildingIndex = 0;
    [SerializeField] private Transform buildingsRoot;        // optional parent for cleanliness
    [SerializeField] private bool forbidWaterCells = false;  // optional rule

    // cellIndex -> spawned building
    private readonly Dictionary<int, GameObject> placed = new Dictionary<int, GameObject>();

    [System.Serializable]
    public class BuildingOption
    {
        public string name = "Building";
        public KeyCode hotkey = KeyCode.Alpha1;
        public GameObject prefab;

        [Header("Spawn Tweaks")]
        public Vector3 localScale = Vector3.one;

        [Tooltip("Extra yaw rotation (degrees) around the tile's up axis.")]
        public float yawDegrees = 0f;

        [Tooltip("Extra height offset along the tile normal, on top of global extraSurfaceOffset.")]
        public float extraHeight = 0f;
    }

    private void Awake()
    {
        if (!picker) picker = FindFirstObjectByType<CubeSphereCellPicker>();
        if (!planet && picker) planet = picker.Planet;
        if (!planet) planet = FindFirstObjectByType<CubeSphereBlockMesh>();

        if (!buildingsRoot)
        {
            var go = new GameObject("BuildingsRoot");
            buildingsRoot = go.transform;
        }
    }

    private void Update()
    {
        // select building option with hotkeys
        for (int i = 0; i < buildingOptions.Count; i++)
        {
            if (Input.GetKeyDown(buildingOptions[i].hotkey))
                selectedBuildingIndex = i;
        }

        if (Input.GetKeyDown(placeKey))
            TryPlaceOnSelectedCell();
    }

    private void TryPlaceOnSelectedCell()
    {
        if (!picker || !planet)
            return;

        if (buildingOptions == null || buildingOptions.Count == 0)
            return;

        selectedBuildingIndex = Mathf.Clamp(selectedBuildingIndex, 0, buildingOptions.Count - 1);
        BuildingOption opt = buildingOptions[selectedBuildingIndex];

        if (opt.prefab == null)
            return;

        int cellIndex = picker.SelectedCellIndex;
        if (cellIndex < 0 || cellIndex >= planet.TotalCells)
            return;

        // Already occupied?
        if (placed.ContainsKey(cellIndex) && placed[cellIndex] != null)
            return;

        // Optional: forbid water
        if (forbidWaterCells && planet.cellIsLand != null && cellIndex < planet.cellIsLand.Length)
        {
            if (!planet.cellIsLand[cellIndex])
                return;
        }

        Vector3 surfacePos = planet.cellSurfaceCenterWS[cellIndex];
        Quaternion rot = planet.cellSurfaceRotationWS[cellIndex];

        // apply per-building yaw on top
        Quaternion yaw = Quaternion.AngleAxis(opt.yawDegrees, rot * Vector3.up);
        Quaternion finalRot = yaw * rot;

        GameObject b = Instantiate(opt.prefab, surfacePos, finalRot, buildingsRoot);
        b.transform.localScale = opt.localScale;

        // Optional small constant lift so you never clip:
        b.transform.position += (finalRot * Vector3.up) * opt.extraHeight;

        placed[cellIndex] = b;
    }

    // Conservative “good enough” fit for now:
    // Uses world AABB size and projects it onto 'up' so the object won't sink into the surface.
    private float ComputeHalfHeightAlongUp(GameObject obj, Vector3 up)
    {
        // Try collider first
        var col = obj.GetComponentInChildren<Collider>();
        if (col != null)
            return ProjectAabbHalfExtent(col.bounds, up);

        // Fallback to renderer
        var r = obj.GetComponentInChildren<Renderer>();
        if (r != null)
            return ProjectAabbHalfExtent(r.bounds, up);

        // Absolute fallback if nothing exists
        return 0.5f;
    }

    private float ProjectAabbHalfExtent(Bounds b, Vector3 dir)
    {
        dir.Normalize();
        Vector3 size = b.size;

        // Approx half-extent along direction, using AABB (safe-ish for rotated cubes)
        float half =
            0.5f * (Mathf.Abs(dir.x) * size.x +
                    Mathf.Abs(dir.y) * size.y +
                    Mathf.Abs(dir.z) * size.z);

        return half;
    }
}
