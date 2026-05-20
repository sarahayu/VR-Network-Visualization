using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class MicHintEvent : MonoBehaviour
{
    private InputAction micPress;

    [Header("Overlay References")]
    [SerializeField]
    private CanvasGroup overlayCanvasGroup;

    [SerializeField]
    private RectTransform edgeGlowImage;

    [Header("Animation")]
    [SerializeField]
    private float fadeSpeed = 8f;

    [SerializeField]
    private float rotationSpeed = 20f;

    [SerializeField]
    private float pulseSpeed = 3f;

    [SerializeField]
    private float pulseAmount = 0.03f;


    [Header("Test Mode")]
    [SerializeField]
    private bool enableKeyboardTest = true;

    [SerializeField]
    private KeyCode testKey = KeyCode.Space;

    private bool showOverlay;

    [SerializeField]
    private AudioSource endingSound;

    void OnEnable()
    {
        micPress = new InputAction(
            name: "CommandPress",
            type: InputActionType.Button,
            binding: "<XRController>{LeftHand}/primaryButton"
        );

        micPress.Enable();
    }

    void Start()
    {
        if (overlayCanvasGroup == null)
        {
            Debug.LogError("overlayCanvasGroup is not assigned.");
            return;
        }

        if (edgeGlowImage == null)
        {
            Debug.LogError("edgeGlowImage is not assigned.");
            return;
        }

        if (endingSound != null)
        {
            endingSound.playOnAwake = false;
            endingSound.Stop();
        }

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;

        edgeGlowImage.localScale = Vector3.one;
    }

    void OnDisable()
    {
        micPress?.Disable();
        micPress?.Dispose();
        micPress = null;
    }

    void Update()
    {
        bool vrPressedThisFrame = micPress != null && micPress.WasPressedThisFrame();
        bool vrReleasedThisFrame = micPress != null && micPress.WasReleasedThisFrame();

        bool keyboardPressedThisFrame = enableKeyboardTest && Input.GetKeyDown(testKey);
        bool keyboardReleasedThisFrame = enableKeyboardTest && Input.GetKeyUp(testKey);

        if (keyboardPressedThisFrame)
        {
            Debug.Log("SPACE PRESSED");
        }

        if (keyboardReleasedThisFrame)
        {
            Debug.Log("SPACE RELEASED");
        }

        if (vrPressedThisFrame || keyboardPressedThisFrame)
        {
            Debug.Log("SHOW OVERLAY");
            showOverlay = true;
            VibrateLeftController(0.4f, 0.16f); //  Vibration feedback when the mic is pressed

        }

        if (vrReleasedThisFrame || keyboardReleasedThisFrame)
        {
            Debug.Log("HIDE OVERLAY");
            showOverlay = false;
            if (endingSound != null)
            {
                endingSound.Play();
                // Should I do Vibration feedback when the mic is released? Maybe a different pattern or amplitude
            }
            VibrateLeftController(0.3f, 0.08f);


        }

        AnimateOverlay();
    }

    private void AnimateOverlay()
    {
        float targetAlpha = showOverlay ? 1f : 0f;

        overlayCanvasGroup.alpha = Mathf.Lerp(
            overlayCanvasGroup.alpha,
            targetAlpha,
            Time.deltaTime * fadeSpeed
        );

        if (showOverlay)
        {
            edgeGlowImage.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            edgeGlowImage.localScale = new Vector3(pulse, pulse, 1f);
        }
        else
        {
            edgeGlowImage.localScale = Vector3.Lerp(
                edgeGlowImage.localScale,
                Vector3.one,
                Time.deltaTime * fadeSpeed
            );
        }
    }

    private void VibrateLeftController(float amplitude, float duration)
    {
        UnityEngine.XR.InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (leftHand.isValid)
        {
            leftHand.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
