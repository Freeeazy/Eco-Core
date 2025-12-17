using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VoxelLetterMeshGeneratorWindow : EditorWindow
{
    [Header("Input")]
    public Texture2D letterMask;              // PNG/Sprite: transparent background, opaque letter
    public string outputMeshName = "Letter_W";

    [Header("Voxel Settings")]
    public float voxelSize = 0.1f;            // world size per pixel
    public int depthVoxels = 6;               // extrusion depth in voxels
    [Range(0f, 1f)] public float alphaThreshold = 0.5f;

    [Header("Output")]
    public string outputFolder = "Assets/VoxelLetters";

    [MenuItem("Tools/EcoCore/Voxel Letter Mesh Generator")]
    public static void ShowWindow()
    {
        GetWindow<VoxelLetterMeshGeneratorWindow>("Voxel Letter Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Voxel Letter Mesh Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        letterMask = (Texture2D)EditorGUILayout.ObjectField("Letter Mask (Texture2D)", letterMask, typeof(Texture2D), false);
        outputMeshName = EditorGUILayout.TextField("Output Mesh Name", outputMeshName);

        EditorGUILayout.Space();
        voxelSize = EditorGUILayout.FloatField("Voxel Size", voxelSize);
        depthVoxels = EditorGUILayout.IntField("Depth (voxels)", depthVoxels);
        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(letterMask == null))
        {
            if (GUILayout.Button("Generate Mesh Asset"))
            {
                Generate();
            }
        }

        EditorGUILayout.HelpBox(
            "Use a mask texture where the letter is opaque and background is transparent.\n" +
            "Tip: 16x16 looks chunky, 32x32 is a sweet spot, 64x64 is smoother.",
            MessageType.Info);
    }

    private void Generate()
    {
        if (letterMask == null)
        {
            Debug.LogError("No letterMask assigned.");
            return;
        }

        // Make sure the texture is readable
        string path = AssetDatabase.GetAssetPath(letterMask);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        bool[,] filled = BuildFilledMap(letterMask, alphaThreshold);
        Mesh mesh = VoxelMeshBuilder.BuildExtrudedVoxelMesh(filled, voxelSize, depthVoxels);

        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{outputMeshName}.asset");
        AssetDatabase.CreateAsset(mesh, meshAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Create a preview GameObject in the scene
        var go = new GameObject(outputMeshName);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;

        // Assign a default material if you have one; otherwise Unity will show pink in URP/HDRP until you set it.
        mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

        Selection.activeObject = go;
        EditorGUIUtility.PingObject(mesh);

        Debug.Log($"Generated voxel letter mesh: {meshAssetPath}");
    }

    private static bool[,] BuildFilledMap(Texture2D tex, float threshold)
    {
        int w = tex.width;
        int h = tex.height;

        var map = new bool[w, h];
        Color32[] pixels = tex.GetPixels32();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte a = pixels[y * w + x].a;
                map[x, y] = (a / 255f) >= threshold;
            }
        }

        return map;
    }
}

/// <summary>
/// Builds a mesh by extruding a 2D filled pixel map into a voxel volume,
/// emitting only outer faces (optimized vs placing cubes).
/// </summary>
public static class VoxelMeshBuilder
{
    // Face directions
    private static readonly Vector3Int[] Dir =
    {
        new Vector3Int( 1, 0, 0), // +X
        new Vector3Int(-1, 0, 0), // -X
        new Vector3Int( 0, 1, 0), // +Y
        new Vector3Int( 0,-1, 0), // -Y
        new Vector3Int( 0, 0, 1), // +Z
        new Vector3Int( 0, 0,-1), // -Z
    };

    public static Mesh BuildExtrudedVoxelMesh(bool[,] filled2D, float voxelSize, int depthVoxels)
    {
        int w = filled2D.GetLength(0);
        int h = filled2D.GetLength(1);
        int d = Mathf.Max(1, depthVoxels);

        // 3D occupancy: (x,y,z) where z is depth extrusion
        bool[,,] solid = new bool[w, h, d];
        for (int z = 0; z < d; z++)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    solid[x, y, z] = filled2D[x, y];

        var verts = new List<Vector3>(w * h * d * 8);
        var tris = new List<int>(w * h * d * 36);
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();

        // Center the mesh around origin for easier placement
        Vector3 originOffset = new Vector3(w, h, d) * voxelSize * 0.5f;

        for (int z = 0; z < d; z++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!solid[x, y, z]) continue;

                    // For each of 6 directions, emit a quad if neighbor is empty/out of bounds
                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int n = Dir[face];
                        int nx = x + n.x;
                        int ny = y + n.y;
                        int nz = z + n.z;

                        bool neighborSolid =
                            nx >= 0 && nx < w &&
                            ny >= 0 && ny < h &&
                            nz >= 0 && nz < d &&
                            solid[nx, ny, nz];

                        if (!neighborSolid)
                        {
                            AddFace(verts, tris, norms, uvs, x, y, z, face, voxelSize, originOffset);
                        }
                    }
                }
            }
        }

        var mesh = new Mesh();
        mesh.indexFormat = (verts.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddFace(
        List<Vector3> v, List<int> t, List<Vector3> n, List<Vector2> uv,
        int x, int y, int z, int face, float s, Vector3 offset)
    {
        // Cube corners in local voxel space
        // We'll build each face as a quad (4 verts).
        Vector3 p = new Vector3(x, y, z) * s - offset;

        Vector3 a, b, c, d;
        Vector3 normal;

        switch (face)
        {
            case 0: // +X
                a = p + new Vector3(s, 0, 0);
                b = p + new Vector3(s, s, 0);
                c = p + new Vector3(s, s, s);
                d = p + new Vector3(s, 0, s);
                normal = Vector3.right;
                break;

            case 1: // -X
                a = p + new Vector3(0, 0, s);
                b = p + new Vector3(0, s, s);
                c = p + new Vector3(0, s, 0);
                d = p + new Vector3(0, 0, 0);
                normal = Vector3.left;
                break;

            case 2: // +Y
                a = p + new Vector3(0, s, s);
                b = p + new Vector3(s, s, s);
                c = p + new Vector3(s, s, 0);
                d = p + new Vector3(0, s, 0);
                normal = Vector3.up;
                break;

            case 3: // -Y
                a = p + new Vector3(0, 0, 0);
                b = p + new Vector3(s, 0, 0);
                c = p + new Vector3(s, 0, s);
                d = p + new Vector3(0, 0, s);
                normal = Vector3.down;
                break;

            case 4: // +Z
                a = p + new Vector3(s, 0, s);
                b = p + new Vector3(s, s, s);
                c = p + new Vector3(0, s, s);
                d = p + new Vector3(0, 0, s);
                normal = Vector3.forward;
                break;

            default: // -Z
                a = p + new Vector3(0, 0, 0);
                b = p + new Vector3(0, s, 0);
                c = p + new Vector3(s, s, 0);
                d = p + new Vector3(s, 0, 0);
                normal = Vector3.back;
                break;
        }

        int start = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);

        // Two triangles (clockwise)
        t.Add(start + 0); t.Add(start + 1); t.Add(start + 2);
        t.Add(start + 0); t.Add(start + 2); t.Add(start + 3);

        n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);

        // Simple quad UVs (you can ignore or replace with atlas logic later)
        uv.Add(new Vector2(0, 0));
        uv.Add(new Vector2(0, 1));
        uv.Add(new Vector2(1, 1));
        uv.Add(new Vector2(1, 0));
    }
}
