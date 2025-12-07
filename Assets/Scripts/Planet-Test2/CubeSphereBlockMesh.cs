using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6-face cube-to-sphere block shell:
/// - Treats the planet as a dice (6 faces)
/// - Each face is split into cellsPerFace x cellsPerFace
/// - Each cell becomes a curved "block" (prism) built from its 4 corner points
/// - Everything in a single combined mesh (no prefabs needed)
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CubeSphereBlockMesh : MonoBehaviour
{
    [Header("Planet")]
    [Tooltip("Center of the planet. If null, uses this GameObject's transform.")]
    public Transform planetCenter;
    public float radius = 50f;          // Sphere radius at "mean" ground level

    [Header("Grid")]
    [Range(1, 128)]
    public int cellsPerFace = 8;        // Number of cells per side per face

    [Header("Blocks")]
    [Tooltip("Thickness of the block shell (radially outward).")]
    public float blockHeight = 1f;      // How far tops extend beyond base radius

    [Header("Noise - Terrain (local height variation)")]
    public float noiseFrequency = 2.0f;    // how big local terrain features are
    public float noiseAmplitude = 2.0f;    // max +/- offset from base radius
    public int noiseSeed = 0;              // terrain randomness
    public float TerrainBias = 0.05f;

    [Header("Noise - Continents (land vs water mask)")]
    [Tooltip("Frequency for big continental blobs (lower = larger continents).")]
    public float continentFrequency = 0.3f;
    public int continentSeed = 1;
    [Range(0f, 1f)] public float OceanRatio = 0.5f;

    [Header("Water / Sea Level")]
    [Tooltip("Offset from radius where sea level sits. Sea radius = radius + seaLevelOffset.")]
    public float seaLevelOffset = 0f;   // 0 = sea at radius; negative = lower sea; positive = higher

    // Per-cell data (for temperature / climate)
    [Header("Generated Per-Cell Data")]
    [Tooltip("Unit direction from planet center through cell center.")]
    [HideInInspector] public Vector3[] cellCenterDirection;

    [Tooltip("Latitude factor per cell (dir.y, i.e. sin(latitude)).")]
    [HideInInspector] public float[] cellLatitude;

    [Tooltip("Elevation relative to base radius (cellBaseRadius - radius).")]
    [HideInInspector] public float[] cellElevation;

    [Tooltip("True if this cell is land; false if water.")]
    [HideInInspector] public bool[] cellIsLand;

    [Tooltip("Relative humidity per cell, 0–100.")]
    [HideInInspector] public float[] cellHumidity;
    public int TotalCells => cellCenterDirection != null ? cellCenterDirection.Length : 0;

    // Directions for the 6 cube faces (like a dice)
    private static readonly Vector3[] faceDirections =
    {
        Vector3.up,       // 0: +Y
        Vector3.down,     // 1: -Y
        Vector3.left,     // 2: -X
        Vector3.right,    // 3: +X
        Vector3.forward,  // 4: +Z
        Vector3.back      // 5: -Z
    };

    private MeshFilter meshFilter;

    [ContextMenu("Generate Block Planet")]
    public void Generate()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        Transform center = planetCenter != null ? planetCenter : transform;

        int steps = Mathf.Max(1, cellsPerFace);
        int totalCells = 6 * steps * steps;

        // Allocate / reallocate per-cell data
        cellCenterDirection = new Vector3[totalCells];
        cellLatitude = new float[totalCells];
        cellElevation = new float[totalCells];
        cellIsLand = new bool[totalCells];
        cellHumidity = new float[totalCells];

        // Each cell: 8 vertices (4 bottom, 4 top)
        int vertsPerCell = 8;

        Vector3[] vertices = new Vector3[totalCells * vertsPerCell];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Triangles stored per submesh: 0 = land, 1 = water
        List<int> landTriangles = new List<int>(totalCells * 6 * 3);
        List<int> waterTriangles = new List<int>(totalCells * 6 * 3);

        int vertIndex = 0;
        float seaRadius = radius + seaLevelOffset;

        // Loop over faces
        for (int face = 0; face < 6; face++)
        {
            Vector3 localUp = faceDirections[face];

            // Build face-local tangent axes
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            for (int y = 0; y < steps; y++)
            {
                for (int x = 0; x < steps; x++)
                {
                    // Compute linear cell index (matches generation order & picker decoding)
                    int cellIndex = face * (steps * steps) + y * steps + x;

                    // Normalized 0–1 cell coords in UV
                    float u0 = (float)x / steps;
                    float v0 = (float)y / steps;
                    float u1 = (float)(x + 1) / steps;
                    float v1 = (float)(y + 1) / steps;

                    // 4 corners on the cube face in [-1,1]² space
                    Vector3 c00 = FacePoint(localUp, axisA, axisB, u0, v0); // bottom-left
                    Vector3 c10 = FacePoint(localUp, axisA, axisB, u1, v0); // bottom-right
                    Vector3 c11 = FacePoint(localUp, axisA, axisB, u1, v1); // top-right
                    Vector3 c01 = FacePoint(localUp, axisA, axisB, u0, v1); // top-left

                    // Map cube corners to spherified-cube directions
                    Vector3 d00 = CubeToSphere(c00);
                    Vector3 d10 = CubeToSphere(c10);
                    Vector3 d11 = CubeToSphere(c11);
                    Vector3 d01 = CubeToSphere(c01);

                    // --- Per-cell noise & classification ---

                    Vector3 dCenter = (d00 + d10 + d11 + d01) * 0.25f;
                    dCenter.Normalize();

                    // Store center direction & latitude factor for later (temperature, etc.)
                    cellCenterDirection[cellIndex] = dCenter;
                    cellLatitude[cellIndex] = dCenter.y; // sin(latitude), assuming world Y is spin axis

                    // Continental mask: 0..1
                    float continentN = SampleSphereNoise(dCenter, continentFrequency, continentSeed);
                    bool isContinentLand = continentN > OceanRatio;    // coarse land vs ocean mask

                    // Terrain noise: 0..1 → -1..1 -> scaled by amplitude
                    float terrainN = SampleSphereNoise(dCenter, noiseFrequency, noiseSeed);
                    float biasedN = Mathf.Clamp01(terrainN + TerrainBias);

                    float terrainOffset = (biasedN - 0.5f) * 2f * noiseAmplitude;

                    // First, compute what the land height *would* be at this point
                    float baseLandRadius = radius + terrainOffset;

                    // Now decide final land/water:
                    // - If the continent mask says "land" AND that land sits above sea level → land
                    // - Otherwise → water (this gives you oceans + lakes in low-lying land)
                    bool isLand;
                    float cellBaseRadius;

                    if (isContinentLand && baseLandRadius >= seaRadius)
                    {
                        // True land
                        isLand = true;
                        cellBaseRadius = baseLandRadius;
                    }
                    else
                    {
                        // Water: use inverted depth from terrain so oceans & lakes have a floor
                        isLand = false;
                        float depth = Mathf.Abs(terrainOffset);
                        cellBaseRadius = seaRadius - depth - seaLevelOffset;
                    }

                    // Store land/water classification
                    cellIsLand[cellIndex] = isLand;

                    // Elevation relative to base radius (can be negative for deep oceans)
                    cellElevation[cellIndex] = cellBaseRadius - radius;

                    // 1) Start from latitude-based pattern:
                    //    - Wet at equator
                    //    - Drier toward poles
                    float latAbs = Mathf.Abs(cellLatitude[cellIndex]); // 0 at equator, 1 at poles
                    float baseHum01 = Mathf.Lerp(1.0f, 0.2f, latAbs);   // equator ~1.0, poles ~0.2

                    // 2) Subtropical dry belt around |lat| ~ 0.5 (≈30°): deserts
                    //    We subtract some humidity there using a simple "bump" function.
                    //    (You can tweak the 0.5 center and 0.15 width later.)
                    float desertBelt = Mathf.Exp(-Mathf.Pow((latAbs - 0.5f) / 0.15f, 2f)); // 0..1
                    baseHum01 -= desertBelt * 0.5f; // up to -0.5 humidity in desert belt

                    // 3) Oceans are more humid than land
                    if (!isLand)
                    {
                        baseHum01 += 0.25f; // extra moisture over water
                    }

                    // 4) High elevation is drier (mountains)
                    if (cellElevation[cellIndex] > 0f)
                    {
                        // scale elevation to some "mountain height" range
                        // assuming your typical mountains are in ~0..20 range; tweak as needed
                        float elevNorm = Mathf.Clamp01(cellElevation[cellIndex] / 20f);
                        baseHum01 -= elevNorm * 0.3f;
                    }

                    // 5) Clamp to [0,1] and scale to [0,100]
                    baseHum01 = Mathf.Clamp01(baseHum01);
                    cellHumidity[cellIndex] = baseHum01 * 100f;
                    // --------------------------------

                    float cellTopRadius = cellBaseRadius + blockHeight;

                    // --- Project to sphere and build bottom/top positions ---

                    Vector3 b00 = center.position + d00 * cellBaseRadius;
                    Vector3 b10 = center.position + d10 * cellBaseRadius;
                    Vector3 b11 = center.position + d11 * cellBaseRadius;
                    Vector3 b01 = center.position + d01 * cellBaseRadius;

                    Vector3 t00 = center.position + d00 * cellTopRadius;
                    Vector3 t10 = center.position + d10 * cellTopRadius;
                    Vector3 t11 = center.position + d11 * cellTopRadius;
                    Vector3 t01 = center.position + d01 * cellTopRadius;

                    int vStart = vertIndex;

                    // Order: bottom quad (00,10,11,01), top quad (00,10,11,01)
                    vertices[vertIndex++] = b00; // 0
                    vertices[vertIndex++] = b10; // 1
                    vertices[vertIndex++] = b11; // 2
                    vertices[vertIndex++] = b01; // 3

                    vertices[vertIndex++] = t00; // 4
                    vertices[vertIndex++] = t10; // 5
                    vertices[vertIndex++] = t11; // 6
                    vertices[vertIndex++] = t01; // 7

                    // UVs (same pattern for bottom & top)
                    uvs[vStart + 0] = new Vector2(u0, v0);
                    uvs[vStart + 1] = new Vector2(u1, v0);
                    uvs[vStart + 2] = new Vector2(u1, v1);
                    uvs[vStart + 3] = new Vector2(u0, v1);

                    uvs[vStart + 4] = new Vector2(u0, v0);
                    uvs[vStart + 5] = new Vector2(u1, v0);
                    uvs[vStart + 6] = new Vector2(u1, v1);
                    uvs[vStart + 7] = new Vector2(u0, v1);

                    // Radial normals: from planet center → vertex
                    normals[vStart + 0] = (b00 - center.position).normalized;
                    normals[vStart + 1] = (b10 - center.position).normalized;
                    normals[vStart + 2] = (b11 - center.position).normalized;
                    normals[vStart + 3] = (b01 - center.position).normalized;

                    normals[vStart + 4] = (t00 - center.position).normalized;
                    normals[vStart + 5] = (t10 - center.position).normalized;
                    normals[vStart + 6] = (t11 - center.position).normalized;
                    normals[vStart + 7] = (t01 - center.position).normalized;

                    // ----- Triangles -----
                    List<int> tris = isLand ? landTriangles : waterTriangles;

                    // Bottom face
                    tris.Add(vStart + 0);
                    tris.Add(vStart + 1);
                    tris.Add(vStart + 2);

                    tris.Add(vStart + 0);
                    tris.Add(vStart + 2);
                    tris.Add(vStart + 3);

                    // Top face
                    tris.Add(vStart + 4);
                    tris.Add(vStart + 6);
                    tris.Add(vStart + 5);

                    tris.Add(vStart + 4);
                    tris.Add(vStart + 7);
                    tris.Add(vStart + 6);

                    // Side 1 (b00-b10-t10-t00)
                    tris.Add(vStart + 0);
                    tris.Add(vStart + 5);
                    tris.Add(vStart + 1);

                    tris.Add(vStart + 0);
                    tris.Add(vStart + 4);
                    tris.Add(vStart + 5);

                    // Side 2 (b10-b11-t11-t10)
                    tris.Add(vStart + 1);
                    tris.Add(vStart + 6);
                    tris.Add(vStart + 2);

                    tris.Add(vStart + 1);
                    tris.Add(vStart + 5);
                    tris.Add(vStart + 6);

                    // Side 3 (b11-b01-t01-t11)
                    tris.Add(vStart + 2);
                    tris.Add(vStart + 7);
                    tris.Add(vStart + 3);

                    tris.Add(vStart + 2);
                    tris.Add(vStart + 6);
                    tris.Add(vStart + 7);

                    // Side 4 (b01-b00-t00-t01)
                    tris.Add(vStart + 3);
                    tris.Add(vStart + 4);
                    tris.Add(vStart + 0);

                    tris.Add(vStart + 3);
                    tris.Add(vStart + 7);
                    tris.Add(vStart + 4);
                }
            }
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null || mesh.name != "CubeSphereBlockMesh")
        {
            mesh = new Mesh();
            mesh.name = "CubeSphereBlockMesh";
            meshFilter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;

        // Two submeshes: 0 = land, 1 = water
        mesh.subMeshCount = 2;
        mesh.SetTriangles(landTriangles, 0);
        mesh.SetTriangles(waterTriangles, 1);

        mesh.RecalculateBounds();

        // Flip triangles to face outward, per submesh
        FlipTriangles(mesh);

        meshFilter.mesh = mesh;

        // Make sure renderer has 2 materials (0 = land, 1 = water)
        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mats = mr.sharedMaterials;
            if (mats == null || mats.Length < 2)
            {
                System.Array.Resize(ref mats, 2);
            }
            mr.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// Spherical-ish Perlin noise sampled with 3 axis projections,
    /// returns value in [0,1].
    /// </summary>
    private float SampleSphereNoise(Vector3 dir, float frequency, int seed)
    {
        float f = frequency;
        float sx = dir.x * f + seed * 0.123f;
        float sy = dir.y * f + seed * 0.456f;
        float sz = dir.z * f + seed * 0.789f;

        float n1 = Mathf.PerlinNoise(sx, sy);
        float n2 = Mathf.PerlinNoise(sy, sz);
        float n3 = Mathf.PerlinNoise(sz, sx);

        return (n1 + n2 + n3) / 3f;
    }

    private static void FlipTriangles(Mesh mesh)
    {
        int subMeshCount = mesh.subMeshCount;
        for (int s = 0; s < subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int tmp = tris[i];
                tris[i] = tris[i + 1];
                tris[i + 1] = tmp;
            }
            mesh.SetTriangles(tris, s);
        }
    }

    /// <summary>
    /// Map a point on a cube (x,y,z in [-1,1]) to a spherified-cube direction.
    /// Reduces center-vs-edge distortion compared to simple normalize().
    /// </summary>
    private static Vector3 CubeToSphere(Vector3 p)
    {
        float x = p.x;
        float y = p.y;
        float z = p.z;

        float x2 = x * x;
        float y2 = y * y;
        float z2 = z * z;

        float sx = x * Mathf.Sqrt(1f - (y2 + z2) * 0.5f + (y2 * z2) / 3f);
        float sy = y * Mathf.Sqrt(1f - (z2 + x2) * 0.5f + (z2 * x2) / 3f);
        float sz = z * Mathf.Sqrt(1f - (x2 + y2) * 0.5f + (x2 * y2) / 3f);

        return new Vector3(sx, sy, sz).normalized;
    }

    /// <summary>
    /// Get a point on the cube face in [-1,1] space for given UV [0,1].
    /// </summary>
    private static Vector3 FacePoint(Vector3 localUp, Vector3 axisA, Vector3 axisB, float u, float v)
    {
        // Shift [0,1] -> [-0.5,0.5], then scale to [-1,1]
        return localUp +
               (u - 0.5f) * 2f * axisA +
               (v - 0.5f) * 2f * axisB;
    }
}
