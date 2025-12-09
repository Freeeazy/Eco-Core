using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [Header("FPS Display")]
    public TMP_Text fpsText;        // main FPS text
    public TMP_Text fpsCapText;     // secondary text showing FPS vs cap

    [Tooltip("How often to update the FPS text (in seconds).")]
    public float updateInterval = 0.5f;

    [Header("FPS Cap")]
    public bool useFPSCap = false;

    public enum FPSCapMode
    {
        Unlimited = -1,
        FPS30 = 30,
        FPS60 = 60,
        FPS90 = 90,
        FPS120 = 120,
        FPS144 = 144,
        FPS240 = 240
    }

    public FPSCapMode fpsCap = FPSCapMode.Unlimited;

    private float accumulated = 0f;
    private int frames = 0;
    private float timer = 0f;

    private void Start()
    {
        ApplyFPSCap();
    }

    private void OnValidate()
    {
        // So you see the effect when you change the enum in the inspector (in Play Mode).
        ApplyFPSCap();
    }

    private void ApplyFPSCap()
    {
        if (useFPSCap)
        {
            Application.targetFrameRate = (int)fpsCap;  // -1 = unlimited
        }
        else
        {
            Application.targetFrameRate = -1; // explicitly uncapped
        }
    }

    private void Update()
    {
        accumulated += Time.unscaledDeltaTime;
        frames++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            float fps = frames / accumulated;
            int fpsRounded = Mathf.RoundToInt(fps);

            if (fpsText != null)
            {
                fpsText.text = fpsRounded + " FPS";
            }

            if (fpsCapText != null)
            {
                int capValue = (int)fpsCap;

                if (!useFPSCap || capValue <= 0)
                {
                    // No cap active
                    fpsCapText.text = fpsRounded + " FPS (Uncapped)";
                }
                else
                {
                    // Show current vs cap for stability checking
                    fpsCapText.text = fpsRounded + " / " + capValue + " FPS";
                }
            }

            // reset
            frames = 0;
            accumulated = 0f;
            timer = 0f;
        }
    }
}
