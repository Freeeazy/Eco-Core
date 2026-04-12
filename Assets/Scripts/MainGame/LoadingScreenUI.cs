using System.Collections;
using UnityEngine;
using TMPro; // remove if using normal Text

public class LoadingScreenUI : MonoBehaviour
{
    public TextMeshProUGUI loadingText; // assign in inspector

    private void Start()
    {
        StartCoroutine(LoadingRoutine());
    }

    IEnumerator LoadingRoutine()
    {
        float duration = 1f;
        float timer = 0f;

        string[] states = { "Loading", "Loading.", "Loading..", "Loading..." };
        int index = 0;

        while (timer < duration)
        {
            loadingText.text = states[index];

            index = (index + 1) % states.Length;

            yield return new WaitForSeconds(0.25f); // speed of dots
            timer += 0.25f;
        }

        gameObject.SetActive(false); // turn off after 1 second
    }
}