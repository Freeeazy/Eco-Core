using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // remove if using normal InputField

public class ConfirmSeed : MonoBehaviour
{
    public TMP_InputField terrainHeightInput;
    public TMP_InputField continentInput;

    public void OnConfirm()
    {
        int terrain = 4;
        int continent = 5;

        int temp;

        if (int.TryParse(terrainHeightInput.text, out temp))
            terrain = temp;

        if (int.TryParse(continentInput.text, out temp))
            continent = temp;

        // Save to SeedManager
        if (SeedManager.Instance != null)
        {
            SeedManager.Instance.SetSeeds(terrain, continent);
        }
        else
        {
            Debug.LogError("SeedManager not found!");
        }

        // Load next scene
        SceneManager.LoadSceneAsync("MainGame");
    }
}