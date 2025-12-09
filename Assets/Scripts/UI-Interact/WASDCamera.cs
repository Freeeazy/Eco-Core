using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

public class WASDCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Distance Settings")]
    public float FarMax = 500f;
    public float CloseMin = 10f;
    public float Distance = 200f;

    [Header("Keyboard Speed Settings")]
    [Tooltip("Degrees per second when rotating east/west (A/D).")]
    public float yawSpeed = 60f;

    [Tooltip("Degrees per second when rotating up/down (W/S).")]
    public float pitchSpeed = 45f;

    [Tooltip("Units per second when zooming with Q/E.")]
    public float qeZoomSpeed = 200f;

    [Header("Speed Modifiers (optional)")]
    [Tooltip("Multiplier when holding SHIFT (faster).")]
    public float fastMultiplier = 3f;

    [Tooltip("Multiplier when holding CTRL (slower). Note: Unity editor may eat Ctrl+WASD.")]
    public float slowMultiplier = 0.25f;

    [Header("Middle Mouse Orbit")]
    public bool allowMiddleMouseOrbit = true;
    public float mouseYawSpeed = 150f;
    public float mousePitchSpeed = 120f;

    [Header("Orbit Inertia")]
    public bool useOrbitInertia = true;
    [Tooltip("Min spin speed (deg/sec) to start inertia.")]
    public float inertiaThreshold = 30f;
    [Tooltip("Scales how strong the carried spin is.")]
    public float inertiaMultiplier = 1f;
    [Tooltip("How quickly inertia decays (larger = stops faster).")]
    public float inertiaDamping = 5f;

    [Header("Scroll Zoom")]
    public bool allowScrollZoom = true;
    [Tooltip("How strong each scroll tick affects target distance.")]
    public float scrollZoomSpeed = 0.01f;
    [Tooltip("How quickly Distance lerps toward the target distance.")]
    public float zoomSmooth = 10f;

    [Header("Vertical Clamp")]
    public float minPitch = -85f;
    public float maxPitch = 85f;

    private float yaw;
    private float pitch = 20f;

    // Smoothed zoom target
    private float targetDistance;

    // Orbit inertia velocity: x = yawVel (deg/sec), y = pitchVel (deg/sec)
    private Vector2 orbitVelocity = Vector2.zero;

    private void Awake()
    {
        // Use the camera's existing placement as the starting point
        Vector3 euler = transform.rotation.eulerAngles;

        // Convert Unity's 0–360° into -180..180 for pitch
        yaw = euler.y;
        pitch = euler.x;
        if (pitch > 180f)
            pitch -= 360f;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // If we have a target, derive Distance from current placement
        if (target != null)
        {
            Vector3 toCam = transform.position - target.position;
            float dist = toCam.magnitude;

            Distance = Mathf.Clamp(dist, CloseMin, FarMax);
        }
        else
        {
            // No target, just clamp whatever Distance was in the inspector
            Distance = Mathf.Clamp(Distance, CloseMin, FarMax);
        }

        targetDistance = Distance;
    }

    private void LateUpdate()
    {
        if (Keyboard.current == null)
            return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // --- Speed modifiers (Ctrl = slower, Shift = faster) ---
        float speedFactor = 1f;
        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            speedFactor *= fastMultiplier;
        if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)
            speedFactor *= slowMultiplier;

        float dt = Time.deltaTime * speedFactor;

        // =====================
        // 1) KEYBOARD ROTATION
        // =====================

        // Vertical = latitude: W = up, S = down
        float vertical = 0f;
        if (kb.wKey.isPressed) vertical += 1f;
        if (kb.sKey.isPressed) vertical -= 1f;

        // Horizontal = longitude: A = west, D = east
        float horizontal = 0f;
        if (kb.aKey.isPressed) horizontal += 1f;   // A = West
        if (kb.dKey.isPressed) horizontal -= 1f;   // D = East

        yaw += horizontal * yawSpeed * dt;
        pitch += vertical * pitchSpeed * dt;

        // =========================
        // 2) MIDDLE MOUSE ORBIT + INERTIA
        // =========================
        bool hasMouse = mouse != null;
        bool orbitActive = allowMiddleMouseOrbit && hasMouse && mouse.middleButton.isPressed;

        if (orbitActive)
        {
            Vector2 delta = mouse.delta.ReadValue();

            // Convert mouse delta to angular velocity (deg/sec)
            float yawVel = delta.x * mouseYawSpeed;
            float pitchVel = -delta.y * mousePitchSpeed;

            // Apply this frame
            yaw += yawVel * Time.deltaTime;
            pitch += pitchVel * Time.deltaTime;

            if (useOrbitInertia)
            {
                Vector2 frameVel = new Vector2(yawVel, pitchVel);

                // Only start inertia if you're spinning fast enough
                if (frameVel.magnitude >= inertiaThreshold)
                {
                    orbitVelocity = frameVel * inertiaMultiplier;
                }
                else
                {
                    // Slow drag = no carry
                    orbitVelocity = Vector2.zero;
                }
            }
        }
        else if (useOrbitInertia && allowMiddleMouseOrbit && orbitVelocity.sqrMagnitude > 0.0001f)
        {
            // Apply carried rotation
            yaw += orbitVelocity.x * Time.deltaTime;
            pitch += orbitVelocity.y * Time.deltaTime;

            // Exponential decay
            float dampingFactor = Mathf.Exp(-inertiaDamping * Time.deltaTime);
            orbitVelocity *= dampingFactor;

            // Hard-stop when tiny
            if (orbitVelocity.sqrMagnitude < 0.0001f)
                orbitVelocity = Vector2.zero;
        }

        // Clamp pitch once, after all rotation inputs
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // =========================
        // 3) Q/E + SCROLL ZOOM
        // =========================

        // Q/E -> adjust target distance
        float qeZoomDir = 0f;
        if (kb.qKey.isPressed) qeZoomDir += 1f;  // Q = zoom in
        if (kb.eKey.isPressed) qeZoomDir -= 1f;  // E = zoom out

        if (Mathf.Abs(qeZoomDir) > 0.01f)
        {
            targetDistance -= qeZoomDir * qeZoomSpeed * dt;
        }

        // Scroll wheel -> also adjust target distance
        if (allowScrollZoom && hasMouse)
        {
            float scrollY = mouse.scroll.ReadValue().y; // positive = scroll up
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                // No dt here; scroll is event-based, not per-second
                targetDistance -= scrollY * scrollZoomSpeed;
            }
        }

        targetDistance = Mathf.Clamp(targetDistance, CloseMin, FarMax);

        // Smooth actual Distance toward targetDistance
        float t = 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime); // smooth-ish
        Distance = Mathf.Lerp(Distance, targetDistance, t);

        // =========================
        // 4) APPLY TRANSFORM
        // =========================
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;

        Vector3 pos = target
            ? target.position - fwd * Distance
            : -(fwd * Distance);

        transform.SetPositionAndRotation(pos, rot);
    }
}
