using UnityEngine;

public class WaterSurfaceManager : MonoBehaviour
{
    public CubeSphereBlockMesh blockMesh;
    public Material waterMaterial;   // Uses duplicated SG_CubeSphereGridLit_Water (new water ShaderGraph)

    [Header("Water Colors")]
    public Color shallowWaterColor = new Color(0.15f, 0.35f, 0.85f, 1f);
    public Color deepWaterColor = new Color(0.05f, 0.10f, 0.45f, 1f);

    [Header("Non-Water Default Ground Color")]
    public Color defaultGroundColor = new Color(0.4f, 0.3f, 0.2f, 1f);

    Texture2D waterMap;
    int n;

    void Start()
    {
        n = blockMesh.CellsPerFace;

        waterMap = new Texture2D(n, n * 6, TextureFormat.RGBA32, false);
        waterMap.filterMode = FilterMode.Point;
        waterMap.wrapMode = TextureWrapMode.Clamp;

        // Initialize pixels based on water vs non-water
        for (int cell = 0; cell < blockMesh.TotalCells; cell++)
        {
            bool isWater = !blockMesh.cellIsLand[cell];   // inverse condition

            Color c;
            if (isWater)
            {
                // OPTIONAL: blend based on elevation / humidity / etc later
                c = Random.Range(0, 1f) > 0.5f ? shallowWaterColor : deepWaterColor;
            }
            else
            {
                c = defaultGroundColor;
            }

            var (px, py) = CellToPixel(cell);
            waterMap.SetPixel(px, py, c);
        }

        waterMap.Apply();
        waterMaterial.SetTexture("_CellMap", waterMap);
    }

    (int x, int y) CellToPixel(int cell)
    {
        int n2 = n * n;
        int face = cell / n2;
        int idx = cell % n2;
        int cy = idx / n;
        int cx = idx % n;

        int px = cx;
        int py = face * n + cy; // stack faces vertically

        return (px, py);
    }

    public void PaintCell(int cell, Color color)
    {
        var (px, py) = CellToPixel(cell);
        waterMap.SetPixel(px, py, color);
        waterMap.Apply();
    }
}
