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

    [Header("Idle Curvature (Default State)")]
    [Range(-1f, 1f)] public float curveU = 0f;  // per-letter placement
    public float baseCurveStrength = 0.08f;     // how concave
    public float breatheAmp = 0.06f;            // percent modulation
    public float breatheSpeed = 0.25f;          // slow
    public float idleBlendInSeconds = 0.6f;
    public float idleBlendOutSeconds = 0.2f;

    public float idleZScale = 0.02f;            // optional: edge scale boost
    public float stationaryPixelsPerSec = 10f;  // “not moving” threshold
    public float stationaryDelay = 0.35f;       // time before idle returns while hovered

    private Quaternion restRotation;
    private Camera cam;
    private Coroutine running;

    private float lastTriggerTime = -999f;
    private Vector2 lastTriggerMousePos;
    private bool hasMousePos;

    private Vector3 restLocalPos;
    private Vector3 restLocalScale;
    private bool hovered;

    private Vector2 lastMouse;
    private float stationaryTimer;
    private float idleBlend; // 0..1
    private bool isSpinning;

    void Awake()
    {
        cam = Camera.main;
        spinAxis = spinAxis.sqrMagnitude > 0f ? spinAxis.normalized : Vector3.up;
        restRotation = transform.localRotation;
        restLocalPos = transform.localPosition;
        restLocalScale = transform.localScale;
        lastMouse = Input.mousePosition;
    }

    void OnMouseEnter()
    {
        hovered = true;
        TriggerSpin();
    }
    void OnMouseExit() 
    { 
        hovered = false; 
    }

    private void Update()
    {
        Vector2 mouse = Input.mousePosition;
        float mouseSpeed = (mouse - lastMouse).magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        lastMouse = mouse;

        bool mouseMoving = mouseSpeed > stationaryPixelsPerSec;

        if (mouseMoving) stationaryTimer = 0f;
        else stationaryTimer += Time.unscaledDeltaTime;

        // Allow idle when we're not spinning AND (not hovered OR hovered-but-stationary long enough)
        bool idleAllowed =
            !isSpinning &&
            (
                !hovered ||
                stationaryTimer >= stationaryDelay
            );

        // Blend target
        float targetIdleBlend = idleAllowed ? 1f : 0f;

        // Different speeds for blend in vs out
        float blendSpeed = (targetIdleBlend > idleBlend)
            ? (1f / Mathf.Max(idleBlendInSeconds, 0.0001f))
            : (1f / Mathf.Max(idleBlendOutSeconds, 0.0001f));

        idleBlend = Mathf.MoveTowards(idleBlend, targetIdleBlend, blendSpeed * Time.unscaledDeltaTime);

        float t = Time.unscaledTime;
        float curveStrength = baseCurveStrength * (1f + Mathf.Sin(t * breatheSpeed) * breatheAmp);

        float u2 = curveU * curveU;

        float zOffset = -u2 * curveStrength;
        float s = 1f + u2 * curveStrength * idleZScale;

        Vector3 idlePos = restLocalPos + Vector3.forward * zOffset;
        Vector3 idleScale = restLocalScale * s;

        transform.localPosition = Vector3.Lerp(restLocalPos, idlePos, idleBlend);
        transform.localScale = Vector3.Lerp(restLocalScale, idleScale, idleBlend);
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
        isSpinning = true;
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
        isSpinning = false;
        running = null;
    }
}
