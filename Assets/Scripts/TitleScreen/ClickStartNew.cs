using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClickStartNew : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool disableButtonAfterClick = true;

    [Header("Title Letters (3D Objects)")]
    [SerializeField] private List<Transform> titleLetters = new List<Transform>();
    [SerializeField] private float titleWaveDelay = 0.08f;
    [SerializeField] private float titleRiseAmount = 3f;
    [SerializeField] private float titleRiseDuration = 0.6f;
    [SerializeField] private AnimationCurve titleRiseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool disableLettersAfterRise = false;

    [Header("Menu Buttons (UI RectTransforms)")]
    [SerializeField] private List<RectTransform> menuButtons = new List<RectTransform>();
    [SerializeField] private float buttonWaveDelay = 0.06f;
    [SerializeField] private float buttonMoveDistance = 900f;
    [SerializeField] private float buttonMoveDuration = 0.45f;
    [SerializeField] private AnimationCurve buttonMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool disableButtonsAfterMove = true;

    [Header("Camera Movement")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform cameraTargetTransform;
    [SerializeField] private bool useTransformTarget = true;

    [Tooltip("Used only if Use Transform Target is false.")]
    [SerializeField] private Vector3 cameraTargetPositionOffset = new Vector3(0f, 0f, -2f);

    [Tooltip("Used only if Use Transform Target is false.")]
    [SerializeField] private Vector3 cameraTargetRotationEulerOffset = new Vector3(0f, 20f, 0f);

    [SerializeField] private float cameraMoveDuration = 1.25f;
    [SerializeField] private AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Timing")]
    [SerializeField] private float cameraStartDelay = 0.15f;
    [SerializeField] private float seedMenuTriggerDelay = 0.2f;

    [Header("Optional")]
    [SerializeField] private GameObject seedSelectionMenu;
    [SerializeField] private Button startButton;
    [SerializeField] private MonoBehaviour CameraMovement;

    private bool hasPlayed = false;

    private void Start()
    {
        if (playOnStart)
        {
            StartSequence();
        }
    }

    public void StartSequence()
    {
        if (hasPlayed) return;
        hasPlayed = true;

        if (disableButtonAfterClick && startButton != null)
        {
            startButton.interactable = false;
        }

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // 1. Title letters wave upward (left to right)
        for (int i = 0; i < titleLetters.Count; i++)
        {
            if (titleLetters[i] != null)
            {
                StartCoroutine(AnimateTitleLetter(titleLetters[i]));
            }

            yield return new WaitForSeconds(titleWaveDelay);
        }

        // 2. Buttons move left in a top-down wave
        for (int i = 0; i < menuButtons.Count; i++)
        {
            if (menuButtons[i] != null)
            {
                StartCoroutine(AnimateUIButton(menuButtons[i]));
            }

            yield return new WaitForSeconds(buttonWaveDelay);
        }

        // 3. Move camera after a short delay
        yield return new WaitForSeconds(cameraStartDelay);

        if (targetCamera != null)
        {
            yield return StartCoroutine(AnimateCamera());
        }

        // Optional: turn on seed menu after everything finishes
        yield return new WaitForSeconds(seedMenuTriggerDelay);

        if (seedSelectionMenu != null)
        {
            seedSelectionMenu.SetActive(true);
        }

        if (seedSelectionMenu != null)
        {
            CameraMovement.enabled = true;
        }
    }

    private IEnumerator AnimateTitleLetter(Transform letter)
    {
        Vector3 startPos = letter.position;
        Vector3 endPos = startPos + Vector3.up * titleRiseAmount;

        float elapsed = 0f;

        while (elapsed < titleRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / titleRiseDuration);
            float curvedT = titleRiseCurve.Evaluate(t);

            letter.position = Vector3.LerpUnclamped(startPos, endPos, curvedT);
            yield return null;
        }

        letter.position = endPos;

        if (disableLettersAfterRise)
        {
            letter.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateUIButton(RectTransform button)
    {
        Vector2 startPos = button.anchoredPosition;
        Vector2 endPos = startPos + Vector2.left * buttonMoveDistance;

        float elapsed = 0f;

        while (elapsed < buttonMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / buttonMoveDuration);
            float curvedT = buttonMoveCurve.Evaluate(t);

            button.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curvedT);
            yield return null;
        }

        button.anchoredPosition = endPos;

        if (disableButtonsAfterMove)
        {
            button.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateCamera()
    {
        Transform camTransform = targetCamera.transform;

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;

        Vector3 endPos;
        Quaternion endRot;

        if (useTransformTarget && cameraTargetTransform != null)
        {
            endPos = cameraTargetTransform.position;
            endRot = cameraTargetTransform.rotation;
        }
        else
        {
            endPos = startPos + cameraTargetPositionOffset;
            endRot = Quaternion.Euler(camTransform.eulerAngles + cameraTargetRotationEulerOffset);
        }

        float elapsed = 0f;

        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraMoveDuration);
            float curvedT = cameraMoveCurve.Evaluate(t);

            camTransform.position = Vector3.LerpUnclamped(startPos, endPos, curvedT);
            camTransform.rotation = Quaternion.SlerpUnclamped(startRot, endRot, curvedT);

            yield return null;
        }

        camTransform.position = endPos;
        camTransform.rotation = endRot;
    }
}