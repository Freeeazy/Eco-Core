using UnityEngine;

public class CubeSphereSurfaceManager : MonoBehaviour
{
    public CubeSphereBlockMesh blockMesh;
    public Material groundMaterial;   // uses SG_CubeSphereGridLit

    [Header("Latitudinal Ground Colors")]
    public Color equatorColor = new Color(0.20f, 0.45f, 0.18f, 1f);
    public Color midColor = new Color(0.18f, 0.40f, 0.17f, 1f);
    public Color polarColor = new Color(0.80f, 0.90f, 0.90f, 1f);

    [Header("Bands (normalized 0 = equator, 1 = pole)")]
    [Range(0f, 1f)] public float equatorBand = 0.10f; // full equator color radius
    [Range(0f, 1f)] public float equatorBlend = 0.15f; // blend to mid color
    [Range(0f, 1f)] public float polarCap = 0.15f; // full polar color radius
    [Range(0f, 1f)] public float polarBlend = 0.15f; // blend from mid to polar

    Texture2D cellMap;
    int n; // cellsPerFace

    void Start()
    {
        n = blockMesh.CellsPerFace;
        cellMap = new Texture2D(n, n * 6, TextureFormat.RGBA32, false);
        cellMap.filterMode = FilterMode.Point;
        cellMap.wrapMode = TextureWrapMode.Clamp;

        for (int cell = 0; cell < blockMesh.TotalCells; cell++)
        {
            Color c;

            if (blockMesh.cellIsLand[cell])
            {
                // Assume latitude in degrees [-90, 90]
                float latSin = blockMesh.cellLatitude[cell];
                float lat01 = Mathf.Abs(latSin);

                c = EvaluateLatitudeColor(lat01);
            }
            else
            {
                // Non-land cells: you can keep them black/0 and let the water material handle them
                c = Color.black;
            }

            var (px, py) = CellToPixel(cell);
            cellMap.SetPixel(px, py, c);
        }

        cellMap.Apply();
        groundMaterial.SetTexture("_CellMap", cellMap);
    }

    /// <summary>
    /// lat01: 0 = equator, 1 = pole (absolute latitude).
    /// Returns the final ground color for that latitude.
    /// </summary>
    Color EvaluateLatitudeColor(float lat01)
    {
        // ----- Polar side -----
        float poleStart = 1f - polarCap;                 // where solid polar color begins
        float poleBlendStart = 1f - (polarCap + polarBlend);  // start of blend from mid -> polar

        if (lat01 >= poleStart)
        {
            // Solid polar cap
            return polarColor;
        }

        if (lat01 >= poleBlendStart)
        {
            // Blend from midColor to polarColor
            float t = Mathf.InverseLerp(poleBlendStart, poleStart, lat01);
            return Color.Lerp(midColor, polarColor, t);
        }

        // ----- Equator side -----
        float eqBandEnd = equatorBand;                    // solid equator color
        float eqBlendEnd = equatorBand + equatorBlend;     // end of equator blend

        if (lat01 <= eqBandEnd)
        {
            // Solid equator band
            return equatorColor;
        }

        if (lat01 <= eqBlendEnd)
        {
            // Blend from equatorColor to midColor as we move away from the equator
            float t = Mathf.InverseLerp(eqBlendEnd, eqBandEnd, lat01);
            return Color.Lerp(midColor, equatorColor, t);
        }

        // ----- Mid-latitudes -----
        return midColor;
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
