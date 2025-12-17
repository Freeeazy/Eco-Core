using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MouseObjectSpin : MonoBehaviour
{
    [Header("Spin Settings")]
    public int fullSpins = 2;
    public float extraDegrees = 25f;
    public float spinDuration = 0.25f;
    public float returnDuration = 0.12f;

    [Header("Axis")]
    public Vector3 spinAxis = Vector3.up;

    [Header("Retrigger Behavior")]
    [Tooltip("If true, each retrigger treats the current rotation as the new 'rest' rotation.")]
    public bool retriggerResetsRest = true;

    [Header("Anti-Runaway Guard")]
    [Tooltip("Minimum time between triggers (prevents jittery enter/exit spam).")]
    public float minRetriggerSeconds = 0.20f;

    [Tooltip("Require the mouse to move at least this many pixels before a new trigger is allowed.")]
    public float requireMouseMovePixels = 6f;

    private Quaternion restRotation;
    private Camera cam;
    private Coroutine running;


    private float lastTriggerTime = -999f;
    private Vector2 lastTriggerMousePos;
    private bool hasMousePos;

    void Awake()
    {
        restRotation = transform.localRotation;
        cam = Camera.main;
        spinAxis = spinAxis.sqrMagnitude > 0f ? spinAxis.normalized : Vector3.up;
    }

    void OnMouseEnter()
    {
        TriggerSpin();
    }

    void TriggerSpin()
    {
        if (cam == null) return;

        // --- Anti-runaway checks ---
        float now = Time.unscaledTime; // title screens often use unscaled time
        Vector2 mouse = Input.mousePosition;

        // Cooldown gate
        if (now - lastTriggerTime < minRetriggerSeconds)
            return;

        // Mouse-move gate (prevents re-trigger while mouse is stationary)
        if (hasMousePos && Vector2.Distance(mouse, lastTriggerMousePos) < requireMouseMovePixels)
            return;

        // Raycast to confirm we're actually over THIS object
        Ray ray = cam.ScreenPointToRay(mouse);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        if (hit.transform != transform) return;

        // Decide direction based on which side was hit
        Vector3 localHit = transform.InverseTransformPoint(hit.point);
        float direction = localHit.x >= 0f ? -1f : 1f;

        // Record trigger time + mouse pos AFTER we pass checks
        lastTriggerTime = now;
        lastTriggerMousePos = mouse;
        hasMousePos = true;

        // Interrupt current animation immediately
        if (running != null)
        {
            StopCoroutine(running);
            running = null;

            if (retriggerResetsRest)
                restRotation = transform.localRotation;
        }
        else
        {
            restRotation = transform.localRotation;
        }

        running = StartCoroutine(SpinRoutine(direction));
    }

    System.Collections.IEnumerator SpinRoutine(float direction)
    {
        float totalDegrees = fullSpins * 360f + Mathf.Abs(extraDegrees);
        float sign = Mathf.Sign(direction);

        // Spin phase
        float t = 0f;
        float rotated = 0f;

        while (t < spinDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / spinDuration);

            // Ease-out
            float eased = 1f - Mathf.Pow(1f - a, 3f);

            float targetRotated = totalDegrees * eased;
            float delta = targetRotated - rotated;
            rotated = targetRotated;

            transform.Rotate(spinAxis, delta * sign, Space.Self);
            yield return null;
        }

        // Return phase
        Quaternion start = transform.localRotation;
        float r = 0f;

        while (r < returnDuration)
        {
            r += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(r / returnDuration);
            float eased = 1f - Mathf.Pow(1f - a, 3f);
            transform.localRotation = Quaternion.Slerp(start, restRotation, eased);
            yield return null;
        }

        transform.localRotation = restRotation;
        running = null;
    }
}
