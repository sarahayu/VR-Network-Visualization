using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class MicHintEvent : MonoBehaviour
{
    private struct RendererFadeState
    {
        public Renderer Renderer;
        public string ColorProperty;
        public Color OriginalColor;
    }

    private InputAction micPress;
    private RendererFadeState[] fadeRenderers;
    private float currentAlpha;
    private float targetAlpha;
    private Vector3 originalRootScale;

    [Header("Overlay Reference")]
    [SerializeField]
    private GameObject overlayRoot;

    [Header("Animation")]
    [SerializeField]
    private float fadeSpeed = 8f;

    [SerializeField]
    private float minFadeScale = 0.88f;

    [Header("Test Mode")]
    [SerializeField]
    private bool enableKeyboardTest = true;

    [SerializeField]
    private KeyCode testKey = KeyCode.Space;

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
        if (overlayRoot == null)
        {
            Debug.LogError("overlayRoot is not assigned.");
            return;
        }

        if (endingSound != null)
        {
            endingSound.playOnAwake = false;
            endingSound.Stop();
        }

        originalRootScale = overlayRoot.transform.localScale;
        CacheFadeRenderers();
        SetOverlayFade(0f);
        overlayRoot.SetActive(false);
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
            ShowOverlay();
            VibrateLeftController(0.4f, 0.16f); //  Vibration feedback when the mic is pressed

        }

        if (vrReleasedThisFrame || keyboardReleasedThisFrame)
        {
            Debug.Log("HIDE OVERLAY");
            HideOverlay();
            if (endingSound != null)
            {
                endingSound.Play();
                // Should I do Vibration feedback when the mic is released? Maybe a different pattern or amplitude
            }
            VibrateLeftController(0.3f, 0.08f);


        }

        AnimateOverlay();
    }

    private void ShowOverlay()
    {
        overlayRoot.SetActive(true);
        targetAlpha = 1f;
    }

    private void HideOverlay()
    {
        targetAlpha = 0f;
    }

    private void AnimateOverlay()
    {
        if (Mathf.Approximately(currentAlpha, targetAlpha))
        {
            return;
        }

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            Time.deltaTime * fadeSpeed
        );

        SetOverlayFade(currentAlpha);

        if (Mathf.Approximately(currentAlpha, 0f) && Mathf.Approximately(targetAlpha, 0f))
        {
            overlayRoot.SetActive(false);
        }
    }

    private void CacheFadeRenderers()
    {
        Renderer[] renderers = overlayRoot.GetComponentsInChildren<Renderer>(true);
        fadeRenderers = new RendererFadeState[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            string colorProperty = material != null && material.HasProperty("_BaseColor")
                ? "_BaseColor"
                : "_Color";

            Color originalColor = Color.white;
            if (material != null && material.HasProperty(colorProperty))
            {
                originalColor = material.GetColor(colorProperty);
            }

            fadeRenderers[i] = new RendererFadeState
            {
                Renderer = renderers[i],
                ColorProperty = colorProperty,
                OriginalColor = originalColor
            };
        }
    }

    private void SetOverlayFade(float alpha)
    {
        currentAlpha = alpha;
        float scale = Mathf.Lerp(minFadeScale, 1f, alpha);
        overlayRoot.transform.localScale = originalRootScale * scale;

        if (fadeRenderers == null)
        {
            return;
        }

        foreach (RendererFadeState fadeRenderer in fadeRenderers)
        {
            if (fadeRenderer.Renderer == null)
            {
                continue;
            }

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            Color color = fadeRenderer.OriginalColor;
            color.a *= alpha;

            fadeRenderer.Renderer.GetPropertyBlock(props);
            props.SetColor(fadeRenderer.ColorProperty, color);
            fadeRenderer.Renderer.SetPropertyBlock(props);
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
