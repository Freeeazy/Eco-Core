using UnityEngine;

[RequireComponent(typeof(CubeSphereBlockMesh))]
public class SeedLoader : MonoBehaviour
{
    private CubeSphereBlockMesh cubeSphere;

    private void Awake()
    {
        cubeSphere = GetComponent<CubeSphereBlockMesh>();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (SeedManager.Instance == null)
        {
            Debug.LogError("SeedManager not found! Cannot load seeds.");
            return;
        }

        cubeSphere.noiseSeed = SeedManager.Instance.TerrainHeight;
        cubeSphere.continentSeed = SeedManager.Instance.Continent;

        cubeSphere.Generate();
    }
}