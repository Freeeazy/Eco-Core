// LightFlicker.cs
using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    public float minIntensity = 1.2f;
    public float maxIntensity = 1.8f;
    public float flickerRate = 0.08f; // seconds between flickers
    private Light l;
    private float t;

    void Awake() => l = GetComponent<Light>();

    void Update()
    {
        if ((t += Time.deltaTime) >= flickerRate)
        {
            l.intensity = Random.Range(minIntensity, maxIntensity);
            t = 0f;
        }
    }
}
