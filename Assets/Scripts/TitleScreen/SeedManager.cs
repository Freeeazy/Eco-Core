using UnityEngine;

public class SeedManager : MonoBehaviour
{
    public static SeedManager Instance;

    public int TerrainHeight;
    public int Continent;

    private void Awake()
    {
        // Singleton check
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSeeds(int a, int b)
    {
        TerrainHeight = a;
        Continent = b;
    }
}