using UnityEngine;
using UnityEngine.UI;

public class TimeScaleButtons : MonoBehaviour
{
    [Header("Buttons (assign in inspector)")]
    public Button btn1x;
    public Button btn5x;
    public Button btn10x;
    public Button btn50x;

    [Header("Sun time source")]
    public SunRotate sun; // reference the SunRotate script
    // file uploaded SunRotate exists so you can drag it here

    private Animator anim1x, anim5x, anim10x, anim50x;

    private void Awake()
    {
        // Cache animators from buttons
        anim1x = btn1x.GetComponent<Animator>();
        anim5x = btn5x.GetComponent<Animator>();
        anim10x = btn10x.GetComponent<Animator>();
        anim50x = btn50x.GetComponent<Animator>();

        // Add click listeners
        btn1x.onClick.AddListener(() => SetTimeScale(1f));
        btn5x.onClick.AddListener(() => SetTimeScale(5f));
        btn10x.onClick.AddListener(() => SetTimeScale(10f));
        btn50x.onClick.AddListener(() => SetTimeScale(50f));
    }

    private void Update()
    {
        // Read current time scale from the sun system
        float ts = sun.timeScale;

        // Update highlight states
        UpdateCurrentState(btn1x, anim1x, ts == 1f);
        UpdateCurrentState(btn5x, anim5x, ts == 5f);
        UpdateCurrentState(btn10x, anim10x, ts == 10f);
        UpdateCurrentState(btn50x, anim50x, ts == 50f);
    }

    private void SetTimeScale(float newScale)
    {
        sun.timeScale = newScale;
    }

    private void UpdateCurrentState(Button btn, Animator anim, bool isCurrent)
    {
        if (!anim) return;

        anim.SetBool("Current", isCurrent);
    }
}
