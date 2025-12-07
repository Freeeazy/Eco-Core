using UnityEngine;

public class CubeSphereSurfaceManager : MonoBehaviour
{
    public CubeSphereBlockMesh blockMesh;
    public Material groundMaterial;   // uses SG_CubeSphereGridLit

    Texture2D cellMap;
    int n; // cellsPerFace

    void Start()
    {
        n = blockMesh.CellsPerFace;   // expose this as a public getter
        cellMap = new Texture2D(n, n * 6, TextureFormat.RGBA32, false);
        cellMap.filterMode = FilterMode.Point;
        cellMap.wrapMode = TextureWrapMode.Clamp;

        // Initialize from blockMesh.cellIsLand / humidity / whatever
        for (int cell = 0; cell < blockMesh.TotalCells; cell++)
        {
            float v = blockMesh.cellIsLand[cell] ? 1f : 0f;
            Color c = new Color(v, v, v, 1f);
            var (px, py) = CellToPixel(cell); // face,x,y -> pixel
            cellMap.SetPixel(px, py, c);
        }
        cellMap.Apply();

        groundMaterial.SetTexture("_CellMap", cellMap);
    }

    (int x, int y) CellToPixel(int cell)
    {
        int n2 = n * n;
        int face = cell / n2;
        int idx = cell % n2;
        int cy = idx / n;
        int cx = idx % n;

        int px = cx;
        int py = face * n + cy;  // 6 faces stacked vertically
        return (px, py);
    }

    public void PaintCell(int cell, Color color)
    {
        var (px, py) = CellToPixel(cell);
        cellMap.SetPixel(px, py, color);
        cellMap.Apply();
    }
}
