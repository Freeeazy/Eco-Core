using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

public class TMPVoxelTextMeshGeneratorWindow : EditorWindow
{
    [Header("TextMeshPro Input")]
    public TMP_FontAsset fontAsset;
    public string text = "ECOCORE";
    public int renderSize = 128;               // Texture resolution per character (64/128/256)
    public int paddingPixels = 12;             // Padding around glyph
    [Range(0f, 1f)] public float alphaThreshold = 0.25f;

    [Header("Voxel Settings")]
    public float voxelSize = 0.1f;
    public int depthVoxels = 8;

    [Header("Output")]
    public string outputFolder = "Assets/VoxelLetters";
    public bool spawnPreviewObjects = true;

    [MenuItem("Tools/EcoCore/TMP Voxel Text Mesh Generator")]
    public static void ShowWindow()
    {
        GetWindow<TMPVoxelTextMeshGeneratorWindow>("TMP -> Voxel Mesh");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("TMP -> Voxel Letter Mesh Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        fontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font Asset", fontAsset, typeof(TMP_FontAsset), false);
        text = EditorGUILayout.TextField("Text", text);

        EditorGUILayout.Space();
        renderSize = EditorGUILayout.IntPopup("Render Size", renderSize, new[] { "64", "128", "256" }, new[] { 64, 128, 256 });
        paddingPixels = EditorGUILayout.IntField("Padding (px)", paddingPixels);
        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);

        EditorGUILayout.Space();
        voxelSize = EditorGUILayout.FloatField("Voxel Size", voxelSize);
        depthVoxels = EditorGUILayout.IntField("Depth (voxels)", depthVoxels);

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        spawnPreviewObjects = EditorGUILayout.Toggle("Spawn Preview Objects", spawnPreviewObjects);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(fontAsset == null || string.IsNullOrEmpty(text)))
        {
            if (GUILayout.Button("Generate Meshes (one per character)"))
                GenerateAll();
        }

        EditorGUILayout.HelpBox(
            "Tip: Use a bold/blocky font for chunkier voxel letters.\n" +
            "Render Size 128 is usually the sweet spot. 64 = very chunky, 256 = smoother.",
            MessageType.Info);
    }

    private void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        // Create hidden render rig
        var rig = CreateHiddenRig(out Camera cam, out TextMeshPro tmp, out RenderTexture rt);

        try
        {
            tmp.font = fontAsset;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.richText = false;

            // Slightly over-size so glyph fills the frame
            tmp.fontSize = 100; // we'll fit using bounds + camera ortho size

            int created = 0;

            foreach (char ch in text)
            {
                // Skip spaces but keep index consistent if you want
                if (ch == ' ')
                    continue;

                Texture2D mask = RenderGlyphMask(cam, tmp, rt, ch, renderSize, paddingPixels);

                // Build boolean map from alpha
                bool[,] filled = BuildFilledMap(mask, alphaThreshold);

                // Optional: trim empty borders to reduce mesh size
                filled = TrimFilledMap(filled, out int trimW, out int trimH);
                if (filled == null)
                {
                    Debug.LogWarning($"Skipping '{ch}' (empty after trimming).");
                    continue;
                }

                Mesh mesh = VoxelMeshBuilder.BuildExtrudedVoxelMesh(filled, voxelSize, depthVoxels);

                string safeChar = MakeSafeCharName(ch);
                string meshName = $"Letter_{safeChar}";
                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{meshName}.asset");
                AssetDatabase.CreateAsset(mesh, meshPath);

                if (spawnPreviewObjects)
                {
                    var go = new GameObject(meshName);
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mf.sharedMesh = mesh;
                    mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                    go.transform.position = Vector3.right * (created * voxelSize * 12f);
                }

                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Generated {created} voxel letter mesh(es) into {outputFolder}.");
        }
        finally
        {
            DestroyImmediate(rig);
        }
    }

    private static GameObject CreateHiddenRig(out Camera cam, out TextMeshPro tmp, out RenderTexture rt)
    {
        var rig = new GameObject("TMPVoxelRenderRig");
        rig.hideFlags = HideFlags.HideAndDontSave;

        int renderLayer = LayerMask.NameToLayer("VoxelGlyphRender");
        if (renderLayer < 0)
        {
            Debug.LogError("Layer 'VoxelGlyphRender' not found. Add it in Project Settings > Tags and Layers.");
        }

        var textGO = new GameObject("TMP");
        textGO.hideFlags = HideFlags.HideAndDontSave;
        textGO.transform.SetParent(rig.transform, false);

        textGO.layer = renderLayer;

        tmp = textGO.AddComponent<TextMeshPro>();
        tmp.color = Color.white; // white glyph
        tmp.outlineWidth = 0f;
        tmp.fontStyle = FontStyles.Bold; // optional; remove if you want exact font weight

        tmp.gameObject.layer = renderLayer;

        var camGO = new GameObject("Cam");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        camGO.transform.SetParent(rig.transform, false);

        camGO.layer = renderLayer;

        cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // transparent background
        cam.nearClipPlane = -10f;
        cam.farClipPlane = 10f;

        cam.cullingMask = 1 << renderLayer;

        // Put camera in front of text
        cam.transform.position = new Vector3(0, 0, -5f);
        textGO.transform.position = Vector3.zero;

        rt = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1,
            filterMode = FilterMode.Point // crisp mask edges
        };
        rt.Create();
        cam.targetTexture = rt;

        return rig;
    }

    private static Texture2D RenderGlyphMask(Camera cam, TextMeshPro tmp, RenderTexture rt, char ch, int size, int padPx)
    {
        // Resize RT if needed
        if (rt.width != size || rt.height != size)
        {
            rt.Release();
            rt.width = size;
            rt.height = size;
            rt.Create();
        }

        tmp.text = ch.ToString();
        tmp.ForceMeshUpdate(true, true);

        // Fit camera to glyph bounds
        Bounds b = tmp.bounds;
        float maxExtent = Mathf.Max(b.extents.x, b.extents.y);
        float padWorld = maxExtent * (padPx / (float)size) * 2.5f;

        cam.orthographicSize = maxExtent + padWorld;
        cam.transform.position = new Vector3(b.center.x, b.center.y, -5f);

        // Render & read pixels
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        // Clear RT so previous glyph cannot persist
        GL.Clear(true, true, Color.clear);

        cam.Render();

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false, true);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        return tex;
    }

    private static bool[,] BuildFilledMap(Texture2D tex, float threshold)
    {
        int w = tex.width, h = tex.height;
        var map = new bool[w, h];
        var pixels = tex.GetPixels32();

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = pixels[y * w + x].a;
                map[x, y] = (a / 255f) >= threshold;
            }

        return map;
    }

    // Trims empty rows/cols so the mesh isn't a giant square for skinny letters
    private static bool[,] TrimFilledMap(bool[,] src, out int outW, out int outH)
    {
        int w = src.GetLength(0);
        int h = src.GetLength(1);

        int minX = w, minY = h, maxX = -1, maxY = -1;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!src[x, y]) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

        if (maxX < 0 || maxY < 0)
        {
            outW = outH = 0;
            return null;
        }

        outW = (maxX - minX + 1);
        outH = (maxY - minY + 1);

        var dst = new bool[outW, outH];
        for (int y = 0; y < outH; y++)
            for (int x = 0; x < outW; x++)
                dst[x, y] = src[minX + x, minY + y];

        return dst;
    }

    private static string MakeSafeCharName(char c)
    {
        if (char.IsLetterOrDigit(c)) return c.ToString().ToUpperInvariant();
        return ((int)c).ToString(); // fallback for punctuation
    }
}
