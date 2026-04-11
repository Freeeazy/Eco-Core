using TMPro;
using UnityEngine;

public class PreviewSeedButton : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField terrainSeedInput;
    [SerializeField] private TMP_InputField continentSeedInput;

    [Header("Planet Generator")]
    [SerializeField] private CubeSphereBlockMesh planetMesh;

    [Header("Optional")]
    [SerializeField] private bool useRandomIfEmpty = true;
    [SerializeField] private int maxSeedMagnitude = 1000000;

    public void PreviewSeed()
    {
        if (planetMesh == null)
        {
            Debug.LogWarning("PreviewSeedButton: No CubeSphereBlockMesh assigned.");
            return;
        }

        int terrainSeed = ParseSeed(terrainSeedInput != null ? terrainSeedInput.text : "");
        int continentSeed = ParseSeed(continentSeedInput != null ? continentSeedInput.text : "");

        planetMesh.noiseSeed = terrainSeed;
        planetMesh.continentSeed = continentSeed;

        planetMesh.Generate();

        Debug.Log($"Previewed planet with Terrain Seed: {terrainSeed}, Continent Seed: {continentSeed}");
    }

    private int ParseSeed(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            if (useRandomIfEmpty)
                return Random.Range(-maxSeedMagnitude, maxSeedMagnitude + 1);

            return 0;
        }

        input = input.Trim();

        if (int.TryParse(input, out int numericSeed))
        {
            return ClampSeed(numericSeed);
        }

        int hashedSeed = input.GetHashCode();
        return ClampSeed(hashedSeed);
    }

    private int ClampSeed(int seed)
    {
        if (maxSeedMagnitude <= 0)
            return seed;

        if (seed == int.MinValue)
            seed = 0;

        return Mathf.Clamp(seed, -maxSeedMagnitude, maxSeedMagnitude);
    }
}